using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using static SDL2.SDL;

namespace Ryujinx.Input.SDL2
{
    class SDL2Gamepad : IGamepad
    {
        private bool HasConfiguration => _configuration != null;

        private record struct ButtonMappingEntry(GamepadButtonInputId To, GamepadButtonInputId From);

        private StandardControllerInputConfig _configuration;

        private static readonly SDL_GameControllerButton[] _buttonsDriverMapping = new SDL_GameControllerButton[(int)GamepadButtonInputId.Count]
        {
            // Unbound, ignored.
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,

            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER,

            // NOTE: The left and right trigger are axis, we handle those differently
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,

            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_GUIDE,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MISC1,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_PADDLE1,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_PADDLE2,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_PADDLE3,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_PADDLE4,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_TOUCHPAD,

            // Virtual buttons are invalid, ignored.
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,
            SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID,
        };

        private readonly object _userMappingLock = new();

        private readonly List<ButtonMappingEntry> _buttonsUserMapping;

        private readonly StickInputId[] _stickUserMapping = new StickInputId[(int)StickInputId.Count]
        {
            StickInputId.Unbound,
            StickInputId.Left,
            StickInputId.Right,
        };

        public GamepadFeaturesFlag Features { get; }

        private IntPtr _gamepadHandle;

        private float _triggerThreshold;

        public SDL2Gamepad(IntPtr gamepadHandle, string driverId)
        {
            _gamepadHandle = gamepadHandle;
            _buttonsUserMapping = new List<ButtonMappingEntry>(20);

            Name = SDL_GameControllerName(_gamepadHandle);
            Id = driverId;
            Features = GetFeaturesFlag();
            _triggerThreshold = 0.0f;

            // Enable motion tracking
            if (Features.HasFlag(GamepadFeaturesFlag.Motion))
            {
                if (SDL_GameControllerSetSensorEnabled(_gamepadHandle, SDL_SensorType.SDL_SENSOR_ACCEL, SDL_bool.SDL_TRUE) != 0)
                {
                    Logger.Error?.Print(LogClass.Hid, $"Could not enable data reporting for SensorType {SDL_SensorType.SDL_SENSOR_ACCEL}.");
                }

                if (SDL_GameControllerSetSensorEnabled(_gamepadHandle, SDL_SensorType.SDL_SENSOR_GYRO, SDL_bool.SDL_TRUE) != 0)
                {
                    Logger.Error?.Print(LogClass.Hid, $"Could not enable data reporting for SensorType {SDL_SensorType.SDL_SENSOR_GYRO}.");
                }
            }
        }

        private GamepadFeaturesFlag GetFeaturesFlag()
        {
            GamepadFeaturesFlag result = GamepadFeaturesFlag.None;

            if (SDL_GameControllerHasSensor(_gamepadHandle, SDL_SensorType.SDL_SENSOR_ACCEL) == SDL_bool.SDL_TRUE &&
                SDL_GameControllerHasSensor(_gamepadHandle, SDL_SensorType.SDL_SENSOR_GYRO) == SDL_bool.SDL_TRUE)
            {
                result |= GamepadFeaturesFlag.Motion;
            }

            int error = SDL_GameControllerRumble(_gamepadHandle, 0, 0, 100);

            if (error == 0)
            {
                result |= GamepadFeaturesFlag.Rumble;
            }

            return result;
        }

        public string Id { get; }
        public string Name { get; }

