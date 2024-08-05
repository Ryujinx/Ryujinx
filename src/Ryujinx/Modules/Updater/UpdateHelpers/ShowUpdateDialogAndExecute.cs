using Avalonia.Controls;
using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Helper;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static async Task ShowUpdateDialogAndExecute(Window mainWindow)
        {
            var shouldUpdate = await ContentDialogHelper.CreateChoiceDialog(
                LocaleManager.Instance[LocaleKeys.RyujinxUpdater],
                LocaleManager.Instance[LocaleKeys.RyujinxUpdaterMessage],
                $"{Program.Version} -> {_buildVer}");

            if (shouldUpdate)
            {
                await UpdateRyujinx(mainWindow, _buildUrl);
            }
            else
            {
                _running = false;
            }
        }
    }
}
