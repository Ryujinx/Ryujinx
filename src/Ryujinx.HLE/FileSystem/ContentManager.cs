using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Services.Ssl;
using Ryujinx.HLE.HOS.Services.Time;
using Ryujinx.HLE.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Path = System.IO.Path;

namespace Ryujinx.HLE.FileSystem
{
    public class ContentManager
    {
        private const ulong SystemVersionTitleId = 0x0100000000000809;
        private const ulong SystemUpdateTitleId = 0x0100000000000816;

        private Dictionary<StorageId, LinkedList<LocationEntry>> _locationEntries;

        private readonly Dictionary<string, ulong> _sharedFontTitleDictionary;
        private readonly Dictionary<ulong, string> _systemTitlesNameDictionary;
        private readonly Dictionary<string, string> _sharedFontFilenameDictionary;

        private SortedDictionary<(ulong titleId, NcaContentType type), string> _contentDictionary;

        private readonly struct AocItem
        {
            public readonly string ContainerPath;
            public readonly string NcaPath;

            public AocItem(string containerPath, string ncaPath)
            {
                ContainerPath = containerPath;
                NcaPath = ncaPath;
            }
        }

        private SortedList<ulong, AocItem> AocData { get; }

        private readonly VirtualFileSystem _virtualFileSystem;

        private readonly object _lock = new();

        public ContentManager(VirtualFileSystem virtualFileSystem)
        {
            _contentDictionary = new SortedDictionary<(ulong, NcaContentType), string>();
            _locationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();

            _sharedFontTitleDictionary = new Dictionary<string, ulong>
            {
                { "FontStandard",                  0x0100000000000811 },
                { "FontChineseSimplified",         0x0100000000000814 },
                { "FontExtendedChineseSimplified", 0x0100000000000814 },
                { "FontKorean",                    0x0100000000000812 },
                { "FontChineseTraditional",        0x0100000000000813 },
                { "FontNintendoExtended",          0x0100000000000810 },
            };

            _systemTitlesNameDictionary = new Dictionary<ulong, string>()
            {
                { 0x010000000000080E, "TimeZoneBinary"         },
                { 0x0100000000000810, "FontNintendoExtension"  },
                { 0x0100000000000811, "FontStandard"           },
                { 0x0100000000000812, "FontKorean"             },
                { 0x0100000000000813, "FontChineseTraditional" },
                { 0x0100000000000814, "FontChineseSimple"      },
            };

            _sharedFontFilenameDictionary = new Dictionary<string, string>
            {
                { "FontStandard",                  "nintendo_udsg-r_std_003.bfttf" },
                { "FontChineseSimplified",         "nintendo_udsg-r_org_zh-cn_003.bfttf" },
                { "FontExtendedChineseSimplified", "nintendo_udsg-r_ext_zh-cn_003.bfttf" },
                { "FontKorean",                    "nintendo_udsg-r_ko_003.bfttf" },
                { "FontChineseTraditional",        "nintendo_udjxh-db_zh-tw_003.bfttf" },
                { "FontNintendoExtended",          "nintendo_ext_003.bfttf" },
            };

            _virtualFileSystem = virtualFileSystem;

            AocData = new SortedList<ulong, AocItem>();
        }

