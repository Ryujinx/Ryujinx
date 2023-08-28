using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace Ryujinx.HLE.Loaders.Processes.Extensions
{
    using NcaTuple = Tuple<Nca, Nca>;

    public static class PartitionFileSystemExtensions
    {
        private static readonly DownloadableContentJsonSerializerContext _contentSerializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public static Dictionary<ulong, NcaTuple> GetApplicationData(this IFileSystem partitionFileSystem, VirtualFileSystem fileSystem, int programIndex)
        {
            fileSystem.ImportTickets(partitionFileSystem);

            var programs = new Dictionary<ulong, NcaTuple>();

            foreach (DirectoryEntryEx fileEntry in partitionFileSystem.EnumerateEntries("/", "*.nca"))
            {
                Nca nca = partitionFileSystem.GetNca(fileSystem.KeySet, fileEntry.FullPath);

                if (nca.GetProgramIndex() != programIndex)
                {
                    continue;
                }

                if (nca.IsMain() || nca.IsControl())
                {
                    if (!programs.ContainsKey(nca.Header.TitleId))
                    {
                        programs[nca.Header.TitleId] = new NcaTuple(null, null);
                    }
                }

                if (nca.IsMain() && programs[nca.Header.TitleId].Item1 == null)
                {
                    programs[nca.Header.TitleId] = new NcaTuple(nca, programs[nca.Header.TitleId].Item2);
                }
                else if (nca.IsControl() && programs[nca.Header.TitleId].Item2 == null)
                {
                    programs[nca.Header.TitleId] = new NcaTuple(programs[nca.Header.TitleId].Item1, nca);
                }
            }

            return programs;
        }

        public static Dictionary<ulong, NcaTuple> GetUpdateData(this PartitionFileSystem partitionFileSystem, VirtualFileSystem fileSystem, int programIndex)
        {
            fileSystem.ImportTickets(partitionFileSystem);

            var programs = new Dictionary<ulong, NcaTuple>();

            foreach (DirectoryEntryEx fileEntry in partitionFileSystem.EnumerateEntries("/", "*.nca"))
            {
                Nca nca = partitionFileSystem.GetNca(fileSystem.KeySet, fileEntry.FullPath);

                if (nca.GetProgramIndex() != programIndex)
                {
                    continue;
                }

                if (nca.IsPatch() || nca.IsControl())
                {
                    if (!programs.ContainsKey(nca.Header.TitleId))
                    {
                        programs[nca.Header.TitleId] = new NcaTuple(null, null);
                    }
                }

                if (nca.IsPatch() && programs[nca.Header.TitleId].Item1 == null)
                {
                    programs[nca.Header.TitleId] = new NcaTuple(nca, programs[nca.Header.TitleId].Item2);
                }
                else if (nca.IsControl())
                {
                    programs[nca.Header.TitleId] = new NcaTuple(programs[nca.Header.TitleId].Item1, nca);
                }
            }

            return programs;
        }

        internal static (bool, ProcessResult) TryLoad<TMetaData, TFormat, THeader, TEntry>(this PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> partitionFileSystem, Switch device, string path, ulong titleId, out string errorMessage)
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
                Dictionary<ulong, NcaTuple> applications = partitionFileSystem.GetApplicationData(device.FileSystem, device.Configuration.UserChannelPersistence.Index);

                if (titleId == 0)
                {
                    foreach ((ulong _, (Nca main, Nca control)) in applications)
                    {
                        mainNca = main;
                        controlNca = control;
                        break;
                    }
                }
                else if (applications.TryGetValue(titleId, out NcaTuple ncaTuple))
                {
                    (mainNca, controlNca) = ncaTuple;
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

                (Nca updatePatchNca, Nca updateControlNca) = mainNca.GetUpdateData(device.FileSystem, device.Configuration.UserChannelPersistence.Index, out string _);

                if (updatePatchNca != null)
                {
                    patchNca = updatePatchNca;
                }

                if (updateControlNca != null)
                {
                    controlNca = updateControlNca;
                }

                // Load contained DownloadableContents.
                // TODO: If we want to support multi-processes in future, we shouldn't clear AddOnContent data here.
                device.Configuration.ContentManager.ClearAocData();
                device.Configuration.ContentManager.AddAocData(partitionFileSystem, path, mainNca.Header.TitleId, device.Configuration.FsIntegrityCheckLevel);

                // Load DownloadableContents.
                string addOnContentMetadataPath = System.IO.Path.Combine(AppDataManager.GamesDirPath, mainNca.Header.TitleId.ToString("x16"), "dlc.json");
                if (File.Exists(addOnContentMetadataPath))
                {
                    List<DownloadableContentContainer> dlcContainerList = JsonHelper.DeserializeFromFile(addOnContentMetadataPath, _contentSerializerContext.ListDownloadableContentContainer);

                    foreach (DownloadableContentContainer downloadableContentContainer in dlcContainerList)
                    {
                        foreach (DownloadableContentNca downloadableContentNca in downloadableContentContainer.DownloadableContentNcaList)
                        {
                            if (File.Exists(downloadableContentContainer.ContainerPath) && downloadableContentNca.Enabled)
                            {
                                device.Configuration.ContentManager.AddAocItem(downloadableContentNca.TitleId, downloadableContentContainer.ContainerPath, downloadableContentNca.FullPath);
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

            errorMessage = $"Unable to load: Could not find Main NCA for title '{titleId:x16}'";

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
