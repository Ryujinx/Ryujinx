namespace Ryujinx.Horizon.Sdk.Hid.HidDevices
{
    public abstract class BaseDevice
    {
        public bool Active;

        public BaseDevice(bool active)
        {
            Active = active;
        }
    }
}
