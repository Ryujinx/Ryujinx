using ARMeilleure.Memory;
using Ryujinx.Common;
using Ryujinx.Cpu.Signal;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using static Ryujinx.Cpu.MemoryEhMeilleure;

namespace ARMeilleure.Common
{
    /// <summary>
    /// Represents a table of guest address to a value.
    /// </summary>
    /// <typeparam name="TEntry">Type of the value</typeparam>
    public unsafe class AddressTable<TEntry> : IAddressTable<TEntry> where TEntry : unmanaged
    {
        /// <summary>
        /// Represents a page of the address table.
        /// </summary>
        private readonly struct AddressTablePage
        {
            /// <summary>
            /// True if the allocation belongs to a sparse block, false otherwise.
            /// </summary>
            public readonly bool IsSparse;

            /// <summary>
            /// Base address for the page.
            /// </summary>
            public readonly IntPtr Address;

            public AddressTablePage(bool isSparse, IntPtr address)
            {
                IsSparse = isSparse;
                Address = address;
            }
        }

        /// <summary>
        /// A sparsely mapped block of memory with a signal handler to map pages as they're accessed.
        /// </summary>
        private readonly struct TableSparseBlock : IDisposable
        {
            public readonly SparseMemoryBlock Block;
            private readonly TrackingEventDelegate _trackingEvent;

            public TableSparseBlock(ulong size, Action<IntPtr> ensureMapped, PageInitDelegate pageInit)
            {
                var block = new SparseMemoryBlock(size, pageInit, null);

                _trackingEvent = (ulong address, ulong size, bool write) =>
                {
                    ulong pointer = (ulong)block.Block.Pointer + address;

                    ensureMapped((IntPtr)pointer);

                    return pointer;
                };

                bool added = NativeSignalHandler.AddTrackedRegion(
                    (nuint)block.Block.Pointer,
                    (nuint)(block.Block.Pointer + (IntPtr)block.Block.Size),
                    Marshal.GetFunctionPointerForDelegate(_trackingEvent));

                if (!added)
                {
                    throw new InvalidOperationException("Number of allowed tracked regions exceeded.");
                }

                Block = block;
            }

            public void Dispose()
            {
                NativeSignalHandler.RemoveTrackedRegion((nuint)Block.Block.Pointer);

                Block.Dispose();
            }
        }

        private bool _disposed;
        private TEntry** _table;
        private readonly List<AddressTablePage> _pages;
        private TEntry _fill;

        private readonly MemoryBlock _sparseFill;
        private readonly SparseMemoryBlock _fillBottomLevel;
        private readonly TEntry* _fillBottomLevelPtr;

        private readonly List<TableSparseBlock> _sparseReserved;
        private readonly ReaderWriterLockSlim _sparseLock;

        private ulong _sparseBlockSize;
        private ulong _sparseReservedOffset;

        public bool Sparse { get; }

        /// <inheritdoc/>
        public ulong Mask { get; }

        /// <inheritdoc/>
        public AddressTableLevel[] Levels { get; }

        /// <inheritdoc/>
        public TEntry Fill
        {
            get
            {
                return _fill;
            }
            set
            {
                UpdateFill(value);
            }
        }

        /// <inheritdoc/>
        public IntPtr Base
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                lock (_pages)
                {
                    return (IntPtr)GetRootPage();
                }
            }
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="AddressTable{TEntry}"/> class with the specified list of
        /// <see cref="Level"/>.
        /// </summary>
        /// <param name="levels">Levels for the address table</param>
        /// <param name="sparse">True if the bottom page should be sparsely mapped</param>
        /// <exception cref="ArgumentNullException"><paramref name="levels"/> is null</exception>
        /// <exception cref="ArgumentException">Length of <paramref name="levels"/> is less than 2</exception>
        public AddressTable(AddressTableLevel[] levels, bool sparse)
        {
            ArgumentNullException.ThrowIfNull(levels);

            if (levels.Length < 2)
            {
                throw new ArgumentException("Table must be at least 2 levels deep.", nameof(levels));
            }

            _pages = new List<AddressTablePage>(capacity: 16);

            Levels = levels;
            Mask = 0;

            foreach (var level in Levels)
            {
                Mask |= level.Mask;
            }

            Sparse = sparse;

            if (sparse)
            {
                // If the address table is sparse, allocate a fill block

                _sparseFill = new MemoryBlock(65536, MemoryAllocationFlags.Mirrorable);

                ulong bottomLevelSize = (1ul << levels.Last().Length) * (ulong)sizeof(TEntry);

                _fillBottomLevel = new SparseMemoryBlock(bottomLevelSize, null, _sparseFill);
                _fillBottomLevelPtr = (TEntry*)_fillBottomLevel.Block.Pointer;

                _sparseReserved = new List<TableSparseBlock>();
                _sparseLock = new ReaderWriterLockSlim();

                _sparseBlockSize = bottomLevelSize;
            }
        }

        /// <summary>
        /// Create an <see cref="AddressTable{TEntry}"/> instance for an ARM function table.
        /// Selects the best table structure for A32/A64, taking into account the selected memory manager type.
        /// </summary>
        /// <param name="for64Bits">True if the guest is A64, false otherwise</param>
        /// <param name="type">Memory manager type</param>
        /// <returns>An <see cref="AddressTable{TEntry}"/> for ARM function lookup</returns>
        public static AddressTable<TEntry> CreateForArm(bool for64Bits, MemoryManagerType type)
        {
            // Assume software memory means that we don't want to use any signal handlers.
            bool sparse = type != MemoryManagerType.SoftwareMmu && type != MemoryManagerType.SoftwarePageTable;

            return new AddressTable<TEntry>(AddressTablePresets.GetArmPreset(for64Bits, sparse), sparse);
        }

        /// <summary>
        /// Update the fill value for the bottom level of the table.
        /// </summary>
        /// <param name="fillValue">New fill value</param>
        private void UpdateFill(TEntry fillValue)
        {
            if (_sparseFill != null)
            {
                Span<byte> span = _sparseFill.GetSpan(0, (int)_sparseFill.Size);
                MemoryMarshal.Cast<byte, TEntry>(span).Fill(fillValue);
            }

            _fill = fillValue;
        }

        /// <summary>
        /// Signal that the given code range exists.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        public void SignalCodeRange(ulong address, ulong size)
        {
            AddressTableLevel bottom = Levels.Last();
            ulong bottomLevelEntries = 1ul << bottom.Length;

            ulong entryIndex = address >> bottom.Index;
            ulong entries = size >> bottom.Index;
            entries += entryIndex - BitUtils.AlignDown(entryIndex, bottomLevelEntries);

            _sparseBlockSize = Math.Max(_sparseBlockSize, BitUtils.AlignUp(entries, bottomLevelEntries) * (ulong)sizeof(TEntry));
        }

        /// <inheritdoc/>
        public bool IsValid(ulong address)
        {
            return (address & ~Mask) == 0;
        }

        /// <inheritdoc/>
        public ref TEntry GetValue(ulong address)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsValid(address))
            {
                throw new ArgumentException($"Address 0x{address:X} is not mapped onto the table.", nameof(address));
            }

            lock (_pages)
            {
                TEntry* page = GetPage(address);

                int index = Levels[^1].GetValue(address);

                EnsureMapped((IntPtr)(page + index));

                return ref page[index];
            }
        }

        /// <summary>
        /// Gets the leaf page for the specified guest <paramref name="address"/>.
        /// </summary>
        /// <param name="address">Guest address</param>
        /// <returns>Leaf page for the specified guest <paramref name="address"/></returns>
        private TEntry* GetPage(ulong address)
        {
            TEntry** page = GetRootPage();

            for (int i = 0; i < Levels.Length - 1; i++)
            {
                ref AddressTableLevel level = ref Levels[i];
                ref TEntry* nextPage = ref page[level.GetValue(address)];

                if (nextPage == null || nextPage == _fillBottomLevelPtr)
                {
                    ref AddressTableLevel nextLevel = ref Levels[i + 1];

                    if (i == Levels.Length - 2)
                    {
                        nextPage = (TEntry*)Allocate(1 << nextLevel.Length, Fill, leaf: true);
                    }
                    else
                    {
                        nextPage = (TEntry*)Allocate(1 << nextLevel.Length, GetFillValue(i), leaf: false);
                    }
                }

                page = (TEntry**)nextPage;
            }

            return (TEntry*)page;
        }

        /// <summary>
        /// Ensure the given pointer is mapped in any overlapping sparse reservations.
        /// </summary>
        /// <param name="ptr">Pointer to be mapped</param>
        private void EnsureMapped(IntPtr ptr)
        {
            if (Sparse)
            {
                // Check sparse allocations to see if the pointer is in any of them.
                // Ensure the page is committed if there's a match.

                _sparseLock.EnterReadLock();

                try
                {
                    foreach (TableSparseBlock reserved in _sparseReserved)
                    {
                        SparseMemoryBlock sparse = reserved.Block;

                        if (ptr >= sparse.Block.Pointer && ptr < sparse.Block.Pointer + (IntPtr)sparse.Block.Size)
                        {
                            sparse.EnsureMapped((ulong)(ptr - sparse.Block.Pointer));

                            break;
                        }
                    }
                }
                finally
                {
                    _sparseLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Get the fill value for a non-leaf level of the table.
        /// </summary>
        /// <param name="level">Level to get the fill value for</param>
        /// <returns>The fill value</returns>
        private IntPtr GetFillValue(int level)
        {
            if (_fillBottomLevel != null && level == Levels.Length - 2)
            {
                return (IntPtr)_fillBottomLevelPtr;
            }
            else
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Lazily initialize and get the root page of the <see cref="AddressTable{TEntry}"/>.
        /// </summary>
        /// <returns>Root page of the <see cref="AddressTable{TEntry}"/></returns>
        private TEntry** GetRootPage()
        {
            if (_table == null)
            {
                _table = (TEntry**)Allocate(1 << Levels[0].Length, GetFillValue(0), leaf: false);
            }

            return _table;
        }

        /// <summary>
        /// Initialize a leaf page with the fill value.
        /// </summary>
        /// <param name="page">Page to initialize</param>
        private void InitLeafPage(Span<byte> page)
        {
            MemoryMarshal.Cast<byte, TEntry>(page).Fill(_fill);
        }

        /// <summary>
        /// Reserve a new sparse block, and add it to the list.
        /// </summary>
        /// <returns>The new sparse block that was added</returns>
        private TableSparseBlock ReserveNewSparseBlock()
        {
            var block = new TableSparseBlock(_sparseBlockSize, EnsureMapped, InitLeafPage);

            _sparseReserved.Add(block);
            _sparseReservedOffset = 0;

            return block;
        }

        /// <summary>
        /// Allocates a block of memory of the specified type and length.
        /// </summary>
        /// <typeparam name="T">Type of elements</typeparam>
        /// <param name="length">Number of elements</param>
        /// <param name="fill">Fill value</param>
        /// <param name="leaf"><see langword="true"/> if leaf; otherwise <see langword="false"/></param>
        /// <returns>Allocated block</returns>
        private IntPtr Allocate<T>(int length, T fill, bool leaf) where T : unmanaged
        {
            var size = sizeof(T) * length;

            AddressTablePage page;

            if (Sparse && leaf)
            {
                _sparseLock.EnterWriteLock();

                SparseMemoryBlock block;

                if (_sparseReserved.Count == 0)
                {
                    block = ReserveNewSparseBlock().Block;
                }
                else
                {
                    block = _sparseReserved.Last().Block;

                    if (_sparseReservedOffset == block.Block.Size)
                    {
                        block = ReserveNewSparseBlock().Block;
                    }
                }

                page = new AddressTablePage(true, block.Block.Pointer + (IntPtr)_sparseReservedOffset);

                _sparseReservedOffset += (ulong)size;

                _sparseLock.ExitWriteLock();
            }
            else
            {
                var address = (IntPtr)NativeAllocator.Instance.Allocate((uint)size);
                page = new AddressTablePage(false, address);

                var span = new Span<T>((void*)page.Address, length);
                span.Fill(fill);
            }

            _pages.Add(page);

            //TranslatorEventSource.Log.AddressTableAllocated(size, leaf);

            return page.Address;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="AddressTable{TEntry}"/> instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all unmanaged and optionally managed resources used by the <see cref="AddressTable{TEntry}"/>
        /// instance.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to dispose managed resources also; otherwise just unmanaged resouces</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                foreach (var page in _pages)
                {
                    if (!page.IsSparse)
                    {
                        Marshal.FreeHGlobal(page.Address);
                    }
                }

                if (Sparse)
                {
                    foreach (TableSparseBlock block in _sparseReserved)
                    {
                        block.Dispose();
                    }

                    _sparseReserved.Clear();

                    _fillBottomLevel.Dispose();
                    _sparseFill.Dispose();
                    _sparseLock.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Frees resources used by the <see cref="AddressTable{TEntry}"/> instance.
        /// </summary>
        ~AddressTable()
        {
            Dispose(false);
        }
    }
}
