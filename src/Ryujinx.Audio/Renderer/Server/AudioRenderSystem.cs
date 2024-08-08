using Ryujinx.Audio.Integration;
using Ryujinx.Audio.Renderer.Common;
using Ryujinx.Audio.Renderer.Dsp.Command;
using Ryujinx.Audio.Renderer.Dsp.State;
using Ryujinx.Audio.Renderer.Parameter;
using Ryujinx.Audio.Renderer.Server.Effect;
using Ryujinx.Audio.Renderer.Server.MemoryPool;
using Ryujinx.Audio.Renderer.Server.Mix;
using Ryujinx.Audio.Renderer.Server.Performance;
using Ryujinx.Audio.Renderer.Server.Sink;
using Ryujinx.Audio.Renderer.Server.Splitter;
using Ryujinx.Audio.Renderer.Server.Types;
using Ryujinx.Audio.Renderer.Server.Upsampler;
using Ryujinx.Audio.Renderer.Server.Voice;
using Ryujinx.Audio.Renderer.Utils;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using CpuAddress = System.UInt64;

namespace Ryujinx.Audio.Renderer.Server
{
    public class AudioRenderSystem : IDisposable
    {
        private readonly Lock _lock = new();

        private AudioRendererRenderingDevice _renderingDevice;
        private AudioRendererExecutionMode _executionMode;
        private readonly IWritableEvent _systemEvent;
        private MemoryPoolState _dspMemoryPoolState;
        private readonly VoiceContext _voiceContext;
        private readonly MixContext _mixContext;
        private readonly SinkContext _sinkContext;
        private readonly SplitterContext _splitterContext;
        private readonly EffectContext _effectContext;
        private PerformanceManager _performanceManager;
        private UpsamplerManager _upsamplerManager;
        private bool _isActive;
        private BehaviourContext _behaviourContext;
#pragma warning disable IDE0052 // Remove unread private member
        private ulong _totalElapsedTicksUpdating;
        private ulong _totalElapsedTicks;
#pragma warning restore IDE0052
        private int _sessionId;
        private Memory<MemoryPoolState> _memoryPools;

        private uint _sampleRate;
        private uint _sampleCount;
        private uint _mixBufferCount;
        private uint _voiceChannelCountMax;
        private uint _upsamplerCount;
        private uint _memoryPoolCount;
        private uint _processHandle;
        private ulong _appletResourceId;

        private MemoryHandle _workBufferMemoryPin;

        private Memory<float> _mixBuffer;
        private Memory<float> _depopBuffer;

        private uint _renderingTimeLimitPercent;
        private bool _voiceDropEnabled;
        private uint _voiceDropCount;
        private float _voiceDropParameter;
        private bool _isDspRunningBehind;

        private ICommandProcessingTimeEstimator _commandProcessingTimeEstimator;

        private Memory<byte> _performanceBuffer;

        public IVirtualMemoryManager MemoryManager { get; private set; }

        private ulong _elapsedFrameCount;
        private ulong _renderingStartTick;

        private readonly AudioRendererManager _manager;

        private int _disposeState;

        public AudioRenderSystem(AudioRendererManager manager, IWritableEvent systemEvent)
        {
            _manager = manager;
            _dspMemoryPoolState = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            _voiceContext = new VoiceContext();
            _mixContext = new MixContext();
            _sinkContext = new SinkContext();
            _splitterContext = new SplitterContext();
            _effectContext = new EffectContext();

            _commandProcessingTimeEstimator = null;
            _systemEvent = systemEvent;
            _behaviourContext = new BehaviourContext();

            _totalElapsedTicksUpdating = 0;
            _sessionId = 0;
            _voiceDropParameter = 1.0f;
        }

