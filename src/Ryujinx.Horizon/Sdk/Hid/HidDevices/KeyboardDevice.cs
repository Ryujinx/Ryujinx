using System;

namespace Ryujinx.Horizon.Sdk.Hid.HidDevices
{
    public class KeyboardDevice : BaseDevice
    {
        public KeyboardDevice(bool active) : base(active) { }

        public void Update(KeyboardInput keyState)
        {
            ref RingLifo<KeyboardState> lifo = ref _device.Hid.SharedMemory.Keyboard;

            if (!Active)
            {
                lifo.Clear();

                return;
            }

            ref KeyboardState previousEntry = ref lifo.GetCurrentEntryRef();

            KeyboardState newState = new()
            {
                SamplingNumber = previousEntry.SamplingNumber + 1,
            };

            keyState.Keys.AsSpan().CopyTo(newState.Keys.RawData.AsSpan());
            newState.Modifiers = (KeyboardModifier)keyState.Modifier;

            lifo.Write(ref newState);
        }
    }
}
