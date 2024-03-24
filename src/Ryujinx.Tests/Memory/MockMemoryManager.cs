using ARMeilleure.Memory;
using System;

namespace Ryujinx.Tests.Memory
{
    internal class MockMemoryManager : IMemoryManager
    {
        public int AddressSpaceBits => throw new NotImplementedException();

        public IntPtr PageTablePointer => throw new NotImplementedException();

        public MemoryManagerType Type => MemoryManagerType.HostMappedUnsafe;

#pragma warning disable CS0067 // The event is never used
        public event Action<ulong, ulong, bool> UnmapEvent;
#pragma warning restore CS0067

        public ref T GetRef<T>(ulong va) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false)
        {
            throw new NotImplementedException();
        }

        public bool IsMapped(ulong va)
        {
            throw new NotImplementedException();
        }

        public T Read<T>(ulong va) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public T ReadTracked<T>(ulong va) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null)
        {
            throw new NotImplementedException();
        }

        public void Write<T>(ulong va, T value) where T : unmanaged
        {
            throw new NotImplementedException();
        }
    }
}
