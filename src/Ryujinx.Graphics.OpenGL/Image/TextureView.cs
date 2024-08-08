using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using System;
using System.Diagnostics;

namespace Ryujinx.Graphics.OpenGL.Image
{
    class TextureView : TextureBase, ITexture, ITextureInfo
    {
        private readonly OpenGLRenderer _renderer;

        private readonly TextureStorage _parent;

        public ITextureInfo Storage => _parent;

        public int FirstLayer { get; private set; }
        public int FirstLevel { get; private set; }

        public TextureView(
            OpenGLRenderer renderer,
            TextureStorage parent,
            TextureCreateInfo info,
            int firstLayer,
            int firstLevel) : base(info)
        {
            _renderer = renderer;
            _parent = parent;

            FirstLayer = firstLayer;
            FirstLevel = firstLevel;

            CreateView();
        }

        private void CreateView()
        {
            TextureTarget target = Target.Convert();

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            PixelInternalFormat pixelInternalFormat;

            if (format.IsCompressed)
            {
                pixelInternalFormat = (PixelInternalFormat)format.PixelFormat;
            }
            else
            {
                pixelInternalFormat = format.PixelInternalFormat;
            }

            int levels = Info.GetLevelsClamped();

            GL.TextureView(
                Handle,
                target,
                _parent.Handle,
                pixelInternalFormat,
                FirstLevel,
                levels,
                FirstLayer,
                Info.GetLayers());

            GL.ActiveTexture(TextureUnit.Texture0);

            GL.BindTexture(target, Handle);

            int[] swizzleRgba = new int[]
            {
                (int)Info.SwizzleR.Convert(),
                (int)Info.SwizzleG.Convert(),
                (int)Info.SwizzleB.Convert(),
                (int)Info.SwizzleA.Convert(),
            };

            if (Info.Format == Format.A1B5G5R5Unorm)
            {
                int temp = swizzleRgba[0];
                int temp2 = swizzleRgba[1];
                swizzleRgba[0] = swizzleRgba[3];
                swizzleRgba[1] = swizzleRgba[2];
                swizzleRgba[2] = temp2;
                swizzleRgba[3] = temp;
            }
            else if (Info.Format.IsBgr())
            {
                // Swap B <-> R for BGRA formats, as OpenGL has no support for them
                // and we need to manually swap the components on read/write on the GPU.
                (swizzleRgba[2], swizzleRgba[0]) = (swizzleRgba[0], swizzleRgba[2]);
            }

            GL.TexParameter(target, TextureParameterName.TextureSwizzleRgba, swizzleRgba);

            int maxLevel = levels - 1;

            if (maxLevel < 0)
            {
                maxLevel = 0;
            }

            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, maxLevel);
            GL.TexParameter(target, TextureParameterName.DepthStencilTextureMode, (int)Info.DepthStencilMode.Convert());
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            firstLayer += FirstLayer;
            firstLevel += FirstLevel;

            return _parent.CreateView(info, firstLayer, firstLevel);
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            TextureView destinationView = (TextureView)destination;

            bool srcIsMultisample = Target.IsMultisample();
            bool dstIsMultisample = destinationView.Target.IsMultisample();

            if (dstIsMultisample != srcIsMultisample && Info.Format.IsDepthOrStencil())
            {
                int layers = Math.Min(Info.GetLayers(), destinationView.Info.GetLayers() - firstLayer);
                CopyWithBlitForDepthMS(destinationView, 0, firstLayer, layers);
            }
            else if (!dstIsMultisample && srcIsMultisample)
            {
                int layers = Math.Min(Info.GetLayers(), destinationView.Info.GetLayers() - firstLayer);
                _renderer.TextureCopyMS.CopyMSToNonMS(this, destinationView, 0, firstLayer, layers);
            }
            else if (dstIsMultisample && !srcIsMultisample)
            {
                int layers = Math.Min(Info.GetLayers(), destinationView.Info.GetLayers() - firstLayer);
                _renderer.TextureCopyMS.CopyNonMSToMS(this, destinationView, 0, firstLayer, layers);
            }
            else if (destinationView.Info.BytesPerPixel != Info.BytesPerPixel)
            {
                int layers = Math.Min(Info.GetLayers(), destinationView.Info.GetLayers() - firstLayer);
                int levels = Math.Min(Info.Levels, destinationView.Info.Levels - firstLevel);
                _renderer.TextureCopyIncompatible.CopyIncompatibleFormats(this, destinationView, 0, firstLayer, 0, firstLevel, layers, levels);
            }
            else if (destinationView.Format.IsDepthOrStencil() != Format.IsDepthOrStencil())
            {
                int layers = Math.Min(Info.GetLayers(), destinationView.Info.GetLayers() - firstLayer);
                int levels = Math.Min(Info.Levels, destinationView.Info.Levels - firstLevel);

                for (int level = 0; level < levels; level++)
                {
                    int srcWidth = Math.Max(1, Width >> level);
                    int srcHeight = Math.Max(1, Height >> level);

                    int dstWidth = Math.Max(1, destinationView.Width >> (firstLevel + level));
                    int dstHeight = Math.Max(1, destinationView.Height >> (firstLevel + level));

                    int minWidth = Math.Min(srcWidth, dstWidth);
                    int minHeight = Math.Min(srcHeight, dstHeight);

                    for (int layer = 0; layer < layers; layer++)
                    {
                        _renderer.TextureCopy.PboCopy(this, destinationView, 0, firstLayer + layer, 0, firstLevel + level, minWidth, minHeight);
                    }
                }
            }
            else
            {
                _renderer.TextureCopy.CopyUnscaled(this, destinationView, 0, firstLayer, 0, firstLevel);
            }
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            TextureView destinationView = (TextureView)destination;

