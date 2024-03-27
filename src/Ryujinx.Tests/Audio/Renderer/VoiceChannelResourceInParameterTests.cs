using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class VoiceChannelResourceInParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x70, Unsafe.SizeOf<VoiceChannelResourceInParameter>());
        }
    }
}
