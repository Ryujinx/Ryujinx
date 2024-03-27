using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Silk.NET.Vulkan;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CompareOp = Ryujinx.Graphics.GAL.CompareOp;
using Format = Ryujinx.Graphics.GAL.Format;
using FrontFace = Ryujinx.Graphics.GAL.FrontFace;
using IndexType = Ryujinx.Graphics.GAL.IndexType;
using PolygonMode = Ryujinx.Graphics.GAL.PolygonMode;
using PrimitiveTopology = Ryujinx.Graphics.GAL.PrimitiveTopology;
using Viewport = Ryujinx.Graphics.GAL.Viewport;

namespace Ryujinx.Graphics.Vulkan
{
    class PipelineBase : IDisposable
    {
        public const int DescriptorSetLayouts = 4;

        public const int UniformSetIndex = 0;
        public const int StorageSetIndex = 1;
        public const int TextureSetIndex = 2;
        public const int ImageSetIndex = 3;

        protected readonly VulkanRenderer Gd;
        protected readonly Device Device;
        public readonly PipelineCache PipelineCache;

        public readonly AutoFlushCounter AutoFlush;
        public readonly Action EndRenderPassDelegate;

        protected PipelineDynamicState DynamicState;
        private PipelineState _newState;
        private bool _graphicsStateDirty;
        private bool _computeStateDirty;
        private bool _bindingBarriersDirty;
        private PrimitiveTopology _topology;

        private ulong _currentPipelineHandle;

        protected Auto<DisposablePipeline> Pipeline;

        protected PipelineBindPoint Pbp;

        protected CommandBufferScoped Cbs;
        protected CommandBufferScoped? PreloadCbs;
        protected CommandBuffer CommandBuffer;

        public CommandBufferScoped CurrentCommandBuffer => Cbs;

        private ShaderCollection _program;

        protected FramebufferParams FramebufferParams;
        private Auto<DisposableFramebuffer> _framebuffer;
        private Auto<DisposableRenderPass> _renderPass;
        private RenderPassHolder _nullRenderPass;
        private int _writtenAttachmentCount;

        private bool _framebufferUsingColorWriteMask;

        private ITexture[] _preMaskColors;
        private ITexture _preMaskDepthStencil;

        private readonly DescriptorSetUpdater _descriptorSetUpdater;

        private IndexBufferState _indexBuffer;
        private IndexBufferPattern _indexBufferPattern;
        private readonly BufferState[] _transformFeedbackBuffers;
        private readonly VertexBufferState[] _vertexBuffers;
        private ulong _vertexBuffersDirty;
        protected Rectangle<int> ClearScissor;

        private readonly VertexBufferUpdater _vertexBufferUpdater;

        public IndexBufferPattern QuadsToTrisPattern;
        public IndexBufferPattern TriFanToTrisPattern;

        private bool _needsIndexBufferRebind;
        private bool _needsTransformFeedbackBuffersRebind;

        private bool _tfEnabled;
        private bool _tfActive;

        private readonly PipelineColorBlendAttachmentState[] _storedBlend;

        private ulong _drawCountSinceBarrier;
        public ulong DrawCount { get; private set; }
        public bool RenderPassActive { get; private set; }

        public unsafe PipelineBase(VulkanRenderer gd, Device device)
        {
            Gd = gd;
            Device = device;

            AutoFlush = new AutoFlushCounter(gd);
            EndRenderPassDelegate = EndRenderPass;

            var pipelineCacheCreateInfo = new PipelineCacheCreateInfo
            {
                SType = StructureType.PipelineCacheCreateInfo,
            };

            gd.Api.CreatePipelineCache(device, pipelineCacheCreateInfo, null, out PipelineCache).ThrowOnError();

            _descriptorSetUpdater = new DescriptorSetUpdater(gd, device, this);
            _vertexBufferUpdater = new VertexBufferUpdater(gd);

            _transformFeedbackBuffers = new BufferState[Constants.MaxTransformFeedbackBuffers];
            _vertexBuffers = new VertexBufferState[Constants.MaxVertexBuffers + 1];

            const int EmptyVbSize = 16;

            using var emptyVb = gd.BufferManager.Create(gd, EmptyVbSize);
            emptyVb.SetData(0, new byte[EmptyVbSize]);
            _vertexBuffers[0] = new VertexBufferState(emptyVb.GetBuffer(), 0, 0, EmptyVbSize);
            _vertexBuffersDirty = ulong.MaxValue >> (64 - _vertexBuffers.Length);

            ClearScissor = new Rectangle<int>(0, 0, 0xffff, 0xffff);

            _storedBlend = new PipelineColorBlendAttachmentState[Constants.MaxRenderTargets];

            _newState.Initialize();
        }

        public void Initialize()
        {
            _descriptorSetUpdater.Initialize();

            QuadsToTrisPattern = new IndexBufferPattern(Gd, 4, 6, 0, new[] { 0, 1, 2, 0, 2, 3 }, 4, false);
            TriFanToTrisPattern = new IndexBufferPattern(Gd, 3, 3, 2, new[] { int.MinValue, -1, 0 }, 1, true);
        }

        public unsafe void Barrier()
        {
            if (_drawCountSinceBarrier != DrawCount)
            {
                _drawCountSinceBarrier = DrawCount;

                // Barriers are not supported inside a render pass on Apple GPUs.
                // As a workaround, end the render pass.
                if (Gd.Vendor == Vendor.Apple)
                {
                    EndRenderPass();
                }
            }

            MemoryBarrier memoryBarrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
                DstAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            };

            PipelineStageFlags pipelineStageFlags = PipelineStageFlags.VertexShaderBit | PipelineStageFlags.FragmentShaderBit;

            if (Gd.Capabilities.SupportsGeometryShader)
            {
                pipelineStageFlags |= PipelineStageFlags.GeometryShaderBit;
            }

            if (Gd.Capabilities.SupportsTessellationShader)
            {
                pipelineStageFlags |= PipelineStageFlags.TessellationControlShaderBit | PipelineStageFlags.TessellationEvaluationShaderBit;
            }

            Gd.Api.CmdPipelineBarrier(
                CommandBuffer,
                pipelineStageFlags,
                pipelineStageFlags,
                0,
                1,
                memoryBarrier,
                0,
                null,
                0,
                null);
        }

        public void ComputeBarrier()
        {
            MemoryBarrier memoryBarrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
                DstAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            };

