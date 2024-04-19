using Avalonia.Collections;
using Avalonia.Threading;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TimeZone = Ryujinx.Ava.UI.Models.TimeZone;

namespace Ryujinx.Ava.UI.ViewModels.Settings
{
    public class SettingsViewModel : BaseModel
    {
        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly ContentManager _contentManager;
        private TimeZoneContentManager _timeZoneContentManager;

        private readonly List<string> _validTzRegions;
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

        private bool _enableVsync;
        public bool EnableVsync
        {
            get => _enableVsync;
            set
            {
                _enableVsync = value;
                CheckIfModified();
            }
        }

        private bool _enableFsIntegrityChecks;
        public bool EnableFsIntegrityChecks
        {
            get => _enableFsIntegrityChecks;
            set
            {
                _enableFsIntegrityChecks = value;
                CheckIfModified();
            }
        }

        private bool _ignoreMissingServices;
        public bool IgnoreMissingServices
        {
            get => _ignoreMissingServices;
            set
            {
                _ignoreMissingServices = value;
                CheckIfModified();
            }
        }

        private bool _expandedDramSize;
        public bool ExpandDramSize
        {
            get => _expandedDramSize;
            set
            {
                _expandedDramSize = value;
                CheckIfModified();
            }
        }

        public string TimeZone { get; set; }
        public int Language { get; set; }
        public int Region { get; set; }
        public int BaseStyleIndex { get; set; }

        private readonly SettingsAudioViewModel _audioViewModel;
        private readonly SettingsCpuViewModel _cpuViewModel;
        private readonly SettingsGraphicsViewModel _graphicsViewModel;
        private readonly SettingsHotkeysViewModel _hotkeysViewModel;
        private readonly SettingsInputViewModel _inputViewModel;
        private readonly SettingsLoggingViewModel _loggingViewModel;
        private readonly SettingsNetworkViewModel _networkViewModel;

        public DateTimeOffset CurrentDate { get; set; }
        public TimeSpan CurrentTime { get; set; }

        internal AvaloniaList<TimeZone> TimeZones { get; set; }
        public AvaloniaList<string> GameDirectories { get; set; }

        public SettingsViewModel(
            VirtualFileSystem virtualFileSystem,
            ContentManager contentManager,
            SettingsAudioViewModel audioViewModel,
            SettingsCpuViewModel cpuViewModel,
            SettingsGraphicsViewModel graphicsViewModel,
            SettingsHotkeysViewModel hotkeysViewModel,
            SettingsInputViewModel inputViewModel,
            SettingsLoggingViewModel loggingViewModel,
            SettingsNetworkViewModel networkViewModel) : this()
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;

            _audioViewModel = audioViewModel;
            _cpuViewModel = cpuViewModel;
            _graphicsViewModel = graphicsViewModel;
            _hotkeysViewModel = hotkeysViewModel;
            _inputViewModel = inputViewModel;
            _loggingViewModel = loggingViewModel;
            _networkViewModel = networkViewModel;

