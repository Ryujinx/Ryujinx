using Ryujinx.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Effect
{
    public class AuxParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x6C, Unsafe.SizeOf<AuxiliaryBufferParameter>());
        }
    }
}
