using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.OpenGL.Image
{
    class TextureBuffer : TextureBase, ITexture
    {
        private readonly OpenGLRenderer _renderer;
        private int _bufferOffset;
        private int _bufferSize;
        private int _bufferCount;

        private BufferHandle _buffer;

        public TextureBuffer(OpenGLRenderer renderer, TextureCreateInfo info) : base(info)
        {
            _renderer = renderer;
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            throw new NotSupportedException();
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            throw new NotSupportedException();
        }

        public PinnedSpan<byte> GetData()
        {
            return Buffer.GetData(_renderer, _buffer, _bufferOffset, _bufferSize);
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            return GetData();
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void SetData(MemoryOwner<byte> data)
        {
            var dataSpan = data.Span;

            Buffer.SetData(_buffer, _bufferOffset, dataSpan[..Math.Min(dataSpan.Length, _bufferSize)]);

            data.Dispose();
        }

        /// <inheritdoc/>
        public void SetData(MemoryOwner<byte> data, int layer, int level)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public void SetData(MemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            throw new NotSupportedException();
        }

        public void SetStorage(BufferRange buffer)
        {
            if (_buffer != BufferHandle.Null &&
                _buffer == buffer.Handle &&
                buffer.Offset == _bufferOffset &&
                buffer.Size == _bufferSize &&
                _renderer.BufferCount == _bufferCount)
            {
                // Only rebind the buffer when more have been created.
                return;
            }

            _buffer = buffer.Handle;
            _bufferOffset = buffer.Offset;
            _bufferSize = buffer.Size;
            _bufferCount = _renderer.BufferCount;

            Bind(0);

            SizedInternalFormat format = (SizedInternalFormat)FormatTable.GetFormatInfo(Info.Format).PixelInternalFormat;

            GL.TexBufferRange(TextureBufferTarget.TextureBuffer, format, _buffer.ToInt32(), (IntPtr)buffer.Offset, buffer.Size);
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteTexture(Handle);

                Handle = 0;
            }
        }

        public void Release()
        {
            Dispose();
        }
    }
}
