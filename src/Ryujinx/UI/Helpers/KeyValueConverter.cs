using Avalonia.Data.Converters;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class KeyValueConverter : IValueConverter
    {
        public static KeyValueConverter Instance = new();

        private static readonly Dictionary<Key, LocaleKeys> _keysMap = new()
        {
            { Key.Unknown, LocaleKeys.KeyUnknown },
            { Key.ShiftLeft, LocaleKeys.KeyShiftLeft },
            { Key.ShiftRight, LocaleKeys.KeyShiftRight },
            { Key.ControlLeft, LocaleKeys.KeySideLeft },
            { Key.ControlRight, LocaleKeys.KeySideRight },
            { Key.AltLeft, LocaleKeys.KeySideLeft },
            { Key.AltRight, LocaleKeys.KeySideRight },
            { Key.WinLeft, LocaleKeys.KeySideLeft },
            { Key.WinRight, LocaleKeys.KeySideRight },
            { Key.Up, LocaleKeys.KeyUp },
            { Key.Down, LocaleKeys.KeyDown },
            { Key.Left, LocaleKeys.KeyLeft },
            { Key.Right, LocaleKeys.KeyRight },
            { Key.Enter, LocaleKeys.KeyEnter },
            { Key.Escape, LocaleKeys.KeyEscape },
            { Key.Space, LocaleKeys.KeySpace },
            { Key.Tab, LocaleKeys.KeyTab },
            { Key.BackSpace, LocaleKeys.KeyBackSpace },
            { Key.Insert, LocaleKeys.KeyInsert },
            { Key.Delete, LocaleKeys.KeyDelete },
            { Key.PageUp, LocaleKeys.KeyPageUp },
            { Key.PageDown, LocaleKeys.KeyPageDown },
            { Key.Home, LocaleKeys.KeyHome },
            { Key.End, LocaleKeys.KeyEnd },
            { Key.CapsLock, LocaleKeys.KeyCapsLock },
            { Key.ScrollLock, LocaleKeys.KeyScrollLock },
            { Key.PrintScreen, LocaleKeys.KeyPrintScreen },
            { Key.Pause, LocaleKeys.KeyPause },
            { Key.NumLock, LocaleKeys.KeyNumLock },
            { Key.Clear, LocaleKeys.KeyClear },
            { Key.Keypad0, LocaleKeys.KeyKeypad0 },
            { Key.Keypad1, LocaleKeys.KeyKeypad1 },
            { Key.Keypad2, LocaleKeys.KeyKeypad2 },
            { Key.Keypad3, LocaleKeys.KeyKeypad3 },
            { Key.Keypad4, LocaleKeys.KeyKeypad4 },
            { Key.Keypad5, LocaleKeys.KeyKeypad5 },
            { Key.Keypad6, LocaleKeys.KeyKeypad6 },
            { Key.Keypad7, LocaleKeys.KeyKeypad7 },
            { Key.Keypad8, LocaleKeys.KeyKeypad8 },
            { Key.Keypad9, LocaleKeys.KeyKeypad9 },
            { Key.KeypadDivide, LocaleKeys.KeyKeypadDivide },
            { Key.KeypadMultiply, LocaleKeys.KeyKeypadMultiply },
            { Key.KeypadSubtract, LocaleKeys.KeyKeypadSubtract },
            { Key.KeypadAdd, LocaleKeys.KeyKeypadAdd },
            { Key.KeypadDecimal, LocaleKeys.KeyKeypadDecimal },
            { Key.KeypadEnter, LocaleKeys.KeyKeypadEnter },
            { Key.Number0, LocaleKeys.KeyNumber0 },
            { Key.Number1, LocaleKeys.KeyNumber1 },
            { Key.Number2, LocaleKeys.KeyNumber2 },
            { Key.Number3, LocaleKeys.KeyNumber3 },
            { Key.Number4, LocaleKeys.KeyNumber4 },
            { Key.Number5, LocaleKeys.KeyNumber5 },
            { Key.Number6, LocaleKeys.KeyNumber6 },
            { Key.Number7, LocaleKeys.KeyNumber7 },
            { Key.Number8, LocaleKeys.KeyNumber8 },
            { Key.Number9, LocaleKeys.KeyNumber9 },
            { Key.Tilde, LocaleKeys.KeyTilde },
            { Key.Grave, LocaleKeys.KeyGrave },
            { Key.Minus, LocaleKeys.KeyMinus },
            { Key.Plus, LocaleKeys.KeyPlus },
            { Key.BracketLeft, LocaleKeys.KeyBracketLeft },
            { Key.BracketRight, LocaleKeys.KeyBracketRight },
            { Key.Semicolon, LocaleKeys.KeySemicolon },
            { Key.Quote, LocaleKeys.KeyQuote },
            { Key.Comma, LocaleKeys.KeyComma },
            { Key.Period, LocaleKeys.KeyPeriod },
            { Key.Slash, LocaleKeys.KeySlash },
            { Key.BackSlash, LocaleKeys.KeyBackSlash },
            { Key.Unbound, LocaleKeys.KeyUnbound },
        };

        private static readonly Dictionary<GamepadInputId, LocaleKeys> _gamepadInputIdMap = new()
        {
            { GamepadInputId.LeftStick, LocaleKeys.GamepadLeftStick },
            { GamepadInputId.RightStick, LocaleKeys.GamepadRightStick },
            { GamepadInputId.LeftShoulder, LocaleKeys.GamepadLeftShoulder },
            { GamepadInputId.RightShoulder, LocaleKeys.GamepadRightShoulder },
            { GamepadInputId.LeftTrigger, LocaleKeys.GamepadLeftTrigger },
            { GamepadInputId.RightTrigger, LocaleKeys.GamepadRightTrigger },
            { GamepadInputId.DpadUp, LocaleKeys.GamepadDpadUp},
            { GamepadInputId.DpadDown, LocaleKeys.GamepadDpadDown},
            { GamepadInputId.DpadLeft, LocaleKeys.GamepadDpadLeft},
            { GamepadInputId.DpadRight, LocaleKeys.GamepadDpadRight},
            { GamepadInputId.Minus, LocaleKeys.GamepadMinus},
            { GamepadInputId.Plus, LocaleKeys.GamepadPlus},
            { GamepadInputId.Guide, LocaleKeys.GamepadGuide},
            { GamepadInputId.Misc1, LocaleKeys.GamepadMisc1},
            { GamepadInputId.Paddle1, LocaleKeys.GamepadPaddle1},
            { GamepadInputId.Paddle2, LocaleKeys.GamepadPaddle2},
            { GamepadInputId.Paddle3, LocaleKeys.GamepadPaddle3},
            { GamepadInputId.Paddle4, LocaleKeys.GamepadPaddle4},
            { GamepadInputId.Touchpad, LocaleKeys.GamepadTouchpad},
            { GamepadInputId.SingleLeftTrigger0, LocaleKeys.GamepadSingleLeftTrigger0},
            { GamepadInputId.SingleRightTrigger0, LocaleKeys.GamepadSingleRightTrigger0},
            { GamepadInputId.SingleLeftTrigger1, LocaleKeys.GamepadSingleLeftTrigger1},
            { GamepadInputId.SingleRightTrigger1, LocaleKeys.GamepadSingleRightTrigger1},
            { GamepadInputId.Unbound, LocaleKeys.KeyUnbound},
        };

        private static readonly Dictionary<StickInputId, LocaleKeys> _stickInputIdMap = new()
        {
            { StickInputId.Left, LocaleKeys.StickLeft},
            { StickInputId.Right, LocaleKeys.StickRight},
            { StickInputId.Unbound, LocaleKeys.KeyUnbound},
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string keyString = "";

            if (value is Key key)
            {
                if (_keysMap.TryGetValue(key, out LocaleKeys localeKey))
                {
                    var symbol = "";

                    switch (key)
                    {
                        case Key.ControlLeft:
                        case Key.ControlRight:
                            symbol = OperatingSystem.IsMacOS() ? "⌃ " : "Ctrl ";
                            break;
                        case Key.WinLeft:
                        case Key.WinRight:
                            symbol = OperatingSystem.IsMacOS() ? "⌘ " : "⊞ ";
                            break;
                        case Key.AltLeft:
                        case Key.AltRight:
                            symbol = OperatingSystem.IsMacOS() ? "⌥ " : "Alt ";
                            break;
                    }

                    keyString = symbol + LocaleManager.Instance[localeKey];
                }
                else
                {
                    keyString = key.ToString();
                }
            }
            else if (value is GamepadInputId gamepadInputId)
            {
                if (_gamepadInputIdMap.TryGetValue(gamepadInputId, out LocaleKeys localeKey))
                {
                    keyString = LocaleManager.Instance[localeKey];
                }
                else
                {
                    keyString = gamepadInputId.ToString();
                }
            }
            else if (value is StickInputId stickInputId)
            {
                if (_stickInputIdMap.TryGetValue(stickInputId, out LocaleKeys localeKey))
                {
                    keyString = LocaleManager.Instance[localeKey];
                }
                else
                {
                    keyString = stickInputId.ToString();
                }
            }

            return keyString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
