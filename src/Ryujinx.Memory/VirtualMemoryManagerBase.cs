using System;
using System.Numerics;

namespace Ryujinx.Memory
{
    public abstract class VirtualMemoryManagerBase<TVirtual, TPhysical>
        where TVirtual : IBinaryInteger<TVirtual>
        where TPhysical : IBinaryInteger<TPhysical>
    {
        public const int PageBits = 12;
        public const int PageSize = 1 << PageBits;
        public const int PageMask = PageSize - 1;

        protected abstract TVirtual AddressSpaceSize { get; }

        public virtual void Read(TVirtual va, Span<byte> data)
        {
            if (data.Length == 0)
            {
                return;
            }

            AssertValidAddressAndSize(va, TVirtual.CreateChecked(data.Length));

            int offset = 0, size;

            if ((int.CreateTruncating(va) & PageMask) != 0)
            {
                TPhysical pa = TranslateVirtualAddressForRead(va);

                size = Math.Min(data.Length, PageSize - ((int.CreateTruncating(va) & PageMask)));

                GetPhysicalAddressSpan(pa, size).CopyTo(data[..size]);

                offset += size;
            }

            for (; offset < data.Length; offset += size)
            {
                TPhysical pa = TranslateVirtualAddressForRead(va + TVirtual.CreateChecked(offset));

                size = Math.Min(data.Length - offset, PageSize);

                GetPhysicalAddressSpan(pa, size).CopyTo(data.Slice(offset, size));
            }
        }

        /// <summary>
        /// Ensures the combination of virtual address and size is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <exception cref="InvalidMemoryRegionException">Throw when the memory region specified outside the addressable space</exception>
        protected void AssertValidAddressAndSize(TVirtual va, TVirtual size)
        {
            if (!ValidateAddressAndSize(va, size))
            {
                throw new InvalidMemoryRegionException($"va=0x{va:X16}, size=0x{size:X16}");
            }
        }

        protected abstract Span<byte> GetPhysicalAddressSpan(TPhysical pa, int size);

        protected abstract TPhysical TranslateVirtualAddressForRead(TVirtual va);

        /// <summary>
        /// Checks if the virtual address is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address</param>
        /// <returns>True if the virtual address is part of the addressable space</returns>
        protected bool ValidateAddress(TVirtual va)
        {
            return va < AddressSpaceSize;
        }

        /// <summary>
        /// Checks if the combination of virtual address and size is part of the addressable space.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <returns>True if the combination of virtual address and size is part of the addressable space</returns>
        protected bool ValidateAddressAndSize(TVirtual va, TVirtual size)
        {
            TVirtual endVa = va + size;
            return endVa >= va && endVa >= size && endVa <= AddressSpaceSize;
        }

        protected static void ThrowInvalidMemoryRegionException(string message)
            => throw new InvalidMemoryRegionException(message);
    }
}