            bool srcIsMultisample = Target.IsMultisample();
            bool dstIsMultisample = destinationView.Target.IsMultisample();

            if (dstIsMultisample != srcIsMultisample && Info.Format.IsDepthOrStencil())
            {
                CopyWithBlitForDepthMS(destinationView, srcLayer, dstLayer, 1);
            }
            else if (!dstIsMultisample && srcIsMultisample)
            {
                _renderer.TextureCopyMS.CopyMSToNonMS(this, destinationView, srcLayer, dstLayer, 1);
            }
            else if (dstIsMultisample && !srcIsMultisample)
            {
                _renderer.TextureCopyMS.CopyNonMSToMS(this, destinationView, srcLayer, dstLayer, 1);
            }
            else if (destinationView.Info.BytesPerPixel != Info.BytesPerPixel)
            {
                _renderer.TextureCopyIncompatible.CopyIncompatibleFormats(this, destinationView, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
            }
            else if (destinationView.Format.IsDepthOrStencil() != Format.IsDepthOrStencil())
            {
                int minWidth = Math.Min(Width, destinationView.Width);
                int minHeight = Math.Min(Height, destinationView.Height);

                _renderer.TextureCopy.PboCopy(this, destinationView, srcLayer, dstLayer, srcLevel, dstLevel, minWidth, minHeight);
            }
            else
            {
                _renderer.TextureCopy.CopyUnscaled(this, destinationView, srcLayer, dstLayer, srcLevel, dstLevel, 1, 1);
            }
        }

        private void CopyWithBlitForDepthMS(TextureView destinationView, int srcLayer, int dstLayer, int layers)
        {
            // This is currently used for multisample <-> non-multisample copies.
            // We can't do that with compute because it's not possible to write depth textures on compute.
            // It can be done with draws, but we don't have support for saving and restoring the OpenGL state
            // for a draw with different state right now.
            // This approach uses blit, which causes a resolution loss since some samples will be lost
            // in the process.

            Extents2D srcRegion = new(0, 0, Width, Height);
            Extents2D dstRegion = new(0, 0, destinationView.Width, destinationView.Height);

            if (destinationView.Target.IsMultisample())
            {
                TextureView intermmediate = _renderer.TextureCopy.IntermediatePool.GetOrCreateWithAtLeast(
                    Info.Target,
                    Info.BlockWidth,
                    Info.BlockHeight,
                    Info.BytesPerPixel,
                    Format,
                    destinationView.Width,
                    destinationView.Height,
                    Info.Depth,
                    1,
                    1);

                _renderer.TextureCopy.Copy(this, intermmediate, srcRegion, dstRegion, false);
                _renderer.TextureCopy.Copy(intermmediate, destinationView, dstRegion, dstRegion, false, srcLayer, dstLayer, 0, 0, layers, 1);
            }
            else
            {
                Target target = Target switch
                {
                    Target.Texture2DMultisample => Target.Texture2D,
                    Target.Texture2DMultisampleArray => Target.Texture2DArray,
                    _ => Target,
                };

                TextureView intermmediate = _renderer.TextureCopy.IntermediatePool.GetOrCreateWithAtLeast(
                    target,
                    Info.BlockWidth,
                    Info.BlockHeight,
                    Info.BytesPerPixel,
                    Format,
                    Width,
                    Height,
                    Info.Depth,
                    1,
                    1);

                _renderer.TextureCopy.Copy(this, intermmediate, srcRegion, srcRegion, false);
                _renderer.TextureCopy.Copy(intermmediate, destinationView, srcRegion, dstRegion, false, srcLayer, dstLayer, 0, 0, layers, 1);
            }
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            _renderer.TextureCopy.Copy(this, (TextureView)destination, srcRegion, dstRegion, linearFilter);
        }

