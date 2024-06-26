using System;
using System.Runtime.InteropServices;

namespace ARMeilleure.Common
{
    public unsafe sealed class NativeAllocator : Allocator
    {
        public static NativeAllocator Instance { get; } = new();

        public override void* Allocate(ulong size)
        {
            void* result = (void*)Marshal.AllocHGlobal((IntPtr)size);

            if (result == null)
            {
                throw new OutOfMemoryException();
            }

            return result;
        }

        public override void Free(void* block)
        {
            Marshal.FreeHGlobal((IntPtr)block);
        }
    }
}
