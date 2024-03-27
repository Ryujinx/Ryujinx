using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;

namespace Ryujinx.Ava.UI.Models.Input
{
    public class KeyboardInputConfig : BaseModel
    {
        public string Id { get; set; }
        public ControllerType ControllerType { get; set; }
        public PlayerIndex PlayerIndex { get; set; }

        private Key _leftStickUp;
        public Key LeftStickUp
        {
            get => _leftStickUp;
            set
            {
                _leftStickUp = value;
                OnPropertyChanged();
            }
        }

        private Key _leftStickDown;
        public Key LeftStickDown
        {
            get => _leftStickDown;
            set
            {
                _leftStickDown = value;
                OnPropertyChanged();
            }
        }

        private Key _leftStickLeft;
        public Key LeftStickLeft
        {
            get => _leftStickLeft;
            set
            {
                _leftStickLeft = value;
                OnPropertyChanged();
            }
        }

        private Key _leftStickRight;
        public Key LeftStickRight
        {
            get => _leftStickRight;
            set
            {
                _leftStickRight = value;
                OnPropertyChanged();
            }
        }

        private Key _leftStickButton;
        public Key LeftStickButton
        {
            get => _leftStickButton;
            set
            {
                _leftStickButton = value;
                OnPropertyChanged();
            }
        }

        private Key _rightStickUp;
        public Key RightStickUp
        {
            get => _rightStickUp;
            set
            {
                _rightStickUp = value;
                OnPropertyChanged();
            }
        }

        private Key _rightStickDown;
        public Key RightStickDown
        {
            get => _rightStickDown;
            set
            {
                _rightStickDown = value;
                OnPropertyChanged();
            }
        }

        private Key _rightStickLeft;
        public Key RightStickLeft
        {
            get => _rightStickLeft;
            set
            {
                _rightStickLeft = value;
                OnPropertyChanged();
            }
        }

        private Key _rightStickRight;
        public Key RightStickRight
        {
            get => _rightStickRight;
            set
            {
                _rightStickRight = value;
                OnPropertyChanged();
            }
        }

        private Key _rightStickButton;
        public Key RightStickButton
        {
            get => _rightStickButton;
            set
            {
                _rightStickButton = value;
                OnPropertyChanged();
            }
        }

        private Key _dpadUp;
        public Key DpadUp
        {
            get => _dpadUp;
            set
            {
                _dpadUp = value;
                OnPropertyChanged();
            }
        }

        private Key _dpadDown;
        public Key DpadDown
        {
            get => _dpadDown;
            set
            {
                _dpadDown = value;
                OnPropertyChanged();
            }
        }

        private Key _dpadLeft;
        public Key DpadLeft
        {
            get => _dpadLeft;
            set
            {
                _dpadLeft = value;
                OnPropertyChanged();
            }
        }

