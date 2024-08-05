using Ryujinx.Common.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static string _buildUrl;

        // Fetch build size information to learn chunk sizes.
        private static async Task FetchBuildSizeInfo()
        {
            try
            {
                HttpResponseMessage message = await SendAsyncWithHeaders(_buildUrl, new RangeHeaderValue(0, 0));
                _buildSize = message.Content.Headers.ContentRange.Length.Value;
            }
            catch (Exception ex)
            {
                Logger.Warning?.Print(LogClass.Application, ex.Message);
                Logger.Warning?.Print(LogClass.Application, "Couldn't determine build size for update, using single-threaded updater");
                _buildSize = -1;
            }
        }
    }
}
