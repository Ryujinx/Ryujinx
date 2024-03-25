using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Memory.Tracking;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Represents a pool of GPU resources, such as samplers or textures.
    /// </summary>
    /// <typeparam name="T1">Type of the GPU resource</typeparam>
    /// <typeparam name="T2">Type of the descriptor</typeparam>
    abstract class Pool<T1, T2> : IDisposable where T2 : unmanaged
    {
        protected const int DescriptorSize = 0x20;

        protected GpuContext Context;
        protected PhysicalMemory PhysicalMemory;
        protected int SequenceNumber;
        protected int ModifiedSequenceNumber;

        protected T1[] Items;
        protected T2[] DescriptorCache;

        /// <summary>
        /// The maximum ID value of resources on the pool (inclusive).
        /// </summary>
        /// <remarks>
        /// The maximum amount of resources on the pool is equal to this value plus one.
        /// </remarks>
        public int MaximumId { get; }

        /// <summary>
        /// The address of the pool in guest memory.
        /// </summary>
        public ulong Address { get; }

        /// <summary>
        /// The size of the pool in bytes.
        /// </summary>
        public ulong Size { get; }

        private readonly MultiRegionHandle _memoryTracking;
        private readonly Action<ulong, ulong> _modifiedDelegate;

        private int _modifiedSequenceOffset;
        private bool _modified;

        /// <summary>
        /// Creates a new instance of the GPU resource pool.
        /// </summary>
        /// <param name="context">GPU context that the pool belongs to</param>
        /// <param name="physicalMemory">Physical memory where the resource descriptors are mapped</param>
        /// <param name="address">Address of the pool in physical memory</param>
        /// <param name="maximumId">Maximum index of an item on the pool (inclusive)</param>
        public Pool(GpuContext context, PhysicalMemory physicalMemory, ulong address, int maximumId)
        {
            Context = context;
            PhysicalMemory = physicalMemory;
            MaximumId = maximumId;

            int count = maximumId + 1;

            ulong size = (ulong)(uint)count * DescriptorSize;

            Items = new T1[count];
            DescriptorCache = new T2[count];

            Address = address;
            Size = size;

            _memoryTracking = physicalMemory.BeginGranularTracking(address, size, ResourceKind.Pool, RegionFlags.None);
            _memoryTracking.RegisterPreciseAction(address, size, PreciseAction);
            _modifiedDelegate = RegionModified;
        }

        /// <summary>
        /// Gets the descriptor for a given ID.
        /// </summary>
        /// <param name="id">ID of the descriptor. This is effectively a zero-based index</param>
        /// <returns>The descriptor</returns>
        public T2 GetDescriptor(int id)
        {
            return PhysicalMemory.Read<T2>(Address + (ulong)id * DescriptorSize);
        }

        /// <summary>
        /// Gets a reference to the descriptor for a given ID.
        /// </summary>
        /// <param name="id">ID of the descriptor. This is effectively a zero-based index</param>
        /// <returns>A reference to the descriptor</returns>
        public ref readonly T2 GetDescriptorRef(int id)
        {
            return ref GetDescriptorRefAddress(Address + (ulong)id * DescriptorSize);
        }

        /// <summary>
        /// Gets a reference to the descriptor for a given address.
        /// </summary>
        /// <param name="address">Address of the descriptor</param>
        /// <returns>A reference to the descriptor</returns>
        public ref readonly T2 GetDescriptorRefAddress(ulong address)
        {
            return ref MemoryMarshal.Cast<byte, T2>(PhysicalMemory.GetSpan(address, DescriptorSize))[0];
        }

        /// <summary>
        /// Gets the GPU resource with the given ID.
        /// </summary>
        /// <param name="id">ID of the resource. This is effectively a zero-based index</param>
        /// <returns>The GPU resource with the given ID</returns>
        public abstract T1 Get(int id);

        /// <summary>
        /// Checks if a given ID is valid and inside the range of the pool.
        /// </summary>
        /// <param name="id">ID of the descriptor. This is effectively a zero-based index</param>
        /// <returns>True if the specified ID is valid, false otherwise</returns>
        public bool IsValidId(int id)
        {
            return (uint)id <= MaximumId;
        }

        /// <summary>
        /// Synchronizes host memory with guest memory.
        /// This causes invalidation of pool entries,
        /// if a modification of entries by the CPU is detected.
        /// </summary>
        public void SynchronizeMemory()
        {
            _modified = false;
            _memoryTracking.QueryModified(_modifiedDelegate);

            if (_modified)
            {
                UpdateModifiedSequence();
            }
        }

        /// <summary>
        /// Indicate that a region of the pool was modified, and must be loaded from memory.
        /// </summary>
        /// <param name="mAddress">Start address of the modified region</param>
        /// <param name="mSize">Size of the modified region</param>
        private void RegionModified(ulong mAddress, ulong mSize)
        {
            _modified = true;

            if (mAddress < Address)
            {
                mAddress = Address;
            }

            ulong maxSize = Address + Size - mAddress;

            if (mSize > maxSize)
            {
                mSize = maxSize;
            }

            InvalidateRangeImpl(mAddress, mSize);
        }

        /// <summary>
        /// Updates the modified sequence number using the current sequence number and offset,
        /// indicating that it has been modified.
        /// </summary>
        protected void UpdateModifiedSequence()
        {
            ModifiedSequenceNumber = SequenceNumber + _modifiedSequenceOffset;
        }

        /// <summary>
        /// An action to be performed when a precise memory access occurs to this resource.
        /// Makes sure that the dirty flags are checked.
        /// </summary>
        /// <param name="address">Address of the memory action</param>
        /// <param name="size">Size in bytes</param>
        /// <param name="write">True if the access was a write, false otherwise</param>
        private bool PreciseAction(ulong address, ulong size, bool write)
        {
            if (write && Context.SequenceNumber == SequenceNumber)
            {
                if (ModifiedSequenceNumber == SequenceNumber + _modifiedSequenceOffset)
                {
                    // The modified sequence number is offset when PreciseActions occur so that
                    // users checking it will see an increment and know the pool has changed since
                    // their last look, even though the main SequenceNumber has not been changed.

                    _modifiedSequenceOffset++;
                }

                // Force the pool to be checked again the next time it is used.
                SequenceNumber--;
            }

            return false;
        }

        protected abstract void InvalidateRangeImpl(ulong address, ulong size);

        protected abstract void Delete(T1 item);

        /// <summary>
        /// Performs the disposal of all resources stored on the pool.
        /// It's an error to try using the pool after disposal.
        /// </summary>
        public virtual void Dispose()
        {
            if (Items != null)
            {
                for (int index = 0; index < Items.Length; index++)
                {
                    Delete(Items[index]);
                }

                Items = null;
            }
            _memoryTracking.Dispose();
        }
    }
}
