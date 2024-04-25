using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum KeyboardModifier : ulong
    {
        None = 0,
        Control = 1 << 0,
        Shift = 1 << 1,
        LeftAlt = 1 << 2,
        RightAlt = 1 << 3,
        Gui = 1 << 4,
        CapsLock = 1 << 8,
        ScrollLock = 1 << 9,
        NumLock = 1 << 10,
        Katakana = 1 << 11,
        Hiragana = 1 << 12,
    }
}
