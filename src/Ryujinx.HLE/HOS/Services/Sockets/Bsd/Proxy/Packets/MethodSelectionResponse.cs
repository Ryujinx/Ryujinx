using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets
{
    public struct MethodSelectionResponse
    {
        public byte Version;
        public AuthMethod Method;
    }
}
