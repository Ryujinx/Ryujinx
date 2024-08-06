using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Sdk.Hid
{
    interface IHidbusSystemServer : IServiceObject
    {
        Result SetAppletResourceUserId(AppletResourceUserId resourceUserId);
        Result RegisterAppletResourceUserId(AppletResourceUserId resourceUserId, int arg1);
        Result UnregisterAppletResourceUserId(AppletResourceUserId resourceUserId);
    }
}
