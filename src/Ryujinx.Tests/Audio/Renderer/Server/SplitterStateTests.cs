using Ryujinx.Audio.Renderer.Server.Splitter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class SplitterStateTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x20, Unsafe.SizeOf<SplitterState>());
        }
    }
}
