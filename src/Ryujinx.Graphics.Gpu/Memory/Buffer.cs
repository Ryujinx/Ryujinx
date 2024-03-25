using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Synchronization;
using Ryujinx.Memory.Range;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Memory
{
    /// <summary>
    /// Buffer, used to store vertex and index data, uniform and storage buffers, and others.
    /// </summary>
    class Buffer : IRange, ISyncActionHandler, IDisposable
    {
        private const ulong GranularBufferThreshold = 4096;

        private readonly GpuContext _context;
        private readonly PhysicalMemory _physicalMemory;

        /// <summary>
        /// Host buffer handle.
        /// </summary>
        public BufferHandle Handle { get; }

        /// <summary>
        /// Start address of the buffer in guest memory.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// Size of the buffer in bytes.
        /// </summary>
        public ulong Size { get; }

        /// <summary>
        /// End address of the buffer in guest memory.
        /// </summary>
        public ulong EndAddress => Address + Size;

        /// <summary>
        /// Increments when the buffer is (partially) unmapped or disposed.
        /// </summary>
        public int UnmappedSequence { get; private set; }

        /// <summary>
        /// Indicates if the buffer can be used in a sparse buffer mapping.
        /// </summary>
        public bool SparseCompatible { get; }

        /// <summary>
        /// Ranges of the buffer that have been modified on the GPU.
        /// Ranges defined here cannot be updated from CPU until a CPU waiting sync point is reached.
        /// Then, write tracking will signal, wait for GPU sync (generated at the syncpoint) and flush these regions.
        /// </summary>
        /// <remarks>
        /// This is null until at least one modification occurs.
        /// </remarks>
        private BufferModifiedRangeList _modifiedRanges = null;

        private readonly MultiRegionHandle _memoryTrackingGranular;
        private readonly RegionHandle _memoryTracking;

        private readonly RegionSignal _externalFlushDelegate;
        private readonly Action<ulong, ulong> _loadDelegate;
        private readonly Action<ulong, ulong> _modifiedDelegate;

        private HashSet<MultiRangeBuffer> _virtualDependencies;
        private readonly ReaderWriterLockSlim _virtualDependenciesLock;

        private int _sequenceNumber;

        private readonly bool _useGranular;
        private bool _syncActionRegistered;

        private int _referenceCount = 1;

        private ulong _dirtyStart = ulong.MaxValue;
        private ulong _dirtyEnd = ulong.MaxValue;

        /// <summary>
        /// Creates a new instance of the buffer.
        /// </summary>
        /// <param name="context">GPU context that the buffer belongs to</param>
        /// <param name="physicalMemory">Physical memory where the buffer is mapped</param>
        /// <param name="address">Start address of the buffer</param>
        /// <param name="size">Size of the buffer in bytes</param>
        /// <param name="sparseCompatible">Indicates if the buffer can be used in a sparse buffer mapping</param>
        /// <param name="baseBuffers">Buffers which this buffer contains, and will inherit tracking handles from</param>
        public Buffer(
            GpuContext context,
            PhysicalMemory physicalMemory,
            ulong address,
            ulong size,
            bool sparseCompatible,
            IEnumerable<Buffer> baseBuffers = null)
        {
            _context = context;
            _physicalMemory = physicalMemory;
            Address = address;
            Size = size;
            SparseCompatible = sparseCompatible;

            BufferAccess access = sparseCompatible ? BufferAccess.SparseCompatible : BufferAccess.Default;

            Handle = context.Renderer.CreateBuffer((int)size, access, baseBuffers?.MaxBy(x => x.Size).Handle ?? BufferHandle.Null);

            _useGranular = size > GranularBufferThreshold;

            IEnumerable<IRegionHandle> baseHandles = null;

            if (baseBuffers != null)
            {
                baseHandles = baseBuffers.SelectMany(buffer =>
                {
                    if (buffer._useGranular)
                    {
                        return buffer._memoryTrackingGranular.GetHandles();
                    }
                    else
                    {
                        return Enumerable.Repeat(buffer._memoryTracking, 1);
                    }
                });
            }

            if (_useGranular)
            {
                _memoryTrackingGranular = physicalMemory.BeginGranularTracking(address, size, ResourceKind.Buffer, RegionFlags.UnalignedAccess, baseHandles);

                _memoryTrackingGranular.RegisterPreciseAction(address, size, PreciseAction);
            }
            else
            {
                _memoryTracking = physicalMemory.BeginTracking(address, size, ResourceKind.Buffer, RegionFlags.UnalignedAccess);

                if (baseHandles != null)
                {
                    _memoryTracking.Reprotect(false);

                    foreach (IRegionHandle handle in baseHandles)
                    {
                        if (handle.Dirty)
                        {
                            _memoryTracking.Reprotect(true);
                        }

                        handle.Dispose();
                    }
                }

                _memoryTracking.RegisterPreciseAction(PreciseAction);
            }

            _externalFlushDelegate = new RegionSignal(ExternalFlush);
            _loadDelegate = new Action<ulong, ulong>(LoadRegion);
            _modifiedDelegate = new Action<ulong, ulong>(RegionModified);

            _virtualDependenciesLock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// Gets a sub-range from the buffer, from a start address til a page boundary after the given size.
        /// </summary>
        /// <remarks>
        /// This can be used to bind and use sub-ranges of the buffer on the host API.
        /// </remarks>
        /// <param name="address">Start address of the sub-range, must be greater than or equal to the buffer address</param>
        /// <param name="size">Size in bytes of the sub-range, must be less than or equal to the buffer size</param>
        /// <param name="write">Whether the buffer will be written to by this use</param>
        /// <returns>The buffer sub-range</returns>
        public BufferRange GetRangeAligned(ulong address, ulong size, bool write)
        {
            ulong end = ((address + size + MemoryManager.PageMask) & ~MemoryManager.PageMask) - Address;
            ulong offset = address - Address;

            return new BufferRange(Handle, (int)offset, (int)(end - offset), write);
        }

        /// <summary>
        /// Gets a sub-range from the buffer.
        /// </summary>
        /// <remarks>
        /// This can be used to bind and use sub-ranges of the buffer on the host API.
        /// </remarks>
        /// <param name="address">Start address of the sub-range, must be greater than or equal to the buffer address</param>
        /// <param name="size">Size in bytes of the sub-range, must be less than or equal to the buffer size</param>
        /// <param name="write">Whether the buffer will be written to by this use</param>
        /// <returns>The buffer sub-range</returns>
        public BufferRange GetRange(ulong address, ulong size, bool write)
        {
            int offset = (int)(address - Address);

            return new BufferRange(Handle, offset, (int)size, write);
        }

        /// <summary>
        /// Checks if a given range overlaps with the buffer.
        /// </summary>
        /// <param name="address">Start address of the range</param>
        /// <param name="size">Size in bytes of the range</param>
        /// <returns>True if the range overlaps, false otherwise</returns>
        public bool OverlapsWith(ulong address, ulong size)
        {
            return Address < address + size && address < EndAddress;
        }

        /// <summary>
        /// Checks if a given range is fully contained in the buffer.
        /// </summary>
        /// <param name="address">Start address of the range</param>
        /// <param name="size">Size in bytes of the range</param>
        /// <returns>True if the range is contained, false otherwise</returns>
        public bool FullyContains(ulong address, ulong size)
        {
            return address >= Address && address + size <= EndAddress;
        }

        /// <summary>
        /// Performs guest to host memory synchronization of the buffer data.
        /// </summary>
        /// <remarks>
        /// This causes the buffer data to be overwritten if a write was detected from the CPU,
        /// since the last call to this method.
        /// </remarks>
        /// <param name="address">Start address of the range to synchronize</param>
        /// <param name="size">Size in bytes of the range to synchronize</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SynchronizeMemory(ulong address, ulong size)
        {
            if (_useGranular)
            {
                _memoryTrackingGranular.QueryModified(address, size, _modifiedDelegate, _context.SequenceNumber);
            }
            else
            {
                if (_context.SequenceNumber != _sequenceNumber && _memoryTracking.DirtyOrVolatile())
                {
                    _memoryTracking.Reprotect();

                    if (_modifiedRanges != null)
                    {
                        _modifiedRanges.ExcludeModifiedRegions(Address, Size, _loadDelegate);
                    }
                    else
                    {
                        _context.Renderer.SetBufferData(Handle, 0, _physicalMemory.GetSpan(Address, (int)Size));
                        CopyToDependantVirtualBuffers();
                    }

                    _sequenceNumber = _context.SequenceNumber;
                    _dirtyStart = ulong.MaxValue;
                }
            }

            if (_dirtyStart != ulong.MaxValue)
            {
                ulong end = address + size;

                if (end > _dirtyStart && address < _dirtyEnd)
                {
                    if (_modifiedRanges != null)
                    {
                        _modifiedRanges.ExcludeModifiedRegions(_dirtyStart, _dirtyEnd - _dirtyStart, _loadDelegate);
                    }
                    else
                    {
                        LoadRegion(_dirtyStart, _dirtyEnd - _dirtyStart);
                    }

                    _dirtyStart = ulong.MaxValue;
                }
            }
        }

        /// <summary>
        /// Ensure that the modified range list exists.
        /// </summary>
        private void EnsureRangeList()
        {
            _modifiedRanges ??= new BufferModifiedRangeList(_context, this, Flush);
        }

        /// <summary>
        /// Signal that the given region of the buffer has been modified.
        /// </summary>
        /// <param name="address">The start address of the modified region</param>
        /// <param name="size">The size of the modified region</param>
        public void SignalModified(ulong address, ulong size)
        {
            EnsureRangeList();

            _modifiedRanges.SignalModified(address, size);

            if (!_syncActionRegistered)
            {
                _context.RegisterSyncAction(this);
                _syncActionRegistered = true;
            }
        }

        /// <summary>
        /// Indicate that mofifications in a given region of this buffer have been overwritten.
        /// </summary>
        /// <param name="address">The start address of the region</param>
        /// <param name="size">The size of the region</param>
        public void ClearModified(ulong address, ulong size)
        {
            _modifiedRanges?.Clear(address, size);
        }

        /// <summary>
        /// Action to be performed when a syncpoint is reached after modification.
        /// This will register read/write tracking to flush the buffer from GPU when its memory is used.
        /// </summary>
        /// <inheritdoc/>
        public bool SyncAction(bool syncpoint)
        {
            _syncActionRegistered = false;

            if (_useGranular)
            {
                _modifiedRanges?.GetRanges(Address, Size, (address, size) =>
                {
                    _memoryTrackingGranular.RegisterAction(address, size, _externalFlushDelegate);
                    SynchronizeMemory(address, size);
                });
            }
            else
            {
                _memoryTracking.RegisterAction(_externalFlushDelegate);
                SynchronizeMemory(Address, Size);
            }

            return true;
        }

        /// <summary>
        /// Inherit modified and dirty ranges from another buffer.
        /// </summary>
        /// <param name="from">The buffer to inherit from</param>
        public void InheritModifiedRanges(Buffer from)
        {
            if (from._modifiedRanges != null && from._modifiedRanges.HasRanges)
            {
                if (from._syncActionRegistered && !_syncActionRegistered)
                {
                    _context.RegisterSyncAction(this);
                    _syncActionRegistered = true;
                }

                void registerRangeAction(ulong address, ulong size)
                {
                    if (_useGranular)
                    {
                        _memoryTrackingGranular.RegisterAction(address, size, _externalFlushDelegate);
                    }
                    else
                    {
                        _memoryTracking.RegisterAction(_externalFlushDelegate);
                    }
                }

                EnsureRangeList();

                _modifiedRanges.InheritRanges(from._modifiedRanges, registerRangeAction);
            }

            if (from._dirtyStart != ulong.MaxValue)
            {
                ForceDirty(from._dirtyStart, from._dirtyEnd - from._dirtyStart);
            }
        }

        /// <summary>
        /// Determine if a given region of the buffer has been modified, and must be flushed.
        /// </summary>
        /// <param name="address">The start address of the region</param>
        /// <param name="size">The size of the region</param>
        /// <returns></returns>
        public bool IsModified(ulong address, ulong size)
        {
            if (_modifiedRanges != null)
            {
                return _modifiedRanges.HasRange(address, size);
            }

            return false;
        }

        /// <summary>
        /// Clear the dirty range that overlaps with the given region.
        /// </summary>
        /// <param name="address">Start address of the modified region</param>
        /// <param name="size">Size of the modified region</param>
        private void ClearDirty(ulong address, ulong size)
        {
            if (_dirtyStart != ulong.MaxValue)
            {
                ulong end = address + size;

                if (end > _dirtyStart && address < _dirtyEnd)
                {
                    if (address <= _dirtyStart)
                    {
                        // Cut off the start.

                        if (end < _dirtyEnd)
                        {
                            _dirtyStart = end;
                        }
                        else
                        {
                            _dirtyStart = ulong.MaxValue;
                        }
                    }
                    else if (end >= _dirtyEnd)
                    {
                        // Cut off the end.

                        _dirtyEnd = address;
                    }

                    // If fully contained, do nothing.
                }
            }
        }

        /// <summary>
        /// Indicate that a region of the buffer was modified, and must be loaded from memory.
        /// </summary>
        /// <param name="mAddress">Start address of the modified region</param>
        /// <param name="mSize">Size of the modified region</param>
        private void RegionModified(ulong mAddress, ulong mSize)
        {
            if (mAddress < Address)
            {
                mAddress = Address;
            }

            ulong maxSize = Address + Size - mAddress;

            if (mSize > maxSize)
            {
                mSize = maxSize;
            }

            ClearDirty(mAddress, mSize);

            if (_modifiedRanges != null)
            {
                _modifiedRanges.ExcludeModifiedRegions(mAddress, mSize, _loadDelegate);
            }
            else
            {
                LoadRegion(mAddress, mSize);
            }
        }

        /// <summary>
        /// Load a region of the buffer from memory.
        /// </summary>
        /// <param name="mAddress">Start address of the modified region</param>
        /// <param name="mSize">Size of the modified region</param>
        private void LoadRegion(ulong mAddress, ulong mSize)
        {
            int offset = (int)(mAddress - Address);

            _context.Renderer.SetBufferData(Handle, offset, _physicalMemory.GetSpan(mAddress, (int)mSize));

            CopyToDependantVirtualBuffers(mAddress, mSize);
        }

        /// <summary>
        /// Force a region of the buffer to be dirty within the memory tracking. Avoids reprotection and nullifies sequence number check.
        /// </summary>
        /// <param name="mAddress">Start address of the modified region</param>
        /// <param name="mSize">Size of the region to force dirty</param>
        private void ForceTrackingDirty(ulong mAddress, ulong mSize)
        {
            if (_useGranular)
            {
                _memoryTrackingGranular.ForceDirty(mAddress, mSize);
            }
            else
            {
                _memoryTracking.ForceDirty();
                _sequenceNumber--;
            }
        }

        /// <summary>
        /// Force a region of the buffer to be dirty. Avoids reprotection and nullifies sequence number check.
        /// </summary>
        /// <param name="mAddress">Start address of the modified region</param>
        /// <param name="mSize">Size of the region to force dirty</param>
        public void ForceDirty(ulong mAddress, ulong mSize)
        {
            _modifiedRanges?.Clear(mAddress, mSize);

            ulong end = mAddress + mSize;

            if (_dirtyStart == ulong.MaxValue)
            {
                _dirtyStart = mAddress;
                _dirtyEnd = end;
            }
            else
            {
                // Is the new range more than a page away from the existing one?

                if ((long)(mAddress - _dirtyEnd) >= (long)MemoryManager.PageSize ||
                    (long)(_dirtyStart - end) >= (long)MemoryManager.PageSize)
                {
                    ForceTrackingDirty(mAddress, mSize);
                }
                else
                {
                    _dirtyStart = Math.Min(_dirtyStart, mAddress);
                    _dirtyEnd = Math.Max(_dirtyEnd, end);
                }
            }
        }

        /// <summary>
        /// Performs copy of all the buffer data from one buffer to another.
        /// </summary>
        /// <param name="destination">The destination buffer to copy the data into</param>
        /// <param name="dstOffset">The offset of the destination buffer to copy into</param>
        public void CopyTo(Buffer destination, int dstOffset)
        {
            CopyFromDependantVirtualBuffers();
            _context.Renderer.Pipeline.CopyBuffer(Handle, destination.Handle, 0, dstOffset, (int)Size);
        }

        /// <summary>
        /// Flushes a range of the buffer.
        /// This writes the range data back into guest memory.
        /// </summary>
        /// <param name="address">Start address of the range</param>
        /// <param name="size">Size in bytes of the range</param>
        public void Flush(ulong address, ulong size)
        {
            int offset = (int)(address - Address);

            using PinnedSpan<byte> data = _context.Renderer.GetBufferData(Handle, offset, (int)size);

            // TODO: When write tracking shaders, they will need to be aware of changes in overlapping buffers.
            _physicalMemory.WriteUntracked(address, CopyFromDependantVirtualBuffers(data.Get(), address, size));
        }

        /// <summary>
        /// Align a given address and size region to page boundaries.
        /// </summary>
        /// <param name="address">The start address of the region</param>
        /// <param name="size">The size of the region</param>
        /// <returns>The page aligned address and size</returns>
        private static (ulong address, ulong size) PageAlign(ulong address, ulong size)
        {
            ulong pageMask = MemoryManager.PageMask;
            ulong rA = address & ~pageMask;
            ulong rS = ((address + size + pageMask) & ~pageMask) - rA;
            return (rA, rS);
        }

        /// <summary>
        /// Flush modified ranges of the buffer from another thread.
        /// This will flush all modifications made before the active SyncNumber was set, and may block to wait for GPU sync.
        /// </summary>
        /// <param name="address">Address of the memory action</param>
        /// <param name="size">Size in bytes</param>
        public void ExternalFlush(ulong address, ulong size)
        {
            _context.Renderer.BackgroundContextAction(() =>
            {
                var ranges = _modifiedRanges;

                if (ranges != null)
                {
                    (address, size) = PageAlign(address, size);
                    ranges.WaitForAndFlushRanges(address, size);
                }
            }, true);
        }

        /// <summary>
        /// An action to be performed when a precise memory access occurs to this resource.
        /// For buffers, this skips flush-on-write by punching holes directly into the modified range list.
        /// </summary>
        /// <param name="address">Address of the memory action</param>
        /// <param name="size">Size in bytes</param>
        /// <param name="write">True if the access was a write, false otherwise</param>
        private bool PreciseAction(ulong address, ulong size, bool write)
        {
            if (!write)
            {
                // We only want to skip flush-on-write.
                return false;
            }

            ulong maxAddress = Math.Max(address, Address);
            ulong minEndAddress = Math.Min(address + size, Address + Size);

            if (maxAddress >= minEndAddress)
            {
                // Access doesn't overlap.
                return false;
            }

            ForceDirty(maxAddress, minEndAddress - maxAddress);

            return true;
        }

        /// <summary>
        /// Called when part of the memory for this buffer has been unmapped.
        /// Calls are from non-GPU threads.
        /// </summary>
        /// <param name="address">Start address of the unmapped region</param>
        /// <param name="size">Size of the unmapped region</param>
        public void Unmapped(ulong address, ulong size)
        {
            BufferModifiedRangeList modifiedRanges = _modifiedRanges;

            modifiedRanges?.Clear(address, size);

            UnmappedSequence++;
        }

        /// <summary>
        /// Adds a virtual buffer dependency, indicating that a virtual buffer depends on data from this buffer.
        /// </summary>
        /// <param name="virtualBuffer">Dependant virtual buffer</param>
        public void AddVirtualDependency(MultiRangeBuffer virtualBuffer)
        {
            _virtualDependenciesLock.EnterWriteLock();

            try
            {
                (_virtualDependencies ??= new()).Add(virtualBuffer);
            }
            finally
            {
                _virtualDependenciesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a virtual buffer dependency, indicating that a virtual buffer no longer depends on data from this buffer.
        /// </summary>
        /// <param name="virtualBuffer">Dependant virtual buffer</param>
        public void RemoveVirtualDependency(MultiRangeBuffer virtualBuffer)
        {
            _virtualDependenciesLock.EnterWriteLock();

            try
            {
                if (_virtualDependencies != null)
                {
                    _virtualDependencies.Remove(virtualBuffer);

                    if (_virtualDependencies.Count == 0)
                    {
                        _virtualDependencies = null;
                    }
                }
            }
            finally
            {
                _virtualDependenciesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Copies the buffer data to all virtual buffers that depends on it.
        /// </summary>
        public void CopyToDependantVirtualBuffers()
        {
            CopyToDependantVirtualBuffers(Address, Size);
        }

        /// <summary>
        /// Copies the buffer data inside the specifide range to all virtual buffers that depends on it.
        /// </summary>
        /// <param name="address">Address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        public void CopyToDependantVirtualBuffers(ulong address, ulong size)
        {
            if (_virtualDependencies != null)
            {
                foreach (var virtualBuffer in _virtualDependencies)
                {
                    CopyToDependantVirtualBuffer(virtualBuffer, address, size);
                }
            }
        }

        /// <summary>
        /// Copies all modified ranges from all virtual buffers back into this buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFromDependantVirtualBuffers()
        {
            if (_virtualDependencies != null)
            {
                CopyFromDependantVirtualBuffersImpl();
            }
        }

        /// <summary>
        /// Copies all modified ranges from all virtual buffers back into this buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CopyFromDependantVirtualBuffersImpl()
        {
            foreach (var virtualBuffer in _virtualDependencies.OrderBy(x => x.ModificationSequenceNumber))
            {
                virtualBuffer.ConsumeModifiedRegion(this, (mAddress, mSize) =>
                {
                    // Get offset inside both this and the virtual buffer.
                    // Note that sometimes there is no right answer for the virtual offset,
                    // as the same physical range might be mapped multiple times inside a virtual buffer.
                    // We just assume it does not happen in practice as it can only be implemented correctly
                    // when the host has support for proper sparse mapping.

                    ulong mEndAddress = mAddress + mSize;
                    mAddress = Math.Max(mAddress, Address);
                    mSize = Math.Min(mEndAddress, EndAddress) - mAddress;

                    int physicalOffset = (int)(mAddress - Address);
                    int virtualOffset = virtualBuffer.Range.FindOffset(new(mAddress, mSize));

                    _context.Renderer.Pipeline.CopyBuffer(virtualBuffer.Handle, Handle, virtualOffset, physicalOffset, (int)mSize);
                });
            }
        }

        /// <summary>
        /// Copies all overlapping modified ranges from all virtual buffers back into this buffer, and returns an updated span with the data.
        /// </summary>
        /// <param name="dataSpan">Span where the unmodified data will be taken from for the output</param>
        /// <param name="address">Address of the region to copy</param>
        /// <param name="size">Size of the region to copy in bytes</param>
        /// <returns>A span with <paramref name="dataSpan"/>, and the data for all modified ranges if any</returns>
        private ReadOnlySpan<byte> CopyFromDependantVirtualBuffers(ReadOnlySpan<byte> dataSpan, ulong address, ulong size)
        {
            _virtualDependenciesLock.EnterReadLock();

            try
            {
                if (_virtualDependencies != null)
                {
                    byte[] storage = dataSpan.ToArray();

                    foreach (var virtualBuffer in _virtualDependencies.OrderBy(x => x.ModificationSequenceNumber))
                    {
                        virtualBuffer.ConsumeModifiedRegion(address, size, (mAddress, mSize) =>
                        {
                            // Get offset inside both this and the virtual buffer.
                            // Note that sometimes there is no right answer for the virtual offset,
                            // as the same physical range might be mapped multiple times inside a virtual buffer.
                            // We just assume it does not happen in practice as it can only be implemented correctly
                            // when the host has support for proper sparse mapping.

                            ulong mEndAddress = mAddress + mSize;
                            mAddress = Math.Max(mAddress, address);
                            mSize = Math.Min(mEndAddress, address + size) - mAddress;

                            int physicalOffset = (int)(mAddress - Address);
                            int virtualOffset = virtualBuffer.Range.FindOffset(new(mAddress, mSize));

                            _context.Renderer.Pipeline.CopyBuffer(virtualBuffer.Handle, Handle, virtualOffset, physicalOffset, (int)size);
                            virtualBuffer.GetData(storage.AsSpan().Slice((int)(mAddress - address), (int)mSize), virtualOffset, (int)mSize);
                        });
                    }

                    dataSpan = storage;
                }
            }
            finally
            {
                _virtualDependenciesLock.ExitReadLock();
            }

            return dataSpan;
        }

        /// <summary>
        /// Copies the buffer data to the specified virtual buffer.
        /// </summary>
        /// <param name="virtualBuffer">Virtual buffer to copy the data into</param>
        public void CopyToDependantVirtualBuffer(MultiRangeBuffer virtualBuffer)
        {
            CopyToDependantVirtualBuffer(virtualBuffer, Address, Size);
        }

        /// <summary>
        /// Copies the buffer data inside the given range to the specified virtual buffer.
        /// </summary>
        /// <param name="virtualBuffer">Virtual buffer to copy the data into</param>
        /// <param name="address">Address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        public void CopyToDependantVirtualBuffer(MultiRangeBuffer virtualBuffer, ulong address, ulong size)
        {
            // Broadcast data to all ranges of the virtual buffer that are contained inside this buffer.

            ulong lastOffset = 0;

            while (virtualBuffer.TryGetPhysicalOffset(this, lastOffset, out ulong srcOffset, out ulong dstOffset, out ulong copySize))
            {
                ulong innerOffset = address - Address;
                ulong innerEndOffset = (address + size) - Address;

                lastOffset = dstOffset + copySize;

                // Clamp range to the specified range.
                ulong copySrcOffset = Math.Max(srcOffset, innerOffset);
                ulong copySrcEndOffset = Math.Min(innerEndOffset, srcOffset + copySize);

                if (copySrcEndOffset > copySrcOffset)
                {
                    copySize = copySrcEndOffset - copySrcOffset;
                    dstOffset += copySrcOffset - srcOffset;
                    srcOffset = copySrcOffset;

                    _context.Renderer.Pipeline.CopyBuffer(Handle, virtualBuffer.Handle, (int)srcOffset, (int)dstOffset, (int)copySize);
                }
            }
        }

        /// <summary>
        /// Increments the buffer reference count.
        /// </summary>
        public void IncrementReferenceCount()
        {
            _referenceCount++;
        }

        /// <summary>
        /// Decrements the buffer reference count.
        /// </summary>
        public void DecrementReferenceCount()
        {
            if (--_referenceCount == 0)
            {
                DisposeData();
            }
        }

        /// <summary>
        /// Disposes the host buffer's data, not its tracking handles.
        /// </summary>
        public void DisposeData()
        {
            _modifiedRanges?.Clear();

            _context.Renderer.DeleteBuffer(Handle);

            UnmappedSequence++;
        }

        /// <summary>
        /// Disposes the host buffer.
        /// </summary>
        public void Dispose()
        {
            _memoryTrackingGranular?.Dispose();
            _memoryTracking?.Dispose();

            DecrementReferenceCount();
        }
    }
}
