using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using System;
using SharpMetal;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class Pipeline : IPipeline, IDisposable
    {
        private MTLCommandBuffer _commandBuffer;
        private MTLRenderCommandEncoder _renderCommandEncoder;

        public Pipeline(MTLDevice device, MTLCommandBuffer commandBuffer)
        {
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor();
            var renderPipelineState = device.CreateRenderPipelineState(renderPipelineDescriptor, out NSError _);

            _commandBuffer = commandBuffer;
            _renderCommandEncoder = _commandBuffer.CreateRenderCommandEncoder(new MTLRenderPassDescriptor());
            _renderCommandEncoder.SetRenderPipelineState(renderPipelineState);
        }

        public void Barrier()
        {
            throw new NotImplementedException();
        }

        public void ClearBuffer(BufferHandle destination, int offset, int size, uint value)
        {
            throw new NotImplementedException();
        }

        public void ClearRenderTargetColor(int index, int layer, int layerCount, uint componentMask, ColorF color)
        {
            throw new NotImplementedException();
        }

        public void ClearRenderTargetDepthStencil(int layer, int layerCount, float depthValue, bool depthMask, int stencilValue,
            int stencilMask)
        {
            throw new NotImplementedException();
        }

        public void CommandBufferBarrier()
        {
            throw new NotImplementedException();
        }

        public void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size)
        {
            throw new NotImplementedException();
        }

        public void DispatchCompute(int groupsX, int groupsY, int groupsZ)
        {
            throw new NotImplementedException();
        }

        public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        {
            throw new NotImplementedException();
        }

        public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int firstVertex, int firstInstance)
        {
            throw new NotImplementedException();
        }

        public void DrawIndexedIndirect(BufferRange indirectBuffer)
        {
            throw new NotImplementedException();
        }

        public void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            throw new NotImplementedException();
        }

        public void DrawIndirect(BufferRange indirectBuffer)
        {
            throw new NotImplementedException();
        }

        public void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride)
        {
            throw new NotImplementedException();
        }

        public void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion)
        {
            throw new NotImplementedException();
        }

        public void SetAlphaTest(bool enable, float reference, CompareOp op)
        {
            throw new NotImplementedException();
        }

        public void SetBlendState(AdvancedBlendDescriptor blend)
        {
            throw new NotImplementedException();
        }

        public void SetBlendState(int index, BlendDescriptor blend)
        {
            throw new NotImplementedException();
        }

        public void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp)
        {
            throw new NotImplementedException();
        }

        public void SetDepthClamp(bool clamp)
        {
            throw new NotImplementedException();
        }

        public void SetDepthMode(DepthMode mode)
        {
            throw new NotImplementedException();
        }

        public void SetDepthTest(DepthTestDescriptor depthTest)
        {
            throw new NotImplementedException();
        }

        public void SetFaceCulling(bool enable, Face face)
        {
            throw new NotImplementedException();
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            throw new NotImplementedException();
        }

        public void SetIndexBuffer(BufferRange buffer, IndexType type)
        {
            throw new NotImplementedException();
        }

        public void SetImage(int binding, ITexture texture, Format imageFormat)
        {
            throw new NotImplementedException();
        }

        public void SetLineParameters(float width, bool smooth)
        {
            throw new NotImplementedException();
        }

        public void SetLogicOpState(bool enable, LogicalOp op)
        {
            throw new NotImplementedException();
        }

        public void SetMultisampleState(MultisampleDescriptor multisample)
        {
            throw new NotImplementedException();
        }

        public void SetPatchParameters(int vertices, ReadOnlySpan<float> defaultOuterLevel, ReadOnlySpan<float> defaultInnerLevel)
        {
            throw new NotImplementedException();
        }

        public void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin)
        {
            throw new NotImplementedException();
        }

        public void SetPolygonMode(PolygonMode frontMode, PolygonMode backMode)
        {
            throw new NotImplementedException();
        }

        public void SetPrimitiveRestart(bool enable, int index)
        {
            throw new NotImplementedException();
        }

        public void SetPrimitiveTopology(PrimitiveTopology topology)
        {
            throw new NotImplementedException();
        }

        public void SetProgram(IProgram program)
        {
            throw new NotImplementedException();
        }

        public void SetRasterizerDiscard(bool discard)
        {
            throw new NotImplementedException();
        }

        public void SetRenderTargetScale(float scale)
        {
            throw new NotImplementedException();
        }

        public void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask)
        {
            throw new NotImplementedException();
        }

        public void SetRenderTargets(ITexture[] colors, ITexture depthStencil)
        {
            throw new NotImplementedException();
        }

        public void SetScissors(ReadOnlySpan<Rectangle<int>> regions)
        {
            throw new NotImplementedException();
        }

        public void SetStencilTest(StencilTestDescriptor stencilTest)
        {
            throw new NotImplementedException();
        }

        public void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            throw new NotImplementedException();
        }

        public void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler)
        {
            throw new NotImplementedException();
        }

        public void SetUniformBuffers(ReadOnlySpan<BufferAssignment> buffers)
        {
            throw new NotImplementedException();
        }

        public void SetUserClipDistance(int index, bool enableClip)
        {
            throw new NotImplementedException();
        }

        public void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs)
        {
            throw new NotImplementedException();
        }

        public void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers)
        {
            throw new NotImplementedException();
        }

        public void SetViewports(ReadOnlySpan<Viewport> viewports, bool disableTransform)
        {
            throw new NotImplementedException();
        }

        public void TextureBarrier()
        {
            throw new NotImplementedException();
        }

        public void TextureBarrierTiled()
        {
            throw new NotImplementedException();
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual)
        {
            throw new NotImplementedException();
        }

        public bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual)
        {
            throw new NotImplementedException();
        }

        public void EndHostConditionalRendering()
        {
            throw new NotImplementedException();
        }

        public void UpdateRenderScale(ReadOnlySpan<float> scales, int totalCount, int fragmentCount)
        {
            throw new NotImplementedException();
        }

        public void BeginTransformFeedback(PrimitiveTopology topology)
        {
            // Metal does not support Transform Feedback
            throw new NotSupportedException();
        }

        public void EndTransformFeedback()
        {
            // Metal does not support Transform Feedback
            throw new NotSupportedException();
        }

        public void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers)
        {
            // Metal does not support Transform Feedback
            throw new NotSupportedException();
        }

        public void Dispose()
        {

        }
    }
}