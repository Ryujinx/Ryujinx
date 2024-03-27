using Ryujinx.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Effect
{
    public class LimiterParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x44, Unsafe.SizeOf<LimiterParameter>());
        }
    }
}
