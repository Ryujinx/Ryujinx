using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using Format = Ryujinx.Graphics.GAL.Format;
using VkFormat = Silk.NET.Vulkan.Format;

namespace Ryujinx.Graphics.Vulkan
{
    class TextureBuffer : ITexture
    {
        private readonly VulkanRenderer _gd;

        private BufferHandle _bufferHandle;
        private int _offset;
        private int _size;
        private Auto<DisposableBufferView> _bufferView;
        private Dictionary<Format, Auto<DisposableBufferView>> _selfManagedViews;

        private int _bufferCount;

        public int Width { get; }
        public int Height { get; }

        public VkFormat VkFormat { get; }

        public TextureBuffer(VulkanRenderer gd, TextureCreateInfo info)
        {
            _gd = gd;
            Width = info.Width;
            Height = info.Height;
            VkFormat = FormatTable.GetFormat(info.Format);

            gd.Textures.Add(this);
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
            return _gd.GetBufferData(_bufferHandle, _offset, _size);
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            return GetData();
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            throw new NotImplementedException();
        }

        public void Release()
        {
            if (_gd.Textures.Remove(this))
            {
                ReleaseImpl();
            }
        }

        private void ReleaseImpl()
        {
            if (_selfManagedViews != null)
            {
                foreach (var bufferView in _selfManagedViews.Values)
                {
                    bufferView.Dispose();
                }

                _selfManagedViews = null;
            }

            _bufferView?.Dispose();
            _bufferView = null;
        }

        /// <inheritdoc/>
        public void SetData(MemoryOwner<byte> data)
        {
            _gd.SetBufferData(_bufferHandle, _offset, data.Span);
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
            if (_bufferHandle == buffer.Handle &&
                _offset == buffer.Offset &&
                _size == buffer.Size &&
                _bufferCount == _gd.BufferManager.BufferCount)
            {
                return;
            }

            _bufferHandle = buffer.Handle;
            _offset = buffer.Offset;
            _size = buffer.Size;
            _bufferCount = _gd.BufferManager.BufferCount;

            ReleaseImpl();
        }

        public BufferView GetBufferView(CommandBufferScoped cbs, bool write)
        {
            _bufferView ??= _gd.BufferManager.CreateView(_bufferHandle, VkFormat, _offset, _size, ReleaseImpl);

            return _bufferView?.Get(cbs, _offset, _size, write).Value ?? default;
        }

        public BufferView GetBufferView(CommandBufferScoped cbs, Format format, bool write)
        {
            var vkFormat = FormatTable.GetFormat(format);
            if (vkFormat == VkFormat)
            {
                return GetBufferView(cbs, write);
            }

            if (_selfManagedViews != null && _selfManagedViews.TryGetValue(format, out var bufferView))
            {
                return bufferView.Get(cbs, _offset, _size, write).Value;
            }

            bufferView = _gd.BufferManager.CreateView(_bufferHandle, vkFormat, _offset, _size, ReleaseImpl);

            if (bufferView != null)
            {
                (_selfManagedViews ??= new Dictionary<Format, Auto<DisposableBufferView>>()).Add(format, bufferView);
            }

            return bufferView?.Get(cbs, _offset, _size, write).Value ?? default;
        }
    }
}
