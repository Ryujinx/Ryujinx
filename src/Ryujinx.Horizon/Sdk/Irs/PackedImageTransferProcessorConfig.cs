using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x18)]
    struct PackedImageTransferProcessorConfig
    {
        public long ExposureTime;
        public byte LightTarget;
        public byte Gain;
        public byte IsNegativeImageUsed;
        public byte Reserved1;
        public uint Reserved2;
        public PackedMcuVersion RequiredMcuVersion;
        public byte Format;
        public byte Reserved3;
        public ushort Reserved4;
    }
}
