using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Threed;
using Ryujinx.Graphics.Gpu.Engine.Types;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Gpu.Shader.DiskCache;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// Memory cache of shader code.
    /// </summary>
    class ShaderCache : IDisposable
    {
        /// <summary>
        /// Default flags used on the shader translation process.
        /// </summary>
        public const TranslationFlags DefaultFlags = TranslationFlags.DebugMode;

        private readonly struct TranslatedShader
        {
            public readonly CachedShaderStage Shader;
            public readonly ShaderProgram Program;

            public TranslatedShader(CachedShaderStage shader, ShaderProgram program)
            {
                Shader = shader;
                Program = program;
            }
        }

        private readonly struct TranslatedShaderVertexPair
        {
            public readonly CachedShaderStage VertexA;
            public readonly CachedShaderStage VertexB;
            public readonly ShaderProgram Program;

            public TranslatedShaderVertexPair(CachedShaderStage vertexA, CachedShaderStage vertexB, ShaderProgram program)
            {
                VertexA = vertexA;
                VertexB = vertexB;
                Program = program;
            }
        }

        private readonly GpuContext _context;

        private readonly ShaderDumper _dumper;

        private readonly Dictionary<ulong, CachedShaderProgram> _cpPrograms;
        private readonly Dictionary<ShaderAddresses, CachedShaderProgram> _gpPrograms;

        private readonly struct ProgramToSave
        {
            public readonly CachedShaderProgram CachedProgram;
            public readonly IProgram HostProgram;
            public readonly byte[] BinaryCode;

            public ProgramToSave(CachedShaderProgram cachedProgram, IProgram hostProgram, byte[] binaryCode)
            {
                CachedProgram = cachedProgram;
                HostProgram = hostProgram;
                BinaryCode = binaryCode;
            }
        }

        private readonly Queue<ProgramToSave> _programsToSaveQueue;

        private readonly ComputeShaderCacheHashTable _computeShaderCache;
        private readonly ShaderCacheHashTable _graphicsShaderCache;
        private readonly DiskCacheHostStorage _diskCacheHostStorage;
        private readonly BackgroundDiskCacheWriter _cacheWriter;

        /// <summary>
        /// Event for signalling shader cache loading progress.
        /// </summary>
        public event Action<ShaderCacheState, int, int> ShaderCacheStateChanged;

        /// <summary>
        /// Creates a new instance of the shader cache.
        /// </summary>
        /// <param name="context">GPU context that the shader cache belongs to</param>
        public ShaderCache(GpuContext context)
        {
            _context = context;

            _dumper = new ShaderDumper();

            _cpPrograms = new Dictionary<ulong, CachedShaderProgram>();
            _gpPrograms = new Dictionary<ShaderAddresses, CachedShaderProgram>();

            _programsToSaveQueue = new Queue<ProgramToSave>();

            string diskCacheTitleId = GetDiskCachePath();

            _computeShaderCache = new ComputeShaderCacheHashTable();
            _graphicsShaderCache = new ShaderCacheHashTable();
            _diskCacheHostStorage = new DiskCacheHostStorage(diskCacheTitleId);

            if (_diskCacheHostStorage.CacheEnabled)
            {
                _cacheWriter = new BackgroundDiskCacheWriter(context, _diskCacheHostStorage);
            }
        }

        /// <summary>
        /// Gets the path where the disk cache for the current application is stored.
        /// </summary>
        private static string GetDiskCachePath()
        {
            return GraphicsConfig.EnableShaderCache && GraphicsConfig.TitleId != null
                ? Path.Combine(AppDataManager.GamesDirPath, GraphicsConfig.TitleId, "cache", "shader")
                : null;
        }

        /// <summary>
        /// Processes the queue of shaders that must save their binaries to the disk cache.
        /// </summary>
        public void ProcessShaderCacheQueue()
        {
            // Check to see if the binaries for previously compiled shaders are ready, and save them out.

            while (_programsToSaveQueue.TryPeek(out ProgramToSave programToSave))
            {
                ProgramLinkStatus result = programToSave.HostProgram.CheckProgramLink(false);

                if (result != ProgramLinkStatus.Incomplete)
                {
                    if (result == ProgramLinkStatus.Success)
                    {
                        _cacheWriter.AddShader(programToSave.CachedProgram, programToSave.BinaryCode ?? programToSave.HostProgram.GetBinary());
                    }

                    _programsToSaveQueue.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the shader cache initialization process</param>
        internal void Initialize(CancellationToken cancellationToken)
        {
            if (_diskCacheHostStorage.CacheEnabled)
            {
                ParallelDiskCacheLoader loader = new(
                    _context,
                    _graphicsShaderCache,
                    _computeShaderCache,
                    _diskCacheHostStorage,
                    ShaderCacheStateUpdate,
                    cancellationToken);

                loader.LoadShaders();

                int errorCount = loader.ErrorCount;
                if (errorCount != 0)
                {
                    Logger.Warning?.Print(LogClass.Gpu, $"Failed to load {errorCount} shaders from the disk cache.");
                }
            }
        }

        /// <summary>
        /// Shader cache state update handler.
        /// </summary>
        /// <param name="state">Current state of the shader cache load process</param>
        /// <param name="current">Number of the current shader being processed</param>
        /// <param name="total">Total number of shaders to process</param>
        private void ShaderCacheStateUpdate(ShaderCacheState state, int current, int total)
        {
            ShaderCacheStateChanged?.Invoke(state, current, total);
        }

        /// <summary>
        /// Gets a compute shader from the cache.
        /// </summary>
        /// <remarks>
        /// This automatically translates, compiles and adds the code to the cache if not present.
        /// </remarks>
        /// <param name="channel">GPU channel</param>
        /// <param name="samplerPoolMaximumId">Maximum ID that an entry in the sampler pool may have</param>
        /// <param name="poolState">Texture pool state</param>
        /// <param name="computeState">Compute engine state</param>
        /// <param name="gpuVa">GPU virtual address of the binary shader code</param>
        /// <returns>Compiled compute shader code</returns>
        public CachedShaderProgram GetComputeShader(
            GpuChannel channel,
            int samplerPoolMaximumId,
            GpuChannelPoolState poolState,
            GpuChannelComputeState computeState,
            ulong gpuVa)
        {
            if (_cpPrograms.TryGetValue(gpuVa, out var cpShader) && IsShaderEqual(channel, poolState, computeState, cpShader, gpuVa))
            {
                return cpShader;
            }

            if (_computeShaderCache.TryFind(channel, poolState, computeState, gpuVa, out cpShader, out byte[] cachedGuestCode))
            {
                _cpPrograms[gpuVa] = cpShader;
                return cpShader;
            }

            ShaderSpecializationState specState = new(ref computeState);
            GpuAccessorState gpuAccessorState = new(samplerPoolMaximumId, poolState, computeState, default, specState);
            GpuAccessor gpuAccessor = new(_context, channel, gpuAccessorState);
            gpuAccessor.InitializeReservedCounts(tfEnabled: false, vertexAsCompute: false);

            TranslatorContext translatorContext = DecodeComputeShader(gpuAccessor, _context.Capabilities.Api, gpuVa);
            TranslatedShader translatedShader = TranslateShader(_dumper, channel, translatorContext, cachedGuestCode, asCompute: false);

            ShaderSource[] shaderSourcesArray = new ShaderSource[] { CreateShaderSource(translatedShader.Program) };
            ShaderInfo info = ShaderInfoBuilder.BuildForCompute(
                _context,
                translatedShader.Program.Info,
                computeState.GetLocalSize());
            IProgram hostProgram = _context.Renderer.CreateProgram(shaderSourcesArray, info);

            cpShader = new CachedShaderProgram(hostProgram, specState, translatedShader.Shader);

            _computeShaderCache.Add(cpShader);
            EnqueueProgramToSave(cpShader, hostProgram, shaderSourcesArray);
            _cpPrograms[gpuVa] = cpShader;

            return cpShader;
        }

        /// <summary>
        /// Updates the shader pipeline state based on the current GPU state.
        /// </summary>
        /// <param name="state">Current GPU 3D engine state</param>
        /// <param name="pipeline">Shader pipeline state to be updated</param>
        /// <param name="graphicsState">Current graphics state</param>
        /// <param name="channel">Current GPU channel</param>
        private static void UpdatePipelineInfo(
            ref ThreedClassState state,
            ref ProgramPipelineState pipeline,
            GpuChannelGraphicsState graphicsState,
            GpuChannel channel)
        {
            channel.TextureManager.UpdateRenderTargets();

            var rtControl = state.RtControl;
            var msaaMode = state.RtMsaaMode;

            pipeline.SamplesCount = msaaMode.SamplesInX() * msaaMode.SamplesInY();

            int count = rtControl.UnpackCount();

            for (int index = 0; index < Constants.TotalRenderTargets; index++)
            {
                int rtIndex = rtControl.UnpackPermutationIndex(index);

                var colorState = state.RtColorState[rtIndex];

                if (index >= count || colorState.Format == 0 || colorState.WidthOrStride == 0)
                {
                    pipeline.AttachmentEnable[index] = false;
                    pipeline.AttachmentFormats[index] = Format.R8G8B8A8Unorm;
                }
                else
                {
                    pipeline.AttachmentEnable[index] = true;
                    pipeline.AttachmentFormats[index] = colorState.Format.Convert().Format;
                }
            }

            pipeline.DepthStencilEnable = state.RtDepthStencilEnable;
            pipeline.DepthStencilFormat = pipeline.DepthStencilEnable ? state.RtDepthStencilState.Format.Convert().Format : Format.D24UnormS8Uint;

            pipeline.VertexBufferCount = Constants.TotalVertexBuffers;
            pipeline.Topology = graphicsState.Topology;
        }

        /// <summary>
        /// Gets a graphics shader program from the shader cache.
        /// This includes all the specified shader stages.
        /// </summary>
        /// <remarks>
        /// This automatically translates, compiles and adds the code to the cache if not present.
        /// </remarks>
        /// <param name="state">GPU state</param>
        /// <param name="pipeline">Pipeline state</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="samplerPoolMaximumId">Maximum ID that an entry in the sampler pool may have</param>
        /// <param name="poolState">Texture pool state</param>
        /// <param name="graphicsState">3D engine state</param>
        /// <param name="addresses">Addresses of the shaders for each stage</param>
        /// <returns>Compiled graphics shader code</returns>
        public CachedShaderProgram GetGraphicsShader(
            ref ThreedClassState state,
            ref ProgramPipelineState pipeline,
            GpuChannel channel,
            int samplerPoolMaximumId,
            ref GpuChannelPoolState poolState,
            ref GpuChannelGraphicsState graphicsState,
            ShaderAddresses addresses)
        {
            if (_gpPrograms.TryGetValue(addresses, out var gpShaders) && IsShaderEqual(channel, ref poolState, ref graphicsState, gpShaders, addresses))
            {
                return gpShaders;
            }

            if (_graphicsShaderCache.TryFind(channel, ref poolState, ref graphicsState, addresses, out gpShaders, out var cachedGuestCode))
            {
                _gpPrograms[addresses] = gpShaders;
                return gpShaders;
            }

            TransformFeedbackDescriptor[] transformFeedbackDescriptors = GetTransformFeedbackDescriptors(ref state);

            UpdatePipelineInfo(ref state, ref pipeline, graphicsState, channel);

            ShaderSpecializationState specState = new(ref graphicsState, ref pipeline, transformFeedbackDescriptors);
            GpuAccessorState gpuAccessorState = new(samplerPoolMaximumId, poolState, default, graphicsState, specState, transformFeedbackDescriptors);

            ReadOnlySpan<ulong> addressesSpan = addresses.AsSpan();

            GpuAccessor[] gpuAccessors = new GpuAccessor[Constants.ShaderStages];
            TranslatorContext[] translatorContexts = new TranslatorContext[Constants.ShaderStages + 1];
            TranslatorContext nextStage = null;

            TargetApi api = _context.Capabilities.Api;

            for (int stageIndex = Constants.ShaderStages - 1; stageIndex >= 0; stageIndex--)
            {
                ulong gpuVa = addressesSpan[stageIndex + 1];

                if (gpuVa != 0)
                {
                    GpuAccessor gpuAccessor = new(_context, channel, gpuAccessorState, stageIndex, addresses.Geometry != 0);
                    TranslatorContext currentStage = DecodeGraphicsShader(gpuAccessor, api, DefaultFlags, gpuVa);

                    if (nextStage != null)
                    {
                        currentStage.SetNextStage(nextStage);
                    }

                    if (stageIndex == 0 && addresses.VertexA != 0)
                    {
                        translatorContexts[0] = DecodeGraphicsShader(gpuAccessor, api, DefaultFlags | TranslationFlags.VertexA, addresses.VertexA);
                    }

                    gpuAccessors[stageIndex] = gpuAccessor;
                    translatorContexts[stageIndex + 1] = currentStage;
                    nextStage = currentStage;
                }
            }

            bool hasGeometryShader = translatorContexts[4] != null;
            bool vertexHasStore = translatorContexts[1] != null && translatorContexts[1].HasStore;
            bool geometryHasStore = hasGeometryShader && translatorContexts[4].HasStore;
            bool vertexToCompute = ShouldConvertVertexToCompute(_context, vertexHasStore, geometryHasStore, hasGeometryShader);
            bool geometryToCompute = ShouldConvertGeometryToCompute(_context, geometryHasStore);

            CachedShaderStage[] shaders = new CachedShaderStage[Constants.ShaderStages + 1];
            List<ShaderSource> shaderSources = new();

            TranslatorContext previousStage = null;
            ShaderInfoBuilder infoBuilder = new(_context, transformFeedbackDescriptors != null, vertexToCompute);

            if (geometryToCompute && translatorContexts[4] != null)
            {
                translatorContexts[4].SetVertexOutputMapForGeometryAsCompute(translatorContexts[1]);
            }

            ShaderAsCompute vertexAsCompute = null;
            ShaderAsCompute geometryAsCompute = null;

            for (int stageIndex = 0; stageIndex < Constants.ShaderStages; stageIndex++)
            {
                TranslatorContext currentStage = translatorContexts[stageIndex + 1];

                if (currentStage != null)
                {
                    gpuAccessors[stageIndex].InitializeReservedCounts(transformFeedbackDescriptors != null, vertexToCompute);

                    ShaderProgram program;

                    bool asCompute = (stageIndex == 0 && vertexToCompute) || (stageIndex == 3 && geometryToCompute);

                    if (stageIndex == 0 && translatorContexts[0] != null)
                    {
                        TranslatedShaderVertexPair translatedShader = TranslateShader(
                            _dumper,
                            channel,
                            currentStage,
                            translatorContexts[0],
                            cachedGuestCode.VertexACode,
                            cachedGuestCode.VertexBCode,
                            asCompute);

                        shaders[0] = translatedShader.VertexA;
                        shaders[1] = translatedShader.VertexB;
                        program = translatedShader.Program;
                    }
                    else
                    {
                        byte[] code = cachedGuestCode.GetByIndex(stageIndex);

                        TranslatedShader translatedShader = TranslateShader(_dumper, channel, currentStage, code, asCompute);

                        shaders[stageIndex + 1] = translatedShader.Shader;
                        program = translatedShader.Program;
                    }

                    if (asCompute)
                    {
                        bool tfEnabled = transformFeedbackDescriptors != null;

                        if (stageIndex == 0)
                        {
                            vertexAsCompute = CreateHostVertexAsComputeProgram(program, currentStage, tfEnabled);

                            TranslatorContext lastInVertexPipeline = geometryToCompute ? translatorContexts[4] ?? currentStage : currentStage;

                            (program, ShaderProgramInfo vacInfo) = lastInVertexPipeline.GenerateVertexPassthroughForCompute();
                            infoBuilder.AddStageInfoVac(vacInfo);
                        }
                        else
                        {
                            geometryAsCompute = CreateHostVertexAsComputeProgram(program, currentStage, tfEnabled);
                            program = null;
                        }
                    }

                    if (program != null)
                    {
                        shaderSources.Add(CreateShaderSource(program));
                        infoBuilder.AddStageInfo(program.Info);
                    }

                    previousStage = currentStage;
                }
                else if (
                    previousStage != null &&
                    previousStage.LayerOutputWritten &&
                    stageIndex == 3 &&
                    !_context.Capabilities.SupportsLayerVertexTessellation)
                {
                    shaderSources.Add(CreateShaderSource(previousStage.GenerateGeometryPassthrough()));
                }
            }

            ShaderSource[] shaderSourcesArray = shaderSources.ToArray();

            ShaderInfo info = infoBuilder.Build(pipeline);

            IProgram hostProgram = _context.Renderer.CreateProgram(shaderSourcesArray, info);

            gpShaders = new(hostProgram, vertexAsCompute, geometryAsCompute, specState, shaders);

            _graphicsShaderCache.Add(gpShaders);

            // We don't currently support caching shaders that have been converted to compute.
            if (vertexAsCompute == null)
            {
                EnqueueProgramToSave(gpShaders, hostProgram, shaderSourcesArray);
            }

            _gpPrograms[addresses] = gpShaders;

            return gpShaders;
        }

        /// <summary>
        /// Checks if a vertex shader should be converted to a compute shader due to it making use of
        /// features that are not supported on the host.
        /// </summary>
        /// <param name="context">GPU context of the shader</param>
        /// <param name="vertexHasStore">Whether the vertex shader has image or storage buffer store operations</param>
        /// <param name="geometryHasStore">Whether the geometry shader has image or storage buffer store operations, if one exists</param>
        /// <param name="hasGeometryShader">Whether a geometry shader exists</param>
        /// <returns>True if the vertex shader should be converted to compute, false otherwise</returns>
        public static bool ShouldConvertVertexToCompute(GpuContext context, bool vertexHasStore, bool geometryHasStore, bool hasGeometryShader)
        {
            // If the host does not support store operations on vertex,
            // we need to emulate it on a compute shader.
            if (!context.Capabilities.SupportsVertexStoreAndAtomics && vertexHasStore)
            {
                return true;
            }

            // If any stage after the vertex stage is converted to compute,
            // we need to convert vertex to compute too.
            return hasGeometryShader && ShouldConvertGeometryToCompute(context, geometryHasStore);
        }

        /// <summary>
        /// Checks if a geometry shader should be converted to a compute shader due to it making use of
        /// features that are not supported on the host.
        /// </summary>
        /// <param name="context">GPU context of the shader</param>
        /// <param name="geometryHasStore">Whether the geometry shader has image or storage buffer store operations, if one exists</param>
        /// <returns>True if the geometry shader should be converted to compute, false otherwise</returns>
        public static bool ShouldConvertGeometryToCompute(GpuContext context, bool geometryHasStore)
        {
            return (!context.Capabilities.SupportsVertexStoreAndAtomics && geometryHasStore) ||
                   !context.Capabilities.SupportsGeometryShader;
        }

        /// <summary>
        /// Checks if it might be necessary for any vertex, tessellation or geometry shader to be converted to compute,
        /// based on the supported host features.
        /// </summary>
        /// <param name="capabilities">Host capabilities</param>
        /// <returns>True if the possibility of a shader being converted to compute exists, false otherwise</returns>
        public static bool MayConvertVtgToCompute(ref Capabilities capabilities)
        {
            return !capabilities.SupportsVertexStoreAndAtomics || !capabilities.SupportsGeometryShader;
        }

        /// <summary>
        /// Creates a compute shader from a vertex, tessellation or geometry shader that has been converted to compute.
        /// </summary>
        /// <param name="program">Shader program</param>
        /// <param name="context">Translation context of the shader</param>
        /// <param name="tfEnabled">Whether transform feedback is enabled</param>
        /// <returns>Compute shader</returns>
        private ShaderAsCompute CreateHostVertexAsComputeProgram(ShaderProgram program, TranslatorContext context, bool tfEnabled)
        {
            ShaderSource source = new(program.Code, program.BinaryCode, ShaderStage.Compute, program.Language);
            ShaderInfo info = ShaderInfoBuilder.BuildForVertexAsCompute(_context, program.Info, context.GetVertexAsComputeInfo(), tfEnabled);

            return new(_context.Renderer.CreateProgram(new[] { source }, info), program.Info, context.GetResourceReservations());
        }

        /// <summary>
        /// Creates a shader source for use with the backend from a translated shader program.
        /// </summary>
        /// <param name="program">Translated shader program</param>
        /// <returns>Shader source</returns>
        public static ShaderSource CreateShaderSource(ShaderProgram program)
        {
            return new ShaderSource(program.Code, program.BinaryCode, program.Info.Stage, program.Language);
        }

        /// <summary>
        /// Puts a program on the queue of programs to be saved on the disk cache.
        /// </summary>
        /// <remarks>
        /// This will not do anything if disk shader cache is disabled.
        /// </remarks>
        /// <param name="program">Cached shader program</param>
        /// <param name="hostProgram">Host program</param>
        /// <param name="sources">Source for each shader stage</param>
        private void EnqueueProgramToSave(CachedShaderProgram program, IProgram hostProgram, ShaderSource[] sources)
        {
            if (_diskCacheHostStorage.CacheEnabled)
            {
                byte[] binaryCode = _context.Capabilities.Api == TargetApi.Vulkan ? ShaderBinarySerializer.Pack(sources) : null;
                ProgramToSave programToSave = new(program, hostProgram, binaryCode);

                _programsToSaveQueue.Enqueue(programToSave);
            }
        }

        /// <summary>
        /// Gets transform feedback state from the current GPU state.
        /// </summary>
        /// <param name="state">Current GPU state</param>
        /// <returns>Four transform feedback descriptors for the enabled TFBs, or null if TFB is disabled</returns>
        private static TransformFeedbackDescriptor[] GetTransformFeedbackDescriptors(ref ThreedClassState state)
        {
            bool tfEnable = state.TfEnable;
            if (!tfEnable)
            {
                return null;
            }

            TransformFeedbackDescriptor[] descs = new TransformFeedbackDescriptor[Constants.TotalTransformFeedbackBuffers];

            for (int i = 0; i < Constants.TotalTransformFeedbackBuffers; i++)
            {
                var tf = state.TfState[i];

                descs[i] = new TransformFeedbackDescriptor(
                    tf.BufferIndex,
                    tf.Stride,
                    tf.VaryingsCount,
                    ref state.TfVaryingLocations[i]);
            }

            return descs;
        }

        /// <summary>
        /// Checks if compute shader code in memory is equal to the cached shader.
        /// </summary>
        /// <param name="channel">GPU channel using the shader</param>
        /// <param name="poolState">GPU channel state to verify shader compatibility</param>
        /// <param name="computeState">GPU channel compute state to verify shader compatibility</param>
        /// <param name="cpShader">Cached compute shader</param>
        /// <param name="gpuVa">GPU virtual address of the shader code in memory</param>
        /// <returns>True if the code is different, false otherwise</returns>
        private static bool IsShaderEqual(
            GpuChannel channel,
            GpuChannelPoolState poolState,
            GpuChannelComputeState computeState,
            CachedShaderProgram cpShader,
            ulong gpuVa)
        {
            if (IsShaderEqual(channel.MemoryManager, cpShader.Shaders[0], gpuVa))
            {
                return cpShader.SpecializationState.MatchesCompute(channel, ref poolState, computeState, true);
            }

            return false;
        }

        /// <summary>
        /// Checks if graphics shader code from all stages in memory are equal to the cached shaders.
        /// </summary>
        /// <param name="channel">GPU channel using the shader</param>
        /// <param name="poolState">GPU channel state to verify shader compatibility</param>
        /// <param name="graphicsState">GPU channel graphics state to verify shader compatibility</param>
        /// <param name="gpShaders">Cached graphics shaders</param>
        /// <param name="addresses">GPU virtual addresses of all enabled shader stages</param>
        /// <returns>True if the code is different, false otherwise</returns>
        private static bool IsShaderEqual(
            GpuChannel channel,
            ref GpuChannelPoolState poolState,
            ref GpuChannelGraphicsState graphicsState,
            CachedShaderProgram gpShaders,
            ShaderAddresses addresses)
        {
            ReadOnlySpan<ulong> addressesSpan = addresses.AsSpan();

            for (int stageIndex = 0; stageIndex < gpShaders.Shaders.Length; stageIndex++)
            {
                CachedShaderStage shader = gpShaders.Shaders[stageIndex];

                ulong gpuVa = addressesSpan[stageIndex];

                if (!IsShaderEqual(channel.MemoryManager, shader, gpuVa))
                {
                    return false;
                }
            }

            bool vertexAsCompute = gpShaders.VertexAsCompute != null;
            bool usesDrawParameters = gpShaders.Shaders[1]?.Info.UsesDrawParameters ?? false;

            return gpShaders.SpecializationState.MatchesGraphics(
                channel,
                ref poolState,
                ref graphicsState,
                vertexAsCompute,
                usesDrawParameters,
                checkTextures: true);
        }

        /// <summary>
        /// Checks if the code of the specified cached shader is different from the code in memory.
        /// </summary>
        /// <param name="memoryManager">Memory manager used to access the GPU memory where the shader is located</param>
        /// <param name="shader">Cached shader to compare with</param>
        /// <param name="gpuVa">GPU virtual address of the binary shader code</param>
        /// <returns>True if the code is different, false otherwise</returns>
        private static bool IsShaderEqual(MemoryManager memoryManager, CachedShaderStage shader, ulong gpuVa)
        {
            if (shader == null)
            {
                return true;
            }

            ReadOnlySpan<byte> memoryCode = memoryManager.GetSpanMapped(gpuVa, shader.Code.Length);

            return memoryCode.SequenceEqual(shader.Code);
        }

        /// <summary>
        /// Decode the binary Maxwell shader code to a translator context.
        /// </summary>
        /// <param name="gpuAccessor">GPU state accessor</param>
        /// <param name="api">Graphics API that will be used with the shader</param>
        /// <param name="gpuVa">GPU virtual address of the binary shader code</param>
        /// <returns>The generated translator context</returns>
        public static TranslatorContext DecodeComputeShader(IGpuAccessor gpuAccessor, TargetApi api, ulong gpuVa)
        {
            var options = CreateTranslationOptions(api, DefaultFlags | TranslationFlags.Compute);
            return Translator.CreateContext(gpuVa, gpuAccessor, options);
        }

        /// <summary>
        /// Decode the binary Maxwell shader code to a translator context.
        /// </summary>
        /// <remarks>
        /// This will combine the "Vertex A" and "Vertex B" shader stages, if specified, into one shader.
        /// </remarks>
        /// <param name="gpuAccessor">GPU state accessor</param>
        /// <param name="api">Graphics API that will be used with the shader</param>
        /// <param name="flags">Flags that controls shader translation</param>
        /// <param name="gpuVa">GPU virtual address of the shader code</param>
        /// <returns>The generated translator context</returns>
        public static TranslatorContext DecodeGraphicsShader(IGpuAccessor gpuAccessor, TargetApi api, TranslationFlags flags, ulong gpuVa)
        {
            var options = CreateTranslationOptions(api, flags);
            return Translator.CreateContext(gpuVa, gpuAccessor, options);
        }

        /// <summary>
        /// Translates a previously generated translator context to something that the host API accepts.
        /// </summary>
        /// <param name="dumper">Optional shader code dumper</param>
        /// <param name="channel">GPU channel using the shader</param>
        /// <param name="currentStage">Translator context of the stage to be translated</param>
        /// <param name="vertexA">Optional translator context of the shader that should be combined</param>
        /// <param name="codeA">Optional Maxwell binary code of the Vertex A shader, if present</param>
        /// <param name="codeB">Optional Maxwell binary code of the Vertex B or current stage shader, if present on cache</param>
        /// <param name="asCompute">Indicates that the vertex shader should be converted to a compute shader</param>
        /// <returns>Compiled graphics shader code</returns>
        private static TranslatedShaderVertexPair TranslateShader(
            ShaderDumper dumper,
            GpuChannel channel,
            TranslatorContext currentStage,
            TranslatorContext vertexA,
            byte[] codeA,
            byte[] codeB,
            bool asCompute)
        {
            ulong cb1DataAddress = channel.BufferManager.GetGraphicsUniformBufferAddress(0, 1);

            var memoryManager = channel.MemoryManager;

            codeA ??= memoryManager.GetSpan(vertexA.Address, vertexA.Size).ToArray();
            codeB ??= memoryManager.GetSpan(currentStage.Address, currentStage.Size).ToArray();
            byte[] cb1DataA = ReadArray(memoryManager, cb1DataAddress, vertexA.Cb1DataSize);
            byte[] cb1DataB = ReadArray(memoryManager, cb1DataAddress, currentStage.Cb1DataSize);

            ShaderDumpPaths pathsA = default;
            ShaderDumpPaths pathsB = default;

            if (dumper != null)
            {
                pathsA = dumper.Dump(codeA, compute: false);
                pathsB = dumper.Dump(codeB, compute: false);
            }

            ShaderProgram program = currentStage.Translate(vertexA, asCompute);

            pathsB.Prepend(program);
            pathsA.Prepend(program);

            CachedShaderStage vertexAStage = new(null, codeA, cb1DataA);
            CachedShaderStage vertexBStage = new(program.Info, codeB, cb1DataB);

            return new TranslatedShaderVertexPair(vertexAStage, vertexBStage, program);
        }

        /// <summary>
        /// Translates a previously generated translator context to something that the host API accepts.
        /// </summary>
        /// <param name="dumper">Optional shader code dumper</param>
        /// <param name="channel">GPU channel using the shader</param>
        /// <param name="context">Translator context of the stage to be translated</param>
        /// <param name="code">Optional Maxwell binary code of the current stage shader, if present on cache</param>
        /// <param name="asCompute">Indicates that the vertex shader should be converted to a compute shader</param>
        /// <returns>Compiled graphics shader code</returns>
        private static TranslatedShader TranslateShader(ShaderDumper dumper, GpuChannel channel, TranslatorContext context, byte[] code, bool asCompute)
        {
            var memoryManager = channel.MemoryManager;

            ulong cb1DataAddress = context.Stage == ShaderStage.Compute
                ? channel.BufferManager.GetComputeUniformBufferAddress(1)
                : channel.BufferManager.GetGraphicsUniformBufferAddress(StageToStageIndex(context.Stage), 1);

            byte[] cb1Data = ReadArray(memoryManager, cb1DataAddress, context.Cb1DataSize);
            code ??= memoryManager.GetSpan(context.Address, context.Size).ToArray();

            ShaderDumpPaths paths = dumper?.Dump(code, context.Stage == ShaderStage.Compute) ?? default;
            ShaderProgram program = context.Translate(asCompute);

            paths.Prepend(program);

            return new TranslatedShader(new CachedShaderStage(program.Info, code, cb1Data), program);
        }

        /// <summary>
        /// Reads data from physical memory, returns an empty array if the memory is unmapped or size is 0.
        /// </summary>
        /// <param name="memoryManager">Memory manager with the physical memory to read from</param>
        /// <param name="address">Physical address of the region to read</param>
        /// <param name="size">Size in bytes of the data</param>
        /// <returns>An array with the data at the specified memory location</returns>
        private static byte[] ReadArray(MemoryManager memoryManager, ulong address, int size)
        {
            if (address == MemoryManager.PteUnmapped || size == 0)
            {
                return Array.Empty<byte>();
            }

            return memoryManager.Physical.GetSpan(address, size).ToArray();
        }

        /// <summary>
        /// Gets the index of a stage from a <see cref="ShaderStage"/>.
        /// </summary>
        /// <param name="stage">Stage to get the index from</param>
        /// <returns>Stage index</returns>
        private static int StageToStageIndex(ShaderStage stage)
        {
            return stage switch
            {
                ShaderStage.TessellationControl => 1,
                ShaderStage.TessellationEvaluation => 2,
                ShaderStage.Geometry => 3,
                ShaderStage.Fragment => 4,
                _ => 0,
            };
        }

        /// <summary>
        /// Creates shader translation options with the requested graphics API and flags.
        /// The shader language is chosen based on the current configuration and graphics API.
        /// </summary>
        /// <param name="api">Target graphics API</param>
        /// <param name="flags">Translation flags</param>
        /// <returns>Translation options</returns>
        private static TranslationOptions CreateTranslationOptions(TargetApi api, TranslationFlags flags)
        {
            TargetLanguage lang = api switch
            {
                TargetApi.OpenGL => TargetLanguage.Glsl,
                TargetApi.Vulkan => GraphicsConfig.EnableSpirvCompilationOnVulkan ? TargetLanguage.Spirv : TargetLanguage.Glsl,
                TargetApi.Metal => TargetLanguage.Msl,
            };

            return new TranslationOptions(lang, api, flags);
        }

        /// <summary>
        /// Disposes the shader cache, deleting all the cached shaders.
        /// It's an error to use the shader cache after disposal.
        /// </summary>
        public void Dispose()
        {
            foreach (CachedShaderProgram program in _graphicsShaderCache.GetPrograms())
            {
                program.Dispose();
            }

            foreach (CachedShaderProgram program in _computeShaderCache.GetPrograms())
            {
                program.Dispose();
            }

            _cacheWriter?.Dispose();
        }
    }
}
