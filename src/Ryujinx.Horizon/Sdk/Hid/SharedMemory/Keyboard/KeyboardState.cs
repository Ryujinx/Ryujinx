using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct KeyboardState : ISampledDataStruct
    {
        public ulong SamplingNumber;
        public KeyboardModifier Modifiers;
        public KeyboardKey Keys;
    }
}
