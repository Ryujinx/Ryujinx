using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Graphics.Metal
{
    interface IAuto
    {
        bool HasCommandBufferDependency(CommandBufferScoped cbs);

        void IncrementReferenceCount();
        void DecrementReferenceCount(int cbIndex);
        void DecrementReferenceCount();
    }

    interface IAutoPrivate : IAuto
    {
        void AddCommandBufferDependencies(CommandBufferScoped cbs);
    }

    [SupportedOSPlatform("macos")]
    class Auto<T> : IAutoPrivate, IDisposable where T : IDisposable
    {
        private int _referenceCount;
        private T _value;

        private readonly BitMap _cbOwnership;
        private readonly MultiFenceHolder _waitable;

        private bool _disposed;
        private bool _destroyed;

        public Auto(T value)
        {
            _referenceCount = 1;
            _value = value;
            _cbOwnership = new BitMap(CommandBufferPool.MaxCommandBuffers);
        }

        public Auto(T value, MultiFenceHolder waitable) : this(value)
        {
            _waitable = waitable;
        }

        public T Get(CommandBufferScoped cbs, int offset, int size, bool write = false)
        {
            _waitable?.AddBufferUse(cbs.CommandBufferIndex, offset, size, write);
            return Get(cbs);
        }

        public T GetUnsafe()
        {
            return _value;
        }

        public T Get(CommandBufferScoped cbs)
        {
            if (!_destroyed)
            {
                AddCommandBufferDependencies(cbs);
            }

            return _value;
        }

        public bool HasCommandBufferDependency(CommandBufferScoped cbs)
        {
            return _cbOwnership.IsSet(cbs.CommandBufferIndex);
        }

        public bool HasRentedCommandBufferDependency(CommandBufferPool cbp)
        {
            return _cbOwnership.AnySet();
        }

        public void AddCommandBufferDependencies(CommandBufferScoped cbs)
        {
            // We don't want to add a reference to this object to the command buffer
            // more than once, so if we detect that the command buffer already has ownership
            // of this object, then we can just return without doing anything else.
            if (_cbOwnership.Set(cbs.CommandBufferIndex))
            {
                if (_waitable != null)
                {
                    cbs.AddWaitable(_waitable);
                }

                cbs.AddDependant(this);
            }
        }

        public bool TryIncrementReferenceCount()
        {
            int lastValue;
            do
            {
                lastValue = _referenceCount;

                if (lastValue == 0)
                {
                    return false;
                }
            }
            while (Interlocked.CompareExchange(ref _referenceCount, lastValue + 1, lastValue) != lastValue);

            return true;
        }

        public void IncrementReferenceCount()
        {
            if (Interlocked.Increment(ref _referenceCount) == 1)
            {
                Interlocked.Decrement(ref _referenceCount);
                throw new InvalidOperationException("Attempted to increment the reference count of an object that was already destroyed.");
            }
        }

        public void DecrementReferenceCount(int cbIndex)
        {
            _cbOwnership.Clear(cbIndex);
            DecrementReferenceCount();
        }

        public void DecrementReferenceCount()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                _value.Dispose();
                _value = default;
                _destroyed = true;
            }

            Debug.Assert(_referenceCount >= 0);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DecrementReferenceCount();
                _disposed = true;
            }
        }
    }
}
