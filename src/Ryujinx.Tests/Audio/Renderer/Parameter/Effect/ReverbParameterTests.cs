using Ryujinx.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Effect
{
    public class ReverbParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x41, Unsafe.SizeOf<ReverbParameter>());
        }
    }
}
