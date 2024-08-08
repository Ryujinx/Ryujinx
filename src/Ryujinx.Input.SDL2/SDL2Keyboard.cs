using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Keyboard;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using static SDL2.SDL;

using ConfigKey = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.Input.SDL2
{
    class SDL2Keyboard : IKeyboard
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

        private readonly Lock _userMappingLock = new();

#pragma warning disable IDE0052 // Remove unread private member
        private readonly SDL2KeyboardDriver _driver;
#pragma warning restore IDE0052
        private StandardKeyboardInputConfig _configuration;
        private readonly List<ButtonMappingEntry> _buttonsUserMapping;

        private static readonly SDL_Keycode[] _keysDriverMapping = new SDL_Keycode[(int)Key.Count]
        {
            // INVALID
            SDL_Keycode.SDLK_0,
            // Presented as modifiers, so invalid here.
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,

            SDL_Keycode.SDLK_F1,
            SDL_Keycode.SDLK_F2,
            SDL_Keycode.SDLK_F3,
            SDL_Keycode.SDLK_F4,
            SDL_Keycode.SDLK_F5,
            SDL_Keycode.SDLK_F6,
            SDL_Keycode.SDLK_F7,
            SDL_Keycode.SDLK_F8,
            SDL_Keycode.SDLK_F9,
            SDL_Keycode.SDLK_F10,
            SDL_Keycode.SDLK_F11,
            SDL_Keycode.SDLK_F12,
            SDL_Keycode.SDLK_F13,
            SDL_Keycode.SDLK_F14,
            SDL_Keycode.SDLK_F15,
            SDL_Keycode.SDLK_F16,
            SDL_Keycode.SDLK_F17,
            SDL_Keycode.SDLK_F18,
            SDL_Keycode.SDLK_F19,
            SDL_Keycode.SDLK_F20,
            SDL_Keycode.SDLK_F21,
            SDL_Keycode.SDLK_F22,
            SDL_Keycode.SDLK_F23,
            SDL_Keycode.SDLK_F24,

            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_0,

            SDL_Keycode.SDLK_UP,
            SDL_Keycode.SDLK_DOWN,
            SDL_Keycode.SDLK_LEFT,
            SDL_Keycode.SDLK_RIGHT,
            SDL_Keycode.SDLK_RETURN,
            SDL_Keycode.SDLK_ESCAPE,
            SDL_Keycode.SDLK_SPACE,
            SDL_Keycode.SDLK_TAB,
            SDL_Keycode.SDLK_BACKSPACE,
            SDL_Keycode.SDLK_INSERT,
            SDL_Keycode.SDLK_DELETE,
            SDL_Keycode.SDLK_PAGEUP,
            SDL_Keycode.SDLK_PAGEDOWN,
            SDL_Keycode.SDLK_HOME,
            SDL_Keycode.SDLK_END,
            SDL_Keycode.SDLK_CAPSLOCK,
            SDL_Keycode.SDLK_SCROLLLOCK,
            SDL_Keycode.SDLK_PRINTSCREEN,
            SDL_Keycode.SDLK_PAUSE,
            SDL_Keycode.SDLK_NUMLOCKCLEAR,
            SDL_Keycode.SDLK_CLEAR,
            SDL_Keycode.SDLK_KP_0,
            SDL_Keycode.SDLK_KP_1,
            SDL_Keycode.SDLK_KP_2,
            SDL_Keycode.SDLK_KP_3,
            SDL_Keycode.SDLK_KP_4,
            SDL_Keycode.SDLK_KP_5,
            SDL_Keycode.SDLK_KP_6,
            SDL_Keycode.SDLK_KP_7,
            SDL_Keycode.SDLK_KP_8,
            SDL_Keycode.SDLK_KP_9,
            SDL_Keycode.SDLK_KP_DIVIDE,
            SDL_Keycode.SDLK_KP_MULTIPLY,
            SDL_Keycode.SDLK_KP_MINUS,
            SDL_Keycode.SDLK_KP_PLUS,
            SDL_Keycode.SDLK_KP_DECIMAL,
            SDL_Keycode.SDLK_KP_ENTER,
            SDL_Keycode.SDLK_a,
            SDL_Keycode.SDLK_b,
            SDL_Keycode.SDLK_c,
            SDL_Keycode.SDLK_d,
            SDL_Keycode.SDLK_e,
            SDL_Keycode.SDLK_f,
            SDL_Keycode.SDLK_g,
            SDL_Keycode.SDLK_h,
            SDL_Keycode.SDLK_i,
            SDL_Keycode.SDLK_j,
            SDL_Keycode.SDLK_k,
            SDL_Keycode.SDLK_l,
            SDL_Keycode.SDLK_m,
            SDL_Keycode.SDLK_n,
            SDL_Keycode.SDLK_o,
            SDL_Keycode.SDLK_p,
            SDL_Keycode.SDLK_q,
            SDL_Keycode.SDLK_r,
            SDL_Keycode.SDLK_s,
            SDL_Keycode.SDLK_t,
            SDL_Keycode.SDLK_u,
            SDL_Keycode.SDLK_v,
            SDL_Keycode.SDLK_w,
            SDL_Keycode.SDLK_x,
            SDL_Keycode.SDLK_y,
            SDL_Keycode.SDLK_z,
            SDL_Keycode.SDLK_0,
            SDL_Keycode.SDLK_1,
            SDL_Keycode.SDLK_2,
            SDL_Keycode.SDLK_3,
            SDL_Keycode.SDLK_4,
            SDL_Keycode.SDLK_5,
            SDL_Keycode.SDLK_6,
            SDL_Keycode.SDLK_7,
            SDL_Keycode.SDLK_8,
            SDL_Keycode.SDLK_9,
            SDL_Keycode.SDLK_BACKQUOTE,
            SDL_Keycode.SDLK_BACKQUOTE,
            SDL_Keycode.SDLK_MINUS,
            SDL_Keycode.SDLK_PLUS,
            SDL_Keycode.SDLK_LEFTBRACKET,
            SDL_Keycode.SDLK_RIGHTBRACKET,
            SDL_Keycode.SDLK_SEMICOLON,
            SDL_Keycode.SDLK_QUOTE,
            SDL_Keycode.SDLK_COMMA,
            SDL_Keycode.SDLK_PERIOD,
            SDL_Keycode.SDLK_SLASH,
            SDL_Keycode.SDLK_BACKSLASH,

            // Invalids
            SDL_Keycode.SDLK_0,
        };

        public SDL2Keyboard(SDL2KeyboardDriver driver, string id, string name)
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ToSDL2Scancode(Key key)
        {
            if (key >= Key.Unknown && key <= Key.Menu)
            {
                return -1;
            }

            return (int)SDL_GetScancodeFromKey(_keysDriverMapping[(int)key]);
        }

        private static SDL_Keymod GetKeyboardModifierMask(Key key)
        {
            return key switch
            {
                Key.ShiftLeft => SDL_Keymod.KMOD_LSHIFT,
                Key.ShiftRight => SDL_Keymod.KMOD_RSHIFT,
                Key.ControlLeft => SDL_Keymod.KMOD_LCTRL,
                Key.ControlRight => SDL_Keymod.KMOD_RCTRL,
                Key.AltLeft => SDL_Keymod.KMOD_LALT,
                Key.AltRight => SDL_Keymod.KMOD_RALT,
                Key.WinLeft => SDL_Keymod.KMOD_LGUI,
                Key.WinRight => SDL_Keymod.KMOD_RGUI,
                // NOTE: Menu key isn't supported by SDL2.
                _ => SDL_Keymod.KMOD_NONE,
            };
        }

        public KeyboardStateSnapshot GetKeyboardStateSnapshot()
        {
            ReadOnlySpan<byte> rawKeyboardState;
            SDL_Keymod rawKeyboardModifierState = SDL_GetModState();

            unsafe
            {
                IntPtr statePtr = SDL_GetKeyboardState(out int numKeys);

                rawKeyboardState = new ReadOnlySpan<byte>((byte*)statePtr, numKeys);
            }

            bool[] keysState = new bool[(int)Key.Count];

            for (Key key = 0; key < Key.Count; key++)
            {
                int index = ToSDL2Scancode(key);
                if (index == -1)
                {
                    SDL_Keymod modifierMask = GetKeyboardModifierMask(key);

                    if (modifierMask == SDL_Keymod.KMOD_NONE)
                    {
                        continue;
                    }

                    keysState[(int)key] = (rawKeyboardModifierState & modifierMask) == modifierMask;
                }
                else
                {
                    keysState[(int)key] = rawKeyboardState[index] == 1;
                }
            }

            return new KeyboardStateSnapshot(keysState);
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

            Vector2 stick = Vector2.Normalize(new Vector2(stickX, stickY));

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
            // We only implement GetKeyboardStateSnapshot.
            throw new NotSupportedException();
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardKeyboardInputConfig)configuration;

                // First clear the buttons mapping
                _buttonsUserMapping.Clear();

                // Then configure left joycon
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

                // Finally configure right joycon
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
