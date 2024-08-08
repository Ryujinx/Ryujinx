using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using Format = Silk.NET.Vulkan.Format;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;

namespace Ryujinx.Graphics.Vulkan
{
    static class PipelineConverter
    {
        public static unsafe DisposableRenderPass ToRenderPass(this ProgramPipelineState state, VulkanRenderer gd, Device device)
        {
            const int MaxAttachments = Constants.MaxRenderTargets + 1;

            AttachmentDescription[] attachmentDescs = null;

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
            };

            AttachmentReference* attachmentReferences = stackalloc AttachmentReference[MaxAttachments];

            Span<int> attachmentIndices = stackalloc int[MaxAttachments];
            Span<Format> attachmentFormats = stackalloc Format[MaxAttachments];

            int attachmentCount = 0;
            int colorCount = 0;
            int maxColorAttachmentIndex = -1;

            for (int i = 0; i < state.AttachmentEnable.Length; i++)
            {
                if (state.AttachmentEnable[i])
                {
                    attachmentFormats[attachmentCount] = gd.FormatCapabilities.ConvertToVkFormat(state.AttachmentFormats[i]);

                    attachmentIndices[attachmentCount++] = i;
                    colorCount++;
                    maxColorAttachmentIndex = i;
                }
            }

            if (state.DepthStencilEnable)
            {
                attachmentFormats[attachmentCount++] = gd.FormatCapabilities.ConvertToVkFormat(state.DepthStencilFormat);
            }

            if (attachmentCount != 0)
            {
                attachmentDescs = new AttachmentDescription[attachmentCount];

                for (int i = 0; i < attachmentCount; i++)
                {
                    int bindIndex = attachmentIndices[i];

                    attachmentDescs[i] = new AttachmentDescription(
                        0,
                        attachmentFormats[i],
                        TextureStorage.ConvertToSampleCountFlags(gd.Capabilities.SupportedSampleCounts, (uint)state.SamplesCount),
                        AttachmentLoadOp.Load,
                        AttachmentStoreOp.Store,
                        AttachmentLoadOp.Load,
                        AttachmentStoreOp.Store,
                        ImageLayout.General,
                        ImageLayout.General);
                }

                int colorAttachmentsCount = colorCount;

                if (colorAttachmentsCount > MaxAttachments - 1)
                {
                    colorAttachmentsCount = MaxAttachments - 1;
                }

                if (colorAttachmentsCount != 0)
                {
                    subpass.ColorAttachmentCount = (uint)maxColorAttachmentIndex + 1;
                    subpass.PColorAttachments = &attachmentReferences[0];

                    // Fill with VK_ATTACHMENT_UNUSED to cover any gaps.
                    for (int i = 0; i <= maxColorAttachmentIndex; i++)
                    {
                        subpass.PColorAttachments[i] = new AttachmentReference(Vk.AttachmentUnused, ImageLayout.Undefined);
                    }

                    for (int i = 0; i < colorAttachmentsCount; i++)
                    {
                        int bindIndex = attachmentIndices[i];

                        subpass.PColorAttachments[bindIndex] = new AttachmentReference((uint)i, ImageLayout.General);
                    }
                }

                if (state.DepthStencilEnable)
                {
                    uint dsIndex = (uint)attachmentCount - 1;

                    subpass.PDepthStencilAttachment = &attachmentReferences[MaxAttachments - 1];
                    *subpass.PDepthStencilAttachment = new AttachmentReference(dsIndex, ImageLayout.General);
                }
            }

            var subpassDependency = CreateSubpassDependency(gd);

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

