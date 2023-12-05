using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets;
using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Ryujinx.Tests.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets
{
    public class SocksIpv4Tests
    {
        [Test]
        public void Request_Size()
        {
            // Version: 1 byte
            // Command: 1 byte
            // Reserved: 1 byte
            // Address type: 1 byte
            // IPv4 address: 4 bytes
            // Port: 2 bytes
            // Total: 10 bytes

            var request = new SocksIpv4Request
            {
                Version = ProxyConsts.Version,
                Reserved = 0x00,
                Command = ProxyCommand.Connect,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = IPAddress.Any,
                DestinationPort = 0,
            };

            Assert.AreEqual(10, Marshal.SizeOf(request));
        }

        [Test]
        public void Response_Size()
        {
            // Version: 1 byte
            // Reply: 1 byte
            // Reserved: 1 byte
            // Address type: 1 byte
            // IPv4 address: 4 bytes
            // Port: 2 bytes
            // Total: 10 bytes

            var response = new SocksIpv4Response
            {
                Version = ProxyConsts.Version,
                Reserved = 0x00,
                ReplyField = ReplyField.Succeeded,
                AddressType = AddressType.Ipv4Address,
                BoundAddress = IPAddress.Any,
                BoundPort = 0,
            };

            Assert.AreEqual(10, Marshal.SizeOf(response));
        }

        [Test]
        public void UdpHeader_Size()
        {
            // Reserved: 2 bytes
            // Fragment: 1 byte
            // Address type: 1 byte
            // IPv4 address: 4 bytes
            // Port: 2 bytes
            // Total: 10 bytes

            var header = new SocksIpv4UdpHeader
            {
                Reserved = 0x0000,
                Fragment = 0,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = IPAddress.Any,
                DestinationPort = 0,
            };

            Assert.AreEqual(10, Marshal.SizeOf(header));
        }

        [Test, Sequential]
        public void Port_ByteOrder(
            [Values((ushort)443, (ushort)2127, (ushort)22)] ushort port,
            [Values((ushort)47873, (ushort)20232, (ushort)5632)] ushort expected)
        {
            var request = new SocksIpv4Request
            {
                Version = ProxyConsts.Version,
                Reserved = 0x00,
                Command = ProxyCommand.Connect,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = IPAddress.Any,
                DestinationPort = port,
            };

            var response = new SocksIpv4Response
            {
                Version = ProxyConsts.Version,
                Reserved = 0x00,
                ReplyField = ReplyField.Succeeded,
                AddressType = AddressType.Ipv4Address,
                BoundAddress = IPAddress.Any,
                BoundPort = port,
            };

            var header = new SocksIpv4UdpHeader
            {
                Reserved = 0x0000,
                Fragment = 0,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = IPAddress.Any,
                DestinationPort = port,
            };

            byte[] requestData = new byte[10];
            byte[] responseData = new byte[10];
            byte[] headerData = new byte[10];

            MemoryMarshal.Write(requestData, request);
            MemoryMarshal.Write(responseData, response);
            MemoryMarshal.Write(headerData, header);

            Assert.AreEqual(expected, BitConverter.ToUInt16(requestData, 8));
            Assert.AreEqual(expected, BitConverter.ToUInt16(responseData, 8));
            Assert.AreEqual(expected, BitConverter.ToUInt16(headerData, 8));
        }
    }
}
