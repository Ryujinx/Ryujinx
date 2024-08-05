using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static async Task<HttpResponseMessage> SendAsyncWithHeaders(string url, RangeHeaderValue range = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (range != null)
            {
                request.Headers.Range = range;
            }
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        }
    }
}