            Gd.Api.CmdPipelineBarrier(
                CommandBuffer,
                PipelineStageFlags.ComputeShaderBit,
                PipelineStageFlags.AllCommandsBit,
                0,
                1,
                new ReadOnlySpan<MemoryBarrier>(in memoryBarrier),
                0,
                ReadOnlySpan<BufferMemoryBarrier>.Empty,
                0,
                ReadOnlySpan<ImageMemoryBarrier>.Empty);
        }

        public void BeginTransformFeedback(PrimitiveTopology topology)
        {
            _tfEnabled = true;
        }

        public void ClearBuffer(BufferHandle destination, int offset, int size, uint value)
        {
            EndRenderPass();

            var dst = Gd.BufferManager.GetBuffer(CommandBuffer, destination, offset, size, true).Get(Cbs, offset, size, true).Value;

            BufferHolder.InsertBufferBarrier(
                Gd,
                Cbs.CommandBuffer,
                dst,
                BufferHolder.DefaultAccessFlags,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.TransferBit,
                offset,
                size);

            Gd.Api.CmdFillBuffer(CommandBuffer, dst, (ulong)offset, (ulong)size, value);

            BufferHolder.InsertBufferBarrier(
                Gd,
                Cbs.CommandBuffer,
                dst,
                AccessFlags.TransferWriteBit,
                BufferHolder.DefaultAccessFlags,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.AllCommandsBit,
                offset,
                size);
        }

        public unsafe void ClearRenderTargetColor(int index, int layer, int layerCount, ColorF color)
        {
            if (FramebufferParams == null || !FramebufferParams.IsValidColorAttachment(index))
            {
                return;
            }

            if (_renderPass == null)
            {
                CreateRenderPass();
            }

            Gd.Barriers.Flush(Cbs.CommandBuffer, RenderPassActive, EndRenderPassDelegate);

            BeginRenderPass();

            var clearValue = new ClearValue(new ClearColorValue(color.Red, color.Green, color.Blue, color.Alpha));
            var attachment = new ClearAttachment(ImageAspectFlags.ColorBit, (uint)index, clearValue);
            var clearRect = FramebufferParams.GetClearRect(ClearScissor, layer, layerCount);

            Gd.Api.CmdClearAttachments(CommandBuffer, 1, &attachment, 1, &clearRect);
        }

        public unsafe void ClearRenderTargetDepthStencil(int layer, int layerCount, float depthValue, bool depthMask, int stencilValue, bool stencilMask)
        {
            if (FramebufferParams == null || !FramebufferParams.HasDepthStencil)
            {
                return;
            }

            var clearValue = new ClearValue(null, new ClearDepthStencilValue(depthValue, (uint)stencilValue));
            var flags = depthMask ? ImageAspectFlags.DepthBit : 0;

            if (stencilMask)
            {
                flags |= ImageAspectFlags.StencilBit;
            }

            flags &= FramebufferParams.GetDepthStencilAspectFlags();

            if (flags == ImageAspectFlags.None)
            {
                return;
            }

            if (_renderPass == null)
            {
                CreateRenderPass();
            }

            Gd.Barriers.Flush(Cbs.CommandBuffer, RenderPassActive, EndRenderPassDelegate);

            BeginRenderPass();

            var attachment = new ClearAttachment(flags, 0, clearValue);
            var clearRect = FramebufferParams.GetClearRect(ClearScissor, layer, layerCount);

            Gd.Api.CmdClearAttachments(CommandBuffer, 1, &attachment, 1, &clearRect);
        }

        public unsafe void CommandBufferBarrier()
        {
            MemoryBarrier memoryBarrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = BufferHolder.DefaultAccessFlags,
                DstAccessMask = AccessFlags.IndirectCommandReadBit,
            };

            Gd.Api.CmdPipelineBarrier(
                CommandBuffer,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.DrawIndirectBit,
                0,
                1,
                memoryBarrier,
                0,
                null,
                0,
                null);
        }

        public void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size)
        {
            EndRenderPass();

            var src = Gd.BufferManager.GetBuffer(CommandBuffer, source, srcOffset, size, false);
            var dst = Gd.BufferManager.GetBuffer(CommandBuffer, destination, dstOffset, size, true);

            BufferHolder.Copy(Gd, Cbs, src, dst, srcOffset, dstOffset, size);
        }

        public void DirtyVertexBuffer(Auto<DisposableBuffer> buffer)
        {
            for (int i = 0; i < _vertexBuffers.Length; i++)
            {
                if (_vertexBuffers[i].BoundEquals(buffer))
                {
                    _vertexBuffersDirty |= 1UL << i;
                }
            }
        }

        public void DirtyIndexBuffer(Auto<DisposableBuffer> buffer)
        {
            if (_indexBuffer.BoundEquals(buffer))
            {
                _needsIndexBufferRebind = true;
            }
        }

        public void DispatchCompute(int groupsX, int groupsY, int groupsZ)
        {
            if (!_program.IsLinked)
            {
                return;
            }

            EndRenderPass();
            RecreateComputePipelineIfNeeded();

            Gd.Api.CmdDispatch(CommandBuffer, (uint)groupsX, (uint)groupsY, (uint)groupsZ);
        }

        public void DispatchComputeIndirect(Auto<DisposableBuffer> indirectBuffer, int indirectBufferOffset)
        {
            if (!_program.IsLinked)
            {
                return;
            }

            EndRenderPass();
            RecreateComputePipelineIfNeeded();

            Gd.Api.CmdDispatchIndirect(CommandBuffer, indirectBuffer.Get(Cbs, indirectBufferOffset, 12).Value, (ulong)indirectBufferOffset);
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            if (vertexCount == 0)
            {
                return;
            }

            if (!RecreateGraphicsPipelineIfNeeded())
            {
                return;
            }

            BeginRenderPass();
            DrawCount++;

            if (Gd.TopologyUnsupported(_topology))
            {
                // Temporarily bind a conversion pattern as an index buffer.
                _needsIndexBufferRebind = true;

                IndexBufferPattern pattern = _topology switch
                {
                    PrimitiveTopology.Quads => QuadsToTrisPattern,
                    PrimitiveTopology.TriangleFan or
                    PrimitiveTopology.Polygon => TriFanToTrisPattern,
                    _ => throw new NotSupportedException($"Unsupported topology: {_topology}"),
                };

                BufferHandle handle = pattern.GetRepeatingBuffer(vertexCount, out int indexCount);
                var buffer = Gd.BufferManager.GetBuffer(CommandBuffer, handle, false);

                Gd.Api.CmdBindIndexBuffer(CommandBuffer, buffer.Get(Cbs, 0, indexCount * sizeof(int)).Value, 0, Silk.NET.Vulkan.IndexType.Uint32);

                BeginRenderPass(); // May have been interrupted to set buffer data.
                ResumeTransformFeedbackInternal();

                Gd.Api.CmdDrawIndexed(CommandBuffer, (uint)indexCount, (uint)instanceCount, 0, firstVertex, (uint)firstInstance);
            }
            else
            {
                ResumeTransformFeedbackInternal();

                Gd.Api.CmdDraw(CommandBuffer, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
            }
        }

        private void UpdateIndexBufferPattern()
        {
            IndexBufferPattern pattern = null;

            if (Gd.TopologyUnsupported(_topology))
            {
                pattern = _topology switch
                {
                    PrimitiveTopology.Quads => QuadsToTrisPattern,
                    PrimitiveTopology.TriangleFan or
                    PrimitiveTopology.Polygon => TriFanToTrisPattern,
                    _ => throw new NotSupportedException($"Unsupported topology: {_topology}"),
                };
            }

            if (_indexBufferPattern != pattern)
            {
                _indexBufferPattern = pattern;
                _needsIndexBufferRebind = true;
            }
        }

        public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int firstVertex, int firstInstance)
        {
            if (indexCount == 0)
            {
                return;
            }

            UpdateIndexBufferPattern();

            if (!RecreateGraphicsPipelineIfNeeded())
            {
                return;
            }

            BeginRenderPass();
            DrawCount++;

            if (_indexBufferPattern != null)
            {
                // Convert the index buffer into a supported topology.
                IndexBufferPattern pattern = _indexBufferPattern;

                int convertedCount = pattern.GetConvertedCount(indexCount);

                if (_needsIndexBufferRebind)
                {
                    _indexBuffer.BindConvertedIndexBuffer(Gd, Cbs, firstIndex, indexCount, convertedCount, pattern);

                    _needsIndexBufferRebind = false;
                }

                BeginRenderPass(); // May have been interrupted to set buffer data.
                ResumeTransformFeedbackInternal();

                Gd.Api.CmdDrawIndexed(CommandBuffer, (uint)convertedCount, (uint)instanceCount, 0, firstVertex, (uint)firstInstance);
            }
            else
            {
                ResumeTransformFeedbackInternal();

                Gd.Api.CmdDrawIndexed(CommandBuffer, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, firstVertex, (uint)firstInstance);
            }
        }

        public void DrawIndexedIndirect(BufferRange indirectBuffer)
        {
            var buffer = Gd.BufferManager
                .GetBuffer(CommandBuffer, indirectBuffer.Handle, indirectBuffer.Offset, indirectBuffer.Size, false)
                .Get(Cbs, indirectBuffer.Offset, indirectBuffer.Size).Value;

            UpdateIndexBufferPattern();

            if (!RecreateGraphicsPipelineIfNeeded())
            {
                return;
            }

            BeginRenderPass();
            DrawCount++;

            if (_indexBufferPattern != null)
            {
                // Convert the index buffer into a supported topology.
                IndexBufferPattern pattern = _indexBufferPattern;

                Auto<DisposableBuffer> indirectBufferAuto = _indexBuffer.BindConvertedIndexBufferIndirect(
                    Gd,
                    Cbs,
                    indirectBuffer,
                    BufferRange.Empty,
                    pattern,
                    false,
                    1,
                    indirectBuffer.Size);

                _needsIndexBufferRebind = false;

                BeginRenderPass(); // May have been interrupted to set buffer data.
                ResumeTransformFeedbackInternal();

                Gd.Api.CmdDrawIndexedIndirect(CommandBuffer, indirectBufferAuto.Get(Cbs, 0, indirectBuffer.Size).Value, 0, 1, (uint)indirectBuffer.Size);
            }
            else
            {
                ResumeTransformFeedbackInternal();

                Gd.Api.CmdDrawIndexedIndirect(CommandBuffer, buffer, (ulong)indirectBuffer.Offset, 1, (uint)indirectBuffer.Size);
            }
        }

        public void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            var countBuffer = Gd.BufferManager
                .GetBuffer(CommandBuffer, parameterBuffer.Handle, parameterBuffer.Offset, parameterBuffer.Size, false)
                .Get(Cbs, parameterBuffer.Offset, parameterBuffer.Size).Value;

            var buffer = Gd.BufferManager
                .GetBuffer(CommandBuffer, indirectBuffer.Handle, indirectBuffer.Offset, indirectBuffer.Size, false)
                .Get(Cbs, indirectBuffer.Offset, indirectBuffer.Size).Value;

            UpdateIndexBufferPattern();

            if (!RecreateGraphicsPipelineIfNeeded())
            {
                return;
            }

            BeginRenderPass();
            DrawCount++;

            if (_indexBufferPattern != null)
            {
                // Convert the index buffer into a supported topology.
                IndexBufferPattern pattern = _indexBufferPattern;

                Auto<DisposableBuffer> indirectBufferAuto = _indexBuffer.BindConvertedIndexBufferIndirect(
                    Gd,
                    Cbs,
                    indirectBuffer,
                    parameterBuffer,
                    pattern,
                    true,
                    maxDrawCount,
                    stride);

                _needsIndexBufferRebind = false;

                BeginRenderPass(); // May have been interrupted to set buffer data.
                ResumeTransformFeedbackInternal();

                if (Gd.Capabilities.SupportsIndirectParameters)
                {
                    Gd.DrawIndirectCountApi.CmdDrawIndexedIndirectCount(
                        CommandBuffer,
                        indirectBufferAuto.Get(Cbs, 0, indirectBuffer.Size).Value,
                        0,
                        countBuffer,
                        (ulong)parameterBuffer.Offset,
                        (uint)maxDrawCount,
                        (uint)stride);
                }
                else
                {
                    // This is also fine because the indirect data conversion always zeros
                    // the entries that are past the current draw count.

                    Gd.Api.CmdDrawIndexedIndirect(
                        CommandBuffer,
                        indirectBufferAuto.Get(Cbs, 0, indirectBuffer.Size).Value,
                        0,
                        (uint)maxDrawCount,
                        (uint)stride);
                }
            }
            else
            {
                ResumeTransformFeedbackInternal();

                if (Gd.Capabilities.SupportsIndirectParameters)
                {
                    Gd.DrawIndirectCountApi.CmdDrawIndexedIndirectCount(
                        CommandBuffer,
                        buffer,
                        (ulong)indirectBuffer.Offset,
                        countBuffer,
                        (ulong)parameterBuffer.Offset,
                        (uint)maxDrawCount,
                        (uint)stride);
                }
                else
                {
                    // Not fully correct, but we can't do much better if the host does not support indirect count.
                    Gd.Api.CmdDrawIndexedIndirect(
                        CommandBuffer,
                        buffer,
                        (ulong)indirectBuffer.Offset,
                        (uint)maxDrawCount,
                        (uint)stride);
                }
            }
        }

        public void DrawIndirect(BufferRange indirectBuffer)
        {
            // TODO: Support quads and other unsupported topologies.

            var buffer = Gd.BufferManager
                .GetBuffer(CommandBuffer, indirectBuffer.Handle, indirectBuffer.Offset, indirectBuffer.Size, false)
                .Get(Cbs, indirectBuffer.Offset, indirectBuffer.Size, false).Value;

            if (!RecreateGraphicsPipelineIfNeeded())
            {
                return;
            }

            BeginRenderPass();
            ResumeTransformFeedbackInternal();
            DrawCount++;

            Gd.Api.CmdDrawIndirect(CommandBuffer, buffer, (ulong)indirectBuffer.Offset, 1, (uint)indirectBuffer.Size);
        }

        public void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            if (!Gd.Capabilities.SupportsIndirectParameters)
            {
                // TODO: Fallback for when this is not supported.
                throw new NotSupportedException();
            }

            var buffer = Gd.BufferManager
                .GetBuffer(CommandBuffer, indirectBuffer.Handle, indirectBuffer.Offset, indirectBuffer.Size, false)
                .Get(Cbs, indirectBuffer.Offset, indirectBuffer.Size, false).Value;

            var countBuffer = Gd.BufferManager
                .GetBuffer(CommandBuffer, parameterBuffer.Handle, parameterBuffer.Offset, parameterBuffer.Size, false)
                .Get(Cbs, parameterBuffer.Offset, parameterBuffer.Size, false).Value;

            // TODO: Support quads and other unsupported topologies.

            if (!RecreateGraphicsPipelineIfNeeded())
            {
                return;
            }

            BeginRenderPass();
            ResumeTransformFeedbackInternal();
            DrawCount++;

            Gd.DrawIndirectCountApi.CmdDrawIndirectCount(
                CommandBuffer,
                buffer,
                (ulong)indirectBuffer.Offset,
                countBuffer,
                (ulong)parameterBuffer.Offset,
                (uint)maxDrawCount,
                (uint)stride);
        }

        public void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion)
        {
            if (texture is TextureView srcTexture)
            {
                var oldCullMode = _newState.CullMode;
                var oldStencilTestEnable = _newState.StencilTestEnable;
                var oldDepthTestEnable = _newState.DepthTestEnable;
                var oldDepthWriteEnable = _newState.DepthWriteEnable;
                var oldTopology = _newState.Topology;
                var oldViewports = DynamicState.Viewports;
                var oldViewportsCount = _newState.ViewportsCount;

                _newState.CullMode = CullModeFlags.None;
                _newState.StencilTestEnable = false;
                _newState.DepthTestEnable = false;
                _newState.DepthWriteEnable = false;
                SignalStateChange();

                Gd.HelperShader.DrawTexture(
                    Gd,
                    this,
                    srcTexture,
                    sampler,
                    srcRegion,
                    dstRegion);

                _newState.CullMode = oldCullMode;
                _newState.StencilTestEnable = oldStencilTestEnable;
                _newState.DepthTestEnable = oldDepthTestEnable;
                _newState.DepthWriteEnable = oldDepthWriteEnable;
                _newState.Topology = oldTopology;

                DynamicState.SetViewports(ref oldViewports, oldViewportsCount);

                _newState.ViewportsCount = oldViewportsCount;
                SignalStateChange();
            }
        }

        public void EndTransformFeedback()
        {
            PauseTransformFeedbackInternal();
            _tfEnabled = false;
        }

        public bool IsCommandBufferActive(CommandBuffer cb)
        {
            return CommandBuffer.Handle == cb.Handle;
        }

        internal void Rebind(Auto<DisposableBuffer> buffer, int offset, int size)
        {
            _descriptorSetUpdater.Rebind(buffer, offset, size);

            if (_indexBuffer.Overlaps(buffer, offset, size))
            {
                _indexBuffer.BindIndexBuffer(Gd, Cbs);
            }

            for (int i = 0; i < _vertexBuffers.Length; i++)
            {
                if (_vertexBuffers[i].Overlaps(buffer, offset, size))
                {
                    _vertexBuffers[i].BindVertexBuffer(Gd, Cbs, (uint)i, ref _newState, _vertexBufferUpdater);
                }
            }

            _vertexBufferUpdater.Commit(Cbs);
        }

