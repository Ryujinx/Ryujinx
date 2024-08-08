using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct PipelineState
    {
        public PipelineUid Internal;

        public uint StagesCount
        {
            readonly get => (byte)((Internal.Id0 >> 0) & 0xFF);
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFFFFFFFFF00) | ((ulong)value << 0);
        }

        public uint VertexAttributeDescriptionsCount
        {
            readonly get => (byte)((Internal.Id0 >> 8) & 0xFF);
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFFFFFFF00FF) | ((ulong)value << 8);
        }

        public uint VertexBindingDescriptionsCount
        {
            readonly get => (byte)((Internal.Id0 >> 16) & 0xFF);
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFFFFF00FFFF) | ((ulong)value << 16);
        }

        public uint ColorBlendAttachmentStateCount
        {
            readonly get => (byte)((Internal.Id0 >> 24) & 0xFF);
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFFF00FFFFFF) | ((ulong)value << 24);
        }

        /*
         * Can be an input to a pipeline, but not sure what the situation for that is.
        public PrimitiveTopology Topology
        {
            readonly get => (PrimitiveTopology)((Internal.Id6 >> 16) & 0xF);
            set => Internal.Id6 = (Internal.Id6 & 0xFFFFFFFFFFF0FFFF) | ((ulong)value << 16);
        }
        */

        // Reserved for when API is available.
        public int LogicOp
        {
            readonly get => (int)((Internal.Id0 >> 32) & 0xF);
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFF0FFFFFFFF) | ((ulong)value << 32);
        }

        //?
        public bool PrimitiveRestartEnable
        {
            readonly get => ((Internal.Id0 >> 36) & 0x1) != 0UL;
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFEFFFFFFFFF) | ((value ? 1UL : 0UL) << 36);
        }

        public bool RasterizerDiscardEnable
        {
            readonly get => ((Internal.Id0 >> 37) & 0x1) != 0UL;
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFDFFFFFFFFF) | ((value ? 1UL : 0UL) << 37);
        }

        // Reserved for when API is available.
        public bool LogicOpEnable
        {
            readonly get => ((Internal.Id0 >> 38) & 0x1) != 0UL;
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFFBFFFFFFFFF) | ((value ? 1UL : 0UL) << 38);
        }

        public bool AlphaToCoverageEnable
        {
            readonly get => ((Internal.Id0 >> 40) & 0x1) != 0UL;
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFEFFFFFFFFFF) | ((value ? 1UL : 0UL) << 40);
        }

        public bool AlphaToOneEnable
        {
            readonly get => ((Internal.Id0 >> 41) & 0x1) != 0UL;
            set => Internal.Id0 = (Internal.Id0 & 0xFFFFFDFFFFFFFFFF) | ((value ? 1UL : 0UL) << 41);
        }

        public MTLPixelFormat DepthStencilFormat
        {
            readonly get => (MTLPixelFormat)(Internal.Id0 >> 48);
            set => Internal.Id0 = (Internal.Id0 & 0x0000FFFFFFFFFFFF) | ((ulong)value << 48);
        }

        // Not sure how to appropriately use this, but it does need to be passed for tess.
        public uint PatchControlPoints
        {
            readonly get => (uint)((Internal.Id1 >> 0) & 0xFFFFFFFF);
            set => Internal.Id1 = (Internal.Id1 & 0xFFFFFFFF00000000) | ((ulong)value << 0);
        }

        public uint SamplesCount
        {
            readonly get => (uint)((Internal.Id1 >> 32) & 0xFFFFFFFF);
            set => Internal.Id1 = (Internal.Id1 & 0xFFFFFFFF) | ((ulong)value << 32);
        }

        // Advanced blend not supported

        private struct RenderPipelineDescriptorResult : IDisposable
        {
            public MTLRenderPipelineDescriptor Pipeline;
            private MTLVertexDescriptor _vertex;

            public RenderPipelineDescriptorResult(MTLRenderPipelineDescriptor pipeline, MTLVertexDescriptor vertex)
            {
                Pipeline = pipeline;
                _vertex = vertex;
            }

            public void Dispose()
            {
                Pipeline.Dispose();
                _vertex.Dispose();
            }
        }

        private readonly void BuildColorAttachment(MTLRenderPipelineColorAttachmentDescriptor descriptor, ColorBlendStateUid blendState)
        {
            descriptor.PixelFormat = blendState.PixelFormat;
            descriptor.SetBlendingEnabled(blendState.Enable);
            descriptor.AlphaBlendOperation = blendState.AlphaBlendOperation;
            descriptor.RgbBlendOperation = blendState.RgbBlendOperation;
            descriptor.SourceAlphaBlendFactor = blendState.SourceAlphaBlendFactor;
            descriptor.DestinationAlphaBlendFactor = blendState.DestinationAlphaBlendFactor;
            descriptor.SourceRGBBlendFactor = blendState.SourceRGBBlendFactor;
            descriptor.DestinationRGBBlendFactor = blendState.DestinationRGBBlendFactor;
            descriptor.WriteMask = blendState.WriteMask;
        }

        private readonly MTLVertexDescriptor BuildVertexDescriptor()
        {
            var vertexDescriptor = new MTLVertexDescriptor();

            for (int i = 0; i < VertexAttributeDescriptionsCount; i++)
            {
                VertexInputAttributeUid uid = Internal.VertexAttributes[i];

                var attrib = vertexDescriptor.Attributes.Object((ulong)i);
                attrib.Format = uid.Format;
                attrib.Offset = uid.Offset;
                attrib.BufferIndex = uid.BufferIndex;
            }

            for (int i = 0; i < VertexBindingDescriptionsCount; i++)
            {
                VertexInputLayoutUid uid = Internal.VertexBindings[i];

                var layout = vertexDescriptor.Layouts.Object((ulong)i);

                layout.StepFunction = uid.StepFunction;
                layout.StepRate = uid.StepRate;
                layout.Stride = uid.Stride;
            }

            return vertexDescriptor;
        }

        private RenderPipelineDescriptorResult CreateRenderDescriptor(Program program)
        {
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor();

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                var blendState = Internal.ColorBlendState[i];

                if (blendState.PixelFormat != MTLPixelFormat.Invalid)
                {
                    var pipelineAttachment = renderPipelineDescriptor.ColorAttachments.Object((ulong)i);

                    BuildColorAttachment(pipelineAttachment, blendState);
                }
            }

            MTLPixelFormat dsFormat = DepthStencilFormat;
            if (dsFormat != MTLPixelFormat.Invalid)
            {
                switch (dsFormat)
                {
                    // Depth Only Attachment
                    case MTLPixelFormat.Depth16Unorm:
                    case MTLPixelFormat.Depth32Float:
                        renderPipelineDescriptor.DepthAttachmentPixelFormat = dsFormat;
                        break;

                    // Stencil Only Attachment
                    case MTLPixelFormat.Stencil8:
                        renderPipelineDescriptor.StencilAttachmentPixelFormat = dsFormat;
                        break;

                    // Combined Attachment
                    case MTLPixelFormat.Depth24UnormStencil8:
                    case MTLPixelFormat.Depth32FloatStencil8:
                        renderPipelineDescriptor.DepthAttachmentPixelFormat = dsFormat;
                        renderPipelineDescriptor.StencilAttachmentPixelFormat = dsFormat;
                        break;
                    default:
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"Unsupported Depth/Stencil Format: {dsFormat}!");
                        break;
                }
            }

            /* TODO: enable when sharpmetal fixes the bindings
            renderPipelineDescriptor.AlphaToCoverageEnabled = AlphaToCoverageEnable;
            renderPipelineDescriptor.AlphaToOneEnabled = AlphaToOneEnable;
            renderPipelineDescriptor.RasterizationEnabled = !RasterizerDiscardEnable;
            */

            renderPipelineDescriptor.SampleCount = Math.Max(1, SamplesCount);

            var vertexDescriptor = BuildVertexDescriptor();
            renderPipelineDescriptor.VertexDescriptor = vertexDescriptor;

            renderPipelineDescriptor.VertexFunction = program.VertexFunction;

            if (program.FragmentFunction.NativePtr != 0)
            {
                renderPipelineDescriptor.FragmentFunction = program.FragmentFunction;
            }

            return new RenderPipelineDescriptorResult(renderPipelineDescriptor, vertexDescriptor);
        }

        public MTLRenderPipelineState CreateRenderPipeline(MTLDevice device, Program program)
        {
            if (program.TryGetGraphicsPipeline(ref Internal, out var pipelineState))
            {
                return pipelineState;
            }

            using RenderPipelineDescriptorResult descriptors = CreateRenderDescriptor(program);

            var error = new NSError(IntPtr.Zero);
            pipelineState = device.NewRenderPipelineState(descriptors.Pipeline, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Render Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }

            program.AddGraphicsPipeline(ref Internal, pipelineState);

            return pipelineState;
        }

        public static MTLComputePipelineDescriptor CreateComputeDescriptor(Program program)
        {
            ComputeSize localSize = program.ComputeLocalSize;

            uint maxThreads = (uint)(localSize.X * localSize.Y * localSize.Z);

            if (maxThreads == 0)
            {
                throw new InvalidOperationException($"Local thread size for compute cannot be 0 in any dimension.");
            }

            var descriptor = new MTLComputePipelineDescriptor
            {
                ComputeFunction = program.ComputeFunction,
                MaxTotalThreadsPerThreadgroup = maxThreads,
                ThreadGroupSizeIsMultipleOfThreadExecutionWidth = true,
            };

            return descriptor;
        }

        public static MTLComputePipelineState CreateComputePipeline(MTLDevice device, Program program)
        {
            if (program.TryGetComputePipeline(out var pipelineState))
            {
                return pipelineState;
            }

            using MTLComputePipelineDescriptor descriptor = CreateComputeDescriptor(program);

            var error = new NSError(IntPtr.Zero);
            pipelineState = device.NewComputePipelineState(descriptor, MTLPipelineOption.None, 0, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Compute Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }

            program.AddComputePipeline(pipelineState);

            return pipelineState;
        }

        public void Initialize()
        {
            SamplesCount = 1;

            Internal.ResetColorState();
        }

        /*
         * TODO, this is from vulkan.

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
        */
    }
}
