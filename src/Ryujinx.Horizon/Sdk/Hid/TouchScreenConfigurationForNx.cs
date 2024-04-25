using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Hid
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    struct TouchScreenConfigurationForNx
    {
        public TouchScreenModeForNx Mode;
    }
}
