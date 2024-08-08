using System;
using System.Threading;

namespace Ryujinx.HLE.HOS.Applets.SoftwareKeyboard
{
    /// <summary>
    /// A threaded executor of periodic actions that can be cancelled. The total execution time is optional
    /// and, in this case, a progress is reported back to the action.
    /// </summary>
    class TimedAction
    {
        public const int MaxThreadSleep = 100;

        private class SleepSubstepData
        {
            public readonly int SleepMilliseconds;
            public readonly int SleepCount;
            public readonly int SleepRemainderMilliseconds;

            public SleepSubstepData(int sleepMilliseconds)
            {
                SleepMilliseconds = Math.Min(sleepMilliseconds, MaxThreadSleep);
                SleepCount = sleepMilliseconds / SleepMilliseconds;
                SleepRemainderMilliseconds = sleepMilliseconds - SleepCount * SleepMilliseconds;
            }
        }

        private TRef<bool> _cancelled = null;
        private Thread _thread = null;
        private readonly Lock _lock = new();

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    if (_thread == null)
                    {
                        return false;
                    }

                    return _thread.IsAlive;
                }
            }
        }

        public void RequestCancel()
        {
            lock (_lock)
            {
                if (_cancelled != null)
                {
                    Volatile.Write(ref _cancelled.Value, true);
                }
            }
        }

        public TimedAction() { }

        private void Reset(Thread thread, TRef<bool> cancelled)
        {
            lock (_lock)
            {
                // Cancel the current task.
                if (_cancelled != null)
                {
                    Volatile.Write(ref _cancelled.Value, true);
                }

                _cancelled = cancelled;

                _thread = thread;
                _thread.IsBackground = true;
                _thread.Start();
            }
        }

        public void Reset(Action<float> action, int totalMilliseconds, int sleepMilliseconds)
        {
            // Create a dedicated cancel token for each task.
            var cancelled = new TRef<bool>(false);

            Reset(new Thread(() =>
            {
                var substepData = new SleepSubstepData(sleepMilliseconds);

                int totalCount = totalMilliseconds / sleepMilliseconds;
                int totalRemainder = totalMilliseconds - totalCount * sleepMilliseconds;

                if (Volatile.Read(ref cancelled.Value))
                {
                    action(-1);

                    return;
                }

                action(0);

                for (int i = 1; i <= totalCount; i++)
                {
                    if (SleepWithSubstep(substepData, cancelled))
                    {
                        action(-1);

                        return;
                    }

                    action((float)(i * sleepMilliseconds) / totalMilliseconds);
                }

                if (totalRemainder > 0)
                {
                    if (SleepWithSubstep(substepData, cancelled))
                    {
                        action(-1);

                        return;
                    }

                    action(1);
                }
            }), cancelled);
        }

        public void Reset(Action action, int sleepMilliseconds)
        {
            // Create a dedicated cancel token for each task.
            var cancelled = new TRef<bool>(false);

            Reset(new Thread(() =>
            {
                var substepData = new SleepSubstepData(sleepMilliseconds);

                while (!Volatile.Read(ref cancelled.Value))
                {
                    action();

                    if (SleepWithSubstep(substepData, cancelled))
                    {
                        return;
                    }
                }
            }), cancelled);
        }

        public void Reset(Action action)
        {
            // Create a dedicated cancel token for each task.
            var cancelled = new TRef<bool>(false);

            Reset(new Thread(() =>
            {
                while (!Volatile.Read(ref cancelled.Value))
                {
                    action();
                }
            }), cancelled);
        }

        private static bool SleepWithSubstep(SleepSubstepData substepData, TRef<bool> cancelled)
        {
            for (int i = 0; i < substepData.SleepCount; i++)
            {
                if (Volatile.Read(ref cancelled.Value))
                {
                    return true;
                }

                Thread.Sleep(substepData.SleepMilliseconds);
            }

            if (substepData.SleepRemainderMilliseconds > 0)
            {
                if (Volatile.Read(ref cancelled.Value))
                {
                    return true;
                }

                Thread.Sleep(substepData.SleepRemainderMilliseconds);
            }

            return Volatile.Read(ref cancelled.Value);
        }
    }
}
