﻿namespace Ryujinx.HLE.HOS.Services.Usb
{
    [Service("usb:hs")]
    [Service("usb:hs:a")] // 7.0.0+
    sealed class IClientRootSession : IpcService
    {
        public IClientRootSession(ServiceCtx context) { }
    }
}