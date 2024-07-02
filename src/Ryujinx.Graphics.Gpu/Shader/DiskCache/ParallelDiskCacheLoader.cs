using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static Ryujinx.Graphics.Gpu.Shader.ShaderCache;

namespace Ryujinx.Graphics.Gpu.Shader.DiskCache
{
    class ParallelDiskCacheLoader
    {
        private const int ThreadCount = 8;

        private readonly GpuContext _context;
        private readonly ShaderCacheHashTable _graphicsCache;
        private readonly ComputeShaderCacheHashTable _computeCache;
        private readonly DiskCacheHostStorage _hostStorage;
        private readonly CancellationToken _cancellationToken;
        private readonly Action<ShaderCacheState, int, int> _stateChangeCallback;

        /// <summary>
        /// Indicates if the cache should be loaded.
        /// </summary>
        public bool Active => !_cancellationToken.IsCancellationRequested;

        private bool _needsHostRegen;

        /// <summary>
        /// Number of shaders that failed to compile from the cache.
        /// </summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// Program validation entry.
        /// </summary>
        private readonly struct ProgramEntry
        {
            /// <summary>
            /// Cached shader program.
            /// </summary>
            public readonly CachedShaderProgram CachedProgram;

            /// <summary>
            /// Optional binary code. If not null, it is used instead of the backend host binary.
            /// </summary>
            public readonly byte[] BinaryCode;

            /// <summary>
            /// Program index.
            /// </summary>
            public readonly int ProgramIndex;

            /// <summary>
            /// Indicates if the program is a compute shader.
            /// </summary>
            public readonly bool IsCompute;

            /// <summary>
            /// Indicates if the program is a host binary shader.
            /// </summary>
            public readonly bool IsBinary;

            /// <summary>
            /// Creates a new program validation entry.
            /// </summary>
            /// <param name="cachedProgram">Cached shader program</param>
            /// <param name="binaryCode">Optional binary code. If not null, it is used instead of the backend host binary</param>
            /// <param name="programIndex">Program index</param>
            /// <param name="isCompute">Indicates if the program is a compute shader</param>
            /// <param name="isBinary">Indicates if the program is a host binary shader</param>
            public ProgramEntry(
                CachedShaderProgram cachedProgram,
                byte[] binaryCode,
                int programIndex,
                bool isCompute,
                bool isBinary)
            {
                CachedProgram = cachedProgram;
                BinaryCode = binaryCode;
                ProgramIndex = programIndex;
                IsCompute = isCompute;
                IsBinary = isBinary;
            }
        }

        /// <summary>
        /// Translated shader compilation entry.
        /// </summary>
        private readonly struct ProgramCompilation
        {
            /// <summary>
            /// Translated shader stages.
            /// </summary>
            public readonly ShaderProgram[] TranslatedStages;

            /// <summary>
            /// Cached shaders.
            /// </summary>
            public readonly CachedShaderStage[] Shaders;

            /// <summary>
            /// Specialization state.
            /// </summary>
            public readonly ShaderSpecializationState SpecializationState;

            /// <summary>
            /// Program index.
            /// </summary>
            public readonly int ProgramIndex;

            /// <summary>
            /// Indicates if the program is a compute shader.
            /// </summary>
            public readonly bool IsCompute;

            /// <summary>
            /// Creates a new translated shader compilation entry.
            /// </summary>
            /// <param name="translatedStages">Translated shader stages</param>
            /// <param name="shaders">Cached shaders</param>
            /// <param name="specState">Specialization state</param>
            /// <param name="programIndex">Program index</param>
            /// <param name="isCompute">Indicates if the program is a compute shader</param>
            public ProgramCompilation(
                ShaderProgram[] translatedStages,
                CachedShaderStage[] shaders,
                ShaderSpecializationState specState,
                int programIndex,
                bool isCompute)
            {
                TranslatedStages = translatedStages;
                Shaders = shaders;
                SpecializationState = specState;
                ProgramIndex = programIndex;
                IsCompute = isCompute;
            }
        }

