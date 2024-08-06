using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Applets
{
    public struct ControllerAppletUIArgs
    {
        public int PlayerCountMin;
        public int PlayerCountMax;
        public ControllerType SupportedStyles;
        public IEnumerable<PlayerIndex> SupportedPlayers;
        public bool IsDocked;
    }
}
