﻿using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Ryujinx.Graphics.Vulkan
{
    class BufferHolder : IDisposable
    {
        private const int MaxUpdateBufferSize = 0x10000;

        private const int SetCountThreshold = 100;
        private const int WriteCountThreshold = 10;
        private const int FlushCountThreshold = 5;

        public const AccessFlags DefaultAccessFlags =
            AccessFlags.IndirectCommandReadBit |
            AccessFlags.ShaderReadBit |
            AccessFlags.ShaderWriteBit |
            AccessFlags.TransferReadBit |
            AccessFlags.TransferWriteBit |
            AccessFlags.UniformReadBit;

        private readonly VulkanRenderer _gd;
        private readonly Device _device;

        private MemoryAllocation _allocation;
        private Auto<DisposableBuffer> _buffer;
        private Auto<MemoryAllocation> _allocationAuto;
        private ulong _bufferHandle;

        private CacheByRange<BufferHolder> _cachedConvertedBuffers;

        public int Size { get; }

        private IntPtr _map;

        private MultiFenceHolder _waitable;

        private bool _lastAccessIsWrite;

        private BufferAllocationType _baseType;
        private BufferAllocationType _desiredType;
        private BufferAllocationType _currentType;
        private int _setCount;
        private int _writeCount;
        private int _flushCount;

        private ReaderWriterLock _flushLock;
        private FenceHolder _flushFence;
        private int _flushWaiting;

        private List<Action> _swapActions;

        public BufferHolder(VulkanRenderer gd, Device device, VkBuffer buffer, MemoryAllocation allocation, int size, BufferAllocationType type, BufferAllocationType currentType)
        {
            _gd = gd;
            _device = device;
            _allocation = allocation;
            _allocationAuto = new Auto<MemoryAllocation>(allocation);
            _waitable = new MultiFenceHolder(size);
            _buffer = new Auto<DisposableBuffer>(new DisposableBuffer(gd.Api, device, buffer), _waitable, _allocationAuto);
            _bufferHandle = buffer.Handle;
            Size = size;
            _map = allocation.HostPointer;

            _baseType = type;
            _currentType = currentType;
            _desiredType = currentType;

            _flushLock = new ReaderWriterLock();
        }

        private void TrySwapBacking()
        {
            if (_desiredType != _currentType)
            {
                // Only swap if the buffer is not used in any queued command buffer.
                bool isRented = _buffer.HasRentedCommandBufferDependency(_gd.CommandBufferPool);

                if (!isRented && _gd.CommandBufferPool.OwnedByCurrentThread)
                {
                    var currentAllocation = _allocationAuto;
                    var currentBuffer = _buffer;
                    IntPtr currentMap = _map;

                    (VkBuffer buffer, MemoryAllocation allocation, BufferAllocationType resultType) = _gd.BufferManager.CreateBacking(_gd, Size, _desiredType, false, _currentType);

                    if (buffer.Handle != 0)
                    {
                        _flushLock.AcquireWriterLock(Timeout.Infinite);

                        ClearFlushFence();

                        _waitable = new MultiFenceHolder(Size);

                        _allocation = allocation;
                        _allocationAuto = new Auto<MemoryAllocation>(allocation);
                        _buffer = new Auto<DisposableBuffer>(new DisposableBuffer(_gd.Api, _device, buffer), _waitable, _allocationAuto);
                        _bufferHandle = buffer.Handle;
                        _map = allocation.HostPointer;

                        if (_map != IntPtr.Zero && currentMap != IntPtr.Zero)
                        {
                            // Copy data directly. Readbacks don't have to wait if this is done.

                            unsafe
                            {
                                new Span<byte>((void*)currentMap, Size).CopyTo(new Span<byte>((void*)_map, Size));
                            }
                        }
                        else
                        {
                            using var cbs = _gd.CommandBufferPool.Rent();

                            Copy(_gd, cbs, currentBuffer, _buffer, 0, 0, Size);

                            // Need to wait for the data to reach the new buffer before data can be flushed.

                            _flushFence = _gd.CommandBufferPool.GetFence(cbs.CommandBufferIndex);
                            _flushFence.Get();
                        }

                        Common.Logging.Logger.Error?.PrintMsg(Common.Logging.LogClass.Gpu, $"Converted {Size} buffer {_currentType} to {resultType}");

                        _currentType = resultType;

                        if (_swapActions != null)
                        {
                            foreach (var action in _swapActions)
                            {
                                action();
                            }

                            _swapActions.Clear();
                        }

                        currentBuffer.Dispose();
                        currentAllocation.Dispose();

                        _gd.PipelineInternal.SwapBuffer(currentBuffer, _buffer);

                        _flushLock.ReleaseWriterLock();
                    }
                }
            }
        }

        private void ConsiderBackingSwap()
        {
            if (_baseType == BufferAllocationType.Auto)
            {
                if (_writeCount >= WriteCountThreshold || _setCount >= SetCountThreshold || _flushCount >= FlushCountThreshold)
                {
                    if (_flushCount > 0 || _currentType == BufferAllocationType.DeviceLocalMapped)
                    {
                        // Buffers that flush often should ideally be in the locally mapped heap.
                        _desiredType = BufferAllocationType.DeviceLocalMapped;
                    }
                    else if (_writeCount >= WriteCountThreshold)
                    {
                        // Buffers that are written often should ideally be in the device local heap. (Storage buffers)
                        _desiredType = BufferAllocationType.DeviceLocal;
                    }
                    else if (_setCount > SetCountThreshold)
                    {
                        // Buffers that have their data set often should ideally be host mapped. (Constant buffers)
                        _desiredType = BufferAllocationType.HostMapped;
                    }

                    _writeCount = 0;
                    _setCount = 0;
                    _flushCount = 0;
                }

                TrySwapBacking();
            }
        }

        public unsafe Auto<DisposableBufferView> CreateView(VkFormat format, int offset, int size, Action invalidateView)
        {
            var bufferViewCreateInfo = new BufferViewCreateInfo()
            {
                SType = StructureType.BufferViewCreateInfo,
                Buffer = new VkBuffer(_bufferHandle),
                Format = format,
                Offset = (uint)offset,
                Range = (uint)size
            };

            _gd.Api.CreateBufferView(_device, bufferViewCreateInfo, null, out var bufferView).ThrowOnError();

            (_swapActions ??= new List<Action>()).Add(invalidateView);

            return new Auto<DisposableBufferView>(new DisposableBufferView(_gd.Api, _device, bufferView), _waitable, _buffer);
        }

        public unsafe void InsertBarrier(CommandBuffer commandBuffer, bool isWrite)
        {
            // If the last access is write, we always need a barrier to be sure we will read or modify
            // the correct data.
            // If the last access is read, and current one is a write, we need to wait until the
            // read finishes to avoid overwriting data still in use.
            // Otherwise, if the last access is a read and the current one too, we don't need barriers.
            bool needsBarrier = isWrite || _lastAccessIsWrite;

            _lastAccessIsWrite = isWrite;

            if (needsBarrier)
            {
                MemoryBarrier memoryBarrier = new MemoryBarrier()
                {
                    SType = StructureType.MemoryBarrier,
                    SrcAccessMask = DefaultAccessFlags,
                    DstAccessMask = DefaultAccessFlags
                };

                _gd.Api.CmdPipelineBarrier(
                    commandBuffer,
                    PipelineStageFlags.AllCommandsBit,
                    PipelineStageFlags.AllCommandsBit,
                    DependencyFlags.DeviceGroupBit,
                    1,
                    memoryBarrier,
                    0,
                    null,
                    0,
                    null);
            }
        }

        public Auto<DisposableBuffer> GetBuffer()
        {
            return _buffer;
        }

        public Auto<DisposableBuffer> GetBuffer(CommandBuffer commandBuffer, bool isWrite = false, bool isSSBO = false)
        {
            if (isWrite)
            {
                _writeCount++;

                SignalWrite(0, Size);
            }
            else if (isSSBO)
            {
                // Always consider SSBO access for swapping to device local memory.

                _writeCount++;

                ConsiderBackingSwap();
            }

            return _buffer;
        }

        public Auto<DisposableBuffer> GetBuffer(CommandBuffer commandBuffer, int offset, int size, bool isWrite = false)
        {
            if (isWrite)
            {
                _writeCount++;

                SignalWrite(offset, size);
            }

            return _buffer;
        }

        public void SignalWrite(int offset, int size)
        {
            ConsiderBackingSwap();

            if (offset == 0 && size == Size)
            {
                _cachedConvertedBuffers.Clear();
            }
            else
            {
                _cachedConvertedBuffers.ClearRange(offset, size);
            }
        }

        public BufferHandle GetHandle()
        {
            var handle = _bufferHandle;
            return Unsafe.As<ulong, BufferHandle>(ref handle);
        }

        public unsafe IntPtr Map(int offset, int mappingSize)
        {
            return _map;
        }

        private void ClearFlushFence()
        {
            // Asusmes _flushLock is held as writer.

            if (_flushFence != null)
            {
                if (_flushWaiting == 0)
                {
                    _flushFence.Put();
                }

                _flushFence = null;
            }
        }

        private void WaitForFlushFence()
        {
            // Assumes the _flushLock is held as reader, returns in same state.

            if (_flushFence != null)
            {
                // If storage has changed, make sure the fence has been reached so that the data is in place.

                var cookie = _flushLock.UpgradeToWriterLock(Timeout.Infinite);

                if (_flushFence != null)
                {
                    var fence = _flushFence;
                    Interlocked.Increment(ref _flushWaiting);

                    // Don't wait in the lock.

                    var restoreCookie = _flushLock.ReleaseLock();

                    fence.Wait();

                    _flushLock.RestoreLock(ref restoreCookie);

                    if (Interlocked.Decrement(ref _flushWaiting) == 0)
                    {
                        fence.Put();
                    }

                    _flushFence = null;

                    _flushLock.DowngradeFromWriterLock(ref cookie);
                }
                else
                {
                    _flushLock.DowngradeFromWriterLock(ref cookie);
                }
            }
        }

        public unsafe ReadOnlySpan<byte> GetData(int offset, int size)
        {
            //Common.Logging.Logger.Error?.PrintMsg(Common.Logging.LogClass.Gpu, $"Flush type {_currentType}");

            _flushLock.AcquireReaderLock(Timeout.Infinite);

            WaitForFlushFence();

            _flushCount++;

            Span<byte> result;

            if (_map != IntPtr.Zero)
            {
                // TODO: Return some kind of scoped container that lets us keep track of when the mapping is no longer used
                // For safety with dispose and backing memory swap.

                result = GetDataStorage(offset, size);

                _flushLock.ReleaseReaderLock();

                return result;
            }
            else
            {
                BackgroundResource resource = _gd.BackgroundResources.Get();

                if (_gd.CommandBufferPool.OwnedByCurrentThread)
                {
                    _gd.FlushAllCommands();

                    result = resource.GetFlushBuffer().GetBufferData(_gd.CommandBufferPool, this, offset, size);
                }
                else
                {
                    result = resource.GetFlushBuffer().GetBufferData(resource.GetPool(), this, offset, size);
                }

                _flushLock.ReleaseReaderLock();

                return result;
            }
        }

        public unsafe Span<byte> GetDataStorage(int offset, int size)
        {
            int mappingSize = Math.Min(size, Size - offset);

            if (_map != IntPtr.Zero)
            {
                return new Span<byte>((void*)(_map + offset), mappingSize);
            }

            throw new InvalidOperationException("The buffer is not host mapped.");
        }

        public unsafe void SetData(int offset, ReadOnlySpan<byte> data, CommandBufferScoped? cbs = null, Action endRenderPass = null)
        {
            int dataSize = Math.Min(data.Length, Size - offset);
            if (dataSize == 0)
            {
                return;
            }

            _setCount++;

            if (_map != IntPtr.Zero)
            {
                // If persistently mapped, set the data directly if the buffer is not currently in use.
                bool isRented = _buffer.HasRentedCommandBufferDependency(_gd.CommandBufferPool);

                // If the buffer is rented, take a little more time and check if the use overlaps this handle.
                bool needsFlush = isRented && _waitable.IsBufferRangeInUse(offset, dataSize);

                if (!needsFlush)
                {
                    WaitForFences(offset, dataSize);

                    data.Slice(0, dataSize).CopyTo(new Span<byte>((void*)(_map + offset), dataSize));

                    SignalWrite(offset, dataSize);

                    return;
                }
            }

            if (cbs != null &&
                _gd.PipelineInternal.RenderPassActive &&
                !(_buffer.HasCommandBufferDependency(cbs.Value) &&
                _waitable.IsBufferRangeInUse(cbs.Value.CommandBufferIndex, offset, dataSize)))
            {
                // If the buffer hasn't been used on the command buffer yet, try to preload the data.
                // This avoids ending and beginning render passes on each buffer data upload.

                cbs = _gd.PipelineInternal.GetPreloadCommandBuffer();
                endRenderPass = null;
            }

            if (cbs == null ||
                !VulkanConfiguration.UseFastBufferUpdates ||
                data.Length > MaxUpdateBufferSize ||
                !TryPushData(cbs.Value, endRenderPass, offset, data))
            {
                _gd.BufferManager.StagingBuffer.PushData(_gd.CommandBufferPool, cbs, endRenderPass, this, offset, data);
            }
        }

        public unsafe void SetDataUnchecked(int offset, ReadOnlySpan<byte> data)
        {
            int dataSize = Math.Min(data.Length, Size - offset);
            if (dataSize == 0)
            {
                return;
            }

            if (_map != IntPtr.Zero)
            {
                data.Slice(0, dataSize).CopyTo(new Span<byte>((void*)(_map + offset), dataSize));
            }
            else
            {
                _gd.BufferManager.StagingBuffer.PushData(_gd.CommandBufferPool, null, null, this, offset, data);
            }
        }

        public void SetDataInline(CommandBufferScoped cbs, Action endRenderPass, int dstOffset, ReadOnlySpan<byte> data)
        {
            if (!TryPushData(cbs, endRenderPass, dstOffset, data))
            {
                throw new ArgumentException($"Invalid offset 0x{dstOffset:X} or data size 0x{data.Length:X}.");
            }
        }

        private unsafe bool TryPushData(CommandBufferScoped cbs, Action endRenderPass, int dstOffset, ReadOnlySpan<byte> data)
        {
            if ((dstOffset & 3) != 0 || (data.Length & 3) != 0)
            {
                return false;
            }

            endRenderPass?.Invoke();

            var dstBuffer = GetBuffer(cbs.CommandBuffer, dstOffset, data.Length, true).Get(cbs, dstOffset, data.Length).Value;

            _writeCount--;

            InsertBufferBarrier(
                _gd,
                cbs.CommandBuffer,
                dstBuffer,
                DefaultAccessFlags,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.TransferBit,
                dstOffset,
                data.Length);

            fixed (byte* pData = data)
            {
                for (ulong offset = 0; offset < (ulong)data.Length;)
                {
                    ulong size = Math.Min(MaxUpdateBufferSize, (ulong)data.Length - offset);
                    _gd.Api.CmdUpdateBuffer(cbs.CommandBuffer, dstBuffer, (ulong)dstOffset + offset, size, pData + offset);
                    offset += size;
                }
            }

            InsertBufferBarrier(
                _gd,
                cbs.CommandBuffer,
                dstBuffer,
                AccessFlags.TransferWriteBit,
                DefaultAccessFlags,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.AllCommandsBit,
                dstOffset,
                data.Length);

            return true;
        }

        public static unsafe void Copy(
            VulkanRenderer gd,
            CommandBufferScoped cbs,
            Auto<DisposableBuffer> src,
            Auto<DisposableBuffer> dst,
            int srcOffset,
            int dstOffset,
            int size)
        {
            var srcBuffer = src.Get(cbs, srcOffset, size).Value;
            var dstBuffer = dst.Get(cbs, dstOffset, size).Value;

            InsertBufferBarrier(
                gd,
                cbs.CommandBuffer,
                dstBuffer,
                DefaultAccessFlags,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.TransferBit,
                dstOffset,
                size);

            var region = new BufferCopy((ulong)srcOffset, (ulong)dstOffset, (ulong)size);

            gd.Api.CmdCopyBuffer(cbs.CommandBuffer, srcBuffer, dstBuffer, 1, &region);

            InsertBufferBarrier(
                gd,
                cbs.CommandBuffer,
                dstBuffer,
                AccessFlags.TransferWriteBit,
                DefaultAccessFlags,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.AllCommandsBit,
                dstOffset,
                size);
        }

        public static unsafe void InsertBufferBarrier(
            VulkanRenderer gd,
            CommandBuffer commandBuffer,
            VkBuffer buffer,
            AccessFlags srcAccessMask,
            AccessFlags dstAccessMask,
            PipelineStageFlags srcStageMask,
            PipelineStageFlags dstStageMask,
            int offset,
            int size)
        {
            BufferMemoryBarrier memoryBarrier = new BufferMemoryBarrier()
            {
                SType = StructureType.BufferMemoryBarrier,
                SrcAccessMask = srcAccessMask,
                DstAccessMask = dstAccessMask,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Buffer = buffer,
                Offset = (ulong)offset,
                Size = (ulong)size
            };

            gd.Api.CmdPipelineBarrier(
                commandBuffer,
                srcStageMask,
                dstStageMask,
                0,
                0,
                null,
                1,
                memoryBarrier,
                0,
                null);
        }

        public void WaitForFences()
        {
            _waitable.WaitForFences(_gd.Api, _device);
        }

        public void WaitForFences(int offset, int size)
        {
            _waitable.WaitForFences(_gd.Api, _device, offset, size);
        }

        private bool BoundToRange(int offset, ref int size)
        {
            if (offset >= Size)
            {
                return false;
            }

            size = Math.Min(Size - offset, size);

            return true;
        }

        public Auto<DisposableBuffer> GetBufferI8ToI16(CommandBufferScoped cbs, int offset, int size)
        {
            if (!BoundToRange(offset, ref size))
            {
                return null;
            }

            var key = new I8ToI16CacheKey(_gd);

            if (!_cachedConvertedBuffers.TryGetValue(offset, size, key, out var holder))
            {
                holder = _gd.BufferManager.Create(_gd, (size * 2 + 3) & ~3);

                _gd.PipelineInternal.EndRenderPass();
                _gd.HelperShader.ConvertI8ToI16(_gd, cbs, this, holder, offset, size);

                key.SetBuffer(holder.GetBuffer());

                _cachedConvertedBuffers.Add(offset, size, key, holder);
            }

            return holder.GetBuffer();
        }

        public Auto<DisposableBuffer> GetAlignedVertexBuffer(CommandBufferScoped cbs, int offset, int size, int stride, int alignment)
        {
            if (!BoundToRange(offset, ref size))
            {
                return null;
            }

            var key = new AlignedVertexBufferCacheKey(_gd, stride, alignment);

            if (!_cachedConvertedBuffers.TryGetValue(offset, size, key, out var holder))
            {
                int alignedStride = (stride + (alignment - 1)) & -alignment;

                holder = _gd.BufferManager.Create(_gd, (size / stride) * alignedStride);

                _gd.PipelineInternal.EndRenderPass();
                _gd.HelperShader.ChangeStride(_gd, cbs, this, holder, offset, size, stride, alignedStride);

                key.SetBuffer(holder.GetBuffer());

                _cachedConvertedBuffers.Add(offset, size, key, holder);
            }

            return holder.GetBuffer();
        }

        public Auto<DisposableBuffer> GetBufferTopologyConversion(CommandBufferScoped cbs, int offset, int size, IndexBufferPattern pattern, int indexSize)
        {
            if (!BoundToRange(offset, ref size))
            {
                return null;
            }

            var key = new TopologyConversionCacheKey(_gd, pattern, indexSize);

            if (!_cachedConvertedBuffers.TryGetValue(offset, size, key, out var holder))
            {
                // The destination index size is always I32.

                int indexCount = size / indexSize;

                int convertedCount = pattern.GetConvertedCount(indexCount);

                holder = _gd.BufferManager.Create(_gd, convertedCount * 4);

                _gd.PipelineInternal.EndRenderPass();
                _gd.HelperShader.ConvertIndexBuffer(_gd, cbs, this, holder, pattern, indexSize, offset, indexCount);

                key.SetBuffer(holder.GetBuffer());

                _cachedConvertedBuffers.Add(offset, size, key, holder);
            }

            return holder.GetBuffer();
        }

        public bool TryGetCachedConvertedBuffer(int offset, int size, ICacheKey key, out BufferHolder holder)
        {
            return _cachedConvertedBuffers.TryGetValue(offset, size, key, out holder);
        }

        public void AddCachedConvertedBuffer(int offset, int size, ICacheKey key, BufferHolder holder)
        {
            _cachedConvertedBuffers.Add(offset, size, key, holder);
        }

        public void AddCachedConvertedBufferDependency(int offset, int size, ICacheKey key, Dependency dependency)
        {
            _cachedConvertedBuffers.AddDependency(offset, size, key, dependency);
        }

        public void RemoveCachedConvertedBuffer(int offset, int size, ICacheKey key)
        {
            _cachedConvertedBuffers.Remove(offset, size, key);
        }

        public void Dispose()
        {
            _gd.PipelineInternal?.FlushCommandsIfWeightExceeding(_buffer, (ulong)Size);

            _buffer.Dispose();
            _allocationAuto.Dispose();
            _cachedConvertedBuffers.Dispose();

            _flushLock.AcquireWriterLock(Timeout.Infinite);

            ClearFlushFence();

            _flushLock.ReleaseWriterLock();
        }
    }
}
