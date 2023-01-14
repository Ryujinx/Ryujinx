using Microsoft.Win32;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Ui.Common.Helper
{
    public static partial class FileAssociationHelper
    {
        private const int SHCNE_ASSOCCHANGED = 0x8000000;
        private const int SHCNF_FLUSH        = 0x1000;

        [LibraryImport("shell32.dll", SetLastError = true)]
        public static partial void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static bool IsTypeAssociationSupported => (OperatingSystem.IsLinux() || OperatingSystem.IsWindows()) &&
                                                         !ReleaseInformation.IsFlatHubBuild();

        [SupportedOSPlatform("linux")]
        private static bool RegisterLinuxMimeTypes()
        {
            string mimeDbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "mime");

            if (!File.Exists(Path.Combine(mimeDbPath, "packages", "Ryujinx.xml")))
            {
                string mimeTypesFile = Path.Combine(ReleaseInformation.GetBaseApplicationDirectory(), "mime", "Ryujinx.xml");
                using Process mimeProcess = new();

                mimeProcess.StartInfo.FileName = "xdg-mime";
                mimeProcess.StartInfo.Arguments = $"install --novendor --mode user {mimeTypesFile}";

                mimeProcess.Start();
                mimeProcess.WaitForExit();

                if (mimeProcess.ExitCode != 0)
                {
                    Logger.Error?.PrintMsg(LogClass.Application, $"Unable to install mime types. Make sure xdg-utils is installed. Process exited with code: {mimeProcess.ExitCode}");
                    return false;
                }

                using Process updateMimeProcess = new();

                updateMimeProcess.StartInfo.FileName = "update-mime-database";
                updateMimeProcess.StartInfo.Arguments = mimeDbPath;

                updateMimeProcess.Start();
                updateMimeProcess.WaitForExit();

                if (updateMimeProcess.ExitCode != 0)
                {
                    Logger.Error?.PrintMsg(LogClass.Application, $"Could not update local mime database. Process exited with code: {updateMimeProcess.ExitCode}");
                }
            }

            return true;
        }

        [SupportedOSPlatform("windows")]
        private static bool RegisterWindowsMimeTypes()
        {
            static bool RegisterExtension(string ext)
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@$"Software\Classes\{ext}");

                if (key is null)
                {
                    return false;
                }

                key!.CreateSubKey(@"shell\open\command")!.SetValue("", $"\"{Environment.ProcessPath}\" \"%1\"");
                key.Close();

                // Notify Explorer the file association has been changed.
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero);

                return true;
            }

            bool registered = false;

            foreach (string ext in new string[] { ".nca", ".nro", ".nso", ".nsp", ".xci" })
            {
                registered |= RegisterExtension(ext);
            }

            return registered;
        }

        public static bool RegisterTypeAssociations()
        {
            if (OperatingSystem.IsLinux())
            {
                return RegisterLinuxMimeTypes();
            }

            if (OperatingSystem.IsWindows())
            {
                return RegisterWindowsMimeTypes();
            }

            // TODO: Add macOS support.

            return false;
        }
    }
}