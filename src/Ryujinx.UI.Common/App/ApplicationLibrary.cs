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
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private bool _isLoading;

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
            if (resourceName == "")
            {
                return Array.Empty<byte>();
            }

            Stream resourceStream = Assembly.GetCallingAssembly().GetManifestResourceStream(resourceName);
            byte[] resourceByteArray = new byte[resourceStream.Length];

            resourceStream.Read(resourceByteArray);

            return resourceByteArray;
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
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            Thread applicationLibraryThread = new(() =>
            {
                LoadApplicationsReal(appDirs, desiredTitleLanguage);

                _isLoading = false;
            })
            {
                Name = "GUI.ApplicationLibraryThread",
                IsBackground = true,
            };
            applicationLibraryThread.Start();
        }

        private void LoadApplicationsReal(List<string> appDirs, Language desiredTitleLanguage)
        {
            int numApplicationsFound = 0;
            int numApplicationsLoaded = 0;

            _desiredTitleLanguage = desiredTitleLanguage;

            _cancellationToken = new CancellationTokenSource();

            // Builds the applications list with paths to found applications
            List<string> applications = new();

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
                        var FileSearchOption = SearchOption.AllDirectories;

                        if (ConfigurationState.Instance.UI.UseSystemGameFolders)
                        {
                            IEnumerable<string> folders = Directory.EnumerateDirectories(appDir, "*", SearchOption.TopDirectoryOnly);
                            foreach (string folder in folders)
                            {
                                if (_cancellationToken.Token.IsCancellationRequested)
                                {
                                    return;
                                }

                                var fileInfo = new FileInfo(folder);

                                var fullPath = fileInfo.ResolveLinkTarget(true)?.FullName ?? fileInfo.FullName;
                                ApplicationData folderData = new()
                                {
                                    TitleName = fileInfo.Name,
                                    FileExtension = "Folder",
                                    Path = fullPath,
                                };
                                OnApplicationAdded(new ApplicationAddedEventArgs
                                {
                                    AppData = folderData,
                                });
                            }

                            FileSearchOption = SearchOption.TopDirectoryOnly;
                        }

                        IEnumerable<string> files = Directory.EnumerateFiles(appDir, "*", FileSearchOption).Where(file =>
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

                                if (!File.Exists(fullPath))
                                {
                                    Logger.Warning?.Print(LogClass.Application, $"Skipping invalid symlink: {fileInfo.FullName}");
                                    continue;
                                }

                                applications.Add(fullPath);
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
                foreach (string applicationPath in applications)
                {
                    if (_cancellationToken.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    long fileSize = new FileInfo(applicationPath).Length;
                    string titleName = "Unknown";
                    string titleId = "0000000000000000";
                    string developer = "Unknown";
                    string version = "0";
                    byte[] applicationIcon = null;

                    BlitStruct<ApplicationControlProperty> controlHolder = new(1);

                    try
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

                                    // If the NSP doesn't have a main NCA, decrement the number of applications found and then continue to the next application.
                                    bool hasMainNca = false;

                                    foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*"))
                                    {
                                        if (Path.GetExtension(fileEntry.FullPath).ToLower() == ".nca")
                                        {
                                            using UniqueRef<IFile> ncaFile = new();

                                            pfs.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                            Nca nca = new(_virtualFileSystem.KeySet, ncaFile.Get.AsStorage());
                                            int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                                            // Some main NCAs don't have a data partition, so check if the partition exists before opening it
                                            if (nca.Header.ContentType == NcaContentType.Program && !(nca.SectionExists(NcaSectionType.Data) && nca.Header.GetFsHeader(dataIndex).IsPatchSection()))
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

                                    if (!hasMainNca && !isExeFs)
                                    {
                                        numApplicationsFound--;

                                        continue;
                                    }
                                }

                                if (isExeFs)
                                {
                                    applicationIcon = _nspIcon;

                                    using UniqueRef<IFile> npdmFile = new();

                                    Result result = pfs.OpenFile(ref npdmFile.Ref, "/main.npdm".ToU8Span(), OpenMode.Read);

                                    if (ResultFs.PathNotFound.Includes(result))
                                    {
                                        Npdm npdm = new(npdmFile.Get.AsStream());

                                        titleName = npdm.TitleName;
                                        titleId = npdm.Aci0.TitleId.ToString("x16");
                                    }
                                }
                                else
                                {
                                    GetControlFsAndTitleId(pfs, out IFileSystem controlFs, out titleId);

                                    // Check if there is an update available.
                                    if (IsUpdateApplied(titleId, out IFileSystem updatedControlFs))
                                    {
                                        // Replace the original ControlFs by the updated one.
                                        controlFs = updatedControlFs;
                                    }

                                    ReadControlData(controlFs, controlHolder.ByteSpan);

                                    GetGameInformation(ref controlHolder.Value, out titleName, out _, out developer, out version);

                                    // Read the icon from the ControlFS and store it as a byte array
                                    try
                                    {
                                        using UniqueRef<IFile> icon = new();

                                        controlFs.OpenFile(ref icon.Ref, $"/icon_{_desiredTitleLanguage}.dat".ToU8Span(), OpenMode.Read).ThrowIfFailure();

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

                                            using MemoryStream stream = new();

                                            icon.Get.AsStream().CopyTo(stream);
                                            applicationIcon = stream.ToArray();

                                            if (applicationIcon != null)
                                            {
                                                break;
                                            }
                                        }

                                        applicationIcon ??= extension == ".xci" ? _xciIcon : _nspIcon;
                                    }
                                }
                            }
                            catch (MissingKeyException exception)
                            {
                                applicationIcon = extension == ".xci" ? _xciIcon : _nspIcon;

                                Logger.Warning?.Print(LogClass.Application, $"Your key set is missing a key with the name: {exception.Name}");
                            }
                            catch (InvalidDataException)
                            {
                                applicationIcon = extension == ".xci" ? _xciIcon : _nspIcon;

                                Logger.Warning?.Print(LogClass.Application, $"The header key is incorrect or missing and therefore the NCA header content type check has failed. Errored File: {applicationPath}");
                            }
                            catch (Exception exception)
                            {
                                Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. File: '{applicationPath}' Error: {exception}");

                                numApplicationsFound--;

                                continue;
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

                                    ulong nacpOffset = reader.ReadUInt64();
                                    ulong nacpSize = reader.ReadUInt64();

                                    // Reads and stores game icon as byte array
                                    if (iconSize > 0)
                                    {
                                        applicationIcon = Read(assetOffset + iconOffset, (int)iconSize);
                                    }
                                    else
                                    {
                                        applicationIcon = _nroIcon;
                                    }

                                    // Read the NACP data
                                    Read(assetOffset + (int)nacpOffset, (int)nacpSize).AsSpan().CopyTo(controlHolder.ByteSpan);

                                    GetGameInformation(ref controlHolder.Value, out titleName, out titleId, out developer, out version);
                                }
                                else
                                {
                                    applicationIcon = _nroIcon;
                                    titleName = Path.GetFileNameWithoutExtension(applicationPath);
                                }
                            }
                            catch
                            {
                                Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");

                                numApplicationsFound--;

                                continue;
                            }
                        }
                        else if (extension == ".nca")
                        {
                            try
                            {
                                Nca nca = new(_virtualFileSystem.KeySet, new FileStream(applicationPath, FileMode.Open, FileAccess.Read).AsStorage());
                                int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                                if (nca.Header.ContentType != NcaContentType.Program || (nca.SectionExists(NcaSectionType.Data) && nca.Header.GetFsHeader(dataIndex).IsPatchSection()))
                                {
                                    numApplicationsFound--;

                                    continue;
                                }
                            }
                            catch (InvalidDataException)
                            {
                                Logger.Warning?.Print(LogClass.Application, $"The NCA header content type check has failed. This is usually because the header key is incorrect or missing. Errored File: {applicationPath}");
                            }
                            catch
                            {
                                Logger.Warning?.Print(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");

                                numApplicationsFound--;

                                continue;
                            }

                            applicationIcon = _ncaIcon;
                            titleName = Path.GetFileNameWithoutExtension(applicationPath);
                        }
                        // If its an NSO we just set defaults
                        else if (extension == ".nso")
                        {
                            applicationIcon = _nsoIcon;
                            titleName = Path.GetFileNameWithoutExtension(applicationPath);
                        }
                    }
                    catch (IOException exception)
                    {
                        Logger.Warning?.Print(LogClass.Application, exception.Message);

                        numApplicationsFound--;

                        continue;
                    }

                    ApplicationMetadata appMetadata = LoadAndSaveMetaData(titleId, appMetadata =>
                    {
                        appMetadata.Title = titleName;

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

                    ApplicationData data = new()
                    {
                        Favorite = appMetadata.Favorite,
                        Icon = applicationIcon,
                        TitleName = titleName,
                        TitleId = titleId,
                        Developer = developer,
                        Version = version,
                        TimePlayed = appMetadata.TimePlayed,
                        LastPlayed = appMetadata.LastPlayed,
                        FileExtension = Path.GetExtension(applicationPath).TrimStart('.').ToUpper(),
                        FileSize = fileSize,
                        Path = applicationPath,
                        ControlHolder = controlHolder,
                    };

                    numApplicationsLoaded++;

                    OnApplicationAdded(new ApplicationAddedEventArgs
                    {
                        AppData = data,
                    });

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

        private void GetControlFsAndTitleId(IFileSystem pfs, out IFileSystem controlFs, out string titleId)
        {
            (_, _, Nca controlNca) = GetGameData(_virtualFileSystem, pfs, 0);

            // Return the ControlFS
            controlFs = controlNca?.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
            titleId = controlNca?.Header.TitleId.ToString("x16");
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

        public byte[] GetApplicationIcon(string applicationPath, Language desiredTitleLanguage)
        {
            byte[] applicationIcon = null;

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
                                GetControlFsAndTitleId(pfs, out IFileSystem controlFs, out _);

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

                                        if (applicationIcon != null)
                                        {
                                            break;
                                        }
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

        private void GetGameInformation(ref ApplicationControlProperty controlData, out string titleName, out string titleId, out string publisher, out string version)
        {
            _ = Enum.TryParse(_desiredTitleLanguage.ToString(), out TitleLanguage desiredTitleLanguage);

            if (controlData.Title.ItemsRo.Length > (int)desiredTitleLanguage)
            {
                titleName = controlData.Title[(int)desiredTitleLanguage].NameString.ToString();
                publisher = controlData.Title[(int)desiredTitleLanguage].PublisherString.ToString();
            }
            else
            {
                titleName = null;
                publisher = null;
            }

            if (string.IsNullOrWhiteSpace(titleName))
            {
                foreach (ref readonly var controlTitle in controlData.Title.ItemsRo)
                {
                    if (!controlTitle.NameString.IsEmpty())
                    {
                        titleName = controlTitle.NameString.ToString();

                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(publisher))
            {
                foreach (ref readonly var controlTitle in controlData.Title.ItemsRo)
                {
                    if (!controlTitle.PublisherString.IsEmpty())
                    {
                        publisher = controlTitle.PublisherString.ToString();

                        break;
                    }
                }
            }

            if (controlData.PresenceGroupId != 0)
            {
                titleId = controlData.PresenceGroupId.ToString("x16");
            }
            else if (controlData.SaveDataOwnerId != 0)
            {
                titleId = controlData.SaveDataOwnerId.ToString();
            }
            else if (controlData.AddOnContentBaseId != 0)
            {
                titleId = (controlData.AddOnContentBaseId - 0x1000).ToString("x16");
            }
            else
            {
                titleId = "0000000000000000";
            }

            version = controlData.DisplayVersionString.ToString();
        }

        private bool IsUpdateApplied(string titleId, out IFileSystem updatedControlFs)
        {
            updatedControlFs = null;

            string updatePath = "(unknown)";

            try
            {
                (Nca patchNca, Nca controlNca) = GetGameUpdateData(_virtualFileSystem, titleId, 0, out updatePath);

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

        public static (Nca main, Nca patch, Nca control) GetGameData(VirtualFileSystem fileSystem, IFileSystem pfs, int programIndex)
        {
            Nca mainNca = null;
            Nca patchNca = null;
            Nca controlNca = null;

            fileSystem.ImportTickets(pfs);

            foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*.nca"))
            {
                using var ncaFile = new UniqueRef<IFile>();

                pfs.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                Nca nca = new(fileSystem.KeySet, ncaFile.Release().AsStorage());

                int ncaProgramIndex = (int)(nca.Header.TitleId & 0xF);

                if (ncaProgramIndex != programIndex)
                {
                    continue;
                }

                if (nca.Header.ContentType == NcaContentType.Program)
                {
                    int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                    if (nca.SectionExists(NcaSectionType.Data) && nca.Header.GetFsHeader(dataIndex).IsPatchSection())
                    {
                        patchNca = nca;
                    }
                    else
                    {
                        mainNca = nca;
                    }
                }
                else if (nca.Header.ContentType == NcaContentType.Control)
                {
                    controlNca = nca;
                }
            }

            return (mainNca, patchNca, controlNca);
        }

        public static (Nca patch, Nca control) GetGameUpdateDataFromPartition(VirtualFileSystem fileSystem, PartitionFileSystem pfs, string titleId, int programIndex)
        {
            Nca patchNca = null;
            Nca controlNca = null;

            fileSystem.ImportTickets(pfs);

            foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*.nca"))
            {
                using var ncaFile = new UniqueRef<IFile>();

                pfs.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                Nca nca = new(fileSystem.KeySet, ncaFile.Release().AsStorage());

                int ncaProgramIndex = (int)(nca.Header.TitleId & 0xF);

                if (ncaProgramIndex != programIndex)
                {
                    continue;
                }

                if ($"{nca.Header.TitleId.ToString("x16")[..^3]}000" != titleId)
                {
                    break;
                }

                if (nca.Header.ContentType == NcaContentType.Program)
                {
                    patchNca = nca;
                }
                else if (nca.Header.ContentType == NcaContentType.Control)
                {
                    controlNca = nca;
                }
            }

            return (patchNca, controlNca);
        }

        public static (Nca patch, Nca control) GetGameUpdateData(VirtualFileSystem fileSystem, string titleId, int programIndex, out string updatePath)
        {
            updatePath = null;

            if (ulong.TryParse(titleId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong titleIdBase))
            {
                // Clear the program index part.
                titleIdBase &= ~0xFUL;

                // Load update information if exists.
                string titleUpdateMetadataPath = Path.Combine(AppDataManager.GamesDirPath, titleIdBase.ToString("x16"), "updates.json");

                if (File.Exists(titleUpdateMetadataPath))
                {
                    updatePath = JsonHelper.DeserializeFromFile(titleUpdateMetadataPath, _titleSerializerContext.TitleUpdateMetadata).Selected;

                    if (File.Exists(updatePath))
                    {
                        FileStream file = new(updatePath, FileMode.Open, FileAccess.Read);
                        PartitionFileSystem nsp = new();
                        nsp.Initialize(file.AsStorage()).ThrowIfFailure();

                        return GetGameUpdateDataFromPartition(fileSystem, nsp, titleIdBase.ToString("x16"), programIndex);
                    }
                }
            }

            return (null, null);
        }
    }
}
