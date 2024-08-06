using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum MouseAttribute : uint
    {
        None = 0,
        Transferable = 1 << 0,
        IsConnected = 1 << 1,
    }
}