#pragma warning disable CA1822 // Mark member as static
        public void SetAlphaTest(bool enable, float reference, CompareOp op)
        {
            // This is currently handled using shader specialization, as Vulkan does not support alpha test.
            // In the future, we may want to use this to write the reference value into the support buffer,
            // to avoid creating one version of the shader per reference value used.
        }
#pragma warning restore CA1822

        public void SetBlendState(AdvancedBlendDescriptor blend)
        {
            for (int index = 0; index < Constants.MaxRenderTargets; index++)
            {
                ref var vkBlend = ref _newState.Internal.ColorBlendAttachmentState[index];

                if (index == 0)
                {
                    var blendOp = blend.Op.Convert();

                    vkBlend = new PipelineColorBlendAttachmentState(
                        blendEnable: true,
                        colorBlendOp: blendOp,
                        alphaBlendOp: blendOp,
                        colorWriteMask: vkBlend.ColorWriteMask);

                    if (Gd.Capabilities.SupportsBlendEquationAdvancedNonPreMultipliedSrcColor)
                    {
                        _newState.AdvancedBlendSrcPreMultiplied = blend.SrcPreMultiplied;
                    }

                    if (Gd.Capabilities.SupportsBlendEquationAdvancedCorrelatedOverlap)
                    {
                        _newState.AdvancedBlendOverlap = blend.Overlap.Convert();
                    }
                }
                else
                {
                    vkBlend = new PipelineColorBlendAttachmentState(
                        colorWriteMask: vkBlend.ColorWriteMask);
                }

                if (vkBlend.ColorWriteMask == 0)
                {
                    _storedBlend[index] = vkBlend;

                    vkBlend = new PipelineColorBlendAttachmentState();
                }
            }

            SignalStateChange();
        }

        public void SetBlendState(int index, BlendDescriptor blend)
        {
            ref var vkBlend = ref _newState.Internal.ColorBlendAttachmentState[index];

            if (blend.Enable)
            {
                vkBlend.BlendEnable = blend.Enable;
                vkBlend.SrcColorBlendFactor = blend.ColorSrcFactor.Convert();
                vkBlend.DstColorBlendFactor = blend.ColorDstFactor.Convert();
                vkBlend.ColorBlendOp = blend.ColorOp.Convert();
                vkBlend.SrcAlphaBlendFactor = blend.AlphaSrcFactor.Convert();
                vkBlend.DstAlphaBlendFactor = blend.AlphaDstFactor.Convert();
                vkBlend.AlphaBlendOp = blend.AlphaOp.Convert();
            }
            else
            {
                vkBlend = new PipelineColorBlendAttachmentState(
                    colorWriteMask: vkBlend.ColorWriteMask);
            }

            if (vkBlend.ColorWriteMask == 0)
            {
                _storedBlend[index] = vkBlend;

                vkBlend = new PipelineColorBlendAttachmentState();
            }

            DynamicState.SetBlendConstants(
                blend.BlendConstant.Red,
                blend.BlendConstant.Green,
                blend.BlendConstant.Blue,
                blend.BlendConstant.Alpha);

            // Reset advanced blend state back defaults to the cache to help the pipeline cache.
            _newState.AdvancedBlendSrcPreMultiplied = true;
            _newState.AdvancedBlendDstPreMultiplied = true;
            _newState.AdvancedBlendOverlap = BlendOverlapEXT.UncorrelatedExt;

            SignalStateChange();
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            DynamicState.SetDepthBias(factor, units, clamp);

            _newState.DepthBiasEnable = enables != 0;
            SignalStateChange();
        }

        public void SetDepthClamp(bool clamp)
        {
            _newState.DepthClampEnable = clamp;
            SignalStateChange();
        }

        public void SetDepthMode(DepthMode mode)
        {
            bool oldMode = _newState.DepthMode;
            _newState.DepthMode = mode == DepthMode.MinusOneToOne;
            if (_newState.DepthMode != oldMode)
            {
                SignalStateChange();
            }
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            _newState.DepthTestEnable = depthTest.TestEnable;
            _newState.DepthWriteEnable = depthTest.WriteEnable;
            _newState.DepthCompareOp = depthTest.Func.Convert();
            SignalStateChange();
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            _newState.CullMode = enable ? face.Convert() : CullModeFlags.None;
            SignalStateChange();
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            _newState.FrontFace = frontFace.Convert();
            SignalStateChange();
        }

        public void SetImage(ShaderStage stage, int binding, ITexture image, Format imageFormat)
        {
            _descriptorSetUpdater.SetImage(Cbs, stage, binding, image, imageFormat);
        }

        public void SetImage(int binding, Auto<DisposableImageView> image)
        {
            _descriptorSetUpdater.SetImage(binding, image);
        }

        public void SetIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                _indexBuffer = new IndexBufferState(buffer.Handle, buffer.Offset, buffer.Size, type.Convert());
            }
            else
            {
                _indexBuffer = IndexBufferState.Null;
            }

            _needsIndexBufferRebind = true;
        }

        public void SetLineParameters(float width, bool smooth)
        {
            _newState.LineWidth = width;
            SignalStateChange();
        }

        public void SetLogicOpState(bool enable, LogicalOp op)
        {
            _newState.LogicOpEnable = enable;
            _newState.LogicOp = op.Convert();
            SignalStateChange();
        }

        public void SetMultisampleState(MultisampleDescriptor multisample)
        {
            _newState.AlphaToCoverageEnable = multisample.AlphaToCoverageEnable;
            _newState.AlphaToOneEnable = multisample.AlphaToOneEnable;
            SignalStateChange();
        }

        public void SetPatchParameters(int vertices, ReadOnlySpan<float> defaultOuterLevel, ReadOnlySpan<float> defaultInnerLevel)
        {
            _newState.PatchControlPoints = (uint)vertices;
            SignalStateChange();

            // TODO: Default levels (likely needs emulation on shaders?)
        }

