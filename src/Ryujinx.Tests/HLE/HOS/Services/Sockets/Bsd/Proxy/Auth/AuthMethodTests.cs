using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth;
using System;

namespace Ryujinx.Tests.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth
{
    public class AuthMethodTests
    {
        [Test]
        public void GetAuth_ReturnValue([Values] AuthMethod authMethod)
        {
            // TODO: Remove this as soon as we have an implementation for these
            if (authMethod is AuthMethod.UsernameAndPassword or AuthMethod.GSSAPI)
            {
                Assert.Throws<NotImplementedException>(() => authMethod.GetAuth());
                return;
            }

            if (authMethod is AuthMethod.NoAcceptableMethods)
            {
                Assert.Throws<ArgumentException>(() => authMethod.GetAuth());
                return;
            }

            Assert.IsInstanceOf<IProxyAuth>(authMethod.GetAuth());
        }
    }
}
