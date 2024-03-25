using System;

namespace Ryujinx.Input
{
    public readonly struct Button
    {
        public readonly ButtonType Type;
        private readonly uint _rawValue;

        public Button(Key key)
        {
            Type = ButtonType.Key;
            _rawValue = (uint)key;
        }

        public Button(GamepadButtonInputId gamepad)
        {
            Type = ButtonType.GamepadButtonInputId;
            _rawValue = (uint)gamepad;
        }

        public Button(StickInputId stick)
        {
            Type = ButtonType.StickId;
            _rawValue = (uint)stick;
        }

        public T AsHidType<T>() where T : Enum
        {
            return (T)Enum.ToObject(typeof(T), _rawValue);
        }
    }
}
