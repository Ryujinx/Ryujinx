using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Texture : ITexture, IDisposable
    {
        private readonly TextureCreateInfo _info;
        private readonly Pipeline _pipeline;
        private readonly MTLDevice _device;

        public MTLTexture MTLTexture;
        public TextureCreateInfo Info => _info;
        public int Width => Info.Width;
        public int Height => Info.Height;

        public Texture(MTLDevice device, Pipeline pipeline, TextureCreateInfo info)
        {
            _device = device;
            _pipeline = pipeline;
            _info = info;

            var descriptor = new MTLTextureDescriptor();
            descriptor.PixelFormat = FormatTable.GetFormat(Info.Format);
            descriptor.Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.ShaderWrite | MTLTextureUsage.RenderTarget;
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

            MTLTexture = _device.NewTexture(descriptor);
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            MTLBlitCommandEncoder blitCommandEncoder;

            if (_pipeline.CurrentEncoder is MTLBlitCommandEncoder encoder)
            {
                blitCommandEncoder = encoder;
            }
            else
            {
                blitCommandEncoder = _pipeline.BeginBlitPass();
            }

            if (destination is Texture destinationTexture)
            {
                blitCommandEncoder.CopyFromTexture(
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
            MTLBlitCommandEncoder blitCommandEncoder;

            if (_pipeline.CurrentEncoder is MTLBlitCommandEncoder encoder)
            {
                blitCommandEncoder = encoder;
            }
            else
            {
                blitCommandEncoder = _pipeline.BeginBlitPass();
            }

            if (destination is Texture destinationTexture)
            {
                blitCommandEncoder.CopyFromTexture(
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
            MTLBlitCommandEncoder blitCommandEncoder;

            if (_pipeline.CurrentEncoder is MTLBlitCommandEncoder encoder)
            {
                blitCommandEncoder = encoder;
            }
            else
            {
                blitCommandEncoder = _pipeline.BeginBlitPass();
            }

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MTLTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            var handle = range.Handle;
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref handle));

            blitCommandEncoder.CopyFromTexture(
                MTLTexture,
                (ulong)layer,
                (ulong)level,
                new MTLOrigin(),
                new MTLSize { width = MTLTexture.Width, height = MTLTexture.Height, depth = MTLTexture.Depth },
                mtlBuffer,
                (ulong)range.Offset,
                bytesPerRow,
                bytesPerImage);
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
            MTLBlitCommandEncoder blitCommandEncoder;

            if (_pipeline.CurrentEncoder is MTLBlitCommandEncoder encoder)
            {
                blitCommandEncoder = encoder;
            }
            else
            {
                blitCommandEncoder = _pipeline.BeginBlitPass();
            }

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MTLTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            unsafe
            {
                var dataSpan = data.Span;
                var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
                var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
                dataSpan.CopyTo(bufferSpan);

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage,
                    new MTLSize { width = MTLTexture.Width, height = MTLTexture.Height, depth = MTLTexture.Depth},
                    MTLTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin()
                );
            }
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level, Rectangle<int> region)
        {
            MTLBlitCommandEncoder blitCommandEncoder;

            if (_pipeline.CurrentEncoder is MTLBlitCommandEncoder encoder)
            {
                blitCommandEncoder = encoder;
            }
            else
            {
                blitCommandEncoder = _pipeline.BeginBlitPass();
            }

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MTLTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            unsafe
            {
                var dataSpan = data.Span;
                var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
                var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
                dataSpan.CopyTo(bufferSpan);

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage,
                    new MTLSize { width = (ulong)region.Width, height = (ulong)region.Height, depth = 1 },
                    MTLTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin { x = (ulong)region.X, y = (ulong)region.Y }
                );
            }
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