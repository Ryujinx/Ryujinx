using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [Flags]
    enum MouseButton : uint
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Middle = 1 << 2,
        Forward = 1 << 3,
        Back = 1 << 4,
    }
}
