using Gtk;
using LibHac.Tools.FsSystem;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.GraphicsDriver;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Configuration.System;
using Ryujinx.UI.Helper;
using Ryujinx.UI.Widgets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GUI = Gtk.Builder.ObjectAttribute;

namespace Ryujinx.UI.Windows
{
    public class SettingsWindow : Window
    {
        private readonly MainWindow _parent;
        private readonly ListStore _gameDirsBoxStore;
        private readonly ListStore _audioBackendStore;
        private readonly TimeZoneContentManager _timeZoneContentManager;
        private readonly HashSet<string> _validTzRegions;

        private long _systemTimeOffset;
        private float _previousVolumeLevel;
        private bool _directoryChanged = false;

#pragma warning disable CS0649, IDE0044 // Field is never assigned to, Add readonly modifier
        [GUI] CheckButton _traceLogToggle;
        [GUI] CheckButton _errorLogToggle;
        [GUI] CheckButton _warningLogToggle;
        [GUI] CheckButton _infoLogToggle;
        [GUI] CheckButton _stubLogToggle;
        [GUI] CheckButton _debugLogToggle;
        [GUI] CheckButton _fileLogToggle;
        [GUI] CheckButton _guestLogToggle;
        [GUI] CheckButton _fsAccessLogToggle;
        [GUI] Adjustment _fsLogSpinAdjustment;
        [GUI] ComboBoxText _graphicsDebugLevel;
        [GUI] CheckButton _dockedModeToggle;
        [GUI] CheckButton _discordToggle;
        [GUI] CheckButton _checkUpdatesToggle;
        [GUI] CheckButton _showConfirmExitToggle;
        [GUI] RadioButton _hideCursorNever;
        [GUI] RadioButton _hideCursorOnIdle;
        [GUI] RadioButton _hideCursorAlways;
        [GUI] CheckButton _vSyncToggle;
        [GUI] CheckButton _shaderCacheToggle;
        [GUI] CheckButton _textureRecompressionToggle;
        [GUI] CheckButton _macroHLEToggle;
        [GUI] CheckButton _ptcToggle;
        [GUI] CheckButton _internetToggle;
        [GUI] CheckButton _fsicToggle;
        [GUI] RadioButton _mmSoftware;
        [GUI] RadioButton _mmHost;
        [GUI] RadioButton _mmHostUnsafe;
        [GUI] CheckButton _expandRamToggle;
        [GUI] CheckButton _ignoreToggle;
        [GUI] CheckButton _directKeyboardAccess;
        [GUI] CheckButton _directMouseAccess;
        [GUI] ComboBoxText _systemLanguageSelect;
        [GUI] ComboBoxText _systemRegionSelect;
        [GUI] Entry _systemTimeZoneEntry;
        [GUI] EntryCompletion _systemTimeZoneCompletion;
        [GUI] Box _audioBackendBox;
        [GUI] ComboBox _audioBackendSelect;
        [GUI] Label _audioVolumeLabel;
        [GUI] Scale _audioVolumeSlider;
        [GUI] SpinButton _systemTimeYearSpin;
        [GUI] SpinButton _systemTimeMonthSpin;
        [GUI] SpinButton _systemTimeDaySpin;
        [GUI] SpinButton _systemTimeHourSpin;
        [GUI] SpinButton _systemTimeMinuteSpin;
        [GUI] Adjustment _systemTimeYearSpinAdjustment;
        [GUI] Adjustment _systemTimeMonthSpinAdjustment;
        [GUI] Adjustment _systemTimeDaySpinAdjustment;
        [GUI] Adjustment _systemTimeHourSpinAdjustment;
        [GUI] Adjustment _systemTimeMinuteSpinAdjustment;
        [GUI] ComboBoxText _multiLanSelect;
        [GUI] ComboBoxText _multiModeSelect;
        [GUI] CheckButton _custThemeToggle;
        [GUI] Entry _custThemePath;
        [GUI] ToggleButton _browseThemePath;
        [GUI] Label _custThemePathLabel;
        [GUI] TreeView _gameDirsBox;
        [GUI] Entry _addGameDirBox;
        [GUI] ComboBoxText _galThreading;
        [GUI] Entry _graphicsShadersDumpPath;
        [GUI] ComboBoxText _anisotropy;
        [GUI] ComboBoxText _aspectRatio;
        [GUI] ComboBoxText _antiAliasing;
        [GUI] ComboBoxText _scalingFilter;
        [GUI] ComboBoxText _graphicsBackend;
        [GUI] ComboBoxText _preferredGpu;
        [GUI] ComboBoxText _resScaleCombo;
        [GUI] Entry _resScaleText;
        [GUI] Adjustment _scalingFilterLevel;
        [GUI] Scale _scalingFilterSlider;
        [GUI] ToggleButton _configureController1;
        [GUI] ToggleButton _configureController2;
        [GUI] ToggleButton _configureController3;
        [GUI] ToggleButton _configureController4;
        [GUI] ToggleButton _configureController5;
        [GUI] ToggleButton _configureController6;
        [GUI] ToggleButton _configureController7;
        [GUI] ToggleButton _configureController8;
        [GUI] ToggleButton _configureControllerH;

#pragma warning restore CS0649, IDE0044

