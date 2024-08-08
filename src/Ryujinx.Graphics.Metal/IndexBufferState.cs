using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    readonly internal struct IndexBufferState
    {
        public static IndexBufferState Null => new(BufferHandle.Null, 0, 0);

        private readonly int _offset;
        private readonly int _size;
        private readonly IndexType _type;

        private readonly BufferHandle _handle;

        public IndexBufferState(BufferHandle handle, int offset, int size, IndexType type = IndexType.UInt)
        {
            _handle = handle;
            _offset = offset;
            _size = size;
            _type = type;
        }

        public (MTLBuffer, int, MTLIndexType) GetIndexBuffer(MetalRenderer renderer, CommandBufferScoped cbs)
        {
            Auto<DisposableBuffer> autoBuffer;
            int offset, size;
            MTLIndexType type;

            if (_type == IndexType.UByte)
            {
                // Index type is not supported. Convert to I16.
                autoBuffer = renderer.BufferManager.GetBufferI8ToI16(cbs, _handle, _offset, _size);

                type = MTLIndexType.UInt16;
                offset = 0;
                size = _size * 2;
            }
            else
            {
                autoBuffer = renderer.BufferManager.GetBuffer(_handle, false, out int bufferSize);

                if (_offset >= bufferSize)
                {
                    autoBuffer = null;
                }

                type = _type.Convert();
                offset = _offset;
                size = _size;
            }

            if (autoBuffer != null)
            {
                DisposableBuffer buffer = autoBuffer.Get(cbs, offset, size);

                return (buffer.Value, offset, type);
            }

            return (new MTLBuffer(IntPtr.Zero), 0, MTLIndexType.UInt16);
        }
    }
}
