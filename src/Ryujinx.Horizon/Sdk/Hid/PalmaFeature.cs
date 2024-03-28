using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum PalmaFeature
    {
        FrMode = 1 << 0,
        RumbleFeedback = 1 << 1,
        Step = 1 << 2,
        MuteSwitch = 1 << 3
    }
}
