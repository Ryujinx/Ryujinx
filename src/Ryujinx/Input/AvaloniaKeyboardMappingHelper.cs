using Ryujinx.Input;
using System;
using System.Collections.Generic;
using AvaKey = Avalonia.Input.Key;

namespace Ryujinx.Ava.Input
{
    internal static class AvaloniaKeyboardMappingHelper
    {
        private static readonly AvaKey[] _keyMapping = {
            // NOTE: Invalid
            AvaKey.None,

            AvaKey.LeftShift,
            AvaKey.RightShift,
            AvaKey.LeftCtrl,
            AvaKey.RightCtrl,
            AvaKey.LeftAlt,
            AvaKey.RightAlt,
            AvaKey.LWin,
            AvaKey.RWin,
            AvaKey.Apps,
            AvaKey.F1,
            AvaKey.F2,
            AvaKey.F3,
            AvaKey.F4,
            AvaKey.F5,
            AvaKey.F6,
            AvaKey.F7,
            AvaKey.F8,
            AvaKey.F9,
            AvaKey.F10,
            AvaKey.F11,
            AvaKey.F12,
            AvaKey.F13,
            AvaKey.F14,
            AvaKey.F15,
            AvaKey.F16,
            AvaKey.F17,
            AvaKey.F18,
            AvaKey.F19,
            AvaKey.F20,
            AvaKey.F21,
            AvaKey.F22,
            AvaKey.F23,
            AvaKey.F24,

            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,
            AvaKey.None,

            AvaKey.Up,
            AvaKey.Down,
            AvaKey.Left,
            AvaKey.Right,
            AvaKey.Return,
            AvaKey.Escape,
            AvaKey.Space,
            AvaKey.Tab,
            AvaKey.Back,
            AvaKey.Insert,
            AvaKey.Delete,
            AvaKey.PageUp,
            AvaKey.PageDown,
            AvaKey.Home,
            AvaKey.End,
            AvaKey.CapsLock,
            AvaKey.Scroll,
            AvaKey.Print,
            AvaKey.Pause,
            AvaKey.NumLock,
            AvaKey.Clear,
            AvaKey.NumPad0,
            AvaKey.NumPad1,
            AvaKey.NumPad2,
            AvaKey.NumPad3,
            AvaKey.NumPad4,
            AvaKey.NumPad5,
            AvaKey.NumPad6,
            AvaKey.NumPad7,
            AvaKey.NumPad8,
            AvaKey.NumPad9,
            AvaKey.Divide,
            AvaKey.Multiply,
            AvaKey.Subtract,
            AvaKey.Add,
            AvaKey.Decimal,
            AvaKey.Enter,
            AvaKey.A,
            AvaKey.B,
            AvaKey.C,
            AvaKey.D,
            AvaKey.E,
            AvaKey.F,
            AvaKey.G,
            AvaKey.H,
            AvaKey.I,
            AvaKey.J,
            AvaKey.K,
            AvaKey.L,
            AvaKey.M,
            AvaKey.N,
            AvaKey.O,
            AvaKey.P,
            AvaKey.Q,
            AvaKey.R,
            AvaKey.S,
            AvaKey.T,
            AvaKey.U,
            AvaKey.V,
            AvaKey.W,
            AvaKey.X,
            AvaKey.Y,
            AvaKey.Z,
            AvaKey.D0,
            AvaKey.D1,
            AvaKey.D2,
            AvaKey.D3,
            AvaKey.D4,
            AvaKey.D5,
            AvaKey.D6,
            AvaKey.D7,
            AvaKey.D8,
            AvaKey.D9,
            AvaKey.OemTilde,
            AvaKey.OemTilde,AvaKey.OemMinus,
            AvaKey.OemPlus,
            AvaKey.OemOpenBrackets,
            AvaKey.OemCloseBrackets,
            AvaKey.OemSemicolon,
            AvaKey.OemQuotes,
            AvaKey.OemComma,
            AvaKey.OemPeriod,
            AvaKey.OemQuestion,
            AvaKey.OemBackslash,

            // NOTE: invalid
            AvaKey.None,
        };

        private static readonly Dictionary<AvaKey, Key> _avaKeyMapping;

        static AvaloniaKeyboardMappingHelper()
        {
            var inputKeys = Enum.GetValues<Key>();

            // NOTE: Avalonia.Input.Key is not contiguous and quite large, so use a dictionary instead of an array.
            _avaKeyMapping = new Dictionary<AvaKey, Key>();

            foreach (var key in inputKeys)
            {
                if (TryGetAvaKey(key, out var index))
                {
                    _avaKeyMapping[index] = key;
                }
            }
        }

        public static bool TryGetAvaKey(Key key, out AvaKey avaKey)
        {
            avaKey = AvaKey.None;

            bool keyExist = (int)key < _keyMapping.Length;
            if (keyExist)
            {
                avaKey = _keyMapping[(int)key];
            }

            return keyExist;
        }

        public static Key ToInputKey(AvaKey key)
        {
            return _avaKeyMapping.GetValueOrDefault(key, Key.Unknown);
        }
    }
}