            _audioViewModel.DirtyEvent += CheckIfModified;
            _cpuViewModel.DirtyEvent += CheckIfModified;
            _graphicsViewModel.DirtyEvent += CheckIfModified;
            _hotkeysViewModel.DirtyEvent += CheckIfModified;
            _inputViewModel.DirtyEvent += CheckIfModified;
            _loggingViewModel.DirtyEvent += CheckIfModified;
            _networkViewModel.DirtyEvent += CheckIfModified;

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadTimeZones);
            }
        }

        public SettingsViewModel()
        {
            GameDirectories = new AvaloniaList<string>();
            TimeZones = new AvaloniaList<TimeZone>();
            _validTzRegions = new List<string>();

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

            // Keyboard Hotkeys
            // isDirty |= config.Hid.Hotkeys.Value != KeyboardHotkey.GetConfig();

            // System
            isDirty |= config.System.Region.Value != (Region)Region;
            isDirty |= config.System.Language.Value != (Language)Language;

            if (_validTzRegions.Contains(TimeZone))
            {
                isDirty |= config.System.TimeZone.Value != TimeZone;
            }

            // isDirty |= config.System.SystemTimeOffset.Value != Convert.ToInt64((CurrentDate.ToUnixTimeSeconds() + CurrentTime.TotalSeconds) - DateTimeOffset.Now.ToUnixTimeSeconds());
            isDirty |= config.Graphics.EnableVsync.Value != EnableVsync;
            isDirty |= config.System.EnableFsIntegrityChecks.Value != EnableFsIntegrityChecks;
            isDirty |= config.System.ExpandRam.Value != ExpandDramSize;
            isDirty |= config.System.IgnoreMissingServices.Value != IgnoreMissingServices;

            isDirty |= _audioViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _cpuViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _graphicsViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _hotkeysViewModel?.CheckIfModified(config) ?? false;
            // TODO: IMPLEMENT THIS!!
            // isDirty |= _inputViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _loggingViewModel?.CheckIfModified(config) ?? false;
            isDirty |= _networkViewModel?.CheckIfModified(config) ?? false;

            IsModified = isDirty;
        }

        public async Task LoadTimeZones()
        {
            _timeZoneContentManager = new TimeZoneContentManager();

            _timeZoneContentManager.InitializeInstance(_virtualFileSystem, _contentManager, IntegrityCheckLevel.None);

            foreach ((int offset, string location, string abbr) in _timeZoneContentManager.ParseTzOffsets())
            {
                int hours = Math.DivRem(offset, 3600, out int seconds);
                int minutes = Math.Abs(seconds) / 60;

                string abbr2 = abbr.StartsWith('+') || abbr.StartsWith('-') ? string.Empty : abbr;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TimeZones.Add(new TimeZone($"UTC{hours:+0#;-0#;+00}:{minutes:D2}", location, abbr2));

                    _validTzRegions.Add(location);
                });
            }

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(TimeZone)));
        }

        public void ValidateAndSetTimeZone(string location)
        {
            if (_validTzRegions.Contains(location))
            {
                TimeZone = location;
            }
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

            // System
            Region = (int)config.System.Region.Value;
            Language = (int)config.System.Language.Value;
            TimeZone = config.System.TimeZone;

            DateTime currentDateTime = DateTime.Now;

            CurrentDate = currentDateTime.Date;
            CurrentTime = currentDateTime.TimeOfDay.Add(TimeSpan.FromSeconds(config.System.SystemTimeOffset));

            EnableVsync = config.Graphics.EnableVsync;
            EnableFsIntegrityChecks = config.System.EnableFsIntegrityChecks;
            ExpandDramSize = config.System.ExpandRam;
            IgnoreMissingServices = config.System.IgnoreMissingServices;
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


            // System
            config.System.Region.Value = (Region)Region;
            config.System.Language.Value = (Language)Language;

            if (_validTzRegions.Contains(TimeZone))
            {
                config.System.TimeZone.Value = TimeZone;
            }

            config.System.SystemTimeOffset.Value = Convert.ToInt64((CurrentDate.ToUnixTimeSeconds() + CurrentTime.TotalSeconds) - DateTimeOffset.Now.ToUnixTimeSeconds());
            config.Graphics.EnableVsync.Value = EnableVsync;
            config.System.EnableFsIntegrityChecks.Value = EnableFsIntegrityChecks;
            config.System.ExpandRam.Value = ExpandDramSize;
            config.System.IgnoreMissingServices.Value = IgnoreMissingServices;

            _audioViewModel?.Save(config);
            _cpuViewModel?.Save(config);
            _graphicsViewModel?.Save(config);
            _hotkeysViewModel?.Save(config);
            _inputViewModel?.Save(config);
            _loggingViewModel?.Save(config);
            _networkViewModel?.Save(config);

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
