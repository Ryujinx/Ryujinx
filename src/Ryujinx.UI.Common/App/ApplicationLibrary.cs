using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Loaders.Npdm;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using Path = System.IO.Path;
using TimeSpan = System.TimeSpan;

namespace Ryujinx.UI.App.Common
{
    using MPCNcas = Tuple<Nca, Nca, Nca>;

    public class ApplicationLibrary
    {
        public event EventHandler<ApplicationAddedEventArgs> ApplicationAdded;
        public event EventHandler<ApplicationCountUpdatedEventArgs> ApplicationCountUpdated;

        private readonly byte[] _nspIcon;
        private readonly byte[] _xciIcon;
        private readonly byte[] _ncaIcon;
        private readonly byte[] _nroIcon;
        private readonly byte[] _nsoIcon;

        private readonly VirtualFileSystem _virtualFileSystem;
        private Language _desiredTitleLanguage;
        private CancellationTokenSource _cancellationToken;

        private static readonly ApplicationJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());
        private static readonly TitleUpdateMetadataJsonSerializerContext _titleSerializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public ApplicationLibrary(VirtualFileSystem virtualFileSystem)
        {
            _virtualFileSystem = virtualFileSystem;

            _nspIcon = GetResourceBytes("Ryujinx.UI.Common.Resources.Icon_NSP.png");
            _xciIcon = GetResourceBytes("Ryujinx.UI.Common.Resources.Icon_XCI.png");
            _ncaIcon = GetResourceBytes("Ryujinx.UI.Common.Resources.Icon_NCA.png");
            _nroIcon = GetResourceBytes("Ryujinx.UI.Common.Resources.Icon_NRO.png");
            _nsoIcon = GetResourceBytes("Ryujinx.UI.Common.Resources.Icon_NSO.png");
        }

        private static byte[] GetResourceBytes(string resourceName)
        {
            Stream resourceStream = Assembly.GetCallingAssembly().GetManifestResourceStream(resourceName);
            byte[] resourceByteArray = new byte[resourceStream.Length];

            resourceStream.Read(resourceByteArray);

            return resourceByteArray;
        }

        private ApplicationData GetApplicationFromExeFs(PartitionFileSystem pfs, string filePath)
        {
            ApplicationData data = new()
            {
                Icon = _nspIcon,
            };

            using UniqueRef<IFile> npdmFile = new();

            try
            {
                Result result = pfs.OpenFile(ref npdmFile.Ref, "/main.npdm".ToU8Span(), OpenMode.Read);

                if (ResultFs.PathNotFound.Includes(result))
                {
                    Npdm npdm = new(npdmFile.Get.AsStream());

                    data.TitleName = npdm.TitleName;
                    data.TitleId = npdm.Aci0.TitleId.ToString("x16");
                }

                return data;
            }
            catch (Exception exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. File: '{filePath}' Error: {exception}");

                return null;
            }
        }

        private ApplicationData GetApplicationFromNsp(PartitionFileSystem pfs, string filePath)
        {
            bool isExeFs = false;

            // If the NSP doesn't have a main NCA, decrement the number of applications found and then continue to the next application.
            bool hasMainNca = false;

            foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*"))
            {
                if (Path.GetExtension(fileEntry.FullPath)?.ToLower() == ".nca")
                {
                    using UniqueRef<IFile> ncaFile = new();

                    pfs.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                    Nca nca = new(_virtualFileSystem.KeySet, ncaFile.Get.AsStorage());
                    int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                    // Some main NCAs don't have a data partition, so check if the partition exists before opening it
                    if (nca.Header.ContentType == NcaContentType.Program &&
                        !(nca.SectionExists(NcaSectionType.Data) &&
                          nca.Header.GetFsHeader(dataIndex).IsPatchSection()))
                    {
                        hasMainNca = true;

                        break;
                    }
                }
                else if (Path.GetFileNameWithoutExtension(fileEntry.FullPath) == "main")
                {
                    isExeFs = true;
                }
            }

            if (hasMainNca)
            {
                List<ApplicationData> applications = GetApplicationsFromPfs(pfs, filePath);

                switch (applications.Count)
                {
                    case 1:
                        return applications[0];
                    case >= 1:
                        Logger.Warning?.Print(LogClass.Application, $"File '{filePath}' contains more applications than expected: {applications.Count}");
                        return applications[0];
                    default:
                        return null;
                }
            }

            if (isExeFs)
            {
                return GetApplicationFromExeFs(pfs, filePath);
            }

            return null;
        }

