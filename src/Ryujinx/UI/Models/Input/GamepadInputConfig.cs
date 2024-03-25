using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Configuration.Hid.Controller.Motion;
using System;

namespace Ryujinx.Ava.UI.Models.Input
{
    public class GamepadInputConfig : BaseModel
    {
        public bool EnableCemuHookMotion { get; set; }
        public string DsuServerHost { get; set; }
        public int DsuServerPort { get; set; }
        public int Slot { get; set; }
        public int AltSlot { get; set; }
        public bool MirrorInput { get; set; }
        public int Sensitivity { get; set; }
        public double GyroDeadzone { get; set; }

        public float WeakRumble { get; set; }
        public float StrongRumble { get; set; }

        public string Id { get; set; }
        public ControllerType ControllerType { get; set; }
        public PlayerIndex PlayerIndex { get; set; }

        private StickInputId _leftJoystick;
        public StickInputId LeftJoystick
        {
            get => _leftJoystick;
            set
            {
                _leftJoystick = value;
                OnPropertyChanged();
            }
        }

        private bool _leftInvertStickX;
        public bool LeftInvertStickX
        {
            get => _leftInvertStickX;
            set
            {
                _leftInvertStickX = value;
                OnPropertyChanged();
            }
        }

        private bool _leftInvertStickY;
        public bool LeftInvertStickY
        {
            get => _leftInvertStickY;
            set
            {
                _leftInvertStickY = value;
                OnPropertyChanged();
            }
        }

