using Ryujinx.Graphics.GAL;
using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Metal
{
    static class Handle
    {
        public static IntPtr ToIntPtr(this BufferHandle handle)
        {
            return Unsafe.As<BufferHandle, IntPtr>(ref handle);
        }
    }
}
