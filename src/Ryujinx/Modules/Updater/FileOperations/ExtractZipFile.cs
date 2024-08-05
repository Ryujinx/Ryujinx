using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static void ExtractZipFile(TaskDialog taskDialog, string archivePath, string outputDirectoryPath)
        {
            using Stream inStream = File.OpenRead(archivePath);
            using ZipFile zipFile = new(inStream);

            double count = 0;
            foreach (ZipEntry zipEntry in zipFile)
            {
                count++;
                if (zipEntry.IsDirectory)
                {
                    continue;
                }

                string outPath = Path.Combine(outputDirectoryPath, zipEntry.Name);

                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                using Stream zipStream = zipFile.GetInputStream(zipEntry);
                using FileStream outStream = File.OpenWrite(outPath);

                zipStream.CopyTo(outStream);

                File.SetLastWriteTime(outPath, DateTime.SpecifyKind(zipEntry.DateTime, DateTimeKind.Utc));

                Dispatcher.UIThread.Post(() =>
                {
                    taskDialog.SetProgressBarState(GetPercentage(count, zipFile.Count), TaskDialogProgressState.Normal);
                });
            }
        }
    }
}
