using System;
using System.Security.Authentication;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth
{
    public interface IProxyAuth
    {
        /// <summary>
        /// The amount of additional bytes required for a wrapped packet;
        /// </summary>
        public int WrapperLength { get; }

        /// <summary>
        /// Authenticate to the server using a method-specific sub-negotiation.
        /// </summary>
        /// <exception cref="AuthenticationException">Authentication failed.</exception>
        public void Authenticate();

        /// <summary>
        /// Wrap the packet as required by the negotiated authentication method.
        /// </summary>
        /// <param name="packet">The packet to wrap.</param>
        /// <returns>The wrapped packet.</returns>
        public ReadOnlySpan<byte> Wrap(ReadOnlySpan<byte> packet);

        /// <summary>
        /// Unwrap the packet and perform the checks as required by the negotiated authentication method.
        /// </summary>
        /// <param name="packet">The packet to unwrap.</param>
        /// <returns>The unwrapped packet.</returns>
        public ReadOnlySpan<byte> Unwrap(ReadOnlySpan<byte> packet);
    }
}
