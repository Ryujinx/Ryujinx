using System;

namespace Ryujinx.Horizon.Sdk.Hid.HidDevices
{
    public class TouchDevice : BaseDevice
    {
        public TouchDevice(bool active) : base(active) { }

        public void Update(params TouchPoint[] points)
        {
            ref RingLifo<TouchScreenState> lifo = ref _device.Hid.SharedMemory.TouchScreen;

            ref TouchScreenState previousEntry = ref lifo.GetCurrentEntryRef();

            TouchScreenState newState = new()
            {
                SamplingNumber = previousEntry.SamplingNumber + 1,
            };

            if (Active)
            {
                newState.TouchesCount = points.Length;

                int pointsLength = Math.Min(points.Length, newState.Touches.Length);

                for (int i = 0; i < pointsLength; ++i)
                {
                    TouchPoint pi = points[i];
                    newState.Touches[i] = new TouchState
                    {
                        DeltaTime = newState.SamplingNumber,
                        Attribute = pi.Attribute,
                        X = pi.X,
                        Y = pi.Y,
                        FingerId = (uint)i,
                        DiameterX = pi.DiameterX,
                        DiameterY = pi.DiameterY,
                        RotationAngle = pi.Angle,
                    };
                }
            }

            lifo.Write(ref newState);
        }
    }
}
