using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Loader;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.UI.Common.Helper;
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Ryujinx.UI.App.Common
{
    public class ApplicationData
    {
        public bool Favorite { get; set; }
        public byte[] Icon { get; set; }
        public string Name { get; set; } = "Unknown";
        public ulong Id { get; set; }
        public string Developer { get; set; } = "Unknown";
        public string Version { get; set; } = "0";
        public TimeSpan TimePlayed { get; set; }
        public DateTime? LastPlayed { get; set; }
        public string FileExtension { get; set; }
        public long FileSize { get; set; }
        public string Path { get; set; }
        public BlitStruct<ApplicationControlProperty> ControlHolder { get; set; }

        public string TimePlayedString => ValueFormatUtils.FormatTimeSpan(TimePlayed);

        public string LastPlayedString => ValueFormatUtils.FormatDateTime(LastPlayed);

        public string FileSizeString => ValueFormatUtils.FormatFileSize(FileSize);

        [JsonIgnore] public string IdString => Id.ToString("x16");

        [JsonIgnore] public ulong IdBase => Id & ~0x1FFFUL;

        public static string GetBuildId(VirtualFileSystem virtualFileSystem, IntegrityCheckLevel checkLevel, string titleFilePath)
        {
            using FileStream file = new(titleFilePath, FileMode.Open, FileAccess.Read);

            Nca mainNca = null;
            Nca patchNca = null;

            if (!System.IO.Path.Exists(titleFilePath))
            {
                Logger.Error?.Print(LogClass.Application, $"File \"{titleFilePath}\" does not exist.");
                return string.Empty;
            }

            string extension = System.IO.Path.GetExtension(titleFilePath).ToLower();

            if (extension is ".nsp" or ".xci")
            {
                IFileSystem pfs;

                if (extension == ".xci")
                {
                    Xci xci = new(virtualFileSystem.KeySet, file.AsStorage());

                    pfs = xci.OpenPartition(XciPartitionType.Secure);
                }
                else
                {
                    var pfsTemp = new PartitionFileSystem();
                    pfsTemp.Initialize(file.AsStorage()).ThrowIfFailure();
                    pfs = pfsTemp;
                }

                foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*.nca"))
                {
                    using var ncaFile = new UniqueRef<IFile>();

                    pfs.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                    Nca nca = new(virtualFileSystem.KeySet, ncaFile.Get.AsStorage());

                    if (nca.Header.ContentType != NcaContentType.Program)
                    {
                        continue;
                    }

                    int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                    if (nca.Header.GetFsHeader(dataIndex).IsPatchSection())
                    {
                        patchNca = nca;
                    }
                    else
                    {
                        mainNca = nca;
                    }
                }
            }
            else if (extension == ".nca")
            {
                mainNca = new Nca(virtualFileSystem.KeySet, file.AsStorage());
            }

            if (mainNca == null)
            {
                Logger.Error?.Print(LogClass.Application, "Extraction failure. The main NCA was not present in the selected file");

                return string.Empty;
            }

            (Nca updatePatchNca, _) = mainNca.GetUpdateData(virtualFileSystem, checkLevel, 0, out string _);

            if (updatePatchNca != null)
            {
                patchNca = updatePatchNca;
            }

            IFileSystem codeFs = null;

            if (patchNca == null)
            {
                if (mainNca.CanOpenSection(NcaSectionType.Code))
                {
                    codeFs = mainNca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.ErrorOnInvalid);
                }
            }
            else
            {
                if (patchNca.CanOpenSection(NcaSectionType.Code))
                {
                    codeFs = mainNca.OpenFileSystemWithPatch(patchNca, NcaSectionType.Code, IntegrityCheckLevel.ErrorOnInvalid);
                }
            }

            if (codeFs == null)
            {
                Logger.Error?.Print(LogClass.Loader, "No ExeFS found in NCA");

                return string.Empty;
            }

            const string MainExeFs = "main";

            if (!codeFs.FileExists($"/{MainExeFs}"))
            {
                Logger.Error?.Print(LogClass.Loader, "No main binary ExeFS found in ExeFS");

                return string.Empty;
            }

            using var nsoFile = new UniqueRef<IFile>();

            codeFs.OpenFile(ref nsoFile.Ref, $"/{MainExeFs}".ToU8Span(), OpenMode.Read).ThrowIfFailure();

            NsoReader reader = new();
            reader.Initialize(nsoFile.Release().AsStorage().AsFile(OpenMode.Read)).ThrowIfFailure();

            return BitConverter.ToString(reader.Header.ModuleId.ItemsRo.ToArray()).Replace("-", "").ToUpper()[..16];
        }
    }
}
