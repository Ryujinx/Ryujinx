using Ryujinx.Common.Configuration.Hid.Controller;
using Ryujinx.Input;
using System;
using System.Collections.Generic;
using Key = Ryujinx.Common.Configuration.Hid.Key;
using StickInputId = Ryujinx.Common.Configuration.Hid.Controller.StickInputId;

namespace Ryujinx.UI.Helper
{
    public static class ButtonValueHelper
    {
        private static readonly Dictionary<Key, string> _keysMap = new()
        {
            { Key.Unknown, "Unknown" },
            { Key.ShiftLeft, "ShiftLeft" },
            { Key.ShiftRight, "ShiftRight" },
            { Key.ControlLeft, "CtrlLeft" },
            { Key.ControlRight, "CtrlRight" },
            { Key.AltLeft, OperatingSystem.IsMacOS() ? "OptLeft" : "AltLeft" },
            { Key.AltRight, OperatingSystem.IsMacOS() ? "OptRight" : "AltRight" },
            { Key.WinLeft, OperatingSystem.IsMacOS() ? "CmdLeft" : "WinLeft" },
            { Key.WinRight, OperatingSystem.IsMacOS() ? "CmdRight" : "WinRight" },
            { Key.Up, "Up" },
            { Key.Down, "Down" },
            { Key.Left, "Left" },
            { Key.Right, "Right" },
            { Key.Enter, "Enter" },
            { Key.Escape, "Escape" },
            { Key.Space, "Space" },
            { Key.Tab, "Tab" },
            { Key.BackSpace, "Backspace" },
            { Key.Insert, "Insert" },
            { Key.Delete, "Delete" },
            { Key.PageUp, "PageUp" },
            { Key.PageDown, "PageDown" },
            { Key.Home, "Home" },
            { Key.End, "End" },
            { Key.CapsLock, "CapsLock" },
            { Key.ScrollLock, "ScrollLock" },
            { Key.PrintScreen, "PrintScreen" },
            { Key.Pause, "Pause" },
            { Key.NumLock, "NumLock" },
            { Key.Clear, "Clear" },
            { Key.Keypad0, "Keypad0" },
            { Key.Keypad1, "Keypad1" },
            { Key.Keypad2, "Keypad2" },
            { Key.Keypad3, "Keypad3" },
            { Key.Keypad4, "Keypad4" },
            { Key.Keypad5, "Keypad5" },
            { Key.Keypad6, "Keypad6" },
            { Key.Keypad7, "Keypad7" },
            { Key.Keypad8, "Keypad8" },
            { Key.Keypad9, "Keypad9" },
            { Key.KeypadDivide, "KeypadDivide" },
            { Key.KeypadMultiply, "KeypadMultiply" },
            { Key.KeypadSubtract, "KeypadSubtract" },
            { Key.KeypadAdd, "KeypadAdd" },
            { Key.KeypadDecimal, "KeypadDecimal" },
            { Key.KeypadEnter, "KeypadEnter" },
            { Key.Number0, "0" },
            { Key.Number1, "1" },
            { Key.Number2, "2" },
            { Key.Number3, "3" },
            { Key.Number4, "4" },
            { Key.Number5, "5" },
            { Key.Number6, "6" },
            { Key.Number7, "7" },
            { Key.Number8, "8" },
            { Key.Number9, "9" },
            { Key.Tilde, "~" },
            { Key.Grave, "`" },
            { Key.Minus, "-" },
            { Key.Plus, "+" },
            { Key.BracketLeft, "[" },
            { Key.BracketRight, "]" },
            { Key.Semicolon, ";" },
            { Key.Quote, "'" },
            { Key.Comma, "," },
            { Key.Period, "." },
            { Key.Slash, "/" },
            { Key.BackSlash, "\\" },
            { Key.Unbound, "Unbound" },
        };

        private static readonly Dictionary<GamepadInputId, string> _gamepadInputIdMap = new()
        {
            { GamepadInputId.LeftStick, "LeftStick" },
            { GamepadInputId.RightStick, "RightStick" },
            { GamepadInputId.LeftShoulder, "LeftShoulder" },
            { GamepadInputId.RightShoulder, "RightShoulder" },
            { GamepadInputId.LeftTrigger, "LeftTrigger" },
            { GamepadInputId.RightTrigger, "RightTrigger" },
            { GamepadInputId.DpadUp, "DpadUp" },
            { GamepadInputId.DpadDown, "DpadDown" },
            { GamepadInputId.DpadLeft, "DpadLeft" },
            { GamepadInputId.DpadRight, "DpadRight" },
            { GamepadInputId.Minus, "Minus" },
            { GamepadInputId.Plus, "Plus" },
            { GamepadInputId.Guide, "Guide" },
            { GamepadInputId.Misc1, "Misc1" },
            { GamepadInputId.Paddle1, "Paddle1" },
            { GamepadInputId.Paddle2, "Paddle2" },
            { GamepadInputId.Paddle3, "Paddle3" },
            { GamepadInputId.Paddle4, "Paddle4" },
            { GamepadInputId.Touchpad, "Touchpad" },
            { GamepadInputId.SingleLeftTrigger0, "SingleLeftTrigger0" },
            { GamepadInputId.SingleRightTrigger0, "SingleRightTrigger0" },
            { GamepadInputId.SingleLeftTrigger1, "SingleLeftTrigger1" },
            { GamepadInputId.SingleRightTrigger1, "SingleRightTrigger1" },
            { GamepadInputId.Unbound, "Unbound" },
        };

        private static readonly Dictionary<StickInputId, string> _stickInputIdMap = new()
        {
            { StickInputId.Left, "StickLeft" },
            { StickInputId.Right, "StickRight" },
            { StickInputId.Unbound, "Unbound" },
        };

        public static string ToString(ButtonValue buttonValue)
        {
            string keyString = "";

            if (buttonValue.Type == ButtonValueType.Key)
            {
                var key = buttonValue.AsHidType<Key>();

                if (!_keysMap.TryGetValue(buttonValue.AsHidType<Key>(), out keyString))
                {
                    keyString = key.ToString();
                }
            }

            if (buttonValue.Type == ButtonValueType.GamepadButtonInputId)
            {
                var gamepadButton = buttonValue.AsHidType<GamepadInputId>();

                if (!_gamepadInputIdMap.TryGetValue(buttonValue.AsHidType<GamepadInputId>(), out keyString))
                {
                    keyString = gamepadButton.ToString();
                }
            }

            if (buttonValue.Type == ButtonValueType.StickId)
            {
                var stickInput = buttonValue.AsHidType<StickInputId>();

                if (!_stickInputIdMap.TryGetValue(buttonValue.AsHidType<StickInputId>(), out keyString))
                {
                    keyString = stickInput.ToString();
                }
            }

            return keyString;
        }
    }
}
