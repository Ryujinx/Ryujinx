using Ryujinx.Common.Memory;
using Silk.NET.Vulkan;
using System;
using System.Numerics;

namespace Ryujinx.Graphics.Vulkan
{
    struct PipelineState : IDisposable
    {
        private const int RequiredSubgroupSize = 32;

        public PipelineUid Internal;

        public float LineWidth
        {
            readonly get => BitConverter.Int32BitsToSingle((int)((Internal.Id0 >> 0) & 0xFFFFFFFF));
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFFF00000000) | ((ulong)(uint)BitConverter.SingleToInt32Bits(value) << 0);
        }

        public float DepthBiasClamp
        {
            readonly get => BitConverter.Int32BitsToSingle((int)((Internal.Id0 >> 32) & 0xFFFFFFFF));
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFFF) | ((ulong)(uint)BitConverter.SingleToInt32Bits(value) << 32);
        }

        public float DepthBiasConstantFactor
        {
            readonly get => BitConverter.Int32BitsToSingle((int)((Internal.Id1 >> 0) & 0xFFFFFFFF));
            set => Internal.Id1 = (Internal.Id1 & 0xFFFFFFFF00000000) | ((ulong)(uint)BitConverter.SingleToInt32Bits(value) << 0);
        }

        public float DepthBiasSlopeFactor
        {
            readonly get => BitConverter.Int32BitsToSingle((int)((Internal.Id1 >> 32) & 0xFFFFFFFF));
            set => Internal.Id1 = (Internal.Id1 & 0xFFFFFFFF) | ((ulong)(uint)BitConverter.SingleToInt32Bits(value) << 32);
        }

        public uint StencilFrontCompareMask
        {
            readonly get => (uint)((Internal.Id2 >> 0) & 0xFFFFFFFF);
            set => Internal.Id2 = (Internal.Id2 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public uint StencilFrontWriteMask
        {
            readonly get => (uint)((Internal.Id2 >> 32) & 0xFFFFFFFF);
            set => Internal.Id2 = (Internal.Id2 & 0xFFFFFFFF) | ((ulong)value << 32);
        }

        public uint StencilFrontReference
        {
            readonly get => (uint)((Internal.Id3 >> 0) & 0xFFFFFFFF);
            set => Internal.Id3 = (Internal.Id3 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public uint StencilBackCompareMask
        {
            readonly get => (uint)((Internal.Id3 >> 32) & 0xFFFFFFFF);
            set => Internal.Id3 = (Internal.Id3 & 0xFFFFFFFF) | ((ulong)value << 32);
        }

        public uint StencilBackWriteMask
        {
            readonly get => (uint)((Internal.Id4 >> 0) & 0xFFFFFFFF);
            set => Internal.Id4 = (Internal.Id4 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public uint StencilBackReference
        {
            readonly get => (uint)((Internal.Id4 >> 32) & 0xFFFFFFFF);
            set => Internal.Id4 = (Internal.Id4 & 0xFFFFFFFF) | ((ulong)value << 32);
        }

        public float MinDepthBounds
        {
            readonly get => BitConverter.Int32BitsToSingle((int)((Internal.Id5 >> 0) & 0xFFFFFFFF));
            set => Internal.Id5 = (Internal.Id5 & 0xFFFFFFFF00000000) | ((ulong)(uint)BitConverter.SingleToInt32Bits(value) << 0);
        }

        public float MaxDepthBounds
        {
            readonly get => BitConverter.Int32BitsToSingle((int)((Internal.Id5 >> 32) & 0xFFFFFFFF));
            set => Internal.Id5 = (Internal.Id5 & 0xFFFFFFFF) | ((ulong)(uint)BitConverter.SingleToInt32Bits(value) << 32);
        }

        public PolygonMode PolygonMode
        {
            readonly get => (PolygonMode)((Internal.Id6 >> 0) & 0x3FFFFFFF);
            set => Internal.Id6 = (Internal.Id6 & 0xFFFFFFFFC0000000) | ((ulong)value << 0);
        }

        public uint StagesCount
        {
            readonly get => (byte)((Internal.Id6 >> 30) & 0xFF);
            set => Internal.Id6 = (Internal.Id6 & 0xFFFFFFC03FFFFFFF) | ((ulong)value << 30);
        }

        public uint VertexAttributeDescriptionsCount
        {
            readonly get => (byte)((Internal.Id6 >> 38) & 0xFF);
            set => Internal.Id6 = (Internal.Id6 & 0xFFFFC03FFFFFFFFF) | ((ulong)value << 38);
        }

        public uint VertexBindingDescriptionsCount
        {
            readonly get => (byte)((Internal.Id6 >> 46) & 0xFF);
            set => Internal.Id6 = (Internal.Id6 & 0xFFC03FFFFFFFFFFF) | ((ulong)value << 46);
        }

        public uint ViewportsCount
        {
            readonly get => (byte)((Internal.Id6 >> 54) & 0xFF);
            set => Internal.Id6 = (Internal.Id6 & 0xC03FFFFFFFFFFFFF) | ((ulong)value << 54);
        }

        public uint ScissorsCount
        {
            readonly get => (byte)((Internal.Id7 >> 0) & 0xFF);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFFFFFFFF00) | ((ulong)value << 0);
        }

        public uint ColorBlendAttachmentStateCount
        {
            readonly get => (byte)((Internal.Id7 >> 8) & 0xFF);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFFFFFF00FF) | ((ulong)value << 8);
        }

        public PrimitiveTopology Topology
        {
            readonly get => (PrimitiveTopology)((Internal.Id7 >> 16) & 0xF);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFFFFF0FFFF) | ((ulong)value << 16);
        }

        public LogicOp LogicOp
        {
            readonly get => (LogicOp)((Internal.Id7 >> 20) & 0xF);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFFFF0FFFFF) | ((ulong)value << 20);
        }

        public CompareOp DepthCompareOp
        {
            readonly get => (CompareOp)((Internal.Id7 >> 24) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFFF8FFFFFF) | ((ulong)value << 24);
        }

        public StencilOp StencilFrontFailOp
        {
            readonly get => (StencilOp)((Internal.Id7 >> 27) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFFC7FFFFFF) | ((ulong)value << 27);
        }

        public StencilOp StencilFrontPassOp
        {
            readonly get => (StencilOp)((Internal.Id7 >> 30) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFFE3FFFFFFF) | ((ulong)value << 30);
        }

        public StencilOp StencilFrontDepthFailOp
        {
            readonly get => (StencilOp)((Internal.Id7 >> 33) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFFF1FFFFFFFF) | ((ulong)value << 33);
        }

        public CompareOp StencilFrontCompareOp
        {
            readonly get => (CompareOp)((Internal.Id7 >> 36) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFF8FFFFFFFFF) | ((ulong)value << 36);
        }

        public StencilOp StencilBackFailOp
        {
            readonly get => (StencilOp)((Internal.Id7 >> 39) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFFC7FFFFFFFFF) | ((ulong)value << 39);
        }

        public StencilOp StencilBackPassOp
        {
            readonly get => (StencilOp)((Internal.Id7 >> 42) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFFE3FFFFFFFFFF) | ((ulong)value << 42);
        }

        public StencilOp StencilBackDepthFailOp
        {
            readonly get => (StencilOp)((Internal.Id7 >> 45) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFFF1FFFFFFFFFFF) | ((ulong)value << 45);
        }

        public CompareOp StencilBackCompareOp
        {
            readonly get => (CompareOp)((Internal.Id7 >> 48) & 0x7);
            set => Internal.Id7 = (Internal.Id7 & 0xFFF8FFFFFFFFFFFF) | ((ulong)value << 48);
        }

        public CullModeFlags CullMode
        {
            readonly get => (CullModeFlags)((Internal.Id7 >> 51) & 0x3);
            set => Internal.Id7 = (Internal.Id7 & 0xFFE7FFFFFFFFFFFF) | ((ulong)value << 51);
        }

        public bool PrimitiveRestartEnable
        {
            readonly get => ((Internal.Id7 >> 53) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xFFDFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 53);
        }

        public bool DepthClampEnable
        {
            readonly get => ((Internal.Id7 >> 54) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xFFBFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 54);
        }

        public bool RasterizerDiscardEnable
        {
            readonly get => ((Internal.Id7 >> 55) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xFF7FFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 55);
        }

        public FrontFace FrontFace
        {
            readonly get => (FrontFace)((Internal.Id7 >> 56) & 0x1);
            set => Internal.Id7 = (Internal.Id7 & 0xFEFFFFFFFFFFFFFF) | ((ulong)value << 56);
        }

        public bool DepthBiasEnable
        {
            readonly get => ((Internal.Id7 >> 57) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xFDFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 57);
        }

        public bool DepthTestEnable
        {
            readonly get => ((Internal.Id7 >> 58) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xFBFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 58);
        }

        public bool DepthWriteEnable
        {
            readonly get => ((Internal.Id7 >> 59) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xF7FFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 59);
        }

        public bool DepthBoundsTestEnable
        {
            readonly get => ((Internal.Id7 >> 60) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xEFFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 60);
        }

        public bool StencilTestEnable
        {
            readonly get => ((Internal.Id7 >> 61) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xDFFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 61);
        }

        public bool LogicOpEnable
        {
            readonly get => ((Internal.Id7 >> 62) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0xBFFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 62);
        }

        public bool HasDepthStencil
        {
            readonly get => ((Internal.Id7 >> 63) & 0x1) != 0UL;
            set => Internal.Id7 = (Internal.Id7 & 0x7FFFFFFFFFFFFFFF) | ((value ? 1UL : 0UL) << 63);
        }

        public uint PatchControlPoints
        {
            readonly get => (uint)((Internal.Id8 >> 0) & 0xFFFFFFFF);
            set => Internal.Id8 = (Internal.Id8 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public uint SamplesCount
        {
            readonly get => (uint)((Internal.Id8 >> 32) & 0xFFFFFFFF);
            set => Internal.Id8 = (Internal.Id8 & 0xFFFFFFFF) | ((ulong)value << 32);
        }

        public bool AlphaToCoverageEnable
        {
            readonly get => ((Internal.Id9 >> 0) & 0x1) != 0UL;
            set => Internal.Id9 = (Internal.Id9 & 0xFFFFFFFFFFFFFFFE) | ((value ? 1UL : 0UL) << 0);
        }

        public bool AlphaToOneEnable
        {
            readonly get => ((Internal.Id9 >> 1) & 0x1) != 0UL;
            set => Internal.Id9 = (Internal.Id9 & 0xFFFFFFFFFFFFFFFD) | ((value ? 1UL : 0UL) << 1);
        }

        public bool AdvancedBlendSrcPreMultiplied
        {
            readonly get => ((Internal.Id9 >> 2) & 0x1) != 0UL;
            set => Internal.Id9 = (Internal.Id9 & 0xFFFFFFFFFFFFFFFB) | ((value ? 1UL : 0UL) << 2);
        }

        public bool AdvancedBlendDstPreMultiplied
        {
            readonly get => ((Internal.Id9 >> 3) & 0x1) != 0UL;
            set => Internal.Id9 = (Internal.Id9 & 0xFFFFFFFFFFFFFFF7) | ((value ? 1UL : 0UL) << 3);
        }

        public BlendOverlapEXT AdvancedBlendOverlap
        {
            readonly get => (BlendOverlapEXT)((Internal.Id9 >> 4) & 0x3);
            set => Internal.Id9 = (Internal.Id9 & 0xFFFFFFFFFFFFFFCF) | ((ulong)value << 4);
        }

        public bool DepthMode
        {
            readonly get => ((Internal.Id9 >> 6) & 0x1) != 0UL;
            set => Internal.Id9 = (Internal.Id9 & 0xFFFFFFFFFFFFFFBF) | ((value ? 1UL : 0UL) << 6);
        }

        public bool HasTessellationControlShader;
        public NativeArray<PipelineShaderStageCreateInfo> Stages;
        public PipelineLayout PipelineLayout;
        public SpecData SpecializationData;

        private Array32<VertexInputAttributeDescription> _vertexAttributeDescriptions2;

        public void Initialize()
        {
            HasTessellationControlShader = false;
            Stages = new NativeArray<PipelineShaderStageCreateInfo>(Constants.MaxShaderStages);

            AdvancedBlendSrcPreMultiplied = true;
            AdvancedBlendDstPreMultiplied = true;
            AdvancedBlendOverlap = BlendOverlapEXT.UncorrelatedExt;

            LineWidth = 1f;
            SamplesCount = 1;
            DepthMode = true;
        }

        public unsafe Auto<DisposablePipeline> CreateComputePipeline(
            VulkanRenderer gd,
            Device device,
            ShaderCollection program,
            PipelineCache cache)
        {
            if (program.TryGetComputePipeline(ref SpecializationData, out var pipeline))
            {
                return pipeline;
            }

            var pipelineCreateInfo = new ComputePipelineCreateInfo
            {
                SType = StructureType.ComputePipelineCreateInfo,
                Stage = Stages[0],
                BasePipelineIndex = -1,
                Layout = PipelineLayout,
            };

            Pipeline pipelineHandle = default;

            bool hasSpec = program.SpecDescriptions != null;

            var desc = hasSpec ? program.SpecDescriptions[0] : SpecDescription.Empty;

            if (hasSpec && SpecializationData.Length < (int)desc.Info.DataSize)
            {
                throw new InvalidOperationException("Specialization data size does not match description");
            }

            fixed (SpecializationInfo* info = &desc.Info)
            fixed (SpecializationMapEntry* map = desc.Map)
            fixed (byte* data = SpecializationData.Span)
            {
                if (hasSpec)
                {
                    info->PMapEntries = map;
                    info->PData = data;
                    pipelineCreateInfo.Stage.PSpecializationInfo = info;
                }

                gd.Api.CreateComputePipelines(device, cache, 1, &pipelineCreateInfo, null, &pipelineHandle).ThrowOnError();
            }

            pipeline = new Auto<DisposablePipeline>(new DisposablePipeline(gd.Api, device, pipelineHandle));

            program.AddComputePipeline(ref SpecializationData, pipeline);

            return pipeline;
        }

        public unsafe Auto<DisposablePipeline> CreateGraphicsPipeline(
            VulkanRenderer gd,
            Device device,
            ShaderCollection program,
            PipelineCache cache,
            RenderPass renderPass,
            bool throwOnError = false)
        {
            if (program.TryGetGraphicsPipeline(ref Internal, out var pipeline))
            {
                return pipeline;
            }

            Pipeline pipelineHandle = default;

            bool isMoltenVk = gd.IsMoltenVk;

            if (isMoltenVk)
            {
                UpdateVertexAttributeDescriptions(gd);
            }

            fixed (VertexInputAttributeDescription* pVertexAttributeDescriptions = &Internal.VertexAttributeDescriptions[0])
            fixed (VertexInputAttributeDescription* pVertexAttributeDescriptions2 = &_vertexAttributeDescriptions2[0])
            fixed (VertexInputBindingDescription* pVertexBindingDescriptions = &Internal.VertexBindingDescriptions[0])
            fixed (Viewport* pViewports = &Internal.Viewports[0])
            fixed (Rect2D* pScissors = &Internal.Scissors[0])
            fixed (PipelineColorBlendAttachmentState* pColorBlendAttachmentState = &Internal.ColorBlendAttachmentState[0])
            {
                var vertexInputState = new PipelineVertexInputStateCreateInfo
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexAttributeDescriptionCount = VertexAttributeDescriptionsCount,
                    PVertexAttributeDescriptions = isMoltenVk ? pVertexAttributeDescriptions2 : pVertexAttributeDescriptions,
                    VertexBindingDescriptionCount = VertexBindingDescriptionsCount,
                    PVertexBindingDescriptions = pVertexBindingDescriptions,
                };

                // Using patches topology without a tessellation shader is invalid.
                // If we find such a case, return null pipeline to skip the draw.
                if (Topology == PrimitiveTopology.PatchList && !HasTessellationControlShader)
                {
                    program.AddGraphicsPipeline(ref Internal, null);

                    return null;
                }

                bool primitiveRestartEnable = PrimitiveRestartEnable;

                bool topologySupportsRestart;

                if (gd.Capabilities.SupportsPrimitiveTopologyListRestart)
                {
                    topologySupportsRestart = gd.Capabilities.SupportsPrimitiveTopologyPatchListRestart || Topology != PrimitiveTopology.PatchList;
                }
                else
                {
                    topologySupportsRestart = Topology == PrimitiveTopology.LineStrip ||
                                              Topology == PrimitiveTopology.TriangleStrip ||
                                              Topology == PrimitiveTopology.TriangleFan ||
                                              Topology == PrimitiveTopology.LineStripWithAdjacency ||
                                              Topology == PrimitiveTopology.TriangleStripWithAdjacency;
                }

                primitiveRestartEnable &= topologySupportsRestart;

                var inputAssemblyState = new PipelineInputAssemblyStateCreateInfo
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    PrimitiveRestartEnable = primitiveRestartEnable,
                    Topology = Topology,
                };

                var tessellationState = new PipelineTessellationStateCreateInfo
                {
                    SType = StructureType.PipelineTessellationStateCreateInfo,
                    PatchControlPoints = PatchControlPoints,
                };

                var rasterizationState = new PipelineRasterizationStateCreateInfo
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = DepthClampEnable,
                    RasterizerDiscardEnable = RasterizerDiscardEnable,
                    PolygonMode = PolygonMode,
                    LineWidth = LineWidth,
                    CullMode = CullMode,
                    FrontFace = FrontFace,
                    DepthBiasEnable = DepthBiasEnable,
                    DepthBiasClamp = DepthBiasClamp,
                    DepthBiasConstantFactor = DepthBiasConstantFactor,
                    DepthBiasSlopeFactor = DepthBiasSlopeFactor,
                };

                var viewportState = new PipelineViewportStateCreateInfo
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = ViewportsCount,
                    PViewports = pViewports,
                    ScissorCount = ScissorsCount,
                    PScissors = pScissors,
                };

                if (gd.Capabilities.SupportsDepthClipControl)
                {
                    var viewportDepthClipControlState = new PipelineViewportDepthClipControlCreateInfoEXT
                    {
                        SType = StructureType.PipelineViewportDepthClipControlCreateInfoExt,
                        NegativeOneToOne = DepthMode,
                    };

                    viewportState.PNext = &viewportDepthClipControlState;
                }

                var multisampleState = new PipelineMultisampleStateCreateInfo
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = false,
                    RasterizationSamples = TextureStorage.ConvertToSampleCountFlags(gd.Capabilities.SupportedSampleCounts, SamplesCount),
                    MinSampleShading = 1,
                    AlphaToCoverageEnable = AlphaToCoverageEnable,
                    AlphaToOneEnable = AlphaToOneEnable,
                };

                var stencilFront = new StencilOpState(
                    StencilFrontFailOp,
                    StencilFrontPassOp,
                    StencilFrontDepthFailOp,
                    StencilFrontCompareOp,
                    StencilFrontCompareMask,
                    StencilFrontWriteMask,
                    StencilFrontReference);

                var stencilBack = new StencilOpState(
                    StencilBackFailOp,
                    StencilBackPassOp,
                    StencilBackDepthFailOp,
                    StencilBackCompareOp,
                    StencilBackCompareMask,
                    StencilBackWriteMask,
                    StencilBackReference);

                var depthStencilState = new PipelineDepthStencilStateCreateInfo
                {
                    SType = StructureType.PipelineDepthStencilStateCreateInfo,
                    DepthTestEnable = DepthTestEnable,
                    DepthWriteEnable = DepthWriteEnable,
                    DepthCompareOp = DepthCompareOp,
                    DepthBoundsTestEnable = DepthBoundsTestEnable,
                    StencilTestEnable = StencilTestEnable,
                    Front = stencilFront,
                    Back = stencilBack,
                    MinDepthBounds = MinDepthBounds,
                    MaxDepthBounds = MaxDepthBounds,
                };

                uint blendEnables = 0;

                if (gd.IsMoltenVk && Internal.AttachmentIntegerFormatMask != 0)
                {
                    // Blend can't be enabled for integer formats, so let's make sure it is disabled.
                    uint attachmentIntegerFormatMask = Internal.AttachmentIntegerFormatMask;

                    while (attachmentIntegerFormatMask != 0)
                    {
                        int i = BitOperations.TrailingZeroCount(attachmentIntegerFormatMask);

                        if (Internal.ColorBlendAttachmentState[i].BlendEnable)
                        {
                            blendEnables |= 1u << i;
                        }

                        Internal.ColorBlendAttachmentState[i].BlendEnable = false;
                        attachmentIntegerFormatMask &= ~(1u << i);
                    }
                }

                var colorBlendState = new PipelineColorBlendStateCreateInfo
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = LogicOpEnable,
                    LogicOp = LogicOp,
                    AttachmentCount = ColorBlendAttachmentStateCount,
                    PAttachments = pColorBlendAttachmentState,
                };

                PipelineColorBlendAdvancedStateCreateInfoEXT colorBlendAdvancedState;

                if (!AdvancedBlendSrcPreMultiplied ||
                    !AdvancedBlendDstPreMultiplied ||
                    AdvancedBlendOverlap != BlendOverlapEXT.UncorrelatedExt)
                {
                    colorBlendAdvancedState = new PipelineColorBlendAdvancedStateCreateInfoEXT
                    {
                        SType = StructureType.PipelineColorBlendAdvancedStateCreateInfoExt,
                        SrcPremultiplied = AdvancedBlendSrcPreMultiplied,
                        DstPremultiplied = AdvancedBlendDstPreMultiplied,
                        BlendOverlap = AdvancedBlendOverlap,
                    };

                    colorBlendState.PNext = &colorBlendAdvancedState;
                }

                bool supportsExtDynamicState = gd.Capabilities.SupportsExtendedDynamicState;
                int dynamicStatesCount = supportsExtDynamicState ? 9 : 8;

                DynamicState* dynamicStates = stackalloc DynamicState[dynamicStatesCount];

                dynamicStates[0] = DynamicState.Viewport;
                dynamicStates[1] = DynamicState.Scissor;
                dynamicStates[2] = DynamicState.DepthBias;
                dynamicStates[3] = DynamicState.DepthBounds;
                dynamicStates[4] = DynamicState.StencilCompareMask;
                dynamicStates[5] = DynamicState.StencilWriteMask;
                dynamicStates[6] = DynamicState.StencilReference;
                dynamicStates[7] = DynamicState.BlendConstants;

                if (supportsExtDynamicState)
                {
                    dynamicStates[8] = DynamicState.VertexInputBindingStrideExt;
                }

                var pipelineDynamicStateCreateInfo = new PipelineDynamicStateCreateInfo
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = (uint)dynamicStatesCount,
                    PDynamicStates = dynamicStates,
                };

                var pipelineCreateInfo = new GraphicsPipelineCreateInfo
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = StagesCount,
                    PStages = Stages.Pointer,
                    PVertexInputState = &vertexInputState,
                    PInputAssemblyState = &inputAssemblyState,
                    PTessellationState = &tessellationState,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizationState,
                    PMultisampleState = &multisampleState,
                    PDepthStencilState = &depthStencilState,
                    PColorBlendState = &colorBlendState,
                    PDynamicState = &pipelineDynamicStateCreateInfo,
                    Layout = PipelineLayout,
                    RenderPass = renderPass,
                    BasePipelineIndex = -1,
                };

                Result result = gd.Api.CreateGraphicsPipelines(device, cache, 1, &pipelineCreateInfo, null, &pipelineHandle);

                if (throwOnError)
                {
                    result.ThrowOnError();
                }
                else if (result.IsError())
                {
                    program.AddGraphicsPipeline(ref Internal, null);

                    return null;
                }

                // Restore previous blend enable values if we changed it.
                while (blendEnables != 0)
                {
                    int i = BitOperations.TrailingZeroCount(blendEnables);

                    Internal.ColorBlendAttachmentState[i].BlendEnable = true;
                    blendEnables &= ~(1u << i);
                }
            }

            pipeline = new Auto<DisposablePipeline>(new DisposablePipeline(gd.Api, device, pipelineHandle));

            program.AddGraphicsPipeline(ref Internal, pipeline);

            return pipeline;
        }

        private void UpdateVertexAttributeDescriptions(VulkanRenderer gd)
        {
            // Vertex attributes exceeding the stride are invalid.
            // In metal, they cause glitches with the vertex shader fetching incorrect values.
            // To work around this, we reduce the format to something that doesn't exceed the stride if possible.
            // The assumption is that the exceeding components are not actually accessed on the shader.

            for (int index = 0; index < VertexAttributeDescriptionsCount; index++)
            {
                var attribute = Internal.VertexAttributeDescriptions[index];
                int vbIndex = GetVertexBufferIndex(attribute.Binding);

                if (vbIndex >= 0)
                {
                    ref var vb = ref Internal.VertexBindingDescriptions[vbIndex];

                    Format format = attribute.Format;

                    while (vb.Stride != 0 && attribute.Offset + FormatTable.GetAttributeFormatSize(format) > vb.Stride)
                    {
                        Format newFormat = FormatTable.DropLastComponent(format);

                        if (newFormat == format)
                        {
                            // That case means we failed to find a format that fits within the stride,
                            // so just restore the original format and give up.
                            format = attribute.Format;
                            break;
                        }

                        format = newFormat;
                    }

                    if (attribute.Format != format && gd.FormatCapabilities.BufferFormatSupports(FormatFeatureFlags.VertexBufferBit, format))
                    {
                        attribute.Format = format;
                    }
                }

                _vertexAttributeDescriptions2[index] = attribute;
            }
        }

        private int GetVertexBufferIndex(uint binding)
        {
            for (int index = 0; index < VertexBindingDescriptionsCount; index++)
            {
                if (Internal.VertexBindingDescriptions[index].Binding == binding)
                {
                    return index;
                }
            }

            return -1;
        }

        public readonly void Dispose()
        {
            Stages.Dispose();
        }
    }
}
