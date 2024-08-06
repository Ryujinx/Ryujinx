using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum NpadSystemProperties : ulong
    {
        None = 0,

        IsChargingJoyDual = 1 << 0,
        IsChargingJoyLeft = 1 << 1,
        IsChargingJoyRight = 1 << 2,
        IsPoweredJoyDual = 1 << 3,
        IsPoweredJoyLeft = 1 << 4,
        IsPoweredJoyRight = 1 << 5,
        IsUnsuportedButtonPressedOnNpadSystem = 1 << 9,
        IsUnsuportedButtonPressedOnNpadSystemExt = 1 << 10,
        IsAbxyButtonOriented = 1 << 11,
        IsSlSrButtonOriented = 1 << 12,
        IsPlusAvailable = 1 << 13,
        IsMinusAvailable = 1 << 14,
        IsDirectionalButtonsAvailable = 1 << 15,
    }
}