        public bool IsConnected => SDL_GameControllerGetAttached(_gamepadHandle) == SDL_bool.SDL_TRUE;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _gamepadHandle != IntPtr.Zero)
            {
                SDL_GameControllerClose(_gamepadHandle);

                _gamepadHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            _triggerThreshold = triggerThreshold;
        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            if (Features.HasFlag(GamepadFeaturesFlag.Rumble))
            {
                ushort lowFrequencyRaw = (ushort)(lowFrequency * ushort.MaxValue);
                ushort highFrequencyRaw = (ushort)(highFrequency * ushort.MaxValue);

                if (durationMs == uint.MaxValue)
                {
                    if (SDL_GameControllerRumble(_gamepadHandle, lowFrequencyRaw, highFrequencyRaw, SDL_HAPTIC_INFINITY) != 0)
                    {
                        Logger.Error?.Print(LogClass.Hid, "Rumble is not supported on this game controller.");
                    }
                }
                else if (durationMs > SDL_HAPTIC_INFINITY)
                {
                    Logger.Error?.Print(LogClass.Hid, $"Unsupported rumble duration {durationMs}");
                }
                else
                {
                    if (SDL_GameControllerRumble(_gamepadHandle, lowFrequencyRaw, highFrequencyRaw, durationMs) != 0)
                    {
                        Logger.Error?.Print(LogClass.Hid, "Rumble is not supported on this game controller.");
                    }
                }
            }
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            SDL_SensorType sensorType = SDL_SensorType.SDL_SENSOR_INVALID;

            if (inputId == MotionInputId.Accelerometer)
            {
                sensorType = SDL_SensorType.SDL_SENSOR_ACCEL;
            }
            else if (inputId == MotionInputId.Gyroscope)
            {
                sensorType = SDL_SensorType.SDL_SENSOR_GYRO;
            }

            if (Features.HasFlag(GamepadFeaturesFlag.Motion) && sensorType != SDL_SensorType.SDL_SENSOR_INVALID)
            {
                const int ElementCount = 3;

                unsafe
                {
                    float* values = stackalloc float[ElementCount];

                    int result = SDL_GameControllerGetSensorData(_gamepadHandle, sensorType, (IntPtr)values, ElementCount);

                    if (result == 0)
                    {
                        Vector3 value = new(values[0], values[1], values[2]);

                        if (inputId == MotionInputId.Gyroscope)
                        {
                            return RadToDegree(value);
                        }

                        if (inputId == MotionInputId.Accelerometer)
                        {
                            return GsToMs2(value);
                        }

                        return value;
                    }
                }
            }

            return Vector3.Zero;
        }

        private static Vector3 RadToDegree(Vector3 rad)
        {
            return rad * (180 / MathF.PI);
        }

        private static Vector3 GsToMs2(Vector3 gs)
        {
            return gs / SDL_STANDARD_GRAVITY;
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardControllerInputConfig)configuration;

                _buttonsUserMapping.Clear();

                // First update sticks
                _stickUserMapping[(int)StickInputId.Left] = (StickInputId)_configuration.LeftJoyconStick.Joystick;
                _stickUserMapping[(int)StickInputId.Right] = (StickInputId)_configuration.RightJoyconStick.Joystick;

                // Then left joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick, (GamepadButtonInputId)_configuration.LeftJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadUp, (GamepadButtonInputId)_configuration.LeftJoycon.DpadUp));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadDown, (GamepadButtonInputId)_configuration.LeftJoycon.DpadDown));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadLeft, (GamepadButtonInputId)_configuration.LeftJoycon.DpadLeft));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadRight, (GamepadButtonInputId)_configuration.LeftJoycon.DpadRight));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Minus, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonMinus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftShoulder, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonL));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftTrigger, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonZl));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger0, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger0, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonSl));

                // Finally right joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightStick, (GamepadButtonInputId)_configuration.RightJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A, (GamepadButtonInputId)_configuration.RightJoycon.ButtonA));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B, (GamepadButtonInputId)_configuration.RightJoycon.ButtonB));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.X, (GamepadButtonInputId)_configuration.RightJoycon.ButtonX));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Y, (GamepadButtonInputId)_configuration.RightJoycon.ButtonY));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Plus, (GamepadButtonInputId)_configuration.RightJoycon.ButtonPlus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightShoulder, (GamepadButtonInputId)_configuration.RightJoycon.ButtonR));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightTrigger, (GamepadButtonInputId)_configuration.RightJoycon.ButtonZr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger1, (GamepadButtonInputId)_configuration.RightJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger1, (GamepadButtonInputId)_configuration.RightJoycon.ButtonSl));

