using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SocksIpv4Response
    {
        public byte Version;
        public ReplyField ReplyField;
        public byte Reserved;
        // NOTE: Must be AddressType.Ipv4Address
        public AddressType AddressType;
        private uint _boundAddress;
        private ushort _boundPort;

        public IPAddress BoundAddress
        {
            readonly get => new(BitConverter.GetBytes(_boundAddress));
            set => _boundAddress = BitConverter.ToUInt32(value.GetAddressBytes());
        }

        public ushort BoundPort
        {
            readonly get
            {
                byte[] portBytes = BitConverter.GetBytes(_boundPort);
                Array.Reverse(portBytes);
                return BitConverter.ToUInt16(portBytes);
            }
            set
            {
                byte[] portBytes = BitConverter.GetBytes(value);
                Array.Reverse(portBytes);
                _boundPort = BitConverter.ToUInt16(portBytes);
            }
        }
    }
}
