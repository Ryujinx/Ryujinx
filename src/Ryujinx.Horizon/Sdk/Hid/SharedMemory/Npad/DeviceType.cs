using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum DeviceType
    {
        None = 0,

        FullKey = 1 << 0,
        DebugPad = 1 << 1,
        HandheldLeft = 1 << 2,
        HandheldRight = 1 << 3,
        JoyLeft = 1 << 4,
        JoyRight = 1 << 5,
        Palma = 1 << 6,
        LarkHvcLeft = 1 << 7,
        LarkHvcRight = 1 << 8,
        LarkNesLeft = 1 << 9,
        LarkNesRight = 1 << 10,
        HandheldLarkHvcLeft = 1 << 11,
        HandheldLarkHvcRight = 1 << 12,
        HandheldLarkNesLeft = 1 << 13,
        HandheldLarkNesRight = 1 << 14,
        Lucia = 1 << 15,

        System = 1 << 31,
    }
}
