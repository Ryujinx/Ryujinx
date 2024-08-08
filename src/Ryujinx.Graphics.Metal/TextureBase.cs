using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    abstract class TextureBase : IDisposable
    {
        private int _isValid = 1;

        public bool Valid => Volatile.Read(ref _isValid) != 0;

        protected readonly Pipeline Pipeline;
        protected readonly MTLDevice Device;
        protected readonly MetalRenderer Renderer;

        protected MTLTexture MtlTexture;

        public readonly TextureCreateInfo Info;
        public int Width => Info.Width;
        public int Height => Info.Height;
        public int Depth => Info.Depth;

        public MTLPixelFormat MtlFormat { get; protected set; }
        public int FirstLayer { get; protected set; }
        public int FirstLevel { get; protected set; }

        public TextureBase(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info)
        {
            Device = device;
            Renderer = renderer;
            Pipeline = pipeline;
            Info = info;
        }

        public MTLTexture GetHandle()
        {
            if (_isValid == 0)
            {
                return new MTLTexture(IntPtr.Zero);
            }

            return MtlTexture;
        }

        public virtual void Release()
        {
            Dispose();
        }

        public void Dispose()
        {
            bool wasValid = Interlocked.Exchange(ref _isValid, 0) != 0;

            if (wasValid)
            {
                if (MtlTexture != IntPtr.Zero)
                {
                    MtlTexture.Dispose();
                }
            }
        }
    }
}
