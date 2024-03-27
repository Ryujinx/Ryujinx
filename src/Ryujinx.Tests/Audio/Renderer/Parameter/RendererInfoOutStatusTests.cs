using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    public class RendererInfoOutStatusTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<RendererInfoOutStatus>());
        }
    }
}
