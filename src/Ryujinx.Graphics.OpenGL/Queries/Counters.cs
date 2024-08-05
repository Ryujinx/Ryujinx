using Ryujinx.Graphics.GAL;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.OpenGL.Queries
{
    class Counters : IDisposable
    {
        private const int ForceCopyThreshold = 32;

        private readonly CounterQueue[] _counterQueues;
        private readonly List<BufferedQuery> _queuedCopies;
        private bool _flushedThisPass;

        public Counters()
        {
            int count = Enum.GetNames<CounterType>().Length;

            _counterQueues = new CounterQueue[count];
            _queuedCopies = new List<BufferedQuery>();
        }

        public void Initialize()
        {
            for (int index = 0; index < _counterQueues.Length; index++)
            {
                CounterType type = (CounterType)index;
                _counterQueues[index] = new CounterQueue(this, type);
            }
        }

        public CounterQueueEvent QueueReport(CounterType type, EventHandler<ulong> resultHandler, float divisor, ulong lastDrawIndex, int hostReserved)
        {
            return _counterQueues[(int)type].QueueReport(resultHandler, divisor, lastDrawIndex, hostReserved);
        }

        public void QueueReset(CounterType type, ulong lastDrawIndex)
        {
            _counterQueues[(int)type].QueueReset(lastDrawIndex);
        }

        public void Update()
        {
            foreach (var queue in _counterQueues)
            {
                queue.Flush(false);
            }
        }

        public void Flush(CounterType type)
        {
            _counterQueues[(int)type].Flush(true);
        }

        public void PreDraw()
        {
            // Force results to be copied some time into an occlusion query pass.
            if (!_flushedThisPass && _queuedCopies.Count > ForceCopyThreshold)
            {
                _flushedThisPass = true;
                CopyPending(false);
            }
        }

        public void CopyPending(bool newPass = true)
        {
            if (_queuedCopies.Count > 0)
            {
                int i = 0;
                foreach (BufferedQuery query in _queuedCopies)
                {
                    query.CopyQueryResult(_queuedCopies.Count == ++i);
                }

                _queuedCopies.Clear();
            }

            if (newPass)
            {
                _flushedThisPass = false;
            }
        }

        public bool QueueCopy(BufferedQuery query)
        {
            if (HwCapabilities.Vendor == HwCapabilities.GpuVendor.Nvidia)
            {
                // NVIDIA seems to make up a rule where query results can't be copied to buffers
                // when unrelated query objects are in use.
                return false;
            }
            else
            {
                _queuedCopies.Add(query);

                return true;
            }
        }

        public void Dispose()
        {
            foreach (var queue in _counterQueues)
            {
                queue.Dispose();
            }
        }
    }
}
