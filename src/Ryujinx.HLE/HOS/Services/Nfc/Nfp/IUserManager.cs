﻿using Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager;

namespace Ryujinx.HLE.HOS.Services.Nfc.Nfp
{
    [Service("nfp:user")]
    class IUserManager : IpcService
    {
#pragma warning disable IDE0060
        public IUserManager(ServiceCtx context) { }
#pragma warning restore IDE0060

        [CommandCmif(0)]
        // CreateUserInterface() -> object<nn::nfp::detail::IUser>
        public ResultCode CreateUserInterface(ServiceCtx context)
        {
            MakeObject(context, new INfp(NfpPermissionLevel.User));

            return ResultCode.Success;
        }
    }
}
