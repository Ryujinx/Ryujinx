using Ryujinx.Memory.Range;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Memory
{
    /// <summary>
    /// Virtual range cache.
    /// </summary>
    class VirtualRangeCache
    {
        private readonly MemoryManager _memoryManager;

        /// <summary>
        /// Represents a GPU virtual memory range.
        /// </summary>
        private readonly struct VirtualRange : IRange
        {
            /// <summary>
            /// GPU virtual address where the range starts.
            /// </summary>
            public ulong Address { get; }

            /// <summary>
            /// Size of the range in bytes.
            /// </summary>
            public ulong Size { get; }

            /// <summary>
            /// GPU virtual address where the range ends.
            /// </summary>
            public ulong EndAddress => Address + Size;

            /// <summary>
            /// Physical regions where the GPU virtual region is mapped.
            /// </summary>
            public MultiRange Range { get; }

            /// <summary>
            /// Creates a new virtual memory range.
            /// </summary>
            /// <param name="address">GPU virtual address where the range starts</param>
            /// <param name="size">Size of the range in bytes</param>
            /// <param name="range">Physical regions where the GPU virtual region is mapped</param>
            public VirtualRange(ulong address, ulong size, MultiRange range)
            {
                Address = address;
                Size = size;
                Range = range;
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
        }

        private readonly RangeList<VirtualRange> _virtualRanges;
        private VirtualRange[] _virtualRangeOverlaps;
        private readonly ConcurrentQueue<VirtualRange> _deferredUnmaps;
        private int _hasDeferredUnmaps;

        /// <summary>
        /// Creates a new instance of the virtual range cache.
        /// </summary>
        /// <param name="memoryManager">Memory manager that the virtual range cache belongs to</param>
        public VirtualRangeCache(MemoryManager memoryManager)
        {
            _memoryManager = memoryManager;
            _virtualRanges = new RangeList<VirtualRange>();
            _virtualRangeOverlaps = new VirtualRange[BufferCache.OverlapsBufferInitialCapacity];
            _deferredUnmaps = new ConcurrentQueue<VirtualRange>();
        }

        /// <summary>
        /// Handles removal of buffers written to a memory region being unmapped.
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        public void MemoryUnmappedHandler(object sender, UnmapEventArgs e)
        {
            void EnqueueUnmap()
            {
                _deferredUnmaps.Enqueue(new VirtualRange(e.Address, e.Size, default));

                Interlocked.Exchange(ref _hasDeferredUnmaps, 1);
            }

            e.AddRemapAction(EnqueueUnmap);
        }

        /// <summary>
        /// Tries to get a existing, cached physical range for the specified virtual region.
        /// If no cached range is found, a new one is created and added.
        /// </summary>
        /// <param name="gpuVa">GPU virtual address to get the physical range from</param>
        /// <param name="size">Size in bytes of the region</param>
        /// <param name="range">Physical range for the specified GPU virtual region</param>
        /// <returns>True if the range already existed, false if a new one was created and added</returns>
        public bool TryGetOrAddRange(ulong gpuVa, ulong size, out MultiRange range)
        {
            VirtualRange[] overlaps = _virtualRangeOverlaps;
            int overlapsCount;

            if (Interlocked.Exchange(ref _hasDeferredUnmaps, 0) != 0)
            {
                while (_deferredUnmaps.TryDequeue(out VirtualRange unmappedRange))
                {
                    overlapsCount = _virtualRanges.FindOverlapsNonOverlapping(unmappedRange.Address, unmappedRange.Size, ref overlaps);

                    for (int index = 0; index < overlapsCount; index++)
                    {
                        _virtualRanges.Remove(overlaps[index]);
                    }
                }
            }

            bool found = false;

            ulong originalVa = gpuVa;

            overlapsCount = _virtualRanges.FindOverlapsNonOverlapping(gpuVa, size, ref overlaps);

            if (overlapsCount != 0)
            {
                // The virtual range already exists. We just need to check if our range fits inside
                // the existing one, and if not, we must extend the existing one.

                ulong endAddress = gpuVa + size;
                VirtualRange overlap0 = overlaps[0];

                if (overlap0.Address > gpuVa || overlap0.EndAddress < endAddress)
                {
                    for (int index = 0; index < overlapsCount; index++)
                    {
                        VirtualRange virtualRange = overlaps[index];

                        gpuVa = Math.Min(gpuVa, virtualRange.Address);
                        endAddress = Math.Max(endAddress, virtualRange.EndAddress);

                        _virtualRanges.Remove(virtualRange);
                    }

                    ulong newSize = endAddress - gpuVa;
                    MultiRange newRange = _memoryManager.GetPhysicalRegions(gpuVa, newSize);

                    _virtualRanges.Add(new(gpuVa, newSize, newRange));

                    range = newRange.Slice(originalVa - gpuVa, size);
                }
                else
                {
                    found = overlap0.Range.Count == 1 || IsSparseAligned(overlap0.Range);
                    range = overlap0.Range.Slice(gpuVa - overlap0.Address, size);
                }
            }
            else
            {
                // No overlap, just create a new virtual range.
                range = _memoryManager.GetPhysicalRegions(gpuVa, size);

                VirtualRange virtualRange = new(gpuVa, size, range);

                _virtualRanges.Add(virtualRange);
            }

            ShrinkOverlapsBufferIfNeeded();

            // If the range is not properly aligned for sparse mapping,
            // let's just force it to a single range.
            // This might cause issues in some applications that uses sparse
            // mappings.
            if (!IsSparseAligned(range))
            {
                range = new MultiRange(range.GetSubRange(0).Address, size);
            }

            return found;
        }

        /// <summary>
        /// Checks if the physical memory ranges are valid for sparse mapping,
        /// which requires all sub-ranges to be 64KB aligned.
        /// </summary>
        /// <param name="range">Range to check</param>
        /// <returns>True if the range is valid for sparse mapping, false otherwise</returns>
        private static bool IsSparseAligned(MultiRange range)
        {
            if (range.Count == 1)
            {
                return (range.GetSubRange(0).Address & (BufferCache.SparseBufferAlignmentSize - 1)) == 0;
            }

            for (int i = 0; i < range.Count; i++)
            {
                MemoryRange subRange = range.GetSubRange(i);

                // Check if address is aligned. The address of the first sub-range can
                // be misaligned as it is at the start.
                if (i > 0 &&
                    subRange.Address != MemoryManager.PteUnmapped &&
                    (subRange.Address & (BufferCache.SparseBufferAlignmentSize - 1)) != 0)
                {
                    return false;
                }

                // Check if the size is aligned. The size of the last sub-range can
                // be misaligned as it is at the end.
                if (i < range.Count - 1 && (subRange.Size & (BufferCache.SparseBufferAlignmentSize - 1)) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Resizes the temporary buffer used for range list intersection results, if it has grown too much.
        /// </summary>
        private void ShrinkOverlapsBufferIfNeeded()
        {
            if (_virtualRangeOverlaps.Length > BufferCache.OverlapsBufferMaxCapacity)
            {
                Array.Resize(ref _virtualRangeOverlaps, BufferCache.OverlapsBufferMaxCapacity);
            }
        }
    }
}
