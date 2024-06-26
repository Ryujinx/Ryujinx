using Silk.NET.Vulkan;
using System;

namespace Ryujinx.Graphics.Vulkan
{
    readonly struct CommandBufferScoped : IDisposable
    {
        private readonly CommandBufferPool _pool;
        public CommandBuffer CommandBuffer { get; }
        public int CommandBufferIndex { get; }

        public CommandBufferScoped(CommandBufferPool pool, CommandBuffer commandBuffer, int commandBufferIndex)
        {
            _pool = pool;
            CommandBuffer = commandBuffer;
            CommandBufferIndex = commandBufferIndex;
        }

        public void AddDependant(IAuto dependant)
        {
            _pool.AddDependant(CommandBufferIndex, dependant);
        }

        public void AddWaitable(MultiFenceHolder waitable)
        {
            _pool.AddWaitable(CommandBufferIndex, waitable);
        }

        public FenceHolder GetFence()
        {
            return _pool.GetFence(CommandBufferIndex);
        }

        public void Dispose()
        {
            _pool?.Return(this);
        }
    }
}
