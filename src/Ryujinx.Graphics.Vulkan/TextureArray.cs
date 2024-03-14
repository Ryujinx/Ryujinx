using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Vulkan
{
    class TextureArray : ITextureArray
    {
        private record struct TextureRef
        {
            public TextureStorage Storage;
            public Auto<DisposableImageView> View;
            public Auto<DisposableSampler> Sampler;
        }

        private readonly TextureRef[] _textureRefs;
        private readonly TextureBuffer[] _bufferTextureRefs;

        private readonly DescriptorImageInfo[] _textures;
        private readonly BufferView[] _bufferTextures;

        private HashSet<TextureStorage> _storages;

        private int _cachedCommandBufferIndex;
        private int _cachedSubmissionCount;

        private readonly bool _isBuffer;

        public TextureArray(int size, bool isBuffer)
        {
            if (isBuffer)
            {
                _bufferTextureRefs = new TextureBuffer[size];
                _bufferTextures = new BufferView[size];
            }
            else
            {
                _textureRefs = new TextureRef[size];
                _textures = new DescriptorImageInfo[size];
            }

            _storages = null;

            _cachedCommandBufferIndex = -1;
            _cachedSubmissionCount = 0;

            _isBuffer = isBuffer;
        }

        public void SetSamplers(int index, ISampler[] samplers)
        {
            for (int i = 0; i < samplers.Length; i++)
            {
                ISampler sampler = samplers[i];

                if (sampler is SamplerHolder samplerHolder)
                {
                    _textureRefs[index + i].Sampler = samplerHolder.GetSampler();
                }
                else
                {
                    _textureRefs[index + i].Sampler = default;
                }
            }

            SetDirty();
        }

        public void SetTextures(int index, ITexture[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                ITexture texture = textures[i];

                if (texture is TextureBuffer textureBuffer)
                {
                    _bufferTextureRefs[index + i] = textureBuffer;
                }
                else if (texture is TextureView view)
                {
                    _textureRefs[index + i].Storage = view.Storage;
                    _textureRefs[index + i].View = view.GetImageView();
                }
                else if (!_isBuffer)
                {
                    _textureRefs[index + i].Storage = null;
                    _textureRefs[index + i].View = default;
                }
                else
                {
                    _bufferTextureRefs[index + i] = null;
                }
            }

            SetDirty();
        }

        private void SetDirty()
        {
            _cachedCommandBufferIndex = -1;
            _storages = null;
        }

        public void QueueWriteToReadBarriers(CommandBufferScoped cbs, PipelineStageFlags stageFlags)
        {
            HashSet<TextureStorage> storages = _storages;

            if (storages == null)
            {
                storages = new HashSet<TextureStorage>();

                for (int index = 0; index < _textureRefs.Length; index++)
                {
                    if (_textureRefs[index].Storage != null)
                    {
                        storages.Add(_textureRefs[index].Storage);
                    }
                }

                _storages = storages;
            }

            foreach (TextureStorage storage in storages)
            {
                storage.QueueWriteToReadBarrier(cbs, AccessFlags.ShaderReadBit, stageFlags);
            }
        }

        public ReadOnlySpan<DescriptorImageInfo> GetImageInfos(VulkanRenderer gd, CommandBufferScoped cbs, TextureView dummyTexture, SamplerHolder dummySampler)
        {
            int submissionCount = gd.CommandBufferPool.GetSubmissionCount(cbs.CommandBufferIndex);

            Span<DescriptorImageInfo> textures = _textures;

            if (cbs.CommandBufferIndex == _cachedCommandBufferIndex && submissionCount == _cachedSubmissionCount)
            {
                return textures;
            }

            _cachedCommandBufferIndex = cbs.CommandBufferIndex;
            _cachedSubmissionCount = submissionCount;

            for (int i = 0; i < textures.Length; i++)
            {
                ref var texture = ref textures[i];
                ref var refs = ref _textureRefs[i];

                if (i > 0 && _textureRefs[i - 1].View == refs.View && _textureRefs[i - 1].Sampler == refs.Sampler)
                {
                    texture = textures[i - 1];

                    continue;
                }

                texture.ImageLayout = ImageLayout.General;
                texture.ImageView = refs.View?.Get(cbs).Value ?? default;
                texture.Sampler = refs.Sampler?.Get(cbs).Value ?? default;

                if (texture.ImageView.Handle == 0)
                {
                    texture.ImageView = dummyTexture.GetImageView().Get(cbs).Value;
                }

                if (texture.Sampler.Handle == 0)
                {
                    texture.Sampler = dummySampler.GetSampler().Get(cbs).Value;
                }
            }

            return textures;
        }

        public ReadOnlySpan<BufferView> GetBufferViews(CommandBufferScoped cbs)
        {
            Span<BufferView> bufferTextures = _bufferTextures;

            for (int i = 0; i < bufferTextures.Length; i++)
            {
                bufferTextures[i] = _bufferTextureRefs[i]?.GetBufferView(cbs, false) ?? default;
            }

            return bufferTextures;
        }
    }
}