using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum KeyboardLockKeyEvent
    {
        NumLockOn = 1 << 0,
        NumLockOff = 1 << 1,
        NumLockToggle = 1 << 2,
        CapsLockOn = 1 << 3,
        CapsLockOff = 1 << 4,
        CapsLockToggle = 1 << 5,
        ScrollLockOn = 1 << 6,
        ScrollLockOff = 1 << 7,
        ScrollLockToggle = 1 << 8
    }
}
