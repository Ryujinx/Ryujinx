using Ryujinx.Memory;
using Ryujinx.Memory.Tracking;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ryujinx.Cpu
{
    /// <summary>
    /// A page bitmap that keeps track of mapped state and tracking protection
    /// for managed memory accesses (not using host page protection).
    /// </summary>
    internal readonly struct ManagedPageFlags
    {
        public const int PageBits = 12;
        public const int PageSize = 1 << PageBits;
        public const int PageMask = PageSize - 1;

        private readonly ulong[] _pageBitmap;

        public const int PageToPteShift = 5; // 32 pages (2 bits each) in one ulong page table entry.
        public const ulong BlockMappedMask = 0x5555555555555555; // First bit of each table entry set.

        private enum ManagedPtBits : ulong
        {
            Unmapped = 0,
            Mapped,
            WriteTracked,
            ReadWriteTracked,

            MappedReplicated = 0x5555555555555555,
            WriteTrackedReplicated = 0xaaaaaaaaaaaaaaaa,
            ReadWriteTrackedReplicated = ulong.MaxValue,
        }

        public ManagedPageFlags(int addressSpaceBits)
        {
            int bits = Math.Max(0, addressSpaceBits - (PageBits + PageToPteShift));
            _pageBitmap = new ulong[1 << bits];
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

        /// <summary>
        /// Checks if the page at a given CPU virtual address is mapped.
        /// </summary>
        /// <param name="va">Virtual address to check</param>
        /// <returns>True if the address is mapped, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsMapped(ulong va)
        {
            ulong page = va >> PageBits;

            int bit = (int)((page & 31) << 1);

            int pageIndex = (int)(page >> PageToPteShift);
            ref ulong pageRef = ref _pageBitmap[pageIndex];

            ulong pte = Volatile.Read(ref pageRef);

            return ((pte >> bit) & 3) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetPageBlockRange(ulong pageStart, ulong pageEnd, out ulong startMask, out ulong endMask, out int pageIndex, out int pageEndIndex)
        {
            startMask = ulong.MaxValue << ((int)(pageStart & 31) << 1);
            endMask = ulong.MaxValue >> (64 - ((int)(pageEnd & 31) << 1));

            pageIndex = (int)(pageStart >> PageToPteShift);
            pageEndIndex = (int)((pageEnd - 1) >> PageToPteShift);
        }

        /// <summary>
        /// Checks if a memory range is mapped.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <returns>True if the entire range is mapped, false otherwise</returns>
        public readonly bool IsRangeMapped(ulong va, ulong size)
        {
            int pages = GetPagesCount(va, size, out _);

            if (pages == 1)
            {
                return IsMapped(va);
            }

            ulong pageStart = va >> PageBits;
            ulong pageEnd = pageStart + (ulong)pages;

            GetPageBlockRange(pageStart, pageEnd, out ulong startMask, out ulong endMask, out int pageIndex, out int pageEndIndex);

            // Check if either bit in each 2 bit page entry is set.
            // OR the block with itself shifted down by 1, and check the first bit of each entry.

            ulong mask = BlockMappedMask & startMask;

            while (pageIndex <= pageEndIndex)
            {
                if (pageIndex == pageEndIndex)
                {
                    mask &= endMask;
                }

                ref ulong pageRef = ref _pageBitmap[pageIndex++];
                ulong pte = Volatile.Read(ref pageRef);

                pte |= pte >> 1;
                if ((pte & mask) != mask)
                {
                    return false;
                }

                mask = BlockMappedMask;
            }

            return true;
        }

        /// <summary>
        /// Reprotect a region of virtual memory for tracking.
        /// </summary>
        /// <param name="va">Virtual address base</param>
        /// <param name="size">Size of the region to protect</param>
        /// <param name="protection">Memory protection to set</param>
        public readonly void TrackingReprotect(ulong va, ulong size, MemoryPermission protection)
        {
            // Protection is inverted on software pages, since the default value is 0.
            protection = (~protection) & MemoryPermission.ReadAndWrite;

            int pages = GetPagesCount(va, size, out va);
            ulong pageStart = va >> PageBits;

            if (pages == 1)
            {
                ulong protTag = protection switch
                {
                    MemoryPermission.None => (ulong)ManagedPtBits.Mapped,
                    MemoryPermission.Write => (ulong)ManagedPtBits.WriteTracked,
                    _ => (ulong)ManagedPtBits.ReadWriteTracked,
                };

                int bit = (int)((pageStart & 31) << 1);

                ulong tagMask = 3UL << bit;
                ulong invTagMask = ~tagMask;

                ulong tag = protTag << bit;

                int pageIndex = (int)(pageStart >> PageToPteShift);
                ref ulong pageRef = ref _pageBitmap[pageIndex];

                ulong pte;

                do
                {
                    pte = Volatile.Read(ref pageRef);
                }
                while ((pte & tagMask) != 0 && Interlocked.CompareExchange(ref pageRef, (pte & invTagMask) | tag, pte) != pte);
            }
            else
            {
                ulong pageEnd = pageStart + (ulong)pages;

                GetPageBlockRange(pageStart, pageEnd, out ulong startMask, out ulong endMask, out int pageIndex, out int pageEndIndex);

                ulong mask = startMask;

                ulong protTag = protection switch
                {
                    MemoryPermission.None => (ulong)ManagedPtBits.MappedReplicated,
                    MemoryPermission.Write => (ulong)ManagedPtBits.WriteTrackedReplicated,
                    _ => (ulong)ManagedPtBits.ReadWriteTrackedReplicated,
                };

                while (pageIndex <= pageEndIndex)
                {
                    if (pageIndex == pageEndIndex)
                    {
                        mask &= endMask;
                    }

                    ref ulong pageRef = ref _pageBitmap[pageIndex++];

                    ulong pte;
                    ulong mappedMask;

                    // Change the protection of all 2 bit entries that are mapped.
                    do
                    {
                        pte = Volatile.Read(ref pageRef);

                        mappedMask = pte | (pte >> 1);
                        mappedMask |= (mappedMask & BlockMappedMask) << 1;
                        mappedMask &= mask; // Only update mapped pages within the given range.
                    }
                    while (Interlocked.CompareExchange(ref pageRef, (pte & (~mappedMask)) | (protTag & mappedMask), pte) != pte);

                    mask = ulong.MaxValue;
                }
            }
        }

        /// <summary>
        /// Alerts the memory tracking that a given region has been read from or written to.
        /// This should be called before read/write is performed.
        /// </summary>
        /// <param name="tracking">Memory tracking structure to call when pages are protected</param>
        /// <param name="va">Virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="write">True if the region was written, false if read</param>
        /// <param name="exemptId">Optional ID of the handles that should not be signalled</param>
        /// <remarks>
        /// This function also validates that the given range is both valid and mapped, and will throw if it is not.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void SignalMemoryTracking(MemoryTracking tracking, ulong va, ulong size, bool write, int? exemptId = null)
        {
            // Software table, used for managed memory tracking.

            int pages = GetPagesCount(va, size, out _);
            ulong pageStart = va >> PageBits;

            if (pages == 1)
            {
                ulong tag = (ulong)(write ? ManagedPtBits.WriteTracked : ManagedPtBits.ReadWriteTracked);

                int bit = (int)((pageStart & 31) << 1);

                int pageIndex = (int)(pageStart >> PageToPteShift);
                ref ulong pageRef = ref _pageBitmap[pageIndex];

                ulong pte = Volatile.Read(ref pageRef);
                ulong state = ((pte >> bit) & 3);

                if (state >= tag)
                {
                    tracking.VirtualMemoryEvent(va, size, write, precise: false, exemptId);
                    return;
                }
                else if (state == 0)
                {
                    ThrowInvalidMemoryRegionException($"Not mapped: va=0x{va:X16}, size=0x{size:X16}");
                }
            }
            else
            {
                ulong pageEnd = pageStart + (ulong)pages;

                GetPageBlockRange(pageStart, pageEnd, out ulong startMask, out ulong endMask, out int pageIndex, out int pageEndIndex);

                ulong mask = startMask;

                ulong anyTrackingTag = (ulong)ManagedPtBits.WriteTrackedReplicated;

                while (pageIndex <= pageEndIndex)
                {
                    if (pageIndex == pageEndIndex)
                    {
                        mask &= endMask;
                    }

                    ref ulong pageRef = ref _pageBitmap[pageIndex++];

                    ulong pte = Volatile.Read(ref pageRef);
                    ulong mappedMask = mask & BlockMappedMask;

                    ulong mappedPte = pte | (pte >> 1);
                    if ((mappedPte & mappedMask) != mappedMask)
                    {
                        ThrowInvalidMemoryRegionException($"Not mapped: va=0x{va:X16}, size=0x{size:X16}");
                    }

                    pte &= mask;
                    if ((pte & anyTrackingTag) != 0) // Search for any tracking.
                    {
                        // Writes trigger any tracking.
                        // Only trigger tracking from reads if both bits are set on any page.
                        if (write || (pte & (pte >> 1) & BlockMappedMask) != 0)
                        {
                            tracking.VirtualMemoryEvent(va, size, write, precise: false, exemptId);
                            break;
                        }
                    }

                    mask = ulong.MaxValue;
                }
            }
        }

        /// <summary>
        /// Adds the given address mapping to the page table.
        /// </summary>
        /// <param name="va">Virtual memory address</param>
        /// <param name="size">Size to be mapped</param>
        public readonly void AddMapping(ulong va, ulong size)
        {
            int pages = GetPagesCount(va, size, out _);
            ulong pageStart = va >> PageBits;
            ulong pageEnd = pageStart + (ulong)pages;

            GetPageBlockRange(pageStart, pageEnd, out ulong startMask, out ulong endMask, out int pageIndex, out int pageEndIndex);

            ulong mask = startMask;

            while (pageIndex <= pageEndIndex)
            {
                if (pageIndex == pageEndIndex)
                {
                    mask &= endMask;
                }

                ref ulong pageRef = ref _pageBitmap[pageIndex++];

                ulong pte;
                ulong mappedMask;

                // Map all 2-bit entries that are unmapped.
                do
                {
                    pte = Volatile.Read(ref pageRef);

                    mappedMask = pte | (pte >> 1);
                    mappedMask |= (mappedMask & BlockMappedMask) << 1;
                    mappedMask |= ~mask; // Treat everything outside the range as mapped, thus unchanged.
                }
                while (Interlocked.CompareExchange(ref pageRef, (pte & mappedMask) | (BlockMappedMask & (~mappedMask)), pte) != pte);

                mask = ulong.MaxValue;
            }
        }

        /// <summary>
        /// Removes the given address mapping from the page table.
        /// </summary>
        /// <param name="va">Virtual memory address</param>
        /// <param name="size">Size to be unmapped</param>
        public readonly void RemoveMapping(ulong va, ulong size)
        {
            int pages = GetPagesCount(va, size, out _);
            ulong pageStart = va >> PageBits;
            ulong pageEnd = pageStart + (ulong)pages;

            GetPageBlockRange(pageStart, pageEnd, out ulong startMask, out ulong endMask, out int pageIndex, out int pageEndIndex);

            startMask = ~startMask;
            endMask = ~endMask;

            ulong mask = startMask;

            while (pageIndex <= pageEndIndex)
            {
                if (pageIndex == pageEndIndex)
                {
                    mask |= endMask;
                }

                ref ulong pageRef = ref _pageBitmap[pageIndex++];
                ulong pte;

                do
                {
                    pte = Volatile.Read(ref pageRef);
                }
                while (Interlocked.CompareExchange(ref pageRef, pte & mask, pte) != pte);

                mask = 0;
            }
        }

        private static void ThrowInvalidMemoryRegionException(string message) => throw new InvalidMemoryRegionException(message);
    }
}