#pragma warning disable CA1822 // Mark member as static
        public void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin)
        {
            // TODO.
        }

        public void SetPolygonMode(PolygonMode frontMode, PolygonMode backMode)
        {
            // TODO.
        }
#pragma warning restore CA1822

        public void SetPrimitiveRestart(bool enable, int index)
        {
            _newState.PrimitiveRestartEnable = enable;
            // TODO: What to do about the index?
            SignalStateChange();
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            _topology = topology;

            var vkTopology = Gd.TopologyRemap(topology).Convert();

            _newState.Topology = vkTopology;

            SignalStateChange();
        }

        public void SetProgram(IProgram program)
        {
            var internalProgram = (ShaderCollection)program;
            var stages = internalProgram.GetInfos();

            _program = internalProgram;

            _descriptorSetUpdater.SetProgram(Cbs, internalProgram, _currentPipelineHandle != 0);
            _bindingBarriersDirty = true;

            _newState.PipelineLayout = internalProgram.PipelineLayout;
            _newState.HasTessellationControlShader = internalProgram.HasTessellationControlShader;
            _newState.StagesCount = (uint)stages.Length;

            stages.CopyTo(_newState.Stages.AsSpan()[..stages.Length]);

            SignalStateChange();

            if (internalProgram.IsCompute)
            {
                EndRenderPass();
            }
        }

        public void Specialize<T>(in T data) where T : unmanaged
        {
            var dataSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in data), 1));

            if (!dataSpan.SequenceEqual(_newState.SpecializationData.Span))
            {
                _newState.SpecializationData = new SpecData(dataSpan);

                SignalStateChange();
            }
        }

        protected virtual void SignalAttachmentChange()
        {
        }

        public void SetRasterizerDiscard(bool discard)
        {
            _newState.RasterizerDiscardEnable = discard;
            SignalStateChange();
        }

        public void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            int count = Math.Min(Constants.MaxRenderTargets, componentMask.Length);
            int writtenAttachments = 0;

            for (int i = 0; i < count; i++)
            {
                ref var vkBlend = ref _newState.Internal.ColorBlendAttachmentState[i];
                var newMask = (ColorComponentFlags)componentMask[i];

                // When color write mask is 0, remove all blend state to help the pipeline cache.
                // Restore it when the mask becomes non-zero.
                if (vkBlend.ColorWriteMask != newMask)
                {
                    if (newMask == 0)
                    {
                        _storedBlend[i] = vkBlend;

                        vkBlend = new PipelineColorBlendAttachmentState();
                    }
                    else if (vkBlend.ColorWriteMask == 0)
                    {
                        vkBlend = _storedBlend[i];
                    }
                }

                vkBlend.ColorWriteMask = newMask;

                if (componentMask[i] != 0)
                {
                    writtenAttachments++;
                }
            }

            if (_framebufferUsingColorWriteMask)
            {
                SetRenderTargetsInternal(_preMaskColors, _preMaskDepthStencil, true);
            }
            else
            {
                SignalStateChange();

                if (writtenAttachments != _writtenAttachmentCount)
                {
                    SignalAttachmentChange();
                    _writtenAttachmentCount = writtenAttachments;
                }
            }
        }

        private void SetRenderTargetsInternal(ITexture[] colors, ITexture depthStencil, bool filterWriteMasked)
        {
            CreateFramebuffer(colors, depthStencil, filterWriteMasked);
            CreateRenderPass();
            SignalStateChange();
            SignalAttachmentChange();
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _framebufferUsingColorWriteMask = false;
            SetRenderTargetsInternal(colors, depthStencil, Gd.IsTBDR);
        }

        public void SetScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            int maxScissors = Gd.Capabilities.SupportsMultiView ? Constants.MaxViewports : 1;
            int count = Math.Min(maxScissors, regions.Length);
            if (count > 0)
            {
                ClearScissor = regions[0];
            }

            for (int i = 0; i < count; i++)
            {
                var region = regions[i];
                var offset = new Offset2D(region.X, region.Y);
                var extent = new Extent2D((uint)region.Width, (uint)region.Height);

                DynamicState.SetScissor(i, new Rect2D(offset, extent));
            }

            DynamicState.ScissorsCount = count;

            _newState.ScissorsCount = (uint)count;
            SignalStateChange();
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            DynamicState.SetStencilMasks(
                (uint)stencilTest.BackFuncMask,
                (uint)stencilTest.BackMask,
                (uint)stencilTest.BackFuncRef,
                (uint)stencilTest.FrontFuncMask,
                (uint)stencilTest.FrontMask,
                (uint)stencilTest.FrontFuncRef);

            _newState.StencilTestEnable = stencilTest.TestEnable;
            _newState.StencilBackFailOp = stencilTest.BackSFail.Convert();
            _newState.StencilBackPassOp = stencilTest.BackDpPass.Convert();
            _newState.StencilBackDepthFailOp = stencilTest.BackDpFail.Convert();
            _newState.StencilBackCompareOp = stencilTest.BackFunc.Convert();
            _newState.StencilFrontFailOp = stencilTest.FrontSFail.Convert();
            _newState.StencilFrontPassOp = stencilTest.FrontDpPass.Convert();
            _newState.StencilFrontDepthFailOp = stencilTest.FrontDpFail.Convert();
            _newState.StencilFrontCompareOp = stencilTest.FrontFunc.Convert();
            SignalStateChange();
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _descriptorSetUpdater.SetStorageBuffers(CommandBuffer, buffers);
        }

        public void SetStorageBuffers(int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            _descriptorSetUpdater.SetStorageBuffers(CommandBuffer, first, buffers);
        }

        public void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            _descriptorSetUpdater.SetTextureAndSampler(Cbs, stage, binding, texture, sampler);
        }

        public void SetTextureAndSamplerIdentitySwizzle(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            _descriptorSetUpdater.SetTextureAndSamplerIdentitySwizzle(Cbs, stage, binding, texture, sampler);
        }

        public void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            PauseTransformFeedbackInternal();

            int count = Math.Min(Constants.MaxTransformFeedbackBuffers, buffers.Length);

            for (int i = 0; i < count; i++)
            {
                var range = buffers[i];

                _transformFeedbackBuffers[i].Dispose();

                if (range.Handle != BufferHandle.Null)
                {
                    _transformFeedbackBuffers[i] =
                        new BufferState(Gd.BufferManager.GetBuffer(CommandBuffer, range.Handle, range.Offset, range.Size, true), range.Offset, range.Size);
                    _transformFeedbackBuffers[i].BindTransformFeedbackBuffer(Gd, Cbs, (uint)i);
                }
                else
                {
                    _transformFeedbackBuffers[i] = BufferState.Null;
                }
            }
        }

        public void SetUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            _descriptorSetUpdater.SetUniformBuffers(CommandBuffer, buffers);
        }

