using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    enum EncoderType
    {
        Blit,
        Compute,
        Render
    }

    [SupportedOSPlatform("macos")]
    class Pipeline : IPipeline, IDisposable
    {
        private readonly MTLDevice _device;
        private readonly MTLCommandQueue _commandQueue;
        private readonly HelperShaders _helperShaders;

        private MTLCommandBuffer _commandBuffer;
        private MTLCommandEncoder? _currentEncoder;
        private EncoderType _currentEncoderType;
        private MTLTexture[] _renderTargets = [];

        private RenderEncoderState _renderEncoderState;
        private readonly MTLVertexDescriptor _vertexDescriptor = new();
        private BufferInfo[] _vertexBuffers = [];

        private MTLBuffer _indexBuffer;
        private MTLIndexType _indexType;
        private ulong _indexBufferOffset;
        private MTLClearColor _clearColor;

        public Pipeline(MTLDevice device, MTLCommandQueue commandQueue)
        {
            _device = device;
            _commandQueue = commandQueue;
            _helperShaders = new HelperShaders(_device);

            _renderEncoderState = new RenderEncoderState(
                _helperShaders.BlitShader.VertexFunction,
                _helperShaders.BlitShader.FragmentFunction,
                _device);

            _commandBuffer = _commandQueue.CommandBuffer();
        }

        public MTLRenderCommandEncoder GetOrCreateRenderEncoder()
        {
            if (_currentEncoder != null)
            {
                if (_currentEncoderType == EncoderType.Render)
                {
                    return new MTLRenderCommandEncoder(_currentEncoder.Value);
                }
            }

            return BeginRenderPass();
        }

        public MTLBlitCommandEncoder GetOrCreateBlitEncoder()
        {
            if (_currentEncoder != null)
            {
                if (_currentEncoderType == EncoderType.Blit)
                {
                    return new MTLBlitCommandEncoder(_currentEncoder.Value);
                }
            }

            return BeginBlitPass();
        }

        public MTLComputeCommandEncoder GetOrCreateComputeEncoder()
        {
            if (_currentEncoder != null)
            {
                if (_currentEncoderType == EncoderType.Compute)
                {
                    return new MTLComputeCommandEncoder(_currentEncoder.Value);
                }
            }

            return BeginComputePass();
        }

        public void EndCurrentPass()
        {
            if (_currentEncoder != null)
            {
                switch (_currentEncoderType)
                {
                    case EncoderType.Blit:
                        new MTLBlitCommandEncoder(_currentEncoder.Value).EndEncoding();
                        _currentEncoder = null;
                        break;
                    case EncoderType.Compute:
                        new MTLComputeCommandEncoder(_currentEncoder.Value).EndEncoding();
                        _currentEncoder = null;
                        break;
                    case EncoderType.Render:
                        new MTLRenderCommandEncoder(_currentEncoder.Value).EndEncoding();
                        _currentEncoder = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public MTLRenderCommandEncoder BeginRenderPass()
        {
            EndCurrentPass();

            var descriptor = new MTLRenderPassDescriptor();
            for (int i = 0; i < _renderTargets.Length; i++)
            {
                if (_renderTargets[i] != null)
                {
                    var attachment = descriptor.ColorAttachments.Object((ulong)i);
                    attachment.Texture = _renderTargets[i];
                    attachment.LoadAction = MTLLoadAction.Load;
                }
            }

            var renderCommandEncoder = _commandBuffer.RenderCommandEncoder(descriptor);
            _renderEncoderState.SetEncoderState(renderCommandEncoder, _vertexDescriptor);

            for (int i = 0; i < _vertexBuffers.Length; i++)
            {
                renderCommandEncoder.SetVertexBuffer(new MTLBuffer(_vertexBuffers[i].Handle), (ulong)_vertexBuffers[i].Offset, (ulong)i);
            }

            _currentEncoder = renderCommandEncoder;
            _currentEncoderType = EncoderType.Render;
            return renderCommandEncoder;
        }

        public MTLBlitCommandEncoder BeginBlitPass()
        {
            EndCurrentPass();

            var descriptor = new MTLBlitPassDescriptor();
            var blitCommandEncoder = _commandBuffer.BlitCommandEncoder(descriptor);

            _currentEncoder = blitCommandEncoder;
            _currentEncoderType = EncoderType.Blit;
            return blitCommandEncoder;
        }

        public MTLComputeCommandEncoder BeginComputePass()
        {
            EndCurrentPass();

            var descriptor = new MTLComputePassDescriptor();
            var computeCommandEncoder = _commandBuffer.ComputeCommandEncoder(descriptor);

            _currentEncoder = computeCommandEncoder;
            _currentEncoderType = EncoderType.Compute;
            return computeCommandEncoder;
        }

        public void Present(CAMetalDrawable drawable, ITexture texture)
        {
            if (texture is not Texture tex)
            {
                return;
            }

            EndCurrentPass();

            var descriptor = new MTLRenderPassDescriptor();
            var colorAttachment = descriptor.ColorAttachments.Object(0);

            colorAttachment.Texture = drawable.Texture;
            colorAttachment.LoadAction = MTLLoadAction.Clear;
            colorAttachment.ClearColor = _clearColor;

            descriptor.ColorAttachments.SetObject(colorAttachment, 0);

            var renderCommandEncoder = _commandBuffer.RenderCommandEncoder(descriptor);
            _renderEncoderState = new RenderEncoderState(
                _helperShaders.BlitShader.VertexFunction,
                _helperShaders.BlitShader.FragmentFunction,
                _device);
            _renderEncoderState.SetEncoderState(renderCommandEncoder, _vertexDescriptor);

            var sampler = _device.NewSamplerState(new MTLSamplerDescriptor
            {
                MinFilter = MTLSamplerMinMagFilter.Nearest,
                MagFilter = MTLSamplerMinMagFilter.Nearest,
                MipFilter = MTLSamplerMipFilter.NotMipmapped
            });

            renderCommandEncoder.SetFragmentTexture(tex.MTLTexture, 0);
            renderCommandEncoder.SetFragmentSamplerState(sampler, 0);

            renderCommandEncoder.DrawPrimitives(MTLPrimitiveType.Triangle, 0, 6);
            renderCommandEncoder.EndEncoding();

            _commandBuffer.PresentDrawable(drawable);
            _commandBuffer.Commit();

            _commandBuffer = _commandQueue.CommandBuffer();
        }

        public void Barrier()
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void ClearBuffer(BufferHandle destination, int offset, int size, uint value)
        {
            var blitCommandEncoder = GetOrCreateBlitEncoder();

            // Might need a closer look, range's count, lower, and upper bound
            // must be a multiple of 4
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref destination));
            blitCommandEncoder.FillBuffer(mtlBuffer,
                new NSRange
                {
                    location = (ulong)offset,
                    length = (ulong)size
                },
                (byte)value);
        }

        public void ClearRenderTargetColor(int index, int layer, int layerCount, uint componentMask, ColorF color)
        {
            _clearColor = new MTLClearColor { red = color.Red, green = color.Green, blue = color.Blue, alpha = color.Alpha };
        }

        public void ClearRenderTargetDepthStencil(int layer, int layerCount, float depthValue, bool depthMask, int stencilValue,
            int stencilMask)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void CommandBufferBarrier()
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size)
        {
            var blitCommandEncoder = GetOrCreateBlitEncoder();

            MTLBuffer sourceBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref source));
            MTLBuffer destinationBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref destination));

            blitCommandEncoder.CopyFromBuffer(
                sourceBuffer,
                (ulong)srcOffset,
                destinationBuffer,
                (ulong)dstOffset,
                (ulong)size);
        }

        public void DispatchCompute(int groupsX, int groupsY, int groupsZ)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            var renderCommandEncoder = GetOrCreateRenderEncoder();

            // TODO: Support topology re-indexing to provide support for TriangleFans
            var primitiveType = _renderEncoderState.Topology.Convert();

            renderCommandEncoder.DrawPrimitives(primitiveType, (ulong)firstVertex, (ulong)vertexCount, (ulong)instanceCount, (ulong)firstInstance);
        }

        public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int firstVertex, int firstInstance)
        {
            var renderCommandEncoder = GetOrCreateRenderEncoder();

            // TODO: Support topology re-indexing to provide support for TriangleFans
            var primitiveType = _renderEncoderState.Topology.Convert();

            renderCommandEncoder.DrawIndexedPrimitives(primitiveType, (ulong)indexCount, _indexType, _indexBuffer, _indexBufferOffset, (ulong)instanceCount, firstVertex, (ulong)firstInstance);
        }

        public void DrawIndexedIndirect(BufferRange indirectBuffer)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirect(BufferRange indirectBuffer)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion)
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetAlphaTest(bool enable, float reference, CompareOp op)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetBlendState(AdvancedBlendDescriptor blend)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetBlendState(int index, BlendDescriptor blend)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetDepthClamp(bool clamp)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetDepthMode(DepthMode mode)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            var depthStencilState = _renderEncoderState.UpdateDepthState(
                depthTest.TestEnable ? depthTest.Func.Convert() : MTLCompareFunction.Always,
                depthTest.WriteEnable);

            if (_currentEncoderType == EncoderType.Render)
            {
                new MTLRenderCommandEncoder(_currentEncoder.Value).SetDepthStencilState(depthStencilState);
            }
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            var cullMode = enable ? face.Convert() : MTLCullMode.None;

            if (_currentEncoderType == EncoderType.Render)
            {
                new MTLRenderCommandEncoder(_currentEncoder.Value).SetCullMode(cullMode);
            }

            _renderEncoderState.CullMode = cullMode;
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            var winding = frontFace.Convert();

            if (_currentEncoderType == EncoderType.Render)
            {
                new MTLRenderCommandEncoder(_currentEncoder.Value).SetFrontFacingWinding(winding);
            }

            _renderEncoderState.Winding = winding;
        }

        public void SetIndexBuffer(BufferRange buffer, IndexType type)
        {
            if (buffer.Handle != BufferHandle.Null)
            {
                _indexType = type.Convert();
                _indexBufferOffset = (ulong)buffer.Offset;
                var handle = buffer.Handle;
                _indexBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref handle));
            }
        }

        public void SetImage(ShaderStage stage, int binding, ITexture texture, Format imageFormat)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetLineParameters(float width, bool smooth)
        {
            // Not supported in Metal
            Logger.Warning?.Print(LogClass.Gpu, "Wide-line is not supported without private Metal API");
        }

        public void SetLogicOpState(bool enable, LogicalOp op)
        {
            // Not supported in Metal
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetMultisampleState(MultisampleDescriptor multisample)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPatchParameters(int vertices, ReadOnlySpan<float> defaultOuterLevel, ReadOnlySpan<float> defaultInnerLevel)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPolygonMode(PolygonMode frontMode, PolygonMode backMode)
        {
            // Not supported in Metal
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPrimitiveRestart(bool enable, int index)
        {
            // TODO: Supported for LineStrip and TriangleStrip
            // https://github.com/gpuweb/gpuweb/issues/1220#issuecomment-732483263
            // https://developer.apple.com/documentation/metal/mtlrendercommandencoder/1515520-drawindexedprimitives
            // https://stackoverflow.com/questions/70813665/how-to-render-multiple-trianglestrips-using-metal
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            _renderEncoderState.Topology = topology;
        }

        public void SetProgram(IProgram program)
        {
            Program prg = (Program)program;

            if (prg.VertexFunction == IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, "Invalid Vertex Function!");
                return;
            }

            _renderEncoderState = new RenderEncoderState(
                prg.VertexFunction,
                prg.FragmentFunction,
                _device);
        }

        public void SetRasterizerDiscard(bool discard)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            _renderTargets = new MTLTexture[colors.Length];

            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] is not Texture tex)
                {
                    continue;
                }

                if (tex.MTLTexture != null)
                {
                    _renderTargets[i] = tex.MTLTexture;
                }
            }

            // Recreate Render Command Encoder
            BeginRenderPass();
        }

        public unsafe void SetScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            // TODO: Test max allowed scissor rects on device
            var mtlScissorRects = new MTLScissorRect[regions.Length];

            for (int i = 0; i < regions.Length; i++)
            {
                var region = regions[i];
                mtlScissorRects[i] = new MTLScissorRect
                {
                    height = (ulong)region.Height,
                    width = (ulong)region.Width,
                    x = (ulong)region.X,
                    y = (ulong)region.Y
                };
            }

            fixed (MTLScissorRect* pMtlScissorRects = mtlScissorRects)
            {
                // TODO: Fix this function which currently wont accept pointer as intended
                if (_currentEncoderType == EncoderType.Render)
                {
                    // new MTLRenderCommandEncoder(_currentEncoder.Value).SetScissorRects(pMtlScissorRects, (ulong)regions.Length);
                }
            }
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            var backFace = new MTLStencilDescriptor
            {
                StencilFailureOperation = stencilTest.BackSFail.Convert(),
                DepthFailureOperation = stencilTest.BackDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.BackDpPass.Convert(),
                StencilCompareFunction = stencilTest.BackFunc.Convert(),
                ReadMask = (uint)stencilTest.BackFuncMask,
                WriteMask = (uint)stencilTest.BackMask
            };

            var frontFace = new MTLStencilDescriptor
            {
                StencilFailureOperation = stencilTest.FrontSFail.Convert(),
                DepthFailureOperation = stencilTest.FrontDpFail.Convert(),
                DepthStencilPassOperation = stencilTest.FrontDpPass.Convert(),
                StencilCompareFunction = stencilTest.FrontFunc.Convert(),
                ReadMask = (uint)stencilTest.FrontFuncMask,
                WriteMask = (uint)stencilTest.FrontMask
            };

            var depthStencilState = _renderEncoderState.UpdateStencilState(backFace, frontFace);

            if (_currentEncoderType == EncoderType.Render)
            {
                new MTLRenderCommandEncoder(_currentEncoder.Value).SetDepthStencilState(depthStencilState);
            }
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            if (texture is Texture tex)
            {
                if (sampler is Sampler samp)
                {
                    MTLRenderCommandEncoder renderCommandEncoder;
                    MTLComputeCommandEncoder computeCommandEncoder;

                    var mtlTexture = tex.MTLTexture;
                    var mtlSampler = samp.GetSampler();
                    var index = (ulong)binding;

                    switch (stage)
                    {
                        case ShaderStage.Fragment:
                            renderCommandEncoder = GetOrCreateRenderEncoder();
                            renderCommandEncoder.SetFragmentTexture(mtlTexture, index);
                            renderCommandEncoder.SetFragmentSamplerState(mtlSampler, index);
                            break;
                        case ShaderStage.Vertex:
                            renderCommandEncoder = GetOrCreateRenderEncoder();
                            renderCommandEncoder.SetVertexTexture(mtlTexture, index);
                            renderCommandEncoder.SetVertexSamplerState(mtlSampler, index);
                            break;
                        case ShaderStage.Compute:
                            computeCommandEncoder = GetOrCreateComputeEncoder();
                            computeCommandEncoder.SetTexture(mtlTexture, index);
                            computeCommandEncoder.SetSamplerState(mtlSampler, index);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader stage!");
                    }
                }
            }
        }

        public void SetUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetUserClipDistance(int index, bool enableClip)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            for (int i = 0; i < vertexAttribs.Length; i++)
            {
                if (!vertexAttribs[i].IsZero)
                {
                    // TODO: Format should not be hardcoded
                    var attrib = _vertexDescriptor.Attributes.Object((ulong)i);
                    attrib.Format = MTLVertexFormat.Float4;
                    attrib.BufferIndex = (ulong)vertexAttribs[i].BufferIndex;
                    attrib.Offset = (ulong)vertexAttribs[i].Offset;

                    var layout = _vertexDescriptor.Layouts.Object((ulong)vertexAttribs[i].BufferIndex);
                    layout.Stride = 1;
                }
            }
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            _vertexBuffers = new BufferInfo[vertexBuffers.Length];

            for (int i = 0; i < vertexBuffers.Length; i++)
            {
                if (vertexBuffers[i].Stride != 0)
                {
                    var layout = _vertexDescriptor.Layouts.Object((ulong)i);
                    layout.Stride = (ulong)vertexBuffers[i].Stride;

                    _vertexBuffers[i] = new BufferInfo {
                        Handle = vertexBuffers[i].Buffer.Handle.ToIntPtr(),
                        Offset = vertexBuffers[i].Buffer.Offset
                    };
                }
            }
        }

        public unsafe void SetViewports(ReadOnlySpan<Viewport> viewports)
        {
            // TODO: Test max allowed viewports on device
            var mtlViewports = new MTLViewport[viewports.Length];

            for (int i = 0; i < viewports.Length; i++)
            {
                var viewport = viewports[i];
                mtlViewports[i] = new MTLViewport
                {
                    originX = viewport.Region.X,
                    originY = viewport.Region.Y,
                    width = viewport.Region.Width,
                    height = viewport.Region.Height,
                    znear = viewport.DepthNear,
                    zfar = viewport.DepthFar
                };
            }

            fixed (MTLViewport* pMtlViewports = mtlViewports)
            {
                // TODO: Fix this function which currently wont accept pointer as intended
                if (_currentEncoderType == EncoderType.Render)
                {
                    // new MTLRenderCommandEncoder(_currentEncoder.Value).SetViewports(pMtlViewports, (ulong)regions.Length);
                }
            }
        }

        public void TextureBarrier()
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            // renderCommandEncoder.MemoryBarrier(MTLBarrierScope.Textures, );
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void TextureBarrierTiled()
        {
            // var renderCommandEncoder = GetOrCreateRenderEncoder();

            // renderCommandEncoder.MemoryBarrier(MTLBarrierScope.Textures, );
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual)
        {
            // TODO: Implementable via indirect draw commands
            return false;
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual)
        {
            // TODO: Implementable via indirect draw commands
            return false;
        }

        public void EndHostConditionalRendering()
        {
            // TODO: Implementable via indirect draw commands
        }

        public void BeginTransformFeedback(PrimitiveTopology topology)
        {
            // Metal does not support Transform Feedback
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void EndTransformFeedback()
        {
            // Metal does not support Transform Feedback
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            // Metal does not support Transform Feedback
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void Dispose()
        {
            EndCurrentPass();
        }
    }
}
