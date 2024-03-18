using Ryujinx.Common.Logging;
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
        public int Depth => Info.Depth;

        public Texture(MTLDevice device, Pipeline pipeline, TextureCreateInfo info)
        {
            _device = device;
            _pipeline = pipeline;
            _info = info;

            var descriptor = new MTLTextureDescriptor
            {
                PixelFormat = FormatTable.GetFormat(Info.Format),
                Usage = MTLTextureUsage.ShaderRead,
                Width = (ulong)Width,
                Height = (ulong)Height,
                Depth = (ulong)Depth
            };
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
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

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
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
            return this;
        }

        public PinnedSpan<byte> GetData()
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            throw new NotImplementedException();
        }

        // TODO: Handle array formats
        public unsafe void SetData(SpanOrArray<byte> data)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            var dataSpan = data.Span;
            var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
            var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
            dataSpan.CopyTo(bufferSpan);

            int width = Info.Width;
            int height = Info.Height;
            int depth = Info.Depth;
            int levels = Info.GetLevelsClamped();

            int offset = 0;

            for (int level = 0; level < levels; level++)
            {
                int mipSize = Info.GetMipSize(level);

                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)dataSpan.Length)
                {
                    return;
                }

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    (ulong)offset,
                    (ulong)Info.GetMipStride(level),
                    (ulong)mipSize,
                    new MTLSize { width = (ulong)width, height = (ulong)height, depth = (ulong)depth },
                    MTLTexture,
                    0,
                    (ulong)level,
                    new MTLOrigin()
                );

                offset += mipSize;

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
                depth = Math.Max(1, depth >> 1);
            }
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

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
                    new MTLSize { width = MTLTexture.Width, height = MTLTexture.Height, depth = MTLTexture.Depth },
                    MTLTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin()
                );
            }
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level, Rectangle<int> region)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void Release()
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void Dispose()
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }
    }
}
