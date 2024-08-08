using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class FenceHolder : IDisposable
    {
        private MTLCommandBuffer _fence;
        private int _referenceCount;
        private bool _disposed;

        public FenceHolder(MTLCommandBuffer fence)
        {
            _fence = fence;
            _referenceCount = 1;
        }

        public MTLCommandBuffer GetUnsafe()
        {
            return _fence;
        }

        public bool TryGet(out MTLCommandBuffer fence)
        {
            int lastValue;
            do
            {
                lastValue = _referenceCount;

                if (lastValue == 0)
                {
                    fence = default;
                    return false;
                }
            } while (Interlocked.CompareExchange(ref _referenceCount, lastValue + 1, lastValue) != lastValue);

            fence = _fence;
            return true;
        }

        public MTLCommandBuffer Get()
        {
            Interlocked.Increment(ref _referenceCount);
            return _fence;
        }

        public void Put()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                _fence = default;
            }
        }

        public void Wait()
        {
            _fence.WaitUntilCompleted();
        }

        public bool IsSignaled()
        {
            return _fence.Status == MTLCommandBufferStatus.Completed;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Put();
                _disposed = true;
            }
        }
    }
}
