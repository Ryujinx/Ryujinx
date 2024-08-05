using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static async Task InstallUpdate(TaskDialog taskDialog, string updateFile)
        {
            // Extract Update
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                taskDialog.SubHeader = LocaleManager.Instance[LocaleKeys.UpdaterExtracting];
                taskDialog.SetProgressBarState(0, TaskDialogProgressState.Normal);
            });

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                ExtractTarGzipFile(taskDialog, updateFile, _updateDir);
            }
            else if (OperatingSystem.IsWindows())
            {
                ExtractZipFile(taskDialog, updateFile, _updateDir);
            }
            else
            {
                throw new NotSupportedException();
            }

            // Delete downloaded zip
            File.Delete(updateFile);

            List<string> allFiles = EnumerateFilesToDelete().ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                taskDialog.SubHeader = LocaleManager.Instance[LocaleKeys.UpdaterRenaming];
                taskDialog.SetProgressBarState(0, TaskDialogProgressState.Normal);
                taskDialog.Hide();
            });

            // NOTE: On macOS, replacement is delayed to the restart phase.
            if (!OperatingSystem.IsMacOS())
            {
                // Replace old files
                double count = 0;
                foreach (string file in allFiles)
                {
                    count++;
                    try
                    {
                        File.Move(file, file + ".ryuold");

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            taskDialog.SetProgressBarState(GetPercentage(count, allFiles.Count), TaskDialogProgressState.Normal);
                        });
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.UpdaterRenameFailed, file));
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    taskDialog.SubHeader = LocaleManager.Instance[LocaleKeys.UpdaterAddingFiles];
                    taskDialog.SetProgressBarState(0, TaskDialogProgressState.Normal);
                });

                MoveAllFilesOver(_updatePublishDir, _homeDir, taskDialog);

                Directory.Delete(_updateDir, true);
            }

            _updateSuccessful = true;

            taskDialog.Hide();
        }
    }
}
