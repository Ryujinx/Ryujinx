using Ryujinx.Common.Pools;
using Ryujinx.Memory.Range;
using System.Collections.Generic;

namespace Ryujinx.Memory.Tracking
{
    /// <summary>
    /// Manages memory tracking for a given virutal/physical memory block.
    /// </summary>
    public class MemoryTracking
    {
        private readonly IVirtualMemoryManager _memoryManager;
        private readonly InvalidAccessHandler _invalidAccessHandler;

        // Only use these from within the lock.
        private readonly NonOverlappingRangeList<VirtualRegion> _virtualRegions;
        // Guest virtual regions are a subset of the normal virtual regions, with potentially different protection
        // and expanded area of effect on platforms that don't support misaligned page protection.
        private readonly NonOverlappingRangeList<VirtualRegion> _guestVirtualRegions;

        private readonly int _pageSize;

        private readonly bool _singleByteGuestTracking;

        /// <summary>
        /// This lock must be obtained when traversing or updating the region-handle hierarchy.
        /// It is not required when reading dirty flags.
        /// </summary>
        internal object TrackingLock = new();

        /// <summary>
        /// Create a new tracking structure for the given "physical" memory block,
        /// with a given "virtual" memory manager that will provide mappings and virtual memory protection.
        /// </summary>
        /// <remarks>
        /// If <paramref name="singleByteGuestTracking" /> is true, the memory manager must also support protection on partially
        /// unmapped regions without throwing exceptions or dropping protection on the mapped portion.
        /// </remarks>
        /// <param name="memoryManager">Virtual memory manager</param>
        /// <param name="pageSize">Page size of the virtual memory space</param>
        /// <param name="invalidAccessHandler">Method to call for invalid memory accesses</param>
        /// <param name="singleByteGuestTracking">True if the guest only signals writes for the first byte</param>
        public MemoryTracking(
            IVirtualMemoryManager memoryManager,
            int pageSize,
            InvalidAccessHandler invalidAccessHandler = null,
            bool singleByteGuestTracking = false)
        {
            _memoryManager = memoryManager;
            _pageSize = pageSize;
            _invalidAccessHandler = invalidAccessHandler;
            _singleByteGuestTracking = singleByteGuestTracking;

            _virtualRegions = new NonOverlappingRangeList<VirtualRegion>();
            _guestVirtualRegions = new NonOverlappingRangeList<VirtualRegion>();
        }

        private (ulong address, ulong size) PageAlign(ulong address, ulong size)
        {
            ulong pageMask = (ulong)_pageSize - 1;
            ulong rA = address & ~pageMask;
            ulong rS = ((address + size + pageMask) & ~pageMask) - rA;
            return (rA, rS);
        }

        /// <summary>
        /// Indicate that a virtual region has been mapped, and which physical region it has been mapped to.
        /// Should be called after the mapping is complete.
        /// </summary>
        /// <param name="va">Virtual memory address</param>
        /// <param name="size">Size to be mapped</param>
        public void Map(ulong va, ulong size)
        {
            // A mapping may mean we need to re-evaluate each VirtualRegion's affected area.
            // Find all handles that overlap with the range, we need to recalculate their physical regions

            lock (TrackingLock)
            {
                ref var overlaps = ref ThreadStaticArray<VirtualRegion>.Get();

                for (int type = 0; type < 2; type++)
                {
                    NonOverlappingRangeList<VirtualRegion> regions = type == 0 ? _virtualRegions : _guestVirtualRegions;

                    int count = regions.FindOverlapsNonOverlapping(va, size, ref overlaps);

                    for (int i = 0; i < count; i++)
                    {
                        VirtualRegion region = overlaps[i];

                        // If the region has been fully remapped, signal that it has been mapped again.
                        bool remapped = _memoryManager.IsRangeMapped(region.Address, region.Size);
                        if (remapped)
                        {
                            region.SignalMappingChanged(true);
                        }

                        region.UpdateProtection();
                    }
                }
            }
        }

        /// <summary>
        /// Indicate that a virtual region has been unmapped.
        /// Should be called before the unmapping is complete.
        /// </summary>
        /// <param name="va">Virtual memory address</param>
        /// <param name="size">Size to be unmapped</param>
        public void Unmap(ulong va, ulong size)
        {
            // An unmapping may mean we need to re-evaluate each VirtualRegion's affected area.
            // Find all handles that overlap with the range, we need to notify them that the region was unmapped.

            lock (TrackingLock)
            {
                ref var overlaps = ref ThreadStaticArray<VirtualRegion>.Get();

                for (int type = 0; type < 2; type++)
                {
                    NonOverlappingRangeList<VirtualRegion> regions = type == 0 ? _virtualRegions : _guestVirtualRegions;

                    int count = regions.FindOverlapsNonOverlapping(va, size, ref overlaps);

                    for (int i = 0; i < count; i++)
                    {
                        VirtualRegion region = overlaps[i];

                        region.SignalMappingChanged(false);
                    }
                }
            }
        }

