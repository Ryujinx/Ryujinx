using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static bool _updateSuccessful;

        private static async Task UpdateRyujinx(Window parent, string downloadUrl)
        {
            _updateSuccessful = false;

            // Empty update dir, although it shouldn't ever have anything inside it
            if (Directory.Exists(_updateDir))
            {
                Directory.Delete(_updateDir, true);
            }

            Directory.CreateDirectory(_updateDir);

            string updateFile = Path.Combine(_updateDir, "update.bin");

            TaskDialog taskDialog = new()
            {
                Header = LocaleManager.Instance[LocaleKeys.RyujinxUpdater],
                SubHeader = LocaleManager.Instance[LocaleKeys.UpdaterDownloading],
                IconSource = new SymbolIconSource { Symbol = Symbol.Download },
                ShowProgressBar = true,
                XamlRoot = parent,
            };

            taskDialog.Opened += async (s, e) =>
            {
                if (_buildSize >= 0)
                {
                    await DoUpdateWithMultipleThreads(taskDialog, downloadUrl, updateFile);
                }
                else
                {
                    await DoUpdateWithSingleThread(taskDialog, downloadUrl, updateFile);
                }
            };

            await taskDialog.ShowAsync(true);

            if (_updateSuccessful)
            {
                bool shouldRestart = true;

                if (!OperatingSystem.IsMacOS())
                {
                    shouldRestart = await ContentDialogHelper.CreateChoiceDialog(
                        LocaleManager.Instance[LocaleKeys.RyujinxUpdater],
                        LocaleManager.Instance[LocaleKeys.DialogUpdaterCompleteMessage],
                        LocaleManager.Instance[LocaleKeys.DialogUpdaterRestartMessage]);
                }

                if (shouldRestart)
                {
                    RestartApplication(parent);
                }
            }
        }
    }
}
