using Ryujinx.Audio.Renderer.Parameter.Sink;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Sink
{
    public class CircularBufferParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x24, Unsafe.SizeOf<CircularBufferParameter>());
        }
    }
}