        /// <summary>
        /// Program translation entry.
        /// </summary>
        private readonly struct AsyncProgramTranslation
        {
            /// <summary>
            /// Guest code for each active stage.
            /// </summary>
            public readonly GuestCodeAndCbData?[] GuestShaders;

            /// <summary>
            /// Specialization state.
            /// </summary>
            public readonly ShaderSpecializationState SpecializationState;

            /// <summary>
            /// Program index.
            /// </summary>
            public readonly int ProgramIndex;

            /// <summary>
            /// Indicates if the program is a compute shader.
            /// </summary>
            public readonly bool IsCompute;

            /// <summary>
            /// Creates a new program translation entry.
            /// </summary>
            /// <param name="guestShaders">Guest code for each active stage</param>
            /// <param name="specState">Specialization state</param>
            /// <param name="programIndex">Program index</param>
            /// <param name="isCompute">Indicates if the program is a compute shader</param>
            public AsyncProgramTranslation(
                GuestCodeAndCbData?[] guestShaders,
                ShaderSpecializationState specState,
                int programIndex,
                bool isCompute)
            {
                GuestShaders = guestShaders;
                SpecializationState = specState;
                ProgramIndex = programIndex;
                IsCompute = isCompute;
            }
        }

        private readonly Queue<ProgramEntry> _validationQueue;
        private readonly ConcurrentQueue<ProgramCompilation> _compilationQueue;
        private readonly BlockingCollection<AsyncProgramTranslation> _asyncTranslationQueue;
        private readonly SortedList<int, (CachedShaderProgram, byte[])> _programList;

        private readonly int _backendParallelCompileThreads;
        private int _compiledCount;
        private int _totalCount;

        /// <summary>
        /// Creates a new parallel disk cache loader.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="graphicsCache">Graphics shader cache</param>
        /// <param name="computeCache">Compute shader cache</param>
        /// <param name="hostStorage">Disk cache host storage</param>
        /// <param name="stateChangeCallback">Function to be called when there is a state change, reporting state, compiled and total shaders count</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public ParallelDiskCacheLoader(GpuContext context,
            ShaderCacheHashTable graphicsCache,
            ComputeShaderCacheHashTable computeCache,
            DiskCacheHostStorage hostStorage,
            Action<ShaderCacheState, int, int> stateChangeCallback,
            CancellationToken cancellationToken)
        {
            _context = context;
            _graphicsCache = graphicsCache;
            _computeCache = computeCache;
            _hostStorage = hostStorage;
            _stateChangeCallback = stateChangeCallback;
            _cancellationToken = cancellationToken;
            _validationQueue = new Queue<ProgramEntry>();
            _compilationQueue = new ConcurrentQueue<ProgramCompilation>();
            _asyncTranslationQueue = new BlockingCollection<AsyncProgramTranslation>(ThreadCount);
            _programList = new SortedList<int, (CachedShaderProgram, byte[])>();
            _backendParallelCompileThreads = Math.Min(Environment.ProcessorCount, 8); // Must be kept in sync with the backend code.
        }

