using Ryujinx.Common;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.Horizon.Common;
using Ryujinx.Memory;
using Ryujinx.Memory.Range;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ryujinx.HLE.HOS.Kernel.Memory
{
    abstract class KPageTableBase
    {
        private static readonly int[] _mappingUnitSizes = {
            0x1000,
            0x10000,
            0x200000,
            0x400000,
            0x2000000,
            0x40000000,
        };

        private const ulong RegionAlignment = 0x200000;

        public const int PageSize = 0x1000;

        private const int KMemoryBlockSize = 0x40;

        // We need 2 blocks for the case where a big block
        // needs to be split in 2, plus one block that will be the new one inserted.
        private const int MaxBlocksNeededForInsertion = 2;

        protected readonly KernelContext Context;
        protected virtual bool Supports4KBPages => true;

        public ulong AddrSpaceStart { get; private set; }
        public ulong AddrSpaceEnd { get; private set; }

        public ulong CodeRegionStart { get; private set; }
        public ulong CodeRegionEnd { get; private set; }

        public ulong HeapRegionStart { get; private set; }
        public ulong HeapRegionEnd { get; private set; }

        private ulong _currentHeapAddr;

        public ulong AliasRegionStart { get; private set; }
        public ulong AliasRegionEnd { get; private set; }

        public ulong StackRegionStart { get; private set; }
        public ulong StackRegionEnd { get; private set; }

        public ulong TlsIoRegionStart { get; private set; }
        public ulong TlsIoRegionEnd { get; private set; }

        public ulong AslrRegionStart { get; private set; }
        public ulong AslrRegionEnd { get; private set; }

#pragma warning disable IDE0052 // Remove unread private member
        private ulong _heapCapacity;
#pragma warning restore IDE0052

        public ulong PhysicalMemoryUsage { get; private set; }

        private readonly KMemoryBlockManager _blockManager;

        private MemoryRegion _memRegion;

        private bool _allocateFromBack;
        private readonly bool _isKernel;

        private bool _aslrEnabled;

        private KMemoryBlockSlabManager _slabManager;

        private int _contextId;

        private MersenneTwister _randomNumberGenerator;

        private readonly MemoryFillValue _heapFillValue;
        private readonly MemoryFillValue _ipcFillValue;

        private readonly ulong _reservedAddressSpaceSize;

        public KPageTableBase(KernelContext context, ulong reservedAddressSpaceSize)
        {
            Context = context;

            _blockManager = new KMemoryBlockManager();

            _isKernel = false;

            _heapFillValue = MemoryFillValue.Zero;
            _ipcFillValue = MemoryFillValue.Zero;

            _reservedAddressSpaceSize = reservedAddressSpaceSize;
        }

        private static readonly int[] _addrSpaceSizes = { 32, 36, 32, 39 };

        public Result InitializeForProcess(
            AddressSpaceType addrSpaceType,
            bool aslrEnabled,
            bool fromBack,
            MemoryRegion memRegion,
            ulong address,
            ulong size,
            KMemoryBlockSlabManager slabManager)
        {
            if ((uint)addrSpaceType > (uint)AddressSpaceType.Addr39Bits)
            {
                throw new ArgumentException($"AddressSpaceType bigger than {(uint)AddressSpaceType.Addr39Bits}: {(uint)addrSpaceType}", nameof(addrSpaceType));
            }

            _contextId = Context.ContextIdManager.GetId();

            ulong addrSpaceBase = 0;
            ulong addrSpaceSize = 1UL << _addrSpaceSizes[(int)addrSpaceType];

            Result result = CreateUserAddressSpace(
                addrSpaceType,
                aslrEnabled,
                fromBack,
                addrSpaceBase,
                addrSpaceSize,
                memRegion,
                address,
                size,
                slabManager);

            if (result != Result.Success)
            {
                Context.ContextIdManager.PutId(_contextId);
            }

            return result;
        }

        private struct Region
        {
            public ulong Start;
            public ulong End;
            public ulong Size;
            public ulong AslrOffset;
        }

        private Result CreateUserAddressSpace(
            AddressSpaceType addrSpaceType,
            bool aslrEnabled,
            bool fromBack,
            ulong addrSpaceStart,
            ulong addrSpaceEnd,
            MemoryRegion memRegion,
            ulong address,
            ulong size,
            KMemoryBlockSlabManager slabManager)
        {
            ulong endAddr = address + size;

            Region aliasRegion = new();
            Region heapRegion = new();
            Region stackRegion = new();
            Region tlsIoRegion = new();

            ulong codeRegionSize;
            ulong stackAndTlsIoStart;
            ulong stackAndTlsIoEnd;

            switch (addrSpaceType)
            {
                case AddressSpaceType.Addr32Bits:
                    aliasRegion.Size = 0x40000000;
                    heapRegion.Size = 0x40000000;
                    stackRegion.Size = 0;
                    tlsIoRegion.Size = 0;
                    CodeRegionStart = 0x200000;
                    codeRegionSize = 0x3fe00000;
                    AslrRegionStart = 0x200000;
                    AslrRegionEnd = AslrRegionStart + 0xffe00000;
                    stackAndTlsIoStart = 0x200000;
                    stackAndTlsIoEnd = 0x40000000;
                    break;

                case AddressSpaceType.Addr36Bits:
                    aliasRegion.Size = 0x180000000;
                    heapRegion.Size = 0x180000000;
                    stackRegion.Size = 0;
                    tlsIoRegion.Size = 0;
                    CodeRegionStart = 0x8000000;
                    codeRegionSize = 0x78000000;
                    AslrRegionStart = 0x8000000;
                    AslrRegionEnd = AslrRegionStart + 0xff8000000;
                    stackAndTlsIoStart = 0x8000000;
                    stackAndTlsIoEnd = 0x80000000;
                    break;

                case AddressSpaceType.Addr32BitsNoMap:
                    aliasRegion.Size = 0;
                    heapRegion.Size = 0x80000000;
                    stackRegion.Size = 0;
                    tlsIoRegion.Size = 0;
                    CodeRegionStart = 0x200000;
                    codeRegionSize = 0x3fe00000;
                    AslrRegionStart = 0x200000;
                    AslrRegionEnd = AslrRegionStart + 0xffe00000;
                    stackAndTlsIoStart = 0x200000;
                    stackAndTlsIoEnd = 0x40000000;
                    break;

                case AddressSpaceType.Addr39Bits:
                    if (_reservedAddressSpaceSize < addrSpaceEnd)
                    {
                        int addressSpaceWidth = (int)ulong.Log2(_reservedAddressSpaceSize);

                        aliasRegion.Size = 1UL << (addressSpaceWidth - 3);
                        heapRegion.Size = 0x180000000;
                        stackRegion.Size = 1UL << (addressSpaceWidth - 8);
                        tlsIoRegion.Size = 1UL << (addressSpaceWidth - 3);
                        CodeRegionStart = BitUtils.AlignDown<ulong>(address, RegionAlignment);
                        codeRegionSize = BitUtils.AlignUp<ulong>(endAddr, RegionAlignment) - CodeRegionStart;
                        stackAndTlsIoStart = 0;
                        stackAndTlsIoEnd = 0;
                        AslrRegionStart = 0x8000000;
                        addrSpaceEnd = 1UL << addressSpaceWidth;
                        AslrRegionEnd = addrSpaceEnd;
                    }
                    else
                    {
                        aliasRegion.Size = 0x1000000000;
                        heapRegion.Size = 0x180000000;
                        stackRegion.Size = 0x80000000;
                        tlsIoRegion.Size = 0x1000000000;
                        CodeRegionStart = BitUtils.AlignDown(address, RegionAlignment);
                        codeRegionSize = BitUtils.AlignUp(endAddr, RegionAlignment) - CodeRegionStart;
                        AslrRegionStart = 0x8000000;
                        AslrRegionEnd = AslrRegionStart + 0x7ff8000000;
                        stackAndTlsIoStart = 0;
                        stackAndTlsIoEnd = 0;
                    }
                    break;
                default:
                    throw new ArgumentException($"AddressSpaceType bigger than {(uint)AddressSpaceType.Addr39Bits}: {(uint)addrSpaceType}", nameof(addrSpaceType));
            }

            CodeRegionEnd = CodeRegionStart + codeRegionSize;

            ulong mapBaseAddress;
            ulong mapAvailableSize;

            if (CodeRegionStart - AslrRegionStart >= addrSpaceEnd - CodeRegionEnd)
            {
                // Has more space before the start of the code region.
                mapBaseAddress = AslrRegionStart;
                mapAvailableSize = CodeRegionStart - AslrRegionStart;
            }
            else
            {
                // Has more space after the end of the code region.
                mapBaseAddress = CodeRegionEnd;
                mapAvailableSize = addrSpaceEnd - CodeRegionEnd;
            }

            ulong mapTotalSize = aliasRegion.Size + heapRegion.Size + stackRegion.Size + tlsIoRegion.Size;

            ulong aslrMaxOffset = mapAvailableSize - mapTotalSize;

            _aslrEnabled = aslrEnabled;

            AddrSpaceStart = addrSpaceStart;
            AddrSpaceEnd = addrSpaceEnd;

            _slabManager = slabManager;

            if (mapAvailableSize < mapTotalSize)
            {
                return KernelResult.OutOfMemory;
            }

            if (aslrEnabled)
            {
                aliasRegion.AslrOffset = GetRandomValue(0, aslrMaxOffset / RegionAlignment) * RegionAlignment;
                heapRegion.AslrOffset = GetRandomValue(0, aslrMaxOffset / RegionAlignment) * RegionAlignment;
                stackRegion.AslrOffset = GetRandomValue(0, aslrMaxOffset / RegionAlignment) * RegionAlignment;
                tlsIoRegion.AslrOffset = GetRandomValue(0, aslrMaxOffset / RegionAlignment) * RegionAlignment;
            }

            // Regions are sorted based on ASLR offset.
            // When ASLR is disabled, the order is Alias, Heap, Stack and TlsIo.
            aliasRegion.Start = mapBaseAddress + aliasRegion.AslrOffset;
            aliasRegion.End = aliasRegion.Start + aliasRegion.Size;
            heapRegion.Start = mapBaseAddress + heapRegion.AslrOffset;
            heapRegion.End = heapRegion.Start + heapRegion.Size;
            stackRegion.Start = mapBaseAddress + stackRegion.AslrOffset;
            stackRegion.End = stackRegion.Start + stackRegion.Size;
            tlsIoRegion.Start = mapBaseAddress + tlsIoRegion.AslrOffset;
            tlsIoRegion.End = tlsIoRegion.Start + tlsIoRegion.Size;

            SortRegion(ref aliasRegion, ref heapRegion, true);

            if (stackRegion.Size != 0)
            {
                stackRegion.Start = mapBaseAddress + stackRegion.AslrOffset;
                stackRegion.End = stackRegion.Start + stackRegion.Size;

                SortRegion(ref aliasRegion, ref stackRegion);
                SortRegion(ref heapRegion, ref stackRegion);
            }
            else
            {
                stackRegion.Start = stackAndTlsIoStart;
                stackRegion.End = stackAndTlsIoEnd;
            }

            if (tlsIoRegion.Size != 0)
            {
                tlsIoRegion.Start = mapBaseAddress + tlsIoRegion.AslrOffset;
                tlsIoRegion.End = tlsIoRegion.Start + tlsIoRegion.Size;

                SortRegion(ref aliasRegion, ref tlsIoRegion);
                SortRegion(ref heapRegion, ref tlsIoRegion);

                if (stackRegion.Size != 0)
                {
                    SortRegion(ref stackRegion, ref tlsIoRegion);
                }
            }
            else
            {
                tlsIoRegion.Start = stackAndTlsIoStart;
                tlsIoRegion.End = stackAndTlsIoEnd;
            }

            AliasRegionStart = aliasRegion.Start;
            AliasRegionEnd = aliasRegion.End;
            HeapRegionStart = heapRegion.Start;
            HeapRegionEnd = heapRegion.End;
            StackRegionStart = stackRegion.Start;
            StackRegionEnd = stackRegion.End;
            TlsIoRegionStart = tlsIoRegion.Start;
            TlsIoRegionEnd = tlsIoRegion.End;

            // TODO: Check kernel configuration via secure monitor call when implemented to set memory fill values.

            _currentHeapAddr = HeapRegionStart;
            _heapCapacity = 0;
            PhysicalMemoryUsage = 0;

            _memRegion = memRegion;
            _allocateFromBack = fromBack;

            return _blockManager.Initialize(addrSpaceStart, addrSpaceEnd, slabManager);
        }

        private static void SortRegion(ref Region lhs, ref Region rhs, bool checkForEquality = false)
        {
            bool res = checkForEquality ? lhs.AslrOffset <= rhs.AslrOffset : lhs.AslrOffset < rhs.AslrOffset;

            if (res)
            {
                rhs.Start += lhs.Size;
                rhs.End += lhs.Size;
            }
            else
            {
                lhs.Start += rhs.Size;
                lhs.End += rhs.Size;
            }
        }

        private ulong GetRandomValue(ulong min, ulong max)
        {
            return (ulong)GetRandomValue((long)min, (long)max);
        }

        private long GetRandomValue(long min, long max)
        {
            _randomNumberGenerator ??= new MersenneTwister(0);

            return _randomNumberGenerator.GenRandomNumber(min, max);
        }

        public Result MapPages(ulong address, KPageList pageList, MemoryState state, KMemoryPermission permission)
        {
            ulong pagesCount = pageList.GetPagesCount();

            ulong size = pagesCount * PageSize;

            if (!CanContain(address, size, state))
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (!IsUnmapped(address, pagesCount * PageSize))
                {
                    return KernelResult.InvalidMemState;
                }

                if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                {
                    return KernelResult.OutOfResource;
                }

                Result result = MapPages(address, pageList, permission, MemoryMapFlags.None);

                if (result == Result.Success)
                {
                    _blockManager.InsertBlock(address, pagesCount, state, permission);
                }

                return result;
            }
        }

        public Result UnmapPages(ulong address, KPageList pageList, MemoryState stateExpected)
        {
            ulong pagesCount = pageList.GetPagesCount();
            ulong size = pagesCount * PageSize;

            ulong endAddr = address + size;

            ulong addrSpacePagesCount = (AddrSpaceEnd - AddrSpaceStart) / PageSize;

            if (AddrSpaceStart > address)
            {
                return KernelResult.InvalidMemState;
            }

            if (addrSpacePagesCount < pagesCount)
            {
                return KernelResult.InvalidMemState;
            }

            if (endAddr - 1 > AddrSpaceEnd - 1)
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                KPageList currentPageList = new();

                GetPhysicalRegions(address, size, currentPageList);

                if (!currentPageList.IsEqual(pageList))
                {
                    return KernelResult.InvalidMemRange;
                }

                if (CheckRange(
                    address,
                    size,
                    MemoryState.Mask,
                    stateExpected,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState state,
                    out _,
                    out _))
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    Result result = Unmap(address, pagesCount);

                    if (result == Result.Success)
                    {
                        _blockManager.InsertBlock(address, pagesCount, MemoryState.Unmapped);
                    }

                    return result;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public static Result MapNormalMemory(long address, long size, KMemoryPermission permission)
        {
            // TODO.
            return Result.Success;
        }

        public static Result MapIoMemory(long address, long size, KMemoryPermission permission)
        {
            // TODO.
            return Result.Success;
        }

        public Result MapPages(
            ulong pagesCount,
            int alignment,
            ulong srcPa,
            bool paIsValid,
            ulong regionStart,
            ulong regionPagesCount,
            MemoryState state,
            KMemoryPermission permission,
            out ulong address)
        {
            address = 0;

            ulong regionSize = regionPagesCount * PageSize;

            if (!CanContain(regionStart, regionSize, state))
            {
                return KernelResult.InvalidMemState;
            }

            if (regionPagesCount <= pagesCount)
            {
                return KernelResult.OutOfMemory;
            }

            lock (_blockManager)
            {
                address = AllocateVa(regionStart, regionPagesCount, pagesCount, alignment);

                if (address == 0)
                {
                    return KernelResult.OutOfMemory;
                }

                if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                {
                    return KernelResult.OutOfResource;
                }

                Result result;

                if (paIsValid)
                {
                    result = MapPages(address, pagesCount, srcPa, permission, MemoryMapFlags.Private);
                }
                else
                {
                    result = AllocateAndMapPages(address, pagesCount, permission);
                }

                if (result != Result.Success)
                {
                    return result;
                }

                _blockManager.InsertBlock(address, pagesCount, state, permission);
            }

            return Result.Success;
        }

        public Result MapPages(ulong address, ulong pagesCount, MemoryState state, KMemoryPermission permission)
        {
            ulong size = pagesCount * PageSize;

            if (!CanContain(address, size, state))
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (!IsUnmapped(address, size))
                {
                    return KernelResult.InvalidMemState;
                }

                if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                {
                    return KernelResult.OutOfResource;
                }

                Result result = AllocateAndMapPages(address, pagesCount, permission);

                if (result == Result.Success)
                {
                    _blockManager.InsertBlock(address, pagesCount, state, permission);
                }

                return result;
            }
        }

        private Result AllocateAndMapPages(ulong address, ulong pagesCount, KMemoryPermission permission)
        {
            KMemoryRegionManager region = GetMemoryRegionManager();

            Result result = region.AllocatePages(out KPageList pageList, pagesCount);

            if (result != Result.Success)
            {
                return result;
            }

            using var _ = new OnScopeExit(() => pageList.DecrementPagesReferenceCount(Context.MemoryManager));

            return MapPages(address, pageList, permission, MemoryMapFlags.Private);
        }

        public Result MapProcessCodeMemory(ulong dst, ulong src, ulong size)
        {
            lock (_blockManager)
            {
                bool success = CheckRange(
                    src,
                    size,
                    MemoryState.Mask,
                    MemoryState.Heap,
                    KMemoryPermission.Mask,
                    KMemoryPermission.ReadAndWrite,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState state,
                    out KMemoryPermission permission,
                    out _);

                success &= IsUnmapped(dst, size);

                if (success)
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion * 2))
                    {
                        return KernelResult.OutOfResource;
                    }

                    ulong pagesCount = size / PageSize;

                    Result result = MapMemory(src, dst, pagesCount, permission, KMemoryPermission.None);

                    _blockManager.InsertBlock(src, pagesCount, state, KMemoryPermission.None, MemoryAttribute.Borrowed);
                    _blockManager.InsertBlock(dst, pagesCount, MemoryState.ModCodeStatic);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result UnmapProcessCodeMemory(ulong dst, ulong src, ulong size, bool clearRejitQueueOnly = false)
        {
            lock (_blockManager)
            {
                bool success = CheckRange(
                    src,
                    size,
                    MemoryState.Mask,
                    MemoryState.Heap,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.Borrowed,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out _,
                    out _,
                    out _);

                success &= CheckRange(
                    dst,
                    PageSize,
                    MemoryState.UnmapProcessCodeMemoryAllowed,
                    MemoryState.UnmapProcessCodeMemoryAllowed,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask & ~MemoryAttribute.PermissionLocked,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState state,
                    out _,
                    out _);

                success &= CheckRange(
                    dst,
                    size,
                    MemoryState.Mask,
                    state,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask & ~MemoryAttribute.PermissionLocked,
                    MemoryAttribute.None);

                if (success)
                {
                    ulong pagesCount = size / PageSize;

                    Result result = Unmap(dst, pagesCount, clearRejitQueueOnly);

                    if (result != Result.Success)
                    {
                        return result;
                    }

                    // TODO: Missing some checks here.

                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion * 2))
                    {
                        return KernelResult.OutOfResource;
                    }

                    _blockManager.InsertBlock(dst, pagesCount, MemoryState.Unmapped);
                    _blockManager.InsertBlock(src, pagesCount, MemoryState.Heap, KMemoryPermission.ReadAndWrite);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result SetHeapSize(ulong size, out ulong address)
        {
            address = 0;

            if (size > HeapRegionEnd - HeapRegionStart)
            {
                return KernelResult.OutOfMemory;
            }

            KProcess currentProcess = KernelStatic.GetCurrentProcess();

            lock (_blockManager)
            {
                ulong currentHeapSize = GetHeapSize();

                if (currentHeapSize <= size)
                {
                    // Expand.
                    ulong sizeDelta = size - currentHeapSize;

                    if (currentProcess.ResourceLimit != null && sizeDelta != 0 &&
                        !currentProcess.ResourceLimit.Reserve(LimitableResource.Memory, sizeDelta))
                    {
                        return KernelResult.ResLimitExceeded;
                    }

                    ulong pagesCount = sizeDelta / PageSize;

                    KMemoryRegionManager region = GetMemoryRegionManager();

                    Result result = region.AllocatePages(out KPageList pageList, pagesCount);

                    using var _ = new OnScopeExit(() => pageList.DecrementPagesReferenceCount(Context.MemoryManager));

                    void CleanUpForError()
                    {
                        if (currentProcess.ResourceLimit != null && sizeDelta != 0)
                        {
                            currentProcess.ResourceLimit.Release(LimitableResource.Memory, sizeDelta);
                        }
                    }

                    if (result != Result.Success)
                    {
                        CleanUpForError();

                        return result;
                    }

                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        CleanUpForError();

                        return KernelResult.OutOfResource;
                    }

                    if (!IsUnmapped(_currentHeapAddr, sizeDelta))
                    {
                        CleanUpForError();

                        return KernelResult.InvalidMemState;
                    }

                    result = MapPages(_currentHeapAddr, pageList, KMemoryPermission.ReadAndWrite, MemoryMapFlags.Private, true, (byte)_heapFillValue);

                    if (result != Result.Success)
                    {
                        CleanUpForError();

                        return result;
                    }

                    _blockManager.InsertBlock(_currentHeapAddr, pagesCount, MemoryState.Heap, KMemoryPermission.ReadAndWrite);
                }
                else
                {
                    // Shrink.
                    ulong freeAddr = HeapRegionStart + size;
                    ulong sizeDelta = currentHeapSize - size;

                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    if (!CheckRange(
                        freeAddr,
                        sizeDelta,
                        MemoryState.Mask,
                        MemoryState.Heap,
                        KMemoryPermission.Mask,
                        KMemoryPermission.ReadAndWrite,
                        MemoryAttribute.Mask,
                        MemoryAttribute.None,
                        MemoryAttribute.IpcAndDeviceMapped,
                        out _,
                        out _,
                        out _))
                    {
                        return KernelResult.InvalidMemState;
                    }

                    ulong pagesCount = sizeDelta / PageSize;

                    Result result = Unmap(freeAddr, pagesCount);

                    if (result != Result.Success)
                    {
                        return result;
                    }

                    currentProcess.ResourceLimit?.Release(LimitableResource.Memory, sizeDelta);

                    _blockManager.InsertBlock(freeAddr, pagesCount, MemoryState.Unmapped);
                }

                _currentHeapAddr = HeapRegionStart + size;
            }

            address = HeapRegionStart;

            return Result.Success;
        }

        public Result SetMemoryPermission(ulong address, ulong size, KMemoryPermission permission)
        {
            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    MemoryState.PermissionChangeAllowed,
                    MemoryState.PermissionChangeAllowed,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState oldState,
                    out KMemoryPermission oldPermission,
                    out _))
                {
                    if (permission != oldPermission)
                    {
                        if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                        {
                            return KernelResult.OutOfResource;
                        }

                        ulong pagesCount = size / PageSize;

                        Result result = Reprotect(address, pagesCount, permission);

                        if (result != Result.Success)
                        {
                            return result;
                        }

                        _blockManager.InsertBlock(address, pagesCount, oldState, permission);
                    }

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public ulong GetTotalHeapSize()
        {
            lock (_blockManager)
            {
                return GetHeapSize() + PhysicalMemoryUsage;
            }
        }

        private ulong GetHeapSize()
        {
            return _currentHeapAddr - HeapRegionStart;
        }

        public Result SetHeapCapacity(ulong capacity)
        {
            lock (_blockManager)
            {
                _heapCapacity = capacity;
            }

            return Result.Success;
        }

        public Result SetMemoryAttribute(ulong address, ulong size, MemoryAttribute attributeMask, MemoryAttribute attributeValue)
        {
            lock (_blockManager)
            {
                MemoryState stateCheckMask = 0;

                if (attributeMask.HasFlag(MemoryAttribute.Uncached))
                {
                    stateCheckMask = MemoryState.AttributeChangeAllowed;
                }

                if (attributeMask.HasFlag(MemoryAttribute.PermissionLocked))
                {
                    stateCheckMask |= MemoryState.PermissionLockAllowed;
                }

                if (CheckRange(
                    address,
                    size,
                    stateCheckMask,
                    stateCheckMask,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.BorrowedAndIpcMapped,
                    MemoryAttribute.None,
                    MemoryAttribute.DeviceMappedAndUncached,
                    out MemoryState state,
                    out KMemoryPermission permission,
                    out MemoryAttribute attribute))
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    ulong pagesCount = size / PageSize;

                    attribute &= ~attributeMask;
                    attribute |= attributeMask & attributeValue;

                    _blockManager.InsertBlock(address, pagesCount, state, permission, attribute);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public KMemoryInfo QueryMemory(ulong address)
        {
            if (address >= AddrSpaceStart &&
                address < AddrSpaceEnd)
            {
                lock (_blockManager)
                {
                    return _blockManager.FindBlock(address).GetInfo();
                }
            }
            else
            {
                return new KMemoryInfo(
                    AddrSpaceEnd,
                    ~AddrSpaceEnd + 1,
                    MemoryState.Reserved,
                    KMemoryPermission.None,
                    MemoryAttribute.None,
                    KMemoryPermission.None,
                    0,
                    0);
            }
        }

        public Result Map(ulong dst, ulong src, ulong size)
        {
            bool success;

            lock (_blockManager)
            {
                success = CheckRange(
                    src,
                    size,
                    MemoryState.MapAllowed,
                    MemoryState.MapAllowed,
                    KMemoryPermission.Mask,
                    KMemoryPermission.ReadAndWrite,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState srcState,
                    out _,
                    out _);

                success &= IsUnmapped(dst, size);

                if (success)
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion * 2))
                    {
                        return KernelResult.OutOfResource;
                    }

                    ulong pagesCount = size / PageSize;

                    Result result = MapMemory(src, dst, pagesCount, KMemoryPermission.ReadAndWrite, KMemoryPermission.ReadAndWrite);

                    if (result != Result.Success)
                    {
                        return result;
                    }

                    _blockManager.InsertBlock(src, pagesCount, srcState, KMemoryPermission.None, MemoryAttribute.Borrowed);
                    _blockManager.InsertBlock(dst, pagesCount, MemoryState.Stack, KMemoryPermission.ReadAndWrite);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result UnmapForKernel(ulong address, ulong pagesCount, MemoryState stateExpected)
        {
            ulong size = pagesCount * PageSize;

            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    MemoryState.Mask,
                    stateExpected,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out _,
                    out _,
                    out _))
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    Result result = Unmap(address, pagesCount);

                    if (result == Result.Success)
                    {
                        _blockManager.InsertBlock(address, pagesCount, MemoryState.Unmapped);
                    }

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result Unmap(ulong dst, ulong src, ulong size)
        {
            bool success;

            lock (_blockManager)
            {
                success = CheckRange(
                    src,
                    size,
                    MemoryState.MapAllowed,
                    MemoryState.MapAllowed,
                    KMemoryPermission.Mask,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.Borrowed,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState srcState,
                    out _,
                    out _);

                success &= CheckRange(
                    dst,
                    size,
                    MemoryState.Mask,
                    MemoryState.Stack,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out _,
                    out KMemoryPermission dstPermission,
                    out _);

                if (success)
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion * 2))
                    {
                        return KernelResult.OutOfResource;
                    }

                    ulong pagesCount = size / PageSize;

                    Result result = UnmapMemory(dst, src, pagesCount, dstPermission, KMemoryPermission.ReadAndWrite);

                    if (result != Result.Success)
                    {
                        return result;
                    }

                    _blockManager.InsertBlock(src, pagesCount, srcState, KMemoryPermission.ReadAndWrite);
                    _blockManager.InsertBlock(dst, pagesCount, MemoryState.Unmapped);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result UnmapProcessMemory(ulong dst, ulong size, KPageTableBase srcPageTable, ulong src)
        {
            lock (_blockManager)
            {
                lock (srcPageTable._blockManager)
                {
                    bool success = CheckRange(
                        dst,
                        size,
                        MemoryState.Mask,
                        MemoryState.ProcessMemory,
                        KMemoryPermission.ReadAndWrite,
                        KMemoryPermission.ReadAndWrite,
                        MemoryAttribute.Mask,
                        MemoryAttribute.None,
                        MemoryAttribute.IpcAndDeviceMapped,
                        out _,
                        out _,
                        out _);

                    success &= srcPageTable.CheckRange(
                        src,
                        size,
                        MemoryState.MapProcessAllowed,
                        MemoryState.MapProcessAllowed,
                        KMemoryPermission.None,
                        KMemoryPermission.None,
                        MemoryAttribute.Mask,
                        MemoryAttribute.None,
                        MemoryAttribute.IpcAndDeviceMapped,
                        out _,
                        out _,
                        out _);

                    if (!success)
                    {
                        return KernelResult.InvalidMemState;
                    }

                    KPageList srcPageList = new();
                    KPageList dstPageList = new();

                    srcPageTable.GetPhysicalRegions(src, size, srcPageList);
                    GetPhysicalRegions(dst, size, dstPageList);

                    if (!dstPageList.IsEqual(srcPageList))
                    {
                        return KernelResult.InvalidMemRange;
                    }
                }

                if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                {
                    return KernelResult.OutOfResource;
                }

                ulong pagesCount = size / PageSize;

                Result result = Unmap(dst, pagesCount);

                if (result != Result.Success)
                {
                    return result;
                }

                _blockManager.InsertBlock(dst, pagesCount, MemoryState.Unmapped);

                return Result.Success;
            }
        }

        public Result SetProcessMemoryPermission(ulong address, ulong size, KMemoryPermission permission)
        {
            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    MemoryState.ProcessPermissionChangeAllowed,
                    MemoryState.ProcessPermissionChangeAllowed,
                    KMemoryPermission.None,
                    KMemoryPermission.None,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState oldState,
                    out KMemoryPermission oldPermission,
                    out _))
                {
                    MemoryState newState = oldState;

                    // If writing into the code region is allowed, then we need
                    // to change it to mutable.
                    if ((permission & KMemoryPermission.Write) != 0)
                    {
                        if (oldState == MemoryState.CodeStatic)
                        {
                            newState = MemoryState.CodeMutable;
                        }
                        else if (oldState == MemoryState.ModCodeStatic)
                        {
                            newState = MemoryState.ModCodeMutable;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Memory state \"{oldState}\" not valid for this operation.");
                        }
                    }

                    if (newState != oldState || permission != oldPermission)
                    {
                        if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                        {
                            return KernelResult.OutOfResource;
                        }

                        ulong pagesCount = size / PageSize;

                        Result result;

                        if ((oldPermission & KMemoryPermission.Execute) != 0)
                        {
                            result = ReprotectAndFlush(address, pagesCount, permission);
                        }
                        else
                        {
                            result = Reprotect(address, pagesCount, permission);
                        }

                        if (result != Result.Success)
                        {
                            return result;
                        }

                        _blockManager.InsertBlock(address, pagesCount, newState, permission);
                    }

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result MapPhysicalMemory(ulong address, ulong size)
        {
            ulong endAddr = address + size;

            lock (_blockManager)
            {
                ulong mappedSize = 0;

                foreach (KMemoryInfo info in IterateOverRange(address, endAddr))
                {
                    if (info.State != MemoryState.Unmapped)
                    {
                        mappedSize += GetSizeInRange(info, address, endAddr);
                    }
                }

                if (mappedSize == size)
                {
                    return Result.Success;
                }

                ulong remainingSize = size - mappedSize;

                ulong remainingPages = remainingSize / PageSize;

                KProcess currentProcess = KernelStatic.GetCurrentProcess();

                if (currentProcess.ResourceLimit != null &&
                   !currentProcess.ResourceLimit.Reserve(LimitableResource.Memory, remainingSize))
                {
                    return KernelResult.ResLimitExceeded;
                }

                KMemoryRegionManager region = GetMemoryRegionManager();

                Result result = region.AllocatePages(out KPageList pageList, remainingPages);

                using var _ = new OnScopeExit(() => pageList.DecrementPagesReferenceCount(Context.MemoryManager));

                void CleanUpForError()
                {
                    currentProcess.ResourceLimit?.Release(LimitableResource.Memory, remainingSize);
                }

                if (result != Result.Success)
                {
                    CleanUpForError();

                    return result;
                }

                if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                {
                    CleanUpForError();

                    return KernelResult.OutOfResource;
                }

                LinkedListNode<KPageNode> pageListNode = pageList.Nodes.First;

                KPageNode pageNode = pageListNode.Value;

                ulong srcPa = pageNode.Address;
                ulong srcPaPages = pageNode.PagesCount;

                foreach (KMemoryInfo info in IterateOverRange(address, endAddr))
                {
                    if (info.State != MemoryState.Unmapped)
                    {
                        continue;
                    }

                    ulong blockSize = GetSizeInRange(info, address, endAddr);

                    ulong dstVaPages = blockSize / PageSize;

                    ulong dstVa = GetAddrInRange(info, address);

                    while (dstVaPages > 0)
                    {
                        if (srcPaPages == 0)
                        {
                            pageListNode = pageListNode.Next;

                            pageNode = pageListNode.Value;

                            srcPa = pageNode.Address;
                            srcPaPages = pageNode.PagesCount;
                        }

                        ulong currentPagesCount = Math.Min(srcPaPages, dstVaPages);

                        MapPages(dstVa, currentPagesCount, srcPa, KMemoryPermission.ReadAndWrite, MemoryMapFlags.Private);

                        dstVa += currentPagesCount * PageSize;
                        srcPa += currentPagesCount * PageSize;
                        srcPaPages -= currentPagesCount;
                        dstVaPages -= currentPagesCount;
                    }
                }

                PhysicalMemoryUsage += remainingSize;

                ulong pagesCount = size / PageSize;

                _blockManager.InsertBlock(
                    address,
                    pagesCount,
                    MemoryState.Unmapped,
                    KMemoryPermission.None,
                    MemoryAttribute.None,
                    MemoryState.Heap,
                    KMemoryPermission.ReadAndWrite,
                    MemoryAttribute.None);
            }

            return Result.Success;
        }

        public Result UnmapPhysicalMemory(ulong address, ulong size)
        {
            ulong endAddr = address + size;

            lock (_blockManager)
            {
                // Scan, ensure that the region can be unmapped (all blocks are heap or
                // already unmapped), fill pages list for freeing memory.
                ulong heapMappedSize = 0;

                foreach (KMemoryInfo info in IterateOverRange(address, endAddr))
                {
                    if (info.State == MemoryState.Heap)
                    {
                        if (info.Attribute != MemoryAttribute.None)
                        {
                            return KernelResult.InvalidMemState;
                        }

                        ulong blockSize = GetSizeInRange(info, address, endAddr);

                        heapMappedSize += blockSize;
                    }
                    else if (info.State != MemoryState.Unmapped)
                    {
                        return KernelResult.InvalidMemState;
                    }
                }

                if (heapMappedSize == 0)
                {
                    return Result.Success;
                }

                if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                {
                    return KernelResult.OutOfResource;
                }

                // Try to unmap all the heap mapped memory inside range.
                Result result = Result.Success;

                foreach (KMemoryInfo info in IterateOverRange(address, endAddr))
                {
                    if (info.State == MemoryState.Heap)
                    {
                        ulong blockSize = GetSizeInRange(info, address, endAddr);
                        ulong blockAddress = GetAddrInRange(info, address);

                        ulong blockPagesCount = blockSize / PageSize;

                        result = Unmap(blockAddress, blockPagesCount);

                        // The kernel would attempt to remap if this fails, but we don't because:
                        // - The implementation may not support remapping if memory aliasing is not supported on the platform.
                        // - Unmap can't ever fail here anyway.
                        Debug.Assert(result == Result.Success);
                    }
                }

                if (result == Result.Success)
                {
                    PhysicalMemoryUsage -= heapMappedSize;

                    KProcess currentProcess = KernelStatic.GetCurrentProcess();

                    currentProcess.ResourceLimit?.Release(LimitableResource.Memory, heapMappedSize);

                    ulong pagesCount = size / PageSize;

                    _blockManager.InsertBlock(address, pagesCount, MemoryState.Unmapped);
                }

                return result;
            }
        }

        public Result CopyDataToCurrentProcess(
            ulong dst,
            ulong size,
            ulong src,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permission,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected)
        {
            // Client -> server.
            return CopyDataFromOrToCurrentProcess(
                size,
                src,
                dst,
                stateMask,
                stateExpected,
                permission,
                attributeMask,
                attributeExpected,
                toServer: true);
        }

        public Result CopyDataFromCurrentProcess(
            ulong dst,
            ulong size,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permission,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected,
            ulong src)
        {
            // Server -> client.
            return CopyDataFromOrToCurrentProcess(
                size,
                dst,
                src,
                stateMask,
                stateExpected,
                permission,
                attributeMask,
                attributeExpected,
                toServer: false);
        }

        private Result CopyDataFromOrToCurrentProcess(
            ulong size,
            ulong clientAddress,
            ulong serverAddress,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permission,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected,
            bool toServer)
        {
            if (AddrSpaceStart > clientAddress)
            {
                return KernelResult.InvalidMemState;
            }

            ulong srcEndAddr = clientAddress + size;

            if (srcEndAddr <= clientAddress || srcEndAddr - 1 > AddrSpaceEnd - 1)
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (CheckRange(
                    clientAddress,
                    size,
                    stateMask,
                    stateExpected,
                    permission,
                    permission,
                    attributeMask | MemoryAttribute.Uncached,
                    attributeExpected))
                {
                    KProcess currentProcess = KernelStatic.GetCurrentProcess();

                    while (size > 0)
                    {
                        ulong copySize = 0x100000; // Copy chunck size. Any value will do, moderate sizes are recommended.

                        if (copySize > size)
                        {
                            copySize = size;
                        }

                        if (toServer)
                        {
                            currentProcess.CpuMemory.Write(serverAddress, GetSpan(clientAddress, (int)copySize));
                        }
                        else
                        {
                            Write(clientAddress, currentProcess.CpuMemory.GetSpan(serverAddress, (int)copySize));
                        }

                        serverAddress += copySize;
                        clientAddress += copySize;
                        size -= copySize;
                    }

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result MapBufferFromClientProcess(
            ulong size,
            ulong src,
            KPageTableBase srcPageTable,
            KMemoryPermission permission,
            MemoryState state,
            bool send,
            out ulong dst)
        {
            dst = 0;

            lock (srcPageTable._blockManager)
            {
                lock (_blockManager)
                {
                    Result result = srcPageTable.ReprotectClientProcess(
                        src,
                        size,
                        permission,
                        state,
                        out int blocksNeeded);

                    if (result != Result.Success)
                    {
                        return result;
                    }

                    if (!srcPageTable._slabManager.CanAllocate(blocksNeeded))
                    {
                        return KernelResult.OutOfResource;
                    }

                    ulong srcMapAddress = BitUtils.AlignUp<ulong>(src, PageSize);
                    ulong srcMapEndAddr = BitUtils.AlignDown<ulong>(src + size, PageSize);
                    ulong srcMapSize = srcMapEndAddr - srcMapAddress;

                    result = MapPagesFromClientProcess(size, src, permission, state, srcPageTable, send, out ulong va);

                    if (result != Result.Success)
                    {
                        if (srcMapEndAddr > srcMapAddress)
                        {
                            srcPageTable.UnmapIpcRestorePermission(src, size, state);
                        }

                        return result;
                    }

                    if (srcMapAddress < srcMapEndAddr)
                    {
                        KMemoryPermission permissionMask = permission == KMemoryPermission.ReadAndWrite
                            ? KMemoryPermission.None
                            : KMemoryPermission.Read;

                        srcPageTable._blockManager.InsertBlock(srcMapAddress, srcMapSize / PageSize, SetIpcMappingPermissions, permissionMask);
                    }

                    dst = va;
                }
            }

            return Result.Success;
        }

        private Result ReprotectClientProcess(
            ulong address,
            ulong size,
            KMemoryPermission permission,
            MemoryState state,
            out int blocksNeeded)
        {
            blocksNeeded = 0;

            if (AddrSpaceStart > address)
            {
                return KernelResult.InvalidMemState;
            }

            ulong endAddr = address + size;

            if (endAddr <= address || endAddr - 1 > AddrSpaceEnd - 1)
            {
                return KernelResult.InvalidMemState;
            }

            MemoryState stateMask;

            switch (state)
            {
                case MemoryState.IpcBuffer0:
                    stateMask = MemoryState.IpcSendAllowedType0;
                    break;
                case MemoryState.IpcBuffer1:
                    stateMask = MemoryState.IpcSendAllowedType1;
                    break;
                case MemoryState.IpcBuffer3:
                    stateMask = MemoryState.IpcSendAllowedType3;
                    break;
                default:
                    return KernelResult.InvalidCombination;
            }

            KMemoryPermission permissionMask = permission == KMemoryPermission.ReadAndWrite
                ? KMemoryPermission.None
                : KMemoryPermission.Read;

            MemoryAttribute attributeMask = MemoryAttribute.Borrowed | MemoryAttribute.Uncached;

            if (state == MemoryState.IpcBuffer0)
            {
                attributeMask |= MemoryAttribute.DeviceMapped;
            }

            ulong addressRounded = BitUtils.AlignUp<ulong>(address, PageSize);
            ulong addressTruncated = BitUtils.AlignDown<ulong>(address, PageSize);
            ulong endAddrRounded = BitUtils.AlignUp<ulong>(endAddr, PageSize);
            ulong endAddrTruncated = BitUtils.AlignDown<ulong>(endAddr, PageSize);

            if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
            {
                return KernelResult.OutOfResource;
            }

            ulong visitedSize = 0;

            void CleanUpForError()
            {
                if (visitedSize == 0)
                {
                    return;
                }

                ulong endAddrVisited = address + visitedSize;

                foreach (KMemoryInfo info in IterateOverRange(addressRounded, endAddrVisited))
                {
                    if ((info.Permission & KMemoryPermission.ReadAndWrite) != permissionMask && info.IpcRefCount == 0)
                    {
                        ulong blockAddress = GetAddrInRange(info, addressRounded);
                        ulong blockSize = GetSizeInRange(info, addressRounded, endAddrVisited);

                        ulong blockPagesCount = blockSize / PageSize;

                        Result reprotectResult = Reprotect(blockAddress, blockPagesCount, info.Permission);
                        Debug.Assert(reprotectResult == Result.Success);
                    }
                }
            }

            // Signal a read for any resources tracking reads in the region, as the other process is likely to use their data.
            SignalMemoryTracking(addressTruncated, endAddrRounded - addressTruncated, false);

            // Reprotect the aligned pages range on the client to make them inaccessible from the client process.
            Result result;

            if (addressRounded < endAddrTruncated)
            {
                foreach (KMemoryInfo info in IterateOverRange(addressRounded, endAddrTruncated))
                {
                    // Check if the block state matches what we expect.
                    if ((info.State & stateMask) != stateMask ||
                        (info.Permission & permission) != permission ||
                        (info.Attribute & attributeMask) != MemoryAttribute.None)
                    {
                        CleanUpForError();

                        return KernelResult.InvalidMemState;
                    }

                    ulong blockAddress = GetAddrInRange(info, addressRounded);
                    ulong blockSize = GetSizeInRange(info, addressRounded, endAddrTruncated);

                    ulong blockPagesCount = blockSize / PageSize;

                    // If the first block starts before the aligned range, it will need to be split.
                    if (info.Address < addressRounded)
                    {
                        blocksNeeded++;
                    }

                    // If the last block ends after the aligned range, it will need to be split.
                    if (endAddrTruncated - 1 < info.Address + info.Size - 1)
                    {
                        blocksNeeded++;
                    }

                    if ((info.Permission & KMemoryPermission.ReadAndWrite) != permissionMask && info.IpcRefCount == 0)
                    {
                        result = Reprotect(blockAddress, blockPagesCount, permissionMask);

                        if (result != Result.Success)
                        {
                            CleanUpForError();

                            return result;
                        }
                    }

                    visitedSize += blockSize;
                }
            }

            return Result.Success;
        }

        private Result MapPagesFromClientProcess(
            ulong size,
            ulong address,
            KMemoryPermission permission,
            MemoryState state,
            KPageTableBase srcPageTable,
            bool send,
            out ulong dst)
        {
            dst = 0;

            if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
            {
                return KernelResult.OutOfResource;
            }

            ulong endAddr = address + size;

            ulong addressTruncated = BitUtils.AlignDown<ulong>(address, PageSize);
            ulong addressRounded = BitUtils.AlignUp<ulong>(address, PageSize);
            ulong endAddrTruncated = BitUtils.AlignDown<ulong>(endAddr, PageSize);
            ulong endAddrRounded = BitUtils.AlignUp<ulong>(endAddr, PageSize);

            ulong neededSize = endAddrRounded - addressTruncated;

            ulong neededPagesCount = neededSize / PageSize;

            ulong regionPagesCount = (AliasRegionEnd - AliasRegionStart) / PageSize;

            ulong va = 0;

            for (int unit = _mappingUnitSizes.Length - 1; unit >= 0 && va == 0; unit--)
            {
                int alignment = _mappingUnitSizes[unit];

                va = AllocateVa(AliasRegionStart, regionPagesCount, neededPagesCount, alignment);
            }

            if (va == 0)
            {
                return KernelResult.OutOfVaSpace;
            }

            ulong dstFirstPagePa = 0;
            ulong dstLastPagePa = 0;
            ulong currentVa = va;

            using var _ = new OnScopeExit(() =>
            {
                if (dstFirstPagePa != 0)
                {
                    Context.MemoryManager.DecrementPagesReferenceCount(dstFirstPagePa, 1);
                }

                if (dstLastPagePa != 0)
                {
                    Context.MemoryManager.DecrementPagesReferenceCount(dstLastPagePa, 1);
                }
            });

            void CleanUpForError()
            {
                if (currentVa != va)
                {
                    Unmap(va, (currentVa - va) / PageSize);
                }
            }

            // Is the first page address aligned?
            // If not, allocate a new page and copy the unaligned chunck.
            if (addressTruncated < addressRounded)
            {
                dstFirstPagePa = GetMemoryRegionManager().AllocatePagesContiguous(Context, 1, _allocateFromBack);

                if (dstFirstPagePa == 0)
                {
                    CleanUpForError();

                    return KernelResult.OutOfMemory;
                }
            }

            // Is the last page end address aligned?
            // If not, allocate a new page and copy the unaligned chunck.
            if (endAddrTruncated < endAddrRounded && (addressTruncated == addressRounded || addressTruncated < endAddrTruncated))
            {
                dstLastPagePa = GetMemoryRegionManager().AllocatePagesContiguous(Context, 1, _allocateFromBack);

                if (dstLastPagePa == 0)
                {
                    CleanUpForError();

                    return KernelResult.OutOfMemory;
                }
            }

            if (dstFirstPagePa != 0)
            {
                ulong firstPageFillAddress = dstFirstPagePa;
                ulong unusedSizeAfter;

                if (send)
                {
                    ulong unusedSizeBefore = address - addressTruncated;

                    Context.Memory.Fill(GetDramAddressFromPa(dstFirstPagePa), unusedSizeBefore, (byte)_ipcFillValue);

                    ulong copySize = addressRounded <= endAddr ? addressRounded - address : size;
                    var data = srcPageTable.GetSpan(addressTruncated + unusedSizeBefore, (int)copySize);

                    Context.Memory.Write(GetDramAddressFromPa(dstFirstPagePa + unusedSizeBefore), data);

                    firstPageFillAddress += unusedSizeBefore + copySize;

                    unusedSizeAfter = addressRounded > endAddr ? addressRounded - endAddr : 0;
                }
                else
                {
                    unusedSizeAfter = PageSize;
                }

                if (unusedSizeAfter != 0)
                {
                    Context.Memory.Fill(GetDramAddressFromPa(firstPageFillAddress), unusedSizeAfter, (byte)_ipcFillValue);
                }

                Result result = MapPages(currentVa, 1, dstFirstPagePa, permission, MemoryMapFlags.Private);

                if (result != Result.Success)
                {
                    CleanUpForError();

                    return result;
                }

                currentVa += PageSize;
            }

            if (endAddrTruncated > addressRounded)
            {
                ulong alignedSize = endAddrTruncated - addressRounded;

                Result result;

                if (srcPageTable.Supports4KBPages)
                {
                    KPageList pageList = new();
                    srcPageTable.GetPhysicalRegions(addressRounded, alignedSize, pageList);

                    result = MapPages(currentVa, pageList, permission, MemoryMapFlags.None);
                }
                else
                {
                    result = MapForeign(srcPageTable.GetHostRegions(addressRounded, alignedSize), currentVa, alignedSize);
                }

                if (result != Result.Success)
                {
                    CleanUpForError();

                    return result;
                }

                currentVa += alignedSize;
            }

            if (dstLastPagePa != 0)
            {
                ulong lastPageFillAddr = dstLastPagePa;
                ulong unusedSizeAfter;

                if (send)
                {
                    ulong copySize = endAddr - endAddrTruncated;
                    var data = srcPageTable.GetSpan(endAddrTruncated, (int)copySize);

                    Context.Memory.Write(GetDramAddressFromPa(dstLastPagePa), data);

                    lastPageFillAddr += copySize;

                    unusedSizeAfter = PageSize - copySize;
                }
                else
                {
                    unusedSizeAfter = PageSize;
                }

                Context.Memory.Fill(GetDramAddressFromPa(lastPageFillAddr), unusedSizeAfter, (byte)_ipcFillValue);

                Result result = MapPages(currentVa, 1, dstLastPagePa, permission, MemoryMapFlags.Private);

                if (result != Result.Success)
                {
                    CleanUpForError();

                    return result;
                }
            }

            _blockManager.InsertBlock(va, neededPagesCount, state, permission);

            dst = va + (address - addressTruncated);

            return Result.Success;
        }

        public Result UnmapNoAttributeIfStateEquals(ulong address, ulong size, MemoryState state)
        {
            if (AddrSpaceStart > address)
            {
                return KernelResult.InvalidMemState;
            }

            ulong endAddr = address + size;

            if (endAddr <= address || endAddr - 1 > AddrSpaceEnd - 1)
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    MemoryState.Mask,
                    state,
                    KMemoryPermission.Read,
                    KMemoryPermission.Read,
                    MemoryAttribute.Mask,
                    MemoryAttribute.None,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out _,
                    out _,
                    out _))
                {
                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    ulong addressTruncated = BitUtils.AlignDown<ulong>(address, PageSize);
                    ulong addressRounded = BitUtils.AlignUp<ulong>(address, PageSize);
                    ulong endAddrTruncated = BitUtils.AlignDown<ulong>(endAddr, PageSize);
                    ulong endAddrRounded = BitUtils.AlignUp<ulong>(endAddr, PageSize);

                    ulong pagesCount = (endAddrRounded - addressTruncated) / PageSize;

                    Result result = Unmap(addressTruncated, pagesCount);

                    if (result == Result.Success)
                    {
                        _blockManager.InsertBlock(addressTruncated, pagesCount, MemoryState.Unmapped);
                    }

                    return result;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result UnmapIpcRestorePermission(ulong address, ulong size, MemoryState state)
        {
            ulong endAddr = address + size;

            ulong addressRounded = BitUtils.AlignUp<ulong>(address, PageSize);
            ulong addressTruncated = BitUtils.AlignDown<ulong>(address, PageSize);
            ulong endAddrRounded = BitUtils.AlignUp<ulong>(endAddr, PageSize);
            ulong endAddrTruncated = BitUtils.AlignDown<ulong>(endAddr, PageSize);

            ulong pagesCount = addressRounded < endAddrTruncated ? (endAddrTruncated - addressRounded) / PageSize : 0;

            if (pagesCount == 0)
            {
                return Result.Success;
            }

            MemoryState stateMask;

            switch (state)
            {
                case MemoryState.IpcBuffer0:
                    stateMask = MemoryState.IpcSendAllowedType0;
                    break;
                case MemoryState.IpcBuffer1:
                    stateMask = MemoryState.IpcSendAllowedType1;
                    break;
                case MemoryState.IpcBuffer3:
                    stateMask = MemoryState.IpcSendAllowedType3;
                    break;
                default:
                    return KernelResult.InvalidCombination;
            }

            MemoryAttribute attributeMask =
                MemoryAttribute.Borrowed |
                MemoryAttribute.IpcMapped |
                MemoryAttribute.Uncached;

            if (state == MemoryState.IpcBuffer0)
            {
                attributeMask |= MemoryAttribute.DeviceMapped;
            }

            if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
            {
                return KernelResult.OutOfResource;
            }

            // Anything on the client side should see this memory as modified.
            SignalMemoryTracking(addressTruncated, endAddrRounded - addressTruncated, true);

            lock (_blockManager)
            {
                foreach (KMemoryInfo info in IterateOverRange(addressRounded, endAddrTruncated))
                {
                    // Check if the block state matches what we expect.
                    if ((info.State & stateMask) != stateMask ||
                        (info.Attribute & attributeMask) != MemoryAttribute.IpcMapped)
                    {
                        return KernelResult.InvalidMemState;
                    }

                    if (info.Permission != info.SourcePermission && info.IpcRefCount == 1)
                    {
                        ulong blockAddress = GetAddrInRange(info, addressRounded);
                        ulong blockSize = GetSizeInRange(info, addressRounded, endAddrTruncated);

                        ulong blockPagesCount = blockSize / PageSize;

                        Result result = Reprotect(blockAddress, blockPagesCount, info.SourcePermission);

                        if (result != Result.Success)
                        {
                            return result;
                        }
                    }
                }

                _blockManager.InsertBlock(addressRounded, pagesCount, RestoreIpcMappingPermissions);

                return Result.Success;
            }
        }

        private static void SetIpcMappingPermissions(KMemoryBlock block, KMemoryPermission permission)
        {
            block.SetIpcMappingPermission(permission);
        }

        private static void RestoreIpcMappingPermissions(KMemoryBlock block, KMemoryPermission permission)
        {
            block.RestoreIpcMappingPermission();
        }

        public Result GetPagesIfStateEquals(
            ulong address,
            ulong size,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permissionMask,
            KMemoryPermission permissionExpected,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected,
            KPageList pageList)
        {
            if (!InsideAddrSpace(address, size))
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    stateMask | MemoryState.IsPoolAllocated,
                    stateExpected | MemoryState.IsPoolAllocated,
                    permissionMask,
                    permissionExpected,
                    attributeMask,
                    attributeExpected,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out _,
                    out _,
                    out _))
                {
                    GetPhysicalRegions(address, size, pageList);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result BorrowIpcBuffer(ulong address, ulong size)
        {
            return SetAttributesAndChangePermission(
                address,
                size,
                MemoryState.IpcBufferAllowed,
                MemoryState.IpcBufferAllowed,
                KMemoryPermission.Mask,
                KMemoryPermission.ReadAndWrite,
                MemoryAttribute.Mask,
                MemoryAttribute.None,
                KMemoryPermission.None,
                MemoryAttribute.Borrowed);
        }

        public Result BorrowTransferMemory(KPageList pageList, ulong address, ulong size, KMemoryPermission permission)
        {
            return SetAttributesAndChangePermission(
                address,
                size,
                MemoryState.TransferMemoryAllowed,
                MemoryState.TransferMemoryAllowed,
                KMemoryPermission.Mask,
                KMemoryPermission.ReadAndWrite,
                MemoryAttribute.Mask,
                MemoryAttribute.None,
                permission,
                MemoryAttribute.Borrowed,
                pageList);
        }

        public Result BorrowCodeMemory(KPageList pageList, ulong address, ulong size)
        {
            return SetAttributesAndChangePermission(
                address,
                size,
                MemoryState.CodeMemoryAllowed,
                MemoryState.CodeMemoryAllowed,
                KMemoryPermission.Mask,
                KMemoryPermission.ReadAndWrite,
                MemoryAttribute.Mask,
                MemoryAttribute.None,
                KMemoryPermission.None,
                MemoryAttribute.Borrowed,
                pageList);
        }

        private Result SetAttributesAndChangePermission(
            ulong address,
            ulong size,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permissionMask,
            KMemoryPermission permissionExpected,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected,
            KMemoryPermission newPermission,
            MemoryAttribute attributeSetMask,
            KPageList pageList = null)
        {
            if (address + size <= address || !InsideAddrSpace(address, size))
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    stateMask | MemoryState.IsPoolAllocated,
                    stateExpected | MemoryState.IsPoolAllocated,
                    permissionMask,
                    permissionExpected,
                    attributeMask,
                    attributeExpected,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState oldState,
                    out KMemoryPermission oldPermission,
                    out MemoryAttribute oldAttribute))
                {
                    ulong pagesCount = size / PageSize;

                    if (pageList != null)
                    {
                        GetPhysicalRegions(address, pagesCount * PageSize, pageList);
                    }

                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    if (newPermission == KMemoryPermission.None)
                    {
                        newPermission = oldPermission;
                    }

                    if (newPermission != oldPermission)
                    {
                        Result result = Reprotect(address, pagesCount, newPermission);

                        if (result != Result.Success)
                        {
                            return result;
                        }
                    }

                    MemoryAttribute newAttribute = oldAttribute | attributeSetMask;

                    _blockManager.InsertBlock(address, pagesCount, oldState, newPermission, newAttribute);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        public Result UnborrowIpcBuffer(ulong address, ulong size)
        {
            return ClearAttributesAndChangePermission(
                address,
                size,
                MemoryState.IpcBufferAllowed,
                MemoryState.IpcBufferAllowed,
                KMemoryPermission.None,
                KMemoryPermission.None,
                MemoryAttribute.Mask,
                MemoryAttribute.Borrowed,
                KMemoryPermission.ReadAndWrite,
                MemoryAttribute.Borrowed);
        }

        public Result UnborrowTransferMemory(ulong address, ulong size, KPageList pageList)
        {
            return ClearAttributesAndChangePermission(
                address,
                size,
                MemoryState.TransferMemoryAllowed,
                MemoryState.TransferMemoryAllowed,
                KMemoryPermission.None,
                KMemoryPermission.None,
                MemoryAttribute.Mask,
                MemoryAttribute.Borrowed,
                KMemoryPermission.ReadAndWrite,
                MemoryAttribute.Borrowed,
                pageList);
        }

        public Result UnborrowCodeMemory(ulong address, ulong size, KPageList pageList)
        {
            return ClearAttributesAndChangePermission(
                address,
                size,
                MemoryState.CodeMemoryAllowed,
                MemoryState.CodeMemoryAllowed,
                KMemoryPermission.None,
                KMemoryPermission.None,
                MemoryAttribute.Mask,
                MemoryAttribute.Borrowed,
                KMemoryPermission.ReadAndWrite,
                MemoryAttribute.Borrowed,
                pageList);
        }

        private Result ClearAttributesAndChangePermission(
            ulong address,
            ulong size,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permissionMask,
            KMemoryPermission permissionExpected,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected,
            KMemoryPermission newPermission,
            MemoryAttribute attributeClearMask,
            KPageList pageList = null)
        {
            if (address + size <= address || !InsideAddrSpace(address, size))
            {
                return KernelResult.InvalidMemState;
            }

            lock (_blockManager)
            {
                if (CheckRange(
                    address,
                    size,
                    stateMask | MemoryState.IsPoolAllocated,
                    stateExpected | MemoryState.IsPoolAllocated,
                    permissionMask,
                    permissionExpected,
                    attributeMask,
                    attributeExpected,
                    MemoryAttribute.IpcAndDeviceMapped,
                    out MemoryState oldState,
                    out KMemoryPermission oldPermission,
                    out MemoryAttribute oldAttribute))
                {
                    ulong pagesCount = size / PageSize;

                    if (pageList != null)
                    {
                        KPageList currentPageList = new();

                        GetPhysicalRegions(address, pagesCount * PageSize, currentPageList);

                        if (!currentPageList.IsEqual(pageList))
                        {
                            return KernelResult.InvalidMemRange;
                        }
                    }

                    if (!_slabManager.CanAllocate(MaxBlocksNeededForInsertion))
                    {
                        return KernelResult.OutOfResource;
                    }

                    if (newPermission == KMemoryPermission.None)
                    {
                        newPermission = oldPermission;
                    }

                    if (newPermission != oldPermission)
                    {
                        Result result = Reprotect(address, pagesCount, newPermission);

                        if (result != Result.Success)
                        {
                            return result;
                        }
                    }

                    MemoryAttribute newAttribute = oldAttribute & ~attributeClearMask;

                    _blockManager.InsertBlock(address, pagesCount, oldState, newPermission, newAttribute);

                    return Result.Success;
                }
                else
                {
                    return KernelResult.InvalidMemState;
                }
            }
        }

        private static ulong GetAddrInRange(KMemoryInfo info, ulong start)
        {
            if (info.Address < start)
            {
                return start;
            }

            return info.Address;
        }

        private static ulong GetSizeInRange(KMemoryInfo info, ulong start, ulong end)
        {
            ulong endAddr = info.Size + info.Address;
            ulong size = info.Size;

            if (info.Address < start)
            {
                size -= start - info.Address;
            }

            if (endAddr > end)
            {
                size -= endAddr - end;
            }

            return size;
        }

        private bool IsUnmapped(ulong address, ulong size)
        {
            return CheckRange(
                address,
                size,
                MemoryState.Mask,
                MemoryState.Unmapped,
                KMemoryPermission.Mask,
                KMemoryPermission.None,
                MemoryAttribute.Mask,
                MemoryAttribute.None,
                MemoryAttribute.IpcAndDeviceMapped,
                out _,
                out _,
                out _);
        }

        private bool CheckRange(
            ulong address,
            ulong size,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permissionMask,
            KMemoryPermission permissionExpected,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected,
            MemoryAttribute attributeIgnoreMask,
            out MemoryState outState,
            out KMemoryPermission outPermission,
            out MemoryAttribute outAttribute)
        {
            ulong endAddr = address + size;

            KMemoryBlock currBlock = _blockManager.FindBlock(address);

            KMemoryInfo info = currBlock.GetInfo();

            MemoryState firstState = info.State;
            KMemoryPermission firstPermission = info.Permission;
            MemoryAttribute firstAttribute = info.Attribute;

            do
            {
                info = currBlock.GetInfo();

                // Check if the block state matches what we expect.
                if (firstState != info.State ||
                     firstPermission != info.Permission ||
                    (info.Attribute & attributeMask) != attributeExpected ||
                    (firstAttribute | attributeIgnoreMask) != (info.Attribute | attributeIgnoreMask) ||
                    (firstState & stateMask) != stateExpected ||
                    (firstPermission & permissionMask) != permissionExpected)
                {
                    outState = MemoryState.Unmapped;
                    outPermission = KMemoryPermission.None;
                    outAttribute = MemoryAttribute.None;

                    return false;
                }
            }
            while (info.Address + info.Size - 1 < endAddr - 1 && (currBlock = currBlock.Successor) != null);

            outState = firstState;
            outPermission = firstPermission;
            outAttribute = firstAttribute & ~attributeIgnoreMask;

            return true;
        }

        private bool CheckRange(
            ulong address,
            ulong size,
            MemoryState stateMask,
            MemoryState stateExpected,
            KMemoryPermission permissionMask,
            KMemoryPermission permissionExpected,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeExpected)
        {
            foreach (KMemoryInfo info in IterateOverRange(address, address + size))
            {
                // Check if the block state matches what we expect.
                if ((info.State & stateMask) != stateExpected ||
                    (info.Permission & permissionMask) != permissionExpected ||
                    (info.Attribute & attributeMask) != attributeExpected)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<KMemoryInfo> IterateOverRange(ulong start, ulong end)
        {
            KMemoryBlock currBlock = _blockManager.FindBlock(start);

            KMemoryInfo info;

            do
            {
                info = currBlock.GetInfo();

                yield return info;
            }
            while (info.Address + info.Size - 1 < end - 1 && (currBlock = currBlock.Successor) != null);
        }

        private ulong AllocateVa(ulong regionStart, ulong regionPagesCount, ulong neededPagesCount, int alignment)
        {
            ulong address = 0;

            ulong regionEndAddr = regionStart + regionPagesCount * PageSize;

            ulong reservedPagesCount = _isKernel ? 1UL : 4UL;

            if (_aslrEnabled)
            {
                ulong totalNeededSize = (reservedPagesCount + neededPagesCount) * PageSize;

                ulong remainingPages = regionPagesCount - neededPagesCount;

                ulong aslrMaxOffset = ((remainingPages + reservedPagesCount) * PageSize) / (ulong)alignment;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    ulong aslrAddress = BitUtils.AlignDown(regionStart + GetRandomValue(0, aslrMaxOffset) * (ulong)alignment, (ulong)alignment);
                    ulong aslrEndAddr = aslrAddress + totalNeededSize;

                    KMemoryInfo info = _blockManager.FindBlock(aslrAddress).GetInfo();

                    if (info.State != MemoryState.Unmapped)
                    {
                        continue;
                    }

                    ulong currBaseAddr = info.Address + reservedPagesCount * PageSize;
                    ulong currEndAddr = info.Address + info.Size;

                    if (aslrAddress >= regionStart &&
                        aslrAddress >= currBaseAddr &&
                        aslrEndAddr - 1 <= regionEndAddr - 1 &&
                        aslrEndAddr - 1 <= currEndAddr - 1)
                    {
                        address = aslrAddress;
                        break;
                    }
                }

                if (address == 0)
                {
                    ulong aslrPage = GetRandomValue(0, aslrMaxOffset);

                    address = FindFirstFit(
                        regionStart + aslrPage * PageSize,
                        regionPagesCount - aslrPage,
                        neededPagesCount,
                        alignment,
                        0,
                        reservedPagesCount);
                }
            }

            if (address == 0)
            {
                address = FindFirstFit(
                    regionStart,
                    regionPagesCount,
                    neededPagesCount,
                    alignment,
                    0,
                    reservedPagesCount);
            }

            return address;
        }

        private ulong FindFirstFit(
            ulong regionStart,
            ulong regionPagesCount,
            ulong neededPagesCount,
            int alignment,
            ulong reservedStart,
            ulong reservedPagesCount)
        {
            ulong reservedSize = reservedPagesCount * PageSize;

            ulong totalNeededSize = reservedSize + neededPagesCount * PageSize;

            ulong regionEndAddr = (regionStart + regionPagesCount * PageSize) - 1;

            KMemoryBlock currBlock = _blockManager.FindBlock(regionStart);

            KMemoryInfo info = currBlock.GetInfo();

            while (regionEndAddr >= info.Address)
            {
                if (info.State == MemoryState.Unmapped)
                {
                    ulong currBaseAddr = info.Address <= regionStart ? regionStart : info.Address;
                    ulong currEndAddr = info.Address + info.Size - 1;

                    currBaseAddr += reservedSize;

                    ulong address = BitUtils.AlignDown<ulong>(currBaseAddr, (ulong)alignment) + reservedStart;

                    if (currBaseAddr > address)
                    {
                        address += (ulong)alignment;
                    }

                    ulong allocationEndAddr = address + totalNeededSize - 1;

                    if (info.Address <= address &&
                        address < allocationEndAddr &&
                        allocationEndAddr <= regionEndAddr &&
                        allocationEndAddr <= currEndAddr)
                    {
                        return address;
                    }
                }

                currBlock = currBlock.Successor;

                if (currBlock == null)
                {
                    break;
                }

                info = currBlock.GetInfo();
            }

            return 0;
        }

        public bool CanContain(ulong address, ulong size, MemoryState state)
        {
            ulong endAddr = address + size;

            ulong regionBaseAddr = GetBaseAddress(state);
            ulong regionEndAddr = regionBaseAddr + GetSize(state);

            bool InsideRegion()
            {
                return regionBaseAddr <= address &&
                       endAddr > address &&
                       endAddr - 1 <= regionEndAddr - 1;
            }

            bool OutsideHeapRegion()
            {
                return endAddr <= HeapRegionStart || address >= HeapRegionEnd;
            }

            bool OutsideAliasRegion()
            {
                return endAddr <= AliasRegionStart || address >= AliasRegionEnd;
            }

            switch (state)
            {
                case MemoryState.Io:
                case MemoryState.Normal:
                case MemoryState.CodeStatic:
                case MemoryState.CodeMutable:
                case MemoryState.SharedMemory:
                case MemoryState.ModCodeStatic:
                case MemoryState.ModCodeMutable:
                case MemoryState.Stack:
                case MemoryState.ThreadLocal:
                case MemoryState.TransferMemoryIsolated:
                case MemoryState.TransferMemory:
                case MemoryState.ProcessMemory:
                case MemoryState.CodeReadOnly:
                case MemoryState.CodeWritable:
                    return InsideRegion() && OutsideHeapRegion() && OutsideAliasRegion();

                case MemoryState.Heap:
                    return InsideRegion() && OutsideAliasRegion();

                case MemoryState.IpcBuffer0:
                case MemoryState.IpcBuffer1:
                case MemoryState.IpcBuffer3:
                    return InsideRegion() && OutsideHeapRegion();

                case MemoryState.KernelStack:
                    return InsideRegion();
            }

            throw new ArgumentException($"Invalid state value \"{state}\".");
        }

        private ulong GetBaseAddress(MemoryState state)
        {
            switch (state)
            {
                case MemoryState.Io:
                case MemoryState.Normal:
                case MemoryState.ThreadLocal:
                    return TlsIoRegionStart;

                case MemoryState.CodeStatic:
                case MemoryState.CodeMutable:
                case MemoryState.SharedMemory:
                case MemoryState.ModCodeStatic:
                case MemoryState.ModCodeMutable:
                case MemoryState.TransferMemoryIsolated:
                case MemoryState.TransferMemory:
                case MemoryState.ProcessMemory:
                case MemoryState.CodeReadOnly:
                case MemoryState.CodeWritable:
                    return GetAddrSpaceBaseAddr();

                case MemoryState.Heap:
                    return HeapRegionStart;

                case MemoryState.IpcBuffer0:
                case MemoryState.IpcBuffer1:
                case MemoryState.IpcBuffer3:
                    return AliasRegionStart;

                case MemoryState.Stack:
                    return StackRegionStart;

                case MemoryState.KernelStack:
                    return AddrSpaceStart;
            }

            throw new ArgumentException($"Invalid state value \"{state}\".");
        }

        private ulong GetSize(MemoryState state)
        {
            switch (state)
            {
                case MemoryState.Io:
                case MemoryState.Normal:
                case MemoryState.ThreadLocal:
                    return TlsIoRegionEnd - TlsIoRegionStart;

                case MemoryState.CodeStatic:
                case MemoryState.CodeMutable:
                case MemoryState.SharedMemory:
                case MemoryState.ModCodeStatic:
                case MemoryState.ModCodeMutable:
                case MemoryState.TransferMemoryIsolated:
                case MemoryState.TransferMemory:
                case MemoryState.ProcessMemory:
                case MemoryState.CodeReadOnly:
                case MemoryState.CodeWritable:
                    return GetAddrSpaceSize();

                case MemoryState.Heap:
                    return HeapRegionEnd - HeapRegionStart;

                case MemoryState.IpcBuffer0:
                case MemoryState.IpcBuffer1:
                case MemoryState.IpcBuffer3:
                    return AliasRegionEnd - AliasRegionStart;

                case MemoryState.Stack:
                    return StackRegionEnd - StackRegionStart;

                case MemoryState.KernelStack:
                    return AddrSpaceEnd - AddrSpaceStart;
            }

            throw new ArgumentException($"Invalid state value \"{state}\".");
        }

        public ulong GetAddrSpaceBaseAddr()
        {
            return AslrRegionStart;
        }

        public ulong GetAddrSpaceSize()
        {
            return AslrRegionEnd - AslrRegionStart;
        }

        private static ulong GetDramAddressFromPa(ulong pa)
        {
            return pa - DramMemoryMap.DramBase;
        }

        protected KMemoryRegionManager GetMemoryRegionManager()
        {
            return Context.MemoryManager.MemoryRegions[(int)_memRegion];
        }

        public ulong GetMmUsedPages()
        {
            lock (_blockManager)
            {
                return BitUtils.DivRoundUp<ulong>(GetMmUsedSize(), PageSize);
            }
        }

        private ulong GetMmUsedSize()
        {
            return (ulong)(_blockManager.BlocksCount * KMemoryBlockSize);
        }

        public bool IsInvalidRegion(ulong address, ulong size)
        {
            return address + size - 1 > GetAddrSpaceBaseAddr() + GetAddrSpaceSize() - 1;
        }

        public bool InsideAddrSpace(ulong address, ulong size)
        {
            return AddrSpaceStart <= address && address + size - 1 <= AddrSpaceEnd - 1;
        }

        public bool InsideAliasRegion(ulong address, ulong size)
        {
            return address + size > AliasRegionStart && AliasRegionEnd > address;
        }

        public bool InsideHeapRegion(ulong address, ulong size)
        {
            return address + size > HeapRegionStart && HeapRegionEnd > address;
        }

        public bool InsideStackRegion(ulong address, ulong size)
        {
            return address + size > StackRegionStart && StackRegionEnd > address;
        }

        public bool OutsideAliasRegion(ulong address, ulong size)
        {
            return AliasRegionStart > address || address + size - 1 > AliasRegionEnd - 1;
        }

        public bool OutsideAddrSpace(ulong address, ulong size)
        {
            return AddrSpaceStart > address || address + size - 1 > AddrSpaceEnd - 1;
        }

        public bool OutsideStackRegion(ulong address, ulong size)
        {
            return StackRegionStart > address || address + size - 1 > StackRegionEnd - 1;
        }

        /// <summary>
        /// Gets the host regions that make up the given virtual address region.
        /// If any part of the virtual region is unmapped, null is returned.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <returns>The host regions</returns>
        /// <exception cref="Ryujinx.Memory.InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        protected abstract IEnumerable<HostMemoryRange> GetHostRegions(ulong va, ulong size);

        /// <summary>
        /// Gets the physical regions that make up the given virtual address region.
        /// If any part of the virtual region is unmapped, null is returned.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <param name="pageList">Page list where the ranges will be added</param>
        protected abstract void GetPhysicalRegions(ulong va, ulong size, KPageList pageList);

        /// <summary>
        /// Gets a read-only span of data from CPU mapped memory.
        /// </summary>
        /// <remarks>
        /// This may perform a allocation if the data is not contiguous in memory.
        /// For this reason, the span is read-only, you can't modify the data.
        /// </remarks>
        /// <param name="va">Virtual address of the data</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if read tracking is triggered on the span</param>
        /// <returns>A read-only span of the data</returns>
        /// <exception cref="Ryujinx.Memory.InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        protected abstract ReadOnlySpan<byte> GetSpan(ulong va, int size);

        /// <summary>
        /// Maps a new memory region with the contents of a existing memory region.
        /// </summary>
        /// <param name="src">Source memory region where the data will be taken from</param>
        /// <param name="dst">Destination memory region to map</param>
        /// <param name="pagesCount">Number of pages to map</param>
        /// <param name="oldSrcPermission">Current protection of the source memory region</param>
        /// <param name="newDstPermission">Desired protection for the destination memory region</param>
        /// <returns>Result of the mapping operation</returns>
        protected abstract Result MapMemory(ulong src, ulong dst, ulong pagesCount, KMemoryPermission oldSrcPermission, KMemoryPermission newDstPermission);

        /// <summary>
        /// Unmaps a region of memory that was previously mapped with <see cref="MapMemory"/>.
        /// </summary>
        /// <param name="dst">Destination memory region to be unmapped</param>
        /// <param name="src">Source memory region that was originally remapped</param>
        /// <param name="pagesCount">Number of pages to unmap</param>
        /// <param name="oldDstPermission">Current protection of the destination memory region</param>
        /// <param name="newSrcPermission">Desired protection of the source memory region</param>
        /// <returns>Result of the unmapping operation</returns>
        protected abstract Result UnmapMemory(ulong dst, ulong src, ulong pagesCount, KMemoryPermission oldDstPermission, KMemoryPermission newSrcPermission);

        /// <summary>
        /// Maps a region of memory into the specified physical memory region.
        /// </summary>
        /// <param name="dstVa">Destination virtual address that should be mapped</param>
        /// <param name="pagesCount">Number of pages to map</param>
        /// <param name="srcPa">Physical address where the pages should be mapped. May be ignored if aliasing is not supported</param>
        /// <param name="permission">Permission of the region to be mapped</param>
        /// <param name="flags">Flags controlling the memory map operation</param>
        /// <param name="shouldFillPages">Indicate if the pages should be filled with the <paramref name="fillValue"/> value</param>
        /// <param name="fillValue">The value used to fill pages when <paramref name="shouldFillPages"/> is set to true</param>
        /// <returns>Result of the mapping operation</returns>
        protected abstract Result MapPages(
            ulong dstVa,
            ulong pagesCount,
            ulong srcPa,
            KMemoryPermission permission,
            MemoryMapFlags flags,
            bool shouldFillPages = false,
            byte fillValue = 0);

        /// <summary>
        /// Maps a region of memory into the specified physical memory region.
        /// </summary>
        /// <param name="address">Destination virtual address that should be mapped</param>
        /// <param name="pageList">List of physical memory pages where the pages should be mapped. May be ignored if aliasing is not supported</param>
        /// <param name="permission">Permission of the region to be mapped</param>
        /// <param name="flags">Flags controlling the memory map operation</param>
        /// <param name="shouldFillPages">Indicate if the pages should be filled with the <paramref name="fillValue"/> value</param>
        /// <param name="fillValue">The value used to fill pages when <paramref name="shouldFillPages"/> is set to true</param>
        /// <returns>Result of the mapping operation</returns>
        protected abstract Result MapPages(
            ulong address,
            KPageList pageList,
            KMemoryPermission permission,
            MemoryMapFlags flags,
            bool shouldFillPages = false,
            byte fillValue = 0);

        /// <summary>
        /// Maps pages into an arbitrary host memory location.
        /// </summary>
        /// <param name="regions">Host regions to be mapped into the specified virtual memory region</param>
        /// <param name="va">Destination virtual address of the range on this page table</param>
        /// <param name="size">Size of the range</param>
        /// <returns>Result of the mapping operation</returns>
        protected abstract Result MapForeign(IEnumerable<HostMemoryRange> regions, ulong va, ulong size);

        /// <summary>
        /// Unmaps a region of memory that was previously mapped with one of the page mapping methods.
        /// </summary>
        /// <param name="address">Virtual address of the region to unmap</param>
        /// <param name="pagesCount">Number of pages to unmap</param>
        /// <returns>Result of the unmapping operation</returns>
        protected abstract Result Unmap(ulong address, ulong pagesCount, bool clearRejitQueueOnly = false);

        /// <summary>
        /// Changes the permissions of a given virtual memory region.
        /// </summary>
        /// <param name="address">Virtual address of the region to have the permission changes</param>
        /// <param name="pagesCount">Number of pages to have their permissions changed</param>
        /// <param name="permission">New permission</param>
        /// <returns>Result of the permission change operation</returns>
        protected abstract Result Reprotect(ulong address, ulong pagesCount, KMemoryPermission permission);

        /// <summary>
        /// Changes the permissions of a given virtual memory region, while also flushing the cache.
        /// </summary>
        /// <param name="address">Virtual address of the region to have the permission changes</param>
        /// <param name="pagesCount">Number of pages to have their permissions changed</param>
        /// <param name="permission">New permission</param>
        /// <returns>Result of the permission change operation</returns>
        protected abstract Result ReprotectAndFlush(ulong address, ulong pagesCount, KMemoryPermission permission);

        /// <summary>
        /// Alerts the memory tracking that a given region has been read from or written to.
        /// This should be called before read/write is performed.
        /// </summary>
        /// <param name="va">Virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        protected abstract void SignalMemoryTracking(ulong va, ulong size, bool write);

        /// <summary>
        /// Writes data to CPU mapped memory, with write tracking.
        /// </summary>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="data">Data to be written</param>
        /// <exception cref="Ryujinx.Memory.InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        protected abstract void Write(ulong va, ReadOnlySpan<byte> data);
    }
}
