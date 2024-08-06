using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    struct PackedImageTransferProcessorExConfig
    {
        public long ExposureTime;
        public byte LightTarget;
        public byte Gain;
        public byte IsNegativeImageUsed;
        public byte Reserved1;
        public PackedMcuVersion RequiredMcuVersion;
        public byte OrigFormat;
        public byte TrimmingFormat;
        public ushort TrimmingStartX;
        public ushort TrimmingStartY;
        public byte IsExternalLightFilterEnabled;
    }
}
