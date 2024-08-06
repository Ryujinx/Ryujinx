using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x8)]
    struct PackedIrLedProcessorConfig
    {
        public PackedMcuVersion RequiredMcuVersion;
        public byte LightTarget;
    }
}
