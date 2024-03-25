using Ryujinx.Memory.Range;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Memory
{
    /// <summary>
    /// Represents a address space manager.
    /// Supports virtual memory region mapping, address translation and read/write access to mapped regions.
    /// </summary>
    public sealed class AddressSpaceManager : VirtualMemoryManagerBase<ulong, nuint>, IVirtualMemoryManager, IWritableBlock
    {
        /// <inheritdoc/>
        public bool Supports4KBPages => true;

        /// <summary>
        /// Address space width in bits.
        /// </summary>
        public int AddressSpaceBits { get; }

        private readonly MemoryBlock _backingMemory;
        private readonly PageTable<nuint> _pageTable;

        protected override ulong AddressSpaceSize { get; }

        /// <summary>
        /// Creates a new instance of the memory manager.
        /// </summary>
        /// <param name="backingMemory">Physical backing memory where virtual memory will be mapped to</param>
        /// <param name="addressSpaceSize">Size of the address space</param>
        public AddressSpaceManager(MemoryBlock backingMemory, ulong addressSpaceSize)
        {
            ulong asSize = PageSize;
            int asBits = PageBits;

            while (asSize < addressSpaceSize)
            {
                asSize <<= 1;
                asBits++;
            }

            AddressSpaceBits = asBits;
            AddressSpaceSize = asSize;
            _backingMemory = backingMemory;
            _pageTable = new PageTable<nuint>();
        }

        /// <inheritdoc/>
        public void Map(ulong va, ulong pa, ulong size, MemoryMapFlags flags)
        {
            AssertValidAddressAndSize(va, size);

            while (size != 0)
            {
                _pageTable.Map(va, (nuint)(ulong)_backingMemory.GetPointer(pa, PageSize));

                va += PageSize;
                pa += PageSize;
                size -= PageSize;
            }
        }

        /// <inheritdoc/>
        public void MapForeign(ulong va, nuint hostPointer, ulong size)
        {
            AssertValidAddressAndSize(va, size);

            while (size != 0)
            {
                _pageTable.Map(va, hostPointer);

                va += PageSize;
                hostPointer += PageSize;
                size -= PageSize;
            }
        }

        /// <inheritdoc/>
        public void Unmap(ulong va, ulong size)
        {
            AssertValidAddressAndSize(va, size);

            while (size != 0)
            {
                _pageTable.Unmap(va);

                va += PageSize;
                size -= PageSize;
            }
        }

        /// <inheritdoc/>
        public T Read<T>(ulong va) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetSpan(va, Unsafe.SizeOf<T>()))[0];
        }

        /// <inheritdoc/>
        public void Write<T>(ulong va, T value) where T : unmanaged
        {
            Write(va, MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        /// <inheritdoc/>
        public void Write(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            AssertValidAddressAndSize(va, (ulong)data.Length);

            if (IsContiguousAndMapped(va, data.Length))
            {
                data.CopyTo(GetHostSpanContiguous(va, data.Length));
            }
            else
            {
                int offset = 0, size;

                if ((va & PageMask) != 0)
                {
                    size = Math.Min(data.Length, PageSize - (int)(va & PageMask));

                    data[..size].CopyTo(GetHostSpanContiguous(va, size));

                    offset += size;
                }

                for (; offset < data.Length; offset += size)
                {
                    size = Math.Min(data.Length - offset, PageSize);

                    data.Slice(offset, size).CopyTo(GetHostSpanContiguous(va + (ulong)offset, size));
                }
            }
        }

        /// <inheritdoc/>
        public bool WriteWithRedundancyCheck(ulong va, ReadOnlySpan<byte> data)
        {
            Write(va, data);

            return true;
        }

        /// <inheritdoc/>
        public ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            if (IsContiguousAndMapped(va, size))
            {
                return GetHostSpanContiguous(va, size);
            }
            else
            {
                Span<byte> data = new byte[size];

                Read(va, data);

                return data;
            }
        }

        /// <inheritdoc/>
        public unsafe WritableRegion GetWritableRegion(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return new WritableRegion(null, va, Memory<byte>.Empty);
            }

            if (IsContiguousAndMapped(va, size))
            {
                return new WritableRegion(null, va, new NativeMemoryManager<byte>((byte*)GetHostAddress(va), size).Memory);
            }
            else
            {
                Memory<byte> memory = new byte[size];

                GetSpan(va, size).CopyTo(memory.Span);

                return new WritableRegion(this, va, memory);
            }
        }

        /// <inheritdoc/>
        public unsafe ref T GetRef<T>(ulong va) where T : unmanaged
        {
            if (!IsContiguous(va, Unsafe.SizeOf<T>()))
            {
                ThrowMemoryNotContiguous();
            }

            return ref *(T*)GetHostAddress(va);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPagesCount(ulong va, uint size, out ulong startVa)
        {
            // WARNING: Always check if ulong does not overflow during the operations.
            startVa = va & ~(ulong)PageMask;
            ulong vaSpan = (va - startVa + size + PageMask) & ~(ulong)PageMask;

            return (int)(vaSpan / PageSize);
        }

        private static void ThrowMemoryNotContiguous() => throw new MemoryNotContiguousException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsContiguousAndMapped(ulong va, int size) => IsContiguous(va, size) && IsMapped(va);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsContiguous(ulong va, int size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, (ulong)size))
            {
                return false;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return false;
                }

                if (GetHostAddress(va) + PageSize != GetHostAddress(va + PageSize))
                {
                    return false;
                }

                va += PageSize;
            }

            return true;
        }

        /// <inheritdoc/>
        public IEnumerable<HostMemoryRange> GetHostRegions(ulong va, ulong size)
        {
            if (size == 0)
            {
                return Enumerable.Empty<HostMemoryRange>();
            }

            return GetHostRegionsImpl(va, size);
        }

        /// <inheritdoc/>
        public IEnumerable<MemoryRange> GetPhysicalRegions(ulong va, ulong size)
        {
            if (size == 0)
            {
                return Enumerable.Empty<MemoryRange>();
            }

            var hostRegions = GetHostRegionsImpl(va, size);
            if (hostRegions == null)
            {
                return null;
            }

            var regions = new MemoryRange[hostRegions.Count];

            ulong backingStart = (ulong)_backingMemory.Pointer;
            ulong backingEnd = backingStart + _backingMemory.Size;

            int count = 0;

            for (int i = 0; i < regions.Length; i++)
            {
                var hostRegion = hostRegions[i];

                if ((ulong)hostRegion.Address >= backingStart && (ulong)hostRegion.Address < backingEnd)
                {
                    regions[count++] = new MemoryRange((ulong)hostRegion.Address - backingStart, hostRegion.Size);
                }
            }

            if (count != regions.Length)
            {
                return new ArraySegment<MemoryRange>(regions, 0, count);
            }

            return regions;
        }

        private List<HostMemoryRange> GetHostRegionsImpl(ulong va, ulong size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, size))
            {
                return null;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            var regions = new List<HostMemoryRange>();

            nuint regionStart = GetHostAddress(va);
            ulong regionSize = PageSize;

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return null;
                }

                nuint newHostAddress = GetHostAddress(va + PageSize);

                if (GetHostAddress(va) + PageSize != newHostAddress)
                {
                    regions.Add(new HostMemoryRange(regionStart, regionSize));
                    regionStart = newHostAddress;
                    regionSize = 0;
                }

                va += PageSize;
                regionSize += PageSize;
            }

            regions.Add(new HostMemoryRange(regionStart, regionSize));

            return regions;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMapped(ulong va)
        {
            if (!ValidateAddress(va))
            {
                return false;
            }

            return _pageTable.Read(va) != 0;
        }

        /// <inheritdoc/>
        public bool IsRangeMapped(ulong va, ulong size)
        {
            if (size == 0UL)
            {
                return true;
            }

            if (!ValidateAddressAndSize(va, size))
            {
                return false;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            for (int page = 0; page < pages; page++)
            {
                if (!IsMapped(va))
                {
                    return false;
                }

                va += PageSize;
            }

            return true;
        }

        private unsafe Span<byte> GetHostSpanContiguous(ulong va, int size)
        {
            return new Span<byte>((void*)GetHostAddress(va), size);
        }

        private nuint GetHostAddress(ulong va)
        {
            return _pageTable.Read(va) + (nuint)(va & PageMask);
        }

        /// <inheritdoc/>
        public void Reprotect(ulong va, ulong size, MemoryPermission protection)
        {
        }

        /// <inheritdoc/>
        public void TrackingReprotect(ulong va, ulong size, MemoryPermission protection, bool guest = false)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null)
        {
            // Only the ARM Memory Manager has tracking for now.
        }

        protected override unsafe Span<byte> GetPhysicalAddressSpan(nuint pa, int size)
            => new((void*)pa, size);

        protected override nuint TranslateVirtualAddressForRead(ulong va)
            => GetHostAddress(va);
    }
}
