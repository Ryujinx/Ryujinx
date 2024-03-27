using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Graphics.Vulkan
{
    class ShaderCollection : IProgram
    {
        private readonly PipelineShaderStageCreateInfo[] _infos;
        private readonly Shader[] _shaders;

        private readonly PipelineLayoutCacheEntry _plce;

        public PipelineLayout PipelineLayout => _plce.PipelineLayout;

        public bool HasMinimalLayout { get; }
        public bool UsePushDescriptors { get; }
        public bool IsCompute { get; }
        public bool HasTessellationControlShader => (Stages & (1u << 3)) != 0;

        public uint Stages { get; }

        public ResourceBindingSegment[][] ClearSegments { get; }
        public ResourceBindingSegment[][] BindingSegments { get; }
        public DescriptorSetTemplate[] Templates { get; }

        public ProgramLinkStatus LinkStatus { get; private set; }

        public readonly SpecDescription[] SpecDescriptions;

        public bool IsLinked
        {
            get
            {
                if (LinkStatus == ProgramLinkStatus.Incomplete)
                {
                    CheckProgramLink(true);
                }

                return LinkStatus == ProgramLinkStatus.Success;
            }
        }

        private HashTableSlim<PipelineUid, Auto<DisposablePipeline>> _graphicsPipelineCache;
        private HashTableSlim<SpecData, Auto<DisposablePipeline>> _computePipelineCache;

        private readonly VulkanRenderer _gd;
        private Device _device;
        private bool _initialized;

        private ProgramPipelineState _state;
        private DisposableRenderPass _dummyRenderPass;
        private readonly Task _compileTask;
        private bool _firstBackgroundUse;

        public ShaderCollection(
            VulkanRenderer gd,
            Device device,
            ShaderSource[] shaders,
            ResourceLayout resourceLayout,
            SpecDescription[] specDescription = null,
            bool isMinimal = false)
        {
            _gd = gd;
            _device = device;

            if (specDescription != null && specDescription.Length != shaders.Length)
            {
                throw new ArgumentException($"{nameof(specDescription)} array length must match {nameof(shaders)} array if provided");
            }

            gd.Shaders.Add(this);

            var internalShaders = new Shader[shaders.Length];

            _infos = new PipelineShaderStageCreateInfo[shaders.Length];

            SpecDescriptions = specDescription;

            LinkStatus = ProgramLinkStatus.Incomplete;

            uint stages = 0;

            for (int i = 0; i < shaders.Length; i++)
            {
                var shader = new Shader(gd.Api, device, shaders[i]);

                stages |= 1u << shader.StageFlags switch
                {
                    ShaderStageFlags.FragmentBit => 1,
                    ShaderStageFlags.GeometryBit => 2,
                    ShaderStageFlags.TessellationControlBit => 3,
                    ShaderStageFlags.TessellationEvaluationBit => 4,
                    _ => 0,
                };

                if (shader.StageFlags == ShaderStageFlags.ComputeBit)
                {
                    IsCompute = true;
                }

                internalShaders[i] = shader;
            }

            _shaders = internalShaders;

            bool usePushDescriptors = !isMinimal &&
                VulkanConfiguration.UsePushDescriptors &&
                _gd.Capabilities.SupportsPushDescriptors &&
                !IsCompute &&
                !HasPushDescriptorsBug(gd) &&
                CanUsePushDescriptors(gd, resourceLayout, IsCompute);

            ReadOnlyCollection<ResourceDescriptorCollection> sets = usePushDescriptors ?
                BuildPushDescriptorSets(gd, resourceLayout.Sets) : resourceLayout.Sets;

            _plce = gd.PipelineLayoutCache.GetOrCreate(gd, device, sets, usePushDescriptors);

            HasMinimalLayout = isMinimal;
            UsePushDescriptors = usePushDescriptors;

            Stages = stages;

            ClearSegments = BuildClearSegments(sets);
            BindingSegments = BuildBindingSegments(resourceLayout.SetUsages);
            Templates = BuildTemplates(usePushDescriptors);

            _compileTask = Task.CompletedTask;
            _firstBackgroundUse = false;
        }

        public ShaderCollection(
            VulkanRenderer gd,
            Device device,
            ShaderSource[] sources,
            ResourceLayout resourceLayout,
            ProgramPipelineState state,
            bool fromCache) : this(gd, device, sources, resourceLayout)
        {
            _state = state;

            _compileTask = BackgroundCompilation();
            _firstBackgroundUse = !fromCache;
        }

        private static bool HasPushDescriptorsBug(VulkanRenderer gd)
        {
            // Those GPUs/drivers do not work properly with push descriptors, so we must force disable them.
            return gd.IsNvidiaPreTuring || (gd.IsIntelArc && gd.IsIntelWindows);
        }

        private static bool CanUsePushDescriptors(VulkanRenderer gd, ResourceLayout layout, bool isCompute)
        {
            // If binding 3 is immediately used, use an alternate set of reserved bindings.
            ReadOnlyCollection<ResourceUsage> uniformUsage = layout.SetUsages[0].Usages;
            bool hasBinding3 = uniformUsage.Any(x => x.Binding == 3);
            int[] reserved = isCompute ? Array.Empty<int>() : gd.GetPushDescriptorReservedBindings(hasBinding3);

            // Can't use any of the reserved usages.
            for (int i = 0; i < uniformUsage.Count; i++)
            {
                var binding = uniformUsage[i].Binding;

                if (reserved.Contains(binding) ||
                    binding >= Constants.MaxPushDescriptorBinding ||
                    binding >= gd.Capabilities.MaxPushDescriptors + reserved.Count(id => id < binding))
                {
                    return false;
                }
            }

            return true;
        }

        private static ReadOnlyCollection<ResourceDescriptorCollection> BuildPushDescriptorSets(
            VulkanRenderer gd,
            ReadOnlyCollection<ResourceDescriptorCollection> sets)
        {
            // The reserved bindings were selected when determining if push descriptors could be used.
            int[] reserved = gd.GetPushDescriptorReservedBindings(false);

            var result = new ResourceDescriptorCollection[sets.Count];

            for (int i = 0; i < sets.Count; i++)
            {
                if (i == 0)
                {
                    // Push descriptors apply here. Remove reserved bindings.
                    ResourceDescriptorCollection original = sets[i];

                    var pdUniforms = new ResourceDescriptor[original.Descriptors.Count];
                    int j = 0;

                    foreach (ResourceDescriptor descriptor in original.Descriptors)
                    {
                        if (reserved.Contains(descriptor.Binding))
                        {
                            // If the binding is reserved, set its descriptor count to 0.
                            pdUniforms[j++] = new ResourceDescriptor(
                                descriptor.Binding,
                                0,
                                descriptor.Type,
                                descriptor.Stages);
                        }
                        else
                        {
                            pdUniforms[j++] = descriptor;
                        }
                    }

                    result[i] = new ResourceDescriptorCollection(new(pdUniforms));
                }
                else
                {
                    result[i] = sets[i];
                }
            }

            return new(result);
        }

        private static ResourceBindingSegment[][] BuildClearSegments(ReadOnlyCollection<ResourceDescriptorCollection> sets)
        {
            ResourceBindingSegment[][] segments = new ResourceBindingSegment[sets.Count][];

            for (int setIndex = 0; setIndex < sets.Count; setIndex++)
            {
                List<ResourceBindingSegment> currentSegments = new();

                ResourceDescriptor currentDescriptor = default;
                int currentCount = 0;

                for (int index = 0; index < sets[setIndex].Descriptors.Count; index++)
                {
                    ResourceDescriptor descriptor = sets[setIndex].Descriptors[index];

                    if (currentDescriptor.Binding + currentCount != descriptor.Binding ||
                        currentDescriptor.Type != descriptor.Type ||
                        currentDescriptor.Stages != descriptor.Stages)
                    {
                        if (currentCount != 0)
                        {
                            currentSegments.Add(new ResourceBindingSegment(
                                currentDescriptor.Binding,
                                currentCount,
                                currentDescriptor.Type,
                                currentDescriptor.Stages));
                        }

                        currentDescriptor = descriptor;
                        currentCount = descriptor.Count;
                    }
                    else
                    {
                        currentCount += descriptor.Count;
                    }
                }

                if (currentCount != 0)
                {
                    currentSegments.Add(new ResourceBindingSegment(
                        currentDescriptor.Binding,
                        currentCount,
                        currentDescriptor.Type,
                        currentDescriptor.Stages));
                }

                segments[setIndex] = currentSegments.ToArray();
            }

            return segments;
        }

        private static ResourceBindingSegment[][] BuildBindingSegments(ReadOnlyCollection<ResourceUsageCollection> setUsages)
        {
            ResourceBindingSegment[][] segments = new ResourceBindingSegment[setUsages.Count][];

            for (int setIndex = 0; setIndex < setUsages.Count; setIndex++)
            {
                List<ResourceBindingSegment> currentSegments = new();

                ResourceUsage currentUsage = default;
                int currentCount = 0;

                for (int index = 0; index < setUsages[setIndex].Usages.Count; index++)
                {
                    ResourceUsage usage = setUsages[setIndex].Usages[index];

                    if (currentUsage.Binding + currentCount != usage.Binding ||
                        currentUsage.Type != usage.Type ||
                        currentUsage.Stages != usage.Stages)
                    {
                        if (currentCount != 0)
                        {
                            currentSegments.Add(new ResourceBindingSegment(
                                currentUsage.Binding,
                                currentCount,
                                currentUsage.Type,
                                currentUsage.Stages));
                        }

                        currentUsage = usage;
                        currentCount = 1;
                    }
                    else
                    {
                        currentCount++;
                    }
                }

                if (currentCount != 0)
                {
                    currentSegments.Add(new ResourceBindingSegment(
                        currentUsage.Binding,
                        currentCount,
                        currentUsage.Type,
                        currentUsage.Stages));
                }

                segments[setIndex] = currentSegments.ToArray();
            }

            return segments;
        }

        private DescriptorSetTemplate[] BuildTemplates(bool usePushDescriptors)
        {
            var templates = new DescriptorSetTemplate[BindingSegments.Length];

            for (int setIndex = 0; setIndex < BindingSegments.Length; setIndex++)
            {
                if (usePushDescriptors && setIndex == 0)
                {
                    // Push descriptors get updated using templates owned by the pipeline layout.
                    continue;
                }

                ResourceBindingSegment[] segments = BindingSegments[setIndex];

                if (segments != null && segments.Length > 0)
                {
                    templates[setIndex] = new DescriptorSetTemplate(_gd, _device, segments, _plce, IsCompute ? PipelineBindPoint.Compute : PipelineBindPoint.Graphics, setIndex);
                }
            }

            return templates;
        }

        private async Task BackgroundCompilation()
        {
            await Task.WhenAll(_shaders.Select(shader => shader.CompileTask));

            if (Array.Exists(_shaders, shader => shader.CompileStatus == ProgramLinkStatus.Failure))
            {
                LinkStatus = ProgramLinkStatus.Failure;

                return;
            }

            try
            {
                if (IsCompute)
                {
                    CreateBackgroundComputePipeline();
                }
                else
                {
                    CreateBackgroundGraphicsPipeline();
                }
            }
            catch (VulkanException e)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Background Compilation failed: {e.Message}");

                LinkStatus = ProgramLinkStatus.Failure;
            }
        }

        private void EnsureShadersReady()
        {
            if (!_initialized)
            {
                CheckProgramLink(true);

                ProgramLinkStatus resultStatus = ProgramLinkStatus.Success;

                for (int i = 0; i < _shaders.Length; i++)
                {
                    var shader = _shaders[i];

                    if (shader.CompileStatus != ProgramLinkStatus.Success)
                    {
                        resultStatus = ProgramLinkStatus.Failure;
                    }

                    _infos[i] = shader.GetInfo();
                }

                // If the link status was already set as failure by background compilation, prefer that decision.
                if (LinkStatus != ProgramLinkStatus.Failure)
                {
                    LinkStatus = resultStatus;
                }

                _initialized = true;
            }
        }

        public PipelineShaderStageCreateInfo[] GetInfos()
        {
            EnsureShadersReady();

            return _infos;
        }

        protected DisposableRenderPass CreateDummyRenderPass()
        {
            if (_dummyRenderPass.Value.Handle != 0)
            {
                return _dummyRenderPass;
            }

            return _dummyRenderPass = _state.ToRenderPass(_gd, _device);
        }

        public void CreateBackgroundComputePipeline()
        {
            PipelineState pipeline = new();
            pipeline.Initialize();

            pipeline.Stages[0] = _shaders[0].GetInfo();
            pipeline.StagesCount = 1;
            pipeline.PipelineLayout = PipelineLayout;

            pipeline.CreateComputePipeline(_gd, _device, this, (_gd.Pipeline as PipelineBase).PipelineCache);
            pipeline.Dispose();
        }

        public void CreateBackgroundGraphicsPipeline()
        {
            // To compile shaders in the background in Vulkan, we need to create valid pipelines using the shader modules.
            // The GPU provides pipeline state via the GAL that can be converted into our internal Vulkan pipeline state.
            // This should match the pipeline state at the time of the first draw. If it doesn't, then it'll likely be
            // close enough that the GPU driver will reuse the compiled shader for the different state.

            // First, we need to create a render pass object compatible with the one that will be used at runtime.
            // The active attachment formats have been provided by the abstraction layer.
            var renderPass = CreateDummyRenderPass();

            PipelineState pipeline = _state.ToVulkanPipelineState(_gd);

            // Copy the shader stage info to the pipeline.
            var stages = pipeline.Stages.AsSpan();

            for (int i = 0; i < _shaders.Length; i++)
            {
                stages[i] = _shaders[i].GetInfo();
            }

            pipeline.HasTessellationControlShader = HasTessellationControlShader;
            pipeline.StagesCount = (uint)_shaders.Length;
            pipeline.PipelineLayout = PipelineLayout;

            pipeline.CreateGraphicsPipeline(_gd, _device, this, (_gd.Pipeline as PipelineBase).PipelineCache, renderPass.Value, throwOnError: true);
            pipeline.Dispose();
        }

        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            if (LinkStatus == ProgramLinkStatus.Incomplete)
            {
                ProgramLinkStatus resultStatus = ProgramLinkStatus.Success;

                foreach (Shader shader in _shaders)
                {
                    if (shader.CompileStatus == ProgramLinkStatus.Incomplete)
                    {
                        if (blocking)
                        {
                            // Wait for this shader to finish compiling.
                            shader.WaitForCompile();

                            if (shader.CompileStatus != ProgramLinkStatus.Success)
                            {
                                resultStatus = ProgramLinkStatus.Failure;
                            }
                        }
                        else
                        {
                            return ProgramLinkStatus.Incomplete;
                        }
                    }
                }

                if (!_compileTask.IsCompleted)
                {
                    if (blocking)
                    {
                        _compileTask.Wait();

                        if (LinkStatus == ProgramLinkStatus.Failure)
                        {
                            return ProgramLinkStatus.Failure;
                        }
                    }
                    else
                    {
                        return ProgramLinkStatus.Incomplete;
                    }
                }

                return resultStatus;
            }

            return LinkStatus;
        }

        public byte[] GetBinary()
        {
            return null;
        }

        public DescriptorSetTemplate GetPushDescriptorTemplate(long updateMask)
        {
            return _plce.GetPushDescriptorTemplate(IsCompute ? PipelineBindPoint.Compute : PipelineBindPoint.Graphics, updateMask);
        }

        public void AddComputePipeline(ref SpecData key, Auto<DisposablePipeline> pipeline)
        {
            (_computePipelineCache ??= new()).Add(ref key, pipeline);
        }

        public void AddGraphicsPipeline(ref PipelineUid key, Auto<DisposablePipeline> pipeline)
        {
            (_graphicsPipelineCache ??= new()).Add(ref key, pipeline);
        }

        public bool TryGetComputePipeline(ref SpecData key, out Auto<DisposablePipeline> pipeline)
        {
            if (_computePipelineCache == null)
            {
                pipeline = default;
                return false;
            }

            if (_computePipelineCache.TryGetValue(ref key, out pipeline))
            {
                return true;
            }

            return false;
        }

        public bool TryGetGraphicsPipeline(ref PipelineUid key, out Auto<DisposablePipeline> pipeline)
        {
            if (_graphicsPipelineCache == null)
            {
                pipeline = default;
                return false;
            }

            if (!_graphicsPipelineCache.TryGetValue(ref key, out pipeline))
            {
                if (_firstBackgroundUse)
                {
                    Logger.Warning?.Print(LogClass.Gpu, "Background pipeline compile missed on draw - incorrect pipeline state?");
                    _firstBackgroundUse = false;
                }

                return false;
            }

            _firstBackgroundUse = false;

            return true;
        }

        public void UpdateDescriptorCacheCommandBufferIndex(int commandBufferIndex)
        {
            _plce.UpdateCommandBufferIndex(commandBufferIndex);
        }

        public Auto<DescriptorSetCollection> GetNewDescriptorSetCollection(int setIndex, out bool isNew)
        {
            return _plce.GetNewDescriptorSetCollection(setIndex, out isNew);
        }

        public bool HasSameLayout(ShaderCollection other)
        {
            return other != null && _plce == other._plce;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_gd.Shaders.Remove(this))
                {
                    return;
                }

                for (int i = 0; i < _shaders.Length; i++)
                {
                    _shaders[i].Dispose();
                }

                if (_graphicsPipelineCache != null)
                {
                    foreach (Auto<DisposablePipeline> pipeline in _graphicsPipelineCache.Values)
                    {
                        pipeline?.Dispose();
                    }
                }

                if (_computePipelineCache != null)
                {
                    foreach (Auto<DisposablePipeline> pipeline in _computePipelineCache.Values)
                    {
                        pipeline.Dispose();
                    }
                }

                for (int i = 0; i < Templates.Length; i++)
                {
                    Templates[i]?.Dispose();
                }

                if (_dummyRenderPass.Value.Handle != 0)
                {
                    _dummyRenderPass.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
