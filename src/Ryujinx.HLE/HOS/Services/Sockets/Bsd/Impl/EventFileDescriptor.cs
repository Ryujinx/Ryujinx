using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl
{
    class EventFileDescriptor : IFileDescriptor
    {
        private ulong _value;
        private readonly EventFdFlags _flags;

        private readonly object _lock = new();

        public bool Blocking { get => !_flags.HasFlag(EventFdFlags.NonBlocking); set => throw new NotSupportedException(); }

        public ManualResetEvent WriteEvent { get; }
        public ManualResetEvent ReadEvent { get; }

        public EventFileDescriptor(ulong value, EventFdFlags flags)
        {
            // FIXME: We should support blocking operations.
            // Right now they can't be supported because it would cause the
            // service to lock up as we only have one thread processing requests.
            flags |= EventFdFlags.NonBlocking;

            _value = value;
            _flags = flags;

            WriteEvent = new ManualResetEvent(false);
            ReadEvent = new ManualResetEvent(false);
            UpdateEventStates();
        }

        public int RefCount { get; set; }

        public void Dispose()
        {
            WriteEvent.Dispose();
            ReadEvent.Dispose();
        }

        private void ResetEventStates()
        {
            WriteEvent.Reset();
            ReadEvent.Reset();
        }

        private void UpdateEventStates()
        {
            if (_value > 0)
            {
                ReadEvent.Set();
            }

            if (_value != uint.MaxValue - 1)
            {
                WriteEvent.Set();
            }
        }

        public LinuxError Read(out int readSize, Span<byte> buffer)
        {
            if (buffer.Length < sizeof(ulong))
            {
                readSize = 0;

                return LinuxError.EINVAL;
            }

            lock (_lock)
            {
                ResetEventStates();

                ref ulong count = ref MemoryMarshal.Cast<byte, ulong>(buffer)[0];

                if (_value == 0)
                {
                    if (Blocking)
                    {
                        while (_value == 0)
                        {
                            Monitor.Wait(_lock);
                        }
                    }
                    else
                    {
                        readSize = 0;

                        UpdateEventStates();
                        return LinuxError.EAGAIN;
                    }
                }

                readSize = sizeof(ulong);

                if (_flags.HasFlag(EventFdFlags.Semaphore))
                {
                    --_value;

                    count = 1;
                }
                else
                {
                    count = _value;

                    _value = 0;
                }

                UpdateEventStates();
                return LinuxError.SUCCESS;
            }
        }

        public LinuxError Write(out int writeSize, ReadOnlySpan<byte> buffer)
        {
            if (!MemoryMarshal.TryRead(buffer, out ulong count) || count == ulong.MaxValue)
            {
                writeSize = 0;

                return LinuxError.EINVAL;
            }

            lock (_lock)
            {
                ResetEventStates();

                if (_value > _value + count)
                {
                    if (Blocking)
                    {
                        Monitor.Wait(_lock);
                    }
                    else
                    {
                        writeSize = 0;

                        UpdateEventStates();
                        return LinuxError.EAGAIN;
                    }
                }

                writeSize = sizeof(ulong);

                _value += count;
                Monitor.Pulse(_lock);

                UpdateEventStates();
                return LinuxError.SUCCESS;
            }
        }
    }
}
