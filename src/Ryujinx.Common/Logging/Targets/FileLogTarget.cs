using Ryujinx.Common.Logging.Formatters;
using System;
using System.IO;
using System.Linq;

namespace Ryujinx.Common.Logging.Targets
{
    public class FileLogTarget : ILogTarget
    {
        private readonly StreamWriter _logWriter;
        private readonly DefaultLogFormatter _formatter;
        private readonly string _name;

        string ILogTarget.Name { get => _name; }

        public FileLogTarget(string name, FileStream fileStream)
        {
            _name = name;
            _logWriter = new StreamWriter(fileStream);
            _formatter = new DefaultLogFormatter();
        }

        public static FileStream PrepareLogFile(string path)
        {
            // Ensure directory is present
            DirectoryInfo logDir;
            try
            {
                logDir = new DirectoryInfo(path);
            }
            catch (ArgumentException exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Logging directory path ('{path}') was invalid: {exception}");

                return null;
            }

            try
            {
                logDir.Create();
            }
            catch (IOException exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}': {exception}");

                return null;
            }

            // Clean up old logs, should only keep 3
            FileInfo[] files = logDir.GetFiles("*.log").OrderBy((info => info.CreationTime)).ToArray();
            for (int i = 0; i < files.Length - 2; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch (UnauthorizedAccessException exception)
                {
                    Logger.Warning?.Print(LogClass.Application, $"Old log file could not be deleted '{files[i].FullName}': {exception}");

                    return null;
                }
                catch (IOException exception)
                {
                    Logger.Warning?.Print(LogClass.Application, $"Old log file could not be deleted '{files[i].FullName}': {exception}");

                    return null;
                }
            }

            string version = ReleaseInformation.Version;

            // Get path for the current time
            path = Path.Combine(logDir.FullName, $"Ryujinx_{version}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

            try
            {
                return File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Log file could not be created '{path}': {exception}");

                return null;
            }
            catch (IOException exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Log file could not be created '{path}': {exception}");

                return null;
            }
        }

        public void Log(object sender, LogEventArgs args)
        {
            _logWriter.WriteLine(_formatter.Format(args));
            _logWriter.Flush();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _logWriter.WriteLine("---- End of Log ----");
            _logWriter.Flush();
            _logWriter.Dispose();
        }
    }
}
