using NetCoreServer;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Ryujinx.Tests.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    internal class MockIpv4Socks5Server : TcpServer
    {
        public readonly ConcurrentQueue<Guid> MockSessions = new();

        public MockIpv4Socks5Server() : base(IPAddress.Loopback, 0) { }

        protected override TcpSession CreateSession()
        {
            var session = new MockIpv4Socks5NoAuthSession(this);
            MockSessions.Enqueue(session.Id);

            return session;
        }
    }

    internal class MockIpv4Socks5NoAuthSession : TcpSession
    {
        public List<byte[]> Requests = new();
        public byte[] Response;
        public bool IsLastRequestValid;
        public string RequestError;

        public byte UsesVersion;
        public bool IsAuthenticated;
        public AuthMethod[] OfferedMethods;
        public ProxyCommand Command;
        public AddressType AddressType;
        public IPAddress DestinationAddress;
        public ushort DestinationPort;

        public MockIpv4Socks5NoAuthSession(TcpServer server) : base(server) {}

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Requests.Add(buffer);

            if (size < 3)
            {
                IsLastRequestValid = false;
                RequestError = $"Packet is too small. ({size} bytes)";
                return;
            }

            UsesVersion = buffer[0];

            if (!IsAuthenticated)
            {
                Authenticate(buffer, offset, size);
            }
            else
            {
                if (Command == 0)
                {
                    ParseCommand(buffer, offset, size);
                    return;
                }

                if (Command != ProxyCommand.UdpAssociate && Response is { Length: > 0 })
                {
                    IsLastRequestValid = true;
                    RequestError = string.Empty;
                    Send(Response);

                    Response = null;
                }
                else if (Command == ProxyCommand.UdpAssociate)
                {

                }
            }
        }

        public void Reset()
        {
            Requests.Clear();
            IsLastRequestValid = false;
            RequestError = string.Empty;

            UsesVersion = 0;
            IsAuthenticated = false;
            OfferedMethods = null;
            Command = 0;
            AddressType = 0;
            DestinationAddress = null;
            DestinationPort = 0;
        }

        private void SendReply(ReplyField replyCode, IPEndPoint boundEndpoint = null)
        {
            byte[] replyData =
            {
                // Version
                0x05,
                // Reply field
                (byte)replyCode,
                // Reserved
                0x00,
                // Address type: IPv4
                0x01,
                // Bound address
                0x00, 0x00, 0x00, 0x00,
                // Bound port
                0x00, 0x00,
            };

            if (boundEndpoint != null)
            {
                boundEndpoint.Address.GetAddressBytes().CopyTo(replyData, 4);
                BitConverter.GetBytes(boundEndpoint.Port).Reverse().ToArray().CopyTo(replyData, 8);
            }

            Send(replyData);
        }

        private void Authenticate(byte[] buffer, long offset, long size)
        {
            if (size > 2 + buffer[1])
            {
                IsLastRequestValid = false;
                RequestError = $"Packet is too large. (Expected {2 + buffer[1]} bytes, got {size}.)";
                return;
            }

            OfferedMethods = new AuthMethod[buffer[1]];
            for (int i = 0; i < OfferedMethods.Length; i++)
            {
                OfferedMethods[i] = (AuthMethod)buffer[2 + i];
            }

            if (UsesVersion == 5 && OfferedMethods.Contains(AuthMethod.NoAuthenticationRequired))
            {
                IsLastRequestValid = true;
                RequestError = string.Empty;
                Send(new byte[]
                {
                    // Version
                    0x05,
                    // Auth method
                    0x00,
                });
                IsAuthenticated = true;
            }
            else
            {
                IsLastRequestValid = false;
                RequestError = $"Couldn't find {AuthMethod.NoAuthenticationRequired} in offered auth methods.";
                Send(new byte[]
                {
                    // Version
                    0x05,
                    // Auth method: No acceptable method
                    0xFF,
                });
            }
        }

        private void ParseCommand(byte[] buffer, long offset, long size)
        {
            if (size != 10)
            {
                IsLastRequestValid = false;
                RequestError = $"Packet size is invalid. (Expected 10 bytes, got {size}.)";
                SendReply(ReplyField.ServerFailure);
            }

            Command = (ProxyCommand)buffer[1];

            if (buffer[2] != 0x00)
            {
                IsLastRequestValid = false;
                RequestError = $"Reserved must be 0x00. (actual value: 0x{buffer[2]:x})";
                SendReply(ReplyField.ServerFailure);
                return;
            }

            if (buffer[3] != 0x01)
            {
                IsLastRequestValid = false;
                RequestError = $"AddressType must be 0x01. (actual value: 0x{buffer[3]:x})";
                SendReply(ReplyField.AddressTypeNotSupported);
                return;
            }

            DestinationAddress = new IPAddress(buffer[4..8]);
            DestinationPort = BitConverter.ToUInt16(buffer, 8);

            IsLastRequestValid = true;
            RequestError = string.Empty;
            SendReply(ReplyField.Succeeded);
        }
    }
}
