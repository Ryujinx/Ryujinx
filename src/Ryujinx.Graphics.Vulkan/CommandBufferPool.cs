using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Ryujinx.Graphics.Vulkan
{
    class CommandBufferPool : IDisposable
    {
        public const int MaxCommandBuffers = 16;

        private readonly int _totalCommandBuffers;
        private readonly int _totalCommandBuffersMask;

        private readonly Vk _api;
        private readonly Device _device;
        private readonly Queue _queue;
        private readonly Lock _queueLock;
        private readonly bool _concurrentFenceWaitUnsupported;
        private readonly CommandPool _pool;
        private readonly Thread _owner;

        public bool OwnedByCurrentThread => _owner == Thread.CurrentThread;

        private struct ReservedCommandBuffer
        {
            public bool InUse;
            public bool InConsumption;
            public int SubmissionCount;
            public CommandBuffer CommandBuffer;
            public FenceHolder Fence;

            public List<IAuto> Dependants;
            public List<MultiFenceHolder> Waitables;

            public void Initialize(Vk api, Device device, CommandPool pool)
            {
                var allocateInfo = new CommandBufferAllocateInfo
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    CommandBufferCount = 1,
                    CommandPool = pool,
                    Level = CommandBufferLevel.Primary,
                };

                api.AllocateCommandBuffers(device, allocateInfo, out CommandBuffer);

                Dependants = new List<IAuto>();
                Waitables = new List<MultiFenceHolder>();
            }
        }

        private readonly ReservedCommandBuffer[] _commandBuffers;

        private readonly int[] _queuedIndexes;
        private int _queuedIndexesPtr;
        private int _queuedCount;
        private int _inUseCount;

        public unsafe CommandBufferPool(
            Vk api,
            Device device,
            Queue queue,
            Lock queueLock,
            uint queueFamilyIndex,
            bool concurrentFenceWaitUnsupported,
            bool isLight = false)
        {
            _api = api;
            _device = device;
            _queue = queue;
            _queueLock = queueLock;
            _concurrentFenceWaitUnsupported = concurrentFenceWaitUnsupported;
            _owner = Thread.CurrentThread;

            var commandPoolCreateInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamilyIndex,
                Flags = CommandPoolCreateFlags.TransientBit |
                        CommandPoolCreateFlags.ResetCommandBufferBit,
            };

            api.CreateCommandPool(device, commandPoolCreateInfo, null, out _pool).ThrowOnError();

            // We need at least 2 command buffers to get texture data in some cases.
            _totalCommandBuffers = isLight ? 2 : MaxCommandBuffers;
            _totalCommandBuffersMask = _totalCommandBuffers - 1;

            _commandBuffers = new ReservedCommandBuffer[_totalCommandBuffers];

            _queuedIndexes = new int[_totalCommandBuffers];
            _queuedIndexesPtr = 0;
            _queuedCount = 0;

            for (int i = 0; i < _totalCommandBuffers; i++)
            {
                _commandBuffers[i].Initialize(api, device, _pool);
                WaitAndDecrementRef(i);
            }
        }

        public void AddDependant(int cbIndex, IAuto dependant)
        {
            dependant.IncrementReferenceCount();
            _commandBuffers[cbIndex].Dependants.Add(dependant);
        }

        public void AddWaitable(MultiFenceHolder waitable)
        {
            lock (_commandBuffers)
            {
                for (int i = 0; i < _totalCommandBuffers; i++)
                {
                    ref var entry = ref _commandBuffers[i];

                    if (entry.InConsumption)
                    {
                        AddWaitable(i, waitable);
                    }
                }
            }
        }

        public void AddInUseWaitable(MultiFenceHolder waitable)
        {
            lock (_commandBuffers)
            {
                for (int i = 0; i < _totalCommandBuffers; i++)
                {
                    ref var entry = ref _commandBuffers[i];

                    if (entry.InUse)
                    {
                        AddWaitable(i, waitable);
                    }
                }
            }
        }

        public void AddWaitable(int cbIndex, MultiFenceHolder waitable)
        {
            ref var entry = ref _commandBuffers[cbIndex];
            if (waitable.AddFence(cbIndex, entry.Fence))
            {
                entry.Waitables.Add(waitable);
            }
        }

        public bool HasWaitableOnRentedCommandBuffer(MultiFenceHolder waitable, int offset, int size)
        {
            lock (_commandBuffers)
            {
                for (int i = 0; i < _totalCommandBuffers; i++)
                {
                    ref var entry = ref _commandBuffers[i];

                    if (entry.InUse &&
                        waitable.HasFence(i) &&
                        waitable.IsBufferRangeInUse(i, offset, size))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsFenceOnRentedCommandBuffer(FenceHolder fence)
        {
            lock (_commandBuffers)
            {
                for (int i = 0; i < _totalCommandBuffers; i++)
                {
                    ref var entry = ref _commandBuffers[i];

                    if (entry.InUse && entry.Fence == fence)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public FenceHolder GetFence(int cbIndex)
        {
            return _commandBuffers[cbIndex].Fence;
        }

        public int GetSubmissionCount(int cbIndex)
        {
            return _commandBuffers[cbIndex].SubmissionCount;
        }

        private int FreeConsumed(bool wait)
        {
            int freeEntry = 0;

            while (_queuedCount > 0)
            {
                int index = _queuedIndexes[_queuedIndexesPtr];

                ref var entry = ref _commandBuffers[index];

                if (wait || !entry.InConsumption || entry.Fence.IsSignaled())
                {
                    WaitAndDecrementRef(index);

                    wait = false;
                    freeEntry = index;

                    _queuedCount--;
                    _queuedIndexesPtr = (_queuedIndexesPtr + 1) % _totalCommandBuffers;
                }
                else
                {
                    break;
                }
            }

            return freeEntry;
        }

        public CommandBufferScoped ReturnAndRent(CommandBufferScoped cbs)
        {
            Return(cbs);
            return Rent();
        }

        public CommandBufferScoped Rent()
        {
            lock (_commandBuffers)
            {
                int cursor = FreeConsumed(_inUseCount + _queuedCount == _totalCommandBuffers);

                for (int i = 0; i < _totalCommandBuffers; i++)
                {
                    ref var entry = ref _commandBuffers[cursor];

                    if (!entry.InUse && !entry.InConsumption)
                    {
                        entry.InUse = true;

                        _inUseCount++;

                        var commandBufferBeginInfo = new CommandBufferBeginInfo
                        {
                            SType = StructureType.CommandBufferBeginInfo,
                        };

                        _api.BeginCommandBuffer(entry.CommandBuffer, commandBufferBeginInfo).ThrowOnError();

                        return new CommandBufferScoped(this, entry.CommandBuffer, cursor);
                    }

                    cursor = (cursor + 1) & _totalCommandBuffersMask;
                }
            }

            throw new InvalidOperationException($"Out of command buffers (In use: {_inUseCount}, queued: {_queuedCount}, total: {_totalCommandBuffers})");
        }

        public void Return(CommandBufferScoped cbs)
        {
            Return(cbs, null, null, null);
        }

        public unsafe void Return(
            CommandBufferScoped cbs,
            ReadOnlySpan<Semaphore> waitSemaphores,
            ReadOnlySpan<PipelineStageFlags> waitDstStageMask,
            ReadOnlySpan<Semaphore> signalSemaphores)
        {
            lock (_commandBuffers)
            {
                int cbIndex = cbs.CommandBufferIndex;

                ref var entry = ref _commandBuffers[cbIndex];

                Debug.Assert(entry.InUse);
                Debug.Assert(entry.CommandBuffer.Handle == cbs.CommandBuffer.Handle);
                entry.InUse = false;
                entry.InConsumption = true;
                entry.SubmissionCount++;
                _inUseCount--;

                var commandBuffer = entry.CommandBuffer;

                _api.EndCommandBuffer(commandBuffer).ThrowOnError();

                fixed (Semaphore* pWaitSemaphores = waitSemaphores, pSignalSemaphores = signalSemaphores)
                {
                    fixed (PipelineStageFlags* pWaitDstStageMask = waitDstStageMask)
                    {
                        SubmitInfo sInfo = new()
                        {
                            SType = StructureType.SubmitInfo,
                            WaitSemaphoreCount = !waitSemaphores.IsEmpty ? (uint)waitSemaphores.Length : 0,
                            PWaitSemaphores = pWaitSemaphores,
                            PWaitDstStageMask = pWaitDstStageMask,
                            CommandBufferCount = 1,
                            PCommandBuffers = &commandBuffer,
                            SignalSemaphoreCount = !signalSemaphores.IsEmpty ? (uint)signalSemaphores.Length : 0,
                            PSignalSemaphores = pSignalSemaphores,
                        };

                        lock (_queueLock)
                        {
                            _api.QueueSubmit(_queue, 1, sInfo, entry.Fence.GetUnsafe()).ThrowOnError();
                        }
                    }
                }

                int ptr = (_queuedIndexesPtr + _queuedCount) % _totalCommandBuffers;
                _queuedIndexes[ptr] = cbIndex;
                _queuedCount++;
            }
        }

        private void WaitAndDecrementRef(int cbIndex, bool refreshFence = true)
        {
            ref var entry = ref _commandBuffers[cbIndex];

            if (entry.InConsumption)
            {
                entry.Fence.Wait();
                entry.InConsumption = false;
            }

            foreach (var dependant in entry.Dependants)
            {
                dependant.DecrementReferenceCount(cbIndex);
            }

            foreach (var waitable in entry.Waitables)
            {
                waitable.RemoveFence(cbIndex);
                waitable.RemoveBufferUses(cbIndex);
            }

            entry.Dependants.Clear();
            entry.Waitables.Clear();
            entry.Fence?.Dispose();

            if (refreshFence)
            {
                entry.Fence = new FenceHolder(_api, _device, _concurrentFenceWaitUnsupported);
            }
            else
            {
                entry.Fence = null;
            }
        }

        public unsafe void Dispose()
        {
            for (int i = 0; i < _totalCommandBuffers; i++)
            {
                WaitAndDecrementRef(i, refreshFence: false);
            }

            _api.DestroyCommandPool(_device, _pool, null);
        }
    }
}