        public void LoadEntries(Switch device = null)
        {
            lock (_lock)
            {
                _contentDictionary = new SortedDictionary<(ulong, NcaContentType), string>();
                _locationEntries = new Dictionary<StorageId, LinkedList<LocationEntry>>();

                foreach (StorageId storageId in Enum.GetValues<StorageId>())
                {
                    if (!ContentPath.TryGetContentPath(storageId, out var contentPathString))
                    {
                        continue;
                    }
                    if (!ContentPath.TryGetRealPath(contentPathString, out var contentDirectory))
                    {
                        continue;
                    }
                    var registeredDirectory = Path.Combine(contentDirectory, "registered");

                    Directory.CreateDirectory(registeredDirectory);

                    LinkedList<LocationEntry> locationList = new();

                    void AddEntry(LocationEntry entry)
                    {
                        locationList.AddLast(entry);
                    }

                    foreach (string directoryPath in Directory.EnumerateDirectories(registeredDirectory))
                    {
                        if (Directory.GetFiles(directoryPath).Length > 0)
                        {
                            string ncaName = new DirectoryInfo(directoryPath).Name.Replace(".nca", string.Empty);

                            using FileStream ncaFile = File.OpenRead(Directory.GetFiles(directoryPath)[0]);
                            Nca nca = new(_virtualFileSystem.KeySet, ncaFile.AsStorage());

                            string switchPath = contentPathString + ":/" + ncaFile.Name.Replace(contentDirectory, string.Empty).TrimStart(Path.DirectorySeparatorChar);

                            // Change path format to switch's
                            switchPath = switchPath.Replace('\\', '/');

                            LocationEntry entry = new(switchPath, 0, nca.Header.TitleId, nca.Header.ContentType);

                            AddEntry(entry);

                            _contentDictionary.Add((nca.Header.TitleId, nca.Header.ContentType), ncaName);
                        }
                    }

                    foreach (string filePath in Directory.EnumerateFiles(contentDirectory))
                    {
                        if (Path.GetExtension(filePath) == ".nca")
                        {
                            string ncaName = Path.GetFileNameWithoutExtension(filePath);

                            using FileStream ncaFile = new(filePath, FileMode.Open, FileAccess.Read);
                            Nca nca = new(_virtualFileSystem.KeySet, ncaFile.AsStorage());

                            string switchPath = contentPathString + ":/" + filePath.Replace(contentDirectory, string.Empty).TrimStart(Path.DirectorySeparatorChar);

                            // Change path format to switch's
                            switchPath = switchPath.Replace('\\', '/');

                            LocationEntry entry = new(switchPath, 0, nca.Header.TitleId, nca.Header.ContentType);

                            AddEntry(entry);

                            _contentDictionary.Add((nca.Header.TitleId, nca.Header.ContentType), ncaName);
                        }
                    }

                    if (_locationEntries.TryGetValue(storageId, out var locationEntriesItem) && locationEntriesItem?.Count == 0)
                    {
                        _locationEntries.Remove(storageId);
                    }

                    _locationEntries.TryAdd(storageId, locationList);
                }

                if (device != null)
                {
                    TimeManager.Instance.InitializeTimeZone(device);
                    BuiltInCertificateManager.Instance.Initialize(device);
                    device.System.SharedFontManager.Initialize();
                }
            }
        }

        public void AddAocItem(ulong titleId, string containerPath, string ncaPath, bool mergedToContainer = false)
        {
            // TODO: Check Aoc version.
            if (!AocData.TryAdd(titleId, new AocItem(containerPath, ncaPath)))
            {
                Logger.Warning?.Print(LogClass.Application, $"Duplicate AddOnContent detected. TitleId {titleId:X16}");
            }
            else
            {
                Logger.Info?.Print(LogClass.Application, $"Found AddOnContent with TitleId {titleId:X16}");

                if (!mergedToContainer)
                {
                    using var pfs = PartitionFileSystemUtils.OpenApplicationFileSystem(containerPath, _virtualFileSystem);
                }
            }
        }

        public void ClearAocData() => AocData.Clear();

        public int GetAocCount() => AocData.Count;

        public IList<ulong> GetAocTitleIds() => AocData.Select(e => e.Key).ToList();

