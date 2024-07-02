using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    readonly struct DisposableBuffer : IDisposable
    {
        public MTLBuffer Value { get; }

        public DisposableBuffer(MTLBuffer buffer)
        {
            Value = buffer;
        }

        public void Dispose()
        {
            if (Value != IntPtr.Zero)
            {
                Value.SetPurgeableState(MTLPurgeableState.Empty);
                Value.Dispose();
            }
        }
    }
}