        public unsafe PinnedSpan<byte> GetData()
        {
            int size = 0;
            int levels = Info.GetLevelsClamped();

            for (int level = 0; level < levels; level++)
            {
                size += Info.GetMipSize(level);
            }

            ReadOnlySpan<byte> data;

            if (HwCapabilities.UsePersistentBufferForFlush)
            {
                data = _renderer.PersistentBuffers.Default.GetTextureData(this, size);
            }
            else
            {
                IntPtr target = _renderer.PersistentBuffers.Default.GetHostArray(size);

                WriteTo(target);

                data = new ReadOnlySpan<byte>(target.ToPointer(), size);
            }

            if (Format == Format.S8UintD24Unorm)
            {
                data = FormatConverter.ConvertD24S8ToS8D24(data);
            }

            return PinnedSpan<byte>.UnsafeFromSpan(data);
        }

        public unsafe PinnedSpan<byte> GetData(int layer, int level)
        {
            int size = Info.GetMipSize(level);

            if (HwCapabilities.UsePersistentBufferForFlush)
            {
                return PinnedSpan<byte>.UnsafeFromSpan(_renderer.PersistentBuffers.Default.GetTextureData(this, size, layer, level));
            }
            else
            {
                IntPtr target = _renderer.PersistentBuffers.Default.GetHostArray(size);

                int offset = WriteTo2D(target, layer, level);

                return new PinnedSpan<byte>((byte*)target.ToPointer() + offset, size);
            }
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            if (stride != 0 && stride != BitUtils.AlignUp(Info.Width * Info.BytesPerPixel, 4))
            {
                throw new NotSupportedException("Stride conversion for texture copy to buffer not supported.");
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, range.Handle.ToInt32());

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);
            if (format.PixelFormat == PixelFormat.DepthStencil)
            {
                throw new InvalidOperationException("DepthStencil copy to buffer is not supported for layer/level > 0.");
            }

            int offset = WriteToPbo2D(range.Offset, layer, level);

            Debug.Assert(offset == 0);

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
        }

        public void WriteToPbo(int offset, bool forceBgra)
        {
            WriteTo(IntPtr.Zero + offset, forceBgra);
        }

        public int WriteToPbo2D(int offset, int layer, int level)
        {
            return WriteTo2D(IntPtr.Zero + offset, layer, level);
        }

        private int WriteTo2D(IntPtr data, int layer, int level)
        {
            TextureTarget target = Target.Convert();

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            PixelFormat pixelFormat = format.PixelFormat;
            PixelType pixelType = format.PixelType;

            if (target == TextureTarget.TextureCubeMap || target == TextureTarget.TextureCubeMapArray)
            {
                target = TextureTarget.TextureCubeMapPositiveX + (layer % 6);
            }

            int mipSize = Info.GetMipSize2D(level);

            if (format.IsCompressed)
            {
                GL.GetCompressedTextureSubImage(Handle, level, 0, 0, layer, Math.Max(1, Info.Width >> level), Math.Max(1, Info.Height >> level), 1, mipSize, data);
            }
            else if (format.PixelFormat != PixelFormat.DepthStencil)
            {
                GL.GetTextureSubImage(Handle, level, 0, 0, layer, Math.Max(1, Info.Width >> level), Math.Max(1, Info.Height >> level), 1, pixelFormat, pixelType, mipSize, data);
            }
            else
            {
                GL.GetTexImage(target, level, pixelFormat, pixelType, data);

                // The GL function returns all layers. Must return the offset of the layer we're interested in.
                return target switch
                {
                    TextureTarget.TextureCubeMapArray => (layer / 6) * mipSize,
                    TextureTarget.Texture1DArray => layer * mipSize,
                    TextureTarget.Texture2DArray => layer * mipSize,
                    _ => 0,
                };
            }

            return 0;
        }

