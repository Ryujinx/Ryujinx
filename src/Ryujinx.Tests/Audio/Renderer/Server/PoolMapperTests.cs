using Ryujinx.Audio;
using Ryujinx.Audio.Renderer.Server.MemoryPool;
using System;
using Xunit;
using static Ryujinx.Audio.Renderer.Common.BehaviourParameter;
using CpuAddress = System.UInt64;
using DspAddress = System.UInt64;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class PoolMapperTests
    {
        private const uint DummyProcessHandle = 0xCAFEBABE;

        [Fact]
        public void TestInitializeSystemPool()
        {
            PoolMapper poolMapper = new(DummyProcessHandle, true);
            MemoryPoolState memoryPoolDsp = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            MemoryPoolState memoryPoolCpu = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            const CpuAddress CpuAddress = 0x20000;
            const DspAddress DspAddress = CpuAddress; // TODO: DSP LLE
            const ulong CpuSize = 0x1000;

            Assert.False(poolMapper.InitializeSystemPool(ref memoryPoolCpu, CpuAddress, CpuSize));
            Assert.True(poolMapper.InitializeSystemPool(ref memoryPoolDsp, CpuAddress, CpuSize));

            Assert.Equal(CpuAddress, memoryPoolDsp.CpuAddress);
            Assert.Equal(CpuSize, memoryPoolDsp.Size);
            Assert.Equal(DspAddress, memoryPoolDsp.DspAddress);
        }

        [Fact]
        public void TestGetProcessHandle()
        {
            PoolMapper poolMapper = new(DummyProcessHandle, true);
            MemoryPoolState memoryPoolDsp = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            MemoryPoolState memoryPoolCpu = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            Assert.Equal(0xFFFF8001, poolMapper.GetProcessHandle(ref memoryPoolCpu));
            Assert.Equal(DummyProcessHandle, poolMapper.GetProcessHandle(ref memoryPoolDsp));
        }

        [Fact]
        public void TestMappings()
        {
            PoolMapper poolMapper = new(DummyProcessHandle, true);
            MemoryPoolState memoryPoolDsp = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            MemoryPoolState memoryPoolCpu = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            const CpuAddress CpuAddress = 0x20000;
            const DspAddress DspAddress = CpuAddress; // TODO: DSP LLE
            const ulong CpuSize = 0x1000;

            memoryPoolDsp.SetCpuAddress(CpuAddress, CpuSize);
            memoryPoolCpu.SetCpuAddress(CpuAddress, CpuSize);

            Assert.Equal(DspAddress, poolMapper.Map(ref memoryPoolCpu));
            Assert.Equal(DspAddress, poolMapper.Map(ref memoryPoolDsp));
            Assert.Equal(DspAddress, memoryPoolDsp.DspAddress);
            Assert.True(poolMapper.Unmap(ref memoryPoolCpu));

            memoryPoolDsp.IsUsed = true;
            Assert.False(poolMapper.Unmap(ref memoryPoolDsp));
            memoryPoolDsp.IsUsed = false;
            Assert.True(poolMapper.Unmap(ref memoryPoolDsp));
        }

        [Fact]
        public void TestTryAttachBuffer()
        {
            const CpuAddress CpuAddress = 0x20000;
            const DspAddress DspAddress = CpuAddress; // TODO: DSP LLE
            const ulong CpuSize = 0x1000;

            const int MemoryPoolStateArraySize = 0x10;
            const CpuAddress CpuAddressRegionEnding = CpuAddress * MemoryPoolStateArraySize;

            MemoryPoolState[] memoryPoolStateArray = new MemoryPoolState[MemoryPoolStateArraySize];

            for (int i = 0; i < memoryPoolStateArray.Length; i++)
            {
                memoryPoolStateArray[i] = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);
                memoryPoolStateArray[i].SetCpuAddress(CpuAddress + (ulong)i * CpuSize, CpuSize);
            }


            AddressInfo addressInfo = AddressInfo.Create();

            PoolMapper poolMapper = new(DummyProcessHandle, true);

            Assert.True(poolMapper.TryAttachBuffer(out ErrorInfo errorInfo, ref addressInfo, 0, 0));

            Assert.Equal(ResultCode.InvalidAddressInfo, errorInfo.ErrorCode);
            Assert.Equal(0ul, errorInfo.ExtraErrorInfo);
            Assert.Equal(0ul, addressInfo.ForceMappedDspAddress);

            Assert.True(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddress, CpuSize));

            Assert.Equal(ResultCode.InvalidAddressInfo, errorInfo.ErrorCode);
            Assert.Equal(CpuAddress, errorInfo.ExtraErrorInfo);
            Assert.Equal(DspAddress, addressInfo.ForceMappedDspAddress);

            poolMapper = new PoolMapper(DummyProcessHandle, false);

            Assert.False(poolMapper.TryAttachBuffer(out _, ref addressInfo, 0, 0));

            addressInfo.ForceMappedDspAddress = 0;

            Assert.False(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddress, CpuSize));

            Assert.Equal(ResultCode.InvalidAddressInfo, errorInfo.ErrorCode);
            Assert.Equal(CpuAddress, errorInfo.ExtraErrorInfo);
            Assert.Equal(0ul, addressInfo.ForceMappedDspAddress);

            poolMapper = new PoolMapper(DummyProcessHandle, memoryPoolStateArray.AsMemory(), false);

            Assert.False(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddressRegionEnding, CpuSize));

            Assert.Equal(ResultCode.InvalidAddressInfo, errorInfo.ErrorCode);
            Assert.Equal(CpuAddressRegionEnding, errorInfo.ExtraErrorInfo);
            Assert.Equal(0ul, addressInfo.ForceMappedDspAddress);
            Assert.False(addressInfo.HasMemoryPoolState);

            Assert.True(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddress, CpuSize));

            Assert.Equal(ResultCode.Success, errorInfo.ErrorCode);
            Assert.Equal(0ul, errorInfo.ExtraErrorInfo);
            Assert.Equal(0ul, addressInfo.ForceMappedDspAddress);
            Assert.True(addressInfo.HasMemoryPoolState);
        }
    }
}
