using Ryujinx.Memory.Range;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Ryujinx.Memory
{
    public interface IVirtualMemoryManager
    {
        /// <summary>
        /// Indicates whenever the memory manager supports aliasing pages at 4KB granularity.
        /// </summary>
        /// <returns>True if 4KB pages are supported by the memory manager, false otherwise</returns>
        bool Supports4KBPages { get; }

        /// <summary>
        /// Maps a virtual memory range into a physical memory range.
        /// </summary>
        /// <remarks>
        /// Addresses and size must be page aligned.
        /// </remarks>
        /// <param name="va">Virtual memory address</param>
        /// <param name="pa">Physical memory address where the region should be mapped to</param>
        /// <param name="size">Size to be mapped</param>
        /// <param name="flags">Flags controlling memory mapping</param>
        void Map(ulong va, ulong pa, ulong size, MemoryMapFlags flags);

        /// <summary>
        /// Maps a virtual memory range into an arbitrary host memory range.
        /// </summary>
        /// <remarks>
        /// Addresses and size must be page aligned.
        /// Not all memory managers supports this feature.
        /// </remarks>
        /// <param name="va">Virtual memory address</param>
        /// <param name="hostPointer">Host pointer where the virtual region should be mapped</param>
        /// <param name="size">Size to be mapped</param>
        void MapForeign(ulong va, nuint hostPointer, ulong size);

        /// <summary>
        /// Unmaps a previously mapped range of virtual memory.
        /// </summary>
        /// <param name="va">Virtual address of the range to be unmapped</param>
        /// <param name="size">Size of the range to be unmapped</param>
        void Unmap(ulong va, ulong size);

        /// <summary>
        /// Reads data from CPU mapped memory.
        /// </summary>
        /// <typeparam name="T">Type of the data being read</typeparam>
        /// <param name="va">Virtual address of the data in memory</param>
        /// <returns>The data</returns>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        T Read<T>(ulong va) where T : unmanaged;

        /// <summary>
        /// Reads data from CPU mapped memory.
        /// </summary>
        /// <param name="va">Virtual address of the data in memory</param>
        /// <param name="data">Span to store the data being read into</param>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        void Read(ulong va, Span<byte> data);

        /// <summary>
        /// Writes data to CPU mapped memory.
        /// </summary>
        /// <typeparam name="T">Type of the data being written</typeparam>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="value">Data to be written</param>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        void Write<T>(ulong va, T value) where T : unmanaged;

        /// <summary>
        /// Writes data to CPU mapped memory, with write tracking.
        /// </summary>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="data">Data to be written</param>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        void Write(ulong va, ReadOnlySpan<byte> data);

        /// <summary>
        /// Writes data to CPU mapped memory, with write tracking.
        /// </summary>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="data">Data to be written</param>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        public void Write(ulong va, ReadOnlySequence<byte> data)
        {
            foreach (ReadOnlyMemory<byte> segment in data)
            {
                Write(va, segment.Span);
                va += (ulong)segment.Length;
            }
        }

        /// <summary>
        /// Writes data to the application process, returning false if the data was not changed.
        /// This triggers read memory tracking, as a redundancy check would be useless if the data is not up to date.
        /// </summary>
        /// <remarks>The memory manager can return that memory has changed when it hasn't to avoid expensive data copies.</remarks>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="data">Data to be written</param>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        /// <returns>True if the data was changed, false otherwise</returns>
        bool WriteWithRedundancyCheck(ulong va, ReadOnlySpan<byte> data);

        /// <summary>
        /// Fills the specified memory region with the value specified in <paramref name="value"/>.
        /// </summary>
        /// <param name="va">Virtual address to fill the value into</param>
        /// <param name="size">Size of the memory region to fill</param>
        /// <param name="value">Value to fill with</param>
        void Fill(ulong va, ulong size, byte value)
        {
            const int MaxChunkSize = 1 << 24;

            for (ulong subOffset = 0; subOffset < size; subOffset += MaxChunkSize)
            {
                int copySize = (int)Math.Min(MaxChunkSize, size - subOffset);

                using var writableRegion = GetWritableRegion(va + subOffset, copySize);

                writableRegion.Memory.Span.Fill(value);
            }
        }

        /// <summary>
        /// Gets a read-only sequence of read-only memory blocks from CPU mapped memory.
        /// </summary>
        /// <param name="va">Virtual address of the data</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if read tracking is triggered on the memory</param>
        /// <returns>A read-only sequence of read-only memory of the data</returns>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        ReadOnlySequence<byte> GetReadOnlySequence(ulong va, int size, bool tracked = false);

        /// <summary>
        /// Gets a read-only span of data from CPU mapped memory.
        /// </summary>
        /// <param name="va">Virtual address of the data</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if read tracking is triggered on the span</param>
        /// <returns>A read-only span of the data</returns>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false);

        /// <summary>
        /// Gets a region of memory that can be written to.
        /// </summary>
        /// <param name="va">Virtual address of the data</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if write tracking is triggered on the span</param>
        /// <returns>A writable region of memory containing the data</returns>
        /// <exception cref="InvalidMemoryRegionException">Throw for unhandled invalid or unmapped memory accesses</exception>
        WritableRegion GetWritableRegion(ulong va, int size, bool tracked = false);

        /// <summary>
        /// Gets a reference for the given type at the specified virtual memory address.
        /// </summary>
        /// <remarks>
        /// The data must be located at a contiguous memory region.
        /// </remarks>
        /// <typeparam name="T">Type of the data to get the reference</typeparam>
        /// <param name="va">Virtual address of the data</param>
        /// <returns>A reference to the data in memory</returns>
        /// <exception cref="MemoryNotContiguousException">Throw if the specified memory region is not contiguous in physical memory</exception>
        ref T GetRef<T>(ulong va) where T : unmanaged;

        /// <summary>
        /// Gets the host regions that make up the given virtual address region.
        /// If any part of the virtual region is unmapped, null is returned.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <returns>Array of host regions</returns>
        IEnumerable<HostMemoryRange> GetHostRegions(ulong va, ulong size);

        /// <summary>
        /// Gets the physical regions that make up the given virtual address region.
        /// If any part of the virtual region is unmapped, null is returned.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <returns>Array of physical regions</returns>
        IEnumerable<MemoryRange> GetPhysicalRegions(ulong va, ulong size);

        /// <summary>
        /// Checks if the page at a given CPU virtual address is mapped.
        /// </summary>
        /// <param name="va">Virtual address to check</param>
        /// <returns>True if the address is mapped, false otherwise</returns>
        bool IsMapped(ulong va);

        /// <summary>
        /// Checks if a memory range is mapped.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <returns>True if the entire range is mapped, false otherwise</returns>
        bool IsRangeMapped(ulong va, ulong size);

        /// <summary>
        /// Alerts the memory tracking that a given region has been read from or written to.
        /// This should be called before read/write is performed.
        /// </summary>
        /// <param name="va">Virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="write">True if the region was written, false if read</param>
        /// <param name="precise">True if the access is precise, false otherwise</param>
        /// <param name="exemptId">Optional ID of the handles that should not be signalled</param>
        void SignalMemoryTracking(ulong va, ulong size, bool write, bool precise = false, int? exemptId = null);

        /// <summary>
        /// Reprotect a region of virtual memory for guest access.
        /// </summary>
        /// <param name="va">Virtual address base</param>
        /// <param name="size">Size of the region to protect</param>
        /// <param name="protection">Memory protection to set</param>
        void Reprotect(ulong va, ulong size, MemoryPermission protection);

        /// <summary>
        /// Reprotect a region of virtual memory for tracking.
        /// </summary>
        /// <param name="va">Virtual address base</param>
        /// <param name="size">Size of the region to protect</param>
        /// <param name="protection">Memory protection to set</param>
        /// <param name="guest">True if the protection is for guest access, false otherwise</param>
        void TrackingReprotect(ulong va, ulong size, MemoryPermission protection, bool guest);
    }
}
