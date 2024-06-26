using Microsoft.IO;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.Services.Am.AppletAE;
using System;
using System.IO;

namespace Ryujinx.HLE.HOS.Applets
{
    internal class PlayerSelectApplet : IApplet
    {
        private readonly Horizon _system;

        private AppletSession _normalSession;
#pragma warning disable IDE0052 // Remove unread private member
        private AppletSession _interactiveSession;
#pragma warning restore IDE0052

        public event EventHandler AppletStateChanged;

        public PlayerSelectApplet(Horizon system)
        {
            _system = system;
        }

        public ResultCode Start(AppletSession normalSession, AppletSession interactiveSession)
        {
            _normalSession = normalSession;
            _interactiveSession = interactiveSession;

            // TODO(jduncanator): Parse PlayerSelectConfig from input data
            _normalSession.Push(BuildResponse());

            AppletStateChanged?.Invoke(this, null);

            _system.ReturnFocus();

            return ResultCode.Success;
        }

        public ResultCode GetResult()
        {
            return ResultCode.Success;
        }

        private byte[] BuildResponse()
        {
            UserProfile currentUser = _system.AccountManager.LastOpenedUser;

            using RecyclableMemoryStream stream = MemoryStreamManager.Shared.GetStream();
            using BinaryWriter writer = new(stream);

            writer.Write((ulong)PlayerSelectResult.Success);

            currentUser.UserId.Write(writer);

            return stream.ToArray();
        }
    }
}
