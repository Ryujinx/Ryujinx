using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.Views.Input;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Controller.Motion;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.Input;
using Ryujinx.UI.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using ConfigGamepadInputId = Ryujinx.Common.Configuration.Hid.Controller.GamepadInputId;
using ConfigStickInputId = Ryujinx.Common.Configuration.Hid.Controller.StickInputId;
using Key = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class ControllerInputViewModel : BaseModel, IDisposable
    {
        private const string Disabled = "disabled";
        private const string ProControllerResource = "Ryujinx.UI.Common/Resources/Controller_ProCon.svg";
        private const string JoyConPairResource = "Ryujinx.UI.Common/Resources/Controller_JoyConPair.svg";
        private const string JoyConLeftResource = "Ryujinx.UI.Common/Resources/Controller_JoyConLeft.svg";
        private const string JoyConRightResource = "Ryujinx.UI.Common/Resources/Controller_JoyConRight.svg";
        private const string KeyboardString = "keyboard";
        private const string ControllerString = "controller";
        private readonly MainWindow _mainWindow;

        private PlayerIndex _playerId;
        private int _controller;
        private int _controllerNumber;
        private string _controllerImage;
        private int _device;
        private object _configuration;
        private string _profileName;
        private bool _isLoaded;

        private static readonly InputConfigJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public IGamepadDriver AvaloniaKeyboardDriver { get; }
        public IGamepad SelectedGamepad { get; private set; }

        public ObservableCollection<PlayerModel> PlayerIndexes { get; set; }
        public ObservableCollection<(DeviceType Type, string Id, string Name)> Devices { get; set; }
        internal ObservableCollection<ControllerModel> Controllers { get; set; }
        public AvaloniaList<string> ProfilesList { get; set; }
        public AvaloniaList<string> DeviceList { get; set; }

        // XAML Flags
        public bool ShowSettings => _device > 0;
        public bool IsController => _device > 1;
        public bool IsKeyboard => !IsController;
        public bool IsRight { get; set; }
        public bool IsLeft { get; set; }

        public bool IsModified { get; set; }

        public object Configuration
        {
            get => _configuration;
            set
            {
                _configuration = value;

                OnPropertyChanged();
            }
        }

        public PlayerIndex PlayerId
        {
            get => _playerId;
            set
            {
                if (IsModified)
                {
                    return;
                }

                IsModified = false;
                _playerId = value;

                if (!Enum.IsDefined(typeof(PlayerIndex), _playerId))
                {
                    _playerId = PlayerIndex.Player1;
                }

                LoadConfiguration();
                LoadDevice();
                LoadProfiles();

                _isLoaded = true;

                OnPropertyChanged();
            }
        }

        public int Controller
        {
            get => _controller;
            set
            {
                _controller = value;

                if (_controller == -1)
                {
                    _controller = 0;
                }

                if (Controllers.Count > 0 && value < Controllers.Count && _controller > -1)
                {
                    ControllerType controller = Controllers[_controller].Type;

                    IsLeft = true;
                    IsRight = true;

                    switch (controller)
                    {
                        case ControllerType.Handheld:
                            ControllerImage = JoyConPairResource;
                            break;
                        case ControllerType.ProController:
                            ControllerImage = ProControllerResource;
                            break;
                        case ControllerType.JoyconPair:
                            ControllerImage = JoyConPairResource;
                            break;
                        case ControllerType.JoyconLeft:
                            ControllerImage = JoyConLeftResource;
                            IsRight = false;
                            break;
                        case ControllerType.JoyconRight:
                            ControllerImage = JoyConRightResource;
                            IsLeft = false;
                            break;
                    }

                    LoadInputDriver();
                    LoadProfiles();
                }

                OnPropertyChanged();
                NotifyChanges();
            }
        }

        public string ControllerImage
        {
            get => _controllerImage;
            set
            {
                _controllerImage = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(Image));
            }
        }

        public SvgImage Image
        {
            get
            {
                SvgImage image = new();

                if (!string.IsNullOrWhiteSpace(_controllerImage))
                {
                    SvgSource source = new(default(Uri));

                    source.Load(EmbeddedResources.GetStream(_controllerImage));

                    image.Source = source;
                }

                return image;
            }
        }

        public string ProfileName
        {
            get => _profileName; set
            {
                _profileName = value;

                OnPropertyChanged();
            }
        }

        public int Device
        {
            get => _device;
            set
            {
                _device = value < 0 ? 0 : value;

                if (_device >= Devices.Count)
                {
                    return;
                }

                var selected = Devices[_device].Type;

                if (selected != DeviceType.None)
                {
                    LoadControllers();

                    if (_isLoaded)
                    {
                        LoadConfiguration(LoadDefaultConfiguration());
                    }
                }

                OnPropertyChanged();
                NotifyChanges();
            }
        }

        public InputConfig Config { get; set; }

        public ControllerInputViewModel(UserControl owner) : this()
        {
            if (Program.PreviewerDetached)
            {
                _mainWindow =
                    (MainWindow)((IClassicDesktopStyleApplicationLifetime)Application.Current
                        .ApplicationLifetime).MainWindow;

                AvaloniaKeyboardDriver = new AvaloniaKeyboardDriver(owner);

                _mainWindow.InputManager.GamepadDriver.OnGamepadConnected += HandleOnGamepadConnected;
                _mainWindow.InputManager.GamepadDriver.OnGamepadDisconnected += HandleOnGamepadDisconnected;

                _mainWindow.ViewModel.AppHost?.NpadManager.BlockInputUpdates();

                _isLoaded = false;

                LoadDevices();

                PlayerId = PlayerIndex.Player1;
            }
        }

        public ControllerInputViewModel()
        {
            PlayerIndexes = new ObservableCollection<PlayerModel>();
            Controllers = new ObservableCollection<ControllerModel>();
            Devices = new ObservableCollection<(DeviceType Type, string Id, string Name)>();
            ProfilesList = new AvaloniaList<string>();
            DeviceList = new AvaloniaList<string>();

            ControllerImage = ProControllerResource;

            PlayerIndexes.Add(new(PlayerIndex.Player1, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer1]));
            PlayerIndexes.Add(new(PlayerIndex.Player2, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer2]));
            PlayerIndexes.Add(new(PlayerIndex.Player3, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer3]));
            PlayerIndexes.Add(new(PlayerIndex.Player4, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer4]));
            PlayerIndexes.Add(new(PlayerIndex.Player5, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer5]));
            PlayerIndexes.Add(new(PlayerIndex.Player6, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer6]));
            PlayerIndexes.Add(new(PlayerIndex.Player7, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer7]));
            PlayerIndexes.Add(new(PlayerIndex.Player8, LocaleManager.Instance[LocaleKeys.ControllerSettingsPlayer8]));
            PlayerIndexes.Add(new(PlayerIndex.Handheld, LocaleManager.Instance[LocaleKeys.ControllerSettingsHandheld]));
        }

        private void LoadConfiguration(InputConfig inputConfig = null)
        {
            Config = inputConfig ?? ConfigurationState.Instance.Hid.InputConfig.Value.Find(inputConfig => inputConfig.PlayerIndex == _playerId);

            if (Config is StandardKeyboardInputConfig keyboardInputConfig)
            {
                Configuration = new InputConfiguration<Key, ConfigStickInputId>(keyboardInputConfig);
            }

            if (Config is StandardControllerInputConfig controllerInputConfig)
            {
                Configuration = new InputConfiguration<ConfigGamepadInputId, ConfigStickInputId>(controllerInputConfig);
            }
        }

        public void LoadDevice()
        {
            if (Config == null || Config.Backend == InputBackendType.Invalid)
            {
                Device = 0;
            }
            else
            {
                var type = DeviceType.None;

                if (Config is StandardKeyboardInputConfig)
                {
                    type = DeviceType.Keyboard;
                }

                if (Config is StandardControllerInputConfig)
                {
                    type = DeviceType.Controller;
                }

                var item = Devices.FirstOrDefault(x => x.Type == type && x.Id == Config.Id);
                if (item != default)
                {
                    Device = Devices.ToList().FindIndex(x => x.Id == item.Id);
                }
                else
                {
                    Device = 0;
                }
            }
        }

        public async void ShowMotionConfig()
        {
            await MotionInputView.Show(this);
        }

        public async void ShowRumbleConfig()
        {
            await RumbleInputView.Show(this);
        }

        private void LoadInputDriver()
        {
            if (_device < 0)
            {
                return;
            }

            string id = GetCurrentGamepadId();
            var type = Devices[Device].Type;

            if (type == DeviceType.None)
            {
                return;
            }

            if (type == DeviceType.Keyboard)
            {
                if (_mainWindow.InputManager.KeyboardDriver is AvaloniaKeyboardDriver)
                {
                    // NOTE: To get input in this window, we need to bind a custom keyboard driver instead of using the InputManager one as the main window isn't focused...
                    SelectedGamepad = AvaloniaKeyboardDriver.GetGamepad(id);
                }
                else
                {
                    SelectedGamepad = _mainWindow.InputManager.KeyboardDriver.GetGamepad(id);
                }
            }
            else
            {
                SelectedGamepad = _mainWindow.InputManager.GamepadDriver.GetGamepad(id);
            }
        }

        private void HandleOnGamepadDisconnected(string id)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadDevices();
            });
        }

        private void HandleOnGamepadConnected(string id)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadDevices();
            });
        }

        private string GetCurrentGamepadId()
        {
            if (_device < 0)
            {
                return string.Empty;
            }

            var device = Devices[Device];

            if (device.Type == DeviceType.None)
            {
                return null;
            }

            return device.Id.Split(" ")[0];
        }

        public void LoadControllers()
        {
            Controllers.Clear();

            if (_playerId == PlayerIndex.Handheld)
            {
                Controllers.Add(new(ControllerType.Handheld, LocaleManager.Instance[LocaleKeys.ControllerSettingsControllerTypeHandheld]));

                Controller = 0;
            }
            else
            {
                Controllers.Add(new(ControllerType.ProController, LocaleManager.Instance[LocaleKeys.ControllerSettingsControllerTypeProController]));
                Controllers.Add(new(ControllerType.JoyconPair, LocaleManager.Instance[LocaleKeys.ControllerSettingsControllerTypeJoyConPair]));
                Controllers.Add(new(ControllerType.JoyconLeft, LocaleManager.Instance[LocaleKeys.ControllerSettingsControllerTypeJoyConLeft]));
                Controllers.Add(new(ControllerType.JoyconRight, LocaleManager.Instance[LocaleKeys.ControllerSettingsControllerTypeJoyConRight]));

                if (Config != null && Controllers.ToList().FindIndex(x => x.Type == Config.ControllerType) != -1)
                {
                    Controller = Controllers.ToList().FindIndex(x => x.Type == Config.ControllerType);
                }
                else
                {
                    Controller = 0;
                }
            }
        }

        private static string GetShortGamepadName(string str)
        {
            const string Ellipsis = "...";
            const int MaxSize = 50;

            if (str.Length > MaxSize)
            {
                return $"{str.AsSpan(0, MaxSize - Ellipsis.Length)}{Ellipsis}";
            }

            return str;
        }

        private static string GetShortGamepadId(string str)
        {
            const string Hyphen = "-";
            const int Offset = 1;

            return str[(str.IndexOf(Hyphen) + Offset)..];
        }

        public void LoadDevices()
        {
            lock (Devices)
            {
                Devices.Clear();
                DeviceList.Clear();
                Devices.Add((DeviceType.None, Disabled, LocaleManager.Instance[LocaleKeys.ControllerSettingsDeviceDisabled]));

                foreach (string id in _mainWindow.InputManager.KeyboardDriver.GamepadsIds)
                {
                    using IGamepad gamepad = _mainWindow.InputManager.KeyboardDriver.GetGamepad(id);

                    if (gamepad != null)
                    {
                        Devices.Add((DeviceType.Keyboard, id, $"{GetShortGamepadName(gamepad.Name)}"));
                    }
                }

                foreach (string id in _mainWindow.InputManager.GamepadDriver.GamepadsIds)
                {
                    using IGamepad gamepad = _mainWindow.InputManager.GamepadDriver.GetGamepad(id);

                    if (gamepad != null)
                    {
                        if (Devices.Any(controller => GetShortGamepadId(controller.Id) == GetShortGamepadId(gamepad.Id)))
                        {
                            _controllerNumber++;
                        }

                        Devices.Add((DeviceType.Controller, id, $"{GetShortGamepadName(gamepad.Name)} ({_controllerNumber})"));
                    }
                }

                _controllerNumber = 0;

                DeviceList.AddRange(Devices.Select(x => x.Name));
                Device = Math.Min(Device, DeviceList.Count);
            }
        }

        private string GetProfileBasePath()
        {
            string path = AppDataManager.ProfilesDirPath;
            var type = Devices[Device == -1 ? 0 : Device].Type;

            if (type == DeviceType.Keyboard)
            {
                path = Path.Combine(path, KeyboardString);
            }
            else if (type == DeviceType.Controller)
            {
                path = Path.Combine(path, ControllerString);
            }

            return path;
        }

        private void LoadProfiles()
        {
            ProfilesList.Clear();

            string basePath = GetProfileBasePath();

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            ProfilesList.Add((LocaleManager.Instance[LocaleKeys.ControllerSettingsProfileDefault]));

            foreach (string profile in Directory.GetFiles(basePath, "*.json", SearchOption.AllDirectories))
            {
                ProfilesList.Add(Path.GetFileNameWithoutExtension(profile));
            }

            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                ProfileName = LocaleManager.Instance[LocaleKeys.ControllerSettingsProfileDefault];
            }
        }

        public InputConfig LoadDefaultConfiguration()
        {
            var activeDevice = Devices.FirstOrDefault();

            if (Devices.Count > 0 && Device < Devices.Count && Device >= 0)
            {
                activeDevice = Devices[Device];
            }

            InputConfig config;
            if (activeDevice.Type == DeviceType.Keyboard)
            {
                string id = activeDevice.Id;

                config = new StandardKeyboardInputConfig
                {
                    Version = InputConfig.CurrentVersion,
                    Backend = InputBackendType.WindowKeyboard,
                    Id = id,
                    ControllerType = ControllerType.ProController,
                    LeftJoycon = new LeftJoyconCommonConfig<Key>
                    {
                        DpadUp = Key.Up,
                        DpadDown = Key.Down,
                        DpadLeft = Key.Left,
                        DpadRight = Key.Right,
                        ButtonMinus = Key.Minus,
                        ButtonL = Key.E,
                        ButtonZl = Key.Q,
                        ButtonSl = Key.Unbound,
                        ButtonSr = Key.Unbound,
                    },
                    LeftJoyconStick =
                        new JoyconConfigKeyboardStick<Key>
                        {
                            StickUp = Key.W,
                            StickDown = Key.S,
                            StickLeft = Key.A,
                            StickRight = Key.D,
                            StickButton = Key.F,
                        },
                    RightJoycon = new RightJoyconCommonConfig<Key>
                    {
                        ButtonA = Key.Z,
                        ButtonB = Key.X,
                        ButtonX = Key.C,
                        ButtonY = Key.V,
                        ButtonPlus = Key.Plus,
                        ButtonR = Key.U,
                        ButtonZr = Key.O,
                        ButtonSl = Key.Unbound,
                        ButtonSr = Key.Unbound,
                    },
                    RightJoyconStick = new JoyconConfigKeyboardStick<Key>
                    {
                        StickUp = Key.I,
                        StickDown = Key.K,
                        StickLeft = Key.J,
                        StickRight = Key.L,
                        StickButton = Key.H,
                    },
                };
            }
            else if (activeDevice.Type == DeviceType.Controller)
            {
                bool isNintendoStyle = Devices.ToList().Find(x => x.Id == activeDevice.Id).Name.Contains("Nintendo");

                string id = activeDevice.Id.Split(" ")[0];

                config = new StandardControllerInputConfig
                {
                    Version = InputConfig.CurrentVersion,
                    Backend = InputBackendType.GamepadSDL2,
                    Id = id,
                    ControllerType = ControllerType.ProController,
                    DeadzoneLeft = 0.1f,
                    DeadzoneRight = 0.1f,
                    RangeLeft = 1.0f,
                    RangeRight = 1.0f,
                    TriggerThreshold = 0.5f,
                    LeftJoycon = new LeftJoyconCommonConfig<ConfigGamepadInputId>
                    {
                        DpadUp = ConfigGamepadInputId.DpadUp,
                        DpadDown = ConfigGamepadInputId.DpadDown,
                        DpadLeft = ConfigGamepadInputId.DpadLeft,
                        DpadRight = ConfigGamepadInputId.DpadRight,
                        ButtonMinus = ConfigGamepadInputId.Minus,
                        ButtonL = ConfigGamepadInputId.LeftShoulder,
                        ButtonZl = ConfigGamepadInputId.LeftTrigger,
                        ButtonSl = ConfigGamepadInputId.Unbound,
                        ButtonSr = ConfigGamepadInputId.Unbound,
                    },
                    LeftJoyconStick = new JoyconConfigControllerStick<ConfigGamepadInputId, ConfigStickInputId>
                    {
                        Joystick = ConfigStickInputId.Left,
                        StickButton = ConfigGamepadInputId.LeftStick,
                        InvertStickX = false,
                        InvertStickY = false,
                    },
                    RightJoycon = new RightJoyconCommonConfig<ConfigGamepadInputId>
                    {
                        ButtonA = isNintendoStyle ? ConfigGamepadInputId.A : ConfigGamepadInputId.B,
                        ButtonB = isNintendoStyle ? ConfigGamepadInputId.B : ConfigGamepadInputId.A,
                        ButtonX = isNintendoStyle ? ConfigGamepadInputId.X : ConfigGamepadInputId.Y,
                        ButtonY = isNintendoStyle ? ConfigGamepadInputId.Y : ConfigGamepadInputId.X,
                        ButtonPlus = ConfigGamepadInputId.Plus,
                        ButtonR = ConfigGamepadInputId.RightShoulder,
                        ButtonZr = ConfigGamepadInputId.RightTrigger,
                        ButtonSl = ConfigGamepadInputId.Unbound,
                        ButtonSr = ConfigGamepadInputId.Unbound,
                    },
                    RightJoyconStick = new JoyconConfigControllerStick<ConfigGamepadInputId, ConfigStickInputId>
                    {
                        Joystick = ConfigStickInputId.Right,
                        StickButton = ConfigGamepadInputId.RightStick,
                        InvertStickX = false,
                        InvertStickY = false,
                    },
                    Motion = new StandardMotionConfigController
                    {
                        MotionBackend = MotionInputBackendType.GamepadDriver,
                        EnableMotion = true,
                        Sensitivity = 100,
                        GyroDeadzone = 1,
                    },
                    Rumble = new RumbleConfigController
                    {
                        StrongRumble = 1f,
                        WeakRumble = 1f,
                        EnableRumble = false,
                    },
                };
            }
            else
            {
                config = new InputConfig();
            }

            config.PlayerIndex = _playerId;

            return config;
        }

        public async void LoadProfile()
        {
            if (Device == 0)
            {
                return;
            }

            InputConfig config = null;

            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                return;
            }

            if (ProfileName == LocaleManager.Instance[LocaleKeys.ControllerSettingsProfileDefault])
            {
                config = LoadDefaultConfiguration();
            }
            else
            {
                string path = Path.Combine(GetProfileBasePath(), ProfileName + ".json");

                if (!File.Exists(path))
                {
                    var index = ProfilesList.IndexOf(ProfileName);
                    if (index != -1)
                    {
                        ProfilesList.RemoveAt(index);
                    }
                    return;
                }

                try
                {
                    config = JsonHelper.DeserializeFromFile(path, _serializerContext.InputConfig);
                }
                catch (JsonException) { }
                catch (InvalidOperationException)
                {
                    Logger.Error?.Print(LogClass.Configuration, $"Profile {ProfileName} is incompatible with the current input configuration system.");

                    await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogProfileInvalidProfileErrorMessage, ProfileName));

                    return;
                }
            }

            if (config != null)
            {
                _isLoaded = false;

                LoadConfiguration(config);

                LoadDevice();

                _isLoaded = true;

                NotifyChanges();
            }
        }

        public async void SaveProfile()
        {
            if (Device == 0)
            {
                return;
            }

            if (Configuration == null)
            {
                return;
            }

            if (ProfileName == LocaleManager.Instance[LocaleKeys.ControllerSettingsProfileDefault])
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogProfileDefaultProfileOverwriteErrorMessage]);

                return;
            }

            bool validFileName = ProfileName.IndexOfAny(Path.GetInvalidFileNameChars()) == -1;

            if (validFileName)
            {
                string path = Path.Combine(GetProfileBasePath(), ProfileName + ".json");

                InputConfig config = null;

                if (IsKeyboard)
                {
                    config = (Configuration as InputConfiguration<Key, ConfigStickInputId>).GetConfig();
                }
                else if (IsController)
                {
                    config = (Configuration as InputConfiguration<GamepadInputId, ConfigStickInputId>).GetConfig();
                }

                config.ControllerType = Controllers[_controller].Type;

                string jsonString = JsonHelper.Serialize(config, _serializerContext.InputConfig);

                await File.WriteAllTextAsync(path, jsonString);

                LoadProfiles();
            }
            else
            {
                await ContentDialogHelper.CreateErrorDialog(LocaleManager.Instance[LocaleKeys.DialogProfileInvalidProfileNameErrorMessage]);
            }
        }

        public async void RemoveProfile()
        {
            if (Device == 0 || ProfileName == LocaleManager.Instance[LocaleKeys.ControllerSettingsProfileDefault] || ProfilesList.IndexOf(ProfileName) == -1)
            {
                return;
            }

            UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                LocaleManager.Instance[LocaleKeys.DialogProfileDeleteProfileTitle],
                LocaleManager.Instance[LocaleKeys.DialogProfileDeleteProfileMessage],
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

            if (result == UserResult.Yes)
            {
                string path = Path.Combine(GetProfileBasePath(), ProfileName + ".json");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                LoadProfiles();
            }
        }

        public void Save()
        {
            IsModified = false;

            List<InputConfig> newConfig = new();

            newConfig.AddRange(ConfigurationState.Instance.Hid.InputConfig.Value);

            newConfig.Remove(newConfig.Find(x => x == null));

            if (Device == 0)
            {
                newConfig.Remove(newConfig.Find(x => x.PlayerIndex == this.PlayerId));
            }
            else
            {
                var device = Devices[Device];

                if (device.Type == DeviceType.Keyboard)
                {
                    var inputConfig = Configuration as InputConfiguration<Key, ConfigStickInputId>;
                    inputConfig.Id = device.Id;
                }
                else
                {
                    var inputConfig = Configuration as InputConfiguration<GamepadInputId, ConfigStickInputId>;
                    inputConfig.Id = device.Id.Split(" ")[0];
                }

                var config = !IsController
                    ? (Configuration as InputConfiguration<Key, ConfigStickInputId>).GetConfig()
                    : (Configuration as InputConfiguration<GamepadInputId, ConfigStickInputId>).GetConfig();
                config.ControllerType = Controllers[_controller].Type;
                config.PlayerIndex = _playerId;

                int i = newConfig.FindIndex(x => x.PlayerIndex == PlayerId);
                if (i == -1)
                {
                    newConfig.Add(config);
                }
                else
                {
                    newConfig[i] = config;
                }
            }

            _mainWindow.ViewModel.AppHost?.NpadManager.ReloadConfiguration(newConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);

            // Atomically replace and signal input change.
            // NOTE: Do not modify InputConfig.Value directly as other code depends on the on-change event.
            ConfigurationState.Instance.Hid.InputConfig.Value = newConfig;

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
        }

        public void NotifyChange(string property)
        {
            OnPropertyChanged(property);
        }

        public void NotifyChanges()
        {
            OnPropertyChanged(nameof(Configuration));
            OnPropertyChanged(nameof(IsController));
            OnPropertyChanged(nameof(ShowSettings));
            OnPropertyChanged(nameof(IsKeyboard));
            OnPropertyChanged(nameof(IsRight));
            OnPropertyChanged(nameof(IsLeft));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _mainWindow.InputManager.GamepadDriver.OnGamepadConnected -= HandleOnGamepadConnected;
            _mainWindow.InputManager.GamepadDriver.OnGamepadDisconnected -= HandleOnGamepadDisconnected;

            _mainWindow.ViewModel.AppHost?.NpadManager.UnblockInputUpdates();

            SelectedGamepad?.Dispose();

            AvaloniaKeyboardDriver.Dispose();
        }
    }
}
