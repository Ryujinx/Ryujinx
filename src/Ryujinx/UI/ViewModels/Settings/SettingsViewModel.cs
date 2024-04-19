using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models.Input;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.GraphicsDriver;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
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

        private readonly Dictionary<string, string> _networkInterfaces;

        private float _customResolutionScale;
        private int _resolutionScale;
        private int _graphicsBackendMultithreadingIndex;
        private bool _isVulkanAvailable = true;
        private bool _directoryChanged;
        private readonly List<string> _gpuIds = new();
        private int _graphicsBackendIndex;
        private int _scalingFilter;
        private int _scalingFilterLevel;
        private int _networkInterfaceIndex;
        private int _multiplayerModeIndex;

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
        public event Action SaveSettingsEvent;
        public event Action<bool> DirtyEvent;
        public event Action<bool> ToggleButtons;

        public int ResolutionScale
        {
            get => _resolutionScale;
            set
            {
                _resolutionScale = value;

                OnPropertyChanged(nameof(CustomResolutionScale));
                OnPropertyChanged(nameof(IsCustomResolutionScaleActive));
            }
        }

        public int GraphicsBackendMultithreadingIndex
        {
            get => _graphicsBackendMultithreadingIndex;
            set
            {
                _graphicsBackendMultithreadingIndex = value;

                if (_graphicsBackendMultithreadingIndex != (int)ConfigurationState.Instance.Graphics.BackendThreading.Value)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                         ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningMessage],
                            "",
                            "",
                            LocaleManager.Instance[LocaleKeys.InputDialogOk],
                            LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningTitle])
                    );
                }

                OnPropertyChanged();
            }
        }

        public float CustomResolutionScale
        {
            get => _customResolutionScale;
            set
            {
                _customResolutionScale = MathF.Round(value, 1);

                OnPropertyChanged();
            }
        }

        public bool IsVulkanAvailable
        {
            get => _isVulkanAvailable;
            set
            {
                _isVulkanAvailable = value;

                OnPropertyChanged();
            }
        }

        public bool IsOpenGLAvailable => !OperatingSystem.IsMacOS();

        public bool IsHypervisorAvailable => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

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

        private bool _enablePptc;
        public bool EnablePptc
        {
            get => _enablePptc;
            set
            {
                _enablePptc = value;
                CheckIfModified();
            }
        }

        private bool _enableInternetAccess;
        public bool EnableInternetAccess
        {
            get => _enableInternetAccess;
            set
            {
                _enableInternetAccess = value;
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

        private bool _enableShaderCache;
        public bool EnableShaderCache
        {
            get => _enableShaderCache;
            set
            {
                _enableShaderCache = value;
                CheckIfModified();
            }
        }

        private bool _enableTextureRecompression;
        public bool EnableTextureRecompression
        {
            get => _enableTextureRecompression;
            set
            {
                _enableTextureRecompression = value;
                CheckIfModified();
            }
        }

        private bool _enableMacroHLE;
        public bool EnableMacroHLE
        {
            get => _enableMacroHLE;
            set
            {
                _enableMacroHLE = value;
                CheckIfModified();
            }
        }

        private bool _enableColorSpacePassthrough;
        public bool EnableColorSpacePassthrough
        {
            get => _enableColorSpacePassthrough;
            set
            {
                _enableColorSpacePassthrough = value;
                CheckIfModified();
            }
        }

        public bool ColorSpacePassthroughAvailable => IsMacOS;

        public bool IsCustomResolutionScaleActive => _resolutionScale == 4;
        public bool IsScalingFilterActive => _scalingFilter == (int)Ryujinx.Common.Configuration.ScalingFilter.Fsr;

        public bool IsVulkanSelected => GraphicsBackendIndex == 0;
        public bool UseHypervisor { get; set; }

        public string TimeZone { get; set; }
        public string ShaderDumpPath { get; set; }

        public int Language { get; set; }
        public int Region { get; set; }
        public int MaxAnisotropy { get; set; }
        public int AspectRatio { get; set; }
        public int AntiAliasingEffect { get; set; }
        public string ScalingFilterLevelText => ScalingFilterLevel.ToString("0");
        public int ScalingFilterLevel
        {
            get => _scalingFilterLevel;
            set
            {
                _scalingFilterLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScalingFilterLevelText));
            }
        }
        public int MemoryMode { get; set; }
        public int BaseStyleIndex { get; set; }
        public int GraphicsBackendIndex
        {
            get => _graphicsBackendIndex;
            set
            {
                _graphicsBackendIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVulkanSelected));
            }
        }
        public int ScalingFilter
        {
            get => _scalingFilter;
            set
            {
                _scalingFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsScalingFilterActive));
            }
        }

        public int PreferredGpuIndex { get; set; }

        private readonly SettingsAudioViewModel _audioViewModel;
        private readonly SettingsLoggingViewModel _loggingViewModel;

        public DateTimeOffset CurrentDate { get; set; }
        public TimeSpan CurrentTime { get; set; }

        internal AvaloniaList<TimeZone> TimeZones { get; set; }
        public AvaloniaList<string> GameDirectories { get; set; }
        public ObservableCollection<ComboBoxItem> AvailableGpus { get; set; }

        public AvaloniaList<string> NetworkInterfaceList
        {
            get => new(_networkInterfaces.Keys);
        }

        public HotkeyConfig KeyboardHotkey { get; set; }

        public int NetworkInterfaceIndex
        {
            get => _networkInterfaceIndex;
            set
            {
                _networkInterfaceIndex = value != -1 ? value : 0;
                ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[_networkInterfaceIndex]];
            }
        }

        public int MultiplayerModeIndex
        {
            get => _multiplayerModeIndex;
            set
            {
                _multiplayerModeIndex = value;
                ConfigurationState.Instance.Multiplayer.Mode.Value = (MultiplayerMode)_multiplayerModeIndex;
            }
        }

        public SettingsViewModel(
            VirtualFileSystem virtualFileSystem,
            ContentManager contentManager,
            SettingsAudioViewModel audioViewModel,
            SettingsLoggingViewModel loggingViewModel) : this()
        {
            _virtualFileSystem = virtualFileSystem;
            _contentManager = contentManager;

            _audioViewModel = audioViewModel;
            _loggingViewModel = loggingViewModel;

            _audioViewModel.DirtyEvent += CheckIfModified;
            _loggingViewModel.DirtyEvent += CheckIfModified;

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadTimeZones);
            }
        }

        public SettingsViewModel()
        {
            GameDirectories = new AvaloniaList<string>();
            TimeZones = new AvaloniaList<TimeZone>();
            AvailableGpus = new ObservableCollection<ComboBoxItem>();
            _validTzRegions = new List<string>();
            _networkInterfaces = new Dictionary<string, string>();

            Task.Run(PopulateNetworkInterfaces);

            if (Program.PreviewerDetached)
            {
                Task.Run(LoadAvailableGpus);
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

            // CPU
            isDirty |= config.System.EnablePtc.Value != EnablePptc;
            isDirty |= config.System.MemoryManagerMode.Value != (MemoryManagerMode)MemoryMode;
            isDirty |= config.System.UseHypervisor.Value != UseHypervisor;

            // Graphics
            isDirty |= config.Graphics.GraphicsBackend.Value != (GraphicsBackend)GraphicsBackendIndex;
            isDirty |= config.Graphics.PreferredGpu.Value != _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            isDirty |= config.Graphics.EnableShaderCache.Value != EnableShaderCache;
            isDirty |= config.Graphics.EnableTextureRecompression.Value != EnableTextureRecompression;
            isDirty |= config.Graphics.EnableMacroHLE.Value != EnableMacroHLE;
            isDirty |= config.Graphics.EnableColorSpacePassthrough.Value != EnableColorSpacePassthrough;
            isDirty |= config.Graphics.ResScale.Value != (ResolutionScale == 4 ? -1 : ResolutionScale + 1);
            isDirty |= config.Graphics.ResScaleCustom.Value != CustomResolutionScale;
            isDirty |= config.Graphics.MaxAnisotropy.Value != (MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy));
            isDirty |= config.Graphics.AspectRatio.Value != (AspectRatio)AspectRatio;
            isDirty |= config.Graphics.AntiAliasing.Value != (AntiAliasing)AntiAliasingEffect;
            isDirty |= config.Graphics.ScalingFilter.Value != (ScalingFilter)ScalingFilter;
            isDirty |= config.Graphics.ScalingFilterLevel.Value != ScalingFilterLevel;

            if (ConfigurationState.Instance.Graphics.BackendThreading != (BackendThreading)GraphicsBackendMultithreadingIndex)
            {
                DriverUtilities.ToggleOGLThreading(GraphicsBackendMultithreadingIndex == (int)BackendThreading.Off);
            }

            isDirty |= config.Graphics.BackendThreading.Value != (BackendThreading)GraphicsBackendMultithreadingIndex;
            isDirty |= config.Graphics.ShadersDumpPath.Value != ShaderDumpPath;

            if (_audioViewModel != null)
            {
                isDirty |= _audioViewModel.CheckIfModified(config);
            }
            // Network
            isDirty |= config.System.EnableInternetAccess.Value != EnableInternetAccess;

            if (_loggingViewModel != null)
            {
                isDirty |= _loggingViewModel.CheckIfModified(config);
            }

            isDirty |= config.Multiplayer.LanInterfaceId.Value != _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            isDirty |= config.Multiplayer.Mode.Value != (MultiplayerMode)MultiplayerModeIndex;

            IsModified = isDirty;
        }



        private async Task LoadAvailableGpus()
        {
            AvailableGpus.Clear();

            var devices = VulkanRenderer.GetPhysicalDevices();

            if (devices.Length == 0)
            {
                IsVulkanAvailable = false;
                GraphicsBackendIndex = 1;
            }
            else
            {
                foreach (var device in devices)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _gpuIds.Add(device.Id);

                        AvailableGpus.Add(new ComboBoxItem { Content = $"{device.Name} {(device.IsDiscrete ? "(dGPU)" : "")}" });
                    });
                }
            }

            // GPU configuration needs to be loaded during the async method or it will always return 0.
            PreferredGpuIndex = _gpuIds.Contains(ConfigurationState.Instance.Graphics.PreferredGpu) ?
                                _gpuIds.IndexOf(ConfigurationState.Instance.Graphics.PreferredGpu) : 0;

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(PreferredGpuIndex)));
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

        private async Task PopulateNetworkInterfaces()
        {
            _networkInterfaces.Clear();
            _networkInterfaces.Add(LocaleManager.Instance[LocaleKeys.NetworkInterfaceDefault], "0");

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _networkInterfaces.Add(networkInterface.Name, networkInterface.Id);
                });
            }

            // Network interface index  needs to be loaded during the async method or it will always return 0.
            NetworkInterfaceIndex = _networkInterfaces.Values.ToList().IndexOf(ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value);

            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(NetworkInterfaceIndex)));
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

            // Keyboard Hotkeys
            KeyboardHotkey = new HotkeyConfig(config.Hid.Hotkeys.Value);

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

            // CPU
            EnablePptc = config.System.EnablePtc;
            MemoryMode = (int)config.System.MemoryManagerMode.Value;
            UseHypervisor = config.System.UseHypervisor;

            // Graphics
            GraphicsBackendIndex = (int)config.Graphics.GraphicsBackend.Value;
            // Physical devices are queried asynchronously hence the prefered index config value is loaded in LoadAvailableGpus().
            EnableShaderCache = config.Graphics.EnableShaderCache;
            EnableTextureRecompression = config.Graphics.EnableTextureRecompression;
            EnableMacroHLE = config.Graphics.EnableMacroHLE;
            EnableColorSpacePassthrough = config.Graphics.EnableColorSpacePassthrough;
            ResolutionScale = config.Graphics.ResScale == -1 ? 4 : config.Graphics.ResScale - 1;
            CustomResolutionScale = config.Graphics.ResScaleCustom;
            MaxAnisotropy = config.Graphics.MaxAnisotropy == -1 ? 0 : (int)(MathF.Log2(config.Graphics.MaxAnisotropy));
            AspectRatio = (int)config.Graphics.AspectRatio.Value;
            GraphicsBackendMultithreadingIndex = (int)config.Graphics.BackendThreading.Value;
            ShaderDumpPath = config.Graphics.ShadersDumpPath;
            AntiAliasingEffect = (int)config.Graphics.AntiAliasing.Value;
            ScalingFilter = (int)config.Graphics.ScalingFilter.Value;
            ScalingFilterLevel = config.Graphics.ScalingFilterLevel.Value;

            // Network
            EnableInternetAccess = config.System.EnableInternetAccess;
            // LAN interface index is loaded asynchronously in PopulateNetworkInterfaces()

            MultiplayerModeIndex = (int)config.Multiplayer.Mode.Value;
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

            // Keyboard Hotkeys
            config.Hid.Hotkeys.Value = KeyboardHotkey.GetConfig();

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

            // CPU
            config.System.EnablePtc.Value = EnablePptc;
            config.System.MemoryManagerMode.Value = (MemoryManagerMode)MemoryMode;
            config.System.UseHypervisor.Value = UseHypervisor;

            // Graphics
            config.Graphics.GraphicsBackend.Value = (GraphicsBackend)GraphicsBackendIndex;
            config.Graphics.PreferredGpu.Value = _gpuIds.ElementAtOrDefault(PreferredGpuIndex);
            config.Graphics.EnableShaderCache.Value = EnableShaderCache;
            config.Graphics.EnableTextureRecompression.Value = EnableTextureRecompression;
            config.Graphics.EnableMacroHLE.Value = EnableMacroHLE;
            config.Graphics.EnableColorSpacePassthrough.Value = EnableColorSpacePassthrough;
            config.Graphics.ResScale.Value = ResolutionScale == 4 ? -1 : ResolutionScale + 1;
            config.Graphics.ResScaleCustom.Value = CustomResolutionScale;
            config.Graphics.MaxAnisotropy.Value = MaxAnisotropy == 0 ? -1 : MathF.Pow(2, MaxAnisotropy);
            config.Graphics.AspectRatio.Value = (AspectRatio)AspectRatio;
            config.Graphics.AntiAliasing.Value = (AntiAliasing)AntiAliasingEffect;
            config.Graphics.ScalingFilter.Value = (ScalingFilter)ScalingFilter;
            config.Graphics.ScalingFilterLevel.Value = ScalingFilterLevel;

            if (ConfigurationState.Instance.Graphics.BackendThreading != (BackendThreading)GraphicsBackendMultithreadingIndex)
            {
                DriverUtilities.ToggleOGLThreading(GraphicsBackendMultithreadingIndex == (int)BackendThreading.Off);
            }

            config.Graphics.BackendThreading.Value = (BackendThreading)GraphicsBackendMultithreadingIndex;
            config.Graphics.ShadersDumpPath.Value = ShaderDumpPath;

            _audioViewModel?.Save(config);

            // Network
            config.System.EnableInternetAccess.Value = EnableInternetAccess;

            _loggingViewModel?.Save(config);

            config.Multiplayer.LanInterfaceId.Value = _networkInterfaces[NetworkInterfaceList[NetworkInterfaceIndex]];
            config.Multiplayer.Mode.Value = (MultiplayerMode)MultiplayerModeIndex;

            config.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            MainWindow.UpdateGraphicsConfig();

            SaveSettingsEvent?.Invoke();

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
