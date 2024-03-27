using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    public class SinkOutStatusTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x20, Unsafe.SizeOf<SinkOutStatus>());
        }
    }
}
