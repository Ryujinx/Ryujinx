using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Format = Ryujinx.Graphics.GAL.Format;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Ryujinx.Graphics.Vulkan
{
    class TextureView : ITexture, IDisposable
    {
        private readonly VulkanRenderer _gd;

        private readonly Device _device;

        private readonly Auto<DisposableImageView> _imageView;
        private readonly Auto<DisposableImageView> _imageViewDraw;
        private readonly Auto<DisposableImageView> _imageViewIdentity;
        private readonly Auto<DisposableImageView> _imageView2dArray;
        private Dictionary<Format, TextureView> _selfManagedViews;

        private readonly TextureCreateInfo _info;

        private HashTableSlim<RenderPassCacheKey, RenderPassHolder> _renderPasses;

        public TextureCreateInfo Info => _info;

        public TextureStorage Storage { get; }

        public int Width => Info.Width;
        public int Height => Info.Height;
        public int Layers => Info.GetDepthOrLayers();
        public int FirstLayer { get; }
        public int FirstLevel { get; }
        public VkFormat VkFormat { get; }
        private int _isValid;
        public bool Valid => Volatile.Read(ref _isValid) != 0;

        public TextureView(
            VulkanRenderer gd,
            Device device,
            TextureCreateInfo info,
            TextureStorage storage,
            int firstLayer,
            int firstLevel)
        {
            _gd = gd;
            _device = device;
            _info = info;
            Storage = storage;
            FirstLayer = firstLayer;
            FirstLevel = firstLevel;

            storage.IncrementViewsCount();

            gd.Textures.Add(this);

            var format = _gd.FormatCapabilities.ConvertToVkFormat(info.Format);
            var usage = TextureStorage.GetImageUsage(info.Format, info.Target, gd.Capabilities.SupportsShaderStorageImageMultisample);
            var levels = (uint)info.Levels;
            var layers = (uint)info.GetLayers();

            VkFormat = format;

            var type = info.Target.ConvertView();

            var swizzleR = info.SwizzleR.Convert();
            var swizzleG = info.SwizzleG.Convert();
            var swizzleB = info.SwizzleB.Convert();
            var swizzleA = info.SwizzleA.Convert();

            if (info.Format == Format.R5G5B5A1Unorm ||
                info.Format == Format.R5G5B5X1Unorm ||
                info.Format == Format.R5G6B5Unorm)
            {
                (swizzleB, swizzleR) = (swizzleR, swizzleB);
            }
            else if (VkFormat == VkFormat.R4G4B4A4UnormPack16 || info.Format == Format.A1B5G5R5Unorm)
            {
                var tempB = swizzleB;
                var tempA = swizzleA;

                swizzleB = swizzleG;
                swizzleA = swizzleR;
                swizzleR = tempA;
                swizzleG = tempB;
            }

            var componentMapping = new ComponentMapping(swizzleR, swizzleG, swizzleB, swizzleA);

            var aspectFlags = info.Format.ConvertAspectFlags(info.DepthStencilMode);
            var aspectFlagsDepth = info.Format.ConvertAspectFlags();

            var subresourceRange = new ImageSubresourceRange(aspectFlags, (uint)firstLevel, levels, (uint)firstLayer, layers);
            var subresourceRangeDepth = new ImageSubresourceRange(aspectFlagsDepth, (uint)firstLevel, levels, (uint)firstLayer, layers);

            unsafe Auto<DisposableImageView> CreateImageView(ComponentMapping cm, ImageSubresourceRange sr, ImageViewType viewType, ImageUsageFlags usageFlags)
            {
                var usage = new ImageViewUsageCreateInfo
                {
                    SType = StructureType.ImageViewUsageCreateInfo,
                    Usage = usageFlags,
                };

                var imageCreateInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = storage.GetImageForViewCreation(),
                    ViewType = viewType,
                    Format = format,
                    Components = cm,
                    SubresourceRange = sr,
                    PNext = &usage,
                };

                gd.Api.CreateImageView(device, imageCreateInfo, null, out var imageView).ThrowOnError();
                return new Auto<DisposableImageView>(new DisposableImageView(gd.Api, device, imageView), null, storage.GetImage());
            }

            ImageUsageFlags shaderUsage = ImageUsageFlags.SampledBit;

            if (info.Format.IsImageCompatible())
            {
                shaderUsage |= ImageUsageFlags.StorageBit;
            }

            _imageView = CreateImageView(componentMapping, subresourceRange, type, shaderUsage);

            // Framebuffer attachments and storage images requires a identity component mapping.
            var identityComponentMapping = new ComponentMapping(
                ComponentSwizzle.R,
                ComponentSwizzle.G,
                ComponentSwizzle.B,
                ComponentSwizzle.A);

            _imageViewDraw = CreateImageView(identityComponentMapping, subresourceRangeDepth, type, usage);
            _imageViewIdentity = aspectFlagsDepth == aspectFlags ? _imageViewDraw : CreateImageView(identityComponentMapping, subresourceRange, type, usage);

            // Framebuffer attachments also require 3D textures to be bound as 2D array.
            if (info.Target == Target.Texture3D)
            {
                if (gd.Capabilities.PortabilitySubset.HasFlag(PortabilitySubsetFlags.No3DImageView))
                {
                    if (levels == 1 && (info.Format.IsRtColorCompatible() || info.Format.IsDepthOrStencil()))
                    {
                        subresourceRange = new ImageSubresourceRange(aspectFlags, (uint)firstLevel, levels, (uint)firstLayer, 1);

                        _imageView2dArray = CreateImageView(identityComponentMapping, subresourceRange, ImageViewType.Type2D, ImageUsageFlags.ColorAttachmentBit);
                    }
                }
                else
                {
                    subresourceRange = new ImageSubresourceRange(aspectFlags, (uint)firstLevel, levels, (uint)firstLayer, (uint)info.Depth);

                    _imageView2dArray = CreateImageView(identityComponentMapping, subresourceRange, ImageViewType.Type2DArray, usage);
                }
            }

            _isValid = 1;
        }

        /// <summary>
        /// Create a texture view for an existing swapchain image view.
        /// Does not set storage, so only appropriate for swapchain use.
        /// </summary>
        /// <remarks>Do not use this for normal textures, and make sure uses do not try to read storage.</remarks>
        public TextureView(VulkanRenderer gd, Device device, DisposableImageView view, TextureCreateInfo info, VkFormat format)
        {
            _gd = gd;
            _device = device;

            _imageView = new Auto<DisposableImageView>(view);
            _imageViewDraw = _imageView;
            _imageViewIdentity = _imageView;
            _info = info;

            VkFormat = format;

            _isValid = 1;
        }

        public Auto<DisposableImage> GetImage()
        {
            return Storage.GetImage();
        }

        public Auto<DisposableImageView> GetImageView()
        {
            return _imageView;
        }

        public Auto<DisposableImageView> GetIdentityImageView()
        {
            return _imageViewIdentity;
        }

        public Auto<DisposableImageView> GetImageViewForAttachment()
        {
            return _imageView2dArray ?? _imageViewDraw;
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            var src = this;
            var dst = (TextureView)destination;

            if (!Valid || !dst.Valid)
            {
                return;
            }

            _gd.PipelineInternal.EndRenderPass();

            var cbs = _gd.PipelineInternal.CurrentCommandBuffer;

            var srcImage = src.GetImage().Get(cbs).Value;
            var dstImage = dst.GetImage().Get(cbs).Value;

            if (!dst.Info.Target.IsMultisample() && Info.Target.IsMultisample())
            {
                int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);
                _gd.HelperShader.CopyMSToNonMS(_gd, cbs, src, dst, 0, firstLayer, layers);
            }
            else if (dst.Info.Target.IsMultisample() && !Info.Target.IsMultisample())
            {
                int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);
                _gd.HelperShader.CopyNonMSToMS(_gd, cbs, src, dst, 0, firstLayer, layers);
            }
            else if (dst.Info.BytesPerPixel != Info.BytesPerPixel)
            {
                int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);
                int levels = Math.Min(Info.Levels, dst.Info.Levels - firstLevel);
                _gd.HelperShader.CopyIncompatibleFormats(_gd, cbs, src, dst, 0, firstLayer, 0, firstLevel, layers, levels);
            }
            else if (src.Info.Format.IsDepthOrStencil() != dst.Info.Format.IsDepthOrStencil())
            {
                int layers = Math.Min(Info.GetLayers(), dst.Info.GetLayers() - firstLayer);
                int levels = Math.Min(Info.Levels, dst.Info.Levels - firstLevel);

                _gd.HelperShader.CopyColor(_gd, cbs, src, dst, 0, firstLayer, 0, FirstLevel, layers, levels);
            }
            else
            {
                TextureCopy.Copy(
                    _gd.Api,
                    cbs.CommandBuffer,
                    srcImage,
                    dstImage,
                    src.Info,
                    dst.Info,
                    src.FirstLayer,
                    dst.FirstLayer,
                    src.FirstLevel,
                    dst.FirstLevel,
                    0,
                    firstLayer,
                    0,
                    firstLevel);
            }
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            var src = this;
            var dst = (TextureView)destination;

            if (!Valid || !dst.Valid)
            {
                return;
            }

            _gd.PipelineInternal.EndRenderPass();

            var cbs = _gd.PipelineInternal.CurrentCommandBuffer;

            var srcImage = src.GetImage().Get(cbs).Value;
            var dstImage = dst.GetImage().Get(cbs).Value;

            if (!dst.Info.Target.IsMultisample() && Info.Target.IsMultisample())
            {
                _gd.HelperShader.CopyMSToNonMS(_gd, cbs, src, dst, srcLayer, dstLayer, 1);
            }
            else if (dst.Info.Target.IsMultisample() && !Info.Target.IsMultisample())
            {
                _gd.HelperShader.CopyNonMSToMS(_gd, cbs, src, dst, srcLayer, dstLayer, 1);
            }
            else if (dst.Info.BytesPerPixel != Info.BytesPerPixel)
            {
                _gd.HelperShader.CopyIncompatibleFormats(_gd, cbs, src, dst, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
            }
            else if (src.Info.Format.IsDepthOrStencil() != dst.Info.Format.IsDepthOrStencil())
            {
                _gd.HelperShader.CopyColor(_gd, cbs, src, dst, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
            }
            else
            {
                TextureCopy.Copy(
                    _gd.Api,
                    cbs.CommandBuffer,
                    srcImage,
                    dstImage,
                    src.Info,
                    dst.Info,
                    src.FirstLayer,
                    dst.FirstLayer,
                    src.FirstLevel,
                    dst.FirstLevel,
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
            var dst = (TextureView)destination;

            if (_gd.CommandBufferPool.OwnedByCurrentThread)
            {
                _gd.PipelineInternal.EndRenderPass();

                var cbs = _gd.PipelineInternal.CurrentCommandBuffer;

                CopyToImpl(cbs, dst, srcRegion, dstRegion, linearFilter);
            }
            else
            {
                var cbp = _gd.BackgroundResources.Get().GetPool();

                using var cbs = cbp.Rent();

                CopyToImpl(cbs, dst, srcRegion, dstRegion, linearFilter);
            }
        }

        private void CopyToImpl(CommandBufferScoped cbs, TextureView dst, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            var src = this;

            var srcFormat = GetCompatibleGalFormat(src.Info.Format);
            var dstFormat = GetCompatibleGalFormat(dst.Info.Format);

            bool srcUsesStorageFormat = src.VkFormat == src.Storage.VkFormat;
            bool dstUsesStorageFormat = dst.VkFormat == dst.Storage.VkFormat;

            int layers = Math.Min(dst.Info.GetDepthOrLayers(), src.Info.GetDepthOrLayers());
            int levels = Math.Min(dst.Info.Levels, src.Info.Levels);

            if (srcUsesStorageFormat && dstUsesStorageFormat)
            {
                if ((srcRegion.X1 | dstRegion.X1) == 0 &&
                    (srcRegion.Y1 | dstRegion.Y1) == 0 &&
                    srcRegion.X2 == src.Width &&
                    srcRegion.Y2 == src.Height &&
                    dstRegion.X2 == dst.Width &&
                    dstRegion.Y2 == dst.Height &&
                    src.Width == dst.Width &&
                    src.Height == dst.Height &&
                    src.VkFormat == dst.VkFormat)
                {
                    if (src.Info.Samples > 1 && src.Info.Samples != dst.Info.Samples && src.Info.Format.IsDepthOrStencil())
                    {
                        // CmdResolveImage does not support depth-stencil resolve, so we need to use an alternative path
                        // for those textures.
                        TextureCopy.ResolveDepthStencil(_gd, _device, cbs, src, dst);
                    }
                    else
                    {
                        TextureCopy.Copy(
                            _gd.Api,
                            cbs.CommandBuffer,
                            src.GetImage().Get(cbs).Value,
                            dst.GetImage().Get(cbs).Value,
                            src.Info,
                            dst.Info,
                            src.FirstLayer,
                            dst.FirstLayer,
                            src.FirstLevel,
                            dst.FirstLevel,
                            0,
                            0,
                            0,
                            0,
                            layers,
                            levels);
                    }

                    return;
                }

                if (_gd.FormatCapabilities.OptimalFormatSupports(FormatFeatureFlags.BlitSrcBit, srcFormat) &&
                    _gd.FormatCapabilities.OptimalFormatSupports(FormatFeatureFlags.BlitDstBit, dstFormat))
                {
                    TextureCopy.Blit(
                        _gd.Api,
                        cbs.CommandBuffer,
                        src.GetImage().Get(cbs).Value,
                        dst.GetImage().Get(cbs).Value,
                        src.Info,
                        dst.Info,
                        srcRegion,
                        dstRegion,
                        src.FirstLayer,
                        dst.FirstLayer,
                        src.FirstLevel,
                        dst.FirstLevel,
                        layers,
                        levels,
                        linearFilter);

                    return;
                }
            }

            bool isDepthOrStencil = dst.Info.Format.IsDepthOrStencil();

            if (!VulkanConfiguration.UseUnsafeBlit || (_gd.Vendor != Vendor.Nvidia && _gd.Vendor != Vendor.Intel))
            {
                _gd.HelperShader.Blit(
                    _gd,
                    src,
                    dst,
                    srcRegion,
                    dstRegion,
                    layers,
                    levels,
                    isDepthOrStencil,
                    linearFilter);

                return;
            }

            Auto<DisposableImage> srcImage;
            Auto<DisposableImage> dstImage;

            if (isDepthOrStencil)
            {
                srcImage = src.Storage.CreateAliasedColorForDepthStorageUnsafe(srcFormat).GetImage();
                dstImage = dst.Storage.CreateAliasedColorForDepthStorageUnsafe(dstFormat).GetImage();
            }
            else
            {
                srcImage = src.Storage.CreateAliasedStorageUnsafe(srcFormat).GetImage();
                dstImage = dst.Storage.CreateAliasedStorageUnsafe(dstFormat).GetImage();
            }

            TextureCopy.Blit(
                _gd.Api,
                cbs.CommandBuffer,
                srcImage.Get(cbs).Value,
                dstImage.Get(cbs).Value,
                src.Info,
                dst.Info,
                srcRegion,
                dstRegion,
                src.FirstLayer,
                dst.FirstLayer,
                src.FirstLevel,
                dst.FirstLevel,
                layers,
                levels,
                linearFilter,
                ImageAspectFlags.ColorBit,
                ImageAspectFlags.ColorBit);
        }

        public static unsafe void InsertMemoryBarrier(
            Vk api,
            CommandBuffer commandBuffer,
            AccessFlags srcAccessMask,
            AccessFlags dstAccessMask,
            PipelineStageFlags srcStageMask,
            PipelineStageFlags dstStageMask)
        {
            MemoryBarrier memoryBarrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = srcAccessMask,
                DstAccessMask = dstAccessMask,
            };

            api.CmdPipelineBarrier(
                commandBuffer,
                srcStageMask,
                dstStageMask,
                DependencyFlags.None,
                1,
                memoryBarrier,
                0,
                null,
                0,
                null);
        }

        public static ImageMemoryBarrier GetImageBarrier(
            Image image,
            AccessFlags srcAccessMask,
            AccessFlags dstAccessMask,
            ImageAspectFlags aspectFlags,
            int firstLayer,
            int firstLevel,
            int layers,
            int levels)
        {
            return new()
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = srcAccessMask,
                DstAccessMask = dstAccessMask,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                OldLayout = ImageLayout.General,
                NewLayout = ImageLayout.General,
                SubresourceRange = new ImageSubresourceRange(aspectFlags, (uint)firstLevel, (uint)levels, (uint)firstLayer, (uint)layers),
            };
        }

        public static unsafe void InsertImageBarrier(
            Vk api,
            CommandBuffer commandBuffer,
            Image image,
            AccessFlags srcAccessMask,
            AccessFlags dstAccessMask,
            PipelineStageFlags srcStageMask,
            PipelineStageFlags dstStageMask,
            ImageAspectFlags aspectFlags,
            int firstLayer,
            int firstLevel,
            int layers,
            int levels)
        {
            ImageMemoryBarrier memoryBarrier = GetImageBarrier(
                image,
                srcAccessMask,
                dstAccessMask,
                aspectFlags,
                firstLayer,
                firstLevel,
                layers,
                levels);

            api.CmdPipelineBarrier(
                commandBuffer,
                srcStageMask,
                dstStageMask,
                0,
                0,
                null,
                0,
                null,
                1,
                memoryBarrier);
        }

        public TextureView GetView(Format format)
        {
            if (format == Info.Format)
            {
                return this;
            }

            if (_selfManagedViews != null && _selfManagedViews.TryGetValue(format, out var view))
            {
                return view;
            }

            view = CreateViewImpl(new TextureCreateInfo(
                Info.Width,
                Info.Height,
                Info.Depth,
                Info.Levels,
                Info.Samples,
                Info.BlockWidth,
                Info.BlockHeight,
                Info.BytesPerPixel,
                format,
                Info.DepthStencilMode,
                Info.Target,
                Info.SwizzleR,
                Info.SwizzleG,
                Info.SwizzleB,
                Info.SwizzleA), 0, 0);

            (_selfManagedViews ??= new Dictionary<Format, TextureView>()).Add(format, view);

            return view;
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            return CreateViewImpl(info, firstLayer, firstLevel);
        }

        public TextureView CreateViewImpl(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            return new TextureView(_gd, _device, info, Storage, FirstLayer + firstLayer, FirstLevel + firstLevel);
        }

        public byte[] GetData(int x, int y, int width, int height)
        {
            int size = width * height * Info.BytesPerPixel;
            using var bufferHolder = _gd.BufferManager.Create(_gd, size);

            using (var cbs = _gd.CommandBufferPool.Rent())
            {
                var buffer = bufferHolder.GetBuffer(cbs.CommandBuffer).Get(cbs).Value;
                var image = GetImage().Get(cbs).Value;

                CopyFromOrToBuffer(cbs.CommandBuffer, buffer, image, size, true, 0, 0, x, y, width, height);
            }

            bufferHolder.WaitForFences();
            byte[] bitmap = new byte[size];
            GetDataFromBuffer(bufferHolder.GetDataStorage(0, size), size, Span<byte>.Empty).CopyTo(bitmap);
            return bitmap;
        }

        public PinnedSpan<byte> GetData()
        {
            BackgroundResource resources = _gd.BackgroundResources.Get();

            if (_gd.CommandBufferPool.OwnedByCurrentThread)
            {
                _gd.FlushAllCommands();

                return PinnedSpan<byte>.UnsafeFromSpan(GetData(_gd.CommandBufferPool, resources.GetFlushBuffer()));
            }

            return PinnedSpan<byte>.UnsafeFromSpan(GetData(resources.GetPool(), resources.GetFlushBuffer()));
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            BackgroundResource resources = _gd.BackgroundResources.Get();

            if (_gd.CommandBufferPool.OwnedByCurrentThread)
            {
                _gd.FlushAllCommands();

                return PinnedSpan<byte>.UnsafeFromSpan(GetData(_gd.CommandBufferPool, resources.GetFlushBuffer(), layer, level));
            }

            return PinnedSpan<byte>.UnsafeFromSpan(GetData(resources.GetPool(), resources.GetFlushBuffer(), layer, level));
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            _gd.PipelineInternal.EndRenderPass();
            var cbs = _gd.PipelineInternal.CurrentCommandBuffer;

            int outSize = Info.GetMipSize(level);
            int hostSize = GetBufferDataLength(outSize);

            var image = GetImage().Get(cbs).Value;
            int offset = range.Offset;

            Auto<DisposableBuffer> autoBuffer = _gd.BufferManager.GetBuffer(cbs.CommandBuffer, range.Handle, true);
            VkBuffer buffer = autoBuffer.Get(cbs, range.Offset, outSize).Value;

            if (PrepareOutputBuffer(cbs, hostSize, buffer, out VkBuffer copyToBuffer, out BufferHolder tempCopyHolder))
            {
                offset = 0;
            }

            CopyFromOrToBuffer(cbs.CommandBuffer, copyToBuffer, image, hostSize, true, layer, level, 1, 1, singleSlice: true, offset, stride);

            if (tempCopyHolder != null)
            {
                CopyDataToOutputBuffer(cbs, tempCopyHolder, autoBuffer, hostSize, range.Offset);
                tempCopyHolder.Dispose();
            }
        }

        private ReadOnlySpan<byte> GetData(CommandBufferPool cbp, PersistentFlushBuffer flushBuffer)
        {
            int size = 0;

            for (int level = 0; level < Info.Levels; level++)
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

        public void SetData(SpanOrArray<byte> data)
        {
            SetData(data, 0, 0, Info.GetLayers(), Info.Levels, singleSlice: false);
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level)
        {
            SetData(data, layer, level, 1, 1, singleSlice: true);
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level, Rectangle<int> region)
        {
            SetData(data, layer, level, 1, 1, singleSlice: true, region);
        }

        private void SetData(ReadOnlySpan<byte> data, int layer, int level, int layers, int levels, bool singleSlice, Rectangle<int>? region = null)
        {
            int bufferDataLength = GetBufferDataLength(data.Length);

            using var bufferHolder = _gd.BufferManager.Create(_gd, bufferDataLength);

            Auto<DisposableImage> imageAuto = GetImage();

            // Load texture data inline if the texture has been used on the current command buffer.

            bool loadInline = Storage.HasCommandBufferDependency(_gd.PipelineInternal.CurrentCommandBuffer);

            var cbs = loadInline ? _gd.PipelineInternal.CurrentCommandBuffer : _gd.PipelineInternal.GetPreloadCommandBuffer();

            if (loadInline)
            {
                _gd.PipelineInternal.EndRenderPass();
            }

            CopyDataToBuffer(bufferHolder.GetDataStorage(0, bufferDataLength), data);

            var buffer = bufferHolder.GetBuffer(cbs.CommandBuffer).Get(cbs).Value;
            var image = imageAuto.Get(cbs).Value;

            if (region.HasValue)
            {
                CopyFromOrToBuffer(
                    cbs.CommandBuffer,
                    buffer,
                    image,
                    bufferDataLength,
                    false,
                    layer,
                    level,
                    region.Value.X,
                    region.Value.Y,
                    region.Value.Width,
                    region.Value.Height);
            }
            else
            {
                CopyFromOrToBuffer(cbs.CommandBuffer, buffer, image, bufferDataLength, false, layer, level, layers, levels, singleSlice);
            }
        }

        private int GetBufferDataLength(int length)
        {
            if (NeedsD24S8Conversion())
            {
                return length * 2;
            }

            return length;
        }

        private Format GetCompatibleGalFormat(Format format)
        {
            if (NeedsD24S8Conversion())
            {
                return Format.D32FloatS8Uint;
            }

            return format;
        }

        private void CopyDataToBuffer(Span<byte> storage, ReadOnlySpan<byte> input)
        {
            if (NeedsD24S8Conversion())
            {
                FormatConverter.ConvertD24S8ToD32FS8(storage, input);
                return;
            }

            input.CopyTo(storage);
        }

        private ReadOnlySpan<byte> GetDataFromBuffer(ReadOnlySpan<byte> storage, int size, Span<byte> output)
        {
            if (NeedsD24S8Conversion())
            {
                if (output.IsEmpty)
                {
                    output = new byte[GetBufferDataLength(size)];
                }

                FormatConverter.ConvertD32FS8ToD24S8(output, storage);
                return output;
            }

            return storage;
        }

        private bool PrepareOutputBuffer(CommandBufferScoped cbs, int hostSize, VkBuffer target, out VkBuffer copyTarget, out BufferHolder copyTargetHolder)
        {
            if (NeedsD24S8Conversion())
            {
                copyTargetHolder = _gd.BufferManager.Create(_gd, hostSize);
                copyTarget = copyTargetHolder.GetBuffer().Get(cbs, 0, hostSize).Value;

                return true;
            }

            copyTarget = target;
            copyTargetHolder = null;

            return false;
        }

        private void CopyDataToOutputBuffer(CommandBufferScoped cbs, BufferHolder hostData, Auto<DisposableBuffer> copyTarget, int hostSize, int dstOffset)
        {
            if (NeedsD24S8Conversion())
            {
                _gd.HelperShader.ConvertD32S8ToD24S8(_gd, cbs, hostData, copyTarget, hostSize / (2 * sizeof(int)), dstOffset);
            }
        }

        private bool NeedsD24S8Conversion()
        {
            return FormatCapabilities.IsD24S8(Info.Format) && VkFormat == VkFormat.D32SfloatS8Uint;
        }

        public void CopyFromOrToBuffer(
            CommandBuffer commandBuffer,
            VkBuffer buffer,
            Image image,
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
            bool is3D = Info.Target == Target.Texture3D;
            int width = Math.Max(1, Info.Width >> dstLevel);
            int height = Math.Max(1, Info.Height >> dstLevel);
            int depth = is3D && !singleSlice ? Math.Max(1, Info.Depth >> dstLevel) : 1;
            int layer = is3D ? 0 : dstLayer;
            int layers = dstLayers;
            int levels = dstLevels;

            for (int level = 0; level < levels; level++)
            {
                int mipSize = GetBufferDataLength(is3D && !singleSlice
                    ? Info.GetMipSize(dstLevel + level)
                    : Info.GetMipSize2D(dstLevel + level) * dstLayers);

                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)size)
                {
                    break;
                }

                int rowLength = ((stride == 0 ? Info.GetMipStride(dstLevel + level) : stride) / Info.BytesPerPixel) * Info.BlockWidth;

                var aspectFlags = Info.Format.ConvertAspectFlags();

                if (aspectFlags == (ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit))
                {
                    aspectFlags = ImageAspectFlags.DepthBit;
                }

                var sl = new ImageSubresourceLayers(
                    aspectFlags,
                    (uint)(FirstLevel + dstLevel + level),
                    (uint)(FirstLayer + layer),
                    (uint)layers);

                var extent = new Extent3D((uint)width, (uint)height, (uint)depth);

                int z = is3D ? dstLayer : 0;

                var region = new BufferImageCopy(
                    (ulong)offset,
                    (uint)AlignUpNpot(rowLength, Info.BlockWidth),
                    (uint)AlignUpNpot(height, Info.BlockHeight),
                    sl,
                    new Offset3D(0, 0, z),
                    extent);

                if (to)
                {
                    _gd.Api.CmdCopyImageToBuffer(commandBuffer, image, ImageLayout.General, buffer, 1, region);
                }
                else
                {
                    _gd.Api.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.General, 1, region);
                }

                offset += mipSize;

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (Info.Target == Target.Texture3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }
        }

        private void CopyFromOrToBuffer(
            CommandBuffer commandBuffer,
            VkBuffer buffer,
            Image image,
            int size,
            bool to,
            int dstLayer,
            int dstLevel,
            int x,
            int y,
            int width,
            int height)
        {
            var aspectFlags = Info.Format.ConvertAspectFlags();

            if (aspectFlags == (ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit))
            {
                aspectFlags = ImageAspectFlags.DepthBit;
            }

            var sl = new ImageSubresourceLayers(aspectFlags, (uint)(FirstLevel + dstLevel), (uint)(FirstLayer + dstLayer), 1);

            var extent = new Extent3D((uint)width, (uint)height, 1);

            int rowLengthAlignment = Info.BlockWidth;

            // We expect all data being written into the texture to have a stride aligned by 4.
            if (!to && Info.BytesPerPixel < 4)
            {
                rowLengthAlignment = 4 / Info.BytesPerPixel;
            }

            var region = new BufferImageCopy(
                0,
                (uint)AlignUpNpot(width, rowLengthAlignment),
                (uint)AlignUpNpot(height, Info.BlockHeight),
                sl,
                new Offset3D(x, y, 0),
                extent);

            if (to)
            {
                _gd.Api.CmdCopyImageToBuffer(commandBuffer, image, ImageLayout.General, buffer, 1, region);
            }
            else
            {
                _gd.Api.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.General, 1, region);
            }
        }

        private static int AlignUpNpot(int size, int alignment)
        {
            int remainder = size % alignment;
            if (remainder == 0)
            {
                return size;
            }

            return size + (alignment - remainder);
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotImplementedException();
        }

        public (Auto<DisposableRenderPass> renderPass, Auto<DisposableFramebuffer> framebuffer) GetPassAndFramebuffer(
            VulkanRenderer gd,
            Device device,
            CommandBufferScoped cbs,
            FramebufferParams fb)
        {
            var key = fb.GetRenderPassCacheKey();

            if (_renderPasses == null || !_renderPasses.TryGetValue(ref key, out RenderPassHolder rpHolder))
            {
                rpHolder = new RenderPassHolder(gd, device, key, fb);
            }

            return (rpHolder.GetRenderPass(), rpHolder.GetFramebuffer(gd, cbs, fb));
        }

        public void AddRenderPass(RenderPassCacheKey key, RenderPassHolder renderPass)
        {
            _renderPasses ??= new HashTableSlim<RenderPassCacheKey, RenderPassHolder>();

            _renderPasses.Add(ref key, renderPass);
        }

        public void RemoveRenderPass(RenderPassCacheKey key)
        {
            _renderPasses.Remove(ref key);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                bool wasValid = Interlocked.Exchange(ref _isValid, 0) != 0;
                if (wasValid)
                {
                    _gd.Textures.Remove(this);

                    _imageView.Dispose();
                    _imageView2dArray?.Dispose();

                    if (_imageViewIdentity != _imageView)
                    {
                        _imageViewIdentity.Dispose();
                    }

                    if (_imageViewDraw != _imageViewIdentity)
                    {
                        _imageViewDraw.Dispose();
                    }

                    Storage?.DecrementViewsCount();

                    if (_renderPasses != null)
                    {
                        var renderPasses = _renderPasses.Values.ToArray();

                        foreach (var pass in renderPasses)
                        {
                            pass.Dispose();
                        }
                    }

                    if (_selfManagedViews != null)
                    {
                        foreach (var view in _selfManagedViews.Values)
                        {
                            view.Dispose();
                        }

                        _selfManagedViews = null;
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Release()
        {
            Dispose();
        }
    }
}
