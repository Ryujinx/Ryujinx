using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum DebugPadButton : uint
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        X = 1 << 2,
        Y = 1 << 3,
        L = 1 << 4,
        R = 1 << 5,
        ZL = 1 << 6,
        ZR = 1 << 7,
        Start = 1 << 8,
        Select = 1 << 9,
        Left = 1 << 10,
        Up = 1 << 11,
        Right = 1 << 12,
        Down = 1 << 13,
    }
}
