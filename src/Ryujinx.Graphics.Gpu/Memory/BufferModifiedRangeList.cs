using Ryujinx.Common.Pools;
using Ryujinx.Memory.Range;
using System;
using System.Linq;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Memory
{
    /// <summary>
    /// A range within a buffer that has been modified by the GPU.
    /// </summary>
    class BufferModifiedRange : IRange
    {
        /// <summary>
        /// Start address of the range in guest memory.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Size of the range in bytes.
        /// </summary>
        public ulong Size { get; }

        /// <summary>
        /// End address of the range in guest memory.
        /// </summary>
        public ulong EndAddress => Address + Size;

        /// <summary>
        /// The GPU sync number at the time of the last modification.
        /// </summary>
        public ulong SyncNumber { get; internal set; }

        /// <summary>
        /// The range list that originally owned this range.
        /// </summary>
        public BufferModifiedRangeList Parent { get; internal set; }

        /// <summary>
        /// Creates a new instance of a modified range.
        /// </summary>
        /// <param name="address">Start address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <param name="syncNumber">The GPU sync number at the time of creation</param>
        /// <param name="parent">The range list that owns this range</param>
        public BufferModifiedRange(ulong address, ulong size, ulong syncNumber, BufferModifiedRangeList parent)
        {
            Address = address;
            Size = size;
            SyncNumber = syncNumber;
            Parent = parent;
        }

        /// <summary>
        /// Checks if a given range overlaps with the modified range.
        /// </summary>
        /// <param name="address">Start address of the range</param>
        /// <param name="size">Size in bytes of the range</param>
        /// <returns>True if the range overlaps, false otherwise</returns>
        public bool OverlapsWith(ulong address, ulong size)
        {
            return Address < address + size && address < EndAddress;
        }
    }

    /// <summary>
    /// A structure used to track GPU modified ranges within a buffer.
    /// </summary>
    class BufferModifiedRangeList : RangeList<BufferModifiedRange>
    {
        private const int BackingInitialSize = 8;

        private readonly GpuContext _context;
        private readonly Buffer _parent;
        private readonly BufferFlushAction _flushAction;

        private BufferMigration _source;
        private BufferModifiedRangeList _migrationTarget;

        private readonly Lock _lock = new();

        /// <summary>
        /// Whether the modified range list has any entries or not.
        /// </summary>
        public bool HasRanges
        {
            get
            {
                lock (_lock)
                {
                    return Count > 0;
                }
            }
        }

        /// <summary>
        /// Creates a new instance of a modified range list.
        /// </summary>
        /// <param name="context">GPU context that the buffer range list belongs to</param>
        /// <param name="parent">The parent buffer that owns this range list</param>
        /// <param name="flushAction">The flush action for the parent buffer</param>
        public BufferModifiedRangeList(GpuContext context, Buffer parent, BufferFlushAction flushAction) : base(BackingInitialSize)
        {
            _context = context;
            _parent = parent;
            _flushAction = flushAction;
        }

        /// <summary>
        /// Given an input range, calls the given action with sub-ranges which exclude any of the modified regions.
        /// </summary>
        /// <param name="address">Start address of the query range</param>
        /// <param name="size">Size of the query range in bytes</param>
        /// <param name="action">Action to perform for each remaining sub-range of the input range</param>
        public void ExcludeModifiedRegions(ulong address, ulong size, Action<ulong, ulong> action)
        {
            lock (_lock)
            {
                // Slices a given region using the modified regions in the list. Calls the action for the new slices.
                ref var overlaps = ref ThreadStaticArray<BufferModifiedRange>.Get();

                int count = FindOverlapsNonOverlapping(address, size, ref overlaps);

                for (int i = 0; i < count; i++)
                {
                    BufferModifiedRange overlap = overlaps[i];

                    if (overlap.Address > address)
                    {
                        // The start of the remaining region is uncovered by this overlap. Call the action for it.
                        action(address, overlap.Address - address);
                    }

                    // Remaining region is after this overlap.
                    size -= overlap.EndAddress - address;
                    address = overlap.EndAddress;
                }

                if ((long)size > 0)
                {
                    // If there is any region left after removing the overlaps, signal it.
                    action(address, size);
                }
            }
        }

        /// <summary>
        /// Signal that a region of the buffer has been modified, and add the new region to the range list.
        /// Any overlapping ranges will be (partially) removed.
        /// </summary>
        /// <param name="address">Start address of the modified region</param>
        /// <param name="size">Size of the modified region in bytes</param>
        public void SignalModified(ulong address, ulong size)
        {
            // Must lock, as this can affect flushes from the background thread.
            lock (_lock)
            {
                // We may overlap with some existing modified regions. They must be cut into by the new entry.
                ref var overlaps = ref ThreadStaticArray<BufferModifiedRange>.Get();

                int count = FindOverlapsNonOverlapping(address, size, ref overlaps);

                ulong endAddress = address + size;
                ulong syncNumber = _context.SyncNumber;

                for (int i = 0; i < count; i++)
                {
                    // The overlaps must be removed or split.

                    BufferModifiedRange overlap = overlaps[i];

                    if (overlap.Address == address && overlap.Size == size)
                    {
                        // Region already exists. Just update the existing sync number.
                        overlap.SyncNumber = syncNumber;
                        overlap.Parent = this;

                        return;
                    }

                    Remove(overlap);

                    if (overlap.Address < address && overlap.EndAddress > address)
                    {
                        // A split item must be created behind this overlap.

                        Add(new BufferModifiedRange(overlap.Address, address - overlap.Address, overlap.SyncNumber, overlap.Parent));
                    }

                    if (overlap.Address < endAddress && overlap.EndAddress > endAddress)
                    {
                        // A split item must be created after this overlap.

                        Add(new BufferModifiedRange(endAddress, overlap.EndAddress - endAddress, overlap.SyncNumber, overlap.Parent));
                    }
                }

                Add(new BufferModifiedRange(address, size, syncNumber, this));
            }
        }

        /// <summary>
        /// Gets modified ranges within the specified region, and then fires the given action for each range individually.
        /// </summary>
        /// <param name="address">Start address to query</param>
        /// <param name="size">Size to query</param>
        /// <param name="syncNumber">Sync number required for a range to be signalled</param>
        /// <param name="rangeAction">The action to call for each modified range</param>
        public void GetRangesAtSync(ulong address, ulong size, ulong syncNumber, Action<ulong, ulong> rangeAction)
        {
            int count = 0;

            ref var overlaps = ref ThreadStaticArray<BufferModifiedRange>.Get();

            // Range list must be consistent for this operation.
            lock (_lock)
            {
                count = FindOverlapsNonOverlapping(address, size, ref overlaps);
            }

            for (int i = 0; i < count; i++)
            {
                BufferModifiedRange overlap = overlaps[i];

                if (overlap.SyncNumber == syncNumber)
                {
                    rangeAction(overlap.Address, overlap.Size);
                }
            }
        }

        /// <summary>
        /// Gets modified ranges within the specified region, and then fires the given action for each range individually.
        /// </summary>
        /// <param name="address">Start address to query</param>
        /// <param name="size">Size to query</param>
        /// <param name="rangeAction">The action to call for each modified range</param>
        public void GetRanges(ulong address, ulong size, Action<ulong, ulong> rangeAction)
        {
            int count = 0;

            ref var overlaps = ref ThreadStaticArray<BufferModifiedRange>.Get();

            // Range list must be consistent for this operation.
            lock (_lock)
            {
                count = FindOverlapsNonOverlapping(address, size, ref overlaps);
            }

            for (int i = 0; i < count; i++)
            {
                BufferModifiedRange overlap = overlaps[i];
                rangeAction(overlap.Address, overlap.Size);
            }
        }

        /// <summary>
        /// Queries if a range exists within the specified region.
        /// </summary>
        /// <param name="address">Start address to query</param>
        /// <param name="size">Size to query</param>
        /// <returns>True if a range exists in the specified region, false otherwise</returns>
        public bool HasRange(ulong address, ulong size)
        {
            // Range list must be consistent for this operation.
            lock (_lock)
            {
                return FindOverlapsNonOverlapping(address, size, ref ThreadStaticArray<BufferModifiedRange>.Get()) > 0;
            }
        }

        /// <summary>
        /// Performs the given range action, or one from a migration that overlaps and has not synced yet.
        /// </summary>
        /// <param name="offset">The offset to pass to the action</param>
        /// <param name="size">The size to pass to the action</param>
        /// <param name="syncNumber">The sync number that has been reached</param>
        /// <param name="rangeAction">The action to perform</param>
        public void RangeActionWithMigration(ulong offset, ulong size, ulong syncNumber, BufferFlushAction rangeAction)
        {
            if (_source != null)
            {
                _source.RangeActionWithMigration(offset, size, syncNumber, rangeAction);
            }
            else
            {
                rangeAction(offset, size, syncNumber);
            }
        }

        /// <summary>
        /// Removes modified ranges ready by the sync number from the list, and flushes their buffer data within a given address range.
        /// </summary>
        /// <param name="overlaps">Overlapping ranges to check</param>
        /// <param name="rangeCount">Number of overlapping ranges</param>
        /// <param name="highestDiff">The highest difference between an overlapping range's sync number and the current one</param>
        /// <param name="currentSync">The current sync number</param>
        /// <param name="address">The start address of the flush range</param>
        /// <param name="endAddress">The end address of the flush range</param>
        private void RemoveRangesAndFlush(
            BufferModifiedRange[] overlaps,
            int rangeCount,
            long highestDiff,
            ulong currentSync,
            ulong address,
            ulong endAddress)
        {
            lock (_lock)
            {
                if (_migrationTarget == null)
                {
                    ulong waitSync = currentSync + (ulong)highestDiff;

                    for (int i = 0; i < rangeCount; i++)
                    {
                        BufferModifiedRange overlap = overlaps[i];

                        long diff = (long)(overlap.SyncNumber - currentSync);

                        if (diff <= highestDiff)
                        {
                            ulong clampAddress = Math.Max(address, overlap.Address);
                            ulong clampEnd = Math.Min(endAddress, overlap.EndAddress);

                            ClearPart(overlap, clampAddress, clampEnd);

                            RangeActionWithMigration(clampAddress, clampEnd - clampAddress, waitSync, _flushAction);
                        }
                    }

                    return;
                }
            }

            // There is a migration target to call instead. This can't be changed after set so accessing it outside the lock is fine.

            _migrationTarget.RemoveRangesAndFlush(overlaps, rangeCount, highestDiff, currentSync, address, endAddress);
        }

        /// <summary>
        /// Gets modified ranges within the specified region, waits on ones from a previous sync number,
        /// and then fires the flush action for each range individually.
        /// </summary>
        /// <remarks>
        /// This function assumes it is called from the background thread.
        /// Modifications from the current sync number are ignored because the guest should not expect them to be available yet.
        /// They will remain reserved, so that any data sync prioritizes the data in the GPU.
        /// </remarks>
        /// <param name="address">Start address to query</param>
        /// <param name="size">Size to query</param>
        public void WaitForAndFlushRanges(ulong address, ulong size)
        {
            ulong endAddress = address + size;
            ulong currentSync = _context.SyncNumber;

            int rangeCount = 0;

            ref var overlaps = ref ThreadStaticArray<BufferModifiedRange>.Get();

            // Range list must be consistent for this operation
            lock (_lock)
            {
                if (_migrationTarget != null)
                {
                    rangeCount = -1;
                }
                else
                {
                    rangeCount = FindOverlapsNonOverlapping(address, size, ref overlaps);
                }
            }

            if (rangeCount == -1)
            {
                _migrationTarget.WaitForAndFlushRanges(address, size);

                return;
            }
            else if (rangeCount == 0)
            {
                return;
            }

            // First, determine which syncpoint to wait on.
            // This is the latest syncpoint that is not equal to the current sync.

            long highestDiff = long.MinValue;

            for (int i = 0; i < rangeCount; i++)
            {
                BufferModifiedRange overlap = overlaps[i];

                long diff = (long)(overlap.SyncNumber - currentSync);

                if (diff < 0 && diff > highestDiff)
                {
                    highestDiff = diff;
                }
            }

            if (highestDiff == long.MinValue)
            {
                return;
            }

            // Wait for the syncpoint.
            _context.Renderer.WaitSync(currentSync + (ulong)highestDiff);

            RemoveRangesAndFlush(overlaps, rangeCount, highestDiff, currentSync, address, endAddress);
        }

        /// <summary>
        /// Inherit ranges from another modified range list.
        /// </summary>
        /// <remarks>
        /// Assumes that ranges will be inherited in address ascending order.
        /// </remarks>
        /// <param name="ranges">The range list to inherit from</param>
        /// <param name="registerRangeAction">The action to call for each modified range</param>
        public void InheritRanges(BufferModifiedRangeList ranges, Action<ulong, ulong> registerRangeAction)
        {
            BufferModifiedRange[] inheritRanges;

            lock (ranges._lock)
            {
                inheritRanges = ranges.ToArray();

                lock (_lock)
                {
                    // Copy over the migration from the previous range list

                    BufferMigration oldMigration = ranges._source;

                    BufferMigrationSpan span = new BufferMigrationSpan(ranges._parent, ranges._flushAction, oldMigration);
                    ranges._parent.IncrementReferenceCount();

                    if (_source == null)
                    {
                        // Create a new migration. 
                        _source = new BufferMigration(new BufferMigrationSpan[] { span }, this, _context.SyncNumber);

                        _context.RegisterBufferMigration(_source);
                    }
                    else
                    {
                        // Extend the migration
                        _source.AddSpanToEnd(span);
                    }

                    ranges._migrationTarget = this;

                    foreach (BufferModifiedRange range in inheritRanges)
                    {
                        Add(range);
                    }
                }
            }

            ulong currentSync = _context.SyncNumber;
            foreach (BufferModifiedRange range in inheritRanges)
            {
                if (range.SyncNumber != currentSync)
                {
                    registerRangeAction(range.Address, range.Size);
                }
            }
        }

        /// <summary>
        /// Register a migration from previous buffer storage. This migration is from a snapshot of the buffer's
        /// current handle to its handle in the future, and is assumed to be complete when the sync action completes.
        /// When the migration completes, the handle is disposed.
        /// </summary>
        public void SelfMigration()
        {
            lock (_lock)
            {
                BufferMigrationSpan span = new(_parent, _parent.GetSnapshotDisposeAction(), _parent.GetSnapshotFlushAction(), _source);
                BufferMigration migration = new(new BufferMigrationSpan[] { span }, this, _context.SyncNumber);

                // Migration target is used to redirect flush actions to the latest range list,
                // so we don't need to set it here. (this range list is still the latest)

                _context.RegisterBufferMigration(migration);

                _source = migration;
            }
        }

        /// <summary>
        /// Removes a source buffer migration, indicating its copy has completed.
        /// </summary>
        /// <param name="migration">The migration to remove</param>
        public void RemoveMigration(BufferMigration migration)
        {
            lock (_lock)
            {
                if (_source == migration)
                {
                    _source = null;
                }
            }
        }

        private void ClearPart(BufferModifiedRange overlap, ulong address, ulong endAddress)
        {
            Remove(overlap);

            // If the overlap extends outside of the clear range, make sure those parts still exist.

            if (overlap.Address < address)
            {
                Add(new BufferModifiedRange(overlap.Address, address - overlap.Address, overlap.SyncNumber, overlap.Parent));
            }

            if (overlap.EndAddress > endAddress)
            {
                Add(new BufferModifiedRange(endAddress, overlap.EndAddress - endAddress, overlap.SyncNumber, overlap.Parent));
            }
        }

        /// <summary>
        /// Clear modified ranges within the specified area.
        /// </summary>
        /// <param name="address">Start address to clear</param>
        /// <param name="size">Size to clear</param>
        public void Clear(ulong address, ulong size)
        {
            lock (_lock)
            {
                // This function can be called from any thread, so it cannot use the arrays for background or foreground.
                BufferModifiedRange[] toClear = new BufferModifiedRange[1];

                int rangeCount = FindOverlapsNonOverlapping(address, size, ref toClear);

                ulong endAddress = address + size;

                for (int i = 0; i < rangeCount; i++)
                {
                    BufferModifiedRange overlap = toClear[i];

                    ClearPart(overlap, address, endAddress);
                }
            }
        }

        /// <summary>
        /// Clear all modified ranges.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                Count = 0;
            }
        }
    }
}