#pragma warning disable CA1822 // Mark member as static
        public void SetUserClipDistance(int index, bool enableClip)
        {
            // TODO.
        }
#pragma warning restore CA1822

        public void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            var formatCapabilities = Gd.FormatCapabilities;

            Span<int> newVbScalarSizes = stackalloc int[Constants.MaxVertexBuffers];

            int count = Math.Min(Constants.MaxVertexAttributes, vertexAttribs.Length);
            uint dirtyVbSizes = 0;

            for (int i = 0; i < count; i++)
            {
                var attribute = vertexAttribs[i];
                var rawIndex = attribute.BufferIndex;
                var bufferIndex = attribute.IsZero ? 0 : rawIndex + 1;

                if (!attribute.IsZero)
                {
                    newVbScalarSizes[rawIndex] = Math.Max(newVbScalarSizes[rawIndex], attribute.Format.GetScalarSize());
                    dirtyVbSizes |= 1u << rawIndex;
                }

                _newState.Internal.VertexAttributeDescriptions[i] = new VertexInputAttributeDescription(
                    (uint)i,
                    (uint)bufferIndex,
                    formatCapabilities.ConvertToVertexVkFormat(attribute.Format),
                    (uint)attribute.Offset);
            }

            while (dirtyVbSizes != 0)
            {
                int dirtyBit = BitOperations.TrailingZeroCount(dirtyVbSizes);

                ref var buffer = ref _vertexBuffers[dirtyBit + 1];

                if (buffer.AttributeScalarAlignment != newVbScalarSizes[dirtyBit])
                {
                    _vertexBuffersDirty |= 1UL << (dirtyBit + 1);
                    buffer.AttributeScalarAlignment = newVbScalarSizes[dirtyBit];
                }

                dirtyVbSizes &= ~(1u << dirtyBit);
            }

            _newState.VertexAttributeDescriptionsCount = (uint)count;
            SignalStateChange();
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            int count = Math.Min(Constants.MaxVertexBuffers, vertexBuffers.Length);

            _newState.Internal.VertexBindingDescriptions[0] = new VertexInputBindingDescription(0, 0, VertexInputRate.Vertex);

            int validCount = 1;

            BufferHandle lastHandle = default;
            Auto<DisposableBuffer> lastBuffer = default;

            for (int i = 0; i < count; i++)
            {
                var vertexBuffer = vertexBuffers[i];

                // TODO: Support divisor > 1
                var inputRate = vertexBuffer.Divisor != 0 ? VertexInputRate.Instance : VertexInputRate.Vertex;

                if (vertexBuffer.Buffer.Handle != BufferHandle.Null)
                {
                    Auto<DisposableBuffer> vb = (vertexBuffer.Buffer.Handle == lastHandle) ? lastBuffer :
                        Gd.BufferManager.GetBuffer(CommandBuffer, vertexBuffer.Buffer.Handle, false);

                    lastHandle = vertexBuffer.Buffer.Handle;
                    lastBuffer = vb;

                    if (vb != null)
                    {
                        int binding = i + 1;
                        int descriptorIndex = validCount++;

                        _newState.Internal.VertexBindingDescriptions[descriptorIndex] = new VertexInputBindingDescription(
                            (uint)binding,
                            (uint)vertexBuffer.Stride,
                            inputRate);

                        int vbSize = vertexBuffer.Buffer.Size;

                        if (Gd.Vendor == Vendor.Amd && !Gd.IsMoltenVk && vertexBuffer.Stride > 0)
                        {
                            // AMD has a bug where if offset + stride * count is greater than
                            // the size, then the last attribute will have the wrong value.
                            // As a workaround, simply use the full buffer size.
                            int remainder = vbSize % vertexBuffer.Stride;
                            if (remainder != 0)
                            {
                                vbSize += vertexBuffer.Stride - remainder;
                            }
                        }

                        ref var buffer = ref _vertexBuffers[binding];
                        int oldScalarAlign = buffer.AttributeScalarAlignment;

                        if (Gd.Capabilities.VertexBufferAlignment < 2 &&
                            (vertexBuffer.Stride % FormatExtensions.MaxBufferFormatScalarSize) == 0)
                        {
                            if (!buffer.Matches(vb, descriptorIndex, vertexBuffer.Buffer.Offset, vbSize, vertexBuffer.Stride))
                            {
                                buffer.Dispose();

                                buffer = new VertexBufferState(
                                    vb,
                                    descriptorIndex,
                                    vertexBuffer.Buffer.Offset,
                                    vbSize,
                                    vertexBuffer.Stride);

                                buffer.BindVertexBuffer(Gd, Cbs, (uint)binding, ref _newState, _vertexBufferUpdater);
                            }
                        }
                        else
                        {
                            // May need to be rewritten. Bind this buffer before draw.

                            buffer.Dispose();

                            buffer = new VertexBufferState(
                                vertexBuffer.Buffer.Handle,
                                descriptorIndex,
                                vertexBuffer.Buffer.Offset,
                                vbSize,
                                vertexBuffer.Stride);

                            _vertexBuffersDirty |= 1UL << binding;
                        }

                        buffer.AttributeScalarAlignment = oldScalarAlign;
                    }
                }
            }

            _vertexBufferUpdater.Commit(Cbs);

            _newState.VertexBindingDescriptionsCount = (uint)validCount;
            SignalStateChange();
        }

        public void SetViewports(ReadOnlySpan<Viewport> viewports)
        {
            int maxViewports = Gd.Capabilities.SupportsMultiView ? Constants.MaxViewports : 1;
            int count = Math.Min(maxViewports, viewports.Length);

            static float Clamp(float value)
            {
                return Math.Clamp(value, 0f, 1f);
            }

            DynamicState.ViewportsCount = (uint)count;

            for (int i = 0; i < count; i++)
            {
                var viewport = viewports[i];

                DynamicState.SetViewport(i, new Silk.NET.Vulkan.Viewport(
                    viewport.Region.X,
                    viewport.Region.Y,
                    viewport.Region.Width == 0f ? 1f : viewport.Region.Width,
                    viewport.Region.Height == 0f ? 1f : viewport.Region.Height,
                    Clamp(viewport.DepthNear),
                    Clamp(viewport.DepthFar)));
            }

            _newState.ViewportsCount = (uint)count;
            SignalStateChange();
        }

        public void SwapBuffer(Auto<DisposableBuffer> from, Auto<DisposableBuffer> to)
        {
            _indexBuffer.Swap(from, to);

            for (int i = 0; i < _vertexBuffers.Length; i++)
            {
                _vertexBuffers[i].Swap(from, to);
            }

            for (int i = 0; i < _transformFeedbackBuffers.Length; i++)
            {
                _transformFeedbackBuffers[i].Swap(from, to);
            }

            _descriptorSetUpdater.SwapBuffer(from, to);

            SignalCommandBufferChange();
        }

        public unsafe void TextureBarrier()
        {
            MemoryBarrier memoryBarrier = new()
            {
                SType = StructureType.MemoryBarrier,
                SrcAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
                DstAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            };

            Gd.Api.CmdPipelineBarrier(
                CommandBuffer,
                PipelineStageFlags.FragmentShaderBit,
                PipelineStageFlags.FragmentShaderBit,
                0,
                1,
                memoryBarrier,
                0,
                null,
                0,
                null);
        }

        public void TextureBarrierTiled()
        {
            TextureBarrier();
        }

        protected void SignalCommandBufferChange()
        {
            _needsIndexBufferRebind = true;
            _needsTransformFeedbackBuffersRebind = true;
            _vertexBuffersDirty = ulong.MaxValue >> (64 - _vertexBuffers.Length);

            _descriptorSetUpdater.SignalCommandBufferChange();
            DynamicState.ForceAllDirty();
            _currentPipelineHandle = 0;
        }

        private void CreateFramebuffer(ITexture[] colors, ITexture depthStencil, bool filterWriteMasked)
        {
            if (filterWriteMasked)
            {
                // TBDR GPUs don't work properly if the same attachment is bound to multiple targets,
                // due to each attachment being a copy of the real attachment, rather than a direct write.

                // Just try to remove duplicate attachments.
                // Save a copy of the array to rebind when mask changes.

                void MaskOut()
                {
                    if (!_framebufferUsingColorWriteMask)
                    {
                        _preMaskColors = colors.ToArray();
                        _preMaskDepthStencil = depthStencil;
                    }

                    // If true, then the framebuffer must be recreated when the mask changes.
                    _framebufferUsingColorWriteMask = true;
                }

                // Look for textures that are masked out.

                for (int i = 0; i < colors.Length; i++)
                {
                    if (colors[i] == null)
                    {
                        continue;
                    }

                    ref var vkBlend = ref _newState.Internal.ColorBlendAttachmentState[i];

                    for (int j = 0; j < i; j++)
                    {
                        // Check each binding for a duplicate binding before it.

                        if (colors[i] == colors[j])
                        {
                            // Prefer the binding with no write mask.
                            ref var vkBlend2 = ref _newState.Internal.ColorBlendAttachmentState[j];
                            if (vkBlend.ColorWriteMask == 0)
                            {
                                colors[i] = null;
                                MaskOut();
                            }
                            else if (vkBlend2.ColorWriteMask == 0)
                            {
                                colors[j] = null;
                                MaskOut();
                            }
                        }
                    }
                }
            }

            FramebufferParams = new FramebufferParams(Device, colors, depthStencil);
            UpdatePipelineAttachmentFormats();
        }

        protected void UpdatePipelineAttachmentFormats()
        {
            var dstAttachmentFormats = _newState.Internal.AttachmentFormats.AsSpan();
            FramebufferParams.AttachmentFormats.CopyTo(dstAttachmentFormats);
            _newState.Internal.AttachmentIntegerFormatMask = FramebufferParams.AttachmentIntegerFormatMask;

            for (int i = FramebufferParams.AttachmentFormats.Length; i < dstAttachmentFormats.Length; i++)
            {
                dstAttachmentFormats[i] = 0;
            }

            _newState.ColorBlendAttachmentStateCount = (uint)(FramebufferParams.MaxColorAttachmentIndex + 1);
            _newState.HasDepthStencil = FramebufferParams.HasDepthStencil;
            _newState.SamplesCount = FramebufferParams.AttachmentSamples.Length != 0 ? FramebufferParams.AttachmentSamples[0] : 1;
        }

        protected unsafe void CreateRenderPass()
        {
            var hasFramebuffer = FramebufferParams != null;

            EndRenderPass();

            if (!hasFramebuffer || FramebufferParams.AttachmentsCount == 0)
            {
                // Use the null framebuffer.
                _nullRenderPass ??= new RenderPassHolder(Gd, Device, new RenderPassCacheKey(), FramebufferParams);

                _renderPass = _nullRenderPass.GetRenderPass();
                _framebuffer = _nullRenderPass.GetFramebuffer(Gd, Cbs, FramebufferParams);
            }
            else
            {
                (_renderPass, _framebuffer) = FramebufferParams.GetPassAndFramebuffer(Gd, Device, Cbs);
            }
        }

        protected void SignalStateChange()
        {
            _graphicsStateDirty = true;
            _computeStateDirty = true;
        }

        private void RecreateComputePipelineIfNeeded()
        {
            if (_computeStateDirty || Pbp != PipelineBindPoint.Compute)
            {
                CreatePipeline(PipelineBindPoint.Compute);
                _computeStateDirty = false;
                Pbp = PipelineBindPoint.Compute;

                if (_bindingBarriersDirty)
                {
                    // Stale barriers may have been activated by switching program. Emit any that are relevant.
                    _descriptorSetUpdater.InsertBindingBarriers(Cbs);

                    _bindingBarriersDirty = false;
                }
            }

            Gd.Barriers.Flush(Cbs.CommandBuffer, RenderPassActive, EndRenderPassDelegate);

            _descriptorSetUpdater.UpdateAndBindDescriptorSets(Cbs, PipelineBindPoint.Compute);
        }

        private bool RecreateGraphicsPipelineIfNeeded()
        {
            if (AutoFlush.ShouldFlushDraw(DrawCount))
            {
                Gd.FlushAllCommands();
            }

            DynamicState.ReplayIfDirty(Gd.Api, CommandBuffer);

            if (_needsIndexBufferRebind && _indexBufferPattern == null)
            {
                _indexBuffer.BindIndexBuffer(Gd, Cbs);
                _needsIndexBufferRebind = false;
            }

            if (_needsTransformFeedbackBuffersRebind)
            {
                PauseTransformFeedbackInternal();

                for (int i = 0; i < Constants.MaxTransformFeedbackBuffers; i++)
                {
                    _transformFeedbackBuffers[i].BindTransformFeedbackBuffer(Gd, Cbs, (uint)i);
                }

                _needsTransformFeedbackBuffersRebind = false;
            }

            if (_vertexBuffersDirty != 0)
            {
                while (_vertexBuffersDirty != 0)
                {
                    int i = BitOperations.TrailingZeroCount(_vertexBuffersDirty);

                    _vertexBuffers[i].BindVertexBuffer(Gd, Cbs, (uint)i, ref _newState, _vertexBufferUpdater);

                    _vertexBuffersDirty &= ~(1UL << i);
                }

                _vertexBufferUpdater.Commit(Cbs);
            }

            if (_graphicsStateDirty || Pbp != PipelineBindPoint.Graphics)
            {
                if (!CreatePipeline(PipelineBindPoint.Graphics))
                {
                    return false;
                }

                _graphicsStateDirty = false;
                Pbp = PipelineBindPoint.Graphics;

                if (_bindingBarriersDirty)
                {
                    // Stale barriers may have been activated by switching program. Emit any that are relevant.
                    _descriptorSetUpdater.InsertBindingBarriers(Cbs);

                    _bindingBarriersDirty = false;
                }
            }

            Gd.Barriers.Flush(Cbs.CommandBuffer, RenderPassActive, EndRenderPassDelegate);

            _descriptorSetUpdater.UpdateAndBindDescriptorSets(Cbs, PipelineBindPoint.Graphics);

            return true;
        }

        private bool CreatePipeline(PipelineBindPoint pbp)
        {
            // We can only create a pipeline if the have the shader stages set.
            if (_newState.Stages != null)
            {
                if (pbp == PipelineBindPoint.Graphics && _renderPass == null)
                {
                    CreateRenderPass();
                }

                if (!_program.IsLinked)
                {
                    // Background compile failed, we likely can't create the pipeline because the shader is broken
                    // or the driver failed to compile it.

                    return false;
                }

                var pipeline = pbp == PipelineBindPoint.Compute
                    ? _newState.CreateComputePipeline(Gd, Device, _program, PipelineCache)
                    : _newState.CreateGraphicsPipeline(Gd, Device, _program, PipelineCache, _renderPass.Get(Cbs).Value);

                if (pipeline == null)
                {
                    // Host failed to create the pipeline, likely due to driver bugs.

                    return false;
                }

                ulong pipelineHandle = pipeline.GetUnsafe().Value.Handle;

                if (_currentPipelineHandle != pipelineHandle)
                {
                    _currentPipelineHandle = pipelineHandle;
                    Pipeline = pipeline;

                    PauseTransformFeedbackInternal();
                    Gd.Api.CmdBindPipeline(CommandBuffer, pbp, Pipeline.Get(Cbs).Value);
                }
            }

            return true;
        }

        private unsafe void BeginRenderPass()
        {
            if (!RenderPassActive)
            {
                FramebufferParams.InsertLoadOpBarriers(Gd, Cbs);

                var renderArea = new Rect2D(null, new Extent2D(FramebufferParams.Width, FramebufferParams.Height));
                var clearValue = new ClearValue();

                var renderPassBeginInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _renderPass.Get(Cbs).Value,
                    Framebuffer = _framebuffer.Get(Cbs).Value,
                    RenderArea = renderArea,
                    PClearValues = &clearValue,
                    ClearValueCount = 1,
                };

                Gd.Api.CmdBeginRenderPass(CommandBuffer, renderPassBeginInfo, SubpassContents.Inline);
                RenderPassActive = true;
            }
        }

        public void EndRenderPass()
        {
            if (RenderPassActive)
            {
                PauseTransformFeedbackInternal();
                Gd.Api.CmdEndRenderPass(CommandBuffer);
                SignalRenderPassEnd();
                RenderPassActive = false;
            }
        }

        protected virtual void SignalRenderPassEnd()
        {
        }

        private void PauseTransformFeedbackInternal()
        {
            if (_tfEnabled && _tfActive)
            {
                EndTransformFeedbackInternal();
                _tfActive = false;
            }
        }

        private void ResumeTransformFeedbackInternal()
        {
            if (_tfEnabled && !_tfActive)
            {
                BeginTransformFeedbackInternal();
                _tfActive = true;
            }
        }

        private unsafe void BeginTransformFeedbackInternal()
        {
            Gd.TransformFeedbackApi.CmdBeginTransformFeedback(CommandBuffer, 0, 0, null, null);
        }

        private unsafe void EndTransformFeedbackInternal()
        {
            Gd.TransformFeedbackApi.CmdEndTransformFeedback(CommandBuffer, 0, 0, null, null);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nullRenderPass?.Dispose();
                _newState.Dispose();
                _descriptorSetUpdater.Dispose();
                _vertexBufferUpdater.Dispose();

                for (int i = 0; i < _vertexBuffers.Length; i++)
                {
                    _vertexBuffers[i].Dispose();
                }

                for (int i = 0; i < _transformFeedbackBuffers.Length; i++)
                {
                    _transformFeedbackBuffers[i].Dispose();
                }

                Pipeline?.Dispose();

                unsafe
                {
                    Gd.Api.DestroyPipelineCache(Device, PipelineCache, null);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
