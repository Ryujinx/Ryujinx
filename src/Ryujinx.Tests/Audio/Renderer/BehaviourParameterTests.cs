using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class BehaviourParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<BehaviourParameter>());
            Assert.Equal(0x10, Unsafe.SizeOf<BehaviourParameter.ErrorInfo>());
        }
    }
}
