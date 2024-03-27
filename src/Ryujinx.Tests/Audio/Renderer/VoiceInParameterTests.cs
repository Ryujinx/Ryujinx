using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class VoiceInParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x170, Unsafe.SizeOf<VoiceInParameter>());
        }
    }
}