        public ResultCode Initialize(
            ref AudioRendererConfiguration parameter,
            uint processHandle,
            Memory<byte> workBufferMemory,
            CpuAddress workBuffer,
            ulong workBufferSize,
            int sessionId,
            ulong appletResourceId,
            IVirtualMemoryManager memoryManager)
        {
            if (!BehaviourContext.CheckValidRevision(parameter.Revision))
            {
                return ResultCode.OperationFailed;
            }

            if (GetWorkBufferSize(ref parameter) > workBufferSize)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            Debug.Assert(parameter.RenderingDevice == AudioRendererRenderingDevice.Dsp && parameter.ExecutionMode == AudioRendererExecutionMode.Auto);

            Logger.Info?.Print(LogClass.AudioRenderer, $"Initializing with REV{BehaviourContext.GetRevisionNumber(parameter.Revision)}");

            _behaviourContext.SetUserRevision(parameter.Revision);

            _sampleRate = parameter.SampleRate;
            _sampleCount = parameter.SampleCount;
            _mixBufferCount = parameter.MixBufferCount;
            _voiceChannelCountMax = Constants.VoiceChannelCountMax;
            _upsamplerCount = parameter.SinkCount + parameter.SubMixBufferCount;
            _appletResourceId = appletResourceId;
            _memoryPoolCount = parameter.EffectCount + parameter.VoiceCount * Constants.VoiceWaveBufferCount;
            _renderingDevice = parameter.RenderingDevice;
            _executionMode = parameter.ExecutionMode;
            _sessionId = sessionId;
            MemoryManager = memoryManager;

            if (memoryManager is IRefCounted rc)
            {
                rc.IncrementReferenceCount();
            }

            WorkBufferAllocator workBufferAllocator;

            workBufferMemory.Span.Clear();
            _workBufferMemoryPin = workBufferMemory.Pin();

            workBufferAllocator = new WorkBufferAllocator(workBufferMemory);

            PoolMapper poolMapper = new(processHandle, false);
            poolMapper.InitializeSystemPool(ref _dspMemoryPoolState, workBuffer, workBufferSize);

            _mixBuffer = workBufferAllocator.Allocate<float>(_sampleCount * (_voiceChannelCountMax + _mixBufferCount), 0x10);

            if (_mixBuffer.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            Memory<float> upSamplerWorkBuffer = workBufferAllocator.Allocate<float>(Constants.TargetSampleCount * (_voiceChannelCountMax + _mixBufferCount) * _upsamplerCount, 0x10);

            if (upSamplerWorkBuffer.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            _depopBuffer = workBufferAllocator.Allocate<float>(BitUtils.AlignUp<ulong>(parameter.MixBufferCount, Constants.BufferAlignment), Constants.BufferAlignment);

            if (_depopBuffer.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            Memory<BiquadFilterState> splitterBqfStates = Memory<BiquadFilterState>.Empty;

            if (_behaviourContext.IsBiquadFilterParameterForSplitterEnabled() &&
                parameter.SplitterCount > 0 &&
                parameter.SplitterDestinationCount > 0)
            {
                splitterBqfStates = workBufferAllocator.Allocate<BiquadFilterState>(parameter.SplitterDestinationCount * SplitterContext.BqfStatesPerDestination, 0x10);

                if (splitterBqfStates.IsEmpty)
                {
                    return ResultCode.WorkBufferTooSmall;
                }

                splitterBqfStates.Span.Clear();
            }

            // Invalidate DSP cache on what was currently allocated with workBuffer.
            AudioProcessorMemoryManager.InvalidateDspCache(_dspMemoryPoolState.Translate(workBuffer, workBufferAllocator.Offset), workBufferAllocator.Offset);

            Debug.Assert((workBufferAllocator.Offset % Constants.BufferAlignment) == 0);

            Memory<VoiceState> voices = workBufferAllocator.Allocate<VoiceState>(parameter.VoiceCount, VoiceState.Alignment);

            if (voices.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            foreach (ref VoiceState voice in voices.Span)
            {
                voice.Initialize();
            }

            // A pain to handle as we can't have VoiceState*, use indices to be a bit more safe
            Memory<int> sortedVoices = workBufferAllocator.Allocate<int>(parameter.VoiceCount, 0x10);

            if (sortedVoices.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            // Clear memory (use -1 as it's an invalid index)
            sortedVoices.Span.Fill(-1);

            Memory<VoiceChannelResource> voiceChannelResources = workBufferAllocator.Allocate<VoiceChannelResource>(parameter.VoiceCount, VoiceChannelResource.Alignment);

            if (voiceChannelResources.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            for (uint id = 0; id < voiceChannelResources.Length; id++)
            {
                ref VoiceChannelResource voiceChannelResource = ref voiceChannelResources.Span[(int)id];

                voiceChannelResource.Id = id;
                voiceChannelResource.IsUsed = false;
            }

            Memory<VoiceUpdateState> voiceUpdateStates = workBufferAllocator.Allocate<VoiceUpdateState>(parameter.VoiceCount, VoiceUpdateState.Align);

            if (voiceUpdateStates.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            uint mixesCount = parameter.SubMixBufferCount + 1;

            Memory<MixState> mixes = workBufferAllocator.Allocate<MixState>(mixesCount, MixState.Alignment);

            if (mixes.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            if (parameter.EffectCount == 0)
            {
                foreach (ref MixState mix in mixes.Span)
                {
                    mix = new MixState(Memory<int>.Empty, ref _behaviourContext);
                }
            }
            else
            {
                Memory<int> effectProcessingOrderArray = workBufferAllocator.Allocate<int>(parameter.EffectCount * mixesCount, 0x10);

                foreach (ref MixState mix in mixes.Span)
                {
                    mix = new MixState(effectProcessingOrderArray[..(int)parameter.EffectCount], ref _behaviourContext);

                    effectProcessingOrderArray = effectProcessingOrderArray[(int)parameter.EffectCount..];
                }
            }

            // Initialize the final mix id
            mixes.Span[0].MixId = Constants.FinalMixId;

            Memory<int> sortedMixesState = workBufferAllocator.Allocate<int>(mixesCount, 0x10);

            if (sortedMixesState.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            // Clear memory (use -1 as it's an invalid index)
            sortedMixesState.Span.Fill(-1);

            Memory<byte> nodeStatesWorkBuffer = Memory<byte>.Empty;
            Memory<byte> edgeMatrixWorkBuffer = Memory<byte>.Empty;

            if (_behaviourContext.IsSplitterSupported())
            {
                nodeStatesWorkBuffer = workBufferAllocator.Allocate((uint)NodeStates.GetWorkBufferSize((int)mixesCount), 1);
                edgeMatrixWorkBuffer = workBufferAllocator.Allocate((uint)EdgeMatrix.GetWorkBufferSize((int)mixesCount), 1);

                if (nodeStatesWorkBuffer.IsEmpty || edgeMatrixWorkBuffer.IsEmpty)
                {
                    return ResultCode.WorkBufferTooSmall;
                }
            }

            _mixContext.Initialize(sortedMixesState, mixes, nodeStatesWorkBuffer, edgeMatrixWorkBuffer);

            _memoryPools = workBufferAllocator.Allocate<MemoryPoolState>(_memoryPoolCount, MemoryPoolState.Alignment);

            if (_memoryPools.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            foreach (ref MemoryPoolState state in _memoryPools.Span)
            {
                state = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);
            }

            if (!_splitterContext.Initialize(ref _behaviourContext, ref parameter, workBufferAllocator, splitterBqfStates))
            {
                return ResultCode.WorkBufferTooSmall;
            }

            _processHandle = processHandle;

            _upsamplerManager = new UpsamplerManager(upSamplerWorkBuffer, _upsamplerCount);

            _effectContext.Initialize(parameter.EffectCount, _behaviourContext.IsEffectInfoVersion2Supported() ? parameter.EffectCount : 0);
            _sinkContext.Initialize(parameter.SinkCount);

            Memory<VoiceUpdateState> voiceUpdateStatesDsp = workBufferAllocator.Allocate<VoiceUpdateState>(parameter.VoiceCount, VoiceUpdateState.Align);

            if (voiceUpdateStatesDsp.IsEmpty)
            {
                return ResultCode.WorkBufferTooSmall;
            }

            _voiceContext.Initialize(sortedVoices, voices, voiceChannelResources, voiceUpdateStates, voiceUpdateStatesDsp, parameter.VoiceCount);

            if (parameter.PerformanceMetricFramesCount > 0)
            {
                ulong performanceBufferSize = PerformanceManager.GetRequiredBufferSizeForPerformanceMetricsPerFrame(ref parameter, ref _behaviourContext) * (parameter.PerformanceMetricFramesCount + 1) + 0xC;

                _performanceBuffer = workBufferAllocator.Allocate(performanceBufferSize, Constants.BufferAlignment);

                if (_performanceBuffer.IsEmpty)
                {
                    return ResultCode.WorkBufferTooSmall;
                }

                _performanceManager = PerformanceManager.Create(_performanceBuffer, ref parameter, _behaviourContext);
            }
            else
            {
                _performanceManager = null;
            }

            _totalElapsedTicksUpdating = 0;
            _totalElapsedTicks = 0;
            _renderingTimeLimitPercent = 100;
            _voiceDropEnabled = parameter.VoiceDropEnabled && _executionMode == AudioRendererExecutionMode.Auto;

            AudioProcessorMemoryManager.InvalidateDataCache(workBuffer, workBufferSize);

            _processHandle = processHandle;
            _elapsedFrameCount = 0;
            _voiceDropParameter = 1.0f;

            _commandProcessingTimeEstimator = _behaviourContext.GetCommandProcessingTimeEstimatorVersion() switch
            {
                1 => new CommandProcessingTimeEstimatorVersion1(_sampleCount, _mixBufferCount),
                2 => new CommandProcessingTimeEstimatorVersion2(_sampleCount, _mixBufferCount),
                3 => new CommandProcessingTimeEstimatorVersion3(_sampleCount, _mixBufferCount),
                4 => new CommandProcessingTimeEstimatorVersion4(_sampleCount, _mixBufferCount),
                5 => new CommandProcessingTimeEstimatorVersion5(_sampleCount, _mixBufferCount),
                _ => throw new NotImplementedException($"Unsupported processing time estimator version {_behaviourContext.GetCommandProcessingTimeEstimatorVersion()}."),
            };

            return ResultCode.Success;
        }

        public void Start()
        {
            Logger.Info?.Print(LogClass.AudioRenderer, $"Starting renderer id {_sessionId}");

            lock (_lock)
            {
                _elapsedFrameCount = 0;
                _isActive = true;
            }
        }

        public void Stop()
        {
            Logger.Info?.Print(LogClass.AudioRenderer, $"Stopping renderer id {_sessionId}");

            lock (_lock)
            {
                _isActive = false;
            }

            Logger.Info?.Print(LogClass.AudioRenderer, $"Stopped renderer id {_sessionId}");
        }

        public void Disable()
        {
            lock (_lock)
            {
                _isActive = false;
            }
        }

        public ResultCode Update(Memory<byte> output, Memory<byte> performanceOutput, ReadOnlySequence<byte> input)
        {
            lock (_lock)
            {
                ulong updateStartTicks = GetSystemTicks();

                output.Span.Clear();

                StateUpdater stateUpdater = new(input, output, _processHandle, _behaviourContext);

                ResultCode result;

                result = stateUpdater.UpdateBehaviourContext();

                if (result != ResultCode.Success)
                {
                    return result;
                }

                result = stateUpdater.UpdateMemoryPools(_memoryPools.Span);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                result = stateUpdater.UpdateVoiceChannelResources(_voiceContext);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                PoolMapper poolMapper = new PoolMapper(_processHandle, _memoryPools, _behaviourContext.IsMemoryPoolForceMappingEnabled());

                result = stateUpdater.UpdateVoices(_voiceContext, poolMapper);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                result = stateUpdater.UpdateEffects(_effectContext, _isActive, poolMapper);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                if (_behaviourContext.IsSplitterSupported())
                {
                    result = stateUpdater.UpdateSplitter(_splitterContext);

                    if (result != ResultCode.Success)
                    {
                        return result;
                    }
                }

                result = stateUpdater.UpdateMixes(_mixContext, GetMixBufferCount(), _effectContext, _splitterContext);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                result = stateUpdater.UpdateSinks(_sinkContext, poolMapper);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                result = stateUpdater.UpdatePerformanceBuffer(_performanceManager, performanceOutput.Span);

                if (result != ResultCode.Success)
                {
                    return result;
                }

                result = stateUpdater.UpdateErrorInfo();

                if (result != ResultCode.Success)
                {
                    return result;
                }

                if (_behaviourContext.IsElapsedFrameCountSupported())
                {
                    result = stateUpdater.UpdateRendererInfo(_elapsedFrameCount);

                    if (result != ResultCode.Success)
                    {
                        return result;
                    }
                }

                result = stateUpdater.CheckConsumedSize();

                if (result != ResultCode.Success)
                {
                    return result;
                }

                _systemEvent.Clear();

                ulong updateEndTicks = GetSystemTicks();

                _totalElapsedTicksUpdating += (updateEndTicks - updateStartTicks);

                return result;
            }
        }

        private ulong GetSystemTicks()
        {
            return (ulong)(_manager.TickSource.ElapsedSeconds * Constants.TargetTimerFrequency);
        }

        private uint ComputeVoiceDrop(CommandBuffer commandBuffer, uint voicesEstimatedTime, long deltaTimeDsp)
        {
            int i;

            for (i = 0; i < commandBuffer.CommandList.Commands.Count; i++)
            {
                ICommand command = commandBuffer.CommandList.Commands[i];

                CommandType commandType = command.CommandType;

                if (commandType == CommandType.AdpcmDataSourceVersion1 ||
                    commandType == CommandType.AdpcmDataSourceVersion2 ||
                    commandType == CommandType.PcmInt16DataSourceVersion1 ||
                    commandType == CommandType.PcmInt16DataSourceVersion2 ||
                    commandType == CommandType.PcmFloatDataSourceVersion1 ||
                    commandType == CommandType.PcmFloatDataSourceVersion2 ||
                    commandType == CommandType.Performance)
                {
                    break;
                }
            }

            uint voiceDropped = 0;

            for (; i < commandBuffer.CommandList.Commands.Count; i++)
            {
                ICommand targetCommand = commandBuffer.CommandList.Commands[i];

                int targetNodeId = targetCommand.NodeId;

                if (voicesEstimatedTime <= deltaTimeDsp || NodeIdHelper.GetType(targetNodeId) != NodeIdType.Voice)
                {
                    break;
                }

                ref VoiceState voice = ref _voiceContext.GetState(NodeIdHelper.GetBase(targetNodeId));

                if (voice.Priority == Constants.VoiceHighestPriority)
                {
                    break;
                }

                // We can safely drop this voice, disable all associated commands while activating depop preparation commands.
                voiceDropped++;
                voice.VoiceDropFlag = true;

                Logger.Warning?.Print(LogClass.AudioRenderer, $"Dropping voice {voice.NodeId}");

                for (; i < commandBuffer.CommandList.Commands.Count; i++)
                {
                    ICommand command = commandBuffer.CommandList.Commands[i];

                    if (command.NodeId != targetNodeId)
                    {
                        break;
                    }

                    if (command.CommandType == CommandType.DepopPrepare)
                    {
                        command.Enabled = true;
                    }
                    else if (command.CommandType == CommandType.Performance || !command.Enabled)
                    {
                        continue;
                    }
                    else
                    {
                        command.Enabled = false;

                        voicesEstimatedTime -= (uint)(_voiceDropParameter * command.EstimatedProcessingTime);
                    }
                }
            }

            return voiceDropped;
        }

        private void GenerateCommandList(out CommandList commandList)
        {
            Debug.Assert(_executionMode == AudioRendererExecutionMode.Auto);

            PoolMapper.ClearUsageState(_memoryPools);

            ulong startTicks = GetSystemTicks();

            commandList = new CommandList(this);

            if (_performanceManager != null)
            {
                _performanceManager.TapFrame(_isDspRunningBehind, _voiceDropCount, _renderingStartTick);

                _isDspRunningBehind = false;
                _voiceDropCount = 0;
                _renderingStartTick = 0;
            }

            CommandBuffer commandBuffer = new(commandList, _commandProcessingTimeEstimator);

            CommandGenerator commandGenerator = new(commandBuffer, GetContext(), _voiceContext, _mixContext, _effectContext, _sinkContext, _splitterContext, _performanceManager);

            _voiceContext.Sort();
            commandGenerator.GenerateVoices();

            uint voicesEstimatedTime = (uint)(_voiceDropParameter * commandBuffer.EstimatedProcessingTime);

            commandGenerator.GenerateSubMixes();
            commandGenerator.GenerateFinalMixes();
            commandGenerator.GenerateSinks();

            uint totalEstimatedTime = (uint)(_voiceDropParameter * commandBuffer.EstimatedProcessingTime);

            if (_voiceDropEnabled)
            {
                long maxDspTime = GetMaxAllocatedTimeForDsp();

                long restEstimateTime = totalEstimatedTime - voicesEstimatedTime;

                long deltaTimeDsp = Math.Max(maxDspTime - restEstimateTime, 0);

                _voiceDropCount = ComputeVoiceDrop(commandBuffer, voicesEstimatedTime, deltaTimeDsp);
            }

            _voiceContext.UpdateForCommandGeneration();

            if (_behaviourContext.IsEffectInfoVersion2Supported())
            {
                _effectContext.UpdateResultStateForCommandGeneration();
            }

            ulong endTicks = GetSystemTicks();

            _totalElapsedTicks = endTicks - startTicks;

            _renderingStartTick = GetSystemTicks();
            _elapsedFrameCount++;
        }

        private int GetMaxAllocatedTimeForDsp()
        {
            return (int)(Constants.AudioProcessorMaxUpdateTimePerSessions * _behaviourContext.GetAudioRendererProcessingTimeLimit() * (GetRenderingTimeLimit() / 100.0f));
        }

        public void SendCommands()
        {
            lock (_lock)
            {
                if (_isActive)
                {
                    if (!_manager.Processor.HasRemainingCommands(_sessionId))
                    {
                        GenerateCommandList(out CommandList commands);

                        _manager.Processor.Send(_sessionId,
                                                commands,
                                                GetMaxAllocatedTimeForDsp(),
                                                _appletResourceId);

                        _systemEvent.Signal();
                    }
                    else
                    {
                        _isDspRunningBehind = true;
                    }
                }
            }
        }

        public uint GetMixBufferCount()
        {
            return _mixBufferCount;
        }

        public void SetRenderingTimeLimitPercent(uint percent)
        {
            Debug.Assert(percent <= 100);

            _renderingTimeLimitPercent = percent;
        }

        public uint GetRenderingTimeLimit()
        {
            return _renderingTimeLimitPercent;
        }

        public Memory<float> GetMixBuffer()
        {
            return _mixBuffer;
        }

        public uint GetSampleCount()
        {
            return _sampleCount;
        }

        public uint GetSampleRate()
        {
            return _sampleRate;
        }

        public uint GetVoiceChannelCountMax()
        {
            return _voiceChannelCountMax;
        }

        public bool IsActive()
        {
            return _isActive;
        }

        private RendererSystemContext GetContext()
        {
            return new RendererSystemContext
            {
                ChannelCount = _manager.Processor.OutputDevices[_sessionId].GetChannelCount(),
                BehaviourContext = _behaviourContext,
                DepopBuffer = _depopBuffer,
                MixBufferCount = GetMixBufferCount(),
                SessionId = _sessionId,
                UpsamplerManager = _upsamplerManager,
            };
        }

        public int GetSessionId()
        {
            return _sessionId;
        }

        public static ulong GetWorkBufferSize(ref AudioRendererConfiguration parameter)
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(parameter.Revision);

            uint mixesCount = parameter.SubMixBufferCount + 1;

            uint memoryPoolCount = parameter.EffectCount + parameter.VoiceCount * Constants.VoiceWaveBufferCount;

            ulong size = 0;

            // Mix Buffers
            size = WorkBufferAllocator.GetTargetSize<float>(size, parameter.SampleCount * (Constants.VoiceChannelCountMax + parameter.MixBufferCount), 0x10);

            // Upsampler workbuffer
            size = WorkBufferAllocator.GetTargetSize<float>(size, Constants.TargetSampleCount * (Constants.VoiceChannelCountMax + parameter.MixBufferCount) * (parameter.SinkCount + parameter.SubMixBufferCount), 0x10);

            // Depop buffer
            size = WorkBufferAllocator.GetTargetSize<float>(size, BitUtils.AlignUp<ulong>(parameter.MixBufferCount, Constants.BufferAlignment), Constants.BufferAlignment);

            // Voice
            size = WorkBufferAllocator.GetTargetSize<VoiceState>(size, parameter.VoiceCount, VoiceState.Alignment);
            size = WorkBufferAllocator.GetTargetSize<int>(size, parameter.VoiceCount, 0x10);
            size = WorkBufferAllocator.GetTargetSize<VoiceChannelResource>(size, parameter.VoiceCount, VoiceChannelResource.Alignment);
            size = WorkBufferAllocator.GetTargetSize<VoiceUpdateState>(size, parameter.VoiceCount, VoiceUpdateState.Align);

            // Mix
            size = WorkBufferAllocator.GetTargetSize<MixState>(size, mixesCount, MixState.Alignment);
            size = WorkBufferAllocator.GetTargetSize<int>(size, parameter.EffectCount * mixesCount, 0x10);
            size = WorkBufferAllocator.GetTargetSize<int>(size, mixesCount, 0x10);

            if (behaviourContext.IsSplitterSupported())
            {
                size += (ulong)BitUtils.AlignUp(NodeStates.GetWorkBufferSize((int)mixesCount) + EdgeMatrix.GetWorkBufferSize((int)mixesCount), 0x10);
            }

            // Memory Pool
            size = WorkBufferAllocator.GetTargetSize<MemoryPoolState>(size, memoryPoolCount, MemoryPoolState.Alignment);

            // Splitter
            size = SplitterContext.GetWorkBufferSize(size, ref behaviourContext, ref parameter);

            if (behaviourContext.IsBiquadFilterParameterForSplitterEnabled() &&
                parameter.SplitterCount > 0 &&
                parameter.SplitterDestinationCount > 0)
            {
                size = WorkBufferAllocator.GetTargetSize<BiquadFilterState>(size, parameter.SplitterDestinationCount * SplitterContext.BqfStatesPerDestination, 0x10);
            }

            // DSP Voice
            size = WorkBufferAllocator.GetTargetSize<VoiceUpdateState>(size, parameter.VoiceCount, VoiceUpdateState.Align);

            // Performance
            if (parameter.PerformanceMetricFramesCount > 0)
            {
                ulong performanceMetricsPerFramesSize = PerformanceManager.GetRequiredBufferSizeForPerformanceMetricsPerFrame(ref parameter, ref behaviourContext) * (parameter.PerformanceMetricFramesCount + 1) + 0xC;

                size += BitUtils.AlignUp<ulong>(performanceMetricsPerFramesSize, Constants.PerformanceMetricsPerFramesSizeAlignment);
            }

            return BitUtils.AlignUp<ulong>(size, Constants.WorkBufferAlignment);
        }

        public ResultCode QuerySystemEvent(out IWritableEvent systemEvent)
        {
            systemEvent = default;

            if (_executionMode == AudioRendererExecutionMode.Manual)
            {
                return ResultCode.UnsupportedOperation;
            }

            systemEvent = _systemEvent;

            return ResultCode.Success;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Interlocked.CompareExchange(ref _disposeState, 1, 0) == 0)
            {
                Dispose(true);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isActive)
                {
                    Stop();
                }

                PoolMapper mapper = new(_processHandle, false);
                mapper.Unmap(ref _dspMemoryPoolState);

                PoolMapper.ClearUsageState(_memoryPools);

                for (int i = 0; i < _memoryPoolCount; i++)
                {
                    ref MemoryPoolState memoryPool = ref _memoryPools.Span[i];

                    if (memoryPool.IsMapped())
                    {
                        mapper.Unmap(ref memoryPool);
                    }
                }

                _manager.Unregister(this);
                _workBufferMemoryPin.Dispose();

                if (MemoryManager is IRefCounted rc)
                {
                    rc.DecrementReferenceCount();

                    MemoryManager = null;
                }
            }
        }

        public void SetVoiceDropParameter(float voiceDropParameter)
        {
            _voiceDropParameter = Math.Clamp(voiceDropParameter, 0.0f, 2.0f);
        }

        public float GetVoiceDropParameter()
        {
            return _voiceDropParameter;
        }

        public ResultCode ExecuteAudioRendererRendering()
        {
            if (_executionMode == AudioRendererExecutionMode.Manual && _renderingDevice == AudioRendererRenderingDevice.Cpu)
            {
                // NOTE: Here Nintendo aborts with this error code, we don't want that.
                return ResultCode.InvalidExecutionContextOperation;
            }

            return ResultCode.UnsupportedOperation;
        }
    }
}
