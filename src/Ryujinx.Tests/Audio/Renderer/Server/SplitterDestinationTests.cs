using Ryujinx.Audio.Renderer.Server.Splitter;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class SplitterDestinationTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0xE0, Unsafe.SizeOf<SplitterDestination>());
        }
    }
}
