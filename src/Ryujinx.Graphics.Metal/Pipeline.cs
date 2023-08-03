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
    [SupportedOSPlatform("macos")]
    class Pipeline : IPipeline, IDisposable
    {
        // 0 Frames = No capture
        // Some games like Undertale trigger a stack overflow on capture end
        private const int MaxFramesPerCapture = 5;
        private const string CaptureLocation = "/Users/isaacmarovitz/Desktop/Captures/Trace-";

        private readonly MTLDevice _device;
        private readonly MTLCommandQueue _commandQueue;
        private readonly HelperShaders _helperShaders;

        private MTLCommandBuffer _commandBuffer;
        private MTLCommandEncoder _currentEncoder;

        private RenderEncoderState _renderEncoderState;

        private MTLBuffer _indexBuffer;
        private MTLIndexType _indexType;
        private ulong _indexBufferOffset;
        private MTLClearColor _clearColor;
        private int _frameCount;
        private bool _captureEnded = true;

        public Pipeline(MTLDevice device, MTLCommandQueue commandQueue)
        {
            _device = device;
            _commandQueue = commandQueue;
            _helperShaders = new HelperShaders(_device);

            _renderEncoderState = new RenderEncoderState(_helperShaders.BlitShader, _device);

            _commandBuffer = _commandQueue.CommandBuffer();

            if (MaxFramesPerCapture > 0)
            {
                StartCapture();
            }
        }

        private void StartCapture()
        {
            var captureDescriptor = new MTLCaptureDescriptor
            {
                CaptureObject = _commandQueue,
                Destination = MTLCaptureDestination.GPUTraceDocument,
                OutputURL = NSURL.FileURLWithPath(StringHelper.NSString(CaptureLocation + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ".gputrace"))
            };
            var captureError = new NSError(IntPtr.Zero);
            MTLCaptureManager.SharedCaptureManager().StartCapture(captureDescriptor, ref captureError);
            if (captureError != IntPtr.Zero)
            {
                Console.WriteLine($"Failed to start capture! {StringHelper.String(captureError.LocalizedDescription)}");

            }

            _captureEnded = false;
        }

        public MTLRenderCommandEncoder GetOrCreateRenderEncoder()
        {
            if (_currentEncoder is MTLRenderCommandEncoder encoder)
            {
                return encoder;
            }

            return BeginRenderPass();
        }

        public MTLBlitCommandEncoder GetOrCreateBlitEncoder()
        {
            if (_currentEncoder is MTLBlitCommandEncoder encoder)
            {
                return encoder;
            }

            return BeginBlitPass();
        }

        public MTLComputeCommandEncoder GetOrCreateComputeEncoder()
        {
            if (_currentEncoder is MTLComputeCommandEncoder encoder)
            {
                return encoder;
            }

            return BeginComputePass();
        }

        public void EndCurrentPass()
        {
            if (_currentEncoder != null)
            {
                _currentEncoder.EndEncoding();
                _currentEncoder = null;
            }
        }

        public MTLRenderCommandEncoder BeginRenderPass()
        {
            EndCurrentPass();

            var descriptor = new MTLRenderPassDescriptor();
            var renderCommandEncoder = _commandBuffer.RenderCommandEncoder(descriptor);
            _renderEncoderState.SetEncoderState(renderCommandEncoder);

            _currentEncoder = renderCommandEncoder;
            return renderCommandEncoder;
        }

        public MTLBlitCommandEncoder BeginBlitPass()
        {
            EndCurrentPass();

            var descriptor = new MTLBlitPassDescriptor();
            var blitCommandEncoder = _commandBuffer.BlitCommandEncoder(descriptor);

            _currentEncoder = blitCommandEncoder;
            return blitCommandEncoder;
        }

        public MTLComputeCommandEncoder BeginComputePass()
        {
            EndCurrentPass();

            var descriptor = new MTLComputePassDescriptor();
            var computeCommandEncoder = _commandBuffer.ComputeCommandEncoder(descriptor);

            _currentEncoder = computeCommandEncoder;
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
            descriptor.ColorAttachments.Object(0).Texture = drawable.Texture;
            descriptor.ColorAttachments.Object(0).LoadAction = MTLLoadAction.Clear;
            descriptor.ColorAttachments.Object(0).ClearColor = _clearColor;

            var renderCommandEncoder = _commandBuffer.RenderCommandEncoder(descriptor);
            _renderEncoderState.SetEncoderState(renderCommandEncoder);

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

            if (!_captureEnded)
            {
                _frameCount++;

                if (_frameCount >= MaxFramesPerCapture)
                {
                    _captureEnded = true;
                    MTLCaptureManager.SharedCaptureManager().StopCapture();
                    Logger.Warning?.Print(LogClass.Gpu, "Trace ended!");
                }
            }

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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirect(BufferRange indirectBuffer)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion)
        {
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
                depthTest.TestEnable ? MTLCompareFunction.Always : depthTest.Func.Convert(),
                depthTest.WriteEnable);

            if (_currentEncoder is MTLRenderCommandEncoder renderCommandEncoder)
            {
                renderCommandEncoder.SetDepthStencilState(depthStencilState);
            }
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            var cullMode = enable ? face.Convert() : MTLCullMode.None;

            if (_currentEncoder is MTLRenderCommandEncoder renderCommandEncoder)
            {
                renderCommandEncoder.SetCullMode(cullMode);
            }

            _renderEncoderState.CullMode = cullMode;
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            var winding = frontFace.Convert();

            if (_currentEncoder is MTLRenderCommandEncoder renderCommandEncoder)
            {
                renderCommandEncoder.SetFrontFacingWinding(winding);
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

        public void SetImage(int binding, ITexture texture, Format imageFormat)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetLineParameters(float width, bool smooth)
        {
            // Not supported in Metal
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
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
                // _renderCommandEncoder.SetScissorRects(pMtlScissorRects, regions.Length);
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

            if (_currentEncoder is MTLRenderCommandEncoder renderCommandEncoder)
            {
                renderCommandEncoder.SetDepthStencilState(depthStencilState);
            }
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
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
                // _renderCommandEncoder.SetViewports(pMtlViewports, viewports.Length);
            }
        }

        public void TextureBarrier()
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void TextureBarrierTiled()
        {
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

        }
    }
}
