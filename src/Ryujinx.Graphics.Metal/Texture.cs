using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Texture : ITexture, IDisposable
    {
        private readonly TextureCreateInfo _info;

        public MTLTexture MTLTexture;
        public TextureCreateInfo Info => Info;
        public int Width => Info.Width;
        public int Height => Info.Height;

        public Texture(MTLDevice device, TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            _info = info;

            var descriptor = new MTLTextureDescriptor();
            descriptor.PixelFormat = FormatTable.GetFormat(Info.Format);
            // descriptor.Usage =
            descriptor.Width = (ulong)Width;
            descriptor.Height = (ulong)Height;
            descriptor.Depth = (ulong)Info.Depth;
            descriptor.SampleCount = (ulong)Info.Samples;
            descriptor.TextureType = Info.Target.Convert();
            descriptor.Swizzle = new MTLTextureSwizzleChannels
            {
                red = Info.SwizzleR.Convert(),
                green = Info.SwizzleG.Convert(),
                blue = Info.SwizzleB.Convert(),
                alpha = Info.SwizzleA.Convert()
            };

            MTLTexture = device.NewTexture(descriptor);
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            throw new NotImplementedException();
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData()
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            throw new NotImplementedException();
        }

        public void SetData(SpanOrArray<byte> data)
        {
            throw new NotImplementedException();
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level)
        {
            throw new NotImplementedException();
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level, Rectangle<int> region)
        {
            throw new NotImplementedException();
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotImplementedException();
        }

        public void Release()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}