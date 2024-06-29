using Ryujinx.UI.Common.Helper;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        public static void CleanupUpdate()
        {
            foreach (string file in Directory.GetFiles(_homeDir, "*.ryuold", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }

            // Migration: Delete old Ryujinx binary.
            // TODO: Remove this in a future update.
            if (!OperatingSystem.IsMacOS())
            {
                string[] oldRyuFiles = Directory.GetFiles(_homeDir, "Ryujinx.Ava*", SearchOption.TopDirectoryOnly);
                // Assume we are running the new one if the process path is not available.
                // This helps to prevent an infinite loop of restarts.
                string currentRyuName = Path.GetFileName(Environment.ProcessPath) ?? (OperatingSystem.IsWindows() ? "Ryujinx.exe" : "Ryujinx");

                string newRyuName = Path.Combine(_homeDir, currentRyuName.Replace(".Ava", ""));
                if (!currentRyuName.Contains("Ryujinx.Ava"))
                {
                    foreach (string oldRyuFile in oldRyuFiles)
                    {
                        File.Delete(oldRyuFile);
                    }
                }
                // Should we be running the old binary, start the new one if possible.
                else if (File.Exists(newRyuName))
                {
                    ProcessStartInfo processStart = new(newRyuName)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = _homeDir,
                    };

                    foreach (string argument in CommandLineState.Arguments)
                    {
                        processStart.ArgumentList.Add(argument);
                    }

                    Process.Start(processStart);

                    Environment.Exit(0);
                }
            }
        }
    }
}
