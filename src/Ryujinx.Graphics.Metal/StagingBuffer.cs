using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    readonly struct StagingBufferReserved
    {
        public readonly BufferHolder Buffer;
        public readonly int Offset;
        public readonly int Size;

        public StagingBufferReserved(BufferHolder buffer, int offset, int size)
        {
            Buffer = buffer;
            Offset = offset;
            Size = size;
        }
    }

    [SupportedOSPlatform("macos")]
    class StagingBuffer : IDisposable
    {
        private const int BufferSize = 32 * 1024 * 1024;

        private int _freeOffset;
        private int _freeSize;

        private readonly MetalRenderer _renderer;
        private readonly BufferHolder _buffer;
        private readonly int _resourceAlignment;

        public readonly BufferHandle Handle;

        private readonly struct PendingCopy
        {
            public FenceHolder Fence { get; }
            public int Size { get; }

            public PendingCopy(FenceHolder fence, int size)
            {
                Fence = fence;
                Size = size;
                fence.Get();
            }
        }

        private readonly Queue<PendingCopy> _pendingCopies;

        public StagingBuffer(MetalRenderer renderer, BufferManager bufferManager)
        {
            _renderer = renderer;

            Handle = bufferManager.CreateWithHandle(BufferSize, out _buffer);
            _pendingCopies = new Queue<PendingCopy>();
            _freeSize = BufferSize;
            _resourceAlignment = Constants.MinResourceAlignment;
        }

        public void PushData(CommandBufferPool cbp, CommandBufferScoped? cbs, BufferHolder dst, int dstOffset, ReadOnlySpan<byte> data)
        {
            bool isRender = cbs != null;
            CommandBufferScoped scoped = cbs ?? cbp.Rent();

            // Must push all data to the buffer. If it can't fit, split it up.

            while (data.Length > 0)
            {
                if (_freeSize < data.Length)
                {
                    FreeCompleted();
                }

                while (_freeSize == 0)
                {
                    if (!WaitFreeCompleted(cbp))
                    {
                        if (isRender)
                        {
                            _renderer.FlushAllCommands();
                            scoped = cbp.Rent();
                            isRender = false;
                        }
                        else
                        {
                            scoped = cbp.ReturnAndRent(scoped);
                        }
                    }
                }

                int chunkSize = Math.Min(_freeSize, data.Length);

                PushDataImpl(scoped, dst, dstOffset, data[..chunkSize]);

                dstOffset += chunkSize;
                data = data[chunkSize..];
            }

            if (!isRender)
            {
                scoped.Dispose();
            }
        }

        private void PushDataImpl(CommandBufferScoped cbs, BufferHolder dst, int dstOffset, ReadOnlySpan<byte> data)
        {
            var srcBuffer = _buffer.GetBuffer();
            var dstBuffer = dst.GetBuffer(dstOffset, data.Length, true);

            int offset = _freeOffset;
            int capacity = BufferSize - offset;
            if (capacity < data.Length)
            {
                _buffer.SetDataUnchecked(offset, data[..capacity]);
                _buffer.SetDataUnchecked(0, data[capacity..]);

                BufferHolder.Copy(cbs, srcBuffer, dstBuffer, offset, dstOffset, capacity);
                BufferHolder.Copy(cbs, srcBuffer, dstBuffer, 0, dstOffset + capacity, data.Length - capacity);
            }
            else
            {
                _buffer.SetDataUnchecked(offset, data);

                BufferHolder.Copy(cbs, srcBuffer, dstBuffer, offset, dstOffset, data.Length);
            }

            _freeOffset = (offset + data.Length) & (BufferSize - 1);
            _freeSize -= data.Length;
            Debug.Assert(_freeSize >= 0);

            _pendingCopies.Enqueue(new PendingCopy(cbs.GetFence(), data.Length));
        }

        public bool TryPushData(CommandBufferScoped cbs, BufferHolder dst, int dstOffset, ReadOnlySpan<byte> data)
        {
            if (data.Length > BufferSize)
            {
                return false;
            }

            if (_freeSize < data.Length)
            {
                FreeCompleted();

                if (_freeSize < data.Length)
                {
                    return false;
                }
            }

            PushDataImpl(cbs, dst, dstOffset, data);

            return true;
        }

        private StagingBufferReserved ReserveDataImpl(CommandBufferScoped cbs, int size, int alignment)
        {
            // Assumes the caller has already determined that there is enough space.
            int offset = BitUtils.AlignUp(_freeOffset, alignment);
            int padding = offset - _freeOffset;

            int capacity = Math.Min(_freeSize, BufferSize - offset);
            int reservedLength = size + padding;
            if (capacity < size)
            {
                offset = 0; // Place at start.
                reservedLength += capacity;
            }

            _freeOffset = (_freeOffset + reservedLength) & (BufferSize - 1);
            _freeSize -= reservedLength;
            Debug.Assert(_freeSize >= 0);

            _pendingCopies.Enqueue(new PendingCopy(cbs.GetFence(), reservedLength));

            return new StagingBufferReserved(_buffer, offset, size);
        }

        private int GetContiguousFreeSize(int alignment)
        {
            int alignedFreeOffset = BitUtils.AlignUp(_freeOffset, alignment);
            int padding = alignedFreeOffset - _freeOffset;

            // Free regions:
            // - Aligned free offset to end (minimum free size - padding)
            // - 0 to _freeOffset + freeSize wrapped (only if free area contains 0)

            int endOffset = (_freeOffset + _freeSize) & (BufferSize - 1);

            return Math.Max(
                Math.Min(_freeSize - padding, BufferSize - alignedFreeOffset),
                endOffset <= _freeOffset ? Math.Min(_freeSize, endOffset) : 0
            );
        }

        /// <summary>
        /// Reserve a range on the staging buffer for the current command buffer and upload data to it.
        /// </summary>
        /// <param name="cbs">Command buffer to reserve the data on</param>
        /// <param name="size">The minimum size the reserved data requires</param>
        /// <param name="alignment">The required alignment for the buffer offset</param>
        /// <returns>The reserved range of the staging buffer</returns>
        public StagingBufferReserved? TryReserveData(CommandBufferScoped cbs, int size, int alignment)
        {
            if (size > BufferSize)
            {
                return null;
            }

            // Temporary reserved data cannot be fragmented.

            if (GetContiguousFreeSize(alignment) < size)
            {
                FreeCompleted();

                if (GetContiguousFreeSize(alignment) < size)
                {
                    Logger.Debug?.PrintMsg(LogClass.Gpu, $"Staging buffer out of space to reserve data of size {size}.");
                    return null;
                }
            }

            return ReserveDataImpl(cbs, size, alignment);
        }

        /// <summary>
        /// Reserve a range on the staging buffer for the current command buffer and upload data to it.
        /// Uses the most permissive byte alignment.
        /// </summary>
        /// <param name="cbs">Command buffer to reserve the data on</param>
        /// <param name="size">The minimum size the reserved data requires</param>
        /// <returns>The reserved range of the staging buffer</returns>
        public StagingBufferReserved? TryReserveData(CommandBufferScoped cbs, int size)
        {
            return TryReserveData(cbs, size, _resourceAlignment);
        }

        private bool WaitFreeCompleted(CommandBufferPool cbp)
        {
            if (_pendingCopies.TryPeek(out var pc))
            {
                if (!pc.Fence.IsSignaled())
                {
                    if (cbp.IsFenceOnRentedCommandBuffer(pc.Fence))
                    {
                        return false;
                    }

                    pc.Fence.Wait();
                }

                var dequeued = _pendingCopies.Dequeue();
                Debug.Assert(dequeued.Fence == pc.Fence);
                _freeSize += pc.Size;
                pc.Fence.Put();
            }

            return true;
        }

        public void FreeCompleted()
        {
            FenceHolder signalledFence = null;
            while (_pendingCopies.TryPeek(out var pc) && (pc.Fence == signalledFence || pc.Fence.IsSignaled()))
            {
                signalledFence = pc.Fence; // Already checked - don't need to do it again.
                var dequeued = _pendingCopies.Dequeue();
                Debug.Assert(dequeued.Fence == pc.Fence);
                _freeSize += pc.Size;
                pc.Fence.Put();
            }
        }

        public void Dispose()
        {
            _renderer.BufferManager.Delete(Handle);

            while (_pendingCopies.TryDequeue(out var pc))
            {
                pc.Fence.Put();
            }
        }
    }
}
