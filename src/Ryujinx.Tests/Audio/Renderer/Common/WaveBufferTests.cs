using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Common
{
    public class WaveBufferTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x30, Unsafe.SizeOf<WaveBuffer>());
        }
    }
}
