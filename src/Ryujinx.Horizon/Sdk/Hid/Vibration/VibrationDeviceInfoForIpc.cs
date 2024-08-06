using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid.Vibration
{
    [StructLayout(LayoutKind.Sequential, Size = 0x8)]
    struct VibrationDeviceInfoForIpc
    {
        public VibrationDeviceType DeviceType;
        public VibrationDevicePosition Position;
    }
}
