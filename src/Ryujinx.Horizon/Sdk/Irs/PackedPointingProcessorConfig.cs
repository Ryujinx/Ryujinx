using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    struct PackedPointingProcessorConfig
    {
        public ushort WindowOfInterestX;
        public ushort WindowOfInterestY;
        public ushort WindowOfInterestWidth;
        public ushort WindowOfInterestHeight;
        public PackedMcuVersion RequiredMcuVersion;
    }
}
