using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MouseState : ISampledDataStruct
    {
        public ulong SamplingNumber;
        public int X;
        public int Y;
        public int DeltaX;
        public int DeltaY;
        public int WheelDeltaX;
        public int WheelDeltaY;
        public MouseButton Buttons;
        public MouseAttribute Attributes;
    }
}
