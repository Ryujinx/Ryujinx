using System;
using System.Buffers;

namespace Ryujinx.Memory
{
    public unsafe class NativeMemoryManager<T> : MemoryManager<T> where T : unmanaged
    {
        private readonly T* _pointer;
        private readonly int _length;

        public NativeMemoryManager(T* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        public unsafe T* Pointer => _pointer;

        public int Length => _length;

        public override Span<T> GetSpan()
        {
            return new Span<T>((void*)_pointer, _length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if ((uint)elementIndex >= _length)
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            }

            return new MemoryHandle((void*)(_pointer + elementIndex));
        }

        public override void Unpin()
        {
            // No need to do anything as pointer already points no native memory, not GC tracked.
        }

        protected override void Dispose(bool disposing)
        {
            // Nothing to dispose, MemoryBlock still owns the memory.
        }
    }
}
