using Ryujinx.Graphics.Shader;
using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IPipeline
    {
        void Barrier();

        void BeginTransformFeedback(PrimitiveTopology topology);

        void ClearBuffer(BufferHandle destination, int offset, int size, uint value);

        void ClearRenderTargetColor(int index, int layer, int layerCount, uint componentMask, ColorF color);

        void ClearRenderTargetDepthStencil(
            int layer,
            int layerCount,
            float depthValue,
            bool depthMask,
            int stencilValue,
            int stencilMask);

        void CommandBufferBarrier();

        void CopyBuffer(BufferHandle source, BufferHandle destination, int srcOffset, int dstOffset, int size);

        void DispatchCompute(int groupsX, int groupsY, int groupsZ);

        void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance);
        void DrawIndexed(
            int indexCount,
            int instanceCount,
            int firstIndex,
            int firstVertex,
            int firstInstance);
        void DrawIndexedIndirect(BufferRange indirectBuffer);
        void DrawIndexedIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride);
        void DrawIndirect(BufferRange indirectBuffer);
        void DrawIndirectCount(BufferRange indirectBuffer, BufferRange parameterBuffer, int maxDrawCount, int stride);
        void DrawTexture(ITexture texture, ISampler sampler, Extents2DF srcRegion, Extents2DF dstRegion);

        void EndTransformFeedback();

        void SetAlphaTest(bool enable, float reference, CompareOp op);

        void SetBlendState(AdvancedBlendDescriptor blend);
        void SetBlendState(int index, BlendDescriptor blend);

        void SetDepthBias(PolygonModeMask enables, float factor, float units, float clamp);
        void SetDepthClamp(bool clamp);
        void SetDepthMode(DepthMode mode);
        void SetDepthTest(DepthTestDescriptor depthTest);

        void SetFaceCulling(bool enable, Face face);

        void SetFrontFace(FrontFace frontFace);

        void SetIndexBuffer(BufferRange buffer, IndexType type);

        void SetImage(ShaderStage stage, int binding, ITexture texture, Format imageFormat);
        void SetImageArray(ShaderStage stage, int binding, IImageArray array);

        void SetLineParameters(float width, bool smooth);

        void SetLogicOpState(bool enable, LogicalOp op);

        void SetMultisampleState(MultisampleDescriptor multisample);

        void SetPatchParameters(int vertices, ReadOnlySpan<float> defaultOuterLevel, ReadOnlySpan<float> defaultInnerLevel);
        void SetPointParameters(float size, bool isProgramPointSize, bool enablePointSprite, Origin origin);

        void SetPolygonMode(PolygonMode frontMode, PolygonMode backMode);

        void SetPrimitiveRestart(bool enable, int index);

        void SetPrimitiveTopology(PrimitiveTopology topology);

        void SetProgram(IProgram program);

        void SetRasterizerDiscard(bool discard);

        void SetRenderTargetColorMasks(ReadOnlySpan<uint> componentMask);
        void SetRenderTargets(ITexture[] colors, ITexture depthStencil);

        void SetScissors(ReadOnlySpan<Rectangle<int>> regions);

        void SetStencilTest(StencilTestDescriptor stencilTest);

        void SetStorageBuffers(ReadOnlySpan<BufferAssignment> buffers);

        void SetTextureAndSampler(ShaderStage stage, int binding, ITexture texture, ISampler sampler);
        void SetTextureArray(ShaderStage stage, int binding, ITextureArray array);

        void SetTransformFeedbackBuffers(ReadOnlySpan<BufferRange> buffers);
        void SetUniformBuffers(ReadOnlySpan<BufferAssignment> buffers);

        void SetUserClipDistance(int index, bool enableClip);

        void SetVertexAttribs(ReadOnlySpan<VertexAttribDescriptor> vertexAttribs);
        void SetVertexBuffers(ReadOnlySpan<VertexBufferDescriptor> vertexBuffers);

        void SetViewports(ReadOnlySpan<Viewport> viewports);

        void TextureBarrier();
        void TextureBarrierTiled();

        bool TryHostConditionalRendering(ICounterEvent value, ulong compare, bool isEqual);
        bool TryHostConditionalRendering(ICounterEvent value, ICounterEvent compare, bool isEqual);
        void EndHostConditionalRendering();
    }
}
