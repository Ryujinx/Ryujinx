using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class EffectOutStatusTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<EffectOutStatusVersion1>());
            Assert.Equal(0x90, Unsafe.SizeOf<EffectOutStatusVersion2>());
        }
    }
}