        /// <summary>
        /// Alter a tracked memory region to properly capture unaligned accesses.
        /// For most memory manager modes, this does nothing.
        /// </summary>
        /// <param name="address">Original region address</param>
        /// <param name="size">Original region size</param>
        /// <returns>A new address and size for tracking unaligned accesses</returns>
        internal (ulong newAddress, ulong newSize) GetUnalignedSafeRegion(ulong address, ulong size)
        {
            if (_singleByteGuestTracking)
            {
                // The guest only signals the first byte of each memory access with the current memory manager.
                // To catch unaligned access properly, we need to also protect the page before the address.

                // Assume that the address and size are already aligned.

                return (address - (ulong)_pageSize, size + (ulong)_pageSize);
            }
            else
            {
                return (address, size);
            }
        }

        /// <summary>
        /// Get a list of virtual regions that a handle covers.
        /// </summary>
        /// <param name="va">Starting virtual memory address of the handle</param>
        /// <param name="size">Size of the handle's memory region</param>
        /// <param name="guest">True if getting handles for guest protection, false otherwise</param>
        /// <returns>A list of virtual regions within the given range</returns>
        internal List<VirtualRegion> GetVirtualRegionsForHandle(ulong va, ulong size, bool guest)
        {
            List<VirtualRegion> result = new();
            NonOverlappingRangeList<VirtualRegion> regions = guest ? _guestVirtualRegions : _virtualRegions;
            regions.GetOrAddRegions(result, va, size, (va, size) => new VirtualRegion(this, va, size, guest));

            return result;
        }

        /// <summary>
        /// Remove a virtual region from the range list. This assumes that the lock has been acquired.
        /// </summary>
        /// <param name="region">Region to remove</param>
        internal void RemoveVirtual(VirtualRegion region)
        {
            if (region.Guest)
            {
                _guestVirtualRegions.Remove(region);
            }
            else
            {
                _virtualRegions.Remove(region);
            }
        }

        /// <summary>
        /// Obtains a memory tracking handle for the given virtual region, with a specified granularity. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="handles">Handles to inherit state from or reuse. When none are present, provide null</param>
        /// <param name="granularity">Desired granularity of write tracking</param>
        /// <param name="id">Handle ID</param>
        /// <param name="flags">Region flags</param>
        /// <returns>The memory tracking handle</returns>
        public MultiRegionHandle BeginGranularTracking(ulong address, ulong size, IEnumerable<IRegionHandle> handles, ulong granularity, int id, RegionFlags flags = RegionFlags.None)
        {
            return new MultiRegionHandle(this, address, size, handles, granularity, id, flags);
        }

        /// <summary>
        /// Obtains a smart memory tracking handle for the given virtual region, with a specified granularity. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="granularity">Desired granularity of write tracking</param>
        /// <param name="id">Handle ID</param>
        /// <returns>The memory tracking handle</returns>
        public SmartMultiRegionHandle BeginSmartGranularTracking(ulong address, ulong size, ulong granularity, int id)
        {
            (address, size) = PageAlign(address, size);

            return new SmartMultiRegionHandle(this, address, size, granularity, id);
        }

        /// <summary>
        /// Obtains a memory tracking handle for the given virtual region. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="id">Handle ID</param>
        /// <param name="flags">Region flags</param>
        /// <returns>The memory tracking handle</returns>
        public RegionHandle BeginTracking(ulong address, ulong size, int id, RegionFlags flags = RegionFlags.None)
        {
            var (paAddress, paSize) = PageAlign(address, size);

            lock (TrackingLock)
            {
                bool mapped = _memoryManager.IsRangeMapped(address, size);
                RegionHandle handle = new(this, paAddress, paSize, address, size, id, flags, mapped);

                return handle;
            }
        }

