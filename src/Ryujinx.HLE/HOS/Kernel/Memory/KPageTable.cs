using Ryujinx.Horizon.Common;
using Ryujinx.Memory;
using Ryujinx.Memory.Range;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ryujinx.HLE.HOS.Kernel.Memory
{
    class KPageTable : KPageTableBase
    {
        private readonly IVirtualMemoryManager _cpuMemory;

        protected override bool Supports4KBPages => _cpuMemory.Supports4KBPages;

        public KPageTable(KernelContext context, IVirtualMemoryManager cpuMemory, ulong reservedAddressSpaceSize) : base(context, reservedAddressSpaceSize)
        {
            _cpuMemory = cpuMemory;
        }

        /// <inheritdoc/>
        protected override IEnumerable<HostMemoryRange> GetHostRegions(ulong va, ulong size)
        {
            return _cpuMemory.GetHostRegions(va, size);
        }

        /// <inheritdoc/>
        protected override void GetPhysicalRegions(ulong va, ulong size, KPageList pageList)
        {
            var ranges = _cpuMemory.GetPhysicalRegions(va, size);
            foreach (var range in ranges)
            {
                pageList.AddRange(range.Address + DramMemoryMap.DramBase, range.Size / PageSize);
            }
        }

        /// <inheritdoc/>
        protected override ReadOnlySpan<byte> GetSpan(ulong va, int size)
        {
            return _cpuMemory.GetSpan(va, size);
        }

        /// <inheritdoc/>
        protected override Result MapMemory(ulong src, ulong dst, ulong pagesCount, KMemoryPermission oldSrcPermission, KMemoryPermission newDstPermission)
        {
            KPageList pageList = new();
            GetPhysicalRegions(src, pagesCount * PageSize, pageList);

            Result result = Reprotect(src, pagesCount, KMemoryPermission.None);

            if (result != Result.Success)
            {
                return result;
            }

            result = MapPages(dst, pageList, newDstPermission, MemoryMapFlags.Private, false, 0);

            if (result != Result.Success)
            {
                Result reprotectResult = Reprotect(src, pagesCount, oldSrcPermission);
                Debug.Assert(reprotectResult == Result.Success);
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Result UnmapMemory(ulong dst, ulong src, ulong pagesCount, KMemoryPermission oldDstPermission, KMemoryPermission newSrcPermission)
        {
            ulong size = pagesCount * PageSize;

            KPageList srcPageList = new();
            KPageList dstPageList = new();

            GetPhysicalRegions(src, size, srcPageList);
            GetPhysicalRegions(dst, size, dstPageList);

            if (!dstPageList.IsEqual(srcPageList))
            {
                return KernelResult.InvalidMemRange;
            }

            Result result = Unmap(dst, pagesCount);

            if (result != Result.Success)
            {
                return result;
            }

            result = Reprotect(src, pagesCount, newSrcPermission);

            if (result != Result.Success)
            {
                Result mapResult = MapPages(dst, dstPageList, oldDstPermission, MemoryMapFlags.Private, false, 0);
                Debug.Assert(mapResult == Result.Success);
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Result MapPages(
            ulong dstVa,
            ulong pagesCount,
            ulong srcPa,
            KMemoryPermission permission,
            MemoryMapFlags flags,
            bool shouldFillPages,
            byte fillValue)
        {
            ulong size = pagesCount * PageSize;

            Context.CommitMemory(srcPa - DramMemoryMap.DramBase, size);

            _cpuMemory.Map(dstVa, srcPa - DramMemoryMap.DramBase, size, flags);

            if (DramMemoryMap.IsHeapPhysicalAddress(srcPa))
            {
                Context.MemoryManager.IncrementPagesReferenceCount(srcPa, pagesCount);
            }

            if (shouldFillPages)
            {
                _cpuMemory.Fill(dstVa, size, fillValue);
            }

            return Result.Success;
        }

        /// <inheritdoc/>
        protected override Result MapPages(
            ulong address,
            KPageList pageList,
            KMemoryPermission permission,
            MemoryMapFlags flags,
            bool shouldFillPages,
            byte fillValue)
        {
            using var scopedPageList = new KScopedPageList(Context.MemoryManager, pageList);

            ulong currentVa = address;

            foreach (var pageNode in pageList)
            {
                ulong addr = pageNode.Address - DramMemoryMap.DramBase;
                ulong size = pageNode.PagesCount * PageSize;

                Context.CommitMemory(addr, size);

                _cpuMemory.Map(currentVa, addr, size, flags);

                if (shouldFillPages)
                {
                    _cpuMemory.Fill(currentVa, size, fillValue);
                }

                currentVa += size;
            }

            scopedPageList.SignalSuccess();

            return Result.Success;
        }

        /// <inheritdoc/>
        protected override Result MapForeign(IEnumerable<HostMemoryRange> regions, ulong va, ulong size)
        {
            ulong offset = 0;

            foreach (var region in regions)
            {
                _cpuMemory.MapForeign(va + offset, region.Address, region.Size);

                offset += region.Size;
            }

            return Result.Success;
        }

        /// <inheritdoc/>
        protected override Result Unmap(ulong address, ulong pagesCount, bool clearRejitQueueOnly = false)
        {
            KPageList pagesToClose = new();

            var regions = _cpuMemory.GetPhysicalRegions(address, pagesCount * PageSize);

            foreach (var region in regions)
            {
                ulong pa = region.Address + DramMemoryMap.DramBase;
                if (DramMemoryMap.IsHeapPhysicalAddress(pa))
                {
                    pagesToClose.AddRange(pa, region.Size / PageSize);
                }
            }

            _cpuMemory.Unmap(address, pagesCount * PageSize, clearRejitQueueOnly);

            pagesToClose.DecrementPagesReferenceCount(Context.MemoryManager);

            return Result.Success;
        }

        /// <inheritdoc/>
        protected override Result Reprotect(ulong address, ulong pagesCount, KMemoryPermission permission)
        {
            _cpuMemory.Reprotect(address, pagesCount * PageSize, permission.Convert());

            return Result.Success;
        }

        /// <inheritdoc/>
        protected override Result ReprotectAndFlush(ulong address, ulong pagesCount, KMemoryPermission permission)
        {
            // TODO: Flush JIT cache.

            return Reprotect(address, pagesCount, permission);
        }

        /// <inheritdoc/>
        protected override void SignalMemoryTracking(ulong va, ulong size, bool write)
        {
            _cpuMemory.SignalMemoryTracking(va, size, write);
        }

        /// <inheritdoc/>
        protected override void Write(ulong va, ReadOnlySpan<byte> data)
        {
            _cpuMemory.Write(va, data);
        }
    }
}
