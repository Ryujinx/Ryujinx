using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Common
{
    public class VoiceUpdateStateTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.True(Unsafe.SizeOf<VoiceUpdateState>() <= 0x100);
        }
    }
}
