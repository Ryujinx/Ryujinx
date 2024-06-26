using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Logging.Targets;
using System;
using System.IO;

namespace Ryujinx.UI.Common.Configuration
{
    public static class LoggerModule
    {
        public static void Initialize()
        {
            ConfigurationState.Shared.Logger.EnableDebug.Event += ReloadEnableDebug;
            ConfigurationState.Shared.Logger.EnableStub.Event += ReloadEnableStub;
            ConfigurationState.Shared.Logger.EnableInfo.Event += ReloadEnableInfo;
            ConfigurationState.Shared.Logger.EnableWarn.Event += ReloadEnableWarning;
            ConfigurationState.Shared.Logger.EnableError.Event += ReloadEnableError;
            ConfigurationState.Shared.Logger.EnableTrace.Event += ReloadEnableTrace;
            ConfigurationState.Shared.Logger.EnableGuest.Event += ReloadEnableGuest;
            ConfigurationState.Shared.Logger.EnableFsAccessLog.Event += ReloadEnableFsAccessLog;
            ConfigurationState.Shared.Logger.FilteredClasses.Event += ReloadFilteredClasses;
            ConfigurationState.Shared.Logger.EnableFileLog.Event += ReloadFileLogger;
        }

        private static void ReloadEnableDebug(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Debug, e.NewValue);
        }

        private static void ReloadEnableStub(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Stub, e.NewValue);
        }

        private static void ReloadEnableInfo(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Info, e.NewValue);
        }

        private static void ReloadEnableWarning(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Warning, e.NewValue);
        }

        private static void ReloadEnableError(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Error, e.NewValue);
        }

        private static void ReloadEnableTrace(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Trace, e.NewValue);
        }

        private static void ReloadEnableGuest(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.Guest, e.NewValue);
        }

        private static void ReloadEnableFsAccessLog(object sender, ReactiveEventArgs<bool> e)
        {
            Logger.SetEnable(LogLevel.AccessLog, e.NewValue);
        }

        private static void ReloadFilteredClasses(object sender, ReactiveEventArgs<LogClass[]> e)
        {
            bool noFilter = e.NewValue.Length == 0;

            foreach (var logClass in Enum.GetValues<LogClass>())
            {
                Logger.SetEnable(logClass, noFilter);
            }

            foreach (var logClass in e.NewValue)
            {
                Logger.SetEnable(logClass, true);
            }
        }

        private static void ReloadFileLogger(object sender, ReactiveEventArgs<bool> e)
        {
            if (e.NewValue)
            {
                string logDir = AppDataManager.LogsDirPath;
                FileStream logFile = null;

                if (!string.IsNullOrEmpty(logDir))
                {
                    logFile = FileLogTarget.PrepareLogFile(logDir);
                }

                if (logFile == null)
                {
                    Logger.Error?.Print(LogClass.Application, "No writable log directory available. Make sure either the Logs directory, Application Data, or the Ryujinx directory is writable.");
                    Logger.RemoveTarget("file");

                    return;
                }

                Logger.AddTarget(new AsyncLogTargetWrapper(
                    new FileLogTarget("file", logFile),
                    1000,
                    AsyncLogTargetOverflowAction.Block
                ));
            }
            else
            {
                Logger.RemoveTarget("file");
            }
        }
    }
}