        private void WriteTo(IntPtr data, bool forceBgra = false)
        {
            TextureTarget target = Target.Convert();

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            PixelFormat pixelFormat = format.PixelFormat;
            PixelType pixelType = format.PixelType;

            if (forceBgra)
            {
                if (pixelType == PixelType.UnsignedShort565)
                {
                    pixelType = PixelType.UnsignedShort565Reversed;
                }
                else if (pixelType == PixelType.UnsignedShort565Reversed)
                {
                    pixelType = PixelType.UnsignedShort565;
                }
                else
                {
                    pixelFormat = PixelFormat.Bgra;
                }
            }

            int faces = 1;

            if (target == TextureTarget.TextureCubeMap)
            {
                target = TextureTarget.TextureCubeMapPositiveX;

                faces = 6;
            }

            int levels = Info.GetLevelsClamped();

            for (int level = 0; level < levels; level++)
            {
                for (int face = 0; face < faces; face++)
                {
                    int faceOffset = face * Info.GetMipSize2D(level);

                    if (format.IsCompressed)
                    {
                        GL.GetCompressedTexImage(target + face, level, data + faceOffset);
                    }
                    else
                    {
                        GL.GetTexImage(target + face, level, pixelFormat, pixelType, data + faceOffset);
                    }
                }

                data += Info.GetMipSize(level);
            }
        }

        public void SetData(MemoryOwner<byte> data)
        {
            using (data = EnsureDataFormat(data))
            {
                unsafe
                {
                    var dataSpan = data.Span;
                    fixed (byte* ptr = dataSpan)
                    {
                        ReadFrom((IntPtr)ptr, dataSpan.Length);
                    }
                }
            }
        }

        public void SetData(MemoryOwner<byte> data, int layer, int level)
        {
            using (data = EnsureDataFormat(data))
            {
                unsafe
                {
                    fixed (byte* ptr = data.Span)
                    {
                        int width = Math.Max(Info.Width >> level, 1);
                        int height = Math.Max(Info.Height >> level, 1);

                        ReadFrom2D((IntPtr)ptr, layer, level, 0, 0, width, height);
                    }
                }
            }
        }

        public void SetData(MemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            using (data = EnsureDataFormat(data))
            {
                int wInBlocks = BitUtils.DivRoundUp(region.Width, Info.BlockWidth);
                int hInBlocks = BitUtils.DivRoundUp(region.Height, Info.BlockHeight);

                unsafe
                {
                    fixed (byte* ptr = data.Span)
                    {
                        ReadFrom2D(
                            (IntPtr)ptr,
                            layer,
                            level,
                            region.X,
                            region.Y,
                            region.Width,
                            region.Height,
                            BitUtils.AlignUp(wInBlocks * Info.BytesPerPixel, 4) * hInBlocks);
                    }
                }
            }
        }

        public void ReadFromPbo(int offset, int size)
        {
            ReadFrom(IntPtr.Zero + offset, size);
        }

        public void ReadFromPbo2D(int offset, int layer, int level, int width, int height)
        {
            ReadFrom2D(IntPtr.Zero + offset, layer, level, 0, 0, width, height);
        }

        private void ReadFrom2D(IntPtr data, int layer, int level, int x, int y, int width, int height)
        {
            int mipSize = Info.GetMipSize2D(level);

            ReadFrom2D(data, layer, level, x, y, width, height, mipSize);
        }

        private MemoryOwner<byte> EnsureDataFormat(MemoryOwner<byte> data)
        {
            if (Format == Format.S8UintD24Unorm)
            {
                using (data)
                {
                    return FormatConverter.ConvertS8D24ToD24S8(data.Span);
                }
            }

            return data;
        }

