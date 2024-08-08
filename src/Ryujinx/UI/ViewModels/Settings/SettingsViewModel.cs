using Ryujinx.Ava.UI.Windows;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsViewModel : BaseModel
    {
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

        public bool IsMacOS => OperatingSystem.IsMacOS();

        private readonly SettingsAudioViewModel _audioViewModel;
        private readonly SettingsCpuViewModel _cpuViewModel;
        private readonly SettingsGraphicsViewModel _graphicsViewModel;
        private readonly SettingsHotkeysViewModel _hotkeysViewModel;
        private readonly SettingsInputViewModel _inputViewModel;
        private readonly SettingsLoggingViewModel _loggingViewModel;
        private readonly SettingsNetworkViewModel _networkViewModel;
        private readonly SettingsSystemViewModel _systemViewModel;
        private readonly SettingsUIViewModel _uiViewModel;

        public SettingsViewModel(
            SettingsAudioViewModel audioViewModel,
            SettingsCpuViewModel cpuViewModel,
            SettingsGraphicsViewModel graphicsViewModel,
            SettingsHotkeysViewModel hotkeysViewModel,
            SettingsInputViewModel inputViewModel,
            SettingsLoggingViewModel loggingViewModel,
            SettingsNetworkViewModel networkViewModel,
            SettingsSystemViewModel systemViewModel,
            SettingsUIViewModel uiViewModel)
        {
            _audioViewModel = audioViewModel;
            _cpuViewModel = cpuViewModel;
            _graphicsViewModel = graphicsViewModel;
            _hotkeysViewModel = hotkeysViewModel;
            _inputViewModel = inputViewModel;
            _loggingViewModel = loggingViewModel;
            _networkViewModel = networkViewModel;
            _systemViewModel = systemViewModel;
            _uiViewModel = uiViewModel;

            _audioViewModel.DirtyEvent += CheckIfModified;
            _cpuViewModel.DirtyEvent += CheckIfModified;
            _graphicsViewModel.DirtyEvent += CheckIfModified;
            _hotkeysViewModel.DirtyEvent += CheckIfModified;
            _inputViewModel.DirtyEvent += CheckIfModified;
            _loggingViewModel.DirtyEvent += CheckIfModified;
            _networkViewModel.DirtyEvent += CheckIfModified;
            _systemViewModel.DirtyEvent += CheckIfModified;
            _uiViewModel.DirtyEvent += CheckIfModified;
        }

        public void CheckIfModified()
        {
            bool isDirty = false;

            ConfigurationState config = ConfigurationState.Instance;

            isDirty |= _audioViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _cpuViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _graphicsViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _hotkeysViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _inputViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _loggingViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _networkViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _systemViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _uiViewModel?.CheckIfModified(config) ?? false;

            IsModified = isDirty;
        }

        public void SaveSettings()
        {
            ConfigurationState config = ConfigurationState.Instance;

            _audioViewModel?.Save(config);
            _cpuViewModel?.Save(config);
            _graphicsViewModel?.Save(config);
            _hotkeysViewModel?.Save(config);
            _inputViewModel?.Save(config);
            _loggingViewModel?.Save(config);
            _networkViewModel?.Save(config);
            _systemViewModel?.Save(config);
            _uiViewModel?.Save(config);

            config.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            MainWindow.UpdateGraphicsConfig();
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
