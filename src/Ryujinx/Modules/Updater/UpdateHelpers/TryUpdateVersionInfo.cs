using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.UI.Common.Helper;
using Ryujinx.UI.Common.Models.Github;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static readonly GithubReleasesJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        private static async Task<bool> TryUpdateVersionInfo(string buildInfoUrl, bool showVersionUpToDate)
        {
            try
            {
                HttpResponseMessage response = await SendAsyncWithHeaders(buildInfoUrl);
                string fetchedJson = await response.Content.ReadAsStringAsync();
                var fetched = JsonHelper.Deserialize(fetchedJson, _serializerContext.GithubReleasesJsonResponse);
                _buildVer = fetched.Name;

                foreach (var asset in fetched.Assets)
                {
                    if (asset.Name.StartsWith("ryujinx") && asset.Name.EndsWith(_platformExt) && asset.State == "uploaded")
                    {
                        _buildUrl = asset.BrowserDownloadUrl;
                        return true;
                    }
                }

                if (_buildUrl == null && showVersionUpToDate)
                {
                    await ContentDialogHelper.CreateUpdaterInfoDialog(
                        LocaleManager.Instance[LocaleKeys.DialogUpdaterAlreadyOnLatestVersionMessage], "");
                }

                _running = false;
                return false;
            }
            catch (Exception exception)
            {
                Logger.Error?.Print(LogClass.Application, exception.Message);
                await ContentDialogHelper.CreateErrorDialog(
                    LocaleManager.Instance[LocaleKeys.DialogUpdaterFailedToGetVersionMessage]);
                _running = false;
                return false;
            }
        }
    }
}
