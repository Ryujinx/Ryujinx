using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;

namespace Ryujinx.Graphics.Vulkan.Queries
{
    class Counters : IDisposable
    {
        private readonly CounterQueue[] _counterQueues;
        private readonly PipelineFull _pipeline;

        public Counters(VulkanRenderer gd, Device device, PipelineFull pipeline)
        {
            _pipeline = pipeline;

            int count = Enum.GetNames(typeof(CounterType)).Length;

            _counterQueues = new CounterQueue[count];

            for (int index = 0; index < _counterQueues.Length; index++)
            {
                CounterType type = (CounterType)index;
                _counterQueues[index] = new CounterQueue(gd, device, pipeline, type);
            }
        }

        public void ResetCounterPool()
        {
            foreach (var queue in _counterQueues)
            {
                queue.ResetCounterPool();
            }
        }

        public void ResetFutureCounters(CommandBuffer cmd, int count)
        {
            _counterQueues[(int)CounterType.SamplesPassed].ResetFutureCounters(cmd, count);
        }

        public CounterQueueEvent QueueReport(CounterType type, EventHandler<ulong> resultHandler, float divisor, int hostReserved)
        {
            return _counterQueues[(int)type].QueueReport(resultHandler, divisor, _pipeline.DrawCount, hostReserved);
        }

        public void QueueReset(CounterType type)
        {
            _counterQueues[(int)type].QueueReset(_pipeline.DrawCount);
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

        public void Dispose()
        {
            foreach (var queue in _counterQueues)
            {
                queue.Dispose();
            }
        }
    }
}
