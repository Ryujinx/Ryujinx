using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using ShellLink;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace Ryujinx.UI.Common.Helper
{
    public static class ShortcutHelper
    {
        [SupportedOSPlatform("windows")]
        private static void CreateShortcutWindows(string applicationFilePath, string applicationId, byte[] iconData, string iconPath, string cleanedAppName, string desktopPath)
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".exe");
            iconPath += ".ico";

            MemoryStream iconDataStream = new(iconData);
            var image = Image.Load(iconDataStream);
            image.Mutate(x => x.Resize(128, 128));
            SaveBitmapAsIcon(image, iconPath);

            var shortcut = Shortcut.CreateShortcut(basePath, GetArgsString(applicationFilePath, applicationId), iconPath, 0);
            shortcut.StringData.NameString = cleanedAppName;
            shortcut.WriteToFile(Path.Combine(desktopPath, cleanedAppName + ".lnk"));
        }

        [SupportedOSPlatform("linux")]
        private static void CreateShortcutLinux(string applicationFilePath, string applicationId, byte[] iconData, string iconPath, string desktopPath, string cleanedAppName)
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ryujinx.sh");
            var desktopFile = EmbeddedResources.ReadAllText("Ryujinx.UI.Common/shortcut-template.desktop");
            iconPath += ".png";

            var image = Image.Load<Rgba32>(iconData);
            image.SaveAsPng(iconPath);

            using StreamWriter outputFile = new(Path.Combine(desktopPath, cleanedAppName + ".desktop"));
            outputFile.Write(desktopFile, cleanedAppName, iconPath, $"{basePath} {GetArgsString(applicationFilePath, applicationId)}");
        }

        [SupportedOSPlatform("macos")]
        private static void CreateShortcutMacos(string appFilePath, string applicationId, byte[] iconData, string desktopPath, string cleanedAppName)
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ryujinx");
            var plistFile = EmbeddedResources.ReadAllText("Ryujinx.UI.Common/shortcut-template.plist");
            var shortcutScript = EmbeddedResources.ReadAllText("Ryujinx.UI.Common/shortcut-launch-script.sh");
            // Macos .App folder
            string contentFolderPath = Path.Combine("/Applications", cleanedAppName + ".app", "Contents");
            string scriptFolderPath = Path.Combine(contentFolderPath, "MacOS");

            if (!Directory.Exists(scriptFolderPath))
            {
                Directory.CreateDirectory(scriptFolderPath);
            }

            // Runner script
            const string ScriptName = "runner.sh";
            string scriptPath = Path.Combine(scriptFolderPath, ScriptName);
            using StreamWriter scriptFile = new(scriptPath);

            scriptFile.Write(shortcutScript, basePath, GetArgsString(appFilePath, applicationId));

            // Set execute permission
            FileInfo fileInfo = new(scriptPath);
            fileInfo.UnixFileMode |= UnixFileMode.UserExecute;

            // img
            string resourceFolderPath = Path.Combine(contentFolderPath, "Resources");
            if (!Directory.Exists(resourceFolderPath))
            {
                Directory.CreateDirectory(resourceFolderPath);
            }

            const string IconName = "icon.png";
            var image = Image.Load<Rgba32>(iconData);
            image.SaveAsPng(Path.Combine(resourceFolderPath, IconName));

            // plist file
            using StreamWriter outputFile = new(Path.Combine(contentFolderPath, "Info.plist"));
            outputFile.Write(plistFile, ScriptName, cleanedAppName, IconName);
        }

        public static void CreateAppShortcut(string applicationFilePath, string applicationName, string applicationId, byte[] iconData)
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string cleanedAppName = string.Join("_", applicationName.Split(Path.GetInvalidFileNameChars()));

            if (OperatingSystem.IsWindows())
            {
                string iconPath = Path.Combine(AppDataManager.BaseDirPath, "games", applicationId, "app");

                CreateShortcutWindows(applicationFilePath, applicationId, iconData, iconPath, cleanedAppName, desktopPath);

                return;
            }

            if (OperatingSystem.IsLinux())
            {
                string iconPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "icons", "Ryujinx");

                Directory.CreateDirectory(iconPath);
                CreateShortcutLinux(applicationFilePath, applicationId, iconData, Path.Combine(iconPath, applicationId), desktopPath, cleanedAppName);

                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                CreateShortcutMacos(applicationFilePath, applicationId, iconData, desktopPath, cleanedAppName);

                return;
            }

            throw new NotImplementedException("Shortcut support has not been implemented yet for this OS.");
        }

        private static string GetArgsString(string appFilePath, string applicationId)
        {
            // args are first defined as a list, for easier adjustments in the future
            var argsList = new List<string>();

            if (!string.IsNullOrEmpty(CommandLineState.BaseDirPathArg))
            {
                argsList.Add("--root-data-dir");
                argsList.Add($"\"{CommandLineState.BaseDirPathArg}\"");
            }

            if (appFilePath.ToLower().EndsWith(".xci"))
            {
                argsList.Add("--application-id");
                argsList.Add($"\"{applicationId}\"");
            }

            argsList.Add($"\"{appFilePath}\"");

            return String.Join(" ", argsList);
        }

        /// <summary>
        /// Creates a Icon (.ico) file using the source bitmap image at the specified file path.
        /// </summary>
        /// <param name="source">The source bitmap image that will be saved as an .ico file</param>
        /// <param name="filePath">The location that the new .ico file will be saved too (Make sure to include '.ico' in the path).</param>
        [SupportedOSPlatform("windows")]
        private static void SaveBitmapAsIcon(Image source, string filePath)
        {
            // Code Modified From https://stackoverflow.com/a/11448060/368354 by Benlitz
            byte[] header = { 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 32, 0, 0, 0, 0, 0, 22, 0, 0, 0 };
            using FileStream fs = new(filePath, FileMode.Create);

            fs.Write(header);
            // Writing actual data
            source.Save(fs, PngFormat.Instance);
            // Getting data length (file length minus header)
            long dataLength = fs.Length - header.Length;
            // Write it in the correct place
            fs.Seek(14, SeekOrigin.Begin);
            fs.WriteByte((byte)dataLength);
            fs.WriteByte((byte)(dataLength >> 8));
        }
    }
}