        public bool GetAocDataStorage(ulong aocTitleId, out IStorage aocStorage, IntegrityCheckLevel integrityCheckLevel)
        {
            aocStorage = null;

            if (AocData.TryGetValue(aocTitleId, out AocItem aoc))
            {
                var file = new FileStream(aoc.ContainerPath, FileMode.Open, FileAccess.Read);
                using var ncaFile = new UniqueRef<IFile>();

                switch (Path.GetExtension(aoc.ContainerPath))
                {
                    case ".xci":
                        var xci = new Xci(_virtualFileSystem.KeySet, file.AsStorage()).OpenPartition(XciPartitionType.Secure);
                        xci.OpenFile(ref ncaFile.Ref, aoc.NcaPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        break;
                    case ".nsp":
                        var pfs = new PartitionFileSystem();
                        pfs.Initialize(file.AsStorage());
                        pfs.OpenFile(ref ncaFile.Ref, aoc.NcaPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();
                        break;
                    default:
                        return false; // Print error?
                }

                aocStorage = new Nca(_virtualFileSystem.KeySet, ncaFile.Get.AsStorage()).OpenStorage(NcaSectionType.Data, integrityCheckLevel);

                return true;
            }

            return false;
        }

        public void ClearEntry(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            lock (_lock)
            {
                RemoveLocationEntry(titleId, contentType, storageId);
            }
        }

        public void RefreshEntries(StorageId storageId, int flag)
        {
            lock (_lock)
            {
                LinkedList<LocationEntry> locationList = _locationEntries[storageId];
                LinkedListNode<LocationEntry> locationEntry = locationList.First;

                while (locationEntry != null)
                {
                    LinkedListNode<LocationEntry> nextLocationEntry = locationEntry.Next;

                    if (locationEntry.Value.Flag == flag)
                    {
                        locationList.Remove(locationEntry.Value);
                    }

                    locationEntry = nextLocationEntry;
                }
            }
        }

        public bool HasNca(string ncaId, StorageId storageId)
        {
            lock (_lock)
            {
                if (_contentDictionary.ContainsValue(ncaId))
                {
                    var content = _contentDictionary.FirstOrDefault(x => x.Value == ncaId);
                    ulong titleId = content.Key.titleId;

                    NcaContentType contentType = content.Key.type;
                    StorageId storage = GetInstalledStorage(titleId, contentType, storageId);

                    return storage == storageId;
                }
            }

            return false;
        }

        public UInt128 GetInstalledNcaId(ulong titleId, NcaContentType contentType)
        {
            lock (_lock)
            {
                if (_contentDictionary.TryGetValue((titleId, contentType), out var contentDictionaryItem))
                {
                    return UInt128Utils.FromHex(contentDictionaryItem);
                }
            }

            return new UInt128();
        }

        public StorageId GetInstalledStorage(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(titleId, contentType, storageId);

                return locationEntry.ContentPath != null ? ContentPath.GetStorageId(locationEntry.ContentPath) : StorageId.None;
            }
        }

        public string GetInstalledContentPath(ulong titleId, StorageId storageId, NcaContentType contentType)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(titleId, contentType, storageId);

                if (VerifyContentType(locationEntry, contentType))
                {
                    return locationEntry.ContentPath;
                }
            }

            return string.Empty;
        }

        public void RedirectLocation(LocationEntry newEntry, StorageId storageId)
        {
            lock (_lock)
            {
                LocationEntry locationEntry = GetLocation(newEntry.TitleId, newEntry.ContentType, storageId);

                if (locationEntry.ContentPath != null)
                {
                    RemoveLocationEntry(newEntry.TitleId, newEntry.ContentType, storageId);
                }

                AddLocationEntry(newEntry, storageId);
            }
        }

        private bool VerifyContentType(LocationEntry locationEntry, NcaContentType contentType)
        {
            if (locationEntry.ContentPath == null)
            {
                return false;
            }

            string installedPath = VirtualFileSystem.SwitchPathToSystemPath(locationEntry.ContentPath);

            if (!string.IsNullOrWhiteSpace(installedPath))
            {
                if (File.Exists(installedPath))
                {
                    using FileStream file = new(installedPath, FileMode.Open, FileAccess.Read);
                    Nca nca = new(_virtualFileSystem.KeySet, file.AsStorage());
                    bool contentCheck = nca.Header.ContentType == contentType;

                    return contentCheck;
                }
            }

            return false;
        }

        private void AddLocationEntry(LocationEntry entry, StorageId storageId)
        {
            LinkedList<LocationEntry> locationList = null;

            if (_locationEntries.TryGetValue(storageId, out LinkedList<LocationEntry> locationEntry))
            {
                locationList = locationEntry;
            }

            if (locationList != null)
            {
                locationList.Remove(entry);

                locationList.AddLast(entry);
            }
        }

        private void RemoveLocationEntry(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            LinkedList<LocationEntry> locationList = null;

            if (_locationEntries.TryGetValue(storageId, out LinkedList<LocationEntry> locationEntry))
            {
                locationList = locationEntry;
            }

            if (locationList != null)
            {
                LocationEntry entry =
                    locationList.ToList().Find(x => x.TitleId == titleId && x.ContentType == contentType);

                if (entry.ContentPath != null)
                {
                    locationList.Remove(entry);
                }
            }
        }

        public bool TryGetFontTitle(string fontName, out ulong titleId)
        {
            return _sharedFontTitleDictionary.TryGetValue(fontName, out titleId);
        }

        public bool TryGetFontFilename(string fontName, out string filename)
        {
            return _sharedFontFilenameDictionary.TryGetValue(fontName, out filename);
        }

        public bool TryGetSystemTitlesName(ulong titleId, out string name)
        {
            return _systemTitlesNameDictionary.TryGetValue(titleId, out name);
        }

        private LocationEntry GetLocation(ulong titleId, NcaContentType contentType, StorageId storageId)
        {
            LinkedList<LocationEntry> locationList = _locationEntries[storageId];

            return locationList.ToList().Find(x => x.TitleId == titleId && x.ContentType == contentType);
        }

        public void InstallFirmware(string firmwareSource)
        {
            ContentPath.TryGetContentPath(StorageId.BuiltInSystem, out var contentPathString);
            ContentPath.TryGetRealPath(contentPathString, out var contentDirectory);
            string registeredDirectory = Path.Combine(contentDirectory, "registered");
            string temporaryDirectory = Path.Combine(contentDirectory, "temp");

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }

            if (Directory.Exists(firmwareSource))
            {
                InstallFromDirectory(firmwareSource, temporaryDirectory);
                FinishInstallation(temporaryDirectory, registeredDirectory);

                return;
            }

            if (!File.Exists(firmwareSource))
            {
                throw new FileNotFoundException("Firmware file does not exist.");
            }

            FileInfo info = new(firmwareSource);

            using FileStream file = File.OpenRead(firmwareSource);

