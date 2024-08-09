using System;

namespace Ryujinx.Graphics.GAL
{
    [Flags]
    public enum PolygonModeMask
    {
        None = 0,
        Point = 1 << 0,
        Line = 1 << 1,
        Fill = 1 << 2,
    }
}
