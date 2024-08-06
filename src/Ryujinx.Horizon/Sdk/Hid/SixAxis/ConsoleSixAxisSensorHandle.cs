using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid.SixAxis
{
    [StructLayout(LayoutKind.Sequential, Size = 0x4)]
    struct ConsoleSixAxisSensorHandle
    {
        public int TypeValue;
        public byte Unknown1;
        public byte Unknown2;
    }
}
