using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Ryujinx.Graphics.Vulkan.Queries
{
    class CounterQueue : IDisposable
    {
        private const int QueryPoolInitialSize = 100;

        private readonly VulkanRenderer _gd;
        private readonly Device _device;
        private readonly PipelineFull _pipeline;

        public CounterType Type { get; }
        public bool Disposed { get; private set; }

        private readonly Queue<CounterQueueEvent> _events = new();
        private CounterQueueEvent _current;

        private ulong _accumulatedCounter;
        private int _waiterCount;

        private readonly Lock _lock = new();

        private readonly Queue<BufferedQuery> _queryPool;
        private readonly AutoResetEvent _queuedEvent = new(false);
        private readonly AutoResetEvent _wakeSignal = new(false);
        private readonly AutoResetEvent _eventConsumed = new(false);

        private readonly Thread _consumerThread;

        public int ResetSequence { get; private set; }

        internal CounterQueue(VulkanRenderer gd, Device device, PipelineFull pipeline, CounterType type)
        {
            _gd = gd;
            _device = device;
            _pipeline = pipeline;

            Type = type;

            _queryPool = new Queue<BufferedQuery>(QueryPoolInitialSize);
            for (int i = 0; i < QueryPoolInitialSize; i++)
            {
                // AMD Polaris GPUs on Windows seem to have issues reporting 64-bit query results.
                _queryPool.Enqueue(new BufferedQuery(_gd, _device, _pipeline, type, gd.IsAmdWindows));
            }

            _current = new CounterQueueEvent(this, type, 0);

            _consumerThread = new Thread(EventConsumer);
            _consumerThread.Start();
        }

        public void ResetCounterPool()
        {
            ResetSequence++;
        }

        public void ResetFutureCounters(CommandBuffer cmd, int count)
        {
            // Pre-emptively reset queries to avoid render pass splitting.
            lock (_queryPool)
            {
                count = Math.Min(count, _queryPool.Count);

                if (count > 0)
                {
                    foreach (BufferedQuery query in _queryPool)
                    {
                        query.PoolReset(cmd, ResetSequence);

                        if (--count == 0)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private void EventConsumer()
        {
            while (!Disposed)
            {
                CounterQueueEvent evt = null;
                lock (_lock)
                {
                    if (_events.Count > 0)
                    {
                        evt = _events.Dequeue();
                    }
                }

                if (evt == null)
                {
                    _queuedEvent.WaitOne(); // No more events to go through, wait for more.
                }
                else
                {
                    // Spin-wait rather than sleeping if there are any waiters, by passing null instead of the wake signal.
                    evt.TryConsume(ref _accumulatedCounter, true, _waiterCount == 0 ? _wakeSignal : null);
                }

                if (_waiterCount > 0)
                {
                    _eventConsumed.Set();
                }
            }
        }

        internal BufferedQuery GetQueryObject()
        {
            // Creating/disposing query objects on a context we're sharing with will cause issues.
            // So instead, make a lot of query objects on the main thread and reuse them.

            lock (_lock)
            {
                if (_queryPool.Count > 0)
                {
                    BufferedQuery result = _queryPool.Dequeue();
                    return result;
                }

                return new BufferedQuery(_gd, _device, _pipeline, Type, _gd.IsAmdWindows);
            }
        }

        internal void ReturnQueryObject(BufferedQuery query)
        {
            lock (_lock)
            {
                // The query will be reset when it dequeues.
                _queryPool.Enqueue(query);
            }
        }

        public CounterQueueEvent QueueReport(EventHandler<ulong> resultHandler, float divisor, ulong lastDrawIndex, bool hostReserved)
        {
            CounterQueueEvent result;
            ulong draws = lastDrawIndex - _current.DrawIndex;

            lock (_lock)
            {
                // A query's result only matters if more than one draw was performed during it.
                // Otherwise, dummy it out and return 0 immediately.

                if (hostReserved)
                {
                    // This counter event is guaranteed to be available for host conditional rendering.
                    _current.ReserveForHostAccess();
                }

                _current.Complete(draws > 0 && Type != CounterType.TransformFeedbackPrimitivesWritten, divisor);
                _events.Enqueue(_current);

                _current.OnResult += resultHandler;

                result = _current;

                _current = new CounterQueueEvent(this, Type, lastDrawIndex);
            }

            _queuedEvent.Set();

            return result;
        }

        public void QueueReset(ulong lastDrawIndex)
        {
            ulong draws = lastDrawIndex - _current.DrawIndex;

            lock (_lock)
            {
                _current.Clear(draws != 0);
            }
        }

        public void Flush(bool blocking)
        {
            if (!blocking)
            {
                // Just wake the consumer thread - it will update the queries.
                _wakeSignal.Set();
                return;
            }

            lock (_lock)
            {
                // Tell the queue to process all events.
                while (_events.Count > 0)
                {
                    CounterQueueEvent flush = _events.Peek();
                    if (!flush.TryConsume(ref _accumulatedCounter, true))
                    {
                        return; // If not blocking, then return when we encounter an event that is not ready yet.
                    }
                    _events.Dequeue();
                }
            }
        }

        public void FlushTo(CounterQueueEvent evt)
        {
            // Flush the counter queue on the main thread.
            Interlocked.Increment(ref _waiterCount);

            _wakeSignal.Set();

            while (!evt.Disposed)
            {
                _eventConsumed.WaitOne(1);
            }

            Interlocked.Decrement(ref _waiterCount);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                while (_events.Count > 0)
                {
                    CounterQueueEvent evt = _events.Dequeue();

                    evt.Dispose();
                }

                Disposed = true;
            }

            _queuedEvent.Set();

            _consumerThread.Join();

            _current?.Dispose();

            foreach (BufferedQuery query in _queryPool)
            {
                query.Dispose();
            }

            _queuedEvent.Dispose();
            _wakeSignal.Dispose();
            _eventConsumed.Dispose();
        }
    }
}
