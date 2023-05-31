﻿using Ryujinx.Common;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.LibraryAppletProxy
{
    sealed class IProcessWindingController : IpcService
    {
        public IProcessWindingController() { }

        [CommandCmif(0)]
        // GetLaunchReason() -> nn::am::service::AppletProcessLaunchReason
        public ResultCode GetLaunchReason(ServiceCtx context)
        {
            // NOTE: Flag is set by using an internal field.
            AppletProcessLaunchReason appletProcessLaunchReason = new AppletProcessLaunchReason()
            {
                Flag = 0
            };

            context.ResponseData.WriteStruct(appletProcessLaunchReason);

            return ResultCode.Success;
        }
    }
}