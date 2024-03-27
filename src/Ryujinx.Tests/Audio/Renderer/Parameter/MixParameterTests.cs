using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    public class MixParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x930, Unsafe.SizeOf<MixParameter>());
        }
    }
}
