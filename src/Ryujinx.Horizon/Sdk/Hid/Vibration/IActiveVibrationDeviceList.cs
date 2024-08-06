using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Sdk.Hid.Vibration
{
    interface IActiveVibrationDeviceList : IServiceObject
    {
        Result ActivateVibrationDevice(VibrationDeviceHandle vibrationDeviceHandle);
    }
}
