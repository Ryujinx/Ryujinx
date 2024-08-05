using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static long _buildSize;
        private const int ConnectionCount = 4;

        private static async Task DoUpdateWithMultipleThreads(TaskDialog taskDialog, string downloadUrl, string updateFile)
        {
            long chunkSize = _buildSize / ConnectionCount;
            long remainderChunk = _buildSize % ConnectionCount;

            int completedRequests = 0;
            int[] progressPercentage = new int[ConnectionCount];
            List<byte[]> chunkDataList = new List<byte[]>(new byte[ConnectionCount][]);

            List<Task> downloadTasks = new List<Task>();

            for (int i = 0; i < ConnectionCount; i++)
            {
                long rangeStart = i * chunkSize;
                long rangeEnd = (i == ConnectionCount - 1) ? (rangeStart + chunkSize + remainderChunk - 1) : (rangeStart + chunkSize - 1);
                int index = i;

                downloadTasks.Add(Task.Run(async () =>
                {
                    byte[] chunkData = await DownloadFileChunk(downloadUrl, rangeStart, rangeEnd, index, taskDialog, progressPercentage);
                    chunkDataList[index] = chunkData;

                    Interlocked.Increment(ref completedRequests);
                    if (Interlocked.Equals(completedRequests, ConnectionCount))
                    {
                        byte[] allData = CombineChunks(chunkDataList, _buildSize);
                        File.WriteAllBytes(updateFile, allData);

                        // On macOS, ensure that we remove the quarantine bit to prevent Gatekeeper from blocking execution.
                        if (OperatingSystem.IsMacOS())
                        {
                            using Process xattrProcess = Process.Start("xattr", new List<string> { "-d", "com.apple.quarantine", updateFile });

                            xattrProcess.WaitForExit();
                        }

                        // Ensure that the install update is run on the UI thread.
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            try
                            {
                                await InstallUpdate(taskDialog, updateFile);
                            }
                            catch (Exception e)
                            {
                                Logger.Warning?.Print(LogClass.Application, e.Message);
                                Logger.Warning?.Print(LogClass.Application, "Multi-Threaded update failed, falling back to single-threaded updater.");
                                await DoUpdateWithSingleThread(taskDialog, downloadUrl, updateFile);
                            }
                        });
                    }
                }));
            }

            await Task.WhenAll(downloadTasks);
        }

        private static byte[] CombineChunks(List<byte[]> chunks, long totalSize)
        {
            byte[] data = new byte[totalSize];
            long position = 0;
            foreach (byte[] chunk in chunks)
            {
                Buffer.BlockCopy(chunk, 0, data, (int)position, chunk.Length);
                position += chunk.Length;
            }
            return data;
        }

        private static async Task<byte[]> DownloadFileChunk(string url, long start, long end, int index, TaskDialog taskDialog, int[] progressPercentage)
        {
            Memory<byte> buffer = new byte[8192];

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(start, end);
            HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var memoryStream = new MemoryStream();
            int bytesRead;
            long totalRead = 0;
            int lastReportedProgress = -1;

            while ((bytesRead = await stream.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                memoryStream.Write(buffer.Span[..bytesRead]);
                totalRead += bytesRead;
                int progress = (int)((totalRead * 100) / (end - start + 1));
                progressPercentage[index] = progress;

                // Throttle UI updates to only fire when there is a change in progress percentage
                if (progress != lastReportedProgress)
                {
                    lastReportedProgress = progress;
                    Dispatcher.UIThread.Post(() =>
                    {
                        taskDialog.SetProgressBarState(progressPercentage.Sum() / ConnectionCount, TaskDialogProgressState.Normal);
                    });
                }
            }

            return memoryStream.ToArray();
        }
    }
}
