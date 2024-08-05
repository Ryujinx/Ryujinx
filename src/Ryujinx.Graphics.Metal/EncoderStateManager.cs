using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Metal.State;
using Ryujinx.Graphics.Shader;
using SharpMetal.Metal;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BufferAssignment = Ryujinx.Graphics.GAL.BufferAssignment;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct EncoderStateManager : IDisposable
    {
        private const int ArrayGrowthSize = 16;

        private readonly MTLDevice _device;
        private readonly Pipeline _pipeline;
        private readonly BufferManager _bufferManager;

        private readonly DepthStencilCache _depthStencilCache;

        private readonly EncoderState _mainState = new();
        private EncoderState _currentState;

        public readonly IndexBufferState IndexBuffer => _currentState.IndexBuffer;
        public readonly PrimitiveTopology Topology => _currentState.Topology;
        public readonly Texture[] RenderTargets => _currentState.RenderTargets;
        public readonly Texture DepthStencil => _currentState.DepthStencil;
        public readonly ComputeSize ComputeLocalSize => _currentState.ComputeProgram.ComputeLocalSize;

        // RGBA32F is the biggest format
        private const int ZeroBufferSize = 4 * 4;
        private readonly BufferHandle _zeroBuffer;

        public unsafe EncoderStateManager(MTLDevice device, BufferManager bufferManager, Pipeline pipeline)
        {
            _device = device;
            _pipeline = pipeline;
            _bufferManager = bufferManager;

            _depthStencilCache = new(device);
            _currentState = _mainState;

            // Zero buffer
            byte[] zeros = new byte[ZeroBufferSize];
            fixed (byte* ptr = zeros)
            {
                _zeroBuffer = _bufferManager.Create((IntPtr)ptr, ZeroBufferSize);
            }
        }

        public readonly void Dispose()
        {
            _depthStencilCache.Dispose();
        }

        private readonly void SignalDirty(DirtyFlags flags)
        {
            _currentState.Dirty |= flags;
        }

        public void SignalRenderDirty()
        {
            SignalDirty(DirtyFlags.RenderAll);
        }

        public void SignalComputeDirty()
        {
            SignalDirty(DirtyFlags.ComputeAll);
        }

        public EncoderState SwapState(EncoderState state, DirtyFlags flags = DirtyFlags.All)
        {
            _currentState = state ?? _mainState;

            SignalDirty(flags);

            return _mainState;
        }

        public PredrawState SavePredrawState()
        {
            return new PredrawState
            {
                CullMode = _currentState.CullMode,
                DepthStencilUid = _currentState.DepthStencilUid,
                Topology = _currentState.Topology,
                Viewports = _currentState.Viewports.ToArray(),
            };
        }

        public readonly void RestorePredrawState(PredrawState state)
        {
            _currentState.CullMode = state.CullMode;
            _currentState.DepthStencilUid = state.DepthStencilUid;
            _currentState.Topology = state.Topology;
            _currentState.Viewports = state.Viewports;

            SignalDirty(DirtyFlags.CullMode | DirtyFlags.DepthStencil | DirtyFlags.Viewports);
        }

        public readonly void SetClearLoadAction(bool clear)
        {
            _currentState.ClearLoadAction = clear;
        }

        public readonly void DirtyTextures()
        {
            SignalDirty(DirtyFlags.Textures);
        }

        public readonly void DirtyImages()
        {
            SignalDirty(DirtyFlags.Images);
        }

        public readonly MTLRenderCommandEncoder CreateRenderCommandEncoder()
        {
            // Initialise Pass & State
            using var renderPassDescriptor = new MTLRenderPassDescriptor();

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                if (_currentState.RenderTargets[i] is Texture tex)
                {
                    var passAttachment = renderPassDescriptor.ColorAttachments.Object((ulong)i);
                    tex.PopulateRenderPassAttachment(passAttachment);
                    passAttachment.LoadAction = _currentState.ClearLoadAction ? MTLLoadAction.Clear : MTLLoadAction.Load;
                    passAttachment.StoreAction = MTLStoreAction.Store;
                }
            }

            var depthAttachment = renderPassDescriptor.DepthAttachment;
            var stencilAttachment = renderPassDescriptor.StencilAttachment;

            if (_currentState.DepthStencil != null)
            {
                switch (_currentState.DepthStencil.GetHandle().PixelFormat)
                {
                    // Depth Only Attachment
                    case MTLPixelFormat.Depth16Unorm:
                    case MTLPixelFormat.Depth32Float:
                        depthAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        depthAttachment.LoadAction = MTLLoadAction.Load;
                        depthAttachment.StoreAction = MTLStoreAction.Store;
                        break;

                    // Stencil Only Attachment
                    case MTLPixelFormat.Stencil8:
                        stencilAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        stencilAttachment.LoadAction = MTLLoadAction.Load;
                        stencilAttachment.StoreAction = MTLStoreAction.Store;
                        break;

                    // Combined Attachment
                    case MTLPixelFormat.Depth24UnormStencil8:
                    case MTLPixelFormat.Depth32FloatStencil8:
                        depthAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        depthAttachment.LoadAction = MTLLoadAction.Load;
                        depthAttachment.StoreAction = MTLStoreAction.Store;

                        stencilAttachment.Texture = _currentState.DepthStencil.GetHandle();
                        stencilAttachment.LoadAction = MTLLoadAction.Load;
                        stencilAttachment.StoreAction = MTLStoreAction.Store;
                        break;
                    default:
                        Logger.Error?.PrintMsg(LogClass.Gpu, $"Unsupported Depth/Stencil Format: {_currentState.DepthStencil.GetHandle().PixelFormat}!");
                        break;
                }
            }

            // Initialise Encoder
            var renderCommandEncoder = _pipeline.CommandBuffer.RenderCommandEncoder(renderPassDescriptor);

            return renderCommandEncoder;
        }

        public readonly MTLComputeCommandEncoder CreateComputeCommandEncoder()
        {
            using var descriptor = new MTLComputePassDescriptor();
            var computeCommandEncoder = _pipeline.CommandBuffer.ComputeCommandEncoder(descriptor);

            return computeCommandEncoder;
        }

        public void RenderResourcesPrepass()
        {
            _currentState.RenderEncoderResources.Clear();

            if ((_currentState.Dirty & DirtyFlags.RenderPipeline) != 0)
            {
                SetVertexBuffers(_currentState.VertexBuffers, ref _currentState.RenderEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Uniforms) != 0)
            {
                UpdateAndBind(_currentState.RenderProgram, Constants.ConstantBuffersSetIndex, ref _currentState.RenderEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Storages) != 0)
            {
                UpdateAndBind(_currentState.RenderProgram, Constants.StorageBuffersSetIndex, ref _currentState.RenderEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Textures) != 0)
            {
                UpdateAndBind(_currentState.RenderProgram, Constants.TexturesSetIndex, ref _currentState.RenderEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Images) != 0)
            {
                UpdateAndBind(_currentState.RenderProgram, Constants.ImagesSetIndex, ref _currentState.RenderEncoderResources);
            }
        }

        public void ComputeResourcesPrepass()
        {
            _currentState.ComputeEncoderResources.Clear();

            if ((_currentState.Dirty & DirtyFlags.Uniforms) != 0)
            {
                UpdateAndBind(_currentState.ComputeProgram, Constants.ConstantBuffersSetIndex, ref _currentState.ComputeEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Storages) != 0)
            {
                UpdateAndBind(_currentState.ComputeProgram, Constants.StorageBuffersSetIndex, ref _currentState.ComputeEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Textures) != 0)
            {
                UpdateAndBind(_currentState.ComputeProgram, Constants.TexturesSetIndex, ref _currentState.ComputeEncoderResources);
            }

            if ((_currentState.Dirty & DirtyFlags.Images) != 0)
            {
                UpdateAndBind(_currentState.ComputeProgram, Constants.ImagesSetIndex, ref _currentState.ComputeEncoderResources);
            }
        }

        public void RebindRenderState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if ((_currentState.Dirty & DirtyFlags.RenderPipeline) != 0)
            {
                SetRenderPipelineState(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.DepthStencil) != 0)
            {
                SetDepthStencilState(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.DepthClamp) != 0)
            {
                SetDepthClamp(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.DepthBias) != 0)
            {
                SetDepthBias(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.CullMode) != 0)
            {
                SetCullMode(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.FrontFace) != 0)
            {
                SetFrontFace(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.StencilRef) != 0)
            {
                SetStencilRefValue(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.Viewports) != 0)
            {
                SetViewports(renderCommandEncoder);
            }

            if ((_currentState.Dirty & DirtyFlags.Scissors) != 0)
            {
                SetScissors(renderCommandEncoder);
            }

            foreach (var resource in _currentState.RenderEncoderResources.Resources)
            {
                renderCommandEncoder.UseResource(resource.MtlResource, resource.ResourceUsage, resource.Stages);
            }

            foreach (var buffer in _currentState.RenderEncoderResources.VertexBuffers)
            {
                renderCommandEncoder.SetVertexBuffer(buffer.Buffer, buffer.Offset, buffer.Binding);
            }

            foreach (var buffer in _currentState.RenderEncoderResources.FragmentBuffers)
            {
                renderCommandEncoder.SetFragmentBuffer(buffer.Buffer, buffer.Offset, buffer.Binding);
            }

            _currentState.Dirty &= ~DirtyFlags.RenderAll;
        }

        public readonly void RebindComputeState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if ((_currentState.Dirty & DirtyFlags.ComputePipeline) != 0)
            {
                SetComputePipelineState(computeCommandEncoder);
            }

            foreach (var resource in _currentState.ComputeEncoderResources.Resources)
            {
                computeCommandEncoder.UseResource(resource.MtlResource, resource.ResourceUsage);
            }

            foreach (var buffer in _currentState.ComputeEncoderResources.Buffers)
            {
                computeCommandEncoder.SetBuffer(buffer.Buffer, buffer.Offset, buffer.Binding);
            }

            _currentState.Dirty &= ~DirtyFlags.ComputeAll;
        }

        private readonly void SetRenderPipelineState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            MTLRenderPipelineState pipelineState = _currentState.Pipeline.CreateRenderPipeline(_device, _currentState.RenderProgram);

            renderCommandEncoder.SetRenderPipelineState(pipelineState);

            renderCommandEncoder.SetBlendColor(
                _currentState.BlendColor.Red,
                _currentState.BlendColor.Green,
                _currentState.BlendColor.Blue,
                _currentState.BlendColor.Alpha);
        }

        private readonly void SetComputePipelineState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.ComputeProgram == null)
            {
                return;
            }

            var pipelineState = PipelineState.CreateComputePipeline(_device, _currentState.ComputeProgram);

            computeCommandEncoder.SetComputePipelineState(pipelineState);
        }

        public readonly void UpdateIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                _currentState.IndexBuffer = new IndexBufferState(buffer.Handle, buffer.Offset, buffer.Size, type);
            }
            else
            {
                _currentState.IndexBuffer = IndexBufferState.Null;
            }
        }

        public readonly void UpdatePrimitiveTopology(PrimitiveTopology topology)
        {
            _currentState.Topology = topology;
        }

        public readonly void UpdateProgram(IProgram program)
        {
            Program prg = (Program)program;

            if (prg.VertexFunction == IntPtr.Zero && prg.ComputeFunction == IntPtr.Zero)
            {
                if (prg.FragmentFunction == IntPtr.Zero)
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, "No compute function");
                }
                else
                {
                    Logger.Error?.PrintMsg(LogClass.Gpu, "No vertex function");
                }
                return;
            }

            if (prg.VertexFunction != IntPtr.Zero)
            {
                _currentState.RenderProgram = prg;

                SignalDirty(DirtyFlags.RenderPipeline | DirtyFlags.ArgBuffers);
            }
            else if (prg.ComputeFunction != IntPtr.Zero)
            {
                _currentState.ComputeProgram = prg;

                SignalDirty(DirtyFlags.ComputePipeline | DirtyFlags.ArgBuffers);
            }
        }

        public readonly void UpdateRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _currentState.FramebufferUsingColorWriteMask = false;
            UpdateRenderTargetsInternal(colors, depthStencil);
        }

        public readonly void UpdateRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            ref var blendState = ref _currentState.Pipeline.Internal.ColorBlendState;

            for (int i = 0; i < componentMask.Length; i++)
            {
                bool red = (componentMask[i] & (0x1 << 0)) != 0;
                bool green = (componentMask[i] & (0x1 << 1)) != 0;
                bool blue = (componentMask[i] & (0x1 << 2)) != 0;
                bool alpha = (componentMask[i] & (0x1 << 3)) != 0;

                var mask = MTLColorWriteMask.None;

                mask |= red ? MTLColorWriteMask.Red : 0;
                mask |= green ? MTLColorWriteMask.Green : 0;
                mask |= blue ? MTLColorWriteMask.Blue : 0;
                mask |= alpha ? MTLColorWriteMask.Alpha : 0;

                ref ColorBlendStateUid mtlBlend = ref blendState[i];

                // When color write mask is 0, remove all blend state to help the pipeline cache.
                // Restore it when the mask becomes non-zero.
                if (mtlBlend.WriteMask != mask)
                {
                    if (mask == 0)
                    {
                        _currentState.StoredBlend[i] = mtlBlend;

                        mtlBlend.Swap(new ColorBlendStateUid());
                    }
                    else if (mtlBlend.WriteMask == 0)
                    {
                        mtlBlend.Swap(_currentState.StoredBlend[i]);
                    }
                }

                blendState[i].WriteMask = mask;
            }

            if (_currentState.FramebufferUsingColorWriteMask)
            {
                UpdateRenderTargetsInternal(_currentState.PreMaskRenderTargets, _currentState.PreMaskDepthStencil);
            }
            else
            {
                // Requires recreating pipeline
                if (_pipeline.CurrentEncoderType == EncoderType.Render)
                {
                    _pipeline.EndCurrentPass();
                }
            }
        }

        private readonly void UpdateRenderTargetsInternal(ITexture[] colors, ITexture depthStencil)
        {
            // TBDR GPUs don't work properly if the same attachment is bound to multiple targets,
            // due to each attachment being a copy of the real attachment, rather than a direct write.
            //
            // Just try to remove duplicate attachments.
            // Save a copy of the array to rebind when mask changes.

            // Look for textures that are masked out.

            ref PipelineState pipeline = ref _currentState.Pipeline;
            ref var blendState = ref pipeline.Internal.ColorBlendState;

            pipeline.ColorBlendAttachmentStateCount = (uint)colors.Length;

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] == null)
                {
                    continue;
                }

                var mtlMask = blendState[i].WriteMask;

                for (int j = 0; j < i; j++)
                {
                    // Check each binding for a duplicate binding before it.

                    if (colors[i] == colors[j])
                    {
                        // Prefer the binding with no write mask.

                        var mtlMask2 = blendState[j].WriteMask;

                        if (mtlMask == 0)
                        {
                            colors[i] = null;
                            MaskOut(colors, depthStencil);
                        }
                        else if (mtlMask2 == 0)
                        {
                            colors[j] = null;
                            MaskOut(colors, depthStencil);
                        }
                    }
                }
            }

            _currentState.RenderTargets = new Texture[Constants.MaxColorAttachments];

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] is not Texture tex)
                {
                    blendState[i].PixelFormat = MTLPixelFormat.Invalid;

                    continue;
                }

                blendState[i].PixelFormat = tex.GetHandle().PixelFormat; // TODO: cache this
                _currentState.RenderTargets[i] = tex;
            }

            if (depthStencil is Texture depthTexture)
            {
                pipeline.DepthStencilFormat = depthTexture.GetHandle().PixelFormat; // TODO: cache this
                _currentState.DepthStencil = depthTexture;
            }
            else if (depthStencil == null)
            {
                pipeline.DepthStencilFormat = MTLPixelFormat.Invalid;
                _currentState.DepthStencil = null;
            }

            // Requires recreating pipeline
            if (_pipeline.CurrentEncoderType == EncoderType.Render)
            {
                _pipeline.EndCurrentPass();
            }
        }

        private readonly void MaskOut(ITexture[] colors, ITexture depthStencil)
        {
            if (!_currentState.FramebufferUsingColorWriteMask)
            {
                _currentState.PreMaskRenderTargets = colors;
                _currentState.PreMaskDepthStencil = depthStencil;
            }

            // If true, then the framebuffer must be recreated when the mask changes.
            _currentState.FramebufferUsingColorWriteMask = true;
        }

        public readonly void UpdateVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            vertexAttribs.CopyTo(_currentState.VertexAttribs);

            // Update the buffers on the pipeline
            UpdatePipelineVertexState(_currentState.VertexBuffers, _currentState.VertexAttribs);

            SignalDirty(DirtyFlags.RenderPipeline);
        }

        public readonly void UpdateBlendDescriptors(int index, BlendDescriptor blend)
        {
            ref var blendState = ref _currentState.Pipeline.Internal.ColorBlendState[index];

            blendState.Enable = blend.Enable;
            blendState.AlphaBlendOperation = blend.AlphaOp.Convert();
            blendState.RgbBlendOperation = blend.ColorOp.Convert();
            blendState.SourceAlphaBlendFactor = blend.AlphaSrcFactor.Convert();
            blendState.DestinationAlphaBlendFactor = blend.AlphaDstFactor.Convert();
            blendState.SourceRGBBlendFactor = blend.ColorSrcFactor.Convert();
            blendState.DestinationRGBBlendFactor = blend.ColorDstFactor.Convert();

            if (blendState.WriteMask == 0)
            {
                _currentState.StoredBlend[index] = blendState;

                blendState.Swap(new ColorBlendStateUid());
            }

            _currentState.BlendColor = blend.BlendConstant;

            SignalDirty(DirtyFlags.RenderPipeline);
        }

        public void UpdateStencilState(StencilTestDescriptor stencilTest)
        {
            ref DepthStencilUid uid = ref _currentState.DepthStencilUid;

            uid.FrontFace = new StencilUid
            {
                StencilFailureOperation = stencilTest.FrontSFail.Convert(),
                DepthFailureOperation = stencilTest.FrontDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.FrontDpPass.Convert(),
                StencilCompareFunction = stencilTest.FrontFunc.Convert(),
                ReadMask = (uint)stencilTest.FrontFuncMask,
                WriteMask = (uint)stencilTest.FrontMask
            };

            uid.BackFace = new StencilUid
            {
                StencilFailureOperation = stencilTest.BackSFail.Convert(),
                DepthFailureOperation = stencilTest.BackDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.BackDpPass.Convert(),
                StencilCompareFunction = stencilTest.BackFunc.Convert(),
                ReadMask = (uint)stencilTest.BackFuncMask,
                WriteMask = (uint)stencilTest.BackMask
            };

            uid.StencilTestEnabled = stencilTest.TestEnable;

            UpdateStencilRefValue(stencilTest.FrontFuncRef, stencilTest.BackFuncRef);

            SignalDirty(DirtyFlags.DepthStencil);
        }

        public readonly void UpdateDepthState(DepthTestDescriptor depthTest)
        {
            ref DepthStencilUid uid = ref _currentState.DepthStencilUid;

            uid.DepthCompareFunction = depthTest.TestEnable ? depthTest.Func.Convert() : MTLCompareFunction.Always;
            uid.DepthWriteEnabled = depthTest.TestEnable && depthTest.WriteEnable;

            SignalDirty(DirtyFlags.DepthStencil);
        }

        public readonly void UpdateDepthClamp(bool clamp)
        {
            _currentState.DepthClipMode = clamp ? MTLDepthClipMode.Clamp : MTLDepthClipMode.Clip;

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetDepthClamp(renderCommandEncoder);
                return;
            }

            SignalDirty(DirtyFlags.DepthClamp);
        }

        public readonly void UpdateDepthBias(float depthBias, float slopeScale, float clamp)
        {
            _currentState.DepthBias = depthBias;
            _currentState.SlopeScale = slopeScale;
            _currentState.Clamp = clamp;

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetDepthBias(renderCommandEncoder);
                return;
            }

            SignalDirty(DirtyFlags.DepthBias);
        }

        public void UpdateScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            for (int i = 0; i < regions.Length; i++)
            {
                var region = regions[i];

                _currentState.Scissors[i] = new MTLScissorRect
                {
                    height = (ulong)region.Height,
                    width = (ulong)region.Width,
                    x = (ulong)region.X,
                    y = (ulong)region.Y
                };
            }

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetScissors(renderCommandEncoder);
                return;
            }

            SignalDirty(DirtyFlags.Scissors);
        }

        public void UpdateViewports(ReadOnlySpan<Viewport> viewports)
        {
            static float Clamp(float value)
            {
                return Math.Clamp(value, 0f, 1f);
            }

            for (int i = 0; i < viewports.Length; i++)
            {
                var viewport = viewports[i];
                // Y coordinate is inverted
                _currentState.Viewports[i] = new MTLViewport
                {
                    originX = viewport.Region.X,
                    originY = viewport.Region.Y + viewport.Region.Height,
                    width = viewport.Region.Width,
                    height = -viewport.Region.Height,
                    znear = Clamp(viewport.DepthNear),
                    zfar = Clamp(viewport.DepthFar)
                };
            }

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetViewports(renderCommandEncoder);
                return;
            }

            SignalDirty(DirtyFlags.Viewports);
        }

        public readonly void UpdateVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            for (int i = 0; i < Constants.MaxVertexBuffers; i++)
            {
                if (i < vertexBuffers.Length)
                {
                    var vertexBuffer = vertexBuffers[i];

                    _currentState.VertexBuffers[i] = new VertexBufferState(
                        vertexBuffer.Buffer.Handle,
                        vertexBuffer.Buffer.Offset,
                        vertexBuffer.Buffer.Size,
                        vertexBuffer.Divisor,
                        vertexBuffer.Stride);
                }
                else
                {
                    _currentState.VertexBuffers[i] = VertexBufferState.Null;
                }
            }

            // Update the buffers on the pipeline
            UpdatePipelineVertexState(_currentState.VertexBuffers, _currentState.VertexAttribs);

            SignalDirty(DirtyFlags.RenderPipeline);
        }

        public readonly void UpdateUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            foreach (BufferAssignment assignment in buffers)
            {
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> mtlBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(buffer.Handle, buffer.Write);

                _currentState.UniformBufferRefs[index] = new BufferRef(mtlBuffer, ref buffer);
            }

            SignalDirty(DirtyFlags.Uniforms);
        }

        public readonly void UpdateStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            foreach (BufferAssignment assignment in buffers)
            {
                var buffer = assignment.Range;
                int index = assignment.Binding;

                Auto<DisposableBuffer> mtlBuffer = buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(buffer.Handle, buffer.Write);

                _currentState.StorageBufferRefs[index] = new BufferRef(mtlBuffer, ref buffer);
            }

            SignalDirty(DirtyFlags.Storages);
        }

        public readonly void UpdateStorageBuffers(int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var mtlBuffer = buffers[i];
                int index = first + i;

                _currentState.StorageBufferRefs[index] = new BufferRef(mtlBuffer);
            }

            SignalDirty(DirtyFlags.Storages);
        }

        public void UpdateCullMode(bool enable, Face face)
        {
            var dirtyScissor = (face == Face.FrontAndBack) != _currentState.CullBoth;

            _currentState.CullMode = enable ? face.Convert() : MTLCullMode.None;
            _currentState.CullBoth = face == Face.FrontAndBack;

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetCullMode(renderCommandEncoder);
                SetScissors(renderCommandEncoder);
                return;
            }

            // Mark dirty
            SignalDirty(DirtyFlags.CullMode);

            if (dirtyScissor)
            {
                SignalDirty(DirtyFlags.Scissors);
            }
        }

        public readonly void UpdateFrontFace(FrontFace frontFace)
        {
            _currentState.Winding = frontFace.Convert();

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetFrontFace(renderCommandEncoder);
                return;
            }

            SignalDirty(DirtyFlags.FrontFace);
        }

        private readonly void UpdateStencilRefValue(int frontRef, int backRef)
        {
            _currentState.FrontRefValue = frontRef;
            _currentState.BackRefValue = backRef;

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetStencilRefValue(renderCommandEncoder);
            }

            SignalDirty(DirtyFlags.StencilRef);
        }

        public readonly void UpdateTextureAndSampler(ShaderStage stage, int binding, TextureBase texture, Sampler sampler)
        {
            if (texture != null)
            {
                _currentState.TextureRefs[binding] = new(stage, texture, sampler);
            }
            else
            {
                _currentState.TextureRefs[binding] = default;
            }

            SignalDirty(DirtyFlags.Textures);
        }

        public readonly void UpdateImage(ShaderStage stage, int binding, TextureBase image)
        {
            if (image is Texture view)
            {
                _currentState.ImageRefs[binding] = new(stage, view);
            }
            else
            {
                _currentState.ImageRefs[binding] = default;
            }

            SignalDirty(DirtyFlags.Images);
        }

        public readonly void UpdateTextureArray(ShaderStage stage, int binding, TextureArray array)
        {
            ref EncoderState.ArrayRef<TextureArray> arrayRef = ref GetArrayRef(ref _currentState.TextureArrayRefs, binding, ArrayGrowthSize);

            if (arrayRef.Stage != stage || arrayRef.Array != array)
            {
                arrayRef = new EncoderState.ArrayRef<TextureArray>(stage, array);

                SignalDirty(DirtyFlags.Textures);
            }
        }

        public readonly void UpdateTextureArraySeparate(ShaderStage stage, int setIndex, TextureArray array)
        {
            ref EncoderState.ArrayRef<TextureArray> arrayRef = ref GetArrayRef(ref _currentState.TextureArrayExtraRefs, setIndex - MetalRenderer.TotalSets);

            if (arrayRef.Stage != stage || arrayRef.Array != array)
            {
                arrayRef = new EncoderState.ArrayRef<TextureArray>(stage, array);

                SignalDirty(DirtyFlags.Textures);
            }
        }

        public readonly void UpdateImageArray(ShaderStage stage, int binding, ImageArray array)
        {
            ref EncoderState.ArrayRef<ImageArray> arrayRef = ref GetArrayRef(ref _currentState.ImageArrayRefs, binding, ArrayGrowthSize);

            if (arrayRef.Stage != stage || arrayRef.Array != array)
            {
                arrayRef = new EncoderState.ArrayRef<ImageArray>(stage, array);

                SignalDirty(DirtyFlags.Images);
            }
        }

        public readonly void UpdateImageArraySeparate(ShaderStage stage, int setIndex, ImageArray array)
        {
            ref EncoderState.ArrayRef<ImageArray> arrayRef = ref GetArrayRef(ref _currentState.ImageArrayExtraRefs, setIndex - MetalRenderer.TotalSets);

            if (arrayRef.Stage != stage || arrayRef.Array != array)
            {
                arrayRef = new EncoderState.ArrayRef<ImageArray>(stage, array);

                SignalDirty(DirtyFlags.Images);
            }
        }

        private static ref EncoderState.ArrayRef<T> GetArrayRef<T>(ref EncoderState.ArrayRef<T>[] array, int index, int growthSize = 1)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            if (array.Length <= index)
            {
                Array.Resize(ref array, index + growthSize);
            }

            return ref array[index];
        }

        private readonly void SetDepthStencilState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            MTLDepthStencilState state = _depthStencilCache.GetOrCreate(_currentState.DepthStencilUid);

            renderCommandEncoder.SetDepthStencilState(state);
        }

        private readonly void SetDepthClamp(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetDepthClipMode(_currentState.DepthClipMode);
        }

        private readonly void SetDepthBias(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetDepthBias(_currentState.DepthBias, _currentState.SlopeScale, _currentState.Clamp);
        }

        private unsafe void SetScissors(MTLRenderCommandEncoder renderCommandEncoder)
        {
            var isTriangles = (_currentState.Topology == PrimitiveTopology.Triangles) ||
                              (_currentState.Topology == PrimitiveTopology.TriangleStrip);

            if (_currentState.CullBoth && isTriangles)
            {
                renderCommandEncoder.SetScissorRect(new MTLScissorRect { x = 0, y = 0, width = 0, height = 0 });
            }
            else
            {
                if (_currentState.Scissors.Length > 0)
                {
                    fixed (MTLScissorRect* pMtlScissors = _currentState.Scissors)
                    {
                        renderCommandEncoder.SetScissorRects((IntPtr)pMtlScissors, (ulong)_currentState.Scissors.Length);
                    }
                }
            }
        }

        private readonly unsafe void SetViewports(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Viewports.Length > 0)
            {
                fixed (MTLViewport* pMtlViewports = _currentState.Viewports)
                {
                    renderCommandEncoder.SetViewports((IntPtr)pMtlViewports, (ulong)_currentState.Viewports.Length);
                }
            }
        }

        private readonly void UpdatePipelineVertexState(VertexBufferState[] bufferDescriptors, VertexAttribDescriptor[] attribDescriptors)
        {
            ref PipelineState pipeline = ref _currentState.Pipeline;
            uint indexMask = 0;

            for (int i = 0; i < attribDescriptors.Length; i++)
            {
                ref var attrib = ref pipeline.Internal.VertexAttributes[i];

                if (attribDescriptors[i].IsZero)
                {
                    attrib.Format = attribDescriptors[i].Format.Convert();
                    indexMask |= 1u << (int)Constants.ZeroBufferIndex;
                    attrib.BufferIndex = Constants.ZeroBufferIndex;
                    attrib.Offset = 0;
                }
                else
                {
                    attrib.Format = attribDescriptors[i].Format.Convert();
                    indexMask |= 1u << attribDescriptors[i].BufferIndex;
                    attrib.BufferIndex = (ulong)attribDescriptors[i].BufferIndex;
                    attrib.Offset = (ulong)attribDescriptors[i].Offset;
                }
            }

            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                ref var layout = ref pipeline.Internal.VertexBindings[i];

                if ((indexMask & (1u << i)) != 0)
                {
                    layout.Stride = (uint)bufferDescriptors[i].Stride;

                    if (layout.Stride == 0)
                    {
                        layout.Stride = 1;
                        layout.StepFunction = MTLVertexStepFunction.Constant;
                        layout.StepRate = 0;
                    }
                    else
                    {
                        if (bufferDescriptors[i].Divisor > 0)
                        {
                            layout.StepFunction = MTLVertexStepFunction.PerInstance;
                            layout.StepRate = (uint)bufferDescriptors[i].Divisor;
                        }
                        else
                        {
                            layout.StepFunction = MTLVertexStepFunction.PerVertex;
                            layout.StepRate = 1;
                        }
                    }
                }
                else
                {
                    layout = new();
                }
            }

            ref var zeroBufLayout = ref pipeline.Internal.VertexBindings[(int)Constants.ZeroBufferIndex];

            // Zero buffer
            if ((indexMask & (1u << (int)Constants.ZeroBufferIndex)) != 0)
            {
                zeroBufLayout.Stride = 1;
                zeroBufLayout.StepFunction = MTLVertexStepFunction.Constant;
                zeroBufLayout.StepRate = 0;
            }
            else
            {
                zeroBufLayout = new();
            }

            pipeline.VertexAttributeDescriptionsCount = (uint)attribDescriptors.Length;
            pipeline.VertexBindingDescriptionsCount = Constants.ZeroBufferIndex + 1; // TODO: move this out?
        }

        private readonly void SetVertexBuffers(VertexBufferState[] bufferStates, ref readonly RenderEncoderResources resources)
        {
            for (int i = 0; i < bufferStates.Length; i++)
            {
                (MTLBuffer mtlBuffer, int offset) = bufferStates[i].GetVertexBuffer(_bufferManager, _pipeline.Cbs);

                if (mtlBuffer.NativePtr != IntPtr.Zero)
                {
                    resources.VertexBuffers.Add(new BufferResource(mtlBuffer, (ulong)offset, (ulong)i));
                }
            }

            Auto<DisposableBuffer> autoZeroBuffer = _zeroBuffer == BufferHandle.Null
                ? null
                : _bufferManager.GetBuffer(_zeroBuffer, false);

            if (autoZeroBuffer == null)
            {
                return;
            }

            var zeroMtlBuffer = autoZeroBuffer.Get(_pipeline.Cbs).Value;
            resources.VertexBuffers.Add(new BufferResource(zeroMtlBuffer, 0, Constants.ZeroBufferIndex));
        }

        private readonly void UpdateAndBind(Program program, uint setIndex, ref readonly RenderEncoderResources resources)
        {
            var bindingSegments = program.BindingSegments[setIndex];

            if (bindingSegments.Length == 0)
            {
                return;
            }

            ScopedTemporaryBuffer vertArgBuffer = default;
            ScopedTemporaryBuffer fragArgBuffer = default;

            if (program.ArgumentBufferSizes[setIndex] > 0)
            {
                vertArgBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, program.ArgumentBufferSizes[setIndex] * sizeof(ulong));
            }

            if (program.FragArgumentBufferSizes[setIndex] > 0)
            {
                fragArgBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, program.FragArgumentBufferSizes[setIndex] * sizeof(ulong));
            }

            Span<ulong> vertResourceIds = stackalloc ulong[program.ArgumentBufferSizes[setIndex]];
            Span<ulong> fragResourceIds = stackalloc ulong[program.FragArgumentBufferSizes[setIndex]];

            var vertResourceIdIndex = 0;
            var fragResourceIdIndex = 0;

            foreach (ResourceBindingSegment segment in bindingSegments)
            {
                int binding = segment.Binding;
                int count = segment.Count;

                switch (setIndex)
                {
                    case Constants.ConstantBuffersSetIndex:
                        for (int i = 0; i < count; i++)
                        {
                            int index = binding + i;

                            ref BufferRef buffer = ref _currentState.UniformBufferRefs[index];

                            var range = buffer.Range;
                            var autoBuffer = buffer.Buffer;
                            var offset = 0;

                            if (autoBuffer == null)
                            {
                                continue;
                            }

                            MTLBuffer mtlBuffer;

                            if (range.HasValue)
                            {
                                offset = range.Value.Offset;
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Value.Size, range.Value.Write).Value;

                            }
                            else
                            {
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs).Value;
                            }

                            MTLRenderStages renderStages = 0;

                            if ((segment.Stages & ResourceStages.Vertex) != 0)
                            {
                                vertResourceIds[vertResourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                vertResourceIdIndex++;

                                renderStages |= MTLRenderStages.RenderStageVertex;
                            }

                            if ((segment.Stages & ResourceStages.Fragment) != 0)
                            {
                                fragResourceIds[fragResourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                fragResourceIdIndex++;

                                renderStages |= MTLRenderStages.RenderStageFragment;
                            }

                            resources.Resources.Add(new Resource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read, renderStages));
                        }
                        break;
                    case Constants.StorageBuffersSetIndex:
                        for (int i = 0; i < count; i++)
                        {
                            int index = binding + i;

                            ref BufferRef buffer = ref _currentState.StorageBufferRefs[index];

                            var range = buffer.Range;
                            var autoBuffer = buffer.Buffer;
                            var offset = 0;

                            if (autoBuffer == null)
                            {
                                continue;
                            }

                            MTLBuffer mtlBuffer;

                            if (range.HasValue)
                            {
                                offset = range.Value.Offset;
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Value.Size, range.Value.Write).Value;

                            }
                            else
                            {
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs).Value;
                            }

                            MTLRenderStages renderStages = 0;

                            if ((segment.Stages & ResourceStages.Vertex) != 0)
                            {
                                vertResourceIds[vertResourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                vertResourceIdIndex++;

                                renderStages |= MTLRenderStages.RenderStageVertex;
                            }

                            if ((segment.Stages & ResourceStages.Fragment) != 0)
                            {
                                fragResourceIds[fragResourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                fragResourceIdIndex++;

                                renderStages |= MTLRenderStages.RenderStageFragment;
                            }

                            resources.Resources.Add(new Resource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read, renderStages));
                        }
                        break;
                    case Constants.TexturesSetIndex:
                        if (!segment.IsArray)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                int index = binding + i;

                                ref var texture = ref _currentState.TextureRefs[index];

                                var storage = texture.Storage;

                                if (storage == null)
                                {
                                    continue;
                                }

                                if (storage is TextureBuffer textureBuffer)
                                {
                                    textureBuffer.RebuildStorage(false);
                                }

                                var mtlTexture = storage.GetHandle();

                                MTLRenderStages renderStages = 0;

                                if ((segment.Stages & ResourceStages.Vertex) != 0)
                                {
                                    vertResourceIds[vertResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                    vertResourceIdIndex++;

                                    if (texture.Sampler != null)
                                    {
                                        vertResourceIds[vertResourceIdIndex] = texture.Sampler.GetSampler().GpuResourceID._impl;
                                        vertResourceIdIndex++;
                                    }

                                    renderStages |= MTLRenderStages.RenderStageVertex;
                                }

                                if ((segment.Stages & ResourceStages.Fragment) != 0)
                                {
                                    fragResourceIds[fragResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                    fragResourceIdIndex++;

                                    if (texture.Sampler != null)
                                    {
                                        fragResourceIds[fragResourceIdIndex] = texture.Sampler.GetSampler().GpuResourceID._impl;
                                        fragResourceIdIndex++;
                                    }

                                    renderStages |= MTLRenderStages.RenderStageFragment;
                                }

                                resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, renderStages));
                            }
                        }
                        else
                        {
                            var textureArray = _currentState.TextureArrayRefs[binding].Array;

                            if (segment.Type != ResourceType.BufferTexture)
                            {
                                var textures = textureArray.GetTextureRefs();
                                var samplers = new Sampler[textures.Length];

                                for (int i = 0; i < textures.Length; i++)
                                {
                                    TextureRef texture = textures[i];

                                    if (texture.Storage == null)
                                    {
                                        continue;
                                    }

                                    var mtlTexture = texture.Storage.GetHandle();
                                    samplers[i] = texture.Sampler;

                                    MTLRenderStages renderStages = 0;

                                    if ((segment.Stages & ResourceStages.Vertex) != 0)
                                    {
                                        vertResourceIds[vertResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        vertResourceIdIndex++;

                                        renderStages |= MTLRenderStages.RenderStageVertex;
                                    }

                                    if ((segment.Stages & ResourceStages.Fragment) != 0)
                                    {
                                        fragResourceIds[fragResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        fragResourceIdIndex++;

                                        renderStages |= MTLRenderStages.RenderStageFragment;
                                    }

                                    resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, renderStages));
                                }

                                foreach (var sampler in samplers)
                                {
                                    if (sampler == null)
                                    {
                                        continue;
                                    }

                                    if ((segment.Stages & ResourceStages.Vertex) != 0)
                                    {
                                        vertResourceIds[vertResourceIdIndex] = sampler.GetSampler().GpuResourceID._impl;
                                        vertResourceIdIndex++;
                                    }

                                    if ((segment.Stages & ResourceStages.Fragment) != 0)
                                    {
                                        fragResourceIds[fragResourceIdIndex] = sampler.GetSampler().GpuResourceID._impl;
                                        fragResourceIdIndex++;
                                    }
                                }
                            }
                            else
                            {
                                var bufferTextures = textureArray.GetBufferTextureRefs();

                                foreach (TextureBuffer bufferTexture in bufferTextures)
                                {
                                    if (bufferTexture == null)
                                    {
                                        continue;
                                    }

                                    bufferTexture.RebuildStorage(false);

                                    var mtlTexture = bufferTexture.GetHandle();

                                    MTLRenderStages renderStages = 0;

                                    if ((segment.Stages & ResourceStages.Vertex) != 0)
                                    {
                                        vertResourceIds[vertResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        vertResourceIdIndex++;

                                        renderStages |= MTLRenderStages.RenderStageVertex;
                                    }

                                    if ((segment.Stages & ResourceStages.Fragment) != 0)
                                    {
                                        fragResourceIds[fragResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        fragResourceIdIndex++;

                                        renderStages |= MTLRenderStages.RenderStageFragment;
                                    }

                                    resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, renderStages));
                                }
                            }
                        }
                        break;
                    case Constants.ImagesSetIndex:
                        if (!segment.IsArray)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                int index = binding + i;

                                ref var image = ref _currentState.ImageRefs[index];

                                var storage = image.Storage;

                                if (storage == null)
                                {
                                    continue;
                                }

                                var mtlTexture = storage.GetHandle();

                                MTLRenderStages renderStages = 0;

                                if ((segment.Stages & ResourceStages.Vertex) != 0)
                                {
                                    vertResourceIds[vertResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                    vertResourceIdIndex++;
                                    renderStages |= MTLRenderStages.RenderStageVertex;
                                }

                                if ((segment.Stages & ResourceStages.Fragment) != 0)
                                {
                                    fragResourceIds[fragResourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                    fragResourceIdIndex++;
                                    renderStages |= MTLRenderStages.RenderStageFragment;
                                }

                                resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read | MTLResourceUsage.Write, renderStages));
                            }
                        }
                        break;
                }
            }

            if (program.ArgumentBufferSizes[setIndex] > 0)
            {
                vertArgBuffer.Holder.SetDataUnchecked(vertArgBuffer.Offset, MemoryMarshal.AsBytes(vertResourceIds));
                var mtlVertArgBuffer = _bufferManager.GetBuffer(vertArgBuffer.Handle, false).Get(_pipeline.Cbs).Value;
                resources.VertexBuffers.Add(new BufferResource(mtlVertArgBuffer, (uint)vertArgBuffer.Range.Offset, SetIndexToBindingIndex(setIndex)));
            }

            if (program.FragArgumentBufferSizes[setIndex] > 0)
            {
                fragArgBuffer.Holder.SetDataUnchecked(fragArgBuffer.Offset, MemoryMarshal.AsBytes(fragResourceIds));
                var mtlFragArgBuffer = _bufferManager.GetBuffer(fragArgBuffer.Handle, false).Get(_pipeline.Cbs).Value;
                resources.FragmentBuffers.Add(new BufferResource(mtlFragArgBuffer, (uint)fragArgBuffer.Range.Offset, SetIndexToBindingIndex(setIndex)));
            }
        }

        private readonly void UpdateAndBind(Program program, uint setIndex, ref readonly ComputeEncoderResources resources)
        {
            var bindingSegments = program.BindingSegments[setIndex];

            if (bindingSegments.Length == 0)
            {
                return;
            }

            ScopedTemporaryBuffer argBuffer = default;

            if (program.ArgumentBufferSizes[setIndex] > 0)
            {
                argBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, program.ArgumentBufferSizes[setIndex] * sizeof(ulong));
            }

            Span<ulong> resourceIds = stackalloc ulong[program.ArgumentBufferSizes[setIndex]];
            var resourceIdIndex = 0;

            foreach (ResourceBindingSegment segment in bindingSegments)
            {
                int binding = segment.Binding;
                int count = segment.Count;

                switch (setIndex)
                {
                    case Constants.ConstantBuffersSetIndex:
                        for (int i = 0; i < count; i++)
                        {
                            int index = binding + i;

                            ref BufferRef buffer = ref _currentState.UniformBufferRefs[index];

                            var range = buffer.Range;
                            var autoBuffer = buffer.Buffer;
                            var offset = 0;

                            if (autoBuffer == null)
                            {
                                continue;
                            }

                            MTLBuffer mtlBuffer;

                            if (range.HasValue)
                            {
                                offset = range.Value.Offset;
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Value.Size, range.Value.Write).Value;

                            }
                            else
                            {
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs).Value;
                            }

                            if ((segment.Stages & ResourceStages.Compute) != 0)
                            {
                                resources.Resources.Add(new Resource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read, 0));
                                resourceIds[resourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                resourceIdIndex++;
                            }
                        }
                        break;
                    case Constants.StorageBuffersSetIndex:
                        for (int i = 0; i < count; i++)
                        {
                            int index = binding + i;

                            ref BufferRef buffer = ref _currentState.StorageBufferRefs[index];

                            var range = buffer.Range;
                            var autoBuffer = buffer.Buffer;
                            var offset = 0;

                            if (autoBuffer == null)
                            {
                                continue;
                            }

                            MTLBuffer mtlBuffer;

                            if (range.HasValue)
                            {
                                offset = range.Value.Offset;
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Value.Size, range.Value.Write).Value;

                            }
                            else
                            {
                                mtlBuffer = autoBuffer.Get(_pipeline.Cbs).Value;
                            }

                            if ((segment.Stages & ResourceStages.Compute) != 0)
                            {
                                resources.Resources.Add(new Resource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read | MTLResourceUsage.Write, 0));
                                resourceIds[resourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                resourceIdIndex++;
                            }
                        }
                        break;
                    case Constants.TexturesSetIndex:
                        if (!segment.IsArray)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                int index = binding + i;

                                ref var texture = ref _currentState.TextureRefs[index];

                                var storage = texture.Storage;

                                if (storage == null)
                                {
                                    continue;
                                }

                                if (storage is TextureBuffer textureBuffer)
                                {
                                    textureBuffer.RebuildStorage(false);
                                }

                                var mtlTexture = storage.GetHandle();

                                if ((segment.Stages & ResourceStages.Compute) != 0)
                                {
                                    resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, 0));
                                    resourceIds[resourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                    resourceIdIndex++;

                                    if (texture.Sampler != null)
                                    {
                                        resourceIds[resourceIdIndex] = texture.Sampler.GetSampler().GpuResourceID._impl;
                                        resourceIdIndex++;
                                    }
                                }
                            }
                        }
                        else
                        {
                            var textureArray = _currentState.TextureArrayRefs[binding].Array;

                            if (segment.Type != ResourceType.BufferTexture)
                            {
                                var textures = textureArray.GetTextureRefs();
                                var samplers = new Sampler[textures.Length];

                                for (int i = 0; i < textures.Length; i++)
                                {
                                    TextureRef texture = textures[i];

                                    if (texture.Storage == null)
                                    {
                                        continue;
                                    }

                                    var mtlTexture = texture.Storage.GetHandle();

                                    if ((segment.Stages & ResourceStages.Compute) != 0)
                                    {
                                        resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, 0));
                                        resourceIds[resourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        resourceIdIndex++;

                                        samplers[i] = texture.Sampler;
                                    }
                                }

                                foreach (var sampler in samplers)
                                {
                                    if (sampler != null)
                                    {
                                        resourceIds[resourceIdIndex] = sampler.GetSampler().GpuResourceID._impl;
                                        resourceIdIndex++;
                                    }
                                }
                            }
                            else
                            {
                                var bufferTextures = textureArray.GetBufferTextureRefs();

                                foreach (TextureBuffer bufferTexture in bufferTextures)
                                {
                                    if (bufferTexture == null)
                                    {
                                        continue;
                                    }

                                    bufferTexture.RebuildStorage(false);

                                    var mtlTexture = bufferTexture.GetHandle();

                                    if ((segment.Stages & ResourceStages.Compute) != 0)
                                    {
                                        resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, 0));
                                        resourceIds[resourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        resourceIdIndex++;
                                    }
                                }
                            }
                        }
                        break;
                    case Constants.ImagesSetIndex:
                        if (!segment.IsArray)
                        {
                            if (segment.Type != ResourceType.BufferTexture)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    int index = binding + i;

                                    ref var image = ref _currentState.ImageRefs[index];

                                    var storage = image.Storage;

                                    if (storage == null)
                                    {
                                        continue;
                                    }

                                    var mtlTexture = storage.GetHandle();

                                    if ((segment.Stages & ResourceStages.Compute) != 0)
                                    {
                                        resources.Resources.Add(new Resource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read | MTLResourceUsage.Write, 0));
                                        resourceIds[resourceIdIndex] = mtlTexture.GpuResourceID._impl;
                                        resourceIdIndex++;
                                    }
                                }
                            }
                        }
                        break;
                }
            }

            if (program.ArgumentBufferSizes[setIndex] > 0)
            {
                argBuffer.Holder.SetDataUnchecked(argBuffer.Offset, MemoryMarshal.AsBytes(resourceIds));
                var mtlArgBuffer = _bufferManager.GetBuffer(argBuffer.Handle, false).Get(_pipeline.Cbs).Value;
                resources.Buffers.Add(new BufferResource(mtlArgBuffer, (uint)argBuffer.Range.Offset, SetIndexToBindingIndex(setIndex)));
            }
        }

        private static uint SetIndexToBindingIndex(uint setIndex)
        {
            return setIndex switch
            {
                Constants.ConstantBuffersSetIndex => Constants.ConstantBuffersIndex,
                Constants.StorageBuffersSetIndex => Constants.StorageBuffersIndex,
                Constants.TexturesSetIndex => Constants.TexturesIndex,
                Constants.ImagesSetIndex => Constants.ImagesIndex,
            };
        }

        private readonly void SetCullMode(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetCullMode(_currentState.CullMode);
        }

        private readonly void SetFrontFace(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetFrontFacingWinding(_currentState.Winding);
        }

        private readonly void SetStencilRefValue(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetStencilReferenceValues((uint)_currentState.FrontRefValue, (uint)_currentState.BackRefValue);
        }
    }
}
