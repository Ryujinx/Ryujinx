using Ryujinx.Audio.Renderer.Parameter.Performance;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    public class PerformanceInParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<PerformanceInParameter>());
        }
    }
}
