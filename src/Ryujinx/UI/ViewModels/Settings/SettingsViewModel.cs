using Avalonia.Collections;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsViewModel : BaseModel
    {
        private bool _directoryChanged;

        private bool _isModified;

        public bool IsModified
        {
            get => _isModified;
            private set
            {
                DirtyEvent?.Invoke(value);
                _isModified = value;
            }
        }

        public event Action CloseWindow;
        public event Action<bool> DirtyEvent;
        public event Action<bool> ToggleButtons;

        public bool DirectoryChanged
        {
            get => _directoryChanged;
            set
            {
                _directoryChanged = value;

                OnPropertyChanged();
            }
        }

        public bool IsMacOS => OperatingSystem.IsMacOS();

        private bool _enableDiscordIntegration;
        public bool EnableDiscordIntegration
        {
            get => _enableDiscordIntegration;
            set
            {
                _enableDiscordIntegration = value;
                CheckIfModified();
            }
        }

        private bool _checkUpdatesOnStart;
        public bool CheckUpdatesOnStart
        {
            get => _checkUpdatesOnStart;
            set
            {
                _checkUpdatesOnStart = value;
                CheckIfModified();
            }
        }

        private bool _showConfirmExit;
        public bool ShowConfirmExit
        {
            get => _showConfirmExit;
            set
            {
                _showConfirmExit = value;
                CheckIfModified();
            }
        }

        private int _hideCursor;
        public int HideCursor
        {
            get => _hideCursor;
            set
            {
                _hideCursor = value;
                CheckIfModified();
            }
        }

        public int BaseStyleIndex { get; set; }

        private readonly SettingsAudioViewModel _audioViewModel;
        private readonly SettingsCpuViewModel _cpuViewModel;
        private readonly SettingsGraphicsViewModel _graphicsViewModel;
        private readonly SettingsHotkeysViewModel _hotkeysViewModel;
        private readonly SettingsInputViewModel _inputViewModel;
        private readonly SettingsLoggingViewModel _loggingViewModel;
        private readonly SettingsNetworkViewModel _networkViewModel;
        private readonly SettingsSystemViewModel _systemViewModel;

        public AvaloniaList<string> GameDirectories { get; set; }

        public SettingsViewModel(
            SettingsAudioViewModel audioViewModel,
            SettingsCpuViewModel cpuViewModel,
            SettingsGraphicsViewModel graphicsViewModel,
            SettingsHotkeysViewModel hotkeysViewModel,
            SettingsInputViewModel inputViewModel,
            SettingsLoggingViewModel loggingViewModel,
            SettingsNetworkViewModel networkViewModel,
            SettingsSystemViewModel systemViewModel) : this()
        {
            _audioViewModel = audioViewModel;
            _cpuViewModel = cpuViewModel;
            _graphicsViewModel = graphicsViewModel;
            _hotkeysViewModel = hotkeysViewModel;
            _inputViewModel = inputViewModel;
            _loggingViewModel = loggingViewModel;
            _networkViewModel = networkViewModel;
            _systemViewModel = systemViewModel;

            _audioViewModel.DirtyEvent += CheckIfModified;
            _cpuViewModel.DirtyEvent += CheckIfModified;
            _graphicsViewModel.DirtyEvent += CheckIfModified;
            _hotkeysViewModel.DirtyEvent += CheckIfModified;
            _inputViewModel.DirtyEvent += CheckIfModified;
            _loggingViewModel.DirtyEvent += CheckIfModified;
            _networkViewModel.DirtyEvent += CheckIfModified;
            _systemViewModel.DirtyEvent += CheckIfModified;
        }

        public SettingsViewModel()
        {
            GameDirectories = new AvaloniaList<string>();

            if (Program.PreviewerDetached)
            {
                LoadCurrentConfiguration();
            }
        }

        public void CheckIfModified()
        {
            bool isDirty = false;

            ConfigurationState config = ConfigurationState.Instance;

            isDirty |= config.EnableDiscordIntegration.Value != EnableDiscordIntegration;
            isDirty |= config.CheckUpdatesOnStart.Value != CheckUpdatesOnStart;
            isDirty |= config.ShowConfirmExit.Value != ShowConfirmExit;
            isDirty |= config.HideCursor.Value != (HideCursorMode)HideCursor;

            // isDirty |= config.UI.GameDirs.Value != GameDirectories.ToList();

            isDirty |= config.UI.BaseStyle.Value != (BaseStyleIndex == 0 ? "Light" : "Dark");

            isDirty |= _audioViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _cpuViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _graphicsViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _hotkeysViewModel?.CheckIfModified(config) ?? false;
            // TODO: IMPLEMENT THIS!!
            // isDirty |= _inputViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _loggingViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _networkViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _systemViewModel?.CheckIfModified(config) ?? false;

            IsModified = isDirty;
        }

        public void LoadCurrentConfiguration()
        {
            ConfigurationState config = ConfigurationState.Instance;

            // User Interface
            EnableDiscordIntegration = config.EnableDiscordIntegration;
            CheckUpdatesOnStart = config.CheckUpdatesOnStart;
            ShowConfirmExit = config.ShowConfirmExit;
            HideCursor = (int)config.HideCursor.Value;

            GameDirectories.Clear();
            GameDirectories.AddRange(config.UI.GameDirs.Value);

            BaseStyleIndex = config.UI.BaseStyle == "Light" ? 0 : 1;
        }

        public void SaveSettings()
        {
            ConfigurationState config = ConfigurationState.Instance;

            // User Interface
            config.EnableDiscordIntegration.Value = EnableDiscordIntegration;
            config.CheckUpdatesOnStart.Value = CheckUpdatesOnStart;
            config.ShowConfirmExit.Value = ShowConfirmExit;
            config.HideCursor.Value = (HideCursorMode)HideCursor;

            if (_directoryChanged)
            {
                List<string> gameDirs = new(GameDirectories);
                config.UI.GameDirs.Value = gameDirs;
            }

            config.UI.BaseStyle.Value = BaseStyleIndex == 0 ? "Light" : "Dark";

            _audioViewModel?.Save(config);
            _cpuViewModel?.Save(config);
            _graphicsViewModel?.Save(config);
            _hotkeysViewModel?.Save(config);
            _inputViewModel?.Save(config);
            _loggingViewModel?.Save(config);
            _networkViewModel?.Save(config);
            _systemViewModel?.Save(config);

            config.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            MainWindow.UpdateGraphicsConfig();

            _directoryChanged = false;
        }

        public void ApplyButton()
        {
            SaveSettings();
        }

        public void OkButton()
        {
            SaveSettings();
            CloseWindow?.Invoke();
        }
    }
}
