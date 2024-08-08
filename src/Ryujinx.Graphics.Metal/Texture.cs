using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Buffers;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Texture : TextureBase, ITexture
    {
        private MTLTexture _identitySwizzleHandle;
        private readonly bool _identityIsDifferent;

        public Texture(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info) : base(device, renderer, pipeline, info)
        {
            MTLPixelFormat pixelFormat = FormatTable.GetFormat(Info.Format);

            var descriptor = new MTLTextureDescriptor
            {
                PixelFormat = pixelFormat,
                Usage = MTLTextureUsage.Unknown,
                SampleCount = (ulong)Info.Samples,
                TextureType = Info.Target.Convert(),
                Width = (ulong)Info.Width,
                Height = (ulong)Info.Height,
                MipmapLevelCount = (ulong)Info.GetLevelsClamped()
            };

            if (info.Target == Target.Texture3D)
            {
                descriptor.Depth = (ulong)Info.Depth;
            }
            else if (info.Target != Target.Cubemap)
            {
                descriptor.ArrayLength = (ulong)Info.Depth;
            }

            MTLTextureSwizzleChannels swizzle = GetSwizzle(info, descriptor.PixelFormat);

            _identitySwizzleHandle = Device.NewTexture(descriptor);

            if (SwizzleIsIdentity(swizzle))
            {
                MtlTexture = _identitySwizzleHandle;
            }
            else
            {
                MtlTexture = CreateDefaultView(_identitySwizzleHandle, swizzle, descriptor);
                _identityIsDifferent = true;
            }

            MtlFormat = pixelFormat;
            descriptor.Dispose();
        }

        public Texture(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info, MTLTexture sourceTexture, int firstLayer, int firstLevel) : base(device, renderer, pipeline, info)
        {
            var pixelFormat = FormatTable.GetFormat(Info.Format);
            var textureType = Info.Target.Convert();
            NSRange levels;
            levels.location = (ulong)firstLevel;
            levels.length = (ulong)Info.GetLevelsClamped();
            NSRange slices;
            slices.location = (ulong)firstLayer;
            slices.length = textureType == MTLTextureType.Type3D ? 1 : (ulong)info.GetDepthOrLayers();

            var swizzle = GetSwizzle(info, pixelFormat);

            _identitySwizzleHandle = sourceTexture.NewTextureView(pixelFormat, textureType, levels, slices);

            if (SwizzleIsIdentity(swizzle))
            {
                MtlTexture = _identitySwizzleHandle;
            }
            else
            {
                MtlTexture = sourceTexture.NewTextureView(pixelFormat, textureType, levels, slices, swizzle);
                _identityIsDifferent = true;
            }

            MtlFormat = pixelFormat;
            FirstLayer = firstLayer;
            FirstLevel = firstLevel;
        }

        public void PopulateRenderPassAttachment(MTLRenderPassColorAttachmentDescriptor descriptor)
        {
            descriptor.Texture = _identitySwizzleHandle;
        }

        private MTLTexture CreateDefaultView(MTLTexture texture, MTLTextureSwizzleChannels swizzle, MTLTextureDescriptor descriptor)
        {
            NSRange levels;
            levels.location = 0;
            levels.length = (ulong)Info.GetLevelsClamped();
            NSRange slices;
            slices.location = 0;
            slices.length = Info.Target == Target.Texture3D ? 1 : (ulong)Info.GetDepthOrLayers();

            return texture.NewTextureView(descriptor.PixelFormat, descriptor.TextureType, levels, slices, swizzle);
        }

        private bool SwizzleIsIdentity(MTLTextureSwizzleChannels swizzle)
        {
            return swizzle.red == MTLTextureSwizzle.Red &&
                   swizzle.green == MTLTextureSwizzle.Green &&
                   swizzle.blue == MTLTextureSwizzle.Blue &&
                   swizzle.alpha == MTLTextureSwizzle.Alpha;
        }

        private MTLTextureSwizzleChannels GetSwizzle(TextureCreateInfo info, MTLPixelFormat pixelFormat)
        {
            var swizzleR = Info.SwizzleR.Convert();
            var swizzleG = Info.SwizzleG.Convert();
            var swizzleB = Info.SwizzleB.Convert();
            var swizzleA = Info.SwizzleA.Convert();

            if (info.Format == Format.R5G5B5A1Unorm ||
                info.Format == Format.R5G5B5X1Unorm ||
                info.Format == Format.R5G6B5Unorm)
            {
                (swizzleB, swizzleR) = (swizzleR, swizzleB);
            }
            else if (pixelFormat == MTLPixelFormat.ABGR4Unorm || info.Format == Format.A1B5G5R5Unorm)
            {
                var tempB = swizzleB;
                var tempA = swizzleA;

                swizzleB = swizzleG;
                swizzleA = swizzleR;
                swizzleR = tempA;
                swizzleG = tempB;
            }

            return new MTLTextureSwizzleChannels
            {
                red = swizzleR,
                green = swizzleG,
                blue = swizzleB,
                alpha = swizzleA
            };
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            CommandBufferScoped cbs = Pipeline.Cbs;

            TextureBase src = this;
            TextureBase dst = (TextureBase)destination;

            if (!Valid || !dst.Valid)
            {
                return;
            }

            var srcImage = GetHandle();
            var dstImage = dst.GetHandle();

            if (!dst.Info.Target.IsMultisample() && Info.Target.IsMultisample())
            {
                // int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);

                // _gd.HelperShader.CopyMSToNonMS(_gd, cbs, src, dst, 0, firstLayer, layers);
            }
            else if (dst.Info.Target.IsMultisample() && !Info.Target.IsMultisample())
            {
                // int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);

                // _gd.HelperShader.CopyNonMSToMS(_gd, cbs, src, dst, 0, firstLayer, layers);
            }
            else if (dst.Info.BytesPerPixel != Info.BytesPerPixel)
            {
                // int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);
                // int levels = Math.Min(Info.Levels, dst.Info.Levels - firstLevel);

                // _gd.HelperShader.CopyIncompatibleFormats(_gd, cbs, src, dst, 0, firstLayer, 0, firstLevel, layers, levels);
            }
            else if (src.Info.Format.IsDepthOrStencil() != dst.Info.Format.IsDepthOrStencil())
            {
                // int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);
                // int levels = Math.Min(Info.Levels, dst.Info.Levels - firstLevel);

                // TODO: depth copy?
                // _gd.HelperShader.CopyColor(_gd, cbs, src, dst, 0, firstLayer, 0, FirstLevel, layers, levels);
            }
            else
            {
                TextureCopy.Copy(
                    cbs,
                    srcImage,
                    dstImage,
                    src.Info,
                    dst.Info,
                    0,
                    firstLayer,
                    0,
                    firstLevel);
            }
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            CommandBufferScoped cbs = Pipeline.Cbs;

            TextureBase src = this;
            TextureBase dst = (TextureBase)destination;

            if (!Valid || !dst.Valid)
            {
                return;
            }

            var srcImage = GetHandle();
            var dstImage = dst.GetHandle();

            if (!dst.Info.Target.IsMultisample() && Info.Target.IsMultisample())
            {
                // _gd.HelperShader.CopyMSToNonMS(_gd, cbs, src, dst, srcLayer, dstLayer, 1);
            }
            else if (dst.Info.Target.IsMultisample() && !Info.Target.IsMultisample())
            {
                // _gd.HelperShader.CopyNonMSToMS(_gd, cbs, src, dst, srcLayer, dstLayer, 1);
            }
            else if (dst.Info.BytesPerPixel != Info.BytesPerPixel)
            {
                // _gd.HelperShader.CopyIncompatibleFormats(_gd, cbs, src, dst, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
            }
            else if (src.Info.Format.IsDepthOrStencil() != dst.Info.Format.IsDepthOrStencil())
            {
                // _gd.HelperShader.CopyColor(_gd, cbs, src, dst, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
            }
            else
            {
                TextureCopy.Copy(
                    cbs,
                    srcImage,
                    dstImage,
                    src.Info,
                    dst.Info,
                    srcLayer,
                    dstLayer,
                    srcLevel,
                    dstLevel,
                    1,
                    1);
            }
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            if (!Renderer.CommandBufferPool.OwnedByCurrentThread)
            {
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Metal doesn't currently support scaled blit on background thread.");

                return;
            }

            var dst = (Texture)destination;

            bool isDepthOrStencil = dst.Info.Format.IsDepthOrStencil();

            Pipeline.Blit(this, dst, srcRegion, dstRegion, isDepthOrStencil, linearFilter);
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            var cbs = Pipeline.Cbs;

            int outSize = Info.GetMipSize(level);
            int hostSize = GetBufferDataLength(outSize);

            int offset = range.Offset;

            var autoBuffer = Renderer.BufferManager.GetBuffer(range.Handle, true);
            var mtlBuffer = autoBuffer.Get(cbs, range.Offset, outSize).Value;

            // TODO: D32S8 conversion via temp copy holder

            CopyFromOrToBuffer(cbs, mtlBuffer, MtlTexture, hostSize, true, layer, level, 1, 1, singleSlice: true, offset: offset, stride: stride);
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            return new Texture(Device, Renderer, Pipeline, info, _identitySwizzleHandle, firstLayer, firstLevel);
        }

        private int GetBufferDataLength(int size)
        {
            // TODO: D32S8 conversion

            return size;
        }

        private void CopyDataToBuffer(Span<byte> storage, ReadOnlySpan<byte> input)
        {
            // TODO: D32S8 conversion

            input.CopyTo(storage);
        }

        private ReadOnlySpan<byte> GetDataFromBuffer(ReadOnlySpan<byte> storage, int size, Span<byte> output)
        {
            // TODO: D32S8 conversion

            return storage;
        }

        public void CopyFromOrToBuffer(
            CommandBufferScoped cbs,
            MTLBuffer buffer,
            MTLTexture image,
            int size,
            bool to,
            int dstLayer,
            int dstLevel,
            int dstLayers,
            int dstLevels,
            bool singleSlice,
            int offset = 0,
            int stride = 0)
        {
            MTLBlitCommandEncoder blitCommandEncoder = cbs.Encoders.EnsureBlitEncoder();

            bool is3D = Info.Target == Target.Texture3D;
            int width = Math.Max(1, Info.Width >> dstLevel);
            int height = Math.Max(1, Info.Height >> dstLevel);
            int depth = is3D && !singleSlice ? Math.Max(1, Info.Depth >> dstLevel) : 1;
            int layers = dstLayers;
            int levels = dstLevels;

            for (int oLevel = 0; oLevel < levels; oLevel++)
            {
                int level = oLevel + dstLevel;
                int mipSize = Info.GetMipSize2D(level);

                int mipSizeLevel = GetBufferDataLength(is3D && !singleSlice
                    ? Info.GetMipSize(level)
                    : mipSize * dstLayers);

                int endOffset = offset + mipSizeLevel;

                if ((uint)endOffset > (uint)size)
                {
                    break;
                }

                for (int oLayer = 0; oLayer < layers; oLayer++)
                {
                    int layer = !is3D ? dstLayer + oLayer : 0;
                    int z = is3D ? dstLayer + oLayer : 0;

                    if (to)
                    {
                        blitCommandEncoder.CopyFromTexture(
                            image,
                            (ulong)layer,
                            (ulong)level,
                            new MTLOrigin { z = (ulong)z },
                            new MTLSize { width = (ulong)width, height = (ulong)height, depth = 1 },
                            buffer,
                            (ulong)offset,
                            (ulong)Info.GetMipStride(level),
                            (ulong)mipSize
                        );
                    }
                    else
                    {
                        blitCommandEncoder.CopyFromBuffer(
                            buffer,
                            (ulong)offset,
                            (ulong)Info.GetMipStride(level),
                            (ulong)mipSize,
                            new MTLSize { width = (ulong)width, height = (ulong)height, depth = 1 },
                            image,
                            (ulong)(layer + oLayer),
                            (ulong)level,
                            new MTLOrigin { z = (ulong)z }
                        );
                    }

                    offset += mipSize;
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (Info.Target == Target.Texture3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }
        }

        private ReadOnlySpan<byte> GetData(CommandBufferPool cbp, PersistentFlushBuffer flushBuffer)
        {
            int size = 0;

            for (int level = 0; level < Info.GetLevelsClamped(); level++)
            {
                size += Info.GetMipSize(level);
            }

            size = GetBufferDataLength(size);

            Span<byte> result = flushBuffer.GetTextureData(cbp, this, size);

            return GetDataFromBuffer(result, size, result);
        }

        private ReadOnlySpan<byte> GetData(CommandBufferPool cbp, PersistentFlushBuffer flushBuffer, int layer, int level)
        {
            int size = GetBufferDataLength(Info.GetMipSize(level));

            Span<byte> result = flushBuffer.GetTextureData(cbp, this, size, layer, level);

            return GetDataFromBuffer(result, size, result);
        }

        public PinnedSpan<byte> GetData()
        {
            BackgroundResource resources = Renderer.BackgroundResources.Get();

            if (Renderer.CommandBufferPool.OwnedByCurrentThread)
            {
                Renderer.FlushAllCommands();

                return PinnedSpan<byte>.UnsafeFromSpan(GetData(Renderer.CommandBufferPool, resources.GetFlushBuffer()));
            }

            return PinnedSpan<byte>.UnsafeFromSpan(GetData(resources.GetPool(), resources.GetFlushBuffer()));
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            BackgroundResource resources = Renderer.BackgroundResources.Get();

            if (Renderer.CommandBufferPool.OwnedByCurrentThread)
            {
                Renderer.FlushAllCommands();

                return PinnedSpan<byte>.UnsafeFromSpan(GetData(Renderer.CommandBufferPool, resources.GetFlushBuffer(), layer, level));
            }

            return PinnedSpan<byte>.UnsafeFromSpan(GetData(resources.GetPool(), resources.GetFlushBuffer(), layer, level));
        }

        public void SetData(IMemoryOwner<byte> data)
        {
            var blitCommandEncoder = Pipeline.GetOrCreateBlitEncoder();

            var dataSpan = data.Memory.Span;

            var buffer = Renderer.BufferManager.Create(dataSpan.Length);
            buffer.SetDataUnchecked(0, dataSpan);
            var mtlBuffer = buffer.GetBuffer(false).Get(Pipeline.Cbs).Value;

            int width = Info.Width;
            int height = Info.Height;
            int depth = Info.Depth;
            int levels = Info.GetLevelsClamped();
            int layers = Info.GetLayers();
            bool is3D = Info.Target == Target.Texture3D;

            int offset = 0;

            for (int level = 0; level < levels; level++)
            {
                int mipSize = Info.GetMipSize2D(level);
                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)dataSpan.Length)
                {
                    return;
                }

                for (int layer = 0; layer < layers; layer++)
                {
                    blitCommandEncoder.CopyFromBuffer(
                        mtlBuffer,
                        (ulong)offset,
                        (ulong)Info.GetMipStride(level),
                        (ulong)mipSize,
                        new MTLSize { width = (ulong)width, height = (ulong)height, depth = is3D ? (ulong)depth : 1 },
                        MtlTexture,
                        (ulong)layer,
                        (ulong)level,
                        new MTLOrigin()
                    );

                    offset += mipSize;
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (is3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }

            // Cleanup
            buffer.Dispose();
        }

        private void SetData(ReadOnlySpan<byte> data, int layer, int level, int layers, int levels, bool singleSlice)
        {
            int bufferDataLength = GetBufferDataLength(data.Length);

            using var bufferHolder = Renderer.BufferManager.Create(bufferDataLength);

            // TODO: loadInline logic

            var cbs = Pipeline.Cbs;

            CopyDataToBuffer(bufferHolder.GetDataStorage(0, bufferDataLength), data);

            var buffer = bufferHolder.GetBuffer().Get(cbs).Value;
            var image = GetHandle();

            CopyFromOrToBuffer(cbs, buffer, image, bufferDataLength, false, layer, level, layers, levels, singleSlice);
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level)
        {
            SetData(data.Memory.Span, layer, level, 1, 1, singleSlice: true);

            data.Dispose();
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            var blitCommandEncoder = Pipeline.GetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (MtlTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            var dataSpan = data.Memory.Span;

            var buffer = Renderer.BufferManager.Create(dataSpan.Length);
            buffer.SetDataUnchecked(0, dataSpan);
            var mtlBuffer = buffer.GetBuffer(false).Get(Pipeline.Cbs).Value;

            blitCommandEncoder.CopyFromBuffer(
                mtlBuffer,
                0,
                bytesPerRow,
                bytesPerImage,
                new MTLSize { width = (ulong)region.Width, height = (ulong)region.Height, depth = 1 },
                MtlTexture,
                (ulong)layer,
                (ulong)level,
                new MTLOrigin { x = (ulong)region.X, y = (ulong)region.Y }
            );

            // Cleanup
            buffer.Dispose();
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotImplementedException();
        }

        public override void Release()
        {
            if (_identityIsDifferent)
            {
                _identitySwizzleHandle.Dispose();
            }

            base.Release();
        }
    }
}
