using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void ExtractTarGzipFile(TaskDialog taskDialog, string archivePath, string outputDirectoryPath)
        {
            using Stream inStream = File.OpenRead(archivePath);
            using GZipInputStream gzipStream = new(inStream);
            using TarInputStream tarStream = new(gzipStream, Encoding.ASCII);

            TarEntry tarEntry;

            while ((tarEntry = tarStream.GetNextEntry()) is not null)
            {
                if (tarEntry.IsDirectory)
                {
                    continue;
                }

                string outPath = Path.Combine(outputDirectoryPath, tarEntry.Name);

                Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                using FileStream outStream = File.OpenWrite(outPath);
                tarStream.CopyEntryContents(outStream);

                File.SetUnixFileMode(outPath, (UnixFileMode)tarEntry.TarHeader.Mode);
                File.SetLastWriteTime(outPath, DateTime.SpecifyKind(tarEntry.ModTime, DateTimeKind.Utc));

                Dispatcher.UIThread.Post(() =>
                {
                    if (tarEntry is null)
                    {
                        return;
                    }

                    taskDialog.SetProgressBarState(GetPercentage(tarEntry.Size, inStream.Length), TaskDialogProgressState.Normal);
                });
            }
        }
    }
}