                SetTriggerThreshold(_configuration.TriggerThreshold);
            }
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            return IGamepad.GetStateSnapshot(this);
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            GamepadStateSnapshot rawState = GetStateSnapshot();
            GamepadStateSnapshot result = default;

            lock (_userMappingLock)
            {
                if (_buttonsUserMapping.Count == 0)
                {
                    return rawState;
                }

                foreach (ButtonMappingEntry entry in _buttonsUserMapping)
                {
                    if (entry.From == GamepadButtonInputId.Unbound || entry.To == GamepadButtonInputId.Unbound)
                    {
                        continue;
                    }

                    // Do not touch state of button already pressed
                    if (!result.IsPressed(entry.To))
                    {
                        result.SetPressed(entry.To, rawState.IsPressed(entry.From));
                    }
                }

                (float leftStickX, float leftStickY) = rawState.GetStick(_stickUserMapping[(int)StickInputId.Left]);
                (float rightStickX, float rightStickY) = rawState.GetStick(_stickUserMapping[(int)StickInputId.Right]);

                result.SetStick(StickInputId.Left, leftStickX, leftStickY);
                result.SetStick(StickInputId.Right, rightStickX, rightStickY);
            }

            return result;
        }

        private static float ConvertRawStickValue(short value)
        {
            const float ConvertRate = 1.0f / (short.MaxValue + 0.5f);

            return value * ConvertRate;
        }

        private JoyconConfigControllerStick<GamepadInputId, Common.Configuration.Hid.Controller.StickInputId> GetLogicalJoyStickConfig(StickInputId inputId)
        {
            switch (inputId)
            {
                case StickInputId.Left:
                    if (_configuration.RightJoyconStick.Joystick == Common.Configuration.Hid.Controller.StickInputId.Left)
                    {
                        return _configuration.RightJoyconStick;
                    }
                    else
                    {
                        return _configuration.LeftJoyconStick;
                    }
                case StickInputId.Right:
                    if (_configuration.LeftJoyconStick.Joystick == Common.Configuration.Hid.Controller.StickInputId.Right)
                    {
                        return _configuration.LeftJoyconStick;
                    }
                    else
                    {
                        return _configuration.RightJoyconStick;
                    }
            }

            return null;
        }

        public (float, float) GetStick(StickInputId inputId)
        {
            if (inputId == StickInputId.Unbound)
            {
                return (0.0f, 0.0f);
            }

            short stickX;
            short stickY;

            if (inputId == StickInputId.Left)
            {
                stickX = SDL_GameControllerGetAxis(_gamepadHandle, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                stickY = SDL_GameControllerGetAxis(_gamepadHandle, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY);
            }
            else if (inputId == StickInputId.Right)
            {
                stickX = SDL_GameControllerGetAxis(_gamepadHandle, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
                stickY = SDL_GameControllerGetAxis(_gamepadHandle, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
            }
            else
            {
                throw new NotSupportedException($"Unsupported stick {inputId}");
            }

            float resultX = ConvertRawStickValue(stickX);
            float resultY = -ConvertRawStickValue(stickY);

            if (HasConfiguration)
            {
                var joyconStickConfig = GetLogicalJoyStickConfig(inputId);

                if (joyconStickConfig != null)
                {
                    if (joyconStickConfig.InvertStickX)
                    {
                        resultX = -resultX;
                    }

                    if (joyconStickConfig.InvertStickY)
                    {
                        resultY = -resultY;
                    }

                    if (joyconStickConfig.Rotate90CW)
                    {
                        float temp = resultX;
                        resultX = resultY;
                        resultY = -temp;
                    }
                }
            }

            return (resultX, resultY);
        }

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            if (inputId == GamepadButtonInputId.LeftTrigger)
            {
                return ConvertRawStickValue(SDL_GameControllerGetAxis(_gamepadHandle, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT)) > _triggerThreshold;
            }

            if (inputId == GamepadButtonInputId.RightTrigger)
            {
                return ConvertRawStickValue(SDL_GameControllerGetAxis(_gamepadHandle, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT)) > _triggerThreshold;
            }

            if (_buttonsDriverMapping[(int)inputId] == SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_INVALID)
            {
                return false;
            }

            return SDL_GameControllerGetButton(_gamepadHandle, _buttonsDriverMapping[(int)inputId]) == 1;
        }
    }
}
