using Gtk;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Controller.Motion;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.Input;
using Ryujinx.Input.Assigner;
using Ryujinx.Input.GTK3;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Widgets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using ConfigGamepadInputId = Ryujinx.Common.Configuration.Hid.Controller.GamepadInputId;
using ConfigStickInputId = Ryujinx.Common.Configuration.Hid.Controller.StickInputId;
using GUI = Gtk.Builder.ObjectAttribute;
using Key = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.UI.Windows
{
    public class ControllerWindow : Window
    {
        private readonly PlayerIndex _playerIndex;
        private readonly InputConfig _inputConfig;

        private bool _isWaitingForInput;

#pragma warning disable CS0649, IDE0044 // Field is never assigned to, Add readonly modifier
        [GUI] Adjustment _controllerStrongRumble;
        [GUI] Adjustment _controllerWeakRumble;
        [GUI] Adjustment _controllerDeadzoneLeft;
        [GUI] Adjustment _controllerDeadzoneRight;
        [GUI] Adjustment _controllerRangeLeft;
        [GUI] Adjustment _controllerRangeRight;
        [GUI] Adjustment _controllerTriggerThreshold;
        [GUI] Adjustment _slotNumber;
        [GUI] Adjustment _altSlotNumber;
        [GUI] Adjustment _sensitivity;
        [GUI] Adjustment _gyroDeadzone;
        [GUI] CheckButton _enableMotion;
        [GUI] CheckButton _enableCemuHook;
        [GUI] CheckButton _mirrorInput;
        [GUI] Entry _dsuServerHost;
        [GUI] Entry _dsuServerPort;
        [GUI] ComboBoxText _inputDevice;
        [GUI] ComboBoxText _profile;
        [GUI] Box _settingsBox;
        [GUI] Box _motionAltBox;
        [GUI] Box _motionBox;
        [GUI] Box _dsuServerHostBox;
        [GUI] Box _dsuServerPortBox;
        [GUI] Box _motionControllerSlot;
        [GUI] Grid _leftStickKeyboard;
        [GUI] Grid _leftStickController;
        [GUI] Box _deadZoneLeftBox;
        [GUI] Box _rangeLeftBox;
        [GUI] Grid _rightStickKeyboard;
        [GUI] Grid _rightStickController;
        [GUI] Box _deadZoneRightBox;
        [GUI] Box _rangeRightBox;
        [GUI] Grid _leftSideTriggerBox;
        [GUI] Grid _rightSideTriggerBox;
        [GUI] Box _triggerThresholdBox;
        [GUI] ComboBoxText _controllerType;
        [GUI] ToggleButton _lStick;
        [GUI] CheckButton _invertLStickX;
        [GUI] CheckButton _invertLStickY;
        [GUI] CheckButton _rotateL90CW;
        [GUI] ToggleButton _lStickUp;
        [GUI] ToggleButton _lStickDown;
        [GUI] ToggleButton _lStickLeft;
        [GUI] ToggleButton _lStickRight;
        [GUI] ToggleButton _lStickButton;
        [GUI] ToggleButton _dpadUp;
        [GUI] ToggleButton _dpadDown;
        [GUI] ToggleButton _dpadLeft;
        [GUI] ToggleButton _dpadRight;
        [GUI] ToggleButton _minus;
        [GUI] ToggleButton _l;
        [GUI] ToggleButton _zL;
        [GUI] ToggleButton _rStick;
        [GUI] CheckButton _invertRStickX;
        [GUI] CheckButton _invertRStickY;
        [GUI] CheckButton _rotateR90CW;
        [GUI] ToggleButton _rStickUp;
        [GUI] ToggleButton _rStickDown;
        [GUI] ToggleButton _rStickLeft;
        [GUI] ToggleButton _rStickRight;
        [GUI] ToggleButton _rStickButton;
        [GUI] ToggleButton _a;
        [GUI] ToggleButton _b;
        [GUI] ToggleButton _x;
        [GUI] ToggleButton _y;
        [GUI] ToggleButton _plus;
        [GUI] ToggleButton _r;
        [GUI] ToggleButton _zR;
        [GUI] ToggleButton _lSl;
        [GUI] ToggleButton _lSr;
        [GUI] ToggleButton _rSl;
        [GUI] ToggleButton _rSr;
        [GUI] Image _controllerImage;
        [GUI] CheckButton _enableRumble;
        [GUI] Box _rumbleBox;
#pragma warning restore CS0649, IDE0044

        private readonly MainWindow _mainWindow;
        private readonly IGamepadDriver _gtk3KeyboardDriver;
        private IGamepad _selectedGamepad;
        private bool _mousePressed;
        private bool _middleMousePressed;

        private static readonly InputConfigJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public ControllerWindow(MainWindow mainWindow, PlayerIndex controllerId) : this(mainWindow, new Builder("Ryujinx.Gtk3.UI.Windows.ControllerWindow.glade"), controllerId) { }

        private ControllerWindow(MainWindow mainWindow, Builder builder, PlayerIndex controllerId) : base(builder.GetRawOwnedObject("_controllerWin"))
        {
            _mainWindow = mainWindow;
            _selectedGamepad = null;

            // NOTE: To get input in this window, we need to bind a custom keyboard driver instead of using the InputManager one as the main window isn't focused...
            _gtk3KeyboardDriver = new GTK3KeyboardDriver(this);

            Icon = new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Logo_Ryujinx.png");

            builder.Autoconnect(this);

            _playerIndex = controllerId;
            _inputConfig = ConfigurationState.Instance.Hid.InputConfig.Value.Find(inputConfig => inputConfig.PlayerIndex == _playerIndex);

            Title = $"Ryujinx - Controller Settings - {_playerIndex}";

            if (_playerIndex == PlayerIndex.Handheld)
            {
                _controllerType.Append(ControllerType.Handheld.ToString(), "Handheld");
                _controllerType.Sensitive = false;
            }
            else
            {
                _controllerType.Append(ControllerType.ProController.ToString(), "Pro Controller");
                _controllerType.Append(ControllerType.JoyconPair.ToString(), "Joycon Pair");
                _controllerType.Append(ControllerType.JoyconLeft.ToString(), "Joycon Left");
                _controllerType.Append(ControllerType.JoyconRight.ToString(), "Joycon Right");
            }

            _controllerType.Active = 0; // Set initial value to first in list.

            // Bind Events.
            _lStick.Clicked += ButtonForStick_Pressed;
            _lStickUp.Clicked += Button_Pressed;
            _lStickDown.Clicked += Button_Pressed;
            _lStickLeft.Clicked += Button_Pressed;
            _lStickRight.Clicked += Button_Pressed;
            _lStickButton.Clicked += Button_Pressed;
            _dpadUp.Clicked += Button_Pressed;
            _dpadDown.Clicked += Button_Pressed;
            _dpadLeft.Clicked += Button_Pressed;
            _dpadRight.Clicked += Button_Pressed;
            _minus.Clicked += Button_Pressed;
            _l.Clicked += Button_Pressed;
            _zL.Clicked += Button_Pressed;
            _lSl.Clicked += Button_Pressed;
            _lSr.Clicked += Button_Pressed;
            _rStick.Clicked += ButtonForStick_Pressed;
            _rStickUp.Clicked += Button_Pressed;
            _rStickDown.Clicked += Button_Pressed;
            _rStickLeft.Clicked += Button_Pressed;
            _rStickRight.Clicked += Button_Pressed;
            _rStickButton.Clicked += Button_Pressed;
            _a.Clicked += Button_Pressed;
            _b.Clicked += Button_Pressed;
            _x.Clicked += Button_Pressed;
            _y.Clicked += Button_Pressed;
            _plus.Clicked += Button_Pressed;
            _r.Clicked += Button_Pressed;
            _zR.Clicked += Button_Pressed;
            _rSl.Clicked += Button_Pressed;
            _rSr.Clicked += Button_Pressed;
            _enableCemuHook.Clicked += CemuHookCheckButtonPressed;

            // Setup current values.
            UpdateInputDeviceList();
            SetAvailableOptions();

            ClearValues();
            if (_inputDevice.ActiveId != null)
            {
                SetCurrentValues();
            }

            mainWindow.InputManager.GamepadDriver.OnGamepadConnected += HandleOnGamepadConnected;
            mainWindow.InputManager.GamepadDriver.OnGamepadDisconnected += HandleOnGamepadDisconnected;

            _mainWindow.RendererWidget?.NpadManager.BlockInputUpdates();
        }

        private void CemuHookCheckButtonPressed(object sender, EventArgs e)
        {
            UpdateCemuHookSpecificFieldsVisibility();
        }

        private void HandleOnGamepadDisconnected(string id)
        {
            Application.Invoke(delegate
            {
                UpdateInputDeviceList();
            });
        }

        private void HandleOnGamepadConnected(string id)
        {
            Application.Invoke(delegate
            {
                UpdateInputDeviceList();
            });
        }

        protected override void OnDestroyed()
        {
            _mainWindow.InputManager.GamepadDriver.OnGamepadConnected -= HandleOnGamepadConnected;
            _mainWindow.InputManager.GamepadDriver.OnGamepadDisconnected -= HandleOnGamepadDisconnected;

            _mainWindow.RendererWidget?.NpadManager.UnblockInputUpdates();

            _selectedGamepad?.Dispose();

            _gtk3KeyboardDriver.Dispose();
        }

        private static string GetShortGamepadName(string str)
        {
            const string ShrinkChars = "...";
            const int MaxSize = 50;

            if (str.Length > MaxSize)
            {
                return $"{str.AsSpan(0, MaxSize - ShrinkChars.Length)}{ShrinkChars}";
            }

            return str;
        }

        private void UpdateInputDeviceList()
        {
            _inputDevice.RemoveAll();
            _inputDevice.Append("disabled", "Disabled");
            _inputDevice.SetActiveId("disabled");

            foreach (string id in _mainWindow.InputManager.KeyboardDriver.GamepadsIds)
            {
                IGamepad gamepad = _mainWindow.InputManager.KeyboardDriver.GetGamepad(id);

                if (gamepad != null)
                {
                    _inputDevice.Append($"keyboard/{id}", GetShortGamepadName($"{gamepad.Name} ({id})"));

                    gamepad.Dispose();
                }
            }

            foreach (string id in _mainWindow.InputManager.GamepadDriver.GamepadsIds)
            {
                IGamepad gamepad = _mainWindow.InputManager.GamepadDriver.GetGamepad(id);

                if (gamepad != null)
                {
                    _inputDevice.Append($"controller/{id}", GetShortGamepadName($"{gamepad.Name} ({id})"));

                    gamepad.Dispose();
                }
            }

            switch (_inputConfig)
            {
                case StandardKeyboardInputConfig keyboard:
                    _inputDevice.SetActiveId($"keyboard/{keyboard.Id}");
                    break;
                case StandardControllerInputConfig controller:
                    _inputDevice.SetActiveId($"controller/{controller.Id}");
                    break;
            }
        }

        private void UpdateCemuHookSpecificFieldsVisibility()
        {
            if (_enableCemuHook.Active)
            {
                _dsuServerHostBox.Show();
                _dsuServerPortBox.Show();
                _motionControllerSlot.Show();
                _motionAltBox.Show();
                _mirrorInput.Show();
            }
            else
            {
                _dsuServerHostBox.Hide();
                _dsuServerPortBox.Hide();
                _motionControllerSlot.Hide();
                _motionAltBox.Hide();
                _mirrorInput.Hide();
            }
        }

        private void SetAvailableOptions()
        {
            if (_inputDevice.ActiveId != null && _inputDevice.ActiveId.StartsWith("keyboard"))
            {
                ShowAll();
                _leftStickController.Hide();
                _rightStickController.Hide();
                _deadZoneLeftBox.Hide();
                _deadZoneRightBox.Hide();
                _rangeLeftBox.Hide();
                _rangeRightBox.Hide();
                _triggerThresholdBox.Hide();
                _motionBox.Hide();
                _rumbleBox.Hide();
            }
            else if (_inputDevice.ActiveId != null && _inputDevice.ActiveId.StartsWith("controller"))
            {
                ShowAll();
                _leftStickKeyboard.Hide();
                _rightStickKeyboard.Hide();

                UpdateCemuHookSpecificFieldsVisibility();
            }
            else
            {
                _settingsBox.Hide();
            }

            ClearValues();
        }

        private void SetCurrentValues()
        {
            SetControllerSpecificFields();

            SetProfiles();

            if (_inputDevice.ActiveId.StartsWith("keyboard") && _inputConfig is StandardKeyboardInputConfig)
            {
                SetValues(_inputConfig);
            }
            else if (_inputDevice.ActiveId.StartsWith("controller") && _inputConfig is StandardControllerInputConfig)
            {
                SetValues(_inputConfig);
            }
        }

        private void SetControllerSpecificFields()
        {
            _leftSideTriggerBox.Hide();
            _rightSideTriggerBox.Hide();
            _motionAltBox.Hide();

            switch (_controllerType.ActiveId)
            {
                case "JoyconLeft":
                    _leftSideTriggerBox.Show();
                    break;
                case "JoyconRight":
                    _rightSideTriggerBox.Show();
                    break;
                case "JoyconPair":
                    _motionAltBox.Show();
                    break;
            }

            if (!OperatingSystem.IsMacOS())
            {
                _controllerImage.Pixbuf = _controllerType.ActiveId switch
                {
                    "ProController" => new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Controller_ProCon.svg", 400, 400),
                    "JoyconLeft" => new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Controller_JoyConLeft.svg", 400, 500),
                    "JoyconRight" => new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Controller_JoyConRight.svg", 400, 500),
                    _ => new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Controller_JoyConPair.svg", 400, 500),
                };
            }
        }

        private void ClearValues()
        {
            _lStick.Label = "Unbound";
            _lStickUp.Label = "Unbound";
            _lStickDown.Label = "Unbound";
            _lStickLeft.Label = "Unbound";
            _lStickRight.Label = "Unbound";
            _lStickButton.Label = "Unbound";
            _dpadUp.Label = "Unbound";
            _dpadDown.Label = "Unbound";
            _dpadLeft.Label = "Unbound";
            _dpadRight.Label = "Unbound";
            _minus.Label = "Unbound";
            _l.Label = "Unbound";
            _zL.Label = "Unbound";
            _lSl.Label = "Unbound";
            _lSr.Label = "Unbound";
            _rStick.Label = "Unbound";
            _rStickUp.Label = "Unbound";
            _rStickDown.Label = "Unbound";
            _rStickLeft.Label = "Unbound";
            _rStickRight.Label = "Unbound";
            _rStickButton.Label = "Unbound";
            _a.Label = "Unbound";
            _b.Label = "Unbound";
            _x.Label = "Unbound";
            _y.Label = "Unbound";
            _plus.Label = "Unbound";
            _r.Label = "Unbound";
            _zR.Label = "Unbound";
            _rSl.Label = "Unbound";
            _rSr.Label = "Unbound";
            _controllerStrongRumble.Value = 1;
            _controllerWeakRumble.Value = 1;
            _controllerDeadzoneLeft.Value = 0;
            _controllerDeadzoneRight.Value = 0;
            _controllerRangeLeft.Value = 1;
            _controllerRangeRight.Value = 1;
            _controllerTriggerThreshold.Value = 0;
            _mirrorInput.Active = false;
            _enableMotion.Active = false;
            _enableCemuHook.Active = false;
            _slotNumber.Value = 0;
            _altSlotNumber.Value = 0;
            _sensitivity.Value = 100;
            _gyroDeadzone.Value = 1;
            _dsuServerHost.Buffer.Text = "";
            _dsuServerPort.Buffer.Text = "";
            _enableRumble.Active = false;
        }

        private void SetValues(InputConfig config)
        {
            switch (config)
            {
                case StandardKeyboardInputConfig keyboardConfig:
                    if (!_controllerType.SetActiveId(keyboardConfig.ControllerType.ToString()))
                    {
                        _controllerType.SetActiveId(_playerIndex == PlayerIndex.Handheld
                            ? ControllerType.Handheld.ToString()
                            : ControllerType.ProController.ToString());
                    }

                    _lStickUp.Label = keyboardConfig.LeftJoyconStick.StickUp.ToString();
                    _lStickDown.Label = keyboardConfig.LeftJoyconStick.StickDown.ToString();
                    _lStickLeft.Label = keyboardConfig.LeftJoyconStick.StickLeft.ToString();
                    _lStickRight.Label = keyboardConfig.LeftJoyconStick.StickRight.ToString();
                    _lStickButton.Label = keyboardConfig.LeftJoyconStick.StickButton.ToString();
                    _dpadUp.Label = keyboardConfig.LeftJoycon.DpadUp.ToString();
                    _dpadDown.Label = keyboardConfig.LeftJoycon.DpadDown.ToString();
                    _dpadLeft.Label = keyboardConfig.LeftJoycon.DpadLeft.ToString();
                    _dpadRight.Label = keyboardConfig.LeftJoycon.DpadRight.ToString();
                    _minus.Label = keyboardConfig.LeftJoycon.ButtonMinus.ToString();
                    _l.Label = keyboardConfig.LeftJoycon.ButtonL.ToString();
                    _zL.Label = keyboardConfig.LeftJoycon.ButtonZl.ToString();
                    _lSl.Label = keyboardConfig.LeftJoycon.ButtonSl.ToString();
                    _lSr.Label = keyboardConfig.LeftJoycon.ButtonSr.ToString();
                    _rStickUp.Label = keyboardConfig.RightJoyconStick.StickUp.ToString();
                    _rStickDown.Label = keyboardConfig.RightJoyconStick.StickDown.ToString();
                    _rStickLeft.Label = keyboardConfig.RightJoyconStick.StickLeft.ToString();
                    _rStickRight.Label = keyboardConfig.RightJoyconStick.StickRight.ToString();
                    _rStickButton.Label = keyboardConfig.RightJoyconStick.StickButton.ToString();
                    _a.Label = keyboardConfig.RightJoycon.ButtonA.ToString();
                    _b.Label = keyboardConfig.RightJoycon.ButtonB.ToString();
                    _x.Label = keyboardConfig.RightJoycon.ButtonX.ToString();
                    _y.Label = keyboardConfig.RightJoycon.ButtonY.ToString();
                    _plus.Label = keyboardConfig.RightJoycon.ButtonPlus.ToString();
                    _r.Label = keyboardConfig.RightJoycon.ButtonR.ToString();
                    _zR.Label = keyboardConfig.RightJoycon.ButtonZr.ToString();
                    _rSl.Label = keyboardConfig.RightJoycon.ButtonSl.ToString();
                    _rSr.Label = keyboardConfig.RightJoycon.ButtonSr.ToString();
                    break;

                case StandardControllerInputConfig controllerConfig:
                    if (!_controllerType.SetActiveId(controllerConfig.ControllerType.ToString()))
                    {
                        _controllerType.SetActiveId(_playerIndex == PlayerIndex.Handheld
                            ? ControllerType.Handheld.ToString()
                            : ControllerType.ProController.ToString());
                    }

                    _lStick.Label = controllerConfig.LeftJoyconStick.Joystick.ToString();
                    _invertLStickX.Active = controllerConfig.LeftJoyconStick.InvertStickX;
                    _invertLStickY.Active = controllerConfig.LeftJoyconStick.InvertStickY;
                    _rotateL90CW.Active = controllerConfig.LeftJoyconStick.Rotate90CW;
                    _lStickButton.Label = controllerConfig.LeftJoyconStick.StickButton.ToString();
                    _dpadUp.Label = controllerConfig.LeftJoycon.DpadUp.ToString();
                    _dpadDown.Label = controllerConfig.LeftJoycon.DpadDown.ToString();
                    _dpadLeft.Label = controllerConfig.LeftJoycon.DpadLeft.ToString();
                    _dpadRight.Label = controllerConfig.LeftJoycon.DpadRight.ToString();
                    _minus.Label = controllerConfig.LeftJoycon.ButtonMinus.ToString();
                    _l.Label = controllerConfig.LeftJoycon.ButtonL.ToString();
                    _zL.Label = controllerConfig.LeftJoycon.ButtonZl.ToString();
                    _lSl.Label = controllerConfig.LeftJoycon.ButtonSl.ToString();
                    _lSr.Label = controllerConfig.LeftJoycon.ButtonSr.ToString();
                    _rStick.Label = controllerConfig.RightJoyconStick.Joystick.ToString();
                    _invertRStickX.Active = controllerConfig.RightJoyconStick.InvertStickX;
                    _invertRStickY.Active = controllerConfig.RightJoyconStick.InvertStickY;
                    _rotateR90CW.Active = controllerConfig.RightJoyconStick.Rotate90CW;
                    _rStickButton.Label = controllerConfig.RightJoyconStick.StickButton.ToString();
                    _a.Label = controllerConfig.RightJoycon.ButtonA.ToString();
                    _b.Label = controllerConfig.RightJoycon.ButtonB.ToString();
                    _x.Label = controllerConfig.RightJoycon.ButtonX.ToString();
                    _y.Label = controllerConfig.RightJoycon.ButtonY.ToString();
                    _plus.Label = controllerConfig.RightJoycon.ButtonPlus.ToString();
                    _r.Label = controllerConfig.RightJoycon.ButtonR.ToString();
                    _zR.Label = controllerConfig.RightJoycon.ButtonZr.ToString();
                    _rSl.Label = controllerConfig.RightJoycon.ButtonSl.ToString();
                    _rSr.Label = controllerConfig.RightJoycon.ButtonSr.ToString();
                    _controllerStrongRumble.Value = controllerConfig.Rumble.StrongRumble;
                    _controllerWeakRumble.Value = controllerConfig.Rumble.WeakRumble;
                    _enableRumble.Active = controllerConfig.Rumble.EnableRumble;
                    _controllerDeadzoneLeft.Value = controllerConfig.DeadzoneLeft;
                    _controllerDeadzoneRight.Value = controllerConfig.DeadzoneRight;
                    _controllerRangeLeft.Value = controllerConfig.RangeLeft;
                    _controllerRangeRight.Value = controllerConfig.RangeRight;
                    _controllerTriggerThreshold.Value = controllerConfig.TriggerThreshold;
                    _sensitivity.Value = controllerConfig.Motion.Sensitivity;
                    _gyroDeadzone.Value = controllerConfig.Motion.GyroDeadzone;
                    _enableMotion.Active = controllerConfig.Motion.EnableMotion;
                    _enableCemuHook.Active = controllerConfig.Motion.MotionBackend == MotionInputBackendType.CemuHook;

                    // If both stick ranges are 0 (usually indicative of an outdated profile load) then both sticks will be set to 1.0.
                    if (_controllerRangeLeft.Value <= 0.0 && _controllerRangeRight.Value <= 0.0)
                    {
                        _controllerRangeLeft.Value = 1.0;
                        _controllerRangeRight.Value = 1.0;

                        Logger.Info?.Print(LogClass.Application, $"{config.PlayerIndex} stick range reset. Save the profile now to update your configuration");
                    }

                    if (controllerConfig.Motion is CemuHookMotionConfigController cemuHookMotionConfig)
                    {
                        _slotNumber.Value = cemuHookMotionConfig.Slot;
                        _altSlotNumber.Value = cemuHookMotionConfig.AltSlot;
                        _mirrorInput.Active = cemuHookMotionConfig.MirrorInput;
                        _dsuServerHost.Buffer.Text = cemuHookMotionConfig.DsuServerHost;
                        _dsuServerPort.Buffer.Text = cemuHookMotionConfig.DsuServerPort.ToString();
                    }

                    break;
            }
        }

        private InputConfig GetValues()
        {
            if (_inputDevice.ActiveId.StartsWith("keyboard"))
            {
#pragma warning disable CA1806, IDE0055 // Disable formatting
                Enum.TryParse(_lStickUp.Label,     out Key lStickUp);
                Enum.TryParse(_lStickDown.Label,   out Key lStickDown);
                Enum.TryParse(_lStickLeft.Label,   out Key lStickLeft);
                Enum.TryParse(_lStickRight.Label,  out Key lStickRight);
                Enum.TryParse(_lStickButton.Label, out Key lStickButton);
                Enum.TryParse(_dpadUp.Label,       out Key lDPadUp);
                Enum.TryParse(_dpadDown.Label,     out Key lDPadDown);
                Enum.TryParse(_dpadLeft.Label,     out Key lDPadLeft);
                Enum.TryParse(_dpadRight.Label,    out Key lDPadRight);
                Enum.TryParse(_minus.Label,        out Key lButtonMinus);
                Enum.TryParse(_l.Label,            out Key lButtonL);
                Enum.TryParse(_zL.Label,           out Key lButtonZl);
                Enum.TryParse(_lSl.Label,          out Key lButtonSl);
                Enum.TryParse(_lSr.Label,          out Key lButtonSr);

                Enum.TryParse(_rStickUp.Label,     out Key rStickUp);
                Enum.TryParse(_rStickDown.Label,   out Key rStickDown);
                Enum.TryParse(_rStickLeft.Label,   out Key rStickLeft);
                Enum.TryParse(_rStickRight.Label,  out Key rStickRight);
                Enum.TryParse(_rStickButton.Label, out Key rStickButton);
                Enum.TryParse(_a.Label,            out Key rButtonA);
                Enum.TryParse(_b.Label,            out Key rButtonB);
                Enum.TryParse(_x.Label,            out Key rButtonX);
                Enum.TryParse(_y.Label,            out Key rButtonY);
                Enum.TryParse(_plus.Label,         out Key rButtonPlus);
                Enum.TryParse(_r.Label,            out Key rButtonR);
                Enum.TryParse(_zR.Label,           out Key rButtonZr);
                Enum.TryParse(_rSl.Label,          out Key rButtonSl);
                Enum.TryParse(_rSr.Label,          out Key rButtonSr);
#pragma warning restore CA1806, IDE0055

                return new StandardKeyboardInputConfig
                {
                    Backend = InputBackendType.WindowKeyboard,
                    Version = InputConfig.CurrentVersion,
                    Id = _inputDevice.ActiveId.Split("/")[1],
                    ControllerType = Enum.Parse<ControllerType>(_controllerType.ActiveId),
                    PlayerIndex = _playerIndex,
                    LeftJoycon = new LeftJoyconCommonConfig<Key>
                    {
                        ButtonMinus = lButtonMinus,
                        ButtonL = lButtonL,
                        ButtonZl = lButtonZl,
                        ButtonSl = lButtonSl,
                        ButtonSr = lButtonSr,
                        DpadUp = lDPadUp,
                        DpadDown = lDPadDown,
                        DpadLeft = lDPadLeft,
                        DpadRight = lDPadRight,
                    },
                    LeftJoyconStick = new JoyconConfigKeyboardStick<Key>
                    {
                        StickUp = lStickUp,
                        StickDown = lStickDown,
                        StickLeft = lStickLeft,
                        StickRight = lStickRight,
                        StickButton = lStickButton,
                    },
                    RightJoycon = new RightJoyconCommonConfig<Key>
                    {
                        ButtonA = rButtonA,
                        ButtonB = rButtonB,
                        ButtonX = rButtonX,
                        ButtonY = rButtonY,
                        ButtonPlus = rButtonPlus,
                        ButtonR = rButtonR,
                        ButtonZr = rButtonZr,
                        ButtonSl = rButtonSl,
                        ButtonSr = rButtonSr,
                    },
                    RightJoyconStick = new JoyconConfigKeyboardStick<Key>
                    {
                        StickUp = rStickUp,
                        StickDown = rStickDown,
                        StickLeft = rStickLeft,
                        StickRight = rStickRight,
                        StickButton = rStickButton,
                    },
                };
            }

            if (_inputDevice.ActiveId.StartsWith("controller"))
            {
#pragma warning disable CA1806, IDE0055 // Disable formatting
                Enum.TryParse(_lStick.Label,       out ConfigStickInputId   lStick);
                Enum.TryParse(_lStickButton.Label, out ConfigGamepadInputId lStickButton);
                Enum.TryParse(_minus.Label,        out ConfigGamepadInputId lButtonMinus);
                Enum.TryParse(_l.Label,            out ConfigGamepadInputId lButtonL);
                Enum.TryParse(_zL.Label,           out ConfigGamepadInputId lButtonZl);
                Enum.TryParse(_lSl.Label,          out ConfigGamepadInputId lButtonSl);
                Enum.TryParse(_lSr.Label,          out ConfigGamepadInputId lButtonSr);
                Enum.TryParse(_dpadUp.Label,       out ConfigGamepadInputId lDPadUp);
                Enum.TryParse(_dpadDown.Label,     out ConfigGamepadInputId lDPadDown);
                Enum.TryParse(_dpadLeft.Label,     out ConfigGamepadInputId lDPadLeft);
                Enum.TryParse(_dpadRight.Label,    out ConfigGamepadInputId lDPadRight);

                Enum.TryParse(_rStick.Label,       out ConfigStickInputId   rStick);
                Enum.TryParse(_rStickButton.Label, out ConfigGamepadInputId rStickButton);
                Enum.TryParse(_a.Label,            out ConfigGamepadInputId rButtonA);
                Enum.TryParse(_b.Label,            out ConfigGamepadInputId rButtonB);
                Enum.TryParse(_x.Label,            out ConfigGamepadInputId rButtonX);
                Enum.TryParse(_y.Label,            out ConfigGamepadInputId rButtonY);
                Enum.TryParse(_plus.Label,         out ConfigGamepadInputId rButtonPlus);
                Enum.TryParse(_r.Label,            out ConfigGamepadInputId rButtonR);
                Enum.TryParse(_zR.Label,           out ConfigGamepadInputId rButtonZr);
                Enum.TryParse(_rSl.Label,          out ConfigGamepadInputId rButtonSl);
                Enum.TryParse(_rSr.Label,          out ConfigGamepadInputId rButtonSr);

                int.TryParse(_dsuServerPort.Buffer.Text, out int port);
#pragma warning restore CA1806, IDE0055

                MotionConfigController motionConfig;

                if (_enableCemuHook.Active)
                {
                    motionConfig = new CemuHookMotionConfigController
                    {
                        MotionBackend = MotionInputBackendType.CemuHook,
                        EnableMotion = _enableMotion.Active,
                        Sensitivity = (int)_sensitivity.Value,
                        GyroDeadzone = _gyroDeadzone.Value,
                        MirrorInput = _mirrorInput.Active,
                        Slot = (int)_slotNumber.Value,
                        AltSlot = (int)_altSlotNumber.Value,
                        DsuServerHost = _dsuServerHost.Buffer.Text,
                        DsuServerPort = port,
                    };
                }
                else
                {
                    motionConfig = new StandardMotionConfigController
                    {
                        MotionBackend = MotionInputBackendType.GamepadDriver,
                        EnableMotion = _enableMotion.Active,
                        Sensitivity = (int)_sensitivity.Value,
                        GyroDeadzone = _gyroDeadzone.Value,
                    };
                }

                return new StandardControllerInputConfig
                {
                    Backend = InputBackendType.GamepadSDL2,
                    Version = InputConfig.CurrentVersion,
                    Id = _inputDevice.ActiveId.Split("/")[1].Split(" ")[0],
                    ControllerType = Enum.Parse<ControllerType>(_controllerType.ActiveId),
                    PlayerIndex = _playerIndex,
                    DeadzoneLeft = (float)_controllerDeadzoneLeft.Value,
                    DeadzoneRight = (float)_controllerDeadzoneRight.Value,
                    RangeLeft = (float)_controllerRangeLeft.Value,
                    RangeRight = (float)_controllerRangeRight.Value,
                    TriggerThreshold = (float)_controllerTriggerThreshold.Value,
                    LeftJoycon = new LeftJoyconCommonConfig<ConfigGamepadInputId>
                    {
                        ButtonMinus = lButtonMinus,
                        ButtonL = lButtonL,
                        ButtonZl = lButtonZl,
                        ButtonSl = lButtonSl,
                        ButtonSr = lButtonSr,
                        DpadUp = lDPadUp,
                        DpadDown = lDPadDown,
                        DpadLeft = lDPadLeft,
                        DpadRight = lDPadRight,
                    },
                    LeftJoyconStick = new JoyconConfigControllerStick<ConfigGamepadInputId, ConfigStickInputId>
                    {
                        InvertStickX = _invertLStickX.Active,
                        Joystick = lStick,
                        InvertStickY = _invertLStickY.Active,
                        StickButton = lStickButton,
                        Rotate90CW = _rotateL90CW.Active,
                    },
                    RightJoycon = new RightJoyconCommonConfig<ConfigGamepadInputId>
                    {
                        ButtonA = rButtonA,
                        ButtonB = rButtonB,
                        ButtonX = rButtonX,
                        ButtonY = rButtonY,
                        ButtonPlus = rButtonPlus,
                        ButtonR = rButtonR,
                        ButtonZr = rButtonZr,
                        ButtonSl = rButtonSl,
                        ButtonSr = rButtonSr,
                    },
                    RightJoyconStick = new JoyconConfigControllerStick<ConfigGamepadInputId, ConfigStickInputId>
                    {
                        InvertStickX = _invertRStickX.Active,
                        Joystick = rStick,
                        InvertStickY = _invertRStickY.Active,
                        StickButton = rStickButton,
                        Rotate90CW = _rotateR90CW.Active,
                    },
                    Motion = motionConfig,
                    Rumble = new RumbleConfigController
                    {
                        StrongRumble = (float)_controllerStrongRumble.Value,
                        WeakRumble = (float)_controllerWeakRumble.Value,
                        EnableRumble = _enableRumble.Active,
                    },
                };
            }

            if (!_inputDevice.ActiveId.StartsWith("disabled"))
            {
                GtkDialog.CreateErrorDialog("Invalid data detected in one or more fields; the configuration was not saved.");
            }

            return null;
        }

        private string GetProfileBasePath()
        {
            if (_inputDevice.ActiveId.StartsWith("keyboard"))
            {
                return System.IO.Path.Combine(AppDataManager.ProfilesDirPath, "keyboard");
            }
            else if (_inputDevice.ActiveId.StartsWith("controller"))
            {
                return System.IO.Path.Combine(AppDataManager.ProfilesDirPath, "controller");
            }

            return AppDataManager.ProfilesDirPath;
        }

        //
        // Events
        //
        private void InputDevice_Changed(object sender, EventArgs args)
        {
            SetAvailableOptions();
            SetControllerSpecificFields();

            _selectedGamepad?.Dispose();
            _selectedGamepad = null;

            if (_inputDevice.ActiveId != null)
            {
                SetProfiles();

                string id = GetCurrentGamepadId();

                if (_inputDevice.ActiveId.StartsWith("keyboard"))
                {
                    if (_inputConfig is StandardKeyboardInputConfig)
                    {
                        SetValues(_inputConfig);
                    }

                    if (_mainWindow.InputManager.KeyboardDriver is GTK3KeyboardDriver)
                    {
                        // NOTE: To get input in this window, we need to bind a custom keyboard driver instead of using the InputManager one as the main window isn't focused...
                        _selectedGamepad = _gtk3KeyboardDriver.GetGamepad(id);
                    }
                    else
                    {
                        _selectedGamepad = _mainWindow.InputManager.KeyboardDriver.GetGamepad(id);
                    }
                }
                else if (_inputDevice.ActiveId.StartsWith("controller"))
                {
                    if (_inputConfig is StandardControllerInputConfig)
                    {
                        SetValues(_inputConfig);
                    }

                    _selectedGamepad = _mainWindow.InputManager.GamepadDriver.GetGamepad(id);
                }
            }
        }

        private string GetCurrentGamepadId()
        {
            if (_inputDevice.ActiveId == null || _inputDevice.ActiveId == "disabled")
            {
                return null;
            }

            return _inputDevice.ActiveId.Split("/")[1].Split(" ")[0];
        }

        private void Controller_Changed(object sender, EventArgs args)
        {
            SetControllerSpecificFields();
        }

        private IButtonAssigner CreateButtonAssigner(bool forStick)
        {
            IButtonAssigner assigner;

            if (_inputDevice.ActiveId.StartsWith("keyboard"))
            {
                assigner = new KeyboardKeyAssigner((IKeyboard)_selectedGamepad);
            }
            else if (_inputDevice.ActiveId.StartsWith("controller"))
            {
                assigner = new GamepadButtonAssigner(_selectedGamepad, (float)_controllerTriggerThreshold.Value, forStick);
            }
            else
            {
                throw new Exception("Controller not supported");
            }

            return assigner;
        }

        private void HandleButtonPressed(ToggleButton button, bool forStick)
        {
            if (_isWaitingForInput)
            {
                button.Active = false;

                return;
            }

            _mousePressed = false;

            ButtonPressEvent += MouseClick;

            IButtonAssigner assigner = CreateButtonAssigner(forStick);

            _isWaitingForInput = true;

            // Open GTK3 keyboard for cancel operations
            IKeyboard keyboard = (IKeyboard)_gtk3KeyboardDriver.GetGamepad("0");

            Thread inputThread = new(() =>
            {
                assigner.Initialize();

                while (true)
                {
                    Thread.Sleep(10);
                    assigner.ReadInput();

                    if (_mousePressed || keyboard.IsPressed(Ryujinx.Input.Key.Escape) || assigner.HasAnyButtonPressed() || assigner.ShouldCancel())
                    {
                        break;
                    }
                }

                string pressedButton = assigner.GetPressedButton();

                Application.Invoke(delegate
                {
                    if (_middleMousePressed)
                    {
                        button.Label = "Unbound";
                    }
                    else if (pressedButton != "")
                    {
                        button.Label = pressedButton;
                    }

                    _middleMousePressed = false;

                    ButtonPressEvent -= MouseClick;
                    keyboard.Dispose();

                    button.Active = false;
                    _isWaitingForInput = false;
                });
            })
            {
                Name = "GUI.InputThread",
                IsBackground = true,
            };
            inputThread.Start();
        }

        private void Button_Pressed(object sender, EventArgs args)
        {
            HandleButtonPressed((ToggleButton)sender, false);
        }

        private void ButtonForStick_Pressed(object sender, EventArgs args)
        {
            HandleButtonPressed((ToggleButton)sender, true);
        }

        private void MouseClick(object sender, ButtonPressEventArgs args)
        {
            _mousePressed = true;
            _middleMousePressed = args.Event.Button == 2;
        }

        private void SetProfiles()
        {
            _profile.RemoveAll();

            string basePath = GetProfileBasePath();

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            if (_inputDevice.ActiveId == null || _inputDevice.ActiveId.Equals("disabled"))
            {
                _profile.Append("default", "None");
            }
            else
            {
                _profile.Append("default", "Default");

                foreach (string profile in Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories))
                {
                    _profile.Append(System.IO.Path.GetFileName(profile), System.IO.Path.GetFileNameWithoutExtension(profile));
                }
            }

            _profile.SetActiveId("default");
        }

        private void ProfileLoad_Activated(object sender, EventArgs args)
        {
            ((ToggleButton)sender).SetStateFlags(StateFlags.Normal, true);

            if (_inputDevice.ActiveId == "disabled" || _profile.ActiveId == null)
            {
                return;
            }

            InputConfig config = null;
            int pos = _profile.Active;

            if (_profile.ActiveId == "default")
            {
                if (_inputDevice.ActiveId.StartsWith("keyboard"))
                {
                    config = new StandardKeyboardInputConfig
                    {
                        Version = InputConfig.CurrentVersion,
                        Backend = InputBackendType.WindowKeyboard,
                        Id = null,
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

                        LeftJoyconStick = new JoyconConfigKeyboardStick<Key>
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
                else if (_inputDevice.ActiveId.StartsWith("controller"))
                {
                    bool isNintendoStyle = _inputDevice.ActiveText.Contains("Nintendo");

                    config = new StandardControllerInputConfig
                    {
                        Version = InputConfig.CurrentVersion,
                        Backend = InputBackendType.GamepadSDL2,
                        Id = null,
                        ControllerType = ControllerType.JoyconPair,
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
                            Rotate90CW = false,
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
                            Rotate90CW = false,
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
            }
            else
            {
                string path = System.IO.Path.Combine(GetProfileBasePath(), _profile.ActiveId);

                if (!File.Exists(path))
                {
                    if (pos >= 0)
                    {
                        _profile.Remove(pos);
                    }

                    return;
                }

                try
                {
                    config = JsonHelper.DeserializeFromFile(path, _serializerContext.InputConfig);
                }
                catch (JsonException) { }
            }

            SetValues(config);
        }

        private void ProfileAdd_Activated(object sender, EventArgs args)
        {
            ((ToggleButton)sender).SetStateFlags(StateFlags.Normal, true);

            if (_inputDevice.ActiveId == "disabled")
            {
                return;
            }

            InputConfig inputConfig = GetValues();
            ProfileDialog profileDialog = new();

            if (inputConfig == null)
            {
                return;
            }

            if (profileDialog.Run() == (int)ResponseType.Ok)
            {
                string path = System.IO.Path.Combine(GetProfileBasePath(), profileDialog.FileName);
                string jsonString = JsonHelper.Serialize(inputConfig, _serializerContext.InputConfig);

                File.WriteAllText(path, jsonString);
            }

            profileDialog.Dispose();

            SetProfiles();
        }

        private void ProfileRemove_Activated(object sender, EventArgs args)
        {
            ((ToggleButton)sender).SetStateFlags(StateFlags.Normal, true);

            if (_inputDevice.ActiveId == "disabled" || _profile.ActiveId == "default" || _profile.ActiveId == null)
            {
                return;
            }

            MessageDialog confirmDialog = GtkDialog.CreateConfirmationDialog("Deleting Profile", "This action is irreversible, are you sure you want to continue?");

            if (confirmDialog.Run() == (int)ResponseType.Yes)
            {
                string path = System.IO.Path.Combine(GetProfileBasePath(), _profile.ActiveId);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                SetProfiles();
            }
        }

        private void SaveToggle_Activated(object sender, EventArgs args)
        {
            InputConfig inputConfig = GetValues();

            var newConfig = new List<InputConfig>();
            newConfig.AddRange(ConfigurationState.Instance.Hid.InputConfig.Value);

            if (_inputConfig == null && inputConfig != null)
            {
                newConfig.Add(inputConfig);
            }
            else
            {
                if (_inputDevice.ActiveId == "disabled")
                {
                    newConfig.Remove(_inputConfig);
                }
                else if (inputConfig != null)
                {
                    int index = newConfig.IndexOf(_inputConfig);

                    newConfig[index] = inputConfig;
                }
            }

            _mainWindow.RendererWidget?.NpadManager.ReloadConfiguration(newConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);

            // Atomically replace and signal input change.
            // NOTE: Do not modify InputConfig.Value directly as other code depends on the on-change event.
            ConfigurationState.Instance.Hid.InputConfig.Value = newConfig;

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

            Dispose();
        }

        private void CloseToggle_Activated(object sender, EventArgs args)
        {
            Dispose();
        }
    }
}
