using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum DebugPadAttribute : uint
    {
        None = 0,
        Connected = 1 << 0,
    }
}
