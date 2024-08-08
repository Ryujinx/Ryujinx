using Ryujinx.Common;
using Ryujinx.Common.Memory;
using System;
using System.Buffers;
using System.Threading;

namespace Ryujinx.Audio.Backends.Common
{
    /// <summary>
    /// A ring buffer that grow if data written to it is too big to fit.
    /// </summary>
    public class DynamicRingBuffer
    {
        private const int RingBufferAlignment = 2048;

        private readonly Lock _lock = new();

        private MemoryOwner<byte> _bufferOwner;
        private Memory<byte> _buffer;
        private int _size;
        private int _headOffset;
        private int _tailOffset;

        public int Length => _size;

        public DynamicRingBuffer(int initialCapacity = RingBufferAlignment)
        {
            _bufferOwner = MemoryOwner<byte>.RentCleared(initialCapacity);
            _buffer = _bufferOwner.Memory;
        }

        public void Clear()
        {
            _size = 0;
            _headOffset = 0;
            _tailOffset = 0;
        }

        public void Clear(int size)
        {
            if (size == 0)
            {
                return;
            }

            lock (_lock)
            {
                if (size > _size)
                {
                    size = _size;
                }

                _headOffset = (_headOffset + size) % _buffer.Length;
                _size -= size;

                if (_size == 0)
                {
                    _headOffset = 0;
                    _tailOffset = 0;
                }
            }
        }

        private void SetCapacityLocked(int capacity)
        {
            MemoryOwner<byte> newBufferOwner = MemoryOwner<byte>.RentCleared(capacity);
            Memory<byte> newBuffer = newBufferOwner.Memory;

            if (_size > 0)
            {
                if (_headOffset < _tailOffset)
                {
                    _buffer.Slice(_headOffset, _size).CopyTo(newBuffer);
                }
                else
                {
                    _buffer[_headOffset..].CopyTo(newBuffer);
                    _buffer[.._tailOffset].CopyTo(newBuffer[(_buffer.Length - _headOffset)..]);
                }
            }

            _bufferOwner.Dispose();

            _bufferOwner = newBufferOwner;
            _buffer = newBuffer;
            _headOffset = 0;
            _tailOffset = _size;
        }

        public void Write(ReadOnlySpan<byte> buffer, int index, int count)
        {
            if (count == 0)
            {
                return;
            }

            lock (_lock)
            {
                if ((_size + count) > _buffer.Length)
                {
                    SetCapacityLocked(BitUtils.AlignUp(_size + count, RingBufferAlignment));
                }

                if (_headOffset < _tailOffset)
                {
                    int tailLength = _buffer.Length - _tailOffset;

                    if (tailLength >= count)
                    {
                        buffer.Slice(index, count).CopyTo(_buffer.Span[_tailOffset..]);
                    }
                    else
                    {
                        buffer.Slice(index, tailLength).CopyTo(_buffer.Span[_tailOffset..]);
                        buffer.Slice(index + tailLength, count - tailLength).CopyTo(_buffer.Span);
                    }
                }
                else
                {
                    buffer.Slice(index, count).CopyTo(_buffer.Span[_tailOffset..]);
                }

                _size += count;
                _tailOffset = (_tailOffset + count) % _buffer.Length;
            }
        }

        public int Read(Span<byte> buffer, int index, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            lock (_lock)
            {
                if (count > _size)
                {
                    count = _size;
                }

                if (_headOffset < _tailOffset)
                {
                    _buffer.Span.Slice(_headOffset, count).CopyTo(buffer[index..]);
                }
                else
                {
                    int tailLength = _buffer.Length - _headOffset;

                    if (tailLength >= count)
                    {
                        _buffer.Span.Slice(_headOffset, count).CopyTo(buffer[index..]);
                    }
                    else
                    {
                        _buffer.Span.Slice(_headOffset, tailLength).CopyTo(buffer[index..]);
                        _buffer.Span[..(count - tailLength)].CopyTo(buffer[(index + tailLength)..]);
                    }
                }

                _size -= count;
                _headOffset = (_headOffset + count) % _buffer.Length;

                if (_size == 0)
                {
                    _headOffset = 0;
                    _tailOffset = 0;
                }

                return count;
            }
        }
    }
}
