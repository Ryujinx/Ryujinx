using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ryujinx.Memory.Tracking
{
    /// <summary>
    /// A region handle that tracks a large region using many smaller handles, to provide
    /// granular tracking that can be used to track partial updates. Backed by a bitmap
    /// to improve performance when scanning large regions.
    /// </summary>
    public class MultiRegionHandle : IMultiRegionHandle
    {
        /// <summary>
        /// A list of region handles for each granularity sized chunk of the whole region.
        /// </summary>
        private readonly RegionHandle[] _handles;
        private readonly ulong Address;
        private readonly ulong Granularity;
        private readonly ulong Size;

        private readonly ConcurrentBitmap _dirtyBitmap;

        private int _sequenceNumber;
        private readonly BitMap _sequenceNumberBitmap;
        private readonly BitMap _dirtyCheckedBitmap;
        private int _uncheckedHandles;

        public bool Dirty { get; private set; } = true;

        internal MultiRegionHandle(
            MemoryTracking tracking,
            ulong address,
            ulong size,
            IEnumerable<IRegionHandle> handles,
            ulong granularity,
            int id,
            RegionFlags flags)
        {
            _handles = new RegionHandle[(size + granularity - 1) / granularity];
            Granularity = granularity;

            _dirtyBitmap = new ConcurrentBitmap(_handles.Length, true);
            _sequenceNumberBitmap = new BitMap(_handles.Length);
            _dirtyCheckedBitmap = new BitMap(_handles.Length);

            int i = 0;

            if (handles != null)
            {
                // Inherit from the handles we were given. Any gaps must be filled with new handles,
                // and old handles larger than our granularity must copy their state onto new granular handles and dispose.
                // It is assumed that the provided handles do not overlap, in order, are on page boundaries,
                // and don't extend past the requested range.

                foreach (RegionHandle handle in handles.Cast<RegionHandle>())
                {
                    int startIndex = (int)((handle.RealAddress - address) / granularity);

                    // Fill any gap left before this handle.
                    while (i < startIndex)
                    {
                        RegionHandle fillHandle = tracking.BeginTrackingBitmap(address + (ulong)i * granularity, granularity, _dirtyBitmap, i, id, flags);
                        fillHandle.Parent = this;
                        _handles[i++] = fillHandle;
                    }

                    lock (tracking.TrackingLock)
                    {
                        if (handle is RegionHandle bitHandle && handle.Size == granularity)
                        {
                            handle.Parent = this;

                            bitHandle.ReplaceBitmap(_dirtyBitmap, i);

                            _handles[i++] = bitHandle;
                        }
                        else
                        {
                            int endIndex = (int)((handle.RealEndAddress - address) / granularity);

                            while (i < endIndex)
                            {
                                RegionHandle splitHandle = tracking.BeginTrackingBitmap(address + (ulong)i * granularity, granularity, _dirtyBitmap, i, id, flags);
                                splitHandle.Parent = this;

                                splitHandle.Reprotect(handle.Dirty);

                                RegionSignal signal = handle.PreAction;
                                if (signal != null)
                                {
                                    splitHandle.RegisterAction(signal);
                                }

                                _handles[i++] = splitHandle;
                            }

                            handle.Dispose();
                        }
                    }
                }
            }

            // Fill any remaining space with new handles.
            while (i < _handles.Length)
            {
                RegionHandle handle = tracking.BeginTrackingBitmap(address + (ulong)i * granularity, granularity, _dirtyBitmap, i, id, flags);
                handle.Parent = this;
                _handles[i++] = handle;
            }

            _uncheckedHandles = _handles.Length;

            Address = address;
            Size = size;
        }

        public void SignalWrite()
        {
            Dirty = true;
        }

        public IEnumerable<RegionHandle> GetHandles()
        {
            return _handles;
        }

        public void ForceDirty(ulong address, ulong size)
        {
            Dirty = true;

            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            for (int i = startHandle; i <= lastHandle; i++)
            {
                if (_sequenceNumberBitmap.Clear(i))
                {
                    _uncheckedHandles++;
                }

                _handles[i].ForceDirty();
            }
        }

        public void QueryModified(Action<ulong, ulong> modifiedAction)
        {
            if (!Dirty)
            {
                return;
            }

            Dirty = false;

            QueryModified(Address, Size, modifiedAction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseDirtyBits(long dirtyBits, ref int baseBit, ref int prevHandle, ref ulong rgStart, ref ulong rgSize, Action<ulong, ulong> modifiedAction)
        {
            while (dirtyBits != 0)
            {
                int bit = BitOperations.TrailingZeroCount(dirtyBits);

                dirtyBits &= ~(1L << bit);

                int handleIndex = baseBit + bit;

                RegionHandle handle = _handles[handleIndex];

                if (handleIndex != prevHandle + 1)
                {
                    // Submit handles scanned until the gap as dirty
                    if (rgSize != 0)
                    {
                        modifiedAction(rgStart, rgSize);
                        rgSize = 0;
                    }

                    rgStart = handle.RealAddress;
                }

                if (handle.Dirty)
                {
                    rgSize += handle.RealSize;
                    handle.Reprotect();
                }

                prevHandle = handleIndex;
            }

            baseBit += ConcurrentBitmap.IntSize;
        }

        public void QueryModified(ulong address, ulong size, Action<ulong, ulong> modifiedAction)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            ulong rgStart = Address + (ulong)startHandle * Granularity;

            if (startHandle == lastHandle)
            {
                RegionHandle handle = _handles[startHandle];

                if (handle.Dirty)
                {
                    handle.Reprotect();
                    modifiedAction(rgStart, handle.RealSize);
                }

                return;
            }

            ulong rgSize = 0;

            long[] masks = _dirtyBitmap.Masks;

            int startIndex = startHandle >> ConcurrentBitmap.IntShift;
            int startBit = startHandle & ConcurrentBitmap.IntMask;
            long startMask = -1L << startBit;

            int endIndex = lastHandle >> ConcurrentBitmap.IntShift;
            int endBit = lastHandle & ConcurrentBitmap.IntMask;
            long endMask = (long)(ulong.MaxValue >> (ConcurrentBitmap.IntMask - endBit));

            long startValue = Volatile.Read(ref masks[startIndex]);

            int baseBit = startIndex << ConcurrentBitmap.IntShift;
            int prevHandle = startHandle - 1;

            if (startIndex == endIndex)
            {
                ParseDirtyBits(startValue & startMask & endMask, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }
            else
            {
                ParseDirtyBits(startValue & startMask, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    ParseDirtyBits(Volatile.Read(ref masks[i]), ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
                }

                long endValue = Volatile.Read(ref masks[endIndex]);

                ParseDirtyBits(endValue & endMask, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }

            if (rgSize != 0)
            {
                modifiedAction(rgStart, rgSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseDirtyBits(long dirtyBits, long mask, int index, long[] seqMasks, long[] checkMasks, ref int baseBit, ref int prevHandle, ref ulong rgStart, ref ulong rgSize, Action<ulong, ulong> modifiedAction)
        {
            long seqMask = mask & ~seqMasks[index];
            long checkMask = (~dirtyBits) & seqMask;
            dirtyBits &= seqMask;

            while (dirtyBits != 0)
            {
                int bit = BitOperations.TrailingZeroCount(dirtyBits);
                long bitValue = 1L << bit;

                dirtyBits &= ~bitValue;

                int handleIndex = baseBit + bit;

                RegionHandle handle = _handles[handleIndex];

                if (handleIndex != prevHandle + 1)
                {
                    // Submit handles scanned until the gap as dirty
                    if (rgSize != 0)
                    {
                        modifiedAction(rgStart, rgSize);
                        rgSize = 0;
                    }
                    rgStart = handle.RealAddress;
                }

                rgSize += handle.RealSize;
                handle.Reprotect(false, (checkMasks[index] & bitValue) == 0);

                checkMasks[index] &= ~bitValue;

                prevHandle = handleIndex;
            }

            checkMasks[index] |= checkMask;
            seqMasks[index] |= mask;
            _uncheckedHandles -= BitOperations.PopCount((ulong)seqMask);

            baseBit += ConcurrentBitmap.IntSize;
        }

        public void QueryModified(ulong address, ulong size, Action<ulong, ulong> modifiedAction, int sequenceNumber)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            ulong rgStart = Address + (ulong)startHandle * Granularity;

            if (sequenceNumber != _sequenceNumber)
            {
                if (_uncheckedHandles != _handles.Length)
                {
                    _sequenceNumberBitmap.Clear();
                    _uncheckedHandles = _handles.Length;
                }

                _sequenceNumber = sequenceNumber;
            }

            if (startHandle == lastHandle)
            {
                var handle = _handles[startHandle];
                if (_sequenceNumberBitmap.Set(startHandle))
                {
                    _uncheckedHandles--;

                    if (handle.DirtyOrVolatile())
                    {
                        handle.Reprotect();

                        modifiedAction(rgStart, handle.RealSize);
                    }
                }

                return;
            }

            if (_uncheckedHandles == 0)
            {
                return;
            }

            ulong rgSize = 0;

            long[] seqMasks = _sequenceNumberBitmap.Masks;
            long[] checkedMasks = _dirtyCheckedBitmap.Masks;
            long[] masks = _dirtyBitmap.Masks;

            int startIndex = startHandle >> ConcurrentBitmap.IntShift;
            int startBit = startHandle & ConcurrentBitmap.IntMask;
            long startMask = -1L << startBit;

            int endIndex = lastHandle >> ConcurrentBitmap.IntShift;
            int endBit = lastHandle & ConcurrentBitmap.IntMask;
            long endMask = (long)(ulong.MaxValue >> (ConcurrentBitmap.IntMask - endBit));

            long startValue = Volatile.Read(ref masks[startIndex]);

            int baseBit = startIndex << ConcurrentBitmap.IntShift;
            int prevHandle = startHandle - 1;

            if (startIndex == endIndex)
            {
                ParseDirtyBits(startValue, startMask & endMask, startIndex, seqMasks, checkedMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }
            else
            {
                ParseDirtyBits(startValue, startMask, startIndex, seqMasks, checkedMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);

                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    ParseDirtyBits(Volatile.Read(ref masks[i]), -1L, i, seqMasks, checkedMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
                }

                long endValue = Volatile.Read(ref masks[endIndex]);

                ParseDirtyBits(endValue, endMask, endIndex, seqMasks, checkedMasks, ref baseBit, ref prevHandle, ref rgStart, ref rgSize, modifiedAction);
            }

            if (rgSize != 0)
            {
                modifiedAction(rgStart, rgSize);
            }
        }

        public void RegisterAction(ulong address, ulong size, RegionSignal action)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            for (int i = startHandle; i <= lastHandle; i++)
            {
                _handles[i].RegisterAction(action);
            }
        }

        public void RegisterPreciseAction(ulong address, ulong size, PreciseRegionSignal action)
        {
            int startHandle = (int)((address - Address) / Granularity);
            int lastHandle = (int)((address + (size - 1) - Address) / Granularity);

            for (int i = startHandle; i <= lastHandle; i++)
            {
                _handles[i].RegisterPreciseAction(action);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            foreach (var handle in _handles)
            {
                handle.Dispose();
            }
        }
    }
}
