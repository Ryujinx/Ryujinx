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
        private readonly MTLDevice _device;
        private readonly Pipeline _pipeline;
        private readonly BufferManager _bufferManager;

        private readonly DepthStencilCache _depthStencilCache;

        private readonly EncoderState _mainState = new();
        private EncoderState _currentState;

        public readonly Auto<DisposableBuffer> IndexBuffer => _currentState.IndexBuffer;
        public readonly MTLIndexType IndexType => _currentState.IndexType;
        public readonly ulong IndexBufferOffset => _currentState.IndexBufferOffset;
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

        public void Dispose()
        {
            // State

            _depthStencilCache.Dispose();
        }

        public EncoderState SwapState(EncoderState state, DirtyFlags flags = DirtyFlags.All)
        {
            _currentState = state ?? _mainState;

            _currentState.Dirty |= flags;

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

        public void RestorePredrawState(PredrawState state)
        {
            _currentState.CullMode = state.CullMode;
            _currentState.DepthStencilUid = state.DepthStencilUid;
            _currentState.Topology = state.Topology;
            _currentState.Viewports = state.Viewports;

            _currentState.Dirty |= DirtyFlags.CullMode | DirtyFlags.DepthStencil | DirtyFlags.Viewports;
        }

        public void SetClearLoadAction(bool clear)
        {
            _currentState.ClearLoadAction = clear;
        }

        public MTLRenderCommandEncoder CreateRenderCommandEncoder()
        {
            // Initialise Pass & State
            var renderPassDescriptor = new MTLRenderPassDescriptor();

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                if (_currentState.RenderTargets[i] != null)
                {
                    var passAttachment = renderPassDescriptor.ColorAttachments.Object((ulong)i);
                    passAttachment.Texture = _currentState.RenderTargets[i].GetHandle();
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

            // Mark all state as dirty to ensure it is set on the encoder
            _currentState.Dirty |= DirtyFlags.RenderAll;

            // Cleanup
            renderPassDescriptor.Dispose();

            return renderCommandEncoder;
        }

        public MTLComputeCommandEncoder CreateComputeCommandEncoder()
        {
            var descriptor = new MTLComputePassDescriptor();
            var computeCommandEncoder = _pipeline.CommandBuffer.ComputeCommandEncoder(descriptor);

            // Mark all state as dirty to ensure it is set on the encoder
            _currentState.Dirty |= DirtyFlags.ComputeAll;

            // Cleanup
            descriptor.Dispose();

            return computeCommandEncoder;
        }

        public void RebindRenderState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if ((_currentState.Dirty & DirtyFlags.RenderPipeline) != 0)
            {
                SetRenderPipelineState(renderCommandEncoder);
                SetVertexBuffers(renderCommandEncoder, _currentState.VertexBuffers);
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

            if ((_currentState.Dirty & DirtyFlags.Uniforms) != 0)
            {
                UpdateAndBind(renderCommandEncoder, _currentState.RenderProgram, MetalRenderer.UniformSetIndex);
            }

            if ((_currentState.Dirty & DirtyFlags.Storages) != 0)
            {
                UpdateAndBind(renderCommandEncoder, _currentState.RenderProgram, MetalRenderer.StorageSetIndex);
            }

            if ((_currentState.Dirty & DirtyFlags.Textures) != 0)
            {
                UpdateAndBind(renderCommandEncoder, _currentState.RenderProgram, MetalRenderer.TextureSetIndex);
            }

            // if (_currentState.Dirty.HasFlag(DirtyFlags.Images))
            // {
            //     UpdateAndBind(renderCommandEncoder, _currentState.RenderProgram, MetalRenderer.ImageSetIndex);
            // }

            _currentState.Dirty &= ~DirtyFlags.RenderAll;
        }

        public void RebindComputeState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.Dirty.HasFlag(DirtyFlags.ComputePipeline))
            {
                SetComputePipelineState(computeCommandEncoder);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Uniforms))
            {
                UpdateAndBind(computeCommandEncoder, _currentState.ComputeProgram, MetalRenderer.UniformSetIndex);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Storages))
            {
                UpdateAndBind(computeCommandEncoder, _currentState.ComputeProgram, MetalRenderer.StorageSetIndex);
            }

            if (_currentState.Dirty.HasFlag(DirtyFlags.Textures))
            {
                UpdateAndBind(computeCommandEncoder, _currentState.ComputeProgram, MetalRenderer.TextureSetIndex);
            }

            // if (_currentState.Dirty.HasFlag(DirtyFlags.Images))
            // {
            //     UpdateAndBind(computeCommandEncoder, _currentState.ComputeProgram, MetalRenderer.ImageSetIndex);
            // }

            _currentState.Dirty &= ~DirtyFlags.ComputeAll;
        }

        private void SetRenderPipelineState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            MTLRenderPipelineState pipelineState = _currentState.Pipeline.CreateRenderPipeline(_device, _currentState.RenderProgram);

            renderCommandEncoder.SetRenderPipelineState(pipelineState);

            renderCommandEncoder.SetBlendColor(
                _currentState.BlendColor.Red,
                _currentState.BlendColor.Green,
                _currentState.BlendColor.Blue,
                _currentState.BlendColor.Alpha);
        }

        private void SetComputePipelineState(MTLComputeCommandEncoder computeCommandEncoder)
        {
            if (_currentState.ComputeProgram == null)
            {
                return;
            }

            var pipelineState = PipelineState.CreateComputePipeline(_device, _currentState.ComputeProgram);

            computeCommandEncoder.SetComputePipelineState(pipelineState);
        }

        public void UpdateIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                if (type == GAL.IndexType.UByte)
                {
                    _currentState.IndexType = MTLIndexType.UInt16;
                    _currentState.IndexBufferOffset = 0;
                    _currentState.IndexBuffer = _bufferManager.GetBufferI8ToI16(_pipeline.Cbs, buffer.Handle, buffer.Offset, buffer.Size);
                }
                else
                {
                    _currentState.IndexType = type.Convert();
                    _currentState.IndexBufferOffset = (ulong)buffer.Offset;
                    _currentState.IndexBuffer = _bufferManager.GetBuffer(buffer.Handle, false);
                }
            }
        }

        public void UpdatePrimitiveTopology(PrimitiveTopology topology)
        {
            _currentState.Topology = topology;
        }

        public void UpdateProgram(IProgram program)
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

                _currentState.Dirty |= DirtyFlags.RenderPipeline | DirtyFlags.ArgBuffers;
            }
            else if (prg.ComputeFunction != IntPtr.Zero)
            {
                _currentState.ComputeProgram = prg;

                _currentState.Dirty |= DirtyFlags.ComputePipeline | DirtyFlags.ArgBuffers;
            }
        }

        public void UpdateRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _currentState.FramebufferUsingColorWriteMask = false;
            UpdateRenderTargetsInternal(colors, depthStencil);
        }

        public void UpdateRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
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

                        mtlBlend = new ColorBlendStateUid();
                    }
                    else if (mtlBlend.WriteMask == 0)
                    {
                        mtlBlend = _currentState.StoredBlend[i];
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

        private void UpdateRenderTargetsInternal(ITexture[] colors, ITexture depthStencil)
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

        private void MaskOut(ITexture[] colors, ITexture depthStencil)
        {
            if (!_currentState.FramebufferUsingColorWriteMask)
            {
                _currentState.PreMaskRenderTargets = colors;
                _currentState.PreMaskDepthStencil = depthStencil;
            }

            // If true, then the framebuffer must be recreated when the mask changes.
            _currentState.FramebufferUsingColorWriteMask = true;
        }

        public void UpdateVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            vertexAttribs.CopyTo(_currentState.VertexAttribs);

            // Update the buffers on the pipeline
            UpdatePipelineVertexState(_currentState.VertexBuffers, _currentState.VertexAttribs);

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.RenderPipeline;
        }

        public void UpdateBlendDescriptors(int index, BlendDescriptor blend)
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

                blendState = new ColorBlendStateUid();
            }

            _currentState.BlendColor = blend.BlendConstant;

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.RenderPipeline;
        }

        // Inlineable
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

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthStencil;
        }

        public void UpdateDepthState(DepthTestDescriptor depthTest)
        {
            ref DepthStencilUid uid = ref _currentState.DepthStencilUid;

            uid.DepthCompareFunction = depthTest.TestEnable ? depthTest.Func.Convert() : MTLCompareFunction.Always;
            uid.DepthWriteEnabled = depthTest.TestEnable && depthTest.WriteEnable;

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthStencil;
        }

        // Inlineable
        public void UpdateDepthClamp(bool clamp)
        {
            _currentState.DepthClipMode = clamp ? MTLDepthClipMode.Clamp : MTLDepthClipMode.Clip;

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetDepthClamp(renderCommandEncoder);
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthClamp;
        }

        // Inlineable
        public void UpdateDepthBias(float depthBias, float slopeScale, float clamp)
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

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.DepthBias;
        }

        // Inlineable
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

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.Scissors;
        }

        // Inlineable
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

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.Viewports;
        }

        public void UpdateVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            vertexBuffers.CopyTo(_currentState.VertexBuffers);

            // Update the buffers on the pipeline
            UpdatePipelineVertexState(_currentState.VertexBuffers, _currentState.VertexAttribs);

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.RenderPipeline;
        }

        public void UpdateUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
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

            _currentState.Dirty |= DirtyFlags.Uniforms;
        }

        public void UpdateStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
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

            _currentState.Dirty |= DirtyFlags.Storages;
        }

        public void UpdateStorageBuffers(int first, ReadOnlySpan<Auto<DisposableBuffer>> buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var mtlBuffer = buffers[i];
                int index = first + i;

                _currentState.StorageBufferRefs[index] = new BufferRef(mtlBuffer);
            }

            _currentState.Dirty |= DirtyFlags.Storages;
        }

        // Inlineable
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
            _currentState.Dirty |= DirtyFlags.CullMode;

            if (dirtyScissor)
            {
                _currentState.Dirty |= DirtyFlags.Scissors;
            }
        }

        // Inlineable
        public void UpdateFrontFace(FrontFace frontFace)
        {
            _currentState.Winding = frontFace.Convert();

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetFrontFace(renderCommandEncoder);
                return;
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.FrontFace;
        }

        private void UpdateStencilRefValue(int frontRef, int backRef)
        {
            _currentState.FrontRefValue = frontRef;
            _currentState.BackRefValue = backRef;

            // Inline update
            if (_pipeline.Encoders.TryGetRenderEncoder(out MTLRenderCommandEncoder renderCommandEncoder))
            {
                SetStencilRefValue(renderCommandEncoder);
            }

            // Mark dirty
            _currentState.Dirty |= DirtyFlags.StencilRef;
        }

        public void UpdateTextureAndSampler(ShaderStage stage, ulong binding, TextureBase texture, Sampler sampler)
        {
            if (texture is TextureBuffer textureBuffer)
            {
                // TODO: Texture buffers
            }
            else if (texture is Texture view)
            {
                _currentState.TextureRefs[binding] = new(stage, view, sampler);
            }
            else
            {
                _currentState.TextureRefs[binding] = default;
            }

            _currentState.Dirty |= DirtyFlags.Textures;
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
                renderCommandEncoder.SetScissorRect(new MTLScissorRect { x = 0, y = 0, width = 0, height = 0});
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

        private unsafe void SetViewports(MTLRenderCommandEncoder renderCommandEncoder)
        {
            if (_currentState.Viewports.Length > 0)
            {
                fixed (MTLViewport* pMtlViewports = _currentState.Viewports)
                {
                    renderCommandEncoder.SetViewports((IntPtr)pMtlViewports, (ulong)_currentState.Viewports.Length);
                }
            }
        }

        private void UpdatePipelineVertexState(VertexBufferDescriptor[] bufferDescriptors, VertexAttribDescriptor[] attribDescriptors)
        {
            ref PipelineState pipeline = ref _currentState.Pipeline;
            uint indexMask = 0;
            int descriptorCount = 0;

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

        private void SetVertexBuffers(MTLRenderCommandEncoder renderCommandEncoder, VertexBufferDescriptor[] bufferDescriptors)
        {
            for (int i = 0; i < bufferDescriptors.Length; i++)
            {
                Auto<DisposableBuffer> autoBuffer = bufferDescriptors[i].Buffer.Handle == BufferHandle.Null
                    ? null
                    : _bufferManager.GetBuffer(bufferDescriptors[i].Buffer.Handle, bufferDescriptors[i].Buffer.Write);

                var range = bufferDescriptors[i].Buffer;
                var offset = range.Offset;

                if (autoBuffer == null)
                {
                    continue;
                }

                var mtlBuffer = autoBuffer.Get(_pipeline.Cbs, offset, range.Size, range.Write).Value;
                renderCommandEncoder.SetVertexBuffer(mtlBuffer, (ulong)offset, (ulong)i);
            }

            Auto<DisposableBuffer> autoZeroBuffer = _zeroBuffer == BufferHandle.Null
                ? null
                : _bufferManager.GetBuffer(_zeroBuffer, false);

            if (autoZeroBuffer == null)
            {
                return;
            }

            var zeroMtlBuffer = autoZeroBuffer.Get(_pipeline.Cbs).Value;
            renderCommandEncoder.SetVertexBuffer(zeroMtlBuffer, 0, Constants.ZeroBufferIndex);
        }

        private void UpdateAndBind(MTLRenderCommandEncoder renderCommandEncoder, Program program, int setIndex)
        {
            var bindingSegments = program.BindingSegments[setIndex];

            if (bindingSegments.Length == 0)
            {
                return;
            }

            var vertArgBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, program.ArgumentBufferSizes[setIndex] * sizeof(ulong));
            var fragArgBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, program.FragArgumentBufferSizes[setIndex] * sizeof(ulong));

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
                    case MetalRenderer.UniformSetIndex:
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

                            renderCommandEncoder.UseResource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read, renderStages);
                        }
                        break;
                    case MetalRenderer.StorageSetIndex:
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

                            if (segment.Stages.HasFlag(ResourceStages.Vertex))
                            {
                                vertResourceIds[vertResourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                vertResourceIdIndex++;

                                renderStages |= MTLRenderStages.RenderStageVertex;
                            }

                            if (segment.Stages.HasFlag(ResourceStages.Fragment))
                            {
                                fragResourceIds[fragResourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                fragResourceIdIndex++;

                                renderStages |= MTLRenderStages.RenderStageFragment;
                            }

                            renderCommandEncoder.UseResource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read, renderStages);
                        }
                        break;
                    case MetalRenderer.TextureSetIndex:
                        if (!segment.IsArray)
                        {
                            if (segment.Type != ResourceType.BufferTexture)
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

                                    renderCommandEncoder.UseResource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read, renderStages);
                                }
                            }
                            else
                            {
                                // TODO: Buffer textures
                            }
                        }
                        else
                        {
                            // TODO: Texture arrays
                        }
                        break;
                    case MetalRenderer.ImageSetIndex:
                        // TODO: Images
                        break;
                }
            }

            vertArgBuffer.Holder.SetDataUnchecked(vertArgBuffer.Offset, MemoryMarshal.AsBytes(vertResourceIds));
            fragArgBuffer.Holder.SetDataUnchecked(fragArgBuffer.Offset, MemoryMarshal.AsBytes(fragResourceIds));

            var mtlVertArgBuffer = _bufferManager.GetBuffer(vertArgBuffer.Handle, false).Get(_pipeline.Cbs).Value;
            var mtlFragArgBuffer = _bufferManager.GetBuffer(fragArgBuffer.Handle, false).Get(_pipeline.Cbs).Value;

            renderCommandEncoder.SetVertexBuffer(mtlVertArgBuffer, (uint)vertArgBuffer.Range.Offset, SetIndexToBindingIndex(setIndex));
            renderCommandEncoder.SetFragmentBuffer(mtlFragArgBuffer, (uint)fragArgBuffer.Range.Offset, SetIndexToBindingIndex(setIndex));
        }

        private void UpdateAndBind(MTLComputeCommandEncoder computeCommandEncoder, Program program, int setIndex)
        {
            var bindingSegments = program.BindingSegments[setIndex];

            if (bindingSegments.Length == 0)
            {
                return;
            }

            var argBuffer = _bufferManager.ReserveOrCreate(_pipeline.Cbs, program.ArgumentBufferSizes[setIndex] * sizeof(ulong));
            Span<ulong> resourceIds = stackalloc ulong[program.ArgumentBufferSizes[setIndex]];
            var resourceIdIndex = 0;

            foreach (ResourceBindingSegment segment in bindingSegments)
            {
                int binding = segment.Binding;
                int count = segment.Count;

                switch (setIndex)
                {
                    case MetalRenderer.UniformSetIndex:
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

                            if (segment.Stages.HasFlag(ResourceStages.Compute))
                            {
                                computeCommandEncoder.UseResource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read);
                                resourceIds[resourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                resourceIdIndex++;
                            }
                        }
                        break;
                    case MetalRenderer.StorageSetIndex:
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

                            if (segment.Stages.HasFlag(ResourceStages.Compute))
                            {
                                computeCommandEncoder.UseResource(new MTLResource(mtlBuffer.NativePtr), MTLResourceUsage.Read | MTLResourceUsage.Write);
                                resourceIds[resourceIdIndex] = mtlBuffer.GpuAddress + (ulong)offset;
                                resourceIdIndex++;
                            }
                        }
                        break;
                    case MetalRenderer.TextureSetIndex:
                        if (!segment.IsArray)
                        {
                            if (segment.Type != ResourceType.BufferTexture)
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

                                    var mtlTexture = storage.GetHandle();

                                    if (segment.Stages.HasFlag(ResourceStages.Compute))
                                    {
                                        computeCommandEncoder.UseResource(new MTLResource(mtlTexture.NativePtr), MTLResourceUsage.Read);
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
                                // TODO: Buffer textures
                            }
                        }
                        else
                        {
                            // TODO: Texture arrays
                        }
                        break;
                    case MetalRenderer.ImageSetIndex:
                        // TODO: Images
                        break;
                }
            }

            argBuffer.Holder.SetDataUnchecked(argBuffer.Offset, MemoryMarshal.AsBytes(resourceIds));

            var mtlArgBuffer = _bufferManager.GetBuffer(argBuffer.Handle, false).Get(_pipeline.Cbs).Value;

            computeCommandEncoder.SetBuffer(mtlArgBuffer, (uint)argBuffer.Range.Offset, SetIndexToBindingIndex(setIndex));
        }

        private uint SetIndexToBindingIndex(int setIndex)
        {
            return setIndex switch
            {
                MetalRenderer.UniformSetIndex => Constants.ConstantBuffersIndex,
                MetalRenderer.StorageSetIndex => Constants.StorageBuffersIndex,
                MetalRenderer.TextureSetIndex => Constants.TexturesIndex,
                MetalRenderer.ImageSetIndex => Constants.ImagessIndex,
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
