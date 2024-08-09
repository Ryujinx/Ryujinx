using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.ViewModels.Input;
using Ryujinx.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Models.Input
{
    public class StickVisualizer : BaseModel, IDisposable
    {
        public const int DrawStickPollRate = 50; // Milliseconds per poll.
        public const int DrawStickCircumference = 5;
        public const float DrawStickScaleFactor = DrawStickCanvasCenter;
        public const int DrawStickCanvasSize = 100;
        public const int DrawStickBorderSize = DrawStickCanvasSize + 5;
        public const float DrawStickCanvasCenter = (DrawStickCanvasSize - DrawStickCircumference) / 2;
        public const float MaxVectorLength = DrawStickCanvasSize / 2;

        public CancellationTokenSource PollTokenSource;
        public CancellationToken PollToken;

        private static float _vectorLength;
        private static float _vectorMultiplier;

        private bool disposedValue;

        private DeviceType _type;
        public DeviceType Type
        {
            get => _type;
            set
            {
                _type = value;

                OnPropertyChanged();
            }
        }

        private GamepadInputConfig _gamepadConfig;
        public GamepadInputConfig GamepadConfig
        {
            get => _gamepadConfig;
            set
            {
                _gamepadConfig = value;

                OnPropertyChanged();
            }
        }

        private KeyboardInputConfig _keyboardConfig;
        public KeyboardInputConfig KeyboardConfig
        {
            get => _keyboardConfig;
            set
            {
                _keyboardConfig = value;

                OnPropertyChanged();
            }
        }

        private (float, float) _uiStickLeft;
        public (float, float) UiStickLeft
        {
            get => (_uiStickLeft.Item1 * DrawStickScaleFactor, _uiStickLeft.Item2 * DrawStickScaleFactor);
            set
            {
                _uiStickLeft = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(UiStickRightX));
                OnPropertyChanged(nameof(UiStickRightY));
                OnPropertyChanged(nameof(UiDeadzoneRight));
            }
        }

        private (float, float) _uiStickRight;
        public (float, float) UiStickRight
        {
            get => (_uiStickRight.Item1 * DrawStickScaleFactor, _uiStickRight.Item2 * DrawStickScaleFactor);
            set
            {
                _uiStickRight = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(UiStickLeftX));
                OnPropertyChanged(nameof(UiStickLeftY));
                OnPropertyChanged(nameof(UiDeadzoneLeft));
            }
        }

        public float UiStickLeftX => ClampVector(UiStickLeft).Item1;
        public float UiStickLeftY => ClampVector(UiStickLeft).Item2;
        public float UiStickRightX => ClampVector(UiStickRight).Item1;
        public float UiStickRightY => ClampVector(UiStickRight).Item2;

        public int UiStickCircumference => DrawStickCircumference;
        public int UiCanvasSize => DrawStickCanvasSize;
        public int UiStickBorderSize => DrawStickBorderSize;

        public float? UiDeadzoneLeft => _gamepadConfig?.DeadzoneLeft * DrawStickCanvasSize - DrawStickCircumference;
        public float? UiDeadzoneRight => _gamepadConfig?.DeadzoneRight * DrawStickCanvasSize - DrawStickCircumference;

        private InputViewModel Parent;

        public StickVisualizer(InputViewModel parent)
        {
            Parent = parent;

            PollTokenSource = new CancellationTokenSource();
            PollToken = PollTokenSource.Token;

            Task.Run(Initialize, PollToken);
        }

        public void UpdateConfig(object config)
        {
            if (config is ControllerInputViewModel padConfig)
            {
                GamepadConfig = padConfig.Config;
                Type = DeviceType.Controller;

                return;
            }
            else if (config is KeyboardInputViewModel keyConfig)
            {
                KeyboardConfig = keyConfig.Config;
                Type = DeviceType.Keyboard;

                return;
            }

            Type = DeviceType.None;
        }

        public async Task Initialize()
        {
            (float, float) leftBuffer;
            (float, float) rightBuffer;

            while (!PollToken.IsCancellationRequested)
            {
                leftBuffer = (0f, 0f);
                rightBuffer = (0f, 0f);

                switch (Type)
                {
                    case DeviceType.Keyboard:
                        IKeyboard keyboard = (IKeyboard)Parent.AvaloniaKeyboardDriver.GetGamepad("0");

                        if (keyboard != null)
                        {
                            KeyboardStateSnapshot snapshot = keyboard.GetKeyboardStateSnapshot();

                            if (snapshot.IsPressed((Key)KeyboardConfig.LeftStickRight))
                            {
                                leftBuffer.Item1 += 1;
                            }
                            if (snapshot.IsPressed((Key)KeyboardConfig.LeftStickLeft))
                            {
                                leftBuffer.Item1 -= 1;
                            }
                            if (snapshot.IsPressed((Key)KeyboardConfig.LeftStickUp))
                            {
                                leftBuffer.Item2 += 1;
                            }
                            if (snapshot.IsPressed((Key)KeyboardConfig.LeftStickDown))
                            {
                                leftBuffer.Item2 -= 1;
                            }

                            if (snapshot.IsPressed((Key)KeyboardConfig.RightStickRight))
                            {
                                rightBuffer.Item1 += 1;
                            }
                            if (snapshot.IsPressed((Key)KeyboardConfig.RightStickLeft))
                            {
                                rightBuffer.Item1 -= 1;
                            }
                            if (snapshot.IsPressed((Key)KeyboardConfig.RightStickUp))
                            {
                                rightBuffer.Item2 += 1;
                            }
                            if (snapshot.IsPressed((Key)KeyboardConfig.RightStickDown))
                            {
                                rightBuffer.Item2 -= 1;
                            }

                            UiStickLeft = leftBuffer;
                            UiStickRight = rightBuffer;
                        }
                        break;

                    case DeviceType.Controller:
                        IGamepad controller = Parent.SelectedGamepad;

                        if (controller != null)
                        {
                            leftBuffer = controller.GetStick((StickInputId)GamepadConfig.LeftJoystick);
                            rightBuffer = controller.GetStick((StickInputId)GamepadConfig.RightJoystick);
                        }
                        break;

                    case DeviceType.None:
                        break;
                    default:
                        throw new ArgumentException($"Unable to poll device type \"{Type}\"");
                }

                UiStickLeft = leftBuffer;
                UiStickRight = rightBuffer;

                await Task.Delay(DrawStickPollRate, PollToken);
            }

            PollTokenSource.Dispose();
        }

        public static (float, float) ClampVector((float, float) vect)
        {
            _vectorMultiplier = 1;
            _vectorLength = MathF.Sqrt((vect.Item1 * vect.Item1) + (vect.Item2 * vect.Item2));

            if (_vectorLength > MaxVectorLength)
            {
                _vectorMultiplier = MaxVectorLength / _vectorLength;
            }

            vect.Item1 = vect.Item1 * _vectorMultiplier + DrawStickCanvasCenter;
            vect.Item2 = vect.Item2 * _vectorMultiplier + DrawStickCanvasCenter;

            return vect;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PollTokenSource.Cancel();
                }

                KeyboardConfig = null;
                GamepadConfig = null;
                Parent = null;

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
