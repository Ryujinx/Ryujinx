using Avalonia.Controls;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static void RestartApplication(Window parent)
        {
            List<string> arguments = CommandLineState.Arguments.ToList();
            string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (OperatingSystem.IsMacOS())
            {
                string baseBundlePath = Path.GetFullPath(Path.Combine(executableDirectory, "..", ".."));
                string newBundlePath = Path.Combine(_updateDir, "Ryujinx.app");
                string updaterScriptPath = Path.Combine(newBundlePath, "Contents", "Resources", "updater.sh");
                string currentPid = Environment.ProcessId.ToString();

                arguments.InsertRange(0, new List<string> { updaterScriptPath, baseBundlePath, newBundlePath, currentPid });
                Process.Start("/bin/bash", arguments);
            }
            else
            {
                string ryuName = Path.GetFileName(Environment.ProcessPath) ?? string.Empty;

                // Migration: Start the updated binary.
                // TODO: Remove this in a future update.
                if (ryuName.StartsWith("Ryujinx.Ava"))
                {
                    ryuName = ryuName.Replace(".Ava", "");
                }

                if (ryuName.EndsWith(".ryuold"))
                {
                    ryuName = ryuName[..^7];
                }

                // Fallback if the executable could not be found.
                if (ryuName.Length == 0 || !Path.Exists(Path.Combine(executableDirectory, ryuName)))
                {
                    ryuName = OperatingSystem.IsWindows() ? "Ryujinx.exe" : "Ryujinx";
                }

                ProcessStartInfo processStart = new(ryuName)
                {
                    UseShellExecute = true,
                    WorkingDirectory = executableDirectory,
                };

                foreach (string argument in arguments)
                {
                    processStart.ArgumentList.Add(argument);
                }

                Process.Start(processStart);
            }

            Environment.Exit(0);
        }
    }
}
