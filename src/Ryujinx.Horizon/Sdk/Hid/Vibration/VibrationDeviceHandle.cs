using Ryujinx.Horizon.Sdk.Hid.Npad;

namespace Ryujinx.Horizon.Sdk.Hid.Vibration
{
    public struct VibrationDeviceHandle
    {
        public NpadStyleIndex DeviceType;
        public NpadIdType PlayerId;
        public byte Position;
        public byte Reserved;
    }
}
