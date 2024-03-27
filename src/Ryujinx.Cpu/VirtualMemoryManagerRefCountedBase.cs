using Ryujinx.Memory;
using System.Diagnostics;
using System.Threading;

namespace Ryujinx.Cpu
{
    public abstract class VirtualMemoryManagerRefCountedBase : VirtualMemoryManagerBase, IRefCounted
    {
        private int _referenceCount;

        public void IncrementReferenceCount()
        {
            int newRefCount = Interlocked.Increment(ref _referenceCount);

            Debug.Assert(newRefCount >= 1);
        }

        public void DecrementReferenceCount()
        {
            int newRefCount = Interlocked.Decrement(ref _referenceCount);

            Debug.Assert(newRefCount >= 0);

            if (newRefCount == 0)
            {
                Destroy();
            }
        }

        protected abstract void Destroy();
    }
}
