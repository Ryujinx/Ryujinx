using Silk.NET.Vulkan;
using System;

namespace Ryujinx.Graphics.Vulkan
{
    internal class RenderPassHolder
    {
        private readonly struct FramebufferCacheKey : IRefEquatable<FramebufferCacheKey>
        {
            private readonly uint _width;
            private readonly uint _height;
            private readonly uint _layers;

            public FramebufferCacheKey(uint width, uint height, uint layers)
            {
                _width = width;
                _height = height;
                _layers = layers;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_width, _height, _layers);
            }

            public bool Equals(ref FramebufferCacheKey other)
            {
                return other._width == _width && other._height == _height && other._layers == _layers;
            }
        }

        private readonly TextureView[] _textures;
        private readonly Auto<DisposableRenderPass> _renderPass;
        private readonly HashTableSlim<FramebufferCacheKey, Auto<DisposableFramebuffer>> _framebuffers;
        private readonly RenderPassCacheKey _key;

        public unsafe RenderPassHolder(VulkanRenderer gd, Device device, RenderPassCacheKey key, FramebufferParams fb)
        {
            // Create render pass using framebuffer params.

            const int MaxAttachments = Constants.MaxRenderTargets + 1;

            AttachmentDescription[] attachmentDescs = null;

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
            };

            AttachmentReference* attachmentReferences = stackalloc AttachmentReference[MaxAttachments];

            var hasFramebuffer = fb != null;

            if (hasFramebuffer && fb.AttachmentsCount != 0)
            {
                attachmentDescs = new AttachmentDescription[fb.AttachmentsCount];

                for (int i = 0; i < fb.AttachmentsCount; i++)
                {
                    attachmentDescs[i] = new AttachmentDescription(
                        0,
                        fb.AttachmentFormats[i],
                        TextureStorage.ConvertToSampleCountFlags(gd.Capabilities.SupportedSampleCounts, fb.AttachmentSamples[i]),
                        AttachmentLoadOp.Load,
                        AttachmentStoreOp.Store,
                        AttachmentLoadOp.Load,
                        AttachmentStoreOp.Store,
                        ImageLayout.General,
                        ImageLayout.General);
                }

                int colorAttachmentsCount = fb.ColorAttachmentsCount;

                if (colorAttachmentsCount > MaxAttachments - 1)
                {
                    colorAttachmentsCount = MaxAttachments - 1;
                }

                if (colorAttachmentsCount != 0)
                {
                    int maxAttachmentIndex = fb.MaxColorAttachmentIndex;
                    subpass.ColorAttachmentCount = (uint)maxAttachmentIndex + 1;
                    subpass.PColorAttachments = &attachmentReferences[0];

                    // Fill with VK_ATTACHMENT_UNUSED to cover any gaps.
                    for (int i = 0; i <= maxAttachmentIndex; i++)
                    {
                        subpass.PColorAttachments[i] = new AttachmentReference(Vk.AttachmentUnused, ImageLayout.Undefined);
                    }

                    for (int i = 0; i < colorAttachmentsCount; i++)
                    {
                        int bindIndex = fb.AttachmentIndices[i];

                        subpass.PColorAttachments[bindIndex] = new AttachmentReference((uint)i, ImageLayout.General);
                    }
                }

                if (fb.HasDepthStencil)
                {
                    uint dsIndex = (uint)fb.AttachmentsCount - 1;

                    subpass.PDepthStencilAttachment = &attachmentReferences[MaxAttachments - 1];
                    *subpass.PDepthStencilAttachment = new AttachmentReference(dsIndex, ImageLayout.General);
                }
            }

            var subpassDependency = PipelineConverter.CreateSubpassDependency();

            fixed (AttachmentDescription* pAttachmentDescs = attachmentDescs)
            {
                var renderPassCreateInfo = new RenderPassCreateInfo
                {
                    SType = StructureType.RenderPassCreateInfo,
                    PAttachments = pAttachmentDescs,
                    AttachmentCount = attachmentDescs != null ? (uint)attachmentDescs.Length : 0,
                    PSubpasses = &subpass,
                    SubpassCount = 1,
                    PDependencies = &subpassDependency,
                    DependencyCount = 1,
                };

                gd.Api.CreateRenderPass(device, renderPassCreateInfo, null, out var renderPass).ThrowOnError();

                _renderPass = new Auto<DisposableRenderPass>(new DisposableRenderPass(gd.Api, device, renderPass));
            }

            _framebuffers = new HashTableSlim<FramebufferCacheKey, Auto<DisposableFramebuffer>>();

            // Register this render pass with all render target views.

            var textures = fb.GetAttachmentViews();

            foreach (var texture in textures)
            {
                texture.AddRenderPass(key, this);
            }

            _textures = textures;
            _key = key;
        }

        public Auto<DisposableFramebuffer> GetFramebuffer(VulkanRenderer gd, CommandBufferScoped cbs, FramebufferParams fb)
        {
            var key = new FramebufferCacheKey(fb.Width, fb.Height, fb.Layers);

            if (!_framebuffers.TryGetValue(ref key, out Auto<DisposableFramebuffer> result))
            {
                result = fb.Create(gd.Api, cbs, _renderPass);

                _framebuffers.Add(ref key, result);
            }

            return result;
        }

        public Auto<DisposableRenderPass> GetRenderPass()
        {
            return _renderPass;
        }

        public void Dispose()
        {
            // Dispose all framebuffers.

            foreach (var fb in _framebuffers.Values)
            {
                fb.Dispose();
            }

            // Notify all texture views that this render pass has been disposed.

            foreach (var texture in _textures)
            {
                texture.RemoveRenderPass(_key);
            }

            // Dispose render pass.

            _renderPass.Dispose();
        }
    }
}