        /// <summary>
        /// Obtains a memory tracking handle for the given virtual region. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="bitmap">The bitmap owning the dirty flag for this handle</param>
        /// <param name="bit">The bit of this handle within the dirty flag</param>
        /// <param name="id">Handle ID</param>
        /// <param name="flags">Region flags</param>
        /// <returns>The memory tracking handle</returns>
        internal RegionHandle BeginTrackingBitmap(ulong address, ulong size, ConcurrentBitmap bitmap, int bit, int id, RegionFlags flags = RegionFlags.None)
        {
            var (paAddress, paSize) = PageAlign(address, size);

            lock (TrackingLock)
            {
                bool mapped = _memoryManager.IsRangeMapped(address, size);
                RegionHandle handle = new(this, paAddress, paSize, address, size, bitmap, bit, id, flags, mapped);

                return handle;
            }
        }

        /// <summary>
        /// Signal that a virtual memory event happened at the given location.
        /// The memory event is assumed to be triggered by guest code.
        /// </summary>
        /// <param name="address">Virtual address accessed</param>
        /// <param name="size">Size of the region affected in bytes</param>
        /// <param name="write">Whether the region was written to or read</param>
        /// <returns>True if the event triggered any tracking regions, false otherwise</returns>
        public bool VirtualMemoryEvent(ulong address, ulong size, bool write)
        {
            return VirtualMemoryEvent(address, size, write, precise: false, exemptId: null, guest: true);
        }

        /// <summary>
        /// Signal that a virtual memory event happened at the given location.
        /// This can be flagged as a precise event, which will avoid reprotection and call special handlers if possible.
        /// A precise event has an exact address and size, rather than triggering on page granularity.
        /// </summary>
        /// <param name="address">Virtual address accessed</param>
        /// <param name="size">Size of the region affected in bytes</param>
        /// <param name="write">Whether the region was written to or read</param>
        /// <param name="precise">True if the access is precise, false otherwise</param>
        /// <param name="exemptId">Optional ID that of the handles that should not be signalled</param>
        /// <param name="guest">True if the access is from the guest, false otherwise</param>
        /// <returns>True if the event triggered any tracking regions, false otherwise</returns>
        public bool VirtualMemoryEvent(ulong address, ulong size, bool write, bool precise, int? exemptId = null, bool guest = false)
        {
            // Look up the virtual region using the region list.
            // Signal up the chain to relevant handles.

            bool shouldThrow = false;

            lock (TrackingLock)
            {
                ref var overlaps = ref ThreadStaticArray<VirtualRegion>.Get();

                NonOverlappingRangeList<VirtualRegion> regions = guest ? _guestVirtualRegions : _virtualRegions;

                int count = regions.FindOverlapsNonOverlapping(address, size, ref overlaps);

                if (count == 0 && !precise)
                {
                    if (_memoryManager.IsRangeMapped(address, size))
                    {
                        // TODO: There is currently the possibility that a page can be protected after its virtual region is removed.
                        // This code handles that case when it happens, but it would be better to find out how this happens.
                        _memoryManager.TrackingReprotect(address & ~(ulong)(_pageSize - 1), (ulong)_pageSize, MemoryPermission.ReadAndWrite, guest);
                        return true; // This memory _should_ be mapped, so we need to try again.
                    }
                    else
                    {
                        shouldThrow = true;
                    }
                }
                else
                {
                    if (guest && _singleByteGuestTracking)
                    {
                        // Increase the access size to trigger handles with misaligned accesses.
                        size += (ulong)_pageSize;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        VirtualRegion region = overlaps[i];

                        if (precise)
                        {
                            region.SignalPrecise(address, size, write, exemptId);
                        }
                        else
                        {
                            region.Signal(address, size, write, exemptId);
                        }
                    }
                }
            }

            if (shouldThrow)
            {
                _invalidAccessHandler?.Invoke(address);

                // We can't continue - it's impossible to remove protection from the page.
                // Even if the access handler wants us to continue, we wouldn't be able to.
                throw new InvalidMemoryRegionException();
            }

            return true;
        }

        /// <summary>
        /// Reprotect a given virtual region. The virtual memory manager will handle this.
        /// </summary>
        /// <param name="region">Region to reprotect</param>
        /// <param name="permission">Memory permission to protect with</param>
        /// <param name="guest">True if the protection is for guest access, false otherwise</param>
        internal void ProtectVirtualRegion(VirtualRegion region, MemoryPermission permission, bool guest)
        {
            _memoryManager.TrackingReprotect(region.Address, region.Size, permission, guest);
        }

        /// <summary>
        /// Returns the number of virtual regions currently being tracked.
        /// Useful for tests and metrics.
        /// </summary>
        /// <returns>The number of virtual regions</returns>
        public int GetRegionCount()
        {
            lock (TrackingLock)
            {
                return _virtualRegions.Count;
            }
        }
    }
}
