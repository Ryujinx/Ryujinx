using Ryujinx.Graphics.GAL.Multithreading.Commands;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Buffer;
using Ryujinx.Graphics.GAL.Multithreading.Commands.CounterEvent;
using Ryujinx.Graphics.GAL.Multithreading.Commands.ImageArray;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Program;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Renderer;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Sampler;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Texture;
using Ryujinx.Graphics.GAL.Multithreading.Commands.TextureArray;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Window;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.GAL.Multithreading
{
    static class CommandHelper
    {
        private delegate void CommandDelegate(Span<byte> memory, ThreadedRenderer threaded, IRenderer renderer);

        private static readonly int _totalCommands = (int)Enum.GetValues<CommandType>().Max() + 1;
        private static readonly CommandDelegate[] _lookup = new CommandDelegate[_totalCommands];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref T GetCommand<T>(Span<byte> memory)
        {
            return ref Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(memory));
        }

        public static int GetMaxCommandSize()
        {
            return InitLookup() + 1; // 1 byte reserved for command size.
        }

        private static int InitLookup()
        {
            int maxCommandSize = 0;

            void Register<T>(CommandType commandType) where T : unmanaged, IGALCommand, IGALCommand<T>
            {
                maxCommandSize = Math.Max(maxCommandSize, Unsafe.SizeOf<T>());
                _lookup[(int)commandType] = (memory, threaded, renderer) => T.Run(ref GetCommand<T>(memory), threaded, renderer);
            }

            Register<ActionCommand>(CommandType.Action);
            Register<CreateBufferCommand>(CommandType.CreateBuffer);
            Register<CreateBufferAccessCommand>(CommandType.CreateBufferAccess);
            Register<CreateBufferSparseCommand>(CommandType.CreateBufferSparse);
            Register<CreateHostBufferCommand>(CommandType.CreateHostBuffer);
            Register<CreateImageArrayCommand>(CommandType.CreateImageArray);
            Register<CreateProgramCommand>(CommandType.CreateProgram);
            Register<CreateSamplerCommand>(CommandType.CreateSampler);
            Register<CreateSyncCommand>(CommandType.CreateSync);
            Register<CreateTextureCommand>(CommandType.CreateTexture);
            Register<CreateTextureArrayCommand>(CommandType.CreateTextureArray);
            Register<GetCapabilitiesCommand>(CommandType.GetCapabilities);
            Register<PreFrameCommand>(CommandType.PreFrame);
            Register<ReportCounterCommand>(CommandType.ReportCounter);
            Register<ResetCounterCommand>(CommandType.ResetCounter);
            Register<UpdateCountersCommand>(CommandType.UpdateCounters);

            Register<BufferDisposeCommand>(CommandType.BufferDispose);
            Register<BufferGetDataCommand>(CommandType.BufferGetData);
            Register<BufferSetDataCommand>(CommandType.BufferSetData);

            Register<CounterEventDisposeCommand>(CommandType.CounterEventDispose);
            Register<CounterEventFlushCommand>(CommandType.CounterEventFlush);

            Register<ImageArraySetFormatsCommand>(CommandType.ImageArraySetFormats);
            Register<ImageArraySetImagesCommand>(CommandType.ImageArraySetImages);

            Register<ProgramDisposeCommand>(CommandType.ProgramDispose);
            Register<ProgramGetBinaryCommand>(CommandType.ProgramGetBinary);
            Register<ProgramCheckLinkCommand>(CommandType.ProgramCheckLink);

            Register<SamplerDisposeCommand>(CommandType.SamplerDispose);

            Register<TextureCopyToCommand>(CommandType.TextureCopyTo);
            Register<TextureCopyToScaledCommand>(CommandType.TextureCopyToScaled);
            Register<TextureCopyToSliceCommand>(CommandType.TextureCopyToSlice);
            Register<TextureCopyToBufferCommand>(CommandType.TextureCopyToBuffer);
            Register<TextureCreateViewCommand>(CommandType.TextureCreateView);
            Register<TextureGetDataCommand>(CommandType.TextureGetData);
            Register<TextureGetDataSliceCommand>(CommandType.TextureGetDataSlice);
            Register<TextureReleaseCommand>(CommandType.TextureRelease);
            Register<TextureSetDataCommand>(CommandType.TextureSetData);
            Register<TextureSetDataSliceCommand>(CommandType.TextureSetDataSlice);
            Register<TextureSetDataSliceRegionCommand>(CommandType.TextureSetDataSliceRegion);
            Register<TextureSetStorageCommand>(CommandType.TextureSetStorage);

            Register<TextureArraySetSamplersCommand>(CommandType.TextureArraySetSamplers);
            Register<TextureArraySetTexturesCommand>(CommandType.TextureArraySetTextures);

            Register<WindowPresentCommand>(CommandType.WindowPresent);

            Register<BarrierCommand>(CommandType.Barrier);
            Register<BeginTransformFeedbackCommand>(CommandType.BeginTransformFeedback);
            Register<ClearBufferCommand>(CommandType.ClearBuffer);
            Register<ClearRenderTargetColorCommand>(CommandType.ClearRenderTargetColor);
            Register<ClearRenderTargetDepthStencilCommand>(CommandType.ClearRenderTargetDepthStencil);
            Register<CommandBufferBarrierCommand>(CommandType.CommandBufferBarrier);
            Register<CopyBufferCommand>(CommandType.CopyBuffer);
            Register<DispatchComputeCommand>(CommandType.DispatchCompute);
            Register<DrawCommand>(CommandType.Draw);
            Register<DrawIndexedCommand>(CommandType.DrawIndexed);
            Register<DrawIndexedIndirectCommand>(CommandType.DrawIndexedIndirect);
            Register<DrawIndexedIndirectCountCommand>(CommandType.DrawIndexedIndirectCount);
            Register<DrawIndirectCommand>(CommandType.DrawIndirect);
            Register<DrawIndirectCountCommand>(CommandType.DrawIndirectCount);
            Register<DrawTextureCommand>(CommandType.DrawTexture);
            Register<EndHostConditionalRenderingCommand>(CommandType.EndHostConditionalRendering);
            Register<EndTransformFeedbackCommand>(CommandType.EndTransformFeedback);
            Register<SetAlphaTestCommand>(CommandType.SetAlphaTest);
            Register<SetBlendStateAdvancedCommand>(CommandType.SetBlendStateAdvanced);
            Register<SetBlendStateCommand>(CommandType.SetBlendState);
            Register<SetDepthBiasCommand>(CommandType.SetDepthBias);
            Register<SetDepthClampCommand>(CommandType.SetDepthClamp);
            Register<SetDepthModeCommand>(CommandType.SetDepthMode);
            Register<SetDepthTestCommand>(CommandType.SetDepthTest);
            Register<SetFaceCullingCommand>(CommandType.SetFaceCulling);
            Register<SetFrontFaceCommand>(CommandType.SetFrontFace);
            Register<SetStorageBuffersCommand>(CommandType.SetStorageBuffers);
            Register<SetTransformFeedbackBuffersCommand>(CommandType.SetTransformFeedbackBuffers);
            Register<SetUniformBuffersCommand>(CommandType.SetUniformBuffers);
            Register<SetImageCommand>(CommandType.SetImage);
            Register<SetImageArrayCommand>(CommandType.SetImageArray);
            Register<SetIndexBufferCommand>(CommandType.SetIndexBuffer);
            Register<SetLineParametersCommand>(CommandType.SetLineParameters);
            Register<SetLogicOpStateCommand>(CommandType.SetLogicOpState);
            Register<SetMultisampleStateCommand>(CommandType.SetMultisampleState);
            Register<SetPatchParametersCommand>(CommandType.SetPatchParameters);
            Register<SetPointParametersCommand>(CommandType.SetPointParameters);
            Register<SetPolygonModeCommand>(CommandType.SetPolygonMode);
            Register<SetPrimitiveRestartCommand>(CommandType.SetPrimitiveRestart);
            Register<SetPrimitiveTopologyCommand>(CommandType.SetPrimitiveTopology);
            Register<SetProgramCommand>(CommandType.SetProgram);
            Register<SetRasterizerDiscardCommand>(CommandType.SetRasterizerDiscard);
            Register<SetRenderTargetColorMasksCommand>(CommandType.SetRenderTargetColorMasks);
            Register<SetRenderTargetsCommand>(CommandType.SetRenderTargets);
            Register<SetScissorsCommand>(CommandType.SetScissor);
            Register<SetStencilTestCommand>(CommandType.SetStencilTest);
            Register<SetTextureAndSamplerCommand>(CommandType.SetTextureAndSampler);
            Register<SetTextureArrayCommand>(CommandType.SetTextureArray);
            Register<SetUserClipDistanceCommand>(CommandType.SetUserClipDistance);
            Register<SetVertexAttribsCommand>(CommandType.SetVertexAttribs);
            Register<SetVertexBuffersCommand>(CommandType.SetVertexBuffers);
            Register<SetViewportsCommand>(CommandType.SetViewports);
            Register<TextureBarrierCommand>(CommandType.TextureBarrier);
            Register<TextureBarrierTiledCommand>(CommandType.TextureBarrierTiled);
            Register<TryHostConditionalRenderingCommand>(CommandType.TryHostConditionalRendering);
            Register<TryHostConditionalRenderingFlushCommand>(CommandType.TryHostConditionalRenderingFlush);

            return maxCommandSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunCommand(Span<byte> memory, ThreadedRenderer threaded, IRenderer renderer)
        {
            _lookup[memory[^1]](memory, threaded, renderer);
        }
    }
}