                return new DisposableRenderPass(gd.Api, device, renderPass);
            }
        }

        public static SubpassDependency CreateSubpassDependency(VulkanRenderer gd)
        {
            var (access, stages) = BarrierBatch.GetSubpassAccessSuperset(gd);

            return new SubpassDependency(
                0,
                0,
                stages,
                stages,
                access,
                access,
                0);
        }

        public unsafe static SubpassDependency2 CreateSubpassDependency2(VulkanRenderer gd)
        {
            var (access, stages) = BarrierBatch.GetSubpassAccessSuperset(gd);

            return new SubpassDependency2(
                StructureType.SubpassDependency2,
                null,
                0,
                0,
                stages,
                stages,
                access,
                access,
                0);
        }

        public static PipelineState ToVulkanPipelineState(this ProgramPipelineState state, VulkanRenderer gd)
        {
            PipelineState pipeline = new();
            pipeline.Initialize();

            // It is assumed that Dynamic State is enabled when this conversion is used.
            pipeline.DepthBoundsTestEnable = false; // Not implemented.

            pipeline.DepthClampEnable = state.DepthClampEnable;

            pipeline.DepthMode = state.DepthMode == DepthMode.MinusOneToOne;

            pipeline.HasDepthStencil = state.DepthStencilEnable;
            pipeline.LogicOpEnable = state.LogicOpEnable;

            pipeline.PolygonMode = PolygonMode.Fill; // Not implemented.

            if (!gd.Capabilities.SupportsExtendedDynamicState2.ExtendedDynamicState2)
            {
                pipeline.PrimitiveRestartEnable = state.PrimitiveRestartEnable;
                pipeline.RasterizerDiscardEnable = state.RasterizerDiscard;
                pipeline.DepthBiasEnable = state.BiasEnable != 0;
            }

            if (!gd.Capabilities.SupportsExtendedDynamicState2.ExtendedDynamicState2LogicOp)
            {
                pipeline.LogicOp = state.LogicOpEnable ? state.LogicOp.Convert() : default;
            }

            if (!gd.Capabilities.SupportsExtendedDynamicState2.ExtendedDynamicState2PatchControlPoints)
            {
                pipeline.PatchControlPoints = state.PatchControlPoints;
            }

            pipeline.SamplesCount = (uint)state.SamplesCount;

            // Stencil masks and ref are dynamic, so are 0 in the Vulkan pipeline.
            if (!gd.Capabilities.SupportsExtendedDynamicState)
            {
                pipeline.DepthTestEnable = state.DepthTest.TestEnable;
                pipeline.DepthWriteEnable = state.DepthTest.WriteEnable && state.DepthTest.TestEnable;

                pipeline.DepthCompareOp = state.DepthTest.TestEnable ? state.DepthTest.Func.Convert() : default;

                pipeline.CullMode = state.CullEnable ? state.CullMode.Convert() : CullModeFlags.None;

                pipeline.FrontFace = state.FrontFace.Convert();

                if (gd.Capabilities.SupportsMultiView)
                {
                    pipeline.ScissorsCount = Constants.MaxViewports;
                    pipeline.ViewportsCount = Constants.MaxViewports;
                }
                else
                {
                    pipeline.ScissorsCount = 1;
                    pipeline.ViewportsCount = 1;
                }

                pipeline.StencilFrontFailOp = state.StencilTest.FrontSFail.Convert();
                pipeline.StencilFrontPassOp = state.StencilTest.FrontDpPass.Convert();
                pipeline.StencilFrontDepthFailOp = state.StencilTest.FrontDpFail.Convert();
                pipeline.StencilFrontCompareOp = state.StencilTest.FrontFunc.Convert();

                pipeline.StencilBackFailOp = state.StencilTest.BackSFail.Convert();
                pipeline.StencilBackPassOp = state.StencilTest.BackDpPass.Convert();
                pipeline.StencilBackDepthFailOp = state.StencilTest.BackDpFail.Convert();
                pipeline.StencilBackCompareOp = state.StencilTest.BackFunc.Convert();

                pipeline.StencilTestEnable = state.StencilTest.TestEnable;
            }

            pipeline.Topology = gd.TopologyRemap(state.Topology).Convert();

            int vaCount = Math.Min(Constants.MaxVertexAttributes, state.VertexAttribCount);
            int vbCount = Math.Min(Constants.MaxVertexBuffers, state.VertexBufferCount);

            Span<int> vbScalarSizes = stackalloc int[vbCount];

            for (int i = 0; i < vaCount; i++)
            {
                var attribute = state.VertexAttribs[i];
                var bufferIndex = attribute.IsZero ? 0 : attribute.BufferIndex + 1;

                pipeline.Internal.VertexAttributeDescriptions[i] = new VertexInputAttributeDescription(
                    (uint)i,
                    (uint)bufferIndex,
                    gd.FormatCapabilities.ConvertToVertexVkFormat(attribute.Format),
                    (uint)attribute.Offset);

                if (!attribute.IsZero && bufferIndex < vbCount)
                {
                    vbScalarSizes[bufferIndex - 1] = Math.Max(attribute.Format.GetScalarSize(), vbScalarSizes[bufferIndex - 1]);
                }
            }

            int descriptorIndex = 1;
            pipeline.Internal.VertexBindingDescriptions[0] = new VertexInputBindingDescription(0, 0, VertexInputRate.Vertex);

            for (int i = 0; i < vbCount; i++)
            {
                var vertexBuffer = state.VertexBuffers[i];

                if (vertexBuffer.Enable)
                {
                    var inputRate = vertexBuffer.Divisor != 0 ? VertexInputRate.Instance : VertexInputRate.Vertex;

                    int alignedStride = vertexBuffer.Stride;

                    if (gd.NeedsVertexBufferAlignment(vbScalarSizes[i], out int alignment))
                    {
                        alignedStride = BitUtils.AlignUp(vertexBuffer.Stride, alignment);
                    }

                    // TODO: Support divisor > 1
                    pipeline.Internal.VertexBindingDescriptions[descriptorIndex++] = new VertexInputBindingDescription(
                        (uint)i + 1,
                        (uint)alignedStride,
                        inputRate);
                }
            }

            pipeline.VertexBindingDescriptionsCount = (uint)descriptorIndex;

            // NOTE: Viewports, Scissors are dynamic.

            for (int i = 0; i < Constants.MaxRenderTargets; i++)
            {
                var blend = state.BlendDescriptors[i];

                if (blend.Enable && state.ColorWriteMask[i] != 0)
                {
                    pipeline.Internal.ColorBlendAttachmentState[i] = new PipelineColorBlendAttachmentState(
                        blend.Enable,
                        blend.ColorSrcFactor.Convert(),
                        blend.ColorDstFactor.Convert(),
                        blend.ColorOp.Convert(),
                        blend.AlphaSrcFactor.Convert(),
                        blend.AlphaDstFactor.Convert(),
                        blend.AlphaOp.Convert(),
                        (ColorComponentFlags)state.ColorWriteMask[i]);
                }
                else
                {
                    pipeline.Internal.ColorBlendAttachmentState[i] = new PipelineColorBlendAttachmentState(
                        colorWriteMask: (ColorComponentFlags)state.ColorWriteMask[i]);
                }
            }

            int attachmentCount = 0;
            int maxColorAttachmentIndex = -1;
            uint attachmentIntegerFormatMask = 0;
            bool allFormatsFloatOrSrgb = true;

            for (int i = 0; i < Constants.MaxRenderTargets; i++)
            {
                if (state.AttachmentEnable[i])
                {
                    pipeline.Internal.AttachmentFormats[attachmentCount++] = gd.FormatCapabilities.ConvertToVkFormat(state.AttachmentFormats[i]);
                    maxColorAttachmentIndex = i;

                    if (state.AttachmentFormats[i].IsInteger())
                    {
                        attachmentIntegerFormatMask |= 1u << i;
                    }

                    allFormatsFloatOrSrgb &= state.AttachmentFormats[i].IsFloatOrSrgb();
                }
            }

            if (state.DepthStencilEnable)
            {
                pipeline.Internal.AttachmentFormats[attachmentCount++] = gd.FormatCapabilities.ConvertToVkFormat(state.DepthStencilFormat);
            }

            pipeline.ColorBlendAttachmentStateCount = (uint)(maxColorAttachmentIndex + 1);
            pipeline.VertexAttributeDescriptionsCount = (uint)Math.Min(Constants.MaxVertexAttributes, state.VertexAttribCount);
            pipeline.Internal.AttachmentIntegerFormatMask = attachmentIntegerFormatMask;
            pipeline.Internal.LogicOpsAllowed = attachmentCount == 0 || !allFormatsFloatOrSrgb;

            return pipeline;
        }
    }
}
