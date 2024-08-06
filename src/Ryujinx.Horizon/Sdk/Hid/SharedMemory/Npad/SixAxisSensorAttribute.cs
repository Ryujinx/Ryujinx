using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum SixAxisSensorAttribute : uint
    {
        None = 0,
        IsConnected = 1 << 0,
        IsInterpolated = 1 << 1,
    }
}
