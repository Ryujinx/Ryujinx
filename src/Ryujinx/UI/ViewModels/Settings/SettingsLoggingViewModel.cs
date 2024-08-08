using Ryujinx.Common.Configuration;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsLoggingViewModel : BaseModel
    {
        public event Action DirtyEvent;

        private bool _enableFileLog;
        public bool EnableFileLog
        {
            get => _enableFileLog;
            set
            {
                _enableFileLog = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableStub;
        public bool EnableStub
        {
            get => _enableStub;
            set
            {
                _enableStub = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableInfo;
        public bool EnableInfo
        {
            get => _enableInfo;
            set
            {
                _enableInfo = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableWarn;
        public bool EnableWarn
        {
            get => _enableWarn;
            set
            {
                _enableWarn = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableError;
        public bool EnableError
        {
            get => _enableError;
            set
            {
                _enableError = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableTrace;
        public bool EnableTrace
        {
            get => _enableTrace;
            set
            {
                _enableTrace = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableGuest;
        public bool EnableGuest
        {
            get => _enableGuest;
            set
            {
                _enableGuest = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableFsAccessLog;
        public bool EnableFsAccessLog
        {
            get => _enableFsAccessLog;
            set
            {
                _enableFsAccessLog = value;
                DirtyEvent?.Invoke();
            }
        }

        private bool _enableDebug;
        public bool EnableDebug
        {
            get => _enableDebug;
            set
            {
                _enableDebug = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _fsGlobalAccessLogMode;
        public int FsGlobalAccessLogMode
        {
            get => _fsGlobalAccessLogMode;
            set
            {
                _fsGlobalAccessLogMode = value;
                DirtyEvent?.Invoke();
            }
        }

        private int _openglDebugLevel;
        public int OpenglDebugLevel
        {
            get => _openglDebugLevel;
            set
            {
                _openglDebugLevel = value;
                DirtyEvent?.Invoke();
            }
        }

        public SettingsLoggingViewModel()
        {
            ConfigurationState config = ConfigurationState.Instance;

            EnableFileLog = config.Logger.EnableFileLog;
            EnableStub = config.Logger.EnableStub;
            EnableInfo = config.Logger.EnableInfo;
            EnableWarn = config.Logger.EnableWarn;
            EnableError = config.Logger.EnableError;
            EnableTrace = config.Logger.EnableTrace;
            EnableGuest = config.Logger.EnableGuest;
            EnableDebug = config.Logger.EnableDebug;
            EnableFsAccessLog = config.Logger.EnableFsAccessLog;
            FsGlobalAccessLogMode = config.System.FsGlobalAccessLogMode;
            OpenglDebugLevel = (int)config.Logger.GraphicsDebugLevel.Value;
        }

        public bool CheckIfModified(ConfigurationState config)
        {
            bool isDirty = false;

            isDirty |= config.Logger.EnableFileLog.Value != EnableFileLog;
            isDirty |= config.Logger.EnableStub.Value != EnableStub;
            isDirty |= config.Logger.EnableInfo.Value != EnableInfo;
            isDirty |= config.Logger.EnableWarn.Value != EnableWarn;
            isDirty |= config.Logger.EnableError.Value != EnableError;
            isDirty |= config.Logger.EnableTrace.Value != EnableTrace;
            isDirty |= config.Logger.EnableGuest.Value != EnableGuest;
            isDirty |= config.Logger.EnableDebug.Value != EnableDebug;
            isDirty |= config.Logger.EnableFsAccessLog.Value != EnableFsAccessLog;
            isDirty |= config.System.FsGlobalAccessLogMode.Value != FsGlobalAccessLogMode;
            isDirty |= config.Logger.GraphicsDebugLevel.Value != (GraphicsDebugLevel)OpenglDebugLevel;

            return isDirty;
        }

        public void Save(ConfigurationState config)
        {
            config.Logger.EnableFileLog.Value = EnableFileLog;
            config.Logger.EnableStub.Value = EnableStub;
            config.Logger.EnableInfo.Value = EnableInfo;
            config.Logger.EnableWarn.Value = EnableWarn;
            config.Logger.EnableError.Value = EnableError;
            config.Logger.EnableTrace.Value = EnableTrace;
            config.Logger.EnableGuest.Value = EnableGuest;
            config.Logger.EnableDebug.Value = EnableDebug;
            config.Logger.EnableFsAccessLog.Value = EnableFsAccessLog;
            config.System.FsGlobalAccessLogMode.Value = FsGlobalAccessLogMode;
            config.Logger.GraphicsDebugLevel.Value = (GraphicsDebugLevel)OpenglDebugLevel;
        }
    }
}
