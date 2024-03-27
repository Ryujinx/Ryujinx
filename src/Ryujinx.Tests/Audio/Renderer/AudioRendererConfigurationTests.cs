using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class AudioRendererConfigurationTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x34, Unsafe.SizeOf<AudioRendererConfiguration>());
        }
    }
}
