using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    struct ImageTransferProcessorState
    {
        public ulong SamplingNumber;
        public uint AmbientNoiseLevel;
        public uint Reserved;
    }
}
