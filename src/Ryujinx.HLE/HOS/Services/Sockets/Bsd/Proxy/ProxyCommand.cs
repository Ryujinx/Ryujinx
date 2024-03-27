namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    public enum ProxyCommand : byte
    {
        Connect = 0x01,
        Bind,
        UdpAssociate,
    }
}
