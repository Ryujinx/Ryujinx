using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter
{
    public class BehaviourErrorInfoOutStatusTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0xB0, Unsafe.SizeOf<BehaviourErrorInfoOutStatus>());
        }
    }
}
