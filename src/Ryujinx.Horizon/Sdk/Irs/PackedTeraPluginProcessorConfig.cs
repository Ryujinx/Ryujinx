using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Irs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x8)]
    struct PackedTeraPluginProcessorConfig
    {
        public PackedMcuVersion RequiredMcuVersion;
        public byte Mode;
        public byte Unknown1;
        public byte Unknown2;
        public byte Unknown3;
    }
}
