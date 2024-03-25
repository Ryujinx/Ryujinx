using System;

namespace ARMeilleure.Memory
{
    public interface IMemoryManager
    {
        int AddressSpaceBits { get; }

        IntPtr PageTablePointer { get; }

        MemoryManagerType Type { get; }

        event Action<ulong, ulong> UnmapEvent;

        /// <summary>
        /// Reads data from CPU mapped memory.
        /// </summary>
        /// <typeparam name="T">Type of the data being read</typeparam>
        /// <param name="va">Virtual address of the data in memory</param>
        /// <returns>The data</returns>
        T Read<T>(ulong va) where T : unmanaged;

        /// <summary>
        /// Reads data from CPU mapped memory, with read tracking
        /// </summary>
        /// <typeparam name="T">Type of the data being read</typeparam>
        /// <param name="va">Virtual address of the data in memory</param>
        /// <returns>The data</returns>
        T ReadTracked<T>(ulong va) where T : unmanaged;

        /// <summary>
        /// Reads data from CPU mapped memory, from guest code. (with read tracking)
        /// </summary>
        /// <typeparam name="T">Type of the data being read</typeparam>
        /// <param name="va">Virtual address of the data in memory</param>
        /// <returns>The data</returns>
        T ReadGuest<T>(ulong va) where T : unmanaged
        {
            return ReadTracked<T>(va);
        }

        /// <summary>
        /// Writes data to CPU mapped memory.
        /// </summary>
        /// <typeparam name="T">Type of the data being written</typeparam>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="value">Data to be written</param>
        void Write<T>(ulong va, T value) where T : unmanaged;

        /// <summary>
        /// Writes data to CPU mapped memory, from guest code.
        /// </summary>
        /// <typeparam name="T">Type of the data being written</typeparam>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="value">Data to be written</param>
        void WriteGuest<T>(ulong va, T value) where T : unmanaged
        {
            Write(va, value);
        }

        /// <summary>
        /// Gets a read-only span of data from CPU mapped memory.
        /// </summary>
        /// <param name="va">Virtual address of the data</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if read tracking is triggered on the span</param>
        /// <returns>A read-only span of the data</returns>
        ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false);

        /// <summary>
        /// Gets a reference for the given type at the specified virtual memory address.
        /// </summary>
        /// <remarks>
        /// The data must be located at a contiguous memory region.
        /// </remarks>
        /// <typeparam name="T">Type of the data to get the reference</typeparam>
        /// <param name="va">Virtual address of the data</param>
        /// <returns>A reference to the data in memory</returns>
        ref T GetRef<T>(ulong va) where T : unmanaged;

        /// <summary>
        /// Checks if the page at a given CPU virtual address is mapped.
        /// </summary>
        /// <param name="va">Virtual address to check</param>
        /// <returns>True if the address is mapped, false otherwise</returns>
        bool IsMapped(ulong va);

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
    }
}
