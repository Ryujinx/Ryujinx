using Ryujinx.Audio.Renderer.Parameter.Effect;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Effect
{
    public class Reverb3dParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x49, Unsafe.SizeOf<Reverb3dParameter>());
        }
    }
}
