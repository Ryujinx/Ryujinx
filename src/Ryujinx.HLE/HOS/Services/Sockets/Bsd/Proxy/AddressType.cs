namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    public enum AddressType : byte
    {
        Ipv4Address = 0x01,
        // TODO: Implement support for DomainName and IPv6 addresses to be SOCKS5 compliant
        DomainName = 0x03,
        Ipv6Address,
    }
}
