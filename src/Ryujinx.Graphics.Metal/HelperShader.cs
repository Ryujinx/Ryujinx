using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class HelperShader : IDisposable
    {
        private const int ConvertElementsPerWorkgroup = 32 * 100; // Work group size of 32 times 100 elements.
        private const string ShadersSourcePath = "/Ryujinx.Graphics.Metal/Shaders";
        private readonly MetalRenderer _renderer;
        private readonly Pipeline _pipeline;
        private MTLDevice _device;

        private readonly ISampler _samplerLinear;
        private readonly ISampler _samplerNearest;
        private readonly IProgram _programColorBlitF;
        private readonly IProgram _programColorBlitI;
        private readonly IProgram _programColorBlitU;
        private readonly IProgram _programColorBlitMsF;
        private readonly IProgram _programColorBlitMsI;
        private readonly IProgram _programColorBlitMsU;
        private readonly List<IProgram> _programsColorClearF = new();
        private readonly List<IProgram> _programsColorClearI = new();
        private readonly List<IProgram> _programsColorClearU = new();
        private readonly IProgram _programDepthStencilClear;
        private readonly IProgram _programStrideChange;
        private readonly IProgram _programDepthBlit;
        private readonly IProgram _programDepthBlitMs;
        private readonly IProgram _programStencilBlit;
        private readonly IProgram _programStencilBlitMs;

        private readonly EncoderState _helperShaderState = new();

        public HelperShader(MTLDevice device, MetalRenderer renderer, Pipeline pipeline)
        {
            _device = device;
            _renderer = renderer;
            _pipeline = pipeline;

            _samplerNearest = new Sampler(_device, SamplerCreateInfo.Create(MinFilter.Nearest, MagFilter.Nearest));
            _samplerLinear = new Sampler(_device, SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            var blitResourceLayout = new ResourceLayoutBuilder()
                .Add(ResourceStages.Vertex, ResourceType.UniformBuffer, 0)
                .Add(ResourceStages.Fragment, ResourceType.TextureAndSampler, 0).Build();

            var blitSource = ReadMsl("Blit.metal");

            var blitSourceF = blitSource.Replace("FORMAT", "float", StringComparison.Ordinal);
            _programColorBlitF = new Program(
            [
                new ShaderSource(blitSourceF, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceF, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var blitSourceI = blitSource.Replace("FORMAT", "int");
            _programColorBlitI = new Program(
            [
                new ShaderSource(blitSourceI, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceI, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var blitSourceU = blitSource.Replace("FORMAT", "uint");
            _programColorBlitU = new Program(
            [
                new ShaderSource(blitSourceU, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceU, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var blitMsSource = ReadMsl("BlitMs.metal");

            var blitMsSourceF = blitMsSource.Replace("FORMAT", "float");
            _programColorBlitMsF = new Program(
            [
                new ShaderSource(blitMsSourceF, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitMsSourceF, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var blitMsSourceI = blitMsSource.Replace("FORMAT", "int");
            _programColorBlitMsI = new Program(
            [
                new ShaderSource(blitMsSourceI, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitMsSourceI, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var blitMsSourceU = blitMsSource.Replace("FORMAT", "uint");
            _programColorBlitMsU = new Program(
            [
                new ShaderSource(blitMsSourceU, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitMsSourceU, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var colorClearResourceLayout = new ResourceLayoutBuilder()
                .Add(ResourceStages.Fragment, ResourceType.UniformBuffer, 0).Build();

            var colorClearSource = ReadMsl("ColorClear.metal");

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                var crntSource = colorClearSource.Replace("COLOR_ATTACHMENT_INDEX", i.ToString()).Replace("FORMAT", "float");
                _programsColorClearF.Add(new Program(
                [
                    new ShaderSource(crntSource, ShaderStage.Fragment, TargetLanguage.Msl),
                    new ShaderSource(crntSource, ShaderStage.Vertex, TargetLanguage.Msl)
                ], colorClearResourceLayout, device));
            }

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                var crntSource = colorClearSource.Replace("COLOR_ATTACHMENT_INDEX", i.ToString()).Replace("FORMAT", "int");
                _programsColorClearI.Add(new Program(
                [
                    new ShaderSource(crntSource, ShaderStage.Fragment, TargetLanguage.Msl),
                    new ShaderSource(crntSource, ShaderStage.Vertex, TargetLanguage.Msl)
                ], colorClearResourceLayout, device));
            }

            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                var crntSource = colorClearSource.Replace("COLOR_ATTACHMENT_INDEX", i.ToString()).Replace("FORMAT", "uint");
                _programsColorClearU.Add(new Program(
                [
                    new ShaderSource(crntSource, ShaderStage.Fragment, TargetLanguage.Msl),
                    new ShaderSource(crntSource, ShaderStage.Vertex, TargetLanguage.Msl)
                ], colorClearResourceLayout, device));
            }

            var depthStencilClearSource = ReadMsl("DepthStencilClear.metal");
            _programDepthStencilClear = new Program(
            [
                new ShaderSource(depthStencilClearSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(depthStencilClearSource, ShaderStage.Vertex, TargetLanguage.Msl)
            ], colorClearResourceLayout, device);

            var strideChangeResourceLayout = new ResourceLayoutBuilder()
                .Add(ResourceStages.Compute, ResourceType.UniformBuffer, 0)
                .Add(ResourceStages.Compute, ResourceType.StorageBuffer, 1)
                .Add(ResourceStages.Compute, ResourceType.StorageBuffer, 2, true).Build();

            var strideChangeSource = ReadMsl("ChangeBufferStride.metal");
            _programStrideChange = new Program(
            [
                new ShaderSource(strideChangeSource, ShaderStage.Compute, TargetLanguage.Msl)
            ], strideChangeResourceLayout, device, new ComputeSize(64, 1, 1));

            var depthBlitSource = ReadMsl("DepthBlit.metal");
            _programDepthBlit = new Program(
            [
                new ShaderSource(depthBlitSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceF, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var depthBlitMsSource = ReadMsl("DepthBlitMs.metal");
            _programDepthBlitMs = new Program(
            [
                new ShaderSource(depthBlitMsSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceF, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var stencilBlitSource = ReadMsl("StencilBlit.metal");
            _programStencilBlit = new Program(
            [
                new ShaderSource(stencilBlitSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceF, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);

            var stencilBlitMsSource = ReadMsl("StencilBlitMs.metal");
            _programStencilBlitMs = new Program(
            [
                new ShaderSource(stencilBlitMsSource, ShaderStage.Fragment, TargetLanguage.Msl),
                new ShaderSource(blitSourceF, ShaderStage.Vertex, TargetLanguage.Msl)
            ], blitResourceLayout, device);
        }

        private static string ReadMsl(string fileName)
        {
            var msl = EmbeddedResources.ReadAllText(string.Join('/', ShadersSourcePath, fileName));

#pragma warning disable IDE0055 // Disable formatting
            msl = msl.Replace("CONSTANT_BUFFERS_INDEX", $"{Constants.ConstantBuffersIndex}")
                     .Replace("STORAGE_BUFFERS_INDEX", $"{Constants.StorageBuffersIndex}")
                     .Replace("TEXTURES_INDEX", $"{Constants.TexturesIndex}")
                     .Replace("IMAGES_INDEX", $"{Constants.ImagesIndex}");
#pragma warning restore IDE0055

            return msl;
        }

        public unsafe void BlitColor(
            CommandBufferScoped cbs,
            Texture src,
            Texture dst,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool linearFilter,
            bool clear = false)
        {
            _pipeline.SwapState(_helperShaderState);

            const int RegionBufferSize = 16;

            var sampler = linearFilter ? _samplerLinear : _samplerNearest;

            _pipeline.SetTextureAndSampler(ShaderStage.Fragment, 0, src, sampler);

            Span<float> region = stackalloc float[RegionBufferSize / sizeof(float)];

            region[0] = srcRegion.X1 / (float)src.Width;
            region[1] = srcRegion.X2 / (float)src.Width;
            region[2] = srcRegion.Y1 / (float)src.Height;
            region[3] = srcRegion.Y2 / (float)src.Height;

            if (dstRegion.X1 > dstRegion.X2)
            {
                (region[0], region[1]) = (region[1], region[0]);
            }

            if (dstRegion.Y1 > dstRegion.Y2)
            {
                (region[2], region[3]) = (region[3], region[2]);
            }

            using var buffer = _renderer.BufferManager.ReserveOrCreate(cbs, RegionBufferSize);
            buffer.Holder.SetDataUnchecked<float>(buffer.Offset, region);
            _pipeline.SetUniformBuffers([new BufferAssignment(0, buffer.Range)]);

            var rect = new Rectangle<float>(
                MathF.Min(dstRegion.X1, dstRegion.X2),
                MathF.Min(dstRegion.Y1, dstRegion.Y2),
                MathF.Abs(dstRegion.X2 - dstRegion.X1),
                MathF.Abs(dstRegion.Y2 - dstRegion.Y1));

            Span<Viewport> viewports = stackalloc Viewport[16];

            viewports[0] = new Viewport(
                rect,
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            bool dstIsDepthOrStencil = dst.Info.Format.IsDepthOrStencil();

            if (dstIsDepthOrStencil)
            {
                // TODO: Depth & stencil blit!
                Logger.Warning?.PrintMsg(LogClass.Gpu, "Requested a depth or stencil blit!");
                _pipeline.SwapState(null);
                return;
            }
            else if (src.Info.Target.IsMultisample())
            {
                if (dst.Info.Format.IsSint())
                {
                    _pipeline.SetProgram(_programColorBlitMsI);
                }
                else if (dst.Info.Format.IsUint())
                {
                    _pipeline.SetProgram(_programColorBlitMsU);
                }
                else
                {
                    _pipeline.SetProgram(_programColorBlitMsF);
                }
            }
            else
            {
                if (dst.Info.Format.IsSint())
                {
                    _pipeline.SetProgram(_programColorBlitI);
                }
                else if (dst.Info.Format.IsUint())
                {
                    _pipeline.SetProgram(_programColorBlitU);
                }
                else
                {
                    _pipeline.SetProgram(_programColorBlitF);
                }
            }

            int dstWidth = dst.Width;
            int dstHeight = dst.Height;

            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[16];

            scissors[0] = new Rectangle<int>(0, 0, dstWidth, dstHeight);

            _pipeline.SetRenderTargets([dst], null);
            _pipeline.SetScissors(scissors);

            _pipeline.SetClearLoadAction(clear);

            _pipeline.SetViewports(viewports);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);

            // Cleanup
            if (clear)
            {
                _pipeline.SetClearLoadAction(false);
            }

            // Restore previous state
            _pipeline.SwapState(null);
        }

        public unsafe void BlitDepthStencil(
            CommandBufferScoped cbs,
            Texture src,
            Texture dst,
            Extents2D srcRegion,
            Extents2D dstRegion)
        {
            _pipeline.SwapState(_helperShaderState);

            const int RegionBufferSize = 16;

            Span<float> region = stackalloc float[RegionBufferSize / sizeof(float)];

            region[0] = srcRegion.X1 / (float)src.Width;
            region[1] = srcRegion.X2 / (float)src.Width;
            region[2] = srcRegion.Y1 / (float)src.Height;
            region[3] = srcRegion.Y2 / (float)src.Height;

            if (dstRegion.X1 > dstRegion.X2)
            {
                (region[0], region[1]) = (region[1], region[0]);
            }

            if (dstRegion.Y1 > dstRegion.Y2)
            {
                (region[2], region[3]) = (region[3], region[2]);
            }

            using var buffer = _renderer.BufferManager.ReserveOrCreate(cbs, RegionBufferSize);
            buffer.Holder.SetDataUnchecked<float>(buffer.Offset, region);
            _pipeline.SetUniformBuffers([new BufferAssignment(0, buffer.Range)]);

            Span<Viewport> viewports = stackalloc Viewport[16];

            var rect = new Rectangle<float>(
                MathF.Min(dstRegion.X1, dstRegion.X2),
                MathF.Min(dstRegion.Y1, dstRegion.Y2),
                MathF.Abs(dstRegion.X2 - dstRegion.X1),
                MathF.Abs(dstRegion.Y2 - dstRegion.Y1));

            viewports[0] = new Viewport(
                rect,
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            int dstWidth = dst.Width;
            int dstHeight = dst.Height;

            Span<Rectangle<int>> scissors = stackalloc Rectangle<int>[16];

            scissors[0] = new Rectangle<int>(0, 0, dstWidth, dstHeight);

            _pipeline.SetRenderTargets([], dst);
            _pipeline.SetScissors(scissors);
            _pipeline.SetViewports(viewports);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);

            if (src.Info.Format is
                Format.D16Unorm or
                Format.D32Float or
                Format.X8UintD24Unorm or
                Format.D24UnormS8Uint or
                Format.D32FloatS8Uint or
                Format.S8UintD24Unorm)
            {
                var depthTexture = CreateDepthOrStencilView(src, DepthStencilMode.Depth);

                BlitDepthStencilDraw(depthTexture, isDepth: true);

                if (depthTexture != src)
                {
                    depthTexture.Release();
                }
            }

            if (src.Info.Format is
                Format.S8Uint or
                Format.D24UnormS8Uint or
                Format.D32FloatS8Uint or
                Format.S8UintD24Unorm)
            {
                var stencilTexture = CreateDepthOrStencilView(src, DepthStencilMode.Stencil);

                BlitDepthStencilDraw(stencilTexture, isDepth: false);

                if (stencilTexture != src)
                {
                    stencilTexture.Release();
                }
            }

            // Restore previous state
            _pipeline.SwapState(null);
        }

        private static Texture CreateDepthOrStencilView(Texture depthStencilTexture, DepthStencilMode depthStencilMode)
        {
            if (depthStencilTexture.Info.DepthStencilMode == depthStencilMode)
            {
                return depthStencilTexture;
            }

            return (Texture)depthStencilTexture.CreateView(new TextureCreateInfo(
                depthStencilTexture.Info.Width,
                depthStencilTexture.Info.Height,
                depthStencilTexture.Info.Depth,
                depthStencilTexture.Info.Levels,
                depthStencilTexture.Info.Samples,
                depthStencilTexture.Info.BlockWidth,
                depthStencilTexture.Info.BlockHeight,
                depthStencilTexture.Info.BytesPerPixel,
                depthStencilTexture.Info.Format,
                depthStencilMode,
                depthStencilTexture.Info.Target,
                SwizzleComponent.Red,
                SwizzleComponent.Green,
                SwizzleComponent.Blue,
                SwizzleComponent.Alpha), 0, 0);
        }

        private void BlitDepthStencilDraw(Texture src, bool isDepth)
        {
            if (isDepth)
            {
                _pipeline.SetProgram(src.Info.Target.IsMultisample() ? _programDepthBlitMs : _programDepthBlit);
                _pipeline.SetDepthTest(new DepthTestDescriptor(true, true, CompareOp.Always));
            }
            else
            {
                _pipeline.SetProgram(src.Info.Target.IsMultisample() ? _programStencilBlitMs : _programStencilBlit);
                _pipeline.SetStencilTest(CreateStencilTestDescriptor(true));
            }

            _pipeline.Draw(4, 1, 0, 0);

            if (isDepth)
            {
                _pipeline.SetDepthTest(new DepthTestDescriptor(false, false, CompareOp.Always));
            }
            else
            {
                _pipeline.SetStencilTest(CreateStencilTestDescriptor(false));
            }
        }

        public unsafe void DrawTexture(
            ITexture src,
            ISampler srcSampler,
            Extents2DF srcRegion,
            Extents2DF dstRegion)
        {
            // Save current state
            var state = _pipeline.SavePredrawState();

            _pipeline.SetFaceCulling(false, Face.Front);
            _pipeline.SetStencilTest(new StencilTestDescriptor());
            _pipeline.SetDepthTest(new DepthTestDescriptor());

            const int RegionBufferSize = 16;

            _pipeline.SetTextureAndSampler(ShaderStage.Fragment, 0, src, srcSampler);

            Span<float> region = stackalloc float[RegionBufferSize / sizeof(float)];

            region[0] = srcRegion.X1 / src.Width;
            region[1] = srcRegion.X2 / src.Width;
            region[2] = srcRegion.Y1 / src.Height;
            region[3] = srcRegion.Y2 / src.Height;

            if (dstRegion.X1 > dstRegion.X2)
            {
                (region[0], region[1]) = (region[1], region[0]);
            }

            if (dstRegion.Y1 > dstRegion.Y2)
            {
                (region[2], region[3]) = (region[3], region[2]);
            }

            var bufferHandle = _renderer.BufferManager.CreateWithHandle(RegionBufferSize);
            _renderer.BufferManager.SetData<float>(bufferHandle, 0, region);
            _pipeline.SetUniformBuffers([new BufferAssignment(0, new BufferRange(bufferHandle, 0, RegionBufferSize))]);

            Span<Viewport> viewports = stackalloc Viewport[16];

            var rect = new Rectangle<float>(
                MathF.Min(dstRegion.X1, dstRegion.X2),
                MathF.Min(dstRegion.Y1, dstRegion.Y2),
                MathF.Abs(dstRegion.X2 - dstRegion.X1),
                MathF.Abs(dstRegion.Y2 - dstRegion.Y1));

            viewports[0] = new Viewport(
                rect,
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            _pipeline.SetProgram(_programColorBlitF);
            _pipeline.SetViewports(viewports);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);

            _renderer.BufferManager.Delete(bufferHandle);

            // Restore previous state
            _pipeline.RestorePredrawState(state);
        }

        public void ConvertI8ToI16(CommandBufferScoped cbs, BufferHolder src, BufferHolder dst, int srcOffset, int size)
        {
            ChangeStride(cbs, src, dst, srcOffset, size, 1, 2);
        }

        public unsafe void ChangeStride(
            CommandBufferScoped cbs,
            BufferHolder src,
            BufferHolder dst,
            int srcOffset,
            int size,
            int stride,
            int newStride)
        {
            int elems = size / stride;

            var srcBuffer = src.GetBuffer();
            var dstBuffer = dst.GetBuffer();

            const int ParamsBufferSize = 16;

            // Save current state
            _pipeline.SwapState(_helperShaderState);

            Span<int> shaderParams = stackalloc int[ParamsBufferSize / sizeof(int)];

            shaderParams[0] = stride;
            shaderParams[1] = newStride;
            shaderParams[2] = size;
            shaderParams[3] = srcOffset;

            using var buffer = _renderer.BufferManager.ReserveOrCreate(cbs, ParamsBufferSize);
            buffer.Holder.SetDataUnchecked<int>(buffer.Offset, shaderParams);
            _pipeline.SetUniformBuffers([new BufferAssignment(0, buffer.Range)]);

            Span<Auto<DisposableBuffer>> sbRanges = new Auto<DisposableBuffer>[2];

            sbRanges[0] = srcBuffer;
            sbRanges[1] = dstBuffer;
            _pipeline.SetStorageBuffers(1, sbRanges);

            _pipeline.SetProgram(_programStrideChange);
            _pipeline.DispatchCompute(1 + elems / ConvertElementsPerWorkgroup, 1, 1);

            // Restore previous state
            _pipeline.SwapState(null);
        }

        public unsafe void ClearColor(
            int index,
            ReadOnlySpan<float> clearColor,
            uint componentMask,
            int dstWidth,
            int dstHeight,
            Format format)
        {
            // Keep original scissor
            DirtyFlags clearFlags = DirtyFlags.All & (~DirtyFlags.Scissors);

            // Save current state
            EncoderState originalState = _pipeline.SwapState(_helperShaderState, clearFlags, false);

            // Inherit some state without fully recreating render pipeline.
            RenderTargetCopy save = _helperShaderState.InheritForClear(originalState, false, index);

            const int ClearColorBufferSize = 16;

            // TODO: Flush

            using var buffer = _renderer.BufferManager.ReserveOrCreate(_pipeline.Cbs, ClearColorBufferSize);
            buffer.Holder.SetDataUnchecked(buffer.Offset, clearColor);
            _pipeline.SetUniformBuffers([new BufferAssignment(0, buffer.Range)]);

            Span<Viewport> viewports = stackalloc Viewport[16];

            // TODO: Set exact viewport!
            viewports[0] = new Viewport(
                new Rectangle<float>(0, 0, dstWidth, dstHeight),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            Span<uint> componentMasks = stackalloc uint[index + 1];
            componentMasks[index] = componentMask;

            if (format.IsSint())
            {
                _pipeline.SetProgram(_programsColorClearI[index]);
            }
            else if (format.IsUint())
            {
                _pipeline.SetProgram(_programsColorClearU[index]);
            }
            else
            {
                _pipeline.SetProgram(_programsColorClearF[index]);
            }

            _pipeline.SetBlendState(index, new BlendDescriptor());
            _pipeline.SetFaceCulling(false, Face.Front);
            _pipeline.SetDepthTest(new DepthTestDescriptor(false, false, CompareOp.Always));
            _pipeline.SetRenderTargetColorMasks(componentMasks);
            _pipeline.SetViewports(viewports);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.Draw(4, 1, 0, 0);

            // Restore previous state
            _pipeline.SwapState(null, clearFlags, false);

            _helperShaderState.Restore(save);
        }

        public unsafe void ClearDepthStencil(
            float depthValue,
            bool depthMask,
            int stencilValue,
            int stencilMask,
            int dstWidth,
            int dstHeight)
        {
            // Keep original scissor
            DirtyFlags clearFlags = DirtyFlags.All & (~DirtyFlags.Scissors);
            var helperScissors = _helperShaderState.Scissors;

            // Save current state
            EncoderState originalState = _pipeline.SwapState(_helperShaderState, clearFlags, false);

            // Inherit some state without fully recreating render pipeline.
            RenderTargetCopy save = _helperShaderState.InheritForClear(originalState, true);

            const int ClearDepthBufferSize = 16;

            using var buffer = _renderer.BufferManager.ReserveOrCreate(_pipeline.Cbs, ClearDepthBufferSize);
            buffer.Holder.SetDataUnchecked(buffer.Offset, new ReadOnlySpan<float>(ref depthValue));
            _pipeline.SetUniformBuffers([new BufferAssignment(0, buffer.Range)]);

            Span<Viewport> viewports = stackalloc Viewport[1];

            viewports[0] = new Viewport(
                new Rectangle<float>(0, 0, dstWidth, dstHeight),
                ViewportSwizzle.PositiveX,
                ViewportSwizzle.PositiveY,
                ViewportSwizzle.PositiveZ,
                ViewportSwizzle.PositiveW,
                0f,
                1f);

            _pipeline.SetProgram(_programDepthStencilClear);
            _pipeline.SetFaceCulling(false, Face.Front);
            _pipeline.SetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            _pipeline.SetViewports(viewports);
            _pipeline.SetDepthTest(new DepthTestDescriptor(true, depthMask, CompareOp.Always));
            _pipeline.SetStencilTest(CreateStencilTestDescriptor(stencilMask != 0, stencilValue, 0xFF, stencilMask));
            _pipeline.Draw(4, 1, 0, 0);

            // Cleanup
            _pipeline.SetDepthTest(new DepthTestDescriptor(false, false, CompareOp.Always));
            _pipeline.SetStencilTest(CreateStencilTestDescriptor(false));

            // Restore previous state
            _pipeline.SwapState(null, clearFlags, false);

            _helperShaderState.Restore(save);
        }

        private static StencilTestDescriptor CreateStencilTestDescriptor(
            bool enabled,
            int refValue = 0,
            int compareMask = 0xff,
            int writeMask = 0xff)
        {
            return new StencilTestDescriptor(
                enabled,
                CompareOp.Always,
                StencilOp.Replace,
                StencilOp.Replace,
                StencilOp.Replace,
                refValue,
                compareMask,
                writeMask,
                CompareOp.Always,
                StencilOp.Replace,
                StencilOp.Replace,
                StencilOp.Replace,
                refValue,
                compareMask,
                writeMask);
        }

        public void Dispose()
        {
            _programColorBlitF.Dispose();
            _programColorBlitI.Dispose();
            _programColorBlitU.Dispose();
            _programColorBlitMsF.Dispose();
            _programColorBlitMsI.Dispose();
            _programColorBlitMsU.Dispose();

            foreach (var programColorClear in _programsColorClearF)
            {
                programColorClear.Dispose();
            }

            foreach (var programColorClear in _programsColorClearU)
            {
                programColorClear.Dispose();
            }

            foreach (var programColorClear in _programsColorClearI)
            {
                programColorClear.Dispose();
            }

            _programDepthStencilClear.Dispose();
            _pipeline.Dispose();
            _samplerLinear.Dispose();
            _samplerNearest.Dispose();
        }
    }
}