        private void ReadFrom2D(IntPtr data, int layer, int level, int x, int y, int width, int height, int mipSize)
        {
            TextureTarget target = Target.Convert();

            Bind(target, 0);

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            switch (Target)
            {
                case Target.Texture1D:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage1D(
                            target,
                            level,
                            x,
                            width,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage1D(
                            target,
                            level,
                            x,
                            width,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Texture1DArray:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage2D(
                            target,
                            level,
                            x,
                            layer,
                            width,
                            1,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage2D(
                            target,
                            level,
                            x,
                            layer,
                            width,
                            1,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Texture2D:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage2D(
                            target,
                            level,
                            x,
                            y,
                            width,
                            height,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage2D(
                            target,
                            level,
                            x,
                            y,
                            width,
                            height,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Texture2DArray:
                case Target.Texture3D:
                case Target.CubemapArray:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage3D(
                            target,
                            level,
                            x,
                            y,
                            layer,
                            width,
                            height,
                            1,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage3D(
                            target,
                            level,
                            x,
                            y,
                            layer,
                            width,
                            height,
                            1,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;

                case Target.Cubemap:
                    if (format.IsCompressed)
                    {
                        GL.CompressedTexSubImage2D(
                            TextureTarget.TextureCubeMapPositiveX + layer,
                            level,
                            x,
                            y,
                            width,
                            height,
                            format.PixelFormat,
                            mipSize,
                            data);
                    }
                    else
                    {
                        GL.TexSubImage2D(
                            TextureTarget.TextureCubeMapPositiveX + layer,
                            level,
                            x,
                            y,
                            width,
                            height,
                            format.PixelFormat,
                            format.PixelType,
                            data);
                    }
                    break;
            }
        }

        private void ReadFrom(IntPtr data, int size)
        {
            TextureTarget target = Target.Convert();
            int baseLevel = 0;

            // glTexSubImage on cubemap views is broken on Intel, we have to use the storage instead.
            if (Target == Target.Cubemap && HwCapabilities.Vendor == HwCapabilities.GpuVendor.IntelWindows)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(target, Storage.Handle);
                baseLevel = FirstLevel;
            }
            else
            {
                Bind(target, 0);
            }

            FormatInfo format = FormatTable.GetFormatInfo(Info.Format);

            int width = Info.Width;
            int height = Info.Height;
            int depth = Info.Depth;
            int levels = Info.GetLevelsClamped();

            int offset = 0;

            for (int level = 0; level < levels; level++)
            {
                int mipSize = Info.GetMipSize(level);

                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)size)
                {
                    return;
                }

                switch (Target)
                {
                    case Target.Texture1D:
                        if (format.IsCompressed)
                        {
                            GL.CompressedTexSubImage1D(
                                target,
                                level,
                                0,
                                width,
                                format.PixelFormat,
                                mipSize,
                                data);
                        }
                        else
                        {
                            GL.TexSubImage1D(
                                target,
                                level,
                                0,
                                width,
                                format.PixelFormat,
                                format.PixelType,
                                data);
                        }
                        break;

                    case Target.Texture1DArray:
                    case Target.Texture2D:
                        if (format.IsCompressed)
                        {
                            GL.CompressedTexSubImage2D(
                                target,
                                level,
                                0,
                                0,
                                width,
                                height,
                                format.PixelFormat,
                                mipSize,
                                data);
                        }
                        else
                        {
                            GL.TexSubImage2D(
                                target,
                                level,
                                0,
                                0,
                                width,
                                height,
                                format.PixelFormat,
                                format.PixelType,
                                data);
                        }
                        break;

                    case Target.Texture2DArray:
                    case Target.Texture3D:
                    case Target.CubemapArray:
                        if (format.IsCompressed)
                        {
                            GL.CompressedTexSubImage3D(
                                target,
                                level,
                                0,
                                0,
                                0,
                                width,
                                height,
                                depth,
                                format.PixelFormat,
                                mipSize,
                                data);
                        }
                        else
                        {
                            GL.TexSubImage3D(
                                target,
                                level,
                                0,
                                0,
                                0,
                                width,
                                height,
                                depth,
                                format.PixelFormat,
                                format.PixelType,
                                data);
                        }
                        break;

                    case Target.Cubemap:
                        int faceOffset = 0;

                        for (int face = 0; face < 6; face++, faceOffset += mipSize / 6)
                        {
                            if (format.IsCompressed)
                            {
                                GL.CompressedTexSubImage2D(
                                    TextureTarget.TextureCubeMapPositiveX + face,
                                    baseLevel + level,
                                    0,
                                    0,
                                    width,
                                    height,
                                    format.PixelFormat,
                                    mipSize / 6,
                                    data + faceOffset);
                            }
                            else
                            {
                                GL.TexSubImage2D(
                                    TextureTarget.TextureCubeMapPositiveX + face,
                                    baseLevel + level,
                                    0,
                                    0,
                                    width,
                                    height,
                                    format.PixelFormat,
                                    format.PixelType,
                                    data + faceOffset);
                            }
                        }
                        break;
                }

                data += mipSize;
                offset += mipSize;

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (Target == Target.Texture3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotSupportedException();
        }

        private void DisposeHandles()
        {
            if (Handle != 0)
            {
                GL.DeleteTexture(Handle);

                Handle = 0;
            }
        }

        /// <summary>
        /// Release the view without necessarily disposing the parent if we are the default view.
        /// This allows it to be added to the resource pool and reused later.
        /// </summary>
        public void Release()
        {
            bool hadHandle = Handle != 0;

            if (_parent.DefaultView != this)
            {
                DisposeHandles();
            }

            if (hadHandle)
            {
                _parent.DecrementViewsCount();
            }
        }

        public void Dispose()
        {
            if (_parent.DefaultView == this)
            {
                // Remove the default view (us), so that the texture cannot be released to the cache.
                _parent.DeleteDefault();
            }

            Release();
        }
    }
}
