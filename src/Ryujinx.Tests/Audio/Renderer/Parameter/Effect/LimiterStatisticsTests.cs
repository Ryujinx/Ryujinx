using Ryujinx.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Effect
{
    public class LimiterStatisticsTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x30, Unsafe.SizeOf<LimiterStatistics>());
        }
    }
}
