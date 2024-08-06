using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    struct PackedMomentProcessorConfig
    {
        public long ExposureTime;
        public byte LightTarget;
        public byte Gain;
        public byte IsNegativeImageUsed;
        public byte Reserved1;
        public uint Reserved2;
        public ushort WindowOfInterestX;
        public ushort WindowOfInterestY;
        public ushort WindowOfInterestWidth;
        public ushort WindowOfInterestHeight;
        public PackedMcuVersion RequiredMcuVersion;
        public byte Preprocess;
        public byte PreprocessIntensityThreshold;
        public ushort Reserved3;
    }
}
