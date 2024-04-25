using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid.Npad
{
    [StructLayout(LayoutKind.Sequential)]
    struct BusHandle
    {
        public int AbstractedPadId;
        public byte InternalIndex;
        public byte PlayerNumber;
        public byte BusTypeId;
        public byte IsValid;
    }
}