        private Key _dpadRight;
        public Key DpadRight
        {
            get => _dpadRight;
            set
            {
                _dpadRight = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonL;
        public Key ButtonL
        {
            get => _buttonL;
            set
            {
                _buttonL = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonMinus;
        public Key ButtonMinus
        {
            get => _buttonMinus;
            set
            {
                _buttonMinus = value;
                OnPropertyChanged();
            }
        }

        private Key _leftButtonSl;
        public Key LeftButtonSl
        {
            get => _leftButtonSl;
            set
            {
                _leftButtonSl = value;
                OnPropertyChanged();
            }
        }

        private Key _leftButtonSr;
        public Key LeftButtonSr
        {
            get => _leftButtonSr;
            set
            {
                _leftButtonSr = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonZl;
        public Key ButtonZl
        {
            get => _buttonZl;
            set
            {
                _buttonZl = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonA;
        public Key ButtonA
        {
            get => _buttonA;
            set
            {
                _buttonA = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonB;
        public Key ButtonB
        {
            get => _buttonB;
            set
            {
                _buttonB = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonX;
        public Key ButtonX
        {
            get => _buttonX;
            set
            {
                _buttonX = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonY;
        public Key ButtonY
        {
            get => _buttonY;
            set
            {
                _buttonY = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonR;
        public Key ButtonR
        {
            get => _buttonR;
            set
            {
                _buttonR = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonPlus;
        public Key ButtonPlus
        {
            get => _buttonPlus;
            set
            {
                _buttonPlus = value;
                OnPropertyChanged();
            }
        }

        private Key _rightButtonSl;
        public Key RightButtonSl
        {
            get => _rightButtonSl;
            set
            {
                _rightButtonSl = value;
                OnPropertyChanged();
            }
        }

        private Key _rightButtonSr;
        public Key RightButtonSr
        {
            get => _rightButtonSr;
            set
            {
                _rightButtonSr = value;
                OnPropertyChanged();
            }
        }

        private Key _buttonZr;
        public Key ButtonZr
        {
            get => _buttonZr;
            set
            {
                _buttonZr = value;
                OnPropertyChanged();
            }
        }

        public KeyboardInputConfig(InputConfig config)
        {
            if (config != null)
            {
                Id = config.Id;
                ControllerType = config.ControllerType;
                PlayerIndex = config.PlayerIndex;

                if (config is not StandardKeyboardInputConfig keyboardConfig)
                {
                    return;
                }

                LeftStickUp = keyboardConfig.LeftJoyconStick.StickUp;
                LeftStickDown = keyboardConfig.LeftJoyconStick.StickDown;
                LeftStickLeft = keyboardConfig.LeftJoyconStick.StickLeft;
                LeftStickRight = keyboardConfig.LeftJoyconStick.StickRight;
                LeftStickButton = keyboardConfig.LeftJoyconStick.StickButton;

                RightStickUp = keyboardConfig.RightJoyconStick.StickUp;
                RightStickDown = keyboardConfig.RightJoyconStick.StickDown;
                RightStickLeft = keyboardConfig.RightJoyconStick.StickLeft;
                RightStickRight = keyboardConfig.RightJoyconStick.StickRight;
                RightStickButton = keyboardConfig.RightJoyconStick.StickButton;

                DpadUp = keyboardConfig.LeftJoycon.DpadUp;
                DpadDown = keyboardConfig.LeftJoycon.DpadDown;
                DpadLeft = keyboardConfig.LeftJoycon.DpadLeft;
                DpadRight = keyboardConfig.LeftJoycon.DpadRight;
                ButtonL = keyboardConfig.LeftJoycon.ButtonL;
                ButtonMinus = keyboardConfig.LeftJoycon.ButtonMinus;
                LeftButtonSl = keyboardConfig.LeftJoycon.ButtonSl;
                LeftButtonSr = keyboardConfig.LeftJoycon.ButtonSr;
                ButtonZl = keyboardConfig.LeftJoycon.ButtonZl;

                ButtonA = keyboardConfig.RightJoycon.ButtonA;
                ButtonB = keyboardConfig.RightJoycon.ButtonB;
                ButtonX = keyboardConfig.RightJoycon.ButtonX;
                ButtonY = keyboardConfig.RightJoycon.ButtonY;
                ButtonR = keyboardConfig.RightJoycon.ButtonR;
                ButtonPlus = keyboardConfig.RightJoycon.ButtonPlus;
                RightButtonSl = keyboardConfig.RightJoycon.ButtonSl;
                RightButtonSr = keyboardConfig.RightJoycon.ButtonSr;
                ButtonZr = keyboardConfig.RightJoycon.ButtonZr;
            }
        }

        public InputConfig GetConfig()
        {
            var config = new StandardKeyboardInputConfig
            {
                Id = Id,
                Backend = InputBackendType.WindowKeyboard,
                PlayerIndex = PlayerIndex,
                ControllerType = ControllerType,
                LeftJoycon = new LeftJoyconCommonConfig<Key>
                {
                    DpadUp = DpadUp,
                    DpadDown = DpadDown,
                    DpadLeft = DpadLeft,
                    DpadRight = DpadRight,
                    ButtonL = ButtonL,
                    ButtonMinus = ButtonMinus,
                    ButtonZl = ButtonZl,
                    ButtonSl = LeftButtonSl,
                    ButtonSr = LeftButtonSr,
                },
                RightJoycon = new RightJoyconCommonConfig<Key>
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
                LeftJoyconStick = new JoyconConfigKeyboardStick<Key>
                {
                    StickUp = LeftStickUp,
                    StickDown = LeftStickDown,
                    StickRight = LeftStickRight,
                    StickLeft = LeftStickLeft,
                    StickButton = LeftStickButton,
                },
                RightJoyconStick = new JoyconConfigKeyboardStick<Key>
                {
                    StickUp = RightStickUp,
                    StickDown = RightStickDown,
                    StickLeft = RightStickLeft,
                    StickRight = RightStickRight,
                    StickButton = RightStickButton,
                },
                Version = InputConfig.CurrentVersion,
            };

            return config;
        }
    }
}
