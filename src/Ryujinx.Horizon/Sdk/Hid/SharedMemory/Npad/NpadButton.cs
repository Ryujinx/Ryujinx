using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum NpadButton : ulong
    {
        None = 0,
        A = 1 << 0,
        B = 1 << 1,
        X = 1 << 2,
        Y = 1 << 3,
        StickL = 1 << 4,
        StickR = 1 << 5,
        L = 1 << 6,
        R = 1 << 7,
        ZL = 1 << 8,
        ZR = 1 << 9,
        Plus = 1 << 10,
        Minus = 1 << 11,
        Left = 1 << 12,
        Up = 1 << 13,
        Right = 1 << 14,
        Down = 1 << 15,
        StickLLeft = 1 << 16,
        StickLUp = 1 << 17,
        StickLRight = 1 << 18,
        StickLDown = 1 << 19,
        StickRLeft = 1 << 20,
        StickRUp = 1 << 21,
        StickRRight = 1 << 22,
        StickRDown = 1 << 23,
        LeftSL = 1 << 24,
        LeftSR = 1 << 25,
        RightSL = 1 << 26,
        RightSR = 1 << 27,
        Palma = 1 << 28,

        // FIXME: Probably a button on Lark.
        Unknown29 = 1 << 29,

        HandheldLeftB = 1 << 30,
    }
}
