using ARMeilleure.Memory;
using Ryujinx.Memory;
using Ryujinx.Memory.Range;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Cpu.AppleHv
{
    /// <summary>
    /// Represents a CPU memory manager which maps guest virtual memory directly onto the Hypervisor page table.
    /// </summary>
    [SupportedOSPlatform("macos")]
    public class HvMemoryManager : VirtualMemoryManagerRefCountedBase<ulong, ulong>, IMemoryManager, IVirtualMemoryManagerTracked, IWritableBlock
    {
        private readonly InvalidAccessHandler _invalidAccessHandler;

        private readonly HvAddressSpace _addressSpace;

        internal HvAddressSpace AddressSpace => _addressSpace;

        private readonly MemoryBlock _backingMemory;
        private readonly PageTable<ulong> _pageTable;

        private readonly ManagedPageFlags _pages;

        public bool Supports4KBPages => true;

        public int AddressSpaceBits { get; }

        public IntPtr PageTablePointer => IntPtr.Zero;

        public MemoryManagerType Type => MemoryManagerType.SoftwarePageTable;

        public MemoryTracking Tracking { get; }

        public event Action<ulong, ulong> UnmapEvent;

        protected override ulong AddressSpaceSize { get; }

        /// <summary>
        /// Creates a new instance of the Hypervisor memory manager.
        /// </summary>
        /// <param name="backingMemory">Physical backing memory where virtual memory will be mapped to</param>
        /// <param name="addressSpaceSize">Size of the address space</param>
        /// <param name="invalidAccessHandler">Optional function to handle invalid memory accesses</param>
        public HvMemoryManager(MemoryBlock backingMemory, ulong addressSpaceSize, InvalidAccessHandler invalidAccessHandler = null)
        {
            _backingMemory = backingMemory;
            _pageTable = new PageTable<ulong>();
            _invalidAccessHandler = invalidAccessHandler;
            AddressSpaceSize = addressSpaceSize;

            ulong asSize = PageSize;
            int asBits = PageBits;

            while (asSize < addressSpaceSize)
            {
                asSize <<= 1;
                asBits++;
            }

            _addressSpace = new HvAddressSpace(backingMemory, asSize);

            AddressSpaceBits = asBits;

            _pages = new ManagedPageFlags(AddressSpaceBits);
            Tracking = new MemoryTracking(this, PageSize, invalidAccessHandler);
        }

        /// <inheritdoc/>
        public void Map(ulong va, ulong pa, ulong size, MemoryMapFlags flags)
        {
            AssertValidAddressAndSize(va, size);

            PtMap(va, pa, size);
            _addressSpace.MapUser(va, pa, size, MemoryPermission.ReadWriteExecute);
            _pages.AddMapping(va, size);

            Tracking.Map(va, size);
        }

        private void PtMap(ulong va, ulong pa, ulong size)
        {
            while (size != 0)
            {
                _pageTable.Map(va, pa);

                va += PageSize;
                pa += PageSize;
                size -= PageSize;
            }
        }

        /// <inheritdoc/>
        public void MapForeign(ulong va, nuint hostPointer, ulong size)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void Unmap(ulong va, ulong size)
        {
            AssertValidAddressAndSize(va, size);

            UnmapEvent?.Invoke(va, size);
            Tracking.Unmap(va, size);

            _pages.RemoveMapping(va, size);
            _addressSpace.UnmapUser(va, size);
            PtUnmap(va, size);
        }

        private void PtUnmap(ulong va, ulong size)
        {
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
        public T ReadTracked<T>(ulong va) where T : unmanaged
        {
            try
            {
                SignalMemoryTracking(va, (ulong)Unsafe.SizeOf<T>(), false);

                return Read<T>(va);
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }

                return default;
            }
        }

        /// <inheritdoc/>
        public override void Read(ulong va, Span<byte> data)
        {
            try
            {
                base.Read(va, data);
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }
            }
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

            SignalMemoryTracking(va, (ulong)data.Length, true);

            WriteImpl(va, data);
        }

        /// <inheritdoc/>
        public void WriteUntracked(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            WriteImpl(va, data);
        }

        /// <inheritdoc/>
        public bool WriteWithRedundancyCheck(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return false;
            }

            SignalMemoryTracking(va, (ulong)data.Length, false);

            if (IsContiguousAndMapped(va, data.Length))
            {
                var target = _backingMemory.GetSpan(GetPhysicalAddressInternal(va), data.Length);

                bool changed = !data.SequenceEqual(target);

                if (changed)
                {
                    data.CopyTo(target);
                }

                return changed;
            }
            else
            {
                WriteImpl(va, data);

                return true;
            }
        }

        private void WriteImpl(ulong va, ReadOnlySpan<byte> data)
        {
            try
            {
                AssertValidAddressAndSize(va, (ulong)data.Length);

                if (IsContiguousAndMapped(va, data.Length))
                {
                    data.CopyTo(_backingMemory.GetSpan(GetPhysicalAddressInternal(va), data.Length));
                }
                else
                {
                    int offset = 0, size;

                    if ((va & PageMask) != 0)
                    {
                        ulong pa = GetPhysicalAddressChecked(va);

                        size = Math.Min(data.Length, PageSize - (int)(va & PageMask));

                        data[..size].CopyTo(_backingMemory.GetSpan(pa, size));

                        offset += size;
                    }

                    for (; offset < data.Length; offset += size)
                    {
                        ulong pa = GetPhysicalAddressChecked(va + (ulong)offset);

                        size = Math.Min(data.Length - offset, PageSize);

                        data.Slice(offset, size).CopyTo(_backingMemory.GetSpan(pa, size));
                    }
                }
            }
            catch (InvalidMemoryRegionException)
            {
                if (_invalidAccessHandler == null || !_invalidAccessHandler(va))
                {
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, false);
            }

            if (IsContiguousAndMapped(va, size))
            {
                return _backingMemory.GetSpan(GetPhysicalAddressInternal(va), size);
            }
            else
            {
                Span<byte> data = new byte[size];

                base.Read(va, data);

                return data;
            }
        }

        /// <inheritdoc/>
        public WritableRegion GetWritableRegion(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return new WritableRegion(null, va, Memory<byte>.Empty);
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, true);
            }

            if (IsContiguousAndMapped(va, size))
            {
                return new WritableRegion(null, va, _backingMemory.GetMemory(GetPhysicalAddressInternal(va), size));
            }
            else
            {
                Memory<byte> memory = new byte[size];

                base.Read(va, memory.Span);

                return new WritableRegion(this, va, memory);
            }
        }

        /// <inheritdoc/>
        public ref T GetRef<T>(ulong va) where T : unmanaged
        {
            if (!IsContiguous(va, Unsafe.SizeOf<T>()))
            {
                ThrowMemoryNotContiguous();
            }

            SignalMemoryTracking(va, (ulong)Unsafe.SizeOf<T>(), true);

            return ref _backingMemory.GetRef<T>(GetPhysicalAddressChecked(va));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMapped(ulong va)
        {
            return ValidateAddress(va) && _pages.IsMapped(va);
        }

        /// <inheritdoc/>
        public bool IsRangeMapped(ulong va, ulong size)
        {
            AssertValidAddressAndSize(va, size);

            return _pages.IsRangeMapped(va, size);
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

                if (GetPhysicalAddressInternal(va) + PageSize != GetPhysicalAddressInternal(va + PageSize))
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

            var guestRegions = GetPhysicalRegionsImpl(va, size);
            if (guestRegions == null)
            {
                return null;
            }

            var regions = new HostMemoryRange[guestRegions.Count];

            for (int i = 0; i < regions.Length; i++)
            {
                var guestRegion = guestRegions[i];
                IntPtr pointer = _backingMemory.GetPointer(guestRegion.Address, guestRegion.Size);
                regions[i] = new HostMemoryRange((nuint)(ulong)pointer, guestRegion.Size);
            }

            return regions;
        }

        /// <inheritdoc/>
        public IEnumerable<MemoryRange> GetPhysicalRegions(ulong va, ulong size)
        {
            if (size == 0)
            {
                return Enumerable.Empty<MemoryRange>();
            }

            return GetPhysicalRegionsImpl(va, size);
        }

        private List<MemoryRange> GetPhysicalRegionsImpl(ulong va, ulong size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, size))
            {
                return null;
            }

            int pages = GetPagesCount(va, (uint)size, out va);

            var regions = new List<MemoryRange>();

            ulong regionStart = GetPhysicalAddressInternal(va);
            ulong regionSize = PageSize;

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return null;
                }

                ulong newPa = GetPhysicalAddressInternal(va + PageSize);

                if (GetPhysicalAddressInternal(va) + PageSize != newPa)
                {
                    regions.Add(new MemoryRange(regionStart, regionSize));
                    regionStart = newPa;
                    regionSize = 0;
                }

                va += PageSize;
                regionSize += PageSize;
            }

            regions.Add(new MemoryRange(regionStart, regionSize));

            return regions;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This function also validates that the given range is both valid and mapped, and will throw if it is not.
        /// </remarks>
        public void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null)
        {
            AssertValidAddressAndSize(va, size);

            if (precise)
            {
                Tracking.VirtualMemoryEvent(va, size, write, precise: true, exemptId);
                return;
            }

            _pages.SignalMemoryTracking(Tracking, va, size, write, exemptId);
        }

        /// <summary>
        /// Computes the number of pages in a virtual address range.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <param name="startVa">The virtual address of the beginning of the first page</param>
        /// <remarks>This function does not differentiate between allocated and unallocated pages.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPagesCount(ulong va, ulong size, out ulong startVa)
        {
            // WARNING: Always check if ulong does not overflow during the operations.
            startVa = va & ~(ulong)PageMask;
            ulong vaSpan = (va - startVa + size + PageMask) & ~(ulong)PageMask;

            return (int)(vaSpan / PageSize);
        }

        /// <inheritdoc/>
        public void Reprotect(ulong va, ulong size, MemoryPermission protection)
        {
            // TODO
        }

        /// <inheritdoc/>
        public void TrackingReprotect(ulong va, ulong size, MemoryPermission protection, bool guest)
        {
            if (guest)
            {
                _addressSpace.ReprotectUser(va, size, protection);
            }
            else
            {
                _pages.TrackingReprotect(va, size, protection);
            }
        }

        /// <inheritdoc/>
        public RegionHandle BeginTracking(ulong address, ulong size, int id, RegionFlags flags = RegionFlags.None)
        {
            return Tracking.BeginTracking(address, size, id, flags);
        }

        /// <inheritdoc/>
        public MultiRegionHandle BeginGranularTracking(ulong address, ulong size, IEnumerable<IRegionHandle> handles, ulong granularity, int id, RegionFlags flags = RegionFlags.None)
        {
            return Tracking.BeginGranularTracking(address, size, handles, granularity, id, flags);
        }

        /// <inheritdoc/>
        public SmartMultiRegionHandle BeginSmartGranularTracking(ulong address, ulong size, ulong granularity, int id)
        {
            return Tracking.BeginSmartGranularTracking(address, size, granularity, id);
        }

        private ulong GetPhysicalAddressChecked(ulong va)
        {
            if (!IsMapped(va))
            {
                ThrowInvalidMemoryRegionException($"Not mapped: va=0x{va:X16}");
            }

            return GetPhysicalAddressInternal(va);
        }

        private ulong GetPhysicalAddressInternal(ulong va)
        {
            return _pageTable.Read(va) + (va & PageMask);
        }

        /// <summary>
        /// Disposes of resources used by the memory manager.
        /// </summary>
        protected override void Destroy()
        {
            _addressSpace.Dispose();
        }

        protected override Span<byte> GetPhysicalAddressSpan(ulong pa, int size)
            => _backingMemory.GetSpan(pa, size);

        protected override ulong TranslateVirtualAddressForRead(ulong va)
            => GetPhysicalAddressChecked(va);
    }
}
