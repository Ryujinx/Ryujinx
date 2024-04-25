using System;

namespace Ryujinx.Horizon.Sdk.Hid.Npad
{
    [Flags]
    public enum ControllerKeys : long
    {
        A = 1 << 0,
        B = 1 << 1,
        X = 1 << 2,
        Y = 1 << 3,
        LStick = 1 << 4,
        RStick = 1 << 5,
        L = 1 << 6,
        R = 1 << 7,
        Zl = 1 << 8,
        Zr = 1 << 9,
        Plus = 1 << 10,
        Minus = 1 << 11,
        DpadLeft = 1 << 12,
        DpadUp = 1 << 13,
        DpadRight = 1 << 14,
        DpadDown = 1 << 15,
        LStickLeft = 1 << 16,
        LStickUp = 1 << 17,
        LStickRight = 1 << 18,
        LStickDown = 1 << 19,
        RStickLeft = 1 << 20,
        RStickUp = 1 << 21,
        RStickRight = 1 << 22,
        RStickDown = 1 << 23,
        SlLeft = 1 << 24,
        SrLeft = 1 << 25,
        SlRight = 1 << 26,
        SrRight = 1 << 27,

        // Generic Catch-all
        Up = DpadUp | LStickUp | RStickUp,
        Down = DpadDown | LStickDown | RStickDown,
        Left = DpadLeft | LStickLeft | RStickLeft,
        Right = DpadRight | LStickRight | RStickRight,
        Sl = SlLeft | SlRight,
        Sr = SrLeft | SrRight,
    }
}
