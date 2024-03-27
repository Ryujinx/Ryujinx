using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class BiquadFilterParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0xC, Unsafe.SizeOf<BiquadFilterParameter>());
        }
    }
}
