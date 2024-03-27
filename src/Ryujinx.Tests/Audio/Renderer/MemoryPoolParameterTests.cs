using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer
{
    public class MemoryPoolParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x20, Unsafe.SizeOf<MemoryPoolInParameter>());
            Assert.Equal(0x10, Unsafe.SizeOf<MemoryPoolOutStatus>());
        }
    }
}
