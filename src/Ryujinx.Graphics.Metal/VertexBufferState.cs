using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    readonly internal struct VertexBufferState
    {
        public static VertexBufferState Null => new(BufferHandle.Null, 0, 0, 0);

        private readonly BufferHandle _handle;
        private readonly int _offset;
        private readonly int _size;

        public readonly int Stride;
        public readonly int Divisor;

        public VertexBufferState(BufferHandle handle, int offset, int size, int divisor, int stride = 0)
        {
            _handle = handle;
            _offset = offset;
            _size = size;

            Stride = stride;
            Divisor = divisor;
        }

        public (MTLBuffer, int) GetVertexBuffer(BufferManager bufferManager, CommandBufferScoped cbs)
        {
            Auto<DisposableBuffer> autoBuffer = null;

            if (_handle != BufferHandle.Null)
            {
                // TODO: Handle restride if necessary

                autoBuffer = bufferManager.GetBuffer(_handle, false, out int size);

                // The original stride must be reapplied in case it was rewritten.
                // TODO: Handle restride if necessary

                if (_offset >= size)
                {
                    autoBuffer = null;
                }
            }

            if (autoBuffer != null)
            {
                int offset = _offset;
                var buffer = autoBuffer.Get(cbs, offset, _size).Value;

                return (buffer, offset);
            }

            return (new MTLBuffer(IntPtr.Zero), 0);
        }
    }
}
