using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Applets
{
    [StructLayout(LayoutKind.Sequential, Size = 0x1a8)]
    struct StartParamForAmiiboSettings
    {
        public byte Unused1;
        public StartParamForAmiiboSettingsType Type;
        public AmiiboSettingsReturnFlag Flags;
        public Array9<byte> StartParamData1;
        public TagInfo TagInfo;
        public RegisterInfo RegisterInfo;
        public Array32<byte> StartParamData2;
        public Array36<byte> Unused2;
    }
}
