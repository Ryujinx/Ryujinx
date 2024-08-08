using SharpMetal.Metal;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class BackgroundResource : IDisposable
    {
        private readonly MetalRenderer _renderer;

        private CommandBufferPool _pool;
        private PersistentFlushBuffer _flushBuffer;

        public BackgroundResource(MetalRenderer renderer)
        {
            _renderer = renderer;
        }

        public CommandBufferPool GetPool()
        {
            if (_pool == null)
            {
                MTLCommandQueue queue = _renderer.BackgroundQueue;
                _pool = new CommandBufferPool(queue, true);
                _pool.Initialize(null); // TODO: Proper encoder factory for background render/compute
            }

            return _pool;
        }

        public PersistentFlushBuffer GetFlushBuffer()
        {
            _flushBuffer ??= new PersistentFlushBuffer(_renderer);

            return _flushBuffer;
        }

        public void Dispose()
        {
            _pool?.Dispose();
            _flushBuffer?.Dispose();
        }
    }

    [SupportedOSPlatform("macos")]
    class BackgroundResources : IDisposable
    {
        private readonly MetalRenderer _renderer;

        private readonly Dictionary<Thread, BackgroundResource> _resources;

        public BackgroundResources(MetalRenderer renderer)
        {
            _renderer = renderer;

            _resources = new Dictionary<Thread, BackgroundResource>();
        }

        private void Cleanup()
        {
            lock (_resources)
            {
                foreach (KeyValuePair<Thread, BackgroundResource> tuple in _resources)
                {
                    if (!tuple.Key.IsAlive)
                    {
                        tuple.Value.Dispose();
                        _resources.Remove(tuple.Key);
                    }
                }
            }
        }

        public BackgroundResource Get()
        {
            Thread thread = Thread.CurrentThread;

            lock (_resources)
            {
                if (!_resources.TryGetValue(thread, out BackgroundResource resource))
                {
                    Cleanup();

                    resource = new BackgroundResource(_renderer);

                    _resources[thread] = resource;
                }

                return resource;
            }
        }

        public void Dispose()
        {
            lock (_resources)
            {
                foreach (var resource in _resources.Values)
                {
                    resource.Dispose();
                }
            }
        }
    }
}
