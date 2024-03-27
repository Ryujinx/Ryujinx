using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class EffectInfoParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0xC0, Unsafe.SizeOf<EffectInParameterVersion1>());
            Assert.Equal(0xC0, Unsafe.SizeOf<EffectInParameterVersion2>());
        }
    }
}
