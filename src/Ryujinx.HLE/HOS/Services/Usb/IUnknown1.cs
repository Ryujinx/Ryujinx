﻿namespace Ryujinx.HLE.HOS.Services.Usb
{
    [Service("usb:qdb")] // 7.0.0+
    sealed class IUnknown1 : IpcService
    {
        public IUnknown1(ServiceCtx context) { }
    }
}