using Ryujinx.Common.Memory;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Memory
{
    public abstract class VirtualMemoryManagerBase : IWritableBlock
    {
        public const int PageBits = 12;
        public const int PageSize = 1 << PageBits;
        public const int PageMask = PageSize - 1;

        protected abstract ulong AddressSpaceSize { get; }

        public virtual ReadOnlySequence<byte> GetReadOnlySequence(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return ReadOnlySequence<byte>.Empty;
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, false);
            }

            if (IsContiguousAndMapped(va, size))
            {
                nuint pa = TranslateVirtualAddressUnchecked(va);

                return new ReadOnlySequence<byte>(GetPhysicalAddressMemory(pa, size));
            }
            else
            {
                AssertValidAddressAndSize(va, size);

                int offset = 0, segmentSize;

                BytesReadOnlySequenceSegment first = null, last = null;

                if ((va & PageMask) != 0)
                {
                    nuint pa = TranslateVirtualAddressChecked(va);

                    segmentSize = Math.Min(size, PageSize - (int)(va & PageMask));

                    Memory<byte> memory = GetPhysicalAddressMemory(pa, segmentSize);

                    first = last = new BytesReadOnlySequenceSegment(memory);

                    offset += segmentSize;
                }

                for (; offset < size; offset += segmentSize)
                {
                    nuint pa = TranslateVirtualAddressChecked(va + (ulong)offset);

                    segmentSize = Math.Min(size - offset, PageSize);

                    Memory<byte> memory = GetPhysicalAddressMemory(pa, segmentSize);

                    if (first is null)
                    {
                        first = last = new BytesReadOnlySequenceSegment(memory);
                    }
                    else
                    {
                        if (last.IsContiguousWith(memory, out nuint contiguousStart, out int contiguousSize))
                        {
                            last.Replace(GetPhysicalAddressMemory(contiguousStart, contiguousSize));
                        }
                        else
                        {
                            last = last.Append(memory);
                        }
                    }
                }

                return new ReadOnlySequence<byte>(first, 0, last, (int)(size - last.RunningIndex));
            }
        }

        public virtual ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, false);
            }

            if (IsContiguousAndMapped(va, size))
            {
                nuint pa = TranslateVirtualAddressUnchecked(va);

                return GetPhysicalAddressSpan(pa, size);
            }
            else
            {
                Span<byte> data = new byte[size];

                Read(va, data);

                return data;
            }
        }

        public virtual WritableRegion GetWritableRegion(ulong va, int size, bool tracked = false)
        {
            if (size == 0)
            {
                return new WritableRegion(null, va, Memory<byte>.Empty);
            }

            if (tracked)
            {
                SignalMemoryTracking(va, (ulong)size, true);
            }

            if (IsContiguousAndMapped(va, size))
            {
                nuint pa = TranslateVirtualAddressUnchecked(va);

                return new WritableRegion(null, va, GetPhysicalAddressMemory(pa, size));
            }
            else
            {
                IMemoryOwner<byte> memoryOwner = ByteMemoryPool.Rent(size);

                Read(va, memoryOwner.Memory.Span);

                return new WritableRegion(this, va, memoryOwner);
            }
        }

        public abstract bool IsMapped(ulong va);

        public virtual void MapForeign(ulong va, nuint hostPointer, ulong size)
        {
            throw new NotSupportedException();
        }

        public virtual T Read<T>(ulong va) where T : unmanaged
        {
            return MemoryMarshal.Cast<byte, T>(GetSpan(va, Unsafe.SizeOf<T>()))[0];
        }

        public virtual void Read(ulong va, Span<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            AssertValidAddressAndSize(va, data.Length);

            int offset = 0, size;

            if ((va & PageMask) != 0)
            {
                nuint pa = TranslateVirtualAddressChecked(va);

                size = Math.Min(data.Length, PageSize - (int)(va & PageMask));

                GetPhysicalAddressSpan(pa, size).CopyTo(data[..size]);

                offset += size;
            }

            for (; offset < data.Length; offset += size)
            {
                nuint pa = TranslateVirtualAddressChecked(va + (ulong)offset);

                size = Math.Min(data.Length - offset, PageSize);

                GetPhysicalAddressSpan(pa, size).CopyTo(data.Slice(offset, size));
            }
        }

        public virtual T ReadTracked<T>(ulong va) where T : unmanaged
        {
            SignalMemoryTracking(va, (ulong)Unsafe.SizeOf<T>(), false);

            return Read<T>(va);
        }

        public virtual void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null)
        {
            // No default implementation
        }

        public virtual void Write(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            SignalMemoryTracking(va, (ulong)data.Length, true);

            WriteImpl(va, data);
        }

        public virtual void Write<T>(ulong va, T value) where T : unmanaged
        {
            Write(va, MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        public virtual void WriteUntracked(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            WriteImpl(va, data);
        }

        public virtual bool WriteWithRedundancyCheck(ulong va, ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
            {
                return false;
            }

            if (IsContiguousAndMapped(va, data.Length))
            {
                SignalMemoryTracking(va, (ulong)data.Length, false);

                nuint pa = TranslateVirtualAddressChecked(va);

                var target = GetPhysicalAddressSpan(pa, data.Length);

                bool changed = !data.SequenceEqual(target);

                if (changed)
                {
                    data.CopyTo(target);
                }

                return changed;
            }
            else
            {
                Write(va, data);

                return true;
            }
        }

        /// <summary>
        /// Ensures the combination of virtual address and size is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <exception cref="InvalidMemoryRegionException">Throw when the memory region specified outside the addressable space</exception>
        protected void AssertValidAddressAndSize(ulong va, ulong size)
        {
            if (!ValidateAddressAndSize(va, size))
            {
                throw new InvalidMemoryRegionException($"va=0x{va:X16}, size=0x{size:X16}");
            }
        }

        /// <summary>
        /// Ensures the combination of virtual address and size is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <exception cref="InvalidMemoryRegionException">Throw when the memory region specified outside the addressable space</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AssertValidAddressAndSize(ulong va, int size)
            => AssertValidAddressAndSize(va, (ulong)size);

        /// <summary>
        /// Computes the number of pages in a virtual address range.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <param name="startVa">The virtual address of the beginning of the first page</param>
        /// <remarks>This function does not differentiate between allocated and unallocated pages.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static int GetPagesCount(ulong va, ulong size, out ulong startVa)
        {
            // WARNING: Always check if ulong does not overflow during the operations.
            startVa = va & ~(ulong)PageMask;
            ulong vaSpan = (va - startVa + size + PageMask) & ~(ulong)PageMask;

            return (int)(vaSpan / PageSize);
        }

        protected abstract Memory<byte> GetPhysicalAddressMemory(nuint pa, int size);

        protected abstract Span<byte> GetPhysicalAddressSpan(nuint pa, int size);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsContiguous(ulong va, int size) => IsContiguous(va, (ulong)size);

        protected virtual bool IsContiguous(ulong va, ulong size)
        {
            if (!ValidateAddress(va) || !ValidateAddressAndSize(va, size))
            {
                return false;
            }

            int pages = GetPagesCount(va, size, out va);

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize))
                {
                    return false;
                }

                if (TranslateVirtualAddressUnchecked(va) + PageSize != TranslateVirtualAddressUnchecked(va + PageSize))
                {
                    return false;
                }

                va += PageSize;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsContiguousAndMapped(ulong va, int size) => IsContiguous(va, size) && IsMapped(va);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsContiguousAndMapped(ulong va, ulong size) => IsContiguous(va, size) && IsMapped(va);

        protected abstract nuint TranslateVirtualAddressChecked(ulong va);

        protected abstract nuint TranslateVirtualAddressUnchecked(ulong va);

        /// <summary>
        /// Checks if the virtual address is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address</param>
        /// <returns>True if the virtual address is part of the addressable space</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool ValidateAddress(ulong va)
        {
            return va < AddressSpaceSize;
        }

        /// <summary>
        /// Checks if the combination of virtual address and size is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <returns>True if the combination of virtual address and size is part of the addressable space</returns>
        protected bool ValidateAddressAndSize(ulong va, ulong size)
        {
            ulong endVa = va + size;
            return endVa >= va && endVa >= size && endVa <= AddressSpaceSize;
        }

        protected static void ThrowInvalidMemoryRegionException(string message)
            => throw new InvalidMemoryRegionException(message);

        protected static void ThrowMemoryNotContiguous()
            => throw new MemoryNotContiguousException();

        private void WriteImpl(ulong va, ReadOnlySpan<byte> data)
        {
            AssertValidAddressAndSize(va, data.Length);

            if (IsContiguousAndMapped(va, data.Length))
            {
                nuint pa = TranslateVirtualAddressUnchecked(va);

                data.CopyTo(GetPhysicalAddressSpan(pa, data.Length));
            }
            else
            {
                int offset = 0, size;

                if ((va & PageMask) != 0)
                {
                    nuint pa = TranslateVirtualAddressChecked(va);

                    size = Math.Min(data.Length, PageSize - (int)(va & PageMask));

                    data[..size].CopyTo(GetPhysicalAddressSpan(pa, size));

                    offset += size;
                }

                for (; offset < data.Length; offset += size)
                {
                    nuint pa = TranslateVirtualAddressChecked(va + (ulong)offset);

                    size = Math.Min(data.Length - offset, PageSize);

                    data.Slice(offset, size).CopyTo(GetPhysicalAddressSpan(pa, size));
                }
            }
        }

    }
}
