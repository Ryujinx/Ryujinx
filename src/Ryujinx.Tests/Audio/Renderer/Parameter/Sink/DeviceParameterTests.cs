using Ryujinx.Audio.Renderer.Parameter.Sink;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Parameter.Sink
{
    public class DeviceParameterTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x11C, Unsafe.SizeOf<DeviceParameter>());
        }
    }
}
