using Ryujinx.Memory;
using Ryujinx.Memory.Tracking;
using System;
using System.Collections.Generic;

namespace Ryujinx.Cpu
{
    public interface IVirtualMemoryManagerTracked : IVirtualMemoryManager
    {
        /// <summary>
        /// Reads data from CPU mapped memory, with read tracking
        /// </summary>
        /// <typeparam name="T">Type of the data being read</typeparam>
        /// <param name="va">Virtual address of the data in memory</param>
        /// <returns>The data</returns>
        T ReadTracked<T>(ulong va) where T : unmanaged;

        /// <summary>
        /// Writes data to CPU mapped memory, without write tracking.
        /// </summary>
        /// <param name="va">Virtual address to write the data into</param>
        /// <param name="data">Data to be written</param>
        void WriteUntracked(ulong va, ReadOnlySpan<byte> data);

        /// <summary>
        /// Obtains a memory tracking handle for the given virtual region. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="id">Handle ID</param>
        /// <param name="flags">Region flags</param>
        /// <returns>The memory tracking handle</returns>
        RegionHandle BeginTracking(ulong address, ulong size, int id, RegionFlags flags = RegionFlags.None);

        /// <summary>
        /// Obtains a memory tracking handle for the given virtual region, with a specified granularity. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="handles">Handles to inherit state from or reuse. When none are present, provide null</param>
        /// <param name="granularity">Desired granularity of write tracking</param>
        /// <param name="id">Handle ID</param>
        /// <param name="flags">Region flags</param>
        /// <returns>The memory tracking handle</returns>
        MultiRegionHandle BeginGranularTracking(ulong address, ulong size, IEnumerable<IRegionHandle> handles, ulong granularity, int id, RegionFlags flags = RegionFlags.None);

        /// <summary>
        /// Obtains a smart memory tracking handle for the given virtual region, with a specified granularity. This should be disposed when finished with.
        /// </summary>
        /// <param name="address">CPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <param name="granularity">Desired granularity of write tracking</param>
        /// <param name="id">Handle ID</param>
        /// <returns>The memory tracking handle</returns>
        SmartMultiRegionHandle BeginSmartGranularTracking(ulong address, ulong size, ulong granularity, int id);
    }
}
