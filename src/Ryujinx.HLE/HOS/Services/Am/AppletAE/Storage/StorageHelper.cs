using Microsoft.IO;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using System.IO;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.Storage
{
    class StorageHelper
    {
        private const uint LaunchParamsMagic = 0xc79497ca;

        public static byte[] MakeLaunchParams(UserProfile userProfile)
        {
            // Size needs to be at least 0x88 bytes otherwise application errors.
            using RecyclableMemoryStream ms = MemoryStreamManager.Shared.GetStream();
            BinaryWriter writer = new(ms);

            ms.SetLength(0x88);

            writer.Write(LaunchParamsMagic);
            writer.Write(1);  // IsAccountSelected? Only lower 8 bits actually used.
            userProfile.UserId.Write(writer);

            return ms.ToArray();
        }
    }
}
