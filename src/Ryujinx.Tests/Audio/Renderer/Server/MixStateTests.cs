using Ryujinx.Audio.Renderer.Server.Mix;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class MixStateTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x940, Unsafe.SizeOf<MixState>());
        }
    }
}
