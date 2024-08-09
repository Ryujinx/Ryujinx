using Ryujinx.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Ryujinx.Memory
{
    public delegate void PageInitDelegate(Span<byte> page);

    public class SparseMemoryBlock : IDisposable
    {
        private const ulong MapGranularity = 1UL << 17;

        private readonly PageInitDelegate _pageInit;

        private readonly object _lock = new object();
        private readonly ulong _pageSize;
        private readonly MemoryBlock _reservedBlock;
        private readonly List<MemoryBlock> _mappedBlocks;
        private ulong _mappedBlockUsage;

        private readonly ulong[] _mappedPageBitmap;

        public MemoryBlock Block => _reservedBlock;

        public SparseMemoryBlock(ulong size, PageInitDelegate pageInit, MemoryBlock fill)
        {
            _pageSize = MemoryBlock.GetPageSize();
            _reservedBlock = new MemoryBlock(size, MemoryAllocationFlags.Reserve | MemoryAllocationFlags.ViewCompatible);
            _mappedBlocks = new List<MemoryBlock>();
            _pageInit = pageInit;

            int pages = (int)BitUtils.DivRoundUp(size, _pageSize);
            int bitmapEntries = BitUtils.DivRoundUp(pages, 64);
            _mappedPageBitmap = new ulong[bitmapEntries];

            if (fill != null)
            {
                // Fill the block with mappings from the fill block.

                if (fill.Size % _pageSize != 0)
                {
                    throw new ArgumentException("Fill memory block should be page aligned.", nameof(fill));
                }

                int repeats = (int)BitUtils.DivRoundUp(size, fill.Size);

                ulong offset = 0;
                for (int i = 0; i < repeats; i++)
                {
                    _reservedBlock.MapView(fill, 0, offset, Math.Min(fill.Size, size - offset));
                    offset += fill.Size;
                }
            }

            // If a fill block isn't provided, the pages that aren't EnsureMapped are unmapped.
            // The caller can rely on signal handler to fill empty pages instead.
        }

        private void MapPage(ulong pageOffset)
        {
            // Take a page from the latest mapped block.
            MemoryBlock block = _mappedBlocks.LastOrDefault();

            if (block == null || _mappedBlockUsage == MapGranularity)
            {
                // Need to map some more memory.

                block = new MemoryBlock(MapGranularity, MemoryAllocationFlags.Mirrorable);

                _mappedBlocks.Add(block);

                _mappedBlockUsage = 0;
            }

            _pageInit(block.GetSpan(_mappedBlockUsage, (int)_pageSize));
            _reservedBlock.MapView(block, _mappedBlockUsage, pageOffset, _pageSize);

            _mappedBlockUsage += _pageSize;
        }

        public void EnsureMapped(ulong offset)
        {
            int pageIndex = (int)(offset / _pageSize);
            int bitmapIndex = pageIndex >> 6;

            ref ulong entry = ref _mappedPageBitmap[bitmapIndex];
            ulong bit = 1UL << (pageIndex & 63);

            if ((Volatile.Read(ref entry) & bit) == 0)
            {
                // Not mapped.

                lock (_lock)
                {
                    // Check the bit while locked to make sure that this only happens once.

                    ulong lockedEntry = Volatile.Read(ref entry);

                    if ((lockedEntry & bit) == 0)
                    {
                        MapPage(offset & ~(_pageSize - 1));

                        lockedEntry |= bit;

                        Interlocked.Exchange(ref entry, lockedEntry);
                    }
                }
            }
        }

        public void Dispose()
        {
            _reservedBlock.Dispose();

            foreach (MemoryBlock block in _mappedBlocks)
            {
                block.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
