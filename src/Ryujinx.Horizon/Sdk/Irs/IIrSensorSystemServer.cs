using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Sdk.Irs
{
    interface IIrSensorSystemServer : IServiceObject
    {
        Result SetAppletResourceUserId(AppletResourceUserId appletResourceUserId);
        Result RegisterAppletResourceUserId(AppletResourceUserId appletResourceUserId, bool arg1);
        Result UnregisterAppletResourceUserId(AppletResourceUserId appletResourceUserId);
        Result EnableAppletToGetInput(AppletResourceUserId appletResourceUserId, bool arg1);
    }
}
