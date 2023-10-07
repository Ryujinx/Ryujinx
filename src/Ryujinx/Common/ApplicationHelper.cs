using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibHac;
using LibHac.Account;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ApplicationId = LibHac.Ncm.ApplicationId;
using Path = System.IO.Path;

namespace Ryujinx.Ava.Common
{
    internal static class ApplicationHelper
    {
        private static HorizonClient _horizonClient;
        private static AccountManager _accountManager;
        private static VirtualFileSystem _virtualFileSystem;

        public static void Initialize(VirtualFileSystem virtualFileSystem, AccountManager accountManager, HorizonClient horizonClient)
        {
            _virtualFileSystem = virtualFileSystem;
            _horizonClient = horizonClient;
            _accountManager = accountManager;
        }

        private static bool TryFindSaveData(string titleName, ulong titleId, BlitStruct<ApplicationControlProperty> controlHolder, in SaveDataFilter filter, out ulong saveDataId)
        {
            saveDataId = default;

            Result result = _horizonClient.Fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, in filter);
            if (ResultFs.TargetNotFound.Includes(result))
            {
                ref ApplicationControlProperty control = ref controlHolder.Value;

                Logger.Info?.Print(LogClass.Application, $"Creating save directory for Title: {titleName} [{titleId:x16}]");

                if (controlHolder.ByteSpan.IsZeros())
                {
                    // If the current application doesn't have a loaded control property, create a dummy one
                    // and set the savedata sizes so a user savedata will be created.
                    control = ref new BlitStruct<ApplicationControlProperty>(1).Value;

                    // The set sizes don't actually matter as long as they're non-zero because we use directory savedata.
                    control.UserAccountSaveDataSize = 0x4000;
                    control.UserAccountSaveDataJournalSize = 0x4000;

                    Logger.Warning?.Print(LogClass.Application, "No control file was found for this game. Using a dummy one instead. This may cause inaccuracies in some games.");
                }

                Uid user = new((ulong)_accountManager.LastOpenedUser.UserId.High, (ulong)_accountManager.LastOpenedUser.UserId.Low);

                result = _horizonClient.Fs.EnsureApplicationSaveData(out _, new ApplicationId(titleId), in control, in user);
                if (result.IsFailure())
                {
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogMessageCreateSaveErrorMessage, result.ToStringWithName()));
                    });

                    return false;
                }

                // Try to find the savedata again after creating it
                result = _horizonClient.Fs.FindSaveDataWithFilter(out saveDataInfo, SaveDataSpaceId.User, in filter);
            }

            if (result.IsSuccess())
            {
                saveDataId = saveDataInfo.SaveDataId;

                return true;
            }

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogMessageFindSaveErrorMessage, result.ToStringWithName()));
            });

            return false;
        }

        public static void OpenSaveDir(in SaveDataFilter saveDataFilter, ulong titleId, BlitStruct<ApplicationControlProperty> controlData, string titleName)
        {
            if (!TryFindSaveData(titleName, titleId, controlData, in saveDataFilter, out ulong saveDataId))
            {
                return;
            }

            OpenSaveDir(saveDataId);
        }

        public static void OpenSaveDir(ulong saveDataId)
        {
            string saveRootPath = Path.Combine(VirtualFileSystem.GetNandPath(), $"user/save/{saveDataId:x16}");

            if (!Directory.Exists(saveRootPath))
            {
                // Inconsistent state. Create the directory
                Directory.CreateDirectory(saveRootPath);
            }

            string committedPath = Path.Combine(saveRootPath, "0");
            string workingPath = Path.Combine(saveRootPath, "1");

            // If the committed directory exists, that path will be loaded the next time the savedata is mounted
            if (Directory.Exists(committedPath))
            {
                OpenHelper.OpenFolder(committedPath);
            }
            else
            {
                // If the working directory exists and the committed directory doesn't,
                // the working directory will be loaded the next time the savedata is mounted
                if (!Directory.Exists(workingPath))
                {
                    Directory.CreateDirectory(workingPath);
                }

                OpenHelper.OpenFolder(workingPath);
            }
        }

        public static async Task ExtractSection(IStorageProvider storageProvider, NcaSectionType ncaSectionType, string titleFilePath, string titleName, int programIndex = 0)
        {
            var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = LocaleManager.Instance[LocaleKeys.FolderDialogExtractTitle],
                AllowMultiple = false,
            });

            if (result.Count == 0)
            {
                return;
            }

            var destination = result[0].Path.LocalPath;
            var cancellationToken = new CancellationTokenSource();

            UpdateWaitWindow waitingDialog = new(
                LocaleManager.Instance[LocaleKeys.DialogNcaExtractionTitle],
                LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogNcaExtractionMessage, ncaSectionType, Path.GetFileName(titleFilePath)),
                cancellationToken);

            Thread extractorThread = new(() =>
            {
                Dispatcher.UIThread.Post(waitingDialog.Show);

                using FileStream file = new(titleFilePath, FileMode.Open, FileAccess.Read);

                Nca mainNca = null;
                Nca patchNca = null;

                string extension = Path.GetExtension(titleFilePath).ToLower();
                if (extension == ".nsp" || extension == ".pfs0" || extension == ".xci")
                {
                    IFileSystem pfs;

                    if (extension == ".xci")
                    {
                        pfs = new Xci(_virtualFileSystem.KeySet, file.AsStorage()).OpenPartition(XciPartitionType.Secure);
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

                        Nca nca = new(_virtualFileSystem.KeySet, ncaFile.Get.AsStorage());
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
                    }
                }
                else if (extension == ".nca")
                {
                    mainNca = new Nca(_virtualFileSystem.KeySet, file.AsStorage());
                }

                if (mainNca == null)
                {
                    Logger.Error?.Print(LogClass.Application, "Extraction failure. The main NCA was not present in the selected file");

                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        waitingDialog.Close();

                        await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogNcaExtractionMainNcaNotFoundErrorMessage]);
                    });

                    return;
                }

                IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                    ? IntegrityCheckLevel.ErrorOnInvalid
                    : IntegrityCheckLevel.None;

                (Nca updatePatchNca, _) = mainNca.GetUpdateData(_virtualFileSystem, checkLevel, programIndex, out _);
                if (updatePatchNca != null)
                {
                    patchNca = updatePatchNca;
                }

                int index = Nca.GetSectionIndexFromType(ncaSectionType, mainNca.Header.ContentType);

                try
                {
                    bool sectionExistsInPatch = false;
                    if (patchNca != null)
                    {
                        sectionExistsInPatch = patchNca.CanOpenSection(index);
                    }

                    IFileSystem ncaFileSystem = sectionExistsInPatch ? mainNca.OpenFileSystemWithPatch(patchNca, index, IntegrityCheckLevel.ErrorOnInvalid)
                                                                     : mainNca.OpenFileSystem(index, IntegrityCheckLevel.ErrorOnInvalid);

                    FileSystemClient fsClient = _horizonClient.Fs;

                    string source = DateTime.Now.ToFileTime().ToString()[10..];
                    string output = DateTime.Now.ToFileTime().ToString()[10..];

                    using var uniqueSourceFs = new UniqueRef<IFileSystem>(ncaFileSystem);
                    using var uniqueOutputFs = new UniqueRef<IFileSystem>(new LocalFileSystem(destination));

                    fsClient.Register(source.ToU8Span(), ref uniqueSourceFs.Ref);
                    fsClient.Register(output.ToU8Span(), ref uniqueOutputFs.Ref);

                    (Result? resultCode, bool canceled) = CopyDirectory(fsClient, $"{source}:/", $"{output}:/", cancellationToken.Token);

                    if (!canceled)
                    {
                        if (resultCode.Value.IsFailure())
                        {
                            Logger.Error?.Print(LogClass.Application, $"LibHac returned error code: {resultCode.Value.ErrorCode}");

                            Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                waitingDialog.Close();

                                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogNcaExtractionCheckLogErrorMessage]);
                            });
                        }
                        else if (resultCode.Value.IsSuccess())
                        {
                            Dispatcher.UIThread.Post(waitingDialog.Close);

                            NotificationHelper.Show(
                                LocaleManager.Instance[LocaleKeys.DialogNcaExtractionTitle],
                                $"{titleName}\n\n{LocaleManager.Instance[LocaleKeys.DialogNcaExtractionSuccessMessage]}",
                                NotificationType.Information);
                        }
                    }

                    fsClient.Unmount(source.ToU8Span());
                    fsClient.Unmount(output.ToU8Span());
                }
                catch (ArgumentException ex)
                {
                    Logger.Error?.Print(LogClass.Application, $"{ex.Message}");

                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        waitingDialog.Close();

                        await ContentDialogHelper.CreateErrorDialog(ex.Message);
                    });
                }
            })
            {
                Name = "GUI.NcaSectionExtractorThread",
                IsBackground = true,
            };
            extractorThread.Start();
        }

        public static (Result? result, bool canceled) CopyDirectory(FileSystemClient fs, string sourcePath, string destPath, CancellationToken token)
        {
            Result rc = fs.OpenDirectory(out DirectoryHandle sourceHandle, sourcePath.ToU8Span(), OpenDirectoryMode.All);
            if (rc.IsFailure())
            {
                return (rc, false);
            }

            using (sourceHandle)
            {
                foreach (DirectoryEntryEx entry in fs.EnumerateEntries(sourcePath, "*", SearchOptions.Default))
                {
                    if (token.IsCancellationRequested)
                    {
                        return (null, true);
                    }

                    string subSrcPath = PathTools.Normalize(PathTools.Combine(sourcePath, entry.Name));
                    string subDstPath = PathTools.Normalize(PathTools.Combine(destPath, entry.Name));

                    if (entry.Type == DirectoryEntryType.Directory)
                    {
                        fs.EnsureDirectoryExists(subDstPath);

                        (Result? result, bool canceled) = CopyDirectory(fs, subSrcPath, subDstPath, token);
                        if (canceled || result.Value.IsFailure())
                        {
                            return (result, canceled);
                        }
                    }

                    if (entry.Type == DirectoryEntryType.File)
                    {
                        fs.CreateOrOverwriteFile(subDstPath, entry.Size);

                        rc = CopyFile(fs, subSrcPath, subDstPath);
                        if (rc.IsFailure())
                        {
                            return (rc, false);
                        }
                    }
                }
            }

            return (Result.Success, false);
        }

        public static Result CopyFile(FileSystemClient fs, string sourcePath, string destPath)
        {
            Result rc = fs.OpenFile(out FileHandle sourceHandle, sourcePath.ToU8Span(), OpenMode.Read);
            if (rc.IsFailure())
            {
                return rc;
            }

            using (sourceHandle)
            {
                rc = fs.OpenFile(out FileHandle destHandle, destPath.ToU8Span(), OpenMode.Write | OpenMode.AllowAppend);
                if (rc.IsFailure())
                {
                    return rc;
                }

                using (destHandle)
                {
                    const int MaxBufferSize = 1024 * 1024;

                    rc = fs.GetFileSize(out long fileSize, sourceHandle);
                    if (rc.IsFailure())
                    {
                        return rc;
                    }

                    int bufferSize = (int)Math.Min(MaxBufferSize, fileSize);

                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        for (long offset = 0; offset < fileSize; offset += bufferSize)
                        {
                            int toRead = (int)Math.Min(fileSize - offset, bufferSize);
                            Span<byte> buf = buffer.AsSpan(0, toRead);

                            rc = fs.ReadFile(out long _, sourceHandle, offset, buf);
                            if (rc.IsFailure())
                            {
                                return rc;
                            }

                            rc = fs.WriteFile(destHandle, offset, buf, WriteOption.None);
                            if (rc.IsFailure())
                            {
                                return rc;
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }

                    rc = fs.FlushFile(destHandle);
                    if (rc.IsFailure())
                    {
                        return rc;
                    }
                }
            }

            return Result.Success;
        }
    }
}
