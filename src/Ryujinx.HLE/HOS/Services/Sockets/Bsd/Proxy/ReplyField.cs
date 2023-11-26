namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    public enum ReplyField : byte
    {
        Succeeded,
        ServerFailure,
        ConnectionNotAllowed,
        NetworkUnreachable,
        HostUnreachable,
        ConnectionRefused,
        TTLExpired,
        CommandNotSupported,
        AddressTypeNotSupported,
    }
}
