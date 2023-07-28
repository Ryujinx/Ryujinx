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
        private readonly Pipeline _pipeline;

        public MTLTexture MTLTexture;
        public TextureCreateInfo Info => Info;
        public int Width => Info.Width;
        public int Height => Info.Height;

        public Texture(MTLDevice device, Pipeline pipeline, TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            _info = info;

            var descriptor = new MTLTextureDescriptor();
            descriptor.PixelFormat = FormatTable.GetFormat(Info.Format);
            // descriptor.Usage =
            descriptor.Width = (ulong)Width;
            descriptor.Height = (ulong)Height;
            descriptor.Depth = (ulong)Info.Depth;
            descriptor.SampleCount = (ulong)Info.Samples;
            descriptor.MipmapLevelCount = (ulong)Info.Levels;
            descriptor.TextureType = Info.Target.Convert();
            descriptor.Swizzle = new MTLTextureSwizzleChannels
            {
                red = Info.SwizzleR.Convert(),
                green = Info.SwizzleG.Convert(),
                blue = Info.SwizzleB.Convert(),
                alpha = Info.SwizzleA.Convert()
            };

            MTLTexture = device.NewTexture(descriptor);
            _pipeline = pipeline;
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            if (destination is Texture destinationTexture)
            {
                _pipeline.BlitCommandEncoder.CopyFromTexture(
                    MTLTexture,
                    (ulong)firstLayer,
                    (ulong)firstLevel,
                    destinationTexture.MTLTexture,
                    (ulong)firstLayer,
                    (ulong)firstLevel,
                    MTLTexture.ArrayLength,
                    MTLTexture.MipmapLevelCount);
            }
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            if (destination is Texture destinationTexture)
            {
                _pipeline.BlitCommandEncoder.CopyFromTexture(
                    MTLTexture,
                    (ulong)srcLayer,
                    (ulong)srcLevel,
                    destinationTexture.MTLTexture,
                    (ulong)dstLayer,
                    (ulong)dstLevel,
                    MTLTexture.ArrayLength,
                    MTLTexture.MipmapLevelCount);
            }
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