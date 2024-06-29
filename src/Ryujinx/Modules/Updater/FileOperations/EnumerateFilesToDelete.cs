using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Ryujinx.Modules
{
    internal static partial class Updater
    {
        private static readonly string[] _windowsDependencyDirs = Array.Empty<string>();

        // NOTE: This method should always reflect the latest build layout.
        private static IEnumerable<string> EnumerateFilesToDelete()
        {
            var files = Directory.EnumerateFiles(_homeDir); // All files directly in base dir.

            // Determine and exclude user files only when the updater is running, not when cleaning old files
            if (_running && !OperatingSystem.IsMacOS())
            {
                // Compare the loose files in base directory against the loose files from the incoming update, and store foreign ones in a user list.
                var oldFiles = Directory.EnumerateFiles(_homeDir, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName);
                var newFiles = Directory.EnumerateFiles(_updatePublishDir, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName);
                var userFiles = oldFiles.Except(newFiles).Select(filename => Path.Combine(_homeDir, filename));

                // Remove user files from the paths in files.
                files = files.Except(userFiles);
            }

            if (OperatingSystem.IsWindows())
            {
                foreach (string dir in _windowsDependencyDirs)
                {
                    string dirPath = Path.Combine(_homeDir, dir);
                    if (Directory.Exists(dirPath))
                    {
                        files = files.Concat(Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories));
                    }
                }
            }

            return files.Where(f => !new FileInfo(f).Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System));
        }
    }
}
