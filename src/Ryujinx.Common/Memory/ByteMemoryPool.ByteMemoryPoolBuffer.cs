using System;
using System.Buffers;
using System.Threading;

namespace Ryujinx.Common.Memory
{
    public partial class ByteMemoryPool
    {
        /// <summary>
        /// Represents a <see cref="IMemoryOwner{Byte}"/> that wraps an array rented from
        /// <see cref="ArrayPool{Byte}.Shared"/> and exposes it as <see cref="Memory{Byte}"/>
        /// with a length of the requested size.
        /// </summary>
        private sealed class ByteMemoryPoolBuffer : IMemoryOwner<byte>
        {
            private byte[] _array;
            private readonly int _length;

            public ByteMemoryPoolBuffer(int length)
            {
                _array = ArrayPool<byte>.Shared.Rent(length);
                _length = length;
            }

            /// <summary>
            /// Returns a <see cref="Memory{Byte}"/> belonging to this owner.
            /// </summary>
            public Memory<byte> Memory
            {
                get
                {
                    byte[] array = _array;

                    ObjectDisposedException.ThrowIf(array is null, this);

                    return new Memory<byte>(array, 0, _length);
                }
            }

            public void Dispose()
            {
                var array = Interlocked.Exchange(ref _array, null);

                if (array != null)
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }
    }
}
