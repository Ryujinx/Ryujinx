using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Applets
{
    [StructLayout(LayoutKind.Sequential, Size = 0x188, Pack = 1)]
    struct ReturnValueForAmiiboSettings
    {
        public AmiiboSettingsReturnFlag Flags;
        public Array3<byte> Padding;
        public ulong DeviceHandle;
        public TagInfo TagInfo;
        public RegisterInfo RegisterInfo;
        public Array36<byte> Ignored;
    }
}