        private List<ApplicationData> GetApplicationsFromPfs(IFileSystem pfs, string filePath)
        {
            var applications = new List<ApplicationData>();
            string extension = Path.GetExtension(filePath).ToLower();

            foreach ((ulong titleId, (Nca mainNca, Nca patchNca, Nca controlNca)) in pfs.GetApplicationData(_virtualFileSystem, 0))
            {
                ApplicationData applicationData = new()
                {
                    TitleId = titleId.ToString("x16"),
                };

                try
                {
                    BlitStruct<ApplicationControlProperty> controlHolder = new(1);

                    IFileSystem controlFs = controlNca?.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                    // Check if there is an update available.
                    // TODO: Take gamecart updates into account as well
                    if (IsUpdateApplied(mainNca, out IFileSystem updatedControlFs))
                    {
                        // Replace the original ControlFs by the updated one.
                        controlFs = updatedControlFs;
                    }

                    ReadControlData(controlFs, controlHolder.ByteSpan);

                    GetApplicationInformation(ref controlHolder.Value, ref applicationData);

                    // Read the icon from the ControlFS and store it as a byte array
                    try
                    {
                        using UniqueRef<IFile> icon = new();

                        controlFs.OpenFile(ref icon.Ref, $"/icon_{_desiredTitleLanguage}.dat".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        using MemoryStream stream = new();

                        icon.Get.AsStream().CopyTo(stream);
                        applicationData.Icon = stream.ToArray();
                    }
                    catch (HorizonResultException)
                    {
                        foreach (DirectoryEntryEx entry in controlFs.EnumerateEntries("/", "*"))
                        {
                            if (entry.Name == "control.nacp")
                            {
                                continue;
                            }

                            using var icon = new UniqueRef<IFile>();

                            controlFs.OpenFile(ref icon.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                            using MemoryStream stream = new();

                            icon.Get.AsStream().CopyTo(stream);
                            applicationData.Icon = stream.ToArray();

                            if (applicationData.Icon != null)
                            {
                                break;
                            }
                        }

                        applicationData.Icon ??= extension == ".xci" ? _xciIcon : _nspIcon;
                    }

                    applicationData.ControlHolder = controlHolder;

                    applications.Add(applicationData);
                }
                catch (MissingKeyException exception)
                {
                    applicationData.Icon = extension == ".xci" ? _xciIcon : _nspIcon;

                    Logger.Warning?.Print(LogClass.Application, $"Your key set is missing a key with the name: {exception.Name}");
                }
                catch (InvalidDataException)
                {
                    applicationData.Icon = extension == ".xci" ? _xciIcon : _nspIcon;

                    Logger.Warning?.Print(LogClass.Application, $"The header key is incorrect or missing and therefore the NCA header content type check has failed. Errored File: {filePath}");
                }
                catch (Exception exception)
                {
                    Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. File: '{filePath}' Error: {exception}");
                }
            }

            return applications;
        }

        private bool TryGetApplicationsFromFile(string applicationPath, out List<ApplicationData> applications)
        {
            applications = new List<ApplicationData>();

            long fileSize = new FileInfo(applicationPath).Length;

            BlitStruct<ApplicationControlProperty> controlHolder = new(1);

            try
            {
                string extension = Path.GetExtension(applicationPath).ToLower();

                using FileStream file = new(applicationPath, FileMode.Open, FileAccess.Read);

                switch (extension)
                {
                    case ".xci":
                    {
                        Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());

                        applications = GetApplicationsFromPfs(xci.OpenPartition(XciPartitionType.Secure), applicationPath);

                        if (applications.Count == 0)
                        {
                            return false;
                        }

                        break;
                    }
                    case ".nsp":
                    case ".pfs0":
                        var pfs = new PartitionFileSystem();
                        pfs.Initialize(file.AsStorage()).ThrowIfFailure();

                        ApplicationData result = GetApplicationFromNsp(pfs, applicationPath);

                        if (result == null)
                        {
                            return false;
                        }

                        applications.Add(result);

                        break;
                    case ".nro":
                    {
                        BinaryReader reader = new(file);
                        ApplicationData application = new();

                        byte[] Read(long position, int size)
                        {
                            file.Seek(position, SeekOrigin.Begin);

                            return reader.ReadBytes(size);
                        }

                        try
                        {
                            file.Seek(24, SeekOrigin.Begin);

                            int assetOffset = reader.ReadInt32();

                            if (Encoding.ASCII.GetString(Read(assetOffset, 4)) == "ASET")
                            {
                                byte[] iconSectionInfo = Read(assetOffset + 8, 0x10);

                                long iconOffset = BitConverter.ToInt64(iconSectionInfo, 0);
                                long iconSize = BitConverter.ToInt64(iconSectionInfo, 8);

                                ulong nacpOffset = reader.ReadUInt64();
                                ulong nacpSize = reader.ReadUInt64();

                                // Reads and stores game icon as byte array
                                if (iconSize > 0)
                                {
                                    application.Icon = Read(assetOffset + iconOffset, (int)iconSize);
                                }
                                else
                                {
                                    application.Icon = _nroIcon;
                                }

                                // Read the NACP data
                                Read(assetOffset + (int)nacpOffset, (int)nacpSize).AsSpan().CopyTo(controlHolder.ByteSpan);

                                GetApplicationInformation(ref controlHolder.Value, ref application);
                            }
                            else
                            {
                                application.Icon = _nroIcon;
                                application.TitleName = Path.GetFileNameWithoutExtension(applicationPath);
                            }

                            application.ControlHolder = controlHolder;
                            applications.Add(application);
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");

                            return false;
                        }

                        break;
                    }
                    case ".nca":
                    {
                        try
                        {
                            ApplicationData application = new();

                            Nca nca = new(_virtualFileSystem.KeySet, new FileStream(applicationPath, FileMode.Open, FileAccess.Read).AsStorage());
                            int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                            if (nca.Header.ContentType != NcaContentType.Program || (nca.SectionExists(NcaSectionType.Data) && nca.Header.GetFsHeader(dataIndex).IsPatchSection()))
                            {
                                return false;
                            }

                            application.Icon = _ncaIcon;
                            application.TitleName = Path.GetFileNameWithoutExtension(applicationPath);
                            application.ControlHolder = controlHolder;

                            applications.Add(application);
                        }
                        catch (InvalidDataException)
                        {
                            Logger.Warning?.Print(LogClass.Application, $"The NCA header content type check has failed. This is usually because the header key is incorrect or missing. Errored File: {applicationPath}");
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");

                            return false;
                        }

                        break;
                    }
                    // If its an NSO we just set defaults
                    case ".nso":
                    {
                        ApplicationData application = new()
                        {
                            Icon = _nsoIcon,
                            TitleName = Path.GetFileNameWithoutExtension(applicationPath),
                        };

                        applications.Add(application);
                        break;
                    }
                }
            }
            catch (IOException exception)
            {
                Logger.Warning?.Print(LogClass.Application, exception.Message);

                return false;
            }

            foreach (var data in applications)
            {
                ApplicationMetadata appMetadata = LoadAndSaveMetaData(data.TitleId, appMetadata =>
                {
                    appMetadata.Title = data.TitleName;

                    // Only do the migration if time_played has a value and timespan_played hasn't been updated yet.
                    if (appMetadata.TimePlayedOld != default && appMetadata.TimePlayed == TimeSpan.Zero)
                    {
                        appMetadata.TimePlayed = TimeSpan.FromSeconds(appMetadata.TimePlayedOld);
                        appMetadata.TimePlayedOld = default;
                    }

                    // Only do the migration if last_played has a value and last_played_utc doesn't exist yet.
                    if (appMetadata.LastPlayedOld != default && !appMetadata.LastPlayed.HasValue)
                    {
                        // Migrate from string-based last_played to DateTime-based last_played_utc.
                        if (DateTime.TryParse(appMetadata.LastPlayedOld, out DateTime lastPlayedOldParsed))
                        {
                            appMetadata.LastPlayed = lastPlayedOldParsed;

                            // Migration successful: deleting last_played from the metadata file.
                            appMetadata.LastPlayedOld = default;
                        }

                    }
                });

                data.Favorite = appMetadata.Favorite;
                data.TimePlayed = appMetadata.TimePlayed;
                data.LastPlayed = appMetadata.LastPlayed;
                data.FileExtension = Path.GetExtension(applicationPath).TrimStart('.').ToUpper();
                data.FileSize = fileSize;
                data.Path = applicationPath;
            }

            return true;
        }

        public void CancelLoading()
        {
            _cancellationToken?.Cancel();
        }

        public static void ReadControlData(IFileSystem controlFs, Span<byte> outProperty)
        {
            using UniqueRef<IFile> controlFile = new();

            controlFs.OpenFile(ref controlFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();
            controlFile.Get.Read(out _, 0, outProperty, ReadOption.None).ThrowIfFailure();
        }

        public void LoadApplications(List<string> appDirs, Language desiredTitleLanguage)
        {
            int numApplicationsFound = 0;
            int numApplicationsLoaded = 0;

            _desiredTitleLanguage = desiredTitleLanguage;

            _cancellationToken = new CancellationTokenSource();

            // Builds the applications list with paths to found applications
            List<string> applicationPaths = new();

            try
            {
                foreach (string appDir in appDirs)
                {
                    if (_cancellationToken.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!Directory.Exists(appDir))
                    {
                        Logger.Warning?.Print(LogClass.Application, $"The \"game_dirs\" section in \"{ReleaseInformation.ConfigName}\" contains an invalid directory: \"{appDir}\"");

                        continue;
                    }

                    try
                    {
                        IEnumerable<string> files = Directory.EnumerateFiles(appDir, "*", SearchOption.AllDirectories).Where(file =>
                        {
                            return
                            (Path.GetExtension(file).ToLower() is ".nsp" && ConfigurationState.Instance.UI.ShownFileTypes.NSP.Value) ||
                            (Path.GetExtension(file).ToLower() is ".pfs0" && ConfigurationState.Instance.UI.ShownFileTypes.PFS0.Value) ||
                            (Path.GetExtension(file).ToLower() is ".xci" && ConfigurationState.Instance.UI.ShownFileTypes.XCI.Value) ||
                            (Path.GetExtension(file).ToLower() is ".nca" && ConfigurationState.Instance.UI.ShownFileTypes.NCA.Value) ||
                            (Path.GetExtension(file).ToLower() is ".nro" && ConfigurationState.Instance.UI.ShownFileTypes.NRO.Value) ||
                            (Path.GetExtension(file).ToLower() is ".nso" && ConfigurationState.Instance.UI.ShownFileTypes.NSO.Value);
                        });

                        foreach (string app in files)
                        {
                            if (_cancellationToken.Token.IsCancellationRequested)
                            {
                                return;
                            }

                            var fileInfo = new FileInfo(app);
                            string extension = fileInfo.Extension.ToLower();

                            if (!fileInfo.Attributes.HasFlag(FileAttributes.Hidden) && extension is ".nsp" or ".pfs0" or ".xci" or ".nca" or ".nro" or ".nso")
                            {
                                var fullPath = fileInfo.ResolveLinkTarget(true)?.FullName ?? fileInfo.FullName;
                                applicationPaths.Add(fullPath);
                                numApplicationsFound++;
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Failed to get access to directory: \"{appDir}\"");
                    }
                }

                // Loops through applications list, creating a struct and then firing an event containing the struct for each application
                foreach (string applicationPath in applicationPaths)
                {
                    if (_cancellationToken.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (TryGetApplicationsFromFile(applicationPath, out List<ApplicationData> applications))
                    {
                        foreach (var application in applications)
                        {
                            OnApplicationAdded(new ApplicationAddedEventArgs
                            {
                                AppData = application,
                            });
                        }

                        if (applications.Count > 1)
                        {
                            numApplicationsFound += applications.Count - 1;
                        }

                        numApplicationsLoaded += applications.Count;
                    }
                    else
                    {
                        numApplicationsFound--;
                    }

                    OnApplicationCountUpdated(new ApplicationCountUpdatedEventArgs
                    {
                        NumAppsFound = numApplicationsFound,
                        NumAppsLoaded = numApplicationsLoaded,
                    });
                }

                OnApplicationCountUpdated(new ApplicationCountUpdatedEventArgs
                {
                    NumAppsFound = numApplicationsFound,
                    NumAppsLoaded = numApplicationsLoaded,
                });
            }
            finally
            {
                _cancellationToken.Dispose();
                _cancellationToken = null;
            }
        }

        protected void OnApplicationAdded(ApplicationAddedEventArgs e)
        {
            ApplicationAdded?.Invoke(null, e);
        }

        protected void OnApplicationCountUpdated(ApplicationCountUpdatedEventArgs e)
        {
            ApplicationCountUpdated?.Invoke(null, e);
        }

        public static ApplicationMetadata LoadAndSaveMetaData(string titleId, Action<ApplicationMetadata> modifyFunction = null)
        {
            string metadataFolder = Path.Combine(AppDataManager.GamesDirPath, titleId, "gui");
            string metadataFile = Path.Combine(metadataFolder, "metadata.json");

            ApplicationMetadata appMetadata;

            if (!File.Exists(metadataFile))
            {
                Directory.CreateDirectory(metadataFolder);

                appMetadata = new ApplicationMetadata();

                JsonHelper.SerializeToFile(metadataFile, appMetadata, _serializerContext.ApplicationMetadata);
            }

            try
            {
                appMetadata = JsonHelper.DeserializeFromFile(metadataFile, _serializerContext.ApplicationMetadata);
            }
            catch (JsonException)
            {
                Logger.Warning?.Print(LogClass.Application, $"Failed to parse metadata json for {titleId}. Loading defaults.");

                appMetadata = new ApplicationMetadata();
            }

            if (modifyFunction != null)
            {
                modifyFunction(appMetadata);

                JsonHelper.SerializeToFile(metadataFile, appMetadata, _serializerContext.ApplicationMetadata);
            }

            return appMetadata;
        }

        public byte[] GetApplicationIcon(string applicationPath, Language desiredTitleLanguage, ulong titleId)
        {
            byte[] applicationIcon = null;

            if (titleId == 0)
            {
                if (Directory.Exists(applicationPath))
                {
                    return _ncaIcon;
                }

                return Path.GetExtension(applicationPath).ToLower() switch
                {
                    ".nsp" => _nspIcon,
                    ".pfs0" => _nspIcon,
                    ".xci" => _xciIcon,
                    ".nso" => _nsoIcon,
                    ".nro" => _nroIcon,
                    ".nca" => _ncaIcon,
                    _ => _ncaIcon,
                };
            }

            try
            {
                // Look for icon only if applicationPath is not a directory
                if (!Directory.Exists(applicationPath))
                {
                    string extension = Path.GetExtension(applicationPath).ToLower();

                    using FileStream file = new(applicationPath, FileMode.Open, FileAccess.Read);

                    if (extension == ".nsp" || extension == ".pfs0" || extension == ".xci")
                    {
                        try
                        {
                            IFileSystem pfs;

                            bool isExeFs = false;

                            if (extension == ".xci")
                            {
                                Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());

                                pfs = xci.OpenPartition(XciPartitionType.Secure);
                            }
                            else
                            {
                                var pfsTemp = new PartitionFileSystem();
                                pfsTemp.Initialize(file.AsStorage()).ThrowIfFailure();
                                pfs = pfsTemp;

                                foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*"))
                                {
                                    if (Path.GetFileNameWithoutExtension(fileEntry.FullPath) == "main")
                                    {
                                        isExeFs = true;
                                    }
                                }
                            }

                            if (isExeFs)
                            {
                                applicationIcon = _nspIcon;
                            }
                            else
                            {
                                // Store the ControlFS in variable called controlFs
                                Dictionary<ulong, MPCNcas> programs = pfs.GetApplicationData(_virtualFileSystem, 0);
                                IFileSystem controlFs = null;

                                if (programs.ContainsKey(titleId))
                                {
                                    if (programs[titleId].Item3 != null)
                                    {
                                        controlFs = programs[titleId].Item3.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                                    }
                                }

                                // Read the icon from the ControlFS and store it as a byte array
                                try
                                {
                                    using var icon = new UniqueRef<IFile>();

                                    controlFs.OpenFile(ref icon.Ref, $"/icon_{desiredTitleLanguage}.dat".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                    using MemoryStream stream = new();

                                    icon.Get.AsStream().CopyTo(stream);
                                    applicationIcon = stream.ToArray();
                                }
                                catch (HorizonResultException)
                                {
                                    foreach (DirectoryEntryEx entry in controlFs.EnumerateEntries("/", "*"))
                                    {
                                        if (entry.Name == "control.nacp")
                                        {
                                            continue;
                                        }

                                        using var icon = new UniqueRef<IFile>();

                                        controlFs.OpenFile(ref icon.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                        using (MemoryStream stream = new())
                                        {
                                            icon.Get.AsStream().CopyTo(stream);
                                            applicationIcon = stream.ToArray();
                                        }

                                        break;
                                    }

                                    applicationIcon ??= extension == ".xci" ? _xciIcon : _nspIcon;
                                }
                            }
                        }
                        catch (MissingKeyException)
                        {
                            applicationIcon = extension == ".xci" ? _xciIcon : _nspIcon;
                        }
                        catch (InvalidDataException)
                        {
                            applicationIcon = extension == ".xci" ? _xciIcon : _nspIcon;
                        }
                        catch (Exception exception)
                        {
                            Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. File: '{applicationPath}' Error: {exception}");
                        }
                    }
                    else if (extension == ".nro")
                    {
                        BinaryReader reader = new(file);

                        byte[] Read(long position, int size)
                        {
                            file.Seek(position, SeekOrigin.Begin);

                            return reader.ReadBytes(size);
                        }

                        try
                        {
                            file.Seek(24, SeekOrigin.Begin);

                            int assetOffset = reader.ReadInt32();

                            if (Encoding.ASCII.GetString(Read(assetOffset, 4)) == "ASET")
                            {
                                byte[] iconSectionInfo = Read(assetOffset + 8, 0x10);

                                long iconOffset = BitConverter.ToInt64(iconSectionInfo, 0);
                                long iconSize = BitConverter.ToInt64(iconSectionInfo, 8);

                                // Reads and stores game icon as byte array
                                if (iconSize > 0)
                                {
                                    applicationIcon = Read(assetOffset + iconOffset, (int)iconSize);
                                }
                                else
                                {
                                    applicationIcon = _nroIcon;
                                }
                            }
                            else
                            {
                                applicationIcon = _nroIcon;
                            }
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");
                        }
                    }
                    else if (extension == ".nca")
                    {
                        applicationIcon = _ncaIcon;
                    }
                    // If its an NSO we just set defaults
                    else if (extension == ".nso")
                    {
                        applicationIcon = _nsoIcon;
                    }
                }
            }
            catch (Exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Could not retrieve a valid icon for the app. Default icon will be used. Errored File: {applicationPath}");
            }

            return applicationIcon ?? _ncaIcon;
        }

        private void GetApplicationInformation(ref ApplicationControlProperty controlData, ref ApplicationData data)
        {
            _ = Enum.TryParse(_desiredTitleLanguage.ToString(), out TitleLanguage desiredTitleLanguage);

            if (controlData.Title.ItemsRo.Length > (int)desiredTitleLanguage)
            {
                data.TitleName = controlData.Title[(int)desiredTitleLanguage].NameString.ToString();
                data.Developer = controlData.Title[(int)desiredTitleLanguage].PublisherString.ToString();
            }
            else
            {
                data.TitleName = null;
                data.Developer = null;
            }

            if (string.IsNullOrWhiteSpace(data.TitleName))
            {
                foreach (ref readonly var controlTitle in controlData.Title.ItemsRo)
                {
                    if (!controlTitle.NameString.IsEmpty())
                    {
                        data.TitleName = controlTitle.NameString.ToString();

                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(data.Developer))
            {
                foreach (ref readonly var controlTitle in controlData.Title.ItemsRo)
                {
                    if (!controlTitle.PublisherString.IsEmpty())
                    {
                        data.Developer = controlTitle.PublisherString.ToString();

                        break;
                    }
                }
            }

            if (controlData.PresenceGroupId != 0)
            {
                data.TitleId = controlData.PresenceGroupId.ToString("x16");
            }
            else if (controlData.SaveDataOwnerId != 0)
            {
                data.TitleId = controlData.SaveDataOwnerId.ToString();
            }
            else if (controlData.AddOnContentBaseId != 0)
            {
                data.TitleId = (controlData.AddOnContentBaseId - 0x1000).ToString("x16");
            }

            data.Version = controlData.DisplayVersionString.ToString();
        }

        private bool IsUpdateApplied(Nca mainNca, out IFileSystem updatedControlFs)
        {
            updatedControlFs = null;

            string updatePath = "(unknown)";

            try
            {
                (Nca patchNca, Nca controlNca) = mainNca.GetUpdateData(_virtualFileSystem, 0, out updatePath);

                if (patchNca != null && controlNca != null)
                {
                    updatedControlFs = controlNca?.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                    return true;
                }
            }
            catch (InvalidDataException)
            {
                Logger.Warning?.Print(LogClass.Application, $"The header key is incorrect or missing and therefore the NCA header content type check has failed. Errored File: {updatePath}");
            }
            catch (MissingKeyException exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Your key set is missing a key with the name: {exception.Name}. Errored File: {updatePath}");
            }

            return false;
        }
    }
}
