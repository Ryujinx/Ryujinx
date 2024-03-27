using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SocksIpv4Request
    {
        public byte Version;
        public ProxyCommand Command;
        public byte Reserved;
        // NOTE: Must be AddressType.Ipv4Address
        public AddressType AddressType;
        private uint _destinationAddress;
        private ushort _destinationPort;

        public IPAddress DestinationAddress
        {
            readonly get => new(BitConverter.GetBytes(_destinationAddress));
            set => _destinationAddress = BitConverter.ToUInt32(value.GetAddressBytes());
        }

        public ushort DestinationPort
        {
            readonly get
            {
                byte[] portBytes = BitConverter.GetBytes(_destinationPort);
                Array.Reverse(portBytes);
                return BitConverter.ToUInt16(portBytes);
            }
            set
            {
                byte[] portBytes = BitConverter.GetBytes(value);
                Array.Reverse(portBytes);
                _destinationPort = BitConverter.ToUInt16(portBytes);
            }
        }
    }
}
