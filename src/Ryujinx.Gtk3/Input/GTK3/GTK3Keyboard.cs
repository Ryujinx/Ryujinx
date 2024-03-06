using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using System;
using System.Collections.Generic;
using System.Numerics;
using ConfigKey = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.Input.GTK3
{
    public class GTK3Keyboard : IKeyboard
    {
        private class ButtonMappingEntry
        {
            public readonly GamepadButtonInputId To;
            public readonly Key From;

            public ButtonMappingEntry(GamepadButtonInputId to, Key from)
            {
                To = to;
                From = from;
            }
        }

        private readonly object _userMappingLock = new();

        private readonly GTK3KeyboardDriver _driver;
        private StandardKeyboardInputConfig _configuration;
        private readonly List<ButtonMappingEntry> _buttonsUserMapping;

        public GTK3Keyboard(GTK3KeyboardDriver driver, string id, string name)
        {
            _driver = driver;
            Id = id;
            Name = name;
            _buttonsUserMapping = new List<ButtonMappingEntry>();
        }

        private bool HasConfiguration => _configuration != null;

        public string Id { get; }

        public string Name { get; }

        public bool IsConnected => true;

        public GamepadFeaturesFlag Features => GamepadFeaturesFlag.None;

        public void Dispose()
        {
            // No operations
            GC.SuppressFinalize(this);
        }

        public KeyboardStateSnapshot GetKeyboardStateSnapshot()
        {
            return IKeyboard.GetStateSnapshot(this);
        }

        private static float ConvertRawStickValue(short value)
        {
            const float ConvertRate = 1.0f / (short.MaxValue + 0.5f);

            return value * ConvertRate;
        }

        private static (short, short) GetStickValues(ref KeyboardStateSnapshot snapshot, JoyconConfigKeyboardStick<ConfigKey> stickConfig)
        {
            short stickX = 0;
            short stickY = 0;

            if (snapshot.IsPressed((Key)stickConfig.StickUp))
            {
                stickY += 1;
            }

            if (snapshot.IsPressed((Key)stickConfig.StickDown))
            {
                stickY -= 1;
            }

            if (snapshot.IsPressed((Key)stickConfig.StickRight))
            {
                stickX += 1;
            }

            if (snapshot.IsPressed((Key)stickConfig.StickLeft))
            {
                stickX -= 1;
            }

            OpenTK.Mathematics.Vector2 stick = new(stickX, stickY);

            stick.NormalizeFast();

            return ((short)(stick.X * short.MaxValue), (short)(stick.Y * short.MaxValue));
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            KeyboardStateSnapshot rawState = GetKeyboardStateSnapshot();
            GamepadStateSnapshot result = default;

            lock (_userMappingLock)
            {
                if (!HasConfiguration)
                {
                    return result;
                }

                foreach (ButtonMappingEntry entry in _buttonsUserMapping)
                {
                    if (entry.From == Key.Unknown || entry.From == Key.Unbound || entry.To == GamepadButtonInputId.Unbound)
                    {
                        continue;
                    }

                    // Do not touch state of button already pressed
                    if (!result.IsPressed(entry.To))
                    {
                        result.SetPressed(entry.To, rawState.IsPressed(entry.From));
                    }
                }

                (short leftStickX, short leftStickY) = GetStickValues(ref rawState, _configuration.LeftJoyconStick);
                (short rightStickX, short rightStickY) = GetStickValues(ref rawState, _configuration.RightJoyconStick);

                result.SetStick(StickInputId.Left, ConvertRawStickValue(leftStickX), ConvertRawStickValue(leftStickY));
                result.SetStick(StickInputId.Right, ConvertRawStickValue(rightStickX), ConvertRawStickValue(rightStickY));
            }

            return result;
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            throw new NotSupportedException();
        }

        public (float, float) GetStick(StickInputId inputId)
        {
            throw new NotSupportedException();
        }

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            throw new NotSupportedException();
        }

        public bool IsPressed(Key key)
        {
            return _driver.IsPressed(key);
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardKeyboardInputConfig)configuration;

                _buttonsUserMapping.Clear();

                // Then left joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick, (Key)_configuration.LeftJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadUp, (Key)_configuration.LeftJoycon.DpadUp));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadDown, (Key)_configuration.LeftJoycon.DpadDown));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadLeft, (Key)_configuration.LeftJoycon.DpadLeft));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadRight, (Key)_configuration.LeftJoycon.DpadRight));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Minus, (Key)_configuration.LeftJoycon.ButtonMinus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftShoulder, (Key)_configuration.LeftJoycon.ButtonL));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftTrigger, (Key)_configuration.LeftJoycon.ButtonZl));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger0, (Key)_configuration.LeftJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger0, (Key)_configuration.LeftJoycon.ButtonSl));

                // Finally right joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightStick, (Key)_configuration.RightJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A, (Key)_configuration.RightJoycon.ButtonA));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B, (Key)_configuration.RightJoycon.ButtonB));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.X, (Key)_configuration.RightJoycon.ButtonX));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Y, (Key)_configuration.RightJoycon.ButtonY));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Plus, (Key)_configuration.RightJoycon.ButtonPlus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightShoulder, (Key)_configuration.RightJoycon.ButtonR));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightTrigger, (Key)_configuration.RightJoycon.ButtonZr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger1, (Key)_configuration.RightJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger1, (Key)_configuration.RightJoycon.ButtonSl));
            }
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            // No operations
        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            // No operations
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            // No operations

            return Vector3.Zero;
        }
    }
}
