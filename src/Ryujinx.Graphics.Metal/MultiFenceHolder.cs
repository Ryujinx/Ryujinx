using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    /// <summary>
    /// Holder for multiple host GPU fences.
    /// </summary>
    [SupportedOSPlatform("macos")]
    class MultiFenceHolder
    {
        private const int BufferUsageTrackingGranularity = 4096;

        private readonly FenceHolder[] _fences;
        private readonly BufferUsageBitmap _bufferUsageBitmap;

        /// <summary>
        /// Creates a new instance of the multiple fence holder.
        /// </summary>
        public MultiFenceHolder()
        {
            _fences = new FenceHolder[CommandBufferPool.MaxCommandBuffers];
        }

        /// <summary>
        /// Creates a new instance of the multiple fence holder, with a given buffer size in mind.
        /// </summary>
        /// <param name="size">Size of the buffer</param>
        public MultiFenceHolder(int size)
        {
            _fences = new FenceHolder[CommandBufferPool.MaxCommandBuffers];
            _bufferUsageBitmap = new BufferUsageBitmap(size, BufferUsageTrackingGranularity);
        }

        /// <summary>
        /// Adds read/write buffer usage information to the uses list.
        /// </summary>
        /// <param name="cbIndex">Index of the command buffer where the buffer is used</param>
        /// <param name="offset">Offset of the buffer being used</param>
        /// <param name="size">Size of the buffer region being used, in bytes</param>
        /// <param name="write">Whether the access is a write or not</param>
        public void AddBufferUse(int cbIndex, int offset, int size, bool write)
        {
            _bufferUsageBitmap.Add(cbIndex, offset, size, false);

            if (write)
            {
                _bufferUsageBitmap.Add(cbIndex, offset, size, true);
            }
        }

        /// <summary>
        /// Removes all buffer usage information for a given command buffer.
        /// </summary>
        /// <param name="cbIndex">Index of the command buffer where the buffer is used</param>
        public void RemoveBufferUses(int cbIndex)
        {
            _bufferUsageBitmap?.Clear(cbIndex);
        }

        /// <summary>
        /// Checks if a given range of a buffer is being used by a command buffer still being processed by the GPU.
        /// </summary>
        /// <param name="cbIndex">Index of the command buffer where the buffer is used</param>
        /// <param name="offset">Offset of the buffer being used</param>
        /// <param name="size">Size of the buffer region being used, in bytes</param>
        /// <returns>True if in use, false otherwise</returns>
        public bool IsBufferRangeInUse(int cbIndex, int offset, int size)
        {
            return _bufferUsageBitmap.OverlapsWith(cbIndex, offset, size);
        }

        /// <summary>
        /// Checks if a given range of a buffer is being used by any command buffer still being processed by the GPU.
        /// </summary>
        /// <param name="offset">Offset of the buffer being used</param>
        /// <param name="size">Size of the buffer region being used, in bytes</param>
        /// <param name="write">True if only write usages should count</param>
        /// <returns>True if in use, false otherwise</returns>
        public bool IsBufferRangeInUse(int offset, int size, bool write)
        {
            return _bufferUsageBitmap.OverlapsWith(offset, size, write);
        }

        /// <summary>
        /// Adds a fence to the holder.
        /// </summary>
        /// <param name="cbIndex">Command buffer index of the command buffer that owns the fence</param>
        /// <param name="fence">Fence to be added</param>
        /// <returns>True if the command buffer's previous fence value was null</returns>
        public bool AddFence(int cbIndex, FenceHolder fence)
        {
            ref FenceHolder fenceRef = ref _fences[cbIndex];

            if (fenceRef == null)
            {
                fenceRef = fence;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a fence from the holder.
        /// </summary>
        /// <param name="cbIndex">Command buffer index of the command buffer that owns the fence</param>
        public void RemoveFence(int cbIndex)
        {
            _fences[cbIndex] = null;
        }

        /// <summary>
        /// Determines if a fence referenced on the given command buffer.
        /// </summary>
        /// <param name="cbIndex">Index of the command buffer to check if it's used</param>
        /// <returns>True if referenced, false otherwise</returns>
        public bool HasFence(int cbIndex)
        {
            return _fences[cbIndex] != null;
        }

        /// <summary>
        /// Wait until all the fences on the holder are signaled.
        /// </summary>
        public void WaitForFences()
        {
            WaitForFencesImpl(0, 0, true);
        }

        /// <summary>
        /// Wait until all the fences on the holder with buffer uses overlapping the specified range are signaled.
        /// </summary>
        /// <param name="offset">Start offset of the buffer range</param>
        /// <param name="size">Size of the buffer range in bytes</param>
        public void WaitForFences(int offset, int size)
        {
            WaitForFencesImpl(offset, size, true);
        }

        /// <summary>
        /// Wait until all the fences on the holder with buffer uses overlapping the specified range are signaled.
        /// </summary>

        // TODO: Add a proper timeout!
        public bool WaitForFences(bool indefinite)
        {
            return WaitForFencesImpl(0, 0, indefinite);
        }

        /// <summary>
        /// Wait until all the fences on the holder with buffer uses overlapping the specified range are signaled.
        /// </summary>
        /// <param name="offset">Start offset of the buffer range</param>
        /// <param name="size">Size of the buffer range in bytes</param>
        /// <param name="indefinite">Indicates if this should wait indefinitely</param>
        /// <returns>True if all fences were signaled before the timeout expired, false otherwise</returns>
        private bool WaitForFencesImpl(int offset, int size, bool indefinite)
        {
            Span<FenceHolder> fenceHolders = new FenceHolder[CommandBufferPool.MaxCommandBuffers];

            int count = size != 0 ? GetOverlappingFences(fenceHolders, offset, size) : GetFences(fenceHolders);
            Span<MTLCommandBuffer> fences = stackalloc MTLCommandBuffer[count];

            int fenceCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (fenceHolders[i].TryGet(out MTLCommandBuffer fence))
                {
                    fences[fenceCount] = fence;

                    if (fenceCount < i)
                    {
                        fenceHolders[fenceCount] = fenceHolders[i];
                    }

                    fenceCount++;
                }
            }

            if (fenceCount == 0)
            {
                return true;
            }

            bool signaled = true;

            if (indefinite)
            {
                foreach (var fence in fences)
                {
                    fence.WaitUntilCompleted();
                }
            }
            else
            {
                foreach (var fence in fences)
                {
                    if (fence.Status != MTLCommandBufferStatus.Completed)
                    {
                        signaled = false;
                    }
                }
            }

            for (int i = 0; i < fenceCount; i++)
            {
                fenceHolders[i].Put();
            }

            return signaled;
        }

        /// <summary>
        /// Gets fences to wait for.
        /// </summary>
        /// <param name="storage">Span to store fences in</param>
        /// <returns>Number of fences placed in storage</returns>
        private int GetFences(Span<FenceHolder> storage)
        {
            int count = 0;

            for (int i = 0; i < _fences.Length; i++)
            {
                var fence = _fences[i];

                if (fence != null)
                {
                    storage[count++] = fence;
                }
            }

            return count;
        }

        /// <summary>
        /// Gets fences to wait for use of a given buffer region.
        /// </summary>
        /// <param name="storage">Span to store overlapping fences in</param>
        /// <param name="offset">Offset of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <returns>Number of fences for the specified region placed in storage</returns>
        private int GetOverlappingFences(Span<FenceHolder> storage, int offset, int size)
        {
            int count = 0;

            for (int i = 0; i < _fences.Length; i++)
            {
                var fence = _fences[i];

                if (fence != null && _bufferUsageBitmap.OverlapsWith(i, offset, size))
                {
                    storage[count++] = fence;
                }
            }

            return count;
        }
    }
}
