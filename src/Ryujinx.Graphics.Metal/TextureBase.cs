using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    abstract class TextureBase : IDisposable
    {
        private bool _disposed;

        protected readonly TextureCreateInfo _info;
        protected readonly Pipeline _pipeline;
        protected readonly MTLDevice _device;
        protected readonly MetalRenderer _renderer;

        protected MTLTexture _mtlTexture;

        public TextureCreateInfo Info => _info;
        public int Width => Info.Width;
        public int Height => Info.Height;
        public int Depth => Info.Depth;
        public MTLPixelFormat MtlFormat { get; protected set; }

        public TextureBase(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info)
        {
            _device = device;
            _renderer = renderer;
            _pipeline = pipeline;
            _info = info;
        }

        public MTLTexture GetHandle()
        {
            if (_disposed)
            {
                return new MTLTexture(IntPtr.Zero);
            }

            return _mtlTexture;
        }

        public virtual void Release()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_mtlTexture != IntPtr.Zero)
            {
                _mtlTexture.Dispose();
            }
            _disposed = true;
        }
    }
}
