using System;

namespace Ryujinx.HLE.HOS.Applets
{
    [Flags]
    enum AmiiboSettingsReturnFlag : byte
    {
        Cancel = 0,

        HasTagInfo = 1 << 1,
        HasRegisterInfo = 1 << 2,

        HasCompleteInfo = HasTagInfo | HasRegisterInfo,
    }
}
