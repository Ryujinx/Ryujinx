using NUnit.Framework;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets;
using System.Runtime.InteropServices;

namespace Ryujinx.Tests.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets
{
    public class MethodSelectionTests
    {
        [Test]
        public void MethodSelectionRequest1_Size()
        {
            // Version: 1 byte
            // Number of methods: 1 byte
            // Methods: 1 - 255 bytes (in this case: 1)
            // Total: 3 bytes

            var request = new MethodSelectionRequest1
            {
                Version = ProxyConsts.Version,
                NumOfMethods = 1,
                Methods = new Array1<AuthMethod> { [0] = AuthMethod.NoAuthenticationRequired },
            };

            Assert.AreEqual(3, Marshal.SizeOf(request));
        }

        [Test]
        public void MethodSelectionResponse_Size()
        {
            // Version: 1 byte
            // Method: 1 byte
            // Total: 2 bytes

            var response = new MethodSelectionResponse()
            {
                Version = ProxyConsts.Version,
                Method = AuthMethod.NoAuthenticationRequired,
            };

            Assert.AreEqual(2, Marshal.SizeOf(response));
        }
    }
}
