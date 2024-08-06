using Ryujinx.Common.Logging;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Irs;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Irs
{
    class IrSensorSystemServer : IIrSensorSystemServer
    {
        [CmifCommand(500)]
        public Result SetAppletResourceUserId(AppletResourceUserId appletResourceUserId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id });

            return Result.Success;
        }

        [CmifCommand(501)]
        public Result RegisterAppletResourceUserId(AppletResourceUserId appletResourceUserId, bool arg1)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, arg1 });

            return Result.Success;
        }

        [CmifCommand(502)]
        public Result UnregisterAppletResourceUserId(AppletResourceUserId appletResourceUserId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id });

            return Result.Success;
        }

        [CmifCommand(503)]
        public Result EnableAppletToGetInput(AppletResourceUserId appletResourceUserId, bool arg1)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, arg1 });

            return Result.Success;
        }
    }
}
