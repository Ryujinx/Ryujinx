using System;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth
{
    public class NoAuthentication : IProxyAuth
    {
        public int WrapperLength => 0;

        public void Authenticate()
        {
            // Nothing to do here.
        }

        public ReadOnlySpan<byte> Wrap(ReadOnlySpan<byte> packet)
        {
            return packet;
        }

        public ReadOnlySpan<byte> Unwrap(ReadOnlySpan<byte> packet)
        {
            return packet;
        }
    }
}
