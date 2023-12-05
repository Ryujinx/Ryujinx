using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using Ryujinx.Tests.HLE.HOS.Services.Sockets.Bsd.Proxy;
using System;
using System.Net;
using System.Net.Sockets;

namespace Ryujinx.Tests.HLE.HOS.Services.Sockets.Bsd.Impl
{
    [TestFixture(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)]
    [TestFixture(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)]
    internal class ManagedProxySocketTestFixture(AddressFamily addressFamily, SocketType socketType,
        ProtocolType protocolType)
    {
        private readonly IPEndPoint _serverEndPoint = new(IPAddress.Loopback, 0);
        private readonly MockIpv4Socks5Server _server = new();
        private ManagedProxySocket _proxySocket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server.Start();
            _serverEndPoint.Port = ((IPEndPoint)_server.Endpoint).Port;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.DisconnectAll();
            _server.Stop();
            _server.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            _proxySocket = new ManagedProxySocket(addressFamily, socketType, protocolType, _serverEndPoint);
        }

        [TearDown]
        public void TearDown()
        {
            _proxySocket.Dispose();
            _proxySocket = null;
        }

        [Test]
        public void Bind()
        {
            LinuxError result = _proxySocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            bool dequeueResult = _server.MockSessions.TryDequeue(out Guid clientId);

            Assert.AreEqual(LinuxError.SUCCESS, result);
            Assert.True(dequeueResult);

            MockIpv4Socks5NoAuthSession proxySession = (MockIpv4Socks5NoAuthSession)_server.FindSession(clientId);

            Assert.AreEqual(0x05, proxySession.UsesVersion);
            Assert.True(proxySession.IsAuthenticated);
            Assert.True(proxySession.IsLastRequestValid, proxySession.RequestError);
            Assert.AreNotEqual(0, proxySession.Command);
            Assert.AreNotEqual(ProxyCommand.Connect, proxySession.Command);
            Assert.NotNull(_proxySocket.LocalEndPoint);
        }
    }
}
