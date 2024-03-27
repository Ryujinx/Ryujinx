using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Ryujinx.Memory
{
    /// <summary>
    /// A concrete implementation of <seealso cref="ReadOnlySequence{Byte}"/>,
    /// with methods to help build a full sequence.
    /// </summary>
    public sealed class BytesReadOnlySequenceSegment : ReadOnlySequenceSegment<byte>
    {
        public BytesReadOnlySequenceSegment(Memory<byte> memory) => Memory = memory;

        public BytesReadOnlySequenceSegment Append(Memory<byte> memory)
        {
            var nextSegment = new BytesReadOnlySequenceSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };

            Next = nextSegment;

            return nextSegment;
        }

        /// <summary>
        /// Attempts to determine if the current <seealso cref="Memory{Byte}"/> and <paramref name="other"/> are contiguous.
        /// Only works if both were created by a <seealso cref="NativeMemoryManager{Byte}"/>.
        /// </summary>
        /// <param name="other">The segment to check if continuous with the current one</param>
        /// <param name="contiguousStart">The starting address of the contiguous segment</param>
        /// <param name="contiguousSize">The size of the contiguous segment</param>
        /// <returns>True if the segments are contiguous, otherwise false</returns>
        public unsafe bool IsContiguousWith(Memory<byte> other, out nuint contiguousStart, out int contiguousSize)
        {
            if (MemoryMarshal.TryGetMemoryManager<byte, NativeMemoryManager<byte>>(Memory, out var thisMemoryManager) &&
                MemoryMarshal.TryGetMemoryManager<byte, NativeMemoryManager<byte>>(other, out var otherMemoryManager) &&
                thisMemoryManager.Pointer + thisMemoryManager.Length == otherMemoryManager.Pointer)
            {
                contiguousStart = (nuint)thisMemoryManager.Pointer;
                contiguousSize = thisMemoryManager.Length + otherMemoryManager.Length;
                return true;
            }
            else
            {
                contiguousStart = 0;
                contiguousSize = 0;
                return false;
            }
        }

        /// <summary>
        /// Replaces the current <seealso cref="Memory{Byte}"/> value with the one provided.
        /// </summary>
        /// <param name="memory">The new segment to hold in this <seealso cref="BytesReadOnlySequenceSegment"/></param>
        public void Replace(Memory<byte> memory)
            => Memory = memory;
    }
}
