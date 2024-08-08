using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Common.PreciseSleep
{
    /// <summary>
    /// A pool of threads used to allow "interruptable" nanosleep for a single target event.
    /// </summary>
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("ios")]
    internal class NanosleepPool : IDisposable
    {
        public const int MaxThreads = 8;

        /// <summary>
        /// A thread that nanosleeps and may signal an event on wake.
        /// When a thread is assigned a nanosleep to perform, it also gets a signal ID.
        /// The pool's target event is only signalled if this ID matches the latest dispatched one.
        /// </summary>
        private class NanosleepThread : IDisposable
        {
            private static readonly long _timePointEpsilon;

            static NanosleepThread()
            {
                _timePointEpsilon = PerformanceCounter.TicksPerMillisecond / 100; // 0.01ms
            }

            private readonly Thread _thread;
            private readonly NanosleepPool _parent;
            private readonly AutoResetEvent _newWaitEvent;
            private bool _running = true;

            private long _signalId;
            private long _nanoseconds;
            private long _timePoint;

            public long SignalId => _signalId;

            /// <summary>
            /// Creates a new NanosleepThread for a parent pool, with a specified thread ID.
            /// </summary>
            /// <param name="parent">Parent NanosleepPool</param>
            /// <param name="id">Thread ID</param>
            public NanosleepThread(NanosleepPool parent, int id)
            {
                _parent = parent;
                _newWaitEvent = new(false);

                _thread = new Thread(Loop)
                {
                    Name = $"Common.Nanosleep.{id}",
                    Priority = ThreadPriority.AboveNormal,
                    IsBackground = true
                };

                _thread.Start();
            }

            /// <summary>
            /// Service requests to perform a nanosleep, signal parent pool when complete.
            /// </summary>
            private void Loop()
            {
                _newWaitEvent.WaitOne();

                while (_running)
                {
                    Nanosleep.Sleep(_nanoseconds);

                    _parent.Signal(this);
                    _newWaitEvent.WaitOne();
                }

                _newWaitEvent.Dispose();
            }

            /// <summary>
            /// Assign a nanosleep for this thread to perform, then signal at the end.
            /// </summary>
            /// <param name="nanoseconds">Nanoseconds to sleep</param>
            /// <param name="signalId">Signal ID</param>
            /// <param name="timePoint">Target timepoint</param>
            public void SleepAndSignal(long nanoseconds, long signalId, long timePoint)
            {
                _signalId = signalId;
                _nanoseconds = nanoseconds;
                _timePoint = timePoint;
                _newWaitEvent.Set();
            }

            /// <summary>
            /// Resurrect an active nanosleep's signal if its target timepoint is a close enough match.
            /// </summary>
            /// <param name="signalId">New signal id to assign the nanosleep</param>
            /// <param name="timePoint">Target timepoint</param>
            /// <returns>True if resurrected, false otherwise</returns>
            public bool Resurrect(long signalId, long timePoint)
            {
                if (Math.Abs(timePoint - _timePoint) < _timePointEpsilon)
                {
                    _signalId = signalId;

                    return true;
                }

                return false;
            }

            /// <summary>
            /// Dispose the NanosleepThread, interrupting its worker loop.
            /// </summary>
            public void Dispose()
            {
                if (_running)
                {
                    _running = false;
                    _newWaitEvent.Set();
                }
            }
        }

        private readonly Lock _lock = new();
        private readonly List<NanosleepThread> _threads = new();
        private readonly List<NanosleepThread> _active = new();
        private readonly Stack<NanosleepThread> _free = new();
        private readonly AutoResetEvent _signalTarget;

        private long _signalId;

        /// <summary>
        /// Creates a new NanosleepPool with a target event to signal when a nanosleep completes.
        /// </summary>
        /// <param name="signalTarget">Event to signal when nanosleeps complete</param>
        public NanosleepPool(AutoResetEvent signalTarget)
        {
            _signalTarget = signalTarget;
        }

        /// <summary>
        /// Signal the target event (if the source sleep has not been superseded)
        /// and free the nanosleep thread.
        /// </summary>
        /// <param name="thread">Nanosleep thread that completed</param>
        private void Signal(NanosleepThread thread)
        {
            lock (_lock)
            {
                _active.Remove(thread);
                _free.Push(thread);

                if (thread.SignalId == _signalId)
                {
                    _signalTarget.Set();
                }
            }
        }

        /// <summary>
        /// Sleep for the given number of nanoseconds and signal the target event.
        /// This does not block the caller thread.
        /// </summary>
        /// <param name="nanoseconds">Nanoseconds to sleep</param>
        /// <param name="timePoint">Target timepoint</param>
        /// <returns>True if the signal will be set, false otherwise</returns>
        public bool SleepAndSignal(long nanoseconds, long timePoint)
        {
            lock (_lock)
            {
                _signalId++;

                // Check active sleeps, if any line up with the requested timepoint then resurrect that nanosleep.
                foreach (NanosleepThread existing in _active)
                {
                    if (existing.Resurrect(_signalId, timePoint))
                    {
                        return true;
                    }
                }

                if (!_free.TryPop(out NanosleepThread thread))
                {
                    if (_threads.Count >= MaxThreads)
                    {
                        return false;
                    }

                    thread = new NanosleepThread(this, _threads.Count);

                    _threads.Add(thread);
                }

                _active.Add(thread);

                thread.SleepAndSignal(nanoseconds, _signalId, timePoint);

                return true;
            }
        }

        /// <summary>
        /// Ignore the latest nanosleep.
        /// </summary>
        public void IgnoreSignal()
        {
            _signalId++;
        }

        /// <summary>
        /// Dispose the NanosleepPool, disposing all of its active threads.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            foreach (NanosleepThread thread in _threads)
            {
                thread.Dispose();
            }

            _threads.Clear();
        }
    }
}
