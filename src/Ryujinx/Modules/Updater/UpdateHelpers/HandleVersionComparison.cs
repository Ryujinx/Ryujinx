using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Helper;
using System;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static async Task<bool> HandleVersionComparison(Version currentVersion, bool showVersionUpToDate)
        {
            try
            {
                Version newVersion = Version.Parse(_buildVer);
                if (newVersion <= currentVersion)
                {
                    if (showVersionUpToDate)
                    {
                        await ContentDialogHelper.CreateUpdaterInfoDialog(
                            LocaleManager.Instance[LocaleKeys.DialogUpdaterAlreadyOnLatestVersionMessage], "");
                    }

                    _running = false;
                    return false;
                }

                return true;
            }
            catch
            {
                Logger.Error?.Print(LogClass.Application, "Failed to convert the received Ryujinx version from Github!");
                await ContentDialogHelper.CreateWarningDialog(
                    LocaleManager.Instance[LocaleKeys.DialogUpdaterConvertFailedGithubMessage],
                    LocaleManager.Instance[LocaleKeys.DialogUpdaterCancelUpdateMessage]);
                _running = false;
                return false;
            }
        }
    }
}
