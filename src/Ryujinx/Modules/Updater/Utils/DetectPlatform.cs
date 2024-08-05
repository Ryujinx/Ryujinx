using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static string _platformExt;

        private static void DetectPlatform()
        {
            if (OperatingSystem.IsMacOS())
            {
                _platformExt = "macos_universal.app.tar.gz";
            }
            else if (OperatingSystem.IsWindows())
            {
                _platformExt = "win_x64.zip";
            }
            else if (OperatingSystem.IsLinux())
            {
                var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
                _platformExt = $"linux_{arch}.tar.gz";
            }
        }
    }
}
