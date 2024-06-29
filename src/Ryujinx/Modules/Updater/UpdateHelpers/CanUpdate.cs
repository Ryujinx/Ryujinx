using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common;
using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        public static bool CanUpdate(bool showWarnings)
        {
#if !DISABLE_UPDATER
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                if (showWarnings)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                        ContentDialogHelper.CreateWarningDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterNoInternetMessage],
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterNoInternetSubMessage])
                    );
                }

                return false;
            }

            if (Program.Version.Contains("dirty") || !ReleaseInformation.IsValid)
            {
                if (showWarnings)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                        ContentDialogHelper.CreateWarningDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterDirtyBuildMessage],
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterDirtyBuildSubMessage])
                    );
                }

                return false;
            }

            return true;
#else
            if (showWarnings)
            {
                if (ReleaseInformation.IsFlatHubBuild)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                        ContentDialogHelper.CreateWarningDialog(
                            LocaleManager.Instance[LocaleKeys.UpdaterDisabledWarningTitle],
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterFlatpakNotSupportedMessage])
                    );
                }
                else
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                        ContentDialogHelper.CreateWarningDialog(
                            LocaleManager.Instance[LocaleKeys.UpdaterDisabledWarningTitle],
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterDirtyBuildSubMessage])
                    );
                }
            }

            return false;
#endif
        }
    }
}