        /// <summary>
        /// Loads all shaders from the cache.
        /// </summary>
        public void LoadShaders()
        {
            Thread[] workThreads = new Thread[ThreadCount];

            for (int index = 0; index < ThreadCount; index++)
            {
                workThreads[index] = new Thread(ProcessAsyncQueue)
                {
                    Name = $"GPU.AsyncTranslationThread.{index}",
                };
            }

            int programCount = _hostStorage.GetProgramCount();

            _compiledCount = 0;
            _totalCount = programCount;

            _stateChangeCallback(ShaderCacheState.Start, 0, programCount);

            Logger.Info?.Print(LogClass.Gpu, $"Loading {programCount} shaders from the cache...");

            for (int index = 0; index < ThreadCount; index++)
            {
                workThreads[index].Start(_cancellationToken);
            }

            try
            {
                _hostStorage.LoadShaders(_context, this);
            }
            catch (DiskCacheLoadException diskCacheLoadException)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Error loading the shader cache. {diskCacheLoadException.Message}");

                // If we can't even access the file, then we also can't rebuild.
                if (diskCacheLoadException.Result != DiskCacheLoadResult.NoAccess)
                {
                    _needsHostRegen = true;
                }
            }
            catch (InvalidDataException invalidDataException)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Error decompressing the shader cache file. {invalidDataException.Message}");
                _needsHostRegen = true;
            }
            catch (IOException ioException)
            {
                Logger.Warning?.Print(LogClass.Gpu, $"Error reading the shader cache file. {ioException.Message}");
                _needsHostRegen = true;
            }

            _asyncTranslationQueue.CompleteAdding();

            for (int index = 0; index < ThreadCount; index++)
            {
                workThreads[index].Join();
            }

            CheckCompilationBlocking();

            if (_needsHostRegen && Active)
            {
                // Rebuild both shared and host cache files.
                // Rebuilding shared is required because the shader information returned by the translator
                // might have changed, and so we have to reconstruct the file with the new information.
                try
                {
                    _hostStorage.ClearSharedCache();
                    _hostStorage.ClearHostCache(_context);

                    if (_programList.Count != 0)
                    {
                        _stateChangeCallback(ShaderCacheState.Packaging, 0, _programList.Count);

                        Logger.Info?.Print(LogClass.Gpu, $"Rebuilding {_programList.Count} shaders...");

                        using var streams = _hostStorage.GetOutputStreams(_context);

                        int packagedShaders = 0;
                        foreach (var kv in _programList)
                        {
                            if (!Active)
                            {
                                break;
                            }

                            (CachedShaderProgram program, byte[] binaryCode) = kv.Value;

                            _hostStorage.AddShader(_context, program, binaryCode, streams);

                            _stateChangeCallback(ShaderCacheState.Packaging, ++packagedShaders, _programList.Count);
                        }

                        Logger.Info?.Print(LogClass.Gpu, $"Rebuilt {_programList.Count} shaders successfully.");
                    }
                    else
                    {
                        _hostStorage.ClearGuestCache();

                        Logger.Info?.Print(LogClass.Gpu, "Shader cache deleted due to corruption.");
                    }
                }
                catch (DiskCacheLoadException diskCacheLoadException)
                {
                    Logger.Warning?.Print(LogClass.Gpu, $"Error deleting the shader cache. {diskCacheLoadException.Message}");
                }
                catch (IOException ioException)
                {
                    Logger.Warning?.Print(LogClass.Gpu, $"Error deleting the shader cache file. {ioException.Message}");
                }
            }

            Logger.Info?.Print(LogClass.Gpu, "Shader cache loaded.");

            _stateChangeCallback(ShaderCacheState.Loaded, programCount, programCount);
        }

        /// <summary>
        /// Enqueues a host program for compilation.
        /// </summary>
        /// <param name="cachedProgram">Cached program</param>
        /// <param name="binaryCode">Host binary code</param>
        /// <param name="programIndex">Program index</param>
        /// <param name="isCompute">Indicates if the program is a compute shader</param>
        public void QueueHostProgram(CachedShaderProgram cachedProgram, byte[] binaryCode, int programIndex, bool isCompute)
        {
            EnqueueForValidation(new ProgramEntry(cachedProgram, binaryCode, programIndex, isCompute, isBinary: true));
        }

        /// <summary>
        /// Enqueues a guest program for compilation.
        /// </summary>
        /// <param name="guestShaders">Guest code for each active stage</param>
        /// <param name="specState">Specialization state</param>
        /// <param name="programIndex">Program index</param>
        /// <param name="isCompute">Indicates if the program is a compute shader</param>
        public void QueueGuestProgram(GuestCodeAndCbData?[] guestShaders, ShaderSpecializationState specState, int programIndex, bool isCompute)
        {
            try
            {
                AsyncProgramTranslation asyncTranslation = new(guestShaders, specState, programIndex, isCompute);
                _asyncTranslationQueue.Add(asyncTranslation, _cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Check the state of programs that have already been compiled,
        /// and add to the cache if the compilation was successful.
        /// </summary>
        public void CheckCompilation()
        {
            ProcessCompilationQueue();

            // Process programs that already finished compiling.
            // If not yet compiled, do nothing. This avoids blocking to wait for shader compilation.
            while (_validationQueue.TryPeek(out ProgramEntry entry))
            {
                ProgramLinkStatus result = entry.CachedProgram.HostProgram.CheckProgramLink(false);

                if (result != ProgramLinkStatus.Incomplete)
                {
                    ProcessCompiledProgram(ref entry, result);
                    _validationQueue.Dequeue();
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Waits until all programs finishes compiling, then adds the ones
        /// with successful compilation to the cache.
        /// </summary>
        private void CheckCompilationBlocking()
        {
            ProcessCompilationQueue();

            while (_validationQueue.TryDequeue(out ProgramEntry entry) && Active)
            {
                ProcessCompiledProgram(ref entry, entry.CachedProgram.HostProgram.CheckProgramLink(true), asyncCompile: false);
            }
        }

        /// <summary>
        /// Process a compiled program result.
        /// </summary>
        /// <param name="entry">Compiled program entry</param>
        /// <param name="result">Compilation result</param>
        /// <param name="asyncCompile">For failed host compilations, indicates if a guest compilation should be done asynchronously</param>
        private void ProcessCompiledProgram(ref ProgramEntry entry, ProgramLinkStatus result, bool asyncCompile = true)
        {
            if (result == ProgramLinkStatus.Success)
            {
                // Compilation successful, add to memory cache.
                if (entry.IsCompute)
                {
                    _computeCache.Add(entry.CachedProgram);
                }
                else
                {
                    _graphicsCache.Add(entry.CachedProgram);
                }

                if (!entry.IsBinary)
                {
                    _needsHostRegen = true;
                }

                // Fetch the binary code from the backend if it isn't already present.
                byte[] binaryCode = entry.BinaryCode ?? entry.CachedProgram.HostProgram.GetBinary();

                _programList.Add(entry.ProgramIndex, (entry.CachedProgram, binaryCode));
                SignalCompiled();
            }
            else if (entry.IsBinary)
            {
                // If this is a host binary and compilation failed,
                // we still have a chance to recompile from the guest binary.
                CachedShaderProgram program = entry.CachedProgram;

                GuestCodeAndCbData?[] guestShaders = new GuestCodeAndCbData?[program.Shaders.Length];

                for (int index = 0; index < program.Shaders.Length; index++)
                {
                    CachedShaderStage shader = program.Shaders[index];

                    if (shader != null)
                    {
                        guestShaders[index] = new GuestCodeAndCbData(shader.Code, shader.Cb1Data);
                    }
                }

                if (asyncCompile)
                {
                    QueueGuestProgram(guestShaders, program.SpecializationState, entry.ProgramIndex, entry.IsCompute);
                }
                else
                {
                    RecompileFromGuestCode(guestShaders, program.SpecializationState, entry.ProgramIndex, entry.IsCompute);
                    ProcessCompilationQueue();
                }
            }
            else
            {
                // Failed to compile from both host and guest binary.
                ErrorCount++;
                SignalCompiled();
            }
        }

        /// <summary>
        /// Processes the queue of translated guest programs that should be compiled on the host.
        /// </summary>
        private void ProcessCompilationQueue()
        {
            while (_compilationQueue.TryDequeue(out ProgramCompilation compilation) && Active)
            {
                ShaderSource[] shaderSources = new ShaderSource[compilation.TranslatedStages.Length];

                ref GpuChannelComputeState computeState = ref compilation.SpecializationState.ComputeState;

                ShaderInfoBuilder shaderInfoBuilder = new(
                    _context,
                    compilation.SpecializationState.TransformFeedbackDescriptors != null,
                    computeLocalSize: computeState.GetLocalSize());

                for (int index = 0; index < compilation.TranslatedStages.Length; index++)
                {
                    ShaderProgram shader = compilation.TranslatedStages[index];
                    shaderSources[index] = CreateShaderSource(shader);
                    shaderInfoBuilder.AddStageInfo(shader.Info);
                }

                ShaderInfo shaderInfo = shaderInfoBuilder.Build(compilation.SpecializationState.PipelineState, fromCache: true);
                IProgram hostProgram = _context.Renderer.CreateProgram(shaderSources, shaderInfo);
                CachedShaderProgram program = new(hostProgram, compilation.SpecializationState, compilation.Shaders);

                // Vulkan's binary code is the SPIR-V used for compilation, so it is ready immediately. Other APIs get this after compilation.
                byte[] binaryCode = _context.Capabilities.Api == TargetApi.Vulkan ? ShaderBinarySerializer.Pack(shaderSources) : null;

                EnqueueForValidation(new ProgramEntry(program, binaryCode, compilation.ProgramIndex, compilation.IsCompute, isBinary: false));
            }
        }

        /// <summary>
        /// Enqueues a program for validation, which will check if the program was compiled successfully.
        /// </summary>
        /// <param name="newEntry">Program entry to be validated</param>
        private void EnqueueForValidation(ProgramEntry newEntry)
        {
            _validationQueue.Enqueue(newEntry);

            // Do not allow more than N shader compilation in-flight, where N is the maximum number of threads
            // the driver will be using for parallel compilation.
            // Submitting more seems to cause NVIDIA OpenGL driver to crash.
            if (_validationQueue.Count >= _backendParallelCompileThreads && _validationQueue.TryDequeue(out ProgramEntry entry))
            {
                ProcessCompiledProgram(ref entry, entry.CachedProgram.HostProgram.CheckProgramLink(true), asyncCompile: false);
            }
        }

        /// <summary>
        /// Processses the queue of programs that should be translated from guest code.
        /// </summary>
        /// <param name="state">Cancellation token</param>
        private void ProcessAsyncQueue(object state)
        {
            CancellationToken ct = (CancellationToken)state;

            try
            {
                foreach (AsyncProgramTranslation asyncCompilation in _asyncTranslationQueue.GetConsumingEnumerable(ct))
                {
                    RecompileFromGuestCode(
                        asyncCompilation.GuestShaders,
                        asyncCompilation.SpecializationState,
                        asyncCompilation.ProgramIndex,
                        asyncCompilation.IsCompute);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Recompiles a program from guest code.
        /// </summary>
        /// <param name="guestShaders">Guest code for each active stage</param>
        /// <param name="specState">Specialization state</param>
        /// <param name="programIndex">Program index</param>
        /// <param name="isCompute">Indicates if the program is a compute shader</param>
        private void RecompileFromGuestCode(GuestCodeAndCbData?[] guestShaders, ShaderSpecializationState specState, int programIndex, bool isCompute)
        {
            try
            {
                if (isCompute)
                {
                    RecompileComputeFromGuestCode(guestShaders, specState, programIndex);
                }
                else
                {
                    RecompileGraphicsFromGuestCode(guestShaders, specState, programIndex);
                }
            }
            catch (Exception exception)
            {
                Logger.Error?.Print(LogClass.Gpu, $"Error translating guest shader. {exception.Message}");

                ErrorCount++;
                SignalCompiled();
            }
        }

        /// <summary>
        /// Recompiles a graphics program from guest code.
        /// </summary>
        /// <param name="guestShaders">Guest code for each active stage</param>
        /// <param name="specState">Specialization state</param>
        /// <param name="programIndex">Program index</param>
        private void RecompileGraphicsFromGuestCode(GuestCodeAndCbData?[] guestShaders, ShaderSpecializationState specState, int programIndex)
        {
            ShaderSpecializationState newSpecState = new(
                ref specState.GraphicsState,
                specState.PipelineState,
                specState.TransformFeedbackDescriptors);

            ResourceCounts counts = new();

            DiskCacheGpuAccessor[] gpuAccessors = new DiskCacheGpuAccessor[Constants.ShaderStages];
            TranslatorContext[] translatorContexts = new TranslatorContext[Constants.ShaderStages + 1];
            TranslatorContext nextStage = null;

            TargetApi api = _context.Capabilities.Api;

            bool hasCachedGs = guestShaders[4].HasValue;

            for (int stageIndex = Constants.ShaderStages - 1; stageIndex >= 0; stageIndex--)
            {
                if (guestShaders[stageIndex + 1].HasValue)
                {
                    GuestCodeAndCbData shader = guestShaders[stageIndex + 1].Value;

                    byte[] guestCode = shader.Code;
                    byte[] cb1Data = shader.Cb1Data;

                    DiskCacheGpuAccessor gpuAccessor = new(_context, guestCode, cb1Data, specState, newSpecState, counts, stageIndex, hasCachedGs);
                    TranslatorContext currentStage = DecodeGraphicsShader(gpuAccessor, api, DefaultFlags, 0);

                    if (nextStage != null)
                    {
                        currentStage.SetNextStage(nextStage);
                    }

                    if (stageIndex == 0 && guestShaders[0].HasValue)
                    {
                        byte[] guestCodeA = guestShaders[0].Value.Code;
                        byte[] cb1DataA = guestShaders[0].Value.Cb1Data;

                        DiskCacheGpuAccessor gpuAccessorA = new(_context, guestCodeA, cb1DataA, specState, newSpecState, counts, 0, hasCachedGs);
                        translatorContexts[0] = DecodeGraphicsShader(gpuAccessorA, api, DefaultFlags | TranslationFlags.VertexA, 0);
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

            // We don't support caching shader stages that have been converted to compute currently,
            // so just eliminate them if they exist in the cache.
            if (vertexToCompute)
            {
                return;
            }

            CachedShaderStage[] shaders = new CachedShaderStage[guestShaders.Length];
            List<ShaderProgram> translatedStages = new();

            TranslatorContext previousStage = null;

            for (int stageIndex = 0; stageIndex < Constants.ShaderStages; stageIndex++)
            {
                TranslatorContext currentStage = translatorContexts[stageIndex + 1];

                if (currentStage != null)
                {
                    gpuAccessors[stageIndex].InitializeReservedCounts(specState.TransformFeedbackDescriptors != null, vertexToCompute);

                    ShaderProgram program;

                    byte[] guestCode = guestShaders[stageIndex + 1].Value.Code;
                    byte[] cb1Data = guestShaders[stageIndex + 1].Value.Cb1Data;

                    if (stageIndex == 0 && guestShaders[0].HasValue)
                    {
                        program = currentStage.Translate(translatorContexts[0]);

                        byte[] guestCodeA = guestShaders[0].Value.Code;
                        byte[] cb1DataA = guestShaders[0].Value.Cb1Data;

                        shaders[0] = new CachedShaderStage(null, guestCodeA, cb1DataA);
                        shaders[1] = new CachedShaderStage(program.Info, guestCode, cb1Data);
                    }
                    else
                    {
                        program = currentStage.Translate();

                        shaders[stageIndex + 1] = new CachedShaderStage(program.Info, guestCode, cb1Data);
                    }

                    if (program != null)
                    {
                        translatedStages.Add(program);
                    }

                    previousStage = currentStage;
                }
                else if (
                    previousStage != null &&
                    previousStage.LayerOutputWritten &&
                    stageIndex == 3 &&
                    !_context.Capabilities.SupportsLayerVertexTessellation)
                {
                    translatedStages.Add(previousStage.GenerateGeometryPassthrough());
                }
            }

            _compilationQueue.Enqueue(new ProgramCompilation(translatedStages.ToArray(), shaders, newSpecState, programIndex, isCompute: false));
        }

        /// <summary>
        /// Recompiles a compute program from guest code.
        /// </summary>
        /// <param name="guestShaders">Guest code for each active stage</param>
        /// <param name="specState">Specialization state</param>
        /// <param name="programIndex">Program index</param>
        private void RecompileComputeFromGuestCode(GuestCodeAndCbData?[] guestShaders, ShaderSpecializationState specState, int programIndex)
        {
            GuestCodeAndCbData shader = guestShaders[0].Value;
            ResourceCounts counts = new();
            ShaderSpecializationState newSpecState = new(ref specState.ComputeState);
            DiskCacheGpuAccessor gpuAccessor = new(_context, shader.Code, shader.Cb1Data, specState, newSpecState, counts, 0, false);
            gpuAccessor.InitializeReservedCounts(tfEnabled: false, vertexAsCompute: false);

            TranslatorContext translatorContext = DecodeComputeShader(gpuAccessor, _context.Capabilities.Api, 0);

            ShaderProgram program = translatorContext.Translate();

            CachedShaderStage[] shaders = new[] { new CachedShaderStage(program.Info, shader.Code, shader.Cb1Data) };

            _compilationQueue.Enqueue(new ProgramCompilation(new[] { program }, shaders, newSpecState, programIndex, isCompute: true));
        }

        /// <summary>
        /// Signals that compilation of a program has been finished successfully,
        /// or that it failed and guest recompilation has also been attempted.
        /// </summary>
        private void SignalCompiled()
        {
            _stateChangeCallback(ShaderCacheState.Loading, ++_compiledCount, _totalCount);
        }
    }
}
