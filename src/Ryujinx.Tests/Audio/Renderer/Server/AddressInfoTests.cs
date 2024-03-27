using Ryujinx.Audio.Renderer.Server.MemoryPool;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class AddressInfoTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.Equal(0x20, Unsafe.SizeOf<AddressInfo>());
        }

        [Fact]
        public void TestGetReference()
        {
            MemoryPoolState[] memoryPoolState = new MemoryPoolState[1];
            memoryPoolState[0] = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);
            memoryPoolState[0].SetCpuAddress(0x1000000, 0x10000);
            memoryPoolState[0].DspAddress = 0x4000000;

            AddressInfo addressInfo = AddressInfo.Create(0x1000000, 0x1000);

            addressInfo.ForceMappedDspAddress = 0x2000000;

            Assert.Equal(0x2000000ul, addressInfo.GetReference(true));

            addressInfo.SetupMemoryPool(memoryPoolState.AsSpan());

            Assert.Equal(0x4000000ul, addressInfo.GetReference(true));
        }
    }
}
