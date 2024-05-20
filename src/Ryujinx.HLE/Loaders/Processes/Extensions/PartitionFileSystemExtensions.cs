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
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using ContentType = LibHac.Ncm.ContentType;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    public static class PartitionFileSystemExtensions
    {
        private static readonly DownloadableContentJsonSerializerContext _contentSerializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public static Dictionary<ulong, ContentMetaData> GetContentData(this IFileSystem partitionFileSystem,
            ContentMetaType contentType, VirtualFileSystem fileSystem, IntegrityCheckLevel checkLevel)
        {
            fileSystem.ImportTickets(partitionFileSystem);

            var programs = new Dictionary<ulong, ContentMetaData>();

            foreach (DirectoryEntryEx fileEntry in partitionFileSystem.EnumerateEntries("/", "*.cnmt.nca"))
            {
                Cnmt cnmt = partitionFileSystem.GetNca(fileSystem.KeySet, fileEntry.FullPath).GetCnmt(checkLevel, contentType);

                if (cnmt == null)
                {
                    continue;
                }

                ContentMetaData content = new(partitionFileSystem, cnmt);

                if (content.Type != contentType)
                {
                    continue;
                }

                programs.TryAdd(content.ApplicationId, content);
            }

            return programs;
        }

        internal static (bool, ProcessResult) TryLoad<TMetaData, TFormat, THeader, TEntry>(this PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> partitionFileSystem, Switch device, string path, ulong applicationId, out string errorMessage)
            where TMetaData : PartitionFileSystemMetaCore<TFormat, THeader, TEntry>, new()
            where TFormat : IPartitionFileSystemFormat
            where THeader : unmanaged, IPartitionFileSystemHeader
            where TEntry : unmanaged, IPartitionFileSystemEntry
        {
            errorMessage = null;

            // Load required NCAs.
            Nca mainNca = null;
            Nca patchNca = null;
            Nca controlNca = null;

            try
            {
                Dictionary<ulong, ContentMetaData> applications = partitionFileSystem.GetContentData(ContentMetaType.Application, device.FileSystem, device.System.FsIntegrityCheckLevel);

                if (applicationId == 0)
                {
                    foreach ((ulong _, ContentMetaData content) in applications)
                    {
                        mainNca = content.GetNcaByType(device.FileSystem.KeySet, ContentType.Program, device.Configuration.UserChannelPersistence.Index);
                        controlNca = content.GetNcaByType(device.FileSystem.KeySet, ContentType.Control, device.Configuration.UserChannelPersistence.Index);
                        break;
                    }
                }
                else if (applications.TryGetValue(applicationId, out ContentMetaData content))
                {
                    mainNca = content.GetNcaByType(device.FileSystem.KeySet, ContentType.Program, device.Configuration.UserChannelPersistence.Index);
                    controlNca = content.GetNcaByType(device.FileSystem.KeySet, ContentType.Control, device.Configuration.UserChannelPersistence.Index);
                }

                ProcessLoaderHelper.RegisterProgramMapInfo(device, partitionFileSystem).ThrowIfFailure();
            }
            catch (Exception ex)
            {
                errorMessage = $"Unable to load: {ex.Message}";

                return (false, ProcessResult.Failed);
            }

            if (mainNca != null)
            {
                if (mainNca.Header.ContentType != NcaContentType.Program)
                {
                    errorMessage = "Selected NCA file is not a \"Program\" NCA";

                    return (false, ProcessResult.Failed);
                }

                (Nca updatePatchNca, Nca updateControlNca) = mainNca.GetUpdateData(device.FileSystem, device.System.FsIntegrityCheckLevel, device.Configuration.UserChannelPersistence.Index, out string _);

                if (updatePatchNca != null)
                {
                    patchNca = updatePatchNca;
                }

                if (updateControlNca != null)
                {
                    controlNca = updateControlNca;
                }

                // TODO: If we want to support multi-processes in future, we shouldn't clear AddOnContent data here.
                device.Configuration.ContentManager.ClearAocData();

                // Load DownloadableContents.
                string addOnContentMetadataPath = System.IO.Path.Combine(AppDataManager.GamesDirPath, mainNca.Header.TitleId.ToString("x16"), "dlc.json");
                if (File.Exists(addOnContentMetadataPath))
                {
                    List<DownloadableContentContainer> dlcContainerList = JsonHelper.DeserializeFromFile(addOnContentMetadataPath, _contentSerializerContext.ListDownloadableContentContainer);

                    foreach (DownloadableContentContainer downloadableContentContainer in dlcContainerList)
                    {
                        foreach (DownloadableContentNca downloadableContentNca in downloadableContentContainer.DownloadableContentNcaList)
                        {
                            if (File.Exists(downloadableContentContainer.ContainerPath))
                            {
                                if (downloadableContentNca.Enabled)
                                {
                                    device.Configuration.ContentManager.AddAocItem(downloadableContentNca.TitleId, downloadableContentContainer.ContainerPath, downloadableContentNca.FullPath);
                                }
                            }
                            else
                            {
                                Logger.Warning?.Print(LogClass.Application, $"Cannot find AddOnContent file {downloadableContentContainer.ContainerPath}. It may have been moved or renamed.");
                            }
                        }
                    }
                }

                return (true, mainNca.Load(device, patchNca, controlNca));
            }

            errorMessage = $"Unable to load: Could not find Main NCA for title \"{applicationId:X16}\"";

            return (false, ProcessResult.Failed);
        }

        public static Nca GetNca(this IFileSystem fileSystem, KeySet keySet, string path)
        {
            using var ncaFile = new UniqueRef<IFile>();

            fileSystem.OpenFile(ref ncaFile.Ref, path.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            return new Nca(keySet, ncaFile.Release().AsStorage());
        }
    }
}
