using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    public class SinkInParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x140, Unsafe.SizeOf<SinkInParameter>());
        }
    }
}
