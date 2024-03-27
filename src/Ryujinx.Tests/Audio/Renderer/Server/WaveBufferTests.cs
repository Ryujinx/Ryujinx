using Ryujinx.Audio.Renderer.Server.Voice;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class WaveBufferTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x58, Unsafe.SizeOf<WaveBuffer>());
        }
    }
}
