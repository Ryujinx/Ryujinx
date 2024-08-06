using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TouchScreenState : ISampledDataStruct
    {
        public ulong SamplingNumber;
        public int TouchesCount;
        private readonly int _reserved;
        public Array16<TouchState> Touches;
    }
}
