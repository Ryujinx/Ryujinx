using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using System;
using System.Threading;

namespace Ryujinx.Graphics.OpenGL.Queries
{
    class CounterQueueEvent : ICounterEvent
    {
        public event EventHandler<ulong> OnResult;

        public QueryTarget Type { get; }
        public bool ClearCounter { get; private set; }
        public int Query => _counter.Query;

        public bool Disposed { get; private set; }
        public bool Invalid { get; set; }

        public ulong DrawIndex { get; private set; }

        private readonly CounterQueue _queue;
        private readonly BufferedQuery _counter;

        private int _hostAccessReserved = 0;
        private int _refCount = 1; // Starts with a reference from the counter queue.

        private readonly object _lock = new();
        private ulong _result = ulong.MaxValue;
        private double _divisor = 1f;

        public CounterQueueEvent(CounterQueue queue, QueryTarget type, ulong drawIndex)
        {
            _queue = queue;

            _counter = queue.GetQueryObject();
            Type = type;

            DrawIndex = drawIndex;

            _counter.Begin();
        }

        internal void Clear(ulong drawIndex)
        {
            _counter.Reset();
            ClearCounter = true;
            DrawIndex = drawIndex;
        }

        internal void Complete(bool withResult, double divisor)
        {
            _counter.End(withResult);

            _divisor = divisor;
        }

        internal bool TryConsume(ref ulong result, bool block, AutoResetEvent wakeSignal = null)
        {
            lock (_lock)
            {
                if (Disposed)
                {
                    return true;
                }

                if (ClearCounter || Type == QueryTarget.Timestamp)
                {
                    result = 0;
                }

                long queryResult;

                if (block)
                {
                    queryResult = _counter.AwaitResult(wakeSignal);
                }
                else
                {
                    if (!_counter.TryGetResult(out queryResult))
                    {
                        return false;
                    }
                }

                result += _divisor == 1 ? (ulong)queryResult : (ulong)Math.Ceiling(queryResult / _divisor);

                _result = result;

                OnResult?.Invoke(this, result);

                Dispose(); // Return the our resources to the pool.

                return true;
            }
        }

        public void Flush()
        {
            if (Disposed)
            {
                return;
            }

            // Tell the queue to process all events up to this one.
            _queue.FlushTo(this);
        }

        public void DecrementRefCount()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                DisposeInternal();
            }
        }

        public bool ReserveForHostAccess()
        {
            if (_hostAccessReserved == 0 && IsValueAvailable())
            {
                return false;
            }

            if (Interlocked.Increment(ref _refCount) == 1)
            {
                Interlocked.Decrement(ref _refCount);

                return false;
            }

            Interlocked.Increment(ref _hostAccessReserved);

            return true;
        }

        public void ReleaseHostAccess()
        {
            Interlocked.Decrement(ref _hostAccessReserved);

            DecrementRefCount();
        }

        private void DisposeInternal()
        {
            _queue.ReturnQueryObject(_counter);
        }

        private bool IsValueAvailable()
        {
            return _result != ulong.MaxValue || _counter.TryGetResult(out _);
        }

        public void Dispose()
        {
            Disposed = true;

            DecrementRefCount();
        }
    }
}
