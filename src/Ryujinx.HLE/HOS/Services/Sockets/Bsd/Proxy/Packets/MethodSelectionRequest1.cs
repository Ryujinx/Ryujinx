using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets
{
    public struct MethodSelectionRequest1
    {
        public byte Version;
        public byte NumOfMethods;
        public Array1<AuthMethod> Methods;
    }
}
