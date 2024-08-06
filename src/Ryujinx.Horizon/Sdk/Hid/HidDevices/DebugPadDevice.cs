namespace Ryujinx.Horizon.Sdk.Hid.HidDevices
{
    public class DebugPadDevice : BaseDevice
    {
        public DebugPadDevice(bool active) : base(active) { }

        public void Update()
        {
            ref RingLifo<DebugPadState> lifo = ref _device.Hid.SharedMemory.DebugPad;

            ref DebugPadState previousEntry = ref lifo.GetCurrentEntryRef();

            DebugPadState newState = new();

            if (Active)
            {
                // TODO: This is a debug device only present in dev environment, do we want to support it?
            }

            newState.SamplingNumber = previousEntry.SamplingNumber + 1;

            lifo.Write(ref newState);
        }
    }
}
