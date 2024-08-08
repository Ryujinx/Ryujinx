using Ryujinx.Common;
using Ryujinx.Horizon.Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Horizon.Sdk.OsTypes.Impl
{
    class MultiWaitImpl
    {
        private const int WaitTimedOut = -1;
        private const int WaitCancelled = -2;
        private const int WaitInvalid = -3;

        private readonly List<MultiWaitHolderBase> _multiWaits;

        private readonly Lock _lock = new();

        private int _waitingThreadHandle;

        private MultiWaitHolderBase _signaledHolder;

        public long CurrentTime { get; private set; }

        public IEnumerable<MultiWaitHolderBase> MultiWaits => _multiWaits;

        public MultiWaitImpl()
        {
            _multiWaits = new List<MultiWaitHolderBase>();
        }

        public void LinkMultiWaitHolder(MultiWaitHolderBase multiWaitHolder)
        {
            _multiWaits.Add(multiWaitHolder);
        }

        public void UnlinkMultiWaitHolder(MultiWaitHolderBase multiWaitHolder)
        {
            _multiWaits.Remove(multiWaitHolder);
        }

        public void MoveAllFrom(MultiWaitImpl other)
        {
            foreach (MultiWaitHolderBase multiWait in other._multiWaits)
            {
                multiWait.SetMultiWait(this);
            }

            _multiWaits.AddRange(other._multiWaits);

            other._multiWaits.Clear();
        }

        public MultiWaitHolderBase WaitAnyImpl(bool infinite, long timeout)
        {
            _signaledHolder = null;
            _waitingThreadHandle = Os.GetCurrentThreadHandle();

            MultiWaitHolderBase result = LinkHoldersToObjectList();

            lock (_lock)
            {
                if (_signaledHolder != null)
                {
                    result = _signaledHolder;
                }
            }

            result ??= WaitAnyHandleImpl(infinite, timeout);

            UnlinkHoldersFromObjectsList();
            _waitingThreadHandle = 0;

            return result;
        }

        private MultiWaitHolderBase WaitAnyHandleImpl(bool infinite, long timeout)
        {
            Span<int> objectHandles = new int[64];

            Span<MultiWaitHolderBase> objects = new MultiWaitHolderBase[64];

            int count = FillObjectsArray(objectHandles, objects);

            long endTime = infinite ? long.MaxValue : PerformanceCounter.ElapsedMilliseconds * 1000000;

            while (true)
            {
                CurrentTime = PerformanceCounter.ElapsedMilliseconds * 1000000;

                MultiWaitHolderBase minTimeoutObject = RecalcMultiWaitTimeout(endTime, out long minTimeout);

                int index;

                if (count == 0 && minTimeout == 0)
                {
                    index = WaitTimedOut;
                }
                else
                {
                    index = WaitSynchronization(objectHandles[..count], minTimeout);

                    DebugUtil.Assert(index != WaitInvalid);
                }

                switch (index)
                {
                    case WaitTimedOut:
                        if (minTimeoutObject != null)
                        {
                            CurrentTime = PerformanceCounter.ElapsedMilliseconds * 1000000;

                            if (minTimeoutObject.Signaled == TriBool.True)
                            {
                                lock (_lock)
                                {
                                    _signaledHolder = minTimeoutObject;

                                    return _signaledHolder;
                                }
                            }
                        }
                        else
                        {
                            return null;
                        }
                        break;
                    case WaitCancelled:
                        lock (_lock)
                        {
                            if (_signaledHolder != null)
                            {
                                return _signaledHolder;
                            }
                        }
                        break;
                    default:
                        lock (_lock)
                        {
                            _signaledHolder = objects[index];

                            return _signaledHolder;
                        }
                }
            }
        }

        private int FillObjectsArray(Span<int> handles, Span<MultiWaitHolderBase> objects)
        {
            int count = 0;

            foreach (MultiWaitHolderBase holder in _multiWaits)
            {
                int handle = holder.Handle;

                if (handle != 0)
                {
                    handles[count] = handle;
                    objects[count] = holder;

                    count++;
                }
            }

            return count;
        }

        private MultiWaitHolderBase RecalcMultiWaitTimeout(long endTime, out long minTimeout)
        {
            MultiWaitHolderBase minTimeHolder = null;

            long minTime = endTime;

            foreach (MultiWaitHolderBase holder in _multiWaits)
            {
                long currentTime = holder.GetAbsoluteTimeToWakeup();

                if ((ulong)currentTime < (ulong)minTime)
                {
                    minTimeHolder = holder;

                    minTime = currentTime;
                }
            }

            minTimeout = (ulong)minTime < (ulong)CurrentTime ? 0 : minTime - CurrentTime;

            return minTimeHolder;
        }

        private static int WaitSynchronization(ReadOnlySpan<int> handles, long timeout)
        {
            Result result = HorizonStatic.Syscall.WaitSynchronization(out int index, handles, timeout);

            if (result == KernelResult.TimedOut)
            {
                return WaitTimedOut;
            }
            else if (result == KernelResult.Cancelled)
            {
                return WaitCancelled;
            }

            result.AbortOnFailure();

            return index;
        }

        public void NotifyAndWakeUpThread(MultiWaitHolderBase holder)
        {
            lock (_lock)
            {
                if (_signaledHolder == null)
                {
                    _signaledHolder = holder;
                    HorizonStatic.Syscall.CancelSynchronization(_waitingThreadHandle).AbortOnFailure();
                }
            }
        }

        private MultiWaitHolderBase LinkHoldersToObjectList()
        {
            MultiWaitHolderBase signaledHolder = null;

            foreach (MultiWaitHolderBase holder in _multiWaits)
            {
                TriBool isSignaled = holder.LinkToObjectList();

                if (signaledHolder == null && isSignaled == TriBool.True)
                {
                    signaledHolder = holder;
                }
            }

            return signaledHolder;
        }

        private void UnlinkHoldersFromObjectsList()
        {
            foreach (MultiWaitHolderBase holder in _multiWaits)
            {
                holder.UnlinkFromObjectList();
            }
        }
    }
}
