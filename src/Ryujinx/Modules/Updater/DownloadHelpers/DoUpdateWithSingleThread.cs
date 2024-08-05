using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static async Task DoUpdateWithSingleThread(TaskDialog taskDialog, string downloadUrl, string updateFile)
        {
            // We do not want to timeout while downloading
            _httpClient.Timeout = TimeSpan.FromDays(1);

            HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error?.Print(LogClass.Application, $"Failed to download file: {response.ReasonPhrase}");
                throw new HttpRequestException($"Failed to download file: {response.ReasonPhrase}");
            }

            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            long byteWritten = 0;

            // Ensure the entire content body is read asynchronously
            using Stream remoteFileStream = await response.Content.ReadAsStreamAsync();
            using Stream updateFileStream = File.Open(updateFile, FileMode.Create);

            Memory<byte> buffer = new byte[32 * 1024];
            int readSize;

            while ((readSize = await remoteFileStream.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                updateFileStream.Write(buffer.Span[..readSize]);
                byteWritten += readSize;

                Dispatcher.UIThread.Post(() =>
                {
                    taskDialog.SetProgressBarState(GetPercentage(byteWritten, totalBytes), TaskDialogProgressState.Normal);
                });
            }

            await InstallUpdate(taskDialog, updateFile);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double GetPercentage(double value, double max)
        {
            return max == 0 ? 0 : value / max * 100;
        }
    }
}
