using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum NpadSystemButtonProperties : uint
    {
        None = 0,
        IsUnintendedHomeButtonInputProtectionEnabled = 1 << 0,
    }
}
