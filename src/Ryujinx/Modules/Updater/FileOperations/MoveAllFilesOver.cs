using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using System;
using System.IO;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static void MoveAllFilesOver(string root, string dest, TaskDialog taskDialog)
        {
            int total = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length;
            foreach (string directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);

                if (!Directory.Exists(Path.Combine(dest, dirName)))
                {
                    Directory.CreateDirectory(Path.Combine(dest, dirName));
                }

                MoveAllFilesOver(directory, Path.Combine(dest, dirName), taskDialog);
            }

            double count = 0;
            foreach (string file in Directory.GetFiles(root))
            {
                count++;

                File.Move(file, Path.Combine(dest, Path.GetFileName(file)), true);

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    taskDialog.SetProgressBarState(GetPercentage(count, total), TaskDialogProgressState.Normal);
                });
            }
        }
    }
}