        private bool _leftRotate90;
        public bool LeftRotate90
        {
            get => _leftRotate90;
            set
            {
                _leftRotate90 = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _leftStickButton;
        public GamepadInputId LeftStickButton
        {
            get => _leftStickButton;
            set
            {
                _leftStickButton = value;
                OnPropertyChanged();
            }
        }

        private StickInputId _rightJoystick;
        public StickInputId RightJoystick
        {
            get => _rightJoystick;
            set
            {
                _rightJoystick = value;
                OnPropertyChanged();
            }
        }

        private bool _rightInvertStickX;
        public bool RightInvertStickX
        {
            get => _rightInvertStickX;
            set
            {
                _rightInvertStickX = value;
                OnPropertyChanged();
            }
        }

        private bool _rightInvertStickY;
        public bool RightInvertStickY
        {
            get => _rightInvertStickY;
            set
            {
                _rightInvertStickY = value;
                OnPropertyChanged();
            }
        }

        private bool _rightRotate90;
        public bool RightRotate90
        {
            get => _rightRotate90;
            set
            {
                _rightRotate90 = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _rightStickButton;
        public GamepadInputId RightStickButton
        {
            get => _rightStickButton;
            set
            {
                _rightStickButton = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _dpadUp;
        public GamepadInputId DpadUp
        {
            get => _dpadUp;
            set
            {
                _dpadUp = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _dpadDown;
        public GamepadInputId DpadDown
        {
            get => _dpadDown;
            set
            {
                _dpadDown = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _dpadLeft;
        public GamepadInputId DpadLeft
        {
            get => _dpadLeft;
            set
            {
                _dpadLeft = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _dpadRight;
        public GamepadInputId DpadRight
        {
            get => _dpadRight;
            set
            {
                _dpadRight = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonL;
        public GamepadInputId ButtonL
        {
            get => _buttonL;
            set
            {
                _buttonL = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonMinus;
        public GamepadInputId ButtonMinus
        {
            get => _buttonMinus;
            set
            {
                _buttonMinus = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _leftButtonSl;
        public GamepadInputId LeftButtonSl
        {
            get => _leftButtonSl;
            set
            {
                _leftButtonSl = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _leftButtonSr;
        public GamepadInputId LeftButtonSr
        {
            get => _leftButtonSr;
            set
            {
                _leftButtonSr = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonZl;
        public GamepadInputId ButtonZl
        {
            get => _buttonZl;
            set
            {
                _buttonZl = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonA;
        public GamepadInputId ButtonA
        {
            get => _buttonA;
            set
            {
                _buttonA = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonB;
        public GamepadInputId ButtonB
        {
            get => _buttonB;
            set
            {
                _buttonB = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonX;
        public GamepadInputId ButtonX
        {
            get => _buttonX;
            set
            {
                _buttonX = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonY;
        public GamepadInputId ButtonY
        {
            get => _buttonY;
            set
            {
                _buttonY = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonR;
        public GamepadInputId ButtonR
        {
            get => _buttonR;
            set
            {
                _buttonR = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonPlus;
        public GamepadInputId ButtonPlus
        {
            get => _buttonPlus;
            set
            {
                _buttonPlus = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _rightButtonSl;
        public GamepadInputId RightButtonSl
        {
            get => _rightButtonSl;
            set
            {
                _rightButtonSl = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _rightButtonSr;
        public GamepadInputId RightButtonSr
        {
            get => _rightButtonSr;
            set
            {
                _rightButtonSr = value;
                OnPropertyChanged();
            }
        }

        private GamepadInputId _buttonZr;
        public GamepadInputId ButtonZr
        {
            get => _buttonZr;
            set
            {
                _buttonZr = value;
                OnPropertyChanged();
            }
        }

        private float _deadzoneLeft;
        public float DeadzoneLeft
        {
            get => _deadzoneLeft;
            set
            {
                _deadzoneLeft = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        private float _deadzoneRight;
        public float DeadzoneRight
        {
            get => _deadzoneRight;
            set
            {
                _deadzoneRight = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        private float _rangeLeft;
        public float RangeLeft
        {
            get => _rangeLeft;
            set
            {
                _rangeLeft = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        private float _rangeRight;
        public float RangeRight
        {
            get => _rangeRight;
            set
            {
                _rangeRight = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        private float _triggerThreshold;
        public float TriggerThreshold
        {
            get => _triggerThreshold;
            set
            {
                _triggerThreshold = MathF.Round(value, 3);
                OnPropertyChanged();
            }
        }

        private bool _enableMotion;
        public bool EnableMotion
        {
            get => _enableMotion;
            set
            {
                _enableMotion = value;
                OnPropertyChanged();
            }
        }

        private bool _enableRumble;
        public bool EnableRumble
        {
            get => _enableRumble;
            set
            {
                _enableRumble = value;
                OnPropertyChanged();
            }
        }

        public GamepadInputConfig(InputConfig config)
        {
            if (config != null)
            {
                Id = config.Id;
                ControllerType = config.ControllerType;
                PlayerIndex = config.PlayerIndex;

                if (config is not StandardControllerInputConfig controllerInput)
                {
                    return;
                }

                LeftJoystick = controllerInput.LeftJoyconStick.Joystick;
                LeftInvertStickX = controllerInput.LeftJoyconStick.InvertStickX;
                LeftInvertStickY = controllerInput.LeftJoyconStick.InvertStickY;
                LeftRotate90 = controllerInput.LeftJoyconStick.Rotate90CW;
                LeftStickButton = controllerInput.LeftJoyconStick.StickButton;

                RightJoystick = controllerInput.RightJoyconStick.Joystick;
                RightInvertStickX = controllerInput.RightJoyconStick.InvertStickX;
                RightInvertStickY = controllerInput.RightJoyconStick.InvertStickY;
                RightRotate90 = controllerInput.RightJoyconStick.Rotate90CW;
                RightStickButton = controllerInput.RightJoyconStick.StickButton;

                DpadUp = controllerInput.LeftJoycon.DpadUp;
                DpadDown = controllerInput.LeftJoycon.DpadDown;
                DpadLeft = controllerInput.LeftJoycon.DpadLeft;
                DpadRight = controllerInput.LeftJoycon.DpadRight;
                ButtonL = controllerInput.LeftJoycon.ButtonL;
                ButtonMinus = controllerInput.LeftJoycon.ButtonMinus;
                LeftButtonSl = controllerInput.LeftJoycon.ButtonSl;
                LeftButtonSr = controllerInput.LeftJoycon.ButtonSr;
                ButtonZl = controllerInput.LeftJoycon.ButtonZl;

                ButtonA = controllerInput.RightJoycon.ButtonA;
                ButtonB = controllerInput.RightJoycon.ButtonB;
                ButtonX = controllerInput.RightJoycon.ButtonX;
                ButtonY = controllerInput.RightJoycon.ButtonY;
                ButtonR = controllerInput.RightJoycon.ButtonR;
                ButtonPlus = controllerInput.RightJoycon.ButtonPlus;
                RightButtonSl = controllerInput.RightJoycon.ButtonSl;
                RightButtonSr = controllerInput.RightJoycon.ButtonSr;
                ButtonZr = controllerInput.RightJoycon.ButtonZr;

                DeadzoneLeft = controllerInput.DeadzoneLeft;
                DeadzoneRight = controllerInput.DeadzoneRight;
                RangeLeft = controllerInput.RangeLeft;
                RangeRight = controllerInput.RangeRight;
                TriggerThreshold = controllerInput.TriggerThreshold;

                if (controllerInput.Motion != null)
                {
                    EnableMotion = controllerInput.Motion.EnableMotion;
                    GyroDeadzone = controllerInput.Motion.GyroDeadzone;
                    Sensitivity = controllerInput.Motion.Sensitivity;

                    if (controllerInput.Motion is CemuHookMotionConfigController cemuHook)
                    {
                        EnableCemuHookMotion = true;
                        DsuServerHost = cemuHook.DsuServerHost;
                        DsuServerPort = cemuHook.DsuServerPort;
                        Slot = cemuHook.Slot;
                        AltSlot = cemuHook.AltSlot;
                        MirrorInput = cemuHook.MirrorInput;
                    }
                }

                if (controllerInput.Rumble != null)
                {
                    EnableRumble = controllerInput.Rumble.EnableRumble;
                    WeakRumble = controllerInput.Rumble.WeakRumble;
                    StrongRumble = controllerInput.Rumble.StrongRumble;
                }
            }
        }

        public InputConfig GetConfig()
        {
            var config = new StandardControllerInputConfig
            {
                Id = Id,
                Backend = InputBackendType.GamepadSDL2,
                PlayerIndex = PlayerIndex,
                ControllerType = ControllerType,
                LeftJoycon = new LeftJoyconCommonConfig<GamepadInputId>
                {
                    DpadUp = DpadUp,
                    DpadDown = DpadDown,
                    DpadLeft = DpadLeft,
                    DpadRight = DpadRight,
                    ButtonL = ButtonL,
                    ButtonMinus = ButtonMinus,
                    ButtonSl = LeftButtonSl,
                    ButtonSr = LeftButtonSr,
                    ButtonZl = ButtonZl,
                },
                RightJoycon = new RightJoyconCommonConfig<GamepadInputId>
                {
                    ButtonA = ButtonA,
                    ButtonB = ButtonB,
                    ButtonX = ButtonX,
                    ButtonY = ButtonY,
                    ButtonPlus = ButtonPlus,
                    ButtonSl = RightButtonSl,
                    ButtonSr = RightButtonSr,
                    ButtonR = ButtonR,
                    ButtonZr = ButtonZr,
                },
                LeftJoyconStick = new JoyconConfigControllerStick<GamepadInputId, StickInputId>
                {
                    Joystick = LeftJoystick,
                    InvertStickX = LeftInvertStickX,
                    InvertStickY = LeftInvertStickY,
                    Rotate90CW = LeftRotate90,
                    StickButton = LeftStickButton,
                },
                RightJoyconStick = new JoyconConfigControllerStick<GamepadInputId, StickInputId>
                {
                    Joystick = RightJoystick,
                    InvertStickX = RightInvertStickX,
                    InvertStickY = RightInvertStickY,
                    Rotate90CW = RightRotate90,
                    StickButton = RightStickButton,
                },
                Rumble = new RumbleConfigController
                {
                    EnableRumble = EnableRumble,
                    WeakRumble = WeakRumble,
                    StrongRumble = StrongRumble,
                },
                Version = InputConfig.CurrentVersion,
                DeadzoneLeft = DeadzoneLeft,
                DeadzoneRight = DeadzoneRight,
                RangeLeft = RangeLeft,
                RangeRight = RangeRight,
                TriggerThreshold = TriggerThreshold,
            };

            if (EnableCemuHookMotion)
            {
                config.Motion = new CemuHookMotionConfigController
                {
                    EnableMotion = EnableMotion,
                    MotionBackend = MotionInputBackendType.CemuHook,
                    GyroDeadzone = GyroDeadzone,
                    Sensitivity = Sensitivity,
                    DsuServerHost = DsuServerHost,
                    DsuServerPort = DsuServerPort,
                    Slot = Slot,
                    AltSlot = AltSlot,
                    MirrorInput = MirrorInput,
                };
            }
            else
            {
                config.Motion = new StandardMotionConfigController
                {
                    EnableMotion = EnableMotion,
                    MotionBackend = MotionInputBackendType.GamepadDriver,
                    GyroDeadzone = GyroDeadzone,
                    Sensitivity = Sensitivity,
                };
            }

            return config;
        }
    }
}