            switch (info.Extension)
            {
                case ".zip":
                    using (ZipArchive archive = ZipFile.OpenRead(firmwareSource))
                    {
                        InstallFromZip(archive, temporaryDirectory);
                    }
                    break;
                case ".xci":
                    Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());
                    InstallFromCart(xci, temporaryDirectory);
                    break;
                default:
                    throw new InvalidFirmwarePackageException("Input file is not a valid firmware package");
            }

            FinishInstallation(temporaryDirectory, registeredDirectory);
        }

        private void FinishInstallation(string temporaryDirectory, string registeredDirectory)
        {
            if (Directory.Exists(registeredDirectory))
            {
                new DirectoryInfo(registeredDirectory).Delete(true);
            }

            Directory.Move(temporaryDirectory, registeredDirectory);

            LoadEntries();
        }

        private void InstallFromDirectory(string firmwareDirectory, string temporaryDirectory)
        {
            InstallFromPartition(new LocalFileSystem(firmwareDirectory), temporaryDirectory);
        }

        private void InstallFromPartition(IFileSystem filesystem, string temporaryDirectory)
        {
            foreach (var entry in filesystem.EnumerateEntries("/", "*.nca"))
            {
                Nca nca = new(_virtualFileSystem.KeySet, OpenPossibleFragmentedFile(filesystem, entry.FullPath, OpenMode.Read).AsStorage());

                SaveNca(nca, entry.Name.Remove(entry.Name.IndexOf('.')), temporaryDirectory);
            }
        }

        private void InstallFromCart(Xci gameCard, string temporaryDirectory)
        {
            if (gameCard.HasPartition(XciPartitionType.Update))
            {
                XciPartition partition = gameCard.OpenPartition(XciPartitionType.Update);

                InstallFromPartition(partition, temporaryDirectory);
            }
            else
            {
                throw new Exception("Update not found in xci file.");
            }
        }

        private static void InstallFromZip(ZipArchive archive, string temporaryDirectory)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".nca") || entry.FullName.EndsWith(".nca/00"))
                {
                    // Clean up the name and get the NcaId

                    string[] pathComponents = entry.FullName.Replace(".cnmt", "").Split('/');

                    string ncaId = pathComponents[^1];

                    // If this is a fragmented nca, we need to get the previous element.GetZip
                    if (ncaId.Equals("00"))
                    {
                        ncaId = pathComponents[^2];
                    }

                    if (ncaId.Contains(".nca"))
                    {
                        string newPath = Path.Combine(temporaryDirectory, ncaId);

                        Directory.CreateDirectory(newPath);

                        entry.ExtractToFile(Path.Combine(newPath, "00"));
                    }
                }
            }
        }

        public static void SaveNca(Nca nca, string ncaId, string temporaryDirectory)
        {
            string newPath = Path.Combine(temporaryDirectory, ncaId + ".nca");

            Directory.CreateDirectory(newPath);

            using FileStream file = File.Create(Path.Combine(newPath, "00"));
            nca.BaseStorage.AsStream().CopyTo(file);
        }

        private static IFile OpenPossibleFragmentedFile(IFileSystem filesystem, string path, OpenMode mode)
        {
            using var file = new UniqueRef<IFile>();

            if (filesystem.FileExists($"{path}/00"))
            {
                filesystem.OpenFile(ref file.Ref, $"{path}/00".ToU8Span(), mode).ThrowIfFailure();
            }
            else
            {
                filesystem.OpenFile(ref file.Ref, path.ToU8Span(), mode).ThrowIfFailure();
            }

            return file.Release();
        }

        private static Stream GetZipStream(ZipArchiveEntry entry)
        {
            MemoryStream dest = MemoryStreamManager.Shared.GetStream();

            using Stream src = entry.Open();
            src.CopyTo(dest);

            return dest;
        }

        public SystemVersion VerifyFirmwarePackage(string firmwarePackage)
        {
            _virtualFileSystem.ReloadKeySet();

            // LibHac.NcaHeader's DecryptHeader doesn't check if HeaderKey is empty and throws InvalidDataException instead
            // So, we check it early for a better user experience.
            if (_virtualFileSystem.KeySet.HeaderKey.IsZeros())
            {
                throw new MissingKeyException("HeaderKey is empty. Cannot decrypt NCA headers.");
            }

            Dictionary<ulong, List<(NcaContentType type, string path)>> updateNcas = new();

            if (Directory.Exists(firmwarePackage))
            {
                return VerifyAndGetVersionDirectory(firmwarePackage);
            }

            if (!File.Exists(firmwarePackage))
            {
                throw new FileNotFoundException("Firmware file does not exist.");
            }

            FileInfo info = new(firmwarePackage);

            using FileStream file = File.OpenRead(firmwarePackage);

            switch (info.Extension)
            {
                case ".zip":
                    using (ZipArchive archive = ZipFile.OpenRead(firmwarePackage))
                    {
                        return VerifyAndGetVersionZip(archive);
                    }
                case ".xci":
                    Xci xci = new(_virtualFileSystem.KeySet, file.AsStorage());

                    if (xci.HasPartition(XciPartitionType.Update))
                    {
                        XciPartition partition = xci.OpenPartition(XciPartitionType.Update);

                        return VerifyAndGetVersion(partition);
                    }
                    else
                    {
                        throw new InvalidFirmwarePackageException("Update not found in xci file.");
                    }
                default:
                    break;
            }

            SystemVersion VerifyAndGetVersionDirectory(string firmwareDirectory)
            {
                return VerifyAndGetVersion(new LocalFileSystem(firmwareDirectory));
            }

            SystemVersion VerifyAndGetVersionZip(ZipArchive archive)
            {
                SystemVersion systemVersion = null;

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".nca") || entry.FullName.EndsWith(".nca/00"))
                    {
                        using Stream ncaStream = GetZipStream(entry);
                        IStorage storage = ncaStream.AsStorage();

                        Nca nca = new(_virtualFileSystem.KeySet, storage);

                        if (updateNcas.TryGetValue(nca.Header.TitleId, out var updateNcasItem))
                        {
                            updateNcasItem.Add((nca.Header.ContentType, entry.FullName));
                        }
                        else
                        {
                            updateNcas.Add(nca.Header.TitleId, new List<(NcaContentType, string)>());
                            updateNcas[nca.Header.TitleId].Add((nca.Header.ContentType, entry.FullName));
                        }
                    }
                }

                if (updateNcas.TryGetValue(SystemUpdateTitleId, out var ncaEntry))
                {
                    string metaPath = ncaEntry.Find(x => x.type == NcaContentType.Meta).path;

                    CnmtContentMetaEntry[] metaEntries = null;

                    var fileEntry = archive.GetEntry(metaPath);

                    using (Stream ncaStream = GetZipStream(fileEntry))
                    {
                        Nca metaNca = new(_virtualFileSystem.KeySet, ncaStream.AsStorage());

                        IFileSystem fs = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                        using var metaFile = new UniqueRef<IFile>();

                        if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var meta = new Cnmt(metaFile.Get.AsStream());

                            if (meta.Type == ContentMetaType.SystemUpdate)
                            {
                                metaEntries = meta.MetaEntries;

                                updateNcas.Remove(SystemUpdateTitleId);
                            }
                        }
                    }

                    if (metaEntries == null)
                    {
                        throw new FileNotFoundException("System update title was not found in the firmware package.");
                    }

                    if (updateNcas.TryGetValue(SystemVersionTitleId, out var updateNcasItem))
                    {
                        string versionEntry = updateNcasItem.Find(x => x.type != NcaContentType.Meta).path;

                        using Stream ncaStream = GetZipStream(archive.GetEntry(versionEntry));
                        Nca nca = new(_virtualFileSystem.KeySet, ncaStream.AsStorage());

                        var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        using var systemVersionFile = new UniqueRef<IFile>();

                        if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            systemVersion = new SystemVersion(systemVersionFile.Get.AsStream());
                        }
                    }

                    foreach (CnmtContentMetaEntry metaEntry in metaEntries)
                    {
                        if (updateNcas.TryGetValue(metaEntry.TitleId, out ncaEntry))
                        {
                            metaPath = ncaEntry.Find(x => x.type == NcaContentType.Meta).path;

                            string contentPath = ncaEntry.Find(x => x.type != NcaContentType.Meta).path;

                            // Nintendo in 9.0.0, removed PPC and only kept the meta nca of it.
                            // This is a perfect valid case, so we should just ignore the missing content nca and continue.
                            if (contentPath == null)
                            {
                                updateNcas.Remove(metaEntry.TitleId);

                                continue;
                            }

                            ZipArchiveEntry metaZipEntry = archive.GetEntry(metaPath);
                            ZipArchiveEntry contentZipEntry = archive.GetEntry(contentPath);

                            using Stream metaNcaStream = GetZipStream(metaZipEntry);
                            using Stream contentNcaStream = GetZipStream(contentZipEntry);
                            Nca metaNca = new(_virtualFileSystem.KeySet, metaNcaStream.AsStorage());

                            IFileSystem fs = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                            string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                            using var metaFile = new UniqueRef<IFile>();

                            if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                            {
                                var meta = new Cnmt(metaFile.Get.AsStream());

                                IStorage contentStorage = contentNcaStream.AsStorage();
                                if (contentStorage.GetSize(out long size).IsSuccess())
                                {
                                    byte[] contentData = new byte[size];

                                    Span<byte> content = new(contentData);

                                    contentStorage.Read(0, content);

                                    Span<byte> hash = new(new byte[32]);

                                    LibHac.Crypto.Sha256.GenerateSha256Hash(content, hash);

                                    if (LibHac.Common.Utilities.ArraysEqual(hash.ToArray(), meta.ContentEntries[0].Hash))
                                    {
                                        updateNcas.Remove(metaEntry.TitleId);
                                    }
                                }
                            }
                        }
                    }

                    if (updateNcas.Count > 0)
                    {
                        StringBuilder extraNcas = new();

                        foreach (var entry in updateNcas)
                        {
                            foreach (var (type, path) in entry.Value)
                            {
                                extraNcas.AppendLine(path);
                            }
                        }

                        throw new InvalidFirmwarePackageException($"Firmware package contains unrelated archives. Please remove these paths: {Environment.NewLine}{extraNcas}");
                    }
                }
                else
                {
                    throw new FileNotFoundException("System update title was not found in the firmware package.");
                }

                return systemVersion;
            }

            SystemVersion VerifyAndGetVersion(IFileSystem filesystem)
            {
                SystemVersion systemVersion = null;

                CnmtContentMetaEntry[] metaEntries = null;

                foreach (var entry in filesystem.EnumerateEntries("/", "*.nca"))
                {
                    IStorage ncaStorage = OpenPossibleFragmentedFile(filesystem, entry.FullPath, OpenMode.Read).AsStorage();

                    Nca nca = new(_virtualFileSystem.KeySet, ncaStorage);

                    if (nca.Header.TitleId == SystemUpdateTitleId && nca.Header.ContentType == NcaContentType.Meta)
                    {
                        IFileSystem fs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                        using var metaFile = new UniqueRef<IFile>();

                        if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var meta = new Cnmt(metaFile.Get.AsStream());

                            if (meta.Type == ContentMetaType.SystemUpdate)
                            {
                                metaEntries = meta.MetaEntries;
                            }
                        }

                        continue;
                    }
                    else if (nca.Header.TitleId == SystemVersionTitleId && nca.Header.ContentType == NcaContentType.Data)
                    {
                        var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        using var systemVersionFile = new UniqueRef<IFile>();

                        if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            systemVersion = new SystemVersion(systemVersionFile.Get.AsStream());
                        }
                    }

                    if (updateNcas.TryGetValue(nca.Header.TitleId, out var updateNcasItem))
                    {
                        updateNcasItem.Add((nca.Header.ContentType, entry.FullPath));
                    }
                    else
                    {
                        updateNcas.Add(nca.Header.TitleId, new List<(NcaContentType, string)>());
                        updateNcas[nca.Header.TitleId].Add((nca.Header.ContentType, entry.FullPath));
                    }

                    ncaStorage.Dispose();
                }

                if (metaEntries == null)
                {
                    throw new FileNotFoundException("System update title was not found in the firmware package.");
                }

                foreach (CnmtContentMetaEntry metaEntry in metaEntries)
                {
                    if (updateNcas.TryGetValue(metaEntry.TitleId, out var ncaEntry))
                    {
                        string metaNcaPath = ncaEntry.Find(x => x.type == NcaContentType.Meta).path;
                        string contentPath = ncaEntry.Find(x => x.type != NcaContentType.Meta).path;

                        // Nintendo in 9.0.0, removed PPC and only kept the meta nca of it.
                        // This is a perfect valid case, so we should just ignore the missing content nca and continue.
                        if (contentPath == null)
                        {
                            updateNcas.Remove(metaEntry.TitleId);

                            continue;
                        }

                        IStorage metaStorage = OpenPossibleFragmentedFile(filesystem, metaNcaPath, OpenMode.Read).AsStorage();
                        IStorage contentStorage = OpenPossibleFragmentedFile(filesystem, contentPath, OpenMode.Read).AsStorage();

                        Nca metaNca = new(_virtualFileSystem.KeySet, metaStorage);

                        IFileSystem fs = metaNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                        string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                        using var metaFile = new UniqueRef<IFile>();

                        if (fs.OpenFile(ref metaFile.Ref, cnmtPath.ToU8Span(), OpenMode.Read).IsSuccess())
                        {
                            var meta = new Cnmt(metaFile.Get.AsStream());

                            if (contentStorage.GetSize(out long size).IsSuccess())
                            {
                                byte[] contentData = new byte[size];

                                Span<byte> content = new(contentData);

                                contentStorage.Read(0, content);

                                Span<byte> hash = new(new byte[32]);

                                LibHac.Crypto.Sha256.GenerateSha256Hash(content, hash);

                                if (LibHac.Common.Utilities.ArraysEqual(hash.ToArray(), meta.ContentEntries[0].Hash))
                                {
                                    updateNcas.Remove(metaEntry.TitleId);
                                }
                            }
                        }
                    }
                }

                if (updateNcas.Count > 0)
                {
                    StringBuilder extraNcas = new();

                    foreach (var entry in updateNcas)
                    {
                        foreach (var (type, path) in entry.Value)
                        {
                            extraNcas.AppendLine(path);
                        }
                    }

                    throw new InvalidFirmwarePackageException($"Firmware package contains unrelated archives. Please remove these paths: {Environment.NewLine}{extraNcas}");
                }

                return systemVersion;
            }

            return null;
        }

        public SystemVersion GetCurrentFirmwareVersion()
        {
            LoadEntries();

            lock (_lock)
            {
                var locationEnties = _locationEntries[StorageId.BuiltInSystem];

                foreach (var entry in locationEnties)
                {
                    if (entry.ContentType == NcaContentType.Data)
                    {
                        var path = VirtualFileSystem.SwitchPathToSystemPath(entry.ContentPath);

                        using FileStream fileStream = File.OpenRead(path);
                        Nca nca = new(_virtualFileSystem.KeySet, fileStream.AsStorage());

                        if (nca.Header.TitleId == SystemVersionTitleId && nca.Header.ContentType == NcaContentType.Data)
                        {
                            var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                            using var systemVersionFile = new UniqueRef<IFile>();

                            if (romfs.OpenFile(ref systemVersionFile.Ref, "/file".ToU8Span(), OpenMode.Read).IsSuccess())
                            {
                                return new SystemVersion(systemVersionFile.Get.AsStream());
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}
