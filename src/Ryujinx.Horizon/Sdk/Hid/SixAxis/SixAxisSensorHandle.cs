using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid.SixAxis
{
    [StructLayout(LayoutKind.Sequential, Size = 0x4)]
    struct SixAxisSensorHandle
    {
        public int TypeValue;
        public byte NpadStyleIndex;
        public byte PlayerNumber;
        public byte DeviceIdx;
    }
}
