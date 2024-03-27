using Ryujinx.Audio.Renderer.Server.Voice;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class VoiceChannelResourceTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0xD0, Unsafe.SizeOf<VoiceChannelResource>());
        }
    }
}
