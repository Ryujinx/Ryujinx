using Ryujinx.Audio.Renderer.Server.MemoryPool;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class MemoryPoolStateTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x20, Unsafe.SizeOf<MemoryPoolState>());
        }

        [Fact]
        public void TestContains()
        {
            MemoryPoolState memoryPool = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            memoryPool.SetCpuAddress(0x1000000, 0x1000);

            memoryPool.DspAddress = 0x2000000;

            Assert.True(memoryPool.Contains(0x1000000, 0x10));
            Assert.True(memoryPool.Contains(0x1000FE0, 0x10));
            Assert.True(memoryPool.Contains(0x1000FFF, 0x1));
            Assert.False(memoryPool.Contains(0x1000FFF, 0x2));
            Assert.False(memoryPool.Contains(0x1001000, 0x10));
            Assert.False(memoryPool.Contains(0x2000000, 0x10));
        }

        [Fact]
        public void TestTranslate()
        {
            MemoryPoolState memoryPool = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            memoryPool.SetCpuAddress(0x1000000, 0x1000);

            memoryPool.DspAddress = 0x2000000;

            Assert.Equal(0x2000FE0ul, memoryPool.Translate(0x1000FE0, 0x10));
            Assert.Equal(0x2000FFFul, memoryPool.Translate(0x1000FFF, 0x1));
            Assert.Equal(0x0ul, memoryPool.Translate(0x1000FFF, 0x2));
            Assert.Equal(0x0ul, memoryPool.Translate(0x1001000, 0x10));
            Assert.Equal(0x0ul, memoryPool.Translate(0x2000000, 0x10));
        }

        [Fact]
        public void TestIsMapped()
        {
            MemoryPoolState memoryPool = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            memoryPool.SetCpuAddress(0x1000000, 0x1000);

            Assert.False(memoryPool.IsMapped());

            memoryPool.DspAddress = 0x2000000;

            Assert.True(memoryPool.IsMapped());
        }
    }
}
