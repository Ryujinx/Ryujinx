using Ryujinx.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Effect
{
    public class BufferMixerParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x94, Unsafe.SizeOf<BufferMixParameter>());
        }
    }
}
