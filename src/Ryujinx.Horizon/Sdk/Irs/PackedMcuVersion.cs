using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x4)]
    public struct PackedMcuVersion
    {
        public short MajorVersion;
        public short MinorVersion;
    }
}
