using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Common
{
    public class UpdateDataHeaderTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x40, Unsafe.SizeOf<UpdateDataHeader>());
        }
    }
}
