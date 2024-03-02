using System;

namespace Ryujinx.Input
{
    public readonly struct ButtonValue
    {
        public readonly ButtonValueType Type;
        private readonly uint _rawValue;

        public ButtonValue(Key key)
        {
            Type = ButtonValueType.Key;
            _rawValue = (uint)key;
        }

        public ButtonValue(GamepadButtonInputId gamepad)
        {
            Type = ButtonValueType.GamepadButtonInputId;
            _rawValue = (uint)gamepad;
        }

        public ButtonValue(StickInputId stick)
        {
            Type = ButtonValueType.StickId;
            _rawValue = (uint)stick;
        }

        public T AsHidType<T>() where T : Enum
        {
            return (T)Enum.ToObject(typeof(T), _rawValue);
        }
    }
}
