using System;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth
{
    public enum AuthMethod : byte
    {
        NoAuthenticationRequired,
        GSSAPI,
        UsernameAndPassword,
        // 0x03 - 0x7F: IANA assigned
        // 0x80 - 0xFE: Reserved for private methods
        NoAcceptableMethods = 0xFF,
    }

    public static class AuthMethodExtensions
    {
        public static IProxyAuth GetAuth(this AuthMethod authMethod)
        {
            return authMethod switch
            {
                AuthMethod.NoAuthenticationRequired => new NoAuthentication(),
                // TODO: Implement GSSAPI to be SOCKS5 compliant
                AuthMethod.GSSAPI => throw new NotImplementedException(
                    $"Authentication method not implemented: {authMethod}"),
                AuthMethod.UsernameAndPassword => throw new NotImplementedException(
                    $"Authentication method not implemented: {authMethod}"),
                _ => throw new ArgumentException($"Invalid authentication method provided: {authMethod}",
                    nameof(authMethod)),
            };
        }
    }
}