        public SettingsWindow(MainWindow parent, VirtualFileSystem virtualFileSystem, ContentManager contentManager) : this(parent, new Builder("Ryujinx.Gtk3.UI.Windows.SettingsWindow.glade"), virtualFileSystem, contentManager) { }

        private SettingsWindow(MainWindow parent, Builder builder, VirtualFileSystem virtualFileSystem, ContentManager contentManager) : base(builder.GetRawOwnedObject("_settingsWin"))
        {
            Icon = new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Logo_Ryujinx.png");

            _parent = parent;

            builder.Autoconnect(this);

            _timeZoneContentManager = new TimeZoneContentManager();
            _timeZoneContentManager.InitializeInstance(virtualFileSystem, contentManager, IntegrityCheckLevel.None);

            _validTzRegions = new HashSet<string>(_timeZoneContentManager.LocationNameCache.Length, StringComparer.Ordinal); // Zone regions are identifiers. Must match exactly.

            // Bind Events.
            _configureController1.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player1);
            _configureController2.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player2);
            _configureController3.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player3);
            _configureController4.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player4);
            _configureController5.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player5);
            _configureController6.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player6);
            _configureController7.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player7);
            _configureController8.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Player8);
            _configureControllerH.Pressed += (sender, args) => ConfigureController_Pressed(sender, PlayerIndex.Handheld);
            _systemTimeZoneEntry.FocusOutEvent += TimeZoneEntry_FocusOut;

            _resScaleCombo.Changed += (sender, args) => _resScaleText.Visible = _resScaleCombo.ActiveId == "-1";
            _scalingFilter.Changed += (sender, args) => _scalingFilterSlider.Visible = _scalingFilter.ActiveId == "2";
            _galThreading.Changed += (sender, args) =>
            {
                if (_galThreading.ActiveId != ConfigurationState.Instance.Graphics.BackendThreading.Value.ToString())
                {
                    GtkDialog.CreateInfoDialog("Warning - Backend Threading", "Ryujinx must be restarted after changing this option for it to apply fully. Depending on your platform, you may need to manually disable your driver's own multithreading when using Ryujinx's.");
                }
            };

            // Setup Currents.
            if (ConfigurationState.Instance.Logger.EnableTrace)
            {
                _traceLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableFileLog)
            {
                _fileLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableError)
            {
                _errorLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableWarn)
            {
                _warningLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableInfo)
            {
                _infoLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableStub)
            {
                _stubLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableDebug)
            {
                _debugLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableGuest)
            {
                _guestLogToggle.Click();
            }

            if (ConfigurationState.Instance.Logger.EnableFsAccessLog)
            {
                _fsAccessLogToggle.Click();
            }

            foreach (GraphicsDebugLevel level in Enum.GetValues<GraphicsDebugLevel>())
            {
                _graphicsDebugLevel.Append(level.ToString(), level.ToString());
            }

            _graphicsDebugLevel.SetActiveId(ConfigurationState.Instance.Logger.GraphicsDebugLevel.Value.ToString());

            if (ConfigurationState.Instance.System.EnableDockedMode)
            {
                _dockedModeToggle.Click();
            }

            if (ConfigurationState.Instance.EnableDiscordIntegration)
            {
                _discordToggle.Click();
            }

            if (ConfigurationState.Instance.CheckUpdatesOnStart)
            {
                _checkUpdatesToggle.Click();
            }

            if (ConfigurationState.Instance.ShowConfirmExit)
            {
                _showConfirmExitToggle.Click();
            }

            switch (ConfigurationState.Instance.HideCursor.Value)
            {
                case HideCursorMode.Never:
                    _hideCursorNever.Click();
                    break;
                case HideCursorMode.OnIdle:
                    _hideCursorOnIdle.Click();
                    break;
                case HideCursorMode.Always:
                    _hideCursorAlways.Click();
                    break;
            }

            if (ConfigurationState.Instance.Graphics.EnableVsync)
            {
                _vSyncToggle.Click();
            }

            if (ConfigurationState.Instance.Graphics.EnableShaderCache)
            {
                _shaderCacheToggle.Click();
            }

            if (ConfigurationState.Instance.Graphics.EnableTextureRecompression)
            {
                _textureRecompressionToggle.Click();
            }

            if (ConfigurationState.Instance.Graphics.EnableMacroHLE)
            {
                _macroHLEToggle.Click();
            }

            if (ConfigurationState.Instance.System.EnablePtc)
            {
                _ptcToggle.Click();
            }

            if (ConfigurationState.Instance.System.EnableInternetAccess)
            {
                _internetToggle.Click();
            }

            if (ConfigurationState.Instance.System.EnableFsIntegrityChecks)
            {
                _fsicToggle.Click();
            }

            switch (ConfigurationState.Instance.System.MemoryManagerMode.Value)
            {
                case MemoryManagerMode.SoftwarePageTable:
                    _mmSoftware.Click();
                    break;
                case MemoryManagerMode.HostMapped:
                    _mmHost.Click();
                    break;
                case MemoryManagerMode.HostMappedUnsafe:
                    _mmHostUnsafe.Click();
                    break;
            }

            if (ConfigurationState.Instance.System.ExpandRam)
            {
                _expandRamToggle.Click();
            }

            if (ConfigurationState.Instance.System.IgnoreMissingServices)
            {
                _ignoreToggle.Click();
            }

            if (ConfigurationState.Instance.Hid.EnableKeyboard)
            {
                _directKeyboardAccess.Click();
            }

            if (ConfigurationState.Instance.Hid.EnableMouse)
            {
                _directMouseAccess.Click();
            }

            if (ConfigurationState.Instance.UI.EnableCustomTheme)
            {
                _custThemeToggle.Click();
            }

            // Custom EntryCompletion Columns. If added to glade, need to override more signals
            ListStore tzList = new(typeof(string), typeof(string), typeof(string));
            _systemTimeZoneCompletion.Model = tzList;

            CellRendererText offsetCol = new();
            CellRendererText abbrevCol = new();

            _systemTimeZoneCompletion.PackStart(offsetCol, false);
            _systemTimeZoneCompletion.AddAttribute(offsetCol, "text", 0);
            _systemTimeZoneCompletion.TextColumn = 1; // Regions Column
            _systemTimeZoneCompletion.PackStart(abbrevCol, false);
            _systemTimeZoneCompletion.AddAttribute(abbrevCol, "text", 2);

            int maxLocationLength = 0;

            foreach (var (offset, location, abbr) in _timeZoneContentManager.ParseTzOffsets())
            {
                var hours = Math.DivRem(offset, 3600, out int seconds);
                var minutes = Math.Abs(seconds) / 60;

                var abbr2 = (abbr.StartsWith('+') || abbr.StartsWith('-')) ? string.Empty : abbr;

                tzList.AppendValues($"UTC{hours:+0#;-0#;+00}:{minutes:D2} ", location, abbr2);
                _validTzRegions.Add(location);

                maxLocationLength = Math.Max(maxLocationLength, location.Length);
            }

            _systemTimeZoneEntry.WidthChars = Math.Max(20, maxLocationLength + 1); // Ensure minimum Entry width
            _systemTimeZoneEntry.Text = _timeZoneContentManager.SanityCheckDeviceLocationName(ConfigurationState.Instance.System.TimeZone);

            _systemTimeZoneCompletion.MatchFunc = TimeZoneMatchFunc;

            _systemLanguageSelect.SetActiveId(ConfigurationState.Instance.System.Language.Value.ToString());
            _systemRegionSelect.SetActiveId(ConfigurationState.Instance.System.Region.Value.ToString());
            _galThreading.SetActiveId(ConfigurationState.Instance.Graphics.BackendThreading.Value.ToString());
            _resScaleCombo.SetActiveId(ConfigurationState.Instance.Graphics.ResScale.Value.ToString());
            _anisotropy.SetActiveId(ConfigurationState.Instance.Graphics.MaxAnisotropy.Value.ToString());
            _aspectRatio.SetActiveId(((int)ConfigurationState.Instance.Graphics.AspectRatio.Value).ToString());
            _graphicsBackend.SetActiveId(((int)ConfigurationState.Instance.Graphics.GraphicsBackend.Value).ToString());
            _antiAliasing.SetActiveId(((int)ConfigurationState.Instance.Graphics.AntiAliasing.Value).ToString());
            _scalingFilter.SetActiveId(((int)ConfigurationState.Instance.Graphics.ScalingFilter.Value).ToString());

            UpdatePreferredGpuComboBox();

            _graphicsBackend.Changed += (sender, e) => UpdatePreferredGpuComboBox();
            PopulateNetworkInterfaces();
            _multiLanSelect.SetActiveId(ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value);
            _multiModeSelect.SetActiveId(ConfigurationState.Instance.Multiplayer.Mode.Value.ToString());

            _custThemePath.Buffer.Text = ConfigurationState.Instance.UI.CustomThemePath;
            _resScaleText.Buffer.Text = ConfigurationState.Instance.Graphics.ResScaleCustom.Value.ToString();
            _scalingFilterLevel.Value = ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value;
            _resScaleText.Visible = _resScaleCombo.ActiveId == "-1";
            _scalingFilterSlider.Visible = _scalingFilter.ActiveId == "2";
            _graphicsShadersDumpPath.Buffer.Text = ConfigurationState.Instance.Graphics.ShadersDumpPath;
            _fsLogSpinAdjustment.Value = ConfigurationState.Instance.System.FsGlobalAccessLogMode;
            _systemTimeOffset = ConfigurationState.Instance.System.SystemTimeOffset;

            _gameDirsBox.AppendColumn("", new CellRendererText(), "text", 0);
            _gameDirsBoxStore = new ListStore(typeof(string));
            _gameDirsBox.Model = _gameDirsBoxStore;

            foreach (string gameDir in ConfigurationState.Instance.UI.GameDirs.Value)
            {
                _gameDirsBoxStore.AppendValues(gameDir);
            }

            if (_custThemeToggle.Active == false)
            {
                _custThemePath.Sensitive = false;
                _custThemePathLabel.Sensitive = false;
                _browseThemePath.Sensitive = false;
            }

            // Setup system time spinners
            UpdateSystemTimeSpinners();

            _audioBackendStore = new ListStore(typeof(string), typeof(AudioBackend));

            TreeIter openAlIter = _audioBackendStore.AppendValues("OpenAL", AudioBackend.OpenAl);
            TreeIter soundIoIter = _audioBackendStore.AppendValues("SoundIO", AudioBackend.SoundIo);
            TreeIter sdl2Iter = _audioBackendStore.AppendValues("SDL2", AudioBackend.SDL2);
            TreeIter dummyIter = _audioBackendStore.AppendValues("Dummy", AudioBackend.Dummy);

            _audioBackendSelect = ComboBox.NewWithModelAndEntry(_audioBackendStore);
            _audioBackendSelect.EntryTextColumn = 0;
            _audioBackendSelect.Entry.IsEditable = false;

            switch (ConfigurationState.Instance.System.AudioBackend.Value)
            {
                case AudioBackend.OpenAl:
                    _audioBackendSelect.SetActiveIter(openAlIter);
                    break;
                case AudioBackend.SoundIo:
                    _audioBackendSelect.SetActiveIter(soundIoIter);
                    break;
                case AudioBackend.SDL2:
                    _audioBackendSelect.SetActiveIter(sdl2Iter);
                    break;
                case AudioBackend.Dummy:
                    _audioBackendSelect.SetActiveIter(dummyIter);
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(ConfigurationState.Instance.System.AudioBackend)} contains an invalid value: {ConfigurationState.Instance.System.AudioBackend.Value}");
            }

            _audioBackendBox.Add(_audioBackendSelect);
            _audioBackendSelect.Show();

            _previousVolumeLevel = ConfigurationState.Instance.System.AudioVolume;
            _audioVolumeLabel = new Label("Volume: ");
            _audioVolumeSlider = new Scale(Orientation.Horizontal, 0, 100, 1);
            _audioVolumeLabel.MarginStart = 10;
            _audioVolumeSlider.ValuePos = PositionType.Right;
            _audioVolumeSlider.WidthRequest = 200;

            _audioVolumeSlider.Value = _previousVolumeLevel * 100;
            _audioVolumeSlider.ValueChanged += VolumeSlider_OnChange;
            _audioBackendBox.Add(_audioVolumeLabel);
            _audioBackendBox.Add(_audioVolumeSlider);
            _audioVolumeLabel.Show();
            _audioVolumeSlider.Show();

            bool openAlIsSupported = false;
            bool soundIoIsSupported = false;
            bool sdl2IsSupported = false;

            Task.Run(() =>
            {
                openAlIsSupported = OpenALHardwareDeviceDriver.IsSupported;
                soundIoIsSupported = !OperatingSystem.IsMacOS() && SoundIoHardwareDeviceDriver.IsSupported;
                sdl2IsSupported = SDL2HardwareDeviceDriver.IsSupported;
            });

            // This function runs whenever the dropdown is opened
            _audioBackendSelect.SetCellDataFunc(_audioBackendSelect.Cells[0], (layout, cell, model, iter) =>
            {
                cell.Sensitive = ((AudioBackend)_audioBackendStore.GetValue(iter, 1)) switch
                {
                    AudioBackend.OpenAl => openAlIsSupported,
                    AudioBackend.SoundIo => soundIoIsSupported,
                    AudioBackend.SDL2 => sdl2IsSupported,
                    AudioBackend.Dummy => true,
                    _ => throw new InvalidOperationException($"{nameof(_audioBackendStore)} contains an invalid value for iteration {iter}: {_audioBackendStore.GetValue(iter, 1)}"),
                };
            });

            if (OperatingSystem.IsMacOS())
            {
                var store = (_graphicsBackend.Model as ListStore);
                store.GetIter(out TreeIter openglIter, new TreePath(new[] { 1 }));
                store.Remove(ref openglIter);

                _graphicsBackend.Model = store;
            }
        }

        private void UpdatePreferredGpuComboBox()
        {
            _preferredGpu.RemoveAll();

            if (Enum.Parse<GraphicsBackend>(_graphicsBackend.ActiveId) == GraphicsBackend.Vulkan)
            {
                var devices = Graphics.Vulkan.VulkanRenderer.GetPhysicalDevices();
                string preferredGpuIdFromConfig = ConfigurationState.Instance.Graphics.PreferredGpu.Value;
                string preferredGpuId = preferredGpuIdFromConfig;
                bool noGpuId = string.IsNullOrEmpty(preferredGpuIdFromConfig);

                foreach (var device in devices)
                {
                    string dGpu = device.IsDiscrete ? " (dGPU)" : "";
                    _preferredGpu.Append(device.Id, $"{device.Name}{dGpu}");

                    // If there's no GPU selected yet, we just pick the first GPU.
                    // If there's a discrete GPU available, we always prefer that over the previous selection,
                    // as it is likely to have better performance and more features.
                    // If the configuration file already has a GPU selection, we always prefer that instead.
                    if (noGpuId && (string.IsNullOrEmpty(preferredGpuId) || device.IsDiscrete))
                    {
                        preferredGpuId = device.Id;
                    }
                }

                if (!string.IsNullOrEmpty(preferredGpuId))
                {
                    _preferredGpu.SetActiveId(preferredGpuId);
                }
            }
        }

        private void PopulateNetworkInterfaces()
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface nif in interfaces)
            {
                string guid = nif.Id;
                string name = nif.Name;

                _multiLanSelect.Append(guid, name);
            }
        }

        private void UpdateSystemTimeSpinners()
        {
            //Bind system time events
            _systemTimeYearSpin.ValueChanged -= SystemTimeSpin_ValueChanged;
            _systemTimeMonthSpin.ValueChanged -= SystemTimeSpin_ValueChanged;
            _systemTimeDaySpin.ValueChanged -= SystemTimeSpin_ValueChanged;
            _systemTimeHourSpin.ValueChanged -= SystemTimeSpin_ValueChanged;
            _systemTimeMinuteSpin.ValueChanged -= SystemTimeSpin_ValueChanged;

            //Apply actual system time + SystemTimeOffset to system time spin buttons
            DateTime systemTime = DateTime.Now.AddSeconds(_systemTimeOffset);

            _systemTimeYearSpinAdjustment.Value = systemTime.Year;
            _systemTimeMonthSpinAdjustment.Value = systemTime.Month;
            _systemTimeDaySpinAdjustment.Value = systemTime.Day;
            _systemTimeHourSpinAdjustment.Value = systemTime.Hour;
            _systemTimeMinuteSpinAdjustment.Value = systemTime.Minute;

            //Format spin buttons text to include leading zeros
            _systemTimeYearSpin.Text = systemTime.Year.ToString("0000");
            _systemTimeMonthSpin.Text = systemTime.Month.ToString("00");
            _systemTimeDaySpin.Text = systemTime.Day.ToString("00");
            _systemTimeHourSpin.Text = systemTime.Hour.ToString("00");
            _systemTimeMinuteSpin.Text = systemTime.Minute.ToString("00");

            //Bind system time events
            _systemTimeYearSpin.ValueChanged += SystemTimeSpin_ValueChanged;
            _systemTimeMonthSpin.ValueChanged += SystemTimeSpin_ValueChanged;
            _systemTimeDaySpin.ValueChanged += SystemTimeSpin_ValueChanged;
            _systemTimeHourSpin.ValueChanged += SystemTimeSpin_ValueChanged;
            _systemTimeMinuteSpin.ValueChanged += SystemTimeSpin_ValueChanged;
        }

        private void SaveSettings()
        {
            if (_directoryChanged)
            {
                List<string> gameDirs = new();

                _gameDirsBoxStore.GetIterFirst(out TreeIter treeIter);

                for (int i = 0; i < _gameDirsBoxStore.IterNChildren(); i++)
                {
                    gameDirs.Add((string)_gameDirsBoxStore.GetValue(treeIter, 0));

                    _gameDirsBoxStore.IterNext(ref treeIter);
                }

                ConfigurationState.Instance.UI.GameDirs.Value = gameDirs;

                _directoryChanged = false;
            }

            HideCursorMode hideCursor = HideCursorMode.Never;

            if (_hideCursorOnIdle.Active)
            {
                hideCursor = HideCursorMode.OnIdle;
            }

            if (_hideCursorAlways.Active)
            {
                hideCursor = HideCursorMode.Always;
            }

            if (!float.TryParse(_resScaleText.Buffer.Text, out float resScaleCustom) || resScaleCustom <= 0.0f)
            {
                resScaleCustom = 1.0f;
            }

            if (_validTzRegions.Contains(_systemTimeZoneEntry.Text))
            {
                ConfigurationState.Instance.System.TimeZone.Value = _systemTimeZoneEntry.Text;
            }

            MemoryManagerMode memoryMode = MemoryManagerMode.SoftwarePageTable;

            if (_mmHost.Active)
            {
                memoryMode = MemoryManagerMode.HostMapped;
            }

            if (_mmHostUnsafe.Active)
            {
                memoryMode = MemoryManagerMode.HostMappedUnsafe;
            }

            BackendThreading backendThreading = Enum.Parse<BackendThreading>(_galThreading.ActiveId);
            if (ConfigurationState.Instance.Graphics.BackendThreading != backendThreading)
            {
                DriverUtilities.ToggleOGLThreading(backendThreading == BackendThreading.Off);
            }

            ConfigurationState.Instance.Logger.EnableError.Value = _errorLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableTrace.Value = _traceLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableWarn.Value = _warningLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableInfo.Value = _infoLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableStub.Value = _stubLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableDebug.Value = _debugLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableGuest.Value = _guestLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableFsAccessLog.Value = _fsAccessLogToggle.Active;
            ConfigurationState.Instance.Logger.EnableFileLog.Value = _fileLogToggle.Active;
            ConfigurationState.Instance.Logger.GraphicsDebugLevel.Value = Enum.Parse<GraphicsDebugLevel>(_graphicsDebugLevel.ActiveId);
            ConfigurationState.Instance.System.EnableDockedMode.Value = _dockedModeToggle.Active;
            ConfigurationState.Instance.EnableDiscordIntegration.Value = _discordToggle.Active;
            ConfigurationState.Instance.CheckUpdatesOnStart.Value = _checkUpdatesToggle.Active;
            ConfigurationState.Instance.ShowConfirmExit.Value = _showConfirmExitToggle.Active;
            ConfigurationState.Instance.HideCursor.Value = hideCursor;
            ConfigurationState.Instance.Graphics.EnableVsync.Value = _vSyncToggle.Active;
            ConfigurationState.Instance.Graphics.EnableShaderCache.Value = _shaderCacheToggle.Active;
            ConfigurationState.Instance.Graphics.EnableTextureRecompression.Value = _textureRecompressionToggle.Active;
            ConfigurationState.Instance.Graphics.EnableMacroHLE.Value = _macroHLEToggle.Active;
            ConfigurationState.Instance.System.EnablePtc.Value = _ptcToggle.Active;
            ConfigurationState.Instance.System.EnableInternetAccess.Value = _internetToggle.Active;
            ConfigurationState.Instance.System.EnableFsIntegrityChecks.Value = _fsicToggle.Active;
            ConfigurationState.Instance.System.MemoryManagerMode.Value = memoryMode;
            ConfigurationState.Instance.System.ExpandRam.Value = _expandRamToggle.Active;
            ConfigurationState.Instance.System.IgnoreMissingServices.Value = _ignoreToggle.Active;
            ConfigurationState.Instance.Hid.EnableKeyboard.Value = _directKeyboardAccess.Active;
            ConfigurationState.Instance.Hid.EnableMouse.Value = _directMouseAccess.Active;
            ConfigurationState.Instance.UI.EnableCustomTheme.Value = _custThemeToggle.Active;
            ConfigurationState.Instance.System.Language.Value = Enum.Parse<Language>(_systemLanguageSelect.ActiveId);
            ConfigurationState.Instance.System.Region.Value = Enum.Parse<Common.Configuration.System.Region>(_systemRegionSelect.ActiveId);
            ConfigurationState.Instance.System.SystemTimeOffset.Value = _systemTimeOffset;
            ConfigurationState.Instance.UI.CustomThemePath.Value = _custThemePath.Buffer.Text;
            ConfigurationState.Instance.Graphics.ShadersDumpPath.Value = _graphicsShadersDumpPath.Buffer.Text;
            ConfigurationState.Instance.System.FsGlobalAccessLogMode.Value = (int)_fsLogSpinAdjustment.Value;
            ConfigurationState.Instance.Graphics.MaxAnisotropy.Value = float.Parse(_anisotropy.ActiveId, CultureInfo.InvariantCulture);
            ConfigurationState.Instance.Graphics.AspectRatio.Value = Enum.Parse<AspectRatio>(_aspectRatio.ActiveId);
            ConfigurationState.Instance.Graphics.BackendThreading.Value = backendThreading;
            ConfigurationState.Instance.Graphics.GraphicsBackend.Value = Enum.Parse<GraphicsBackend>(_graphicsBackend.ActiveId);
            ConfigurationState.Instance.Graphics.PreferredGpu.Value = _preferredGpu.ActiveId;
            ConfigurationState.Instance.Graphics.ResScale.Value = int.Parse(_resScaleCombo.ActiveId);
            ConfigurationState.Instance.Graphics.ResScaleCustom.Value = resScaleCustom;
            ConfigurationState.Instance.System.AudioVolume.Value = (float)_audioVolumeSlider.Value / 100.0f;
            ConfigurationState.Instance.Graphics.AntiAliasing.Value = Enum.Parse<AntiAliasing>(_antiAliasing.ActiveId);
            ConfigurationState.Instance.Graphics.ScalingFilter.Value = Enum.Parse<ScalingFilter>(_scalingFilter.ActiveId);
            ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value = (int)_scalingFilterLevel.Value;
            ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value = _multiLanSelect.ActiveId;

            _previousVolumeLevel = ConfigurationState.Instance.System.AudioVolume.Value;

            ConfigurationState.Instance.Multiplayer.Mode.Value = Enum.Parse<MultiplayerMode>(_multiModeSelect.ActiveId);
            ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value = _multiLanSelect.ActiveId;

            if (_audioBackendSelect.GetActiveIter(out TreeIter activeIter))
            {
                ConfigurationState.Instance.System.AudioBackend.Value = (AudioBackend)_audioBackendStore.GetValue(activeIter, 1);
            }

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            _parent.UpdateInternetAccess();
            MainWindow.UpdateGraphicsConfig();
            ThemeHelper.ApplyTheme();
        }

        //
        // Events
        //
        private void TimeZoneEntry_FocusOut(object sender, FocusOutEventArgs e)
        {
            if (!_validTzRegions.Contains(_systemTimeZoneEntry.Text))
            {
                _systemTimeZoneEntry.Text = _timeZoneContentManager.SanityCheckDeviceLocationName(ConfigurationState.Instance.System.TimeZone);
            }
        }

        private bool TimeZoneMatchFunc(EntryCompletion compl, string key, TreeIter iter)
        {
            key = key.Trim().Replace(' ', '_');

            return ((string)compl.Model.GetValue(iter, 1)).Contains(key, StringComparison.OrdinalIgnoreCase) || // region
                   ((string)compl.Model.GetValue(iter, 2)).StartsWith(key, StringComparison.OrdinalIgnoreCase) || // abbr
                   ((string)compl.Model.GetValue(iter, 0))[3..].StartsWith(key); // offset
        }

        private void SystemTimeSpin_ValueChanged(object sender, EventArgs e)
        {
            int year = _systemTimeYearSpin.ValueAsInt;
            int month = _systemTimeMonthSpin.ValueAsInt;
            int day = _systemTimeDaySpin.ValueAsInt;
            int hour = _systemTimeHourSpin.ValueAsInt;
            int minute = _systemTimeMinuteSpin.ValueAsInt;

            if (!DateTime.TryParse(year + "-" + month + "-" + day + " " + hour + ":" + minute, out DateTime newTime))
            {
                UpdateSystemTimeSpinners();

                return;
            }

            newTime = newTime.AddSeconds(DateTime.Now.Second).AddMilliseconds(DateTime.Now.Millisecond);

            long systemTimeOffset = (long)Math.Ceiling((newTime - DateTime.Now).TotalMinutes) * 60L;

            if (_systemTimeOffset != systemTimeOffset)
            {
                _systemTimeOffset = systemTimeOffset;
                UpdateSystemTimeSpinners();
            }
        }

        private void AddDir_Pressed(object sender, EventArgs args)
        {
            if (Directory.Exists(_addGameDirBox.Buffer.Text))
            {
                _gameDirsBoxStore.AppendValues(_addGameDirBox.Buffer.Text);
                _directoryChanged = true;
            }
            else
            {
                FileChooserNative fileChooser = new("Choose the game directory to add to the list", this, FileChooserAction.SelectFolder, "Add", "Cancel")
                {
                    SelectMultiple = true,
                };

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    _directoryChanged = false;
                    foreach (string directory in fileChooser.Filenames)
                    {
                        if (_gameDirsBoxStore.GetIterFirst(out TreeIter treeIter))
                        {
                            do
                            {
                                if (directory.Equals((string)_gameDirsBoxStore.GetValue(treeIter, 0)))
                                {
                                    break;
                                }
                            } while (_gameDirsBoxStore.IterNext(ref treeIter));
                        }

                        if (!_directoryChanged)
                        {
                            _gameDirsBoxStore.AppendValues(directory);
                        }
                    }

                    _directoryChanged = true;
                }

                fileChooser.Dispose();
            }

            _addGameDirBox.Buffer.Text = "";

            ((ToggleButton)sender).SetStateFlags(StateFlags.Normal, true);
        }

        private void RemoveDir_Pressed(object sender, EventArgs args)
        {
            TreeSelection selection = _gameDirsBox.Selection;

            if (selection.GetSelected(out TreeIter treeIter))
            {
                _gameDirsBoxStore.Remove(ref treeIter);

                _directoryChanged = true;
            }

            ((ToggleButton)sender).SetStateFlags(StateFlags.Normal, true);
        }

        private void CustThemeToggle_Activated(object sender, EventArgs args)
        {
            _custThemePath.Sensitive = _custThemeToggle.Active;
            _custThemePathLabel.Sensitive = _custThemeToggle.Active;
            _browseThemePath.Sensitive = _custThemeToggle.Active;
        }

        private void BrowseThemeDir_Pressed(object sender, EventArgs args)
        {
            using (FileChooserNative fileChooser = new("Choose the theme to load", this, FileChooserAction.Open, "Select", "Cancel"))
            {
                FileFilter filter = new()
                {
                    Name = "Theme Files",
                };
                filter.AddPattern("*.css");

                fileChooser.AddFilter(filter);

                if (fileChooser.Run() == (int)ResponseType.Accept)
                {
                    _custThemePath.Buffer.Text = fileChooser.Filename;
                }
            }

            _browseThemePath.SetStateFlags(StateFlags.Normal, true);
        }

        private void ConfigureController_Pressed(object sender, PlayerIndex playerIndex)
        {
            ((ToggleButton)sender).SetStateFlags(StateFlags.Normal, true);

            ControllerWindow controllerWindow = new(_parent, playerIndex);

            controllerWindow.SetSizeRequest((int)(controllerWindow.DefaultWidth * Program.WindowScaleFactor), (int)(controllerWindow.DefaultHeight * Program.WindowScaleFactor));
            controllerWindow.Show();
        }

        private void VolumeSlider_OnChange(object sender, EventArgs args)
        {
            ConfigurationState.Instance.System.AudioVolume.Value = (float)(_audioVolumeSlider.Value / 100);
        }

        private void SaveToggle_Activated(object sender, EventArgs args)
        {
            SaveSettings();
            Dispose();
        }

        private void ApplyToggle_Activated(object sender, EventArgs args)
        {
            SaveSettings();
        }

        private void CloseToggle_Activated(object sender, EventArgs args)
        {
            ConfigurationState.Instance.System.AudioVolume.Value = _previousVolumeLevel;
            Dispose();
        }
    }
}
