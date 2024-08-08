using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    readonly struct CommandBufferScoped : IDisposable
    {
        private readonly CommandBufferPool _pool;
        public MTLCommandBuffer CommandBuffer { get; }
        public CommandBufferEncoder Encoders { get; }
        public int CommandBufferIndex { get; }

        public CommandBufferScoped(CommandBufferPool pool, MTLCommandBuffer commandBuffer, CommandBufferEncoder encoders, int commandBufferIndex)
        {
            _pool = pool;
            CommandBuffer = commandBuffer;
            Encoders = encoders;
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
