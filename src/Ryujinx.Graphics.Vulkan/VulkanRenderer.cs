using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Ryujinx.Graphics.Vulkan.MoltenVK;
using Ryujinx.Graphics.Vulkan.Queries;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Format = Ryujinx.Graphics.GAL.Format;
using PrimitiveTopology = Ryujinx.Graphics.GAL.PrimitiveTopology;
using SamplerCreateInfo = Ryujinx.Graphics.GAL.SamplerCreateInfo;

namespace Ryujinx.Graphics.Vulkan
{
    public sealed class VulkanRenderer : IRenderer
    {
        private VulkanInstance _instance;
        private SurfaceKHR _surface;
        private VulkanPhysicalDevice _physicalDevice;
        private Device _device;
        private WindowBase _window;

        private bool _initialized;

        internal FormatCapabilities FormatCapabilities { get; private set; }
        internal HardwareCapabilities Capabilities;

        internal Vk Api { get; private set; }
        internal KhrSurface SurfaceApi { get; private set; }
        internal KhrSwapchain SwapchainApi { get; private set; }
        internal ExtConditionalRendering ConditionalRenderingApi { get; private set; }
        internal ExtExtendedDynamicState ExtendedDynamicStateApi { get; private set; }
        internal KhrPushDescriptor PushDescriptorApi { get; private set; }
        internal ExtTransformFeedback TransformFeedbackApi { get; private set; }
        internal KhrDrawIndirectCount DrawIndirectCountApi { get; private set; }

        internal uint QueueFamilyIndex { get; private set; }
        internal Queue Queue { get; private set; }
        internal Queue BackgroundQueue { get; private set; }
        internal object BackgroundQueueLock { get; private set; }
        internal object QueueLock { get; private set; }

        internal MemoryAllocator MemoryAllocator { get; private set; }
        internal HostMemoryAllocator HostMemoryAllocator { get; private set; }
        internal CommandBufferPool CommandBufferPool { get; private set; }
        internal DescriptorSetManager DescriptorSetManager { get; private set; }
        internal PipelineLayoutCache PipelineLayoutCache { get; private set; }
        internal BackgroundResources BackgroundResources { get; private set; }
        internal Action<Action> InterruptAction { get; private set; }
        internal SyncManager SyncManager { get; private set; }

        internal BufferManager BufferManager { get; private set; }

        internal HashSet<ShaderCollection> Shaders { get; }
        internal HashSet<ITexture> Textures { get; }
        internal HashSet<SamplerHolder> Samplers { get; }

        private VulkanDebugMessenger _debugMessenger;
        private Counters _counters;

        private PipelineFull _pipeline;

        internal HelperShader HelperShader { get; private set; }
        internal PipelineFull PipelineInternal => _pipeline;

        internal BarrierBatch Barriers { get; private set; }

        public IPipeline Pipeline => _pipeline;

        public IWindow Window => _window;

        private readonly Func<Instance, Vk, SurfaceKHR> _getSurface;
        private readonly Func<string[]> _getRequiredExtensions;
        private readonly string _preferredGpuId;

        private int[] _pdReservedBindings;
        private readonly static int[] _pdReservedBindingsNvn = { 3, 18, 21, 36, 30 };
        private readonly static int[] _pdReservedBindingsOgl = { 17, 18, 34, 35, 36 };

        internal Vendor Vendor { get; private set; }
        internal bool IsAmdWindows { get; private set; }
        internal bool IsIntelWindows { get; private set; }
        internal bool IsAmdGcn { get; private set; }
        internal bool IsNvidiaPreTuring { get; private set; }
        internal bool IsMoltenVk { get; private set; }
        internal bool IsTBDR { get; private set; }
        internal bool IsSharedMemory { get; private set; }
        public string GpuVendor { get; private set; }
        public string GpuDriver { get; private set; }
        public string GpuRenderer { get; private set; }
        public string GpuVersion { get; private set; }

        public bool PreferThreading => true;

        public event EventHandler<ScreenCaptureImageInfo> ScreenCaptured;

        public VulkanRenderer(Vk api, Func<Instance, Vk, SurfaceKHR> surfaceFunc, Func<string[]> requiredExtensionsFunc, string preferredGpuId)
        {
            _getSurface = surfaceFunc;
            _getRequiredExtensions = requiredExtensionsFunc;
            _preferredGpuId = preferredGpuId;
            Api = api;
            Shaders = new HashSet<ShaderCollection>();
            Textures = new HashSet<ITexture>();
            Samplers = new HashSet<SamplerHolder>();

            if (OperatingSystem.IsMacOS())
            {
                MVKInitialization.Initialize();

                // Any device running on MacOS is using MoltenVK, even Intel and AMD vendors.
                IsMoltenVk = true;
            }
        }

        private unsafe void LoadFeatures(uint maxQueueCount, uint queueFamilyIndex)
        {
            FormatCapabilities = new FormatCapabilities(Api, _physicalDevice.PhysicalDevice);

            if (Api.TryGetDeviceExtension(_instance.Instance, _device, out ExtConditionalRendering conditionalRenderingApi))
            {
                ConditionalRenderingApi = conditionalRenderingApi;
            }

            if (Api.TryGetDeviceExtension(_instance.Instance, _device, out ExtExtendedDynamicState extendedDynamicStateApi))
            {
                ExtendedDynamicStateApi = extendedDynamicStateApi;
            }

            if (Api.TryGetDeviceExtension(_instance.Instance, _device, out KhrPushDescriptor pushDescriptorApi))
            {
                PushDescriptorApi = pushDescriptorApi;
            }

            if (Api.TryGetDeviceExtension(_instance.Instance, _device, out ExtTransformFeedback transformFeedbackApi))
            {
                TransformFeedbackApi = transformFeedbackApi;
            }

            if (Api.TryGetDeviceExtension(_instance.Instance, _device, out KhrDrawIndirectCount drawIndirectCountApi))
            {
                DrawIndirectCountApi = drawIndirectCountApi;
            }

            if (maxQueueCount >= 2)
            {
                Api.GetDeviceQueue(_device, queueFamilyIndex, 1, out var backgroundQueue);
                BackgroundQueue = backgroundQueue;
                BackgroundQueueLock = new object();
            }

            PhysicalDeviceProperties2 properties2 = new()
            {
                SType = StructureType.PhysicalDeviceProperties2,
            };

            PhysicalDeviceSubgroupProperties propertiesSubgroup = new()
            {
                SType = StructureType.PhysicalDeviceSubgroupProperties,
                PNext = properties2.PNext,
            };

            properties2.PNext = &propertiesSubgroup;

            PhysicalDeviceBlendOperationAdvancedPropertiesEXT propertiesBlendOperationAdvanced = new()
            {
                SType = StructureType.PhysicalDeviceBlendOperationAdvancedPropertiesExt,
            };

            bool supportsBlendOperationAdvanced = _physicalDevice.IsDeviceExtensionPresent("VK_EXT_blend_operation_advanced");

            if (supportsBlendOperationAdvanced)
            {
                propertiesBlendOperationAdvanced.PNext = properties2.PNext;
                properties2.PNext = &propertiesBlendOperationAdvanced;
            }

            bool supportsTransformFeedback = _physicalDevice.IsDeviceExtensionPresent(ExtTransformFeedback.ExtensionName);

            PhysicalDeviceTransformFeedbackPropertiesEXT propertiesTransformFeedback = new()
            {
                SType = StructureType.PhysicalDeviceTransformFeedbackPropertiesExt,
            };

            if (supportsTransformFeedback)
            {
                propertiesTransformFeedback.PNext = properties2.PNext;
                properties2.PNext = &propertiesTransformFeedback;
            }

            PhysicalDevicePortabilitySubsetPropertiesKHR propertiesPortabilitySubset = new()
            {
                SType = StructureType.PhysicalDevicePortabilitySubsetPropertiesKhr,
            };

            bool supportsPushDescriptors = _physicalDevice.IsDeviceExtensionPresent(KhrPushDescriptor.ExtensionName);

            PhysicalDevicePushDescriptorPropertiesKHR propertiesPushDescriptor = new PhysicalDevicePushDescriptorPropertiesKHR()
            {
                SType = StructureType.PhysicalDevicePushDescriptorPropertiesKhr
            };

            if (supportsPushDescriptors)
            {
                propertiesPushDescriptor.PNext = properties2.PNext;
                properties2.PNext = &propertiesPushDescriptor;
            }

            PhysicalDeviceFeatures2 features2 = new()
            {
                SType = StructureType.PhysicalDeviceFeatures2,
            };

            PhysicalDevicePrimitiveTopologyListRestartFeaturesEXT featuresPrimitiveTopologyListRestart = new()
            {
                SType = StructureType.PhysicalDevicePrimitiveTopologyListRestartFeaturesExt,
            };

            PhysicalDeviceRobustness2FeaturesEXT featuresRobustness2 = new()
            {
                SType = StructureType.PhysicalDeviceRobustness2FeaturesExt,
            };

            PhysicalDeviceShaderFloat16Int8FeaturesKHR featuresShaderInt8 = new()
            {
                SType = StructureType.PhysicalDeviceShaderFloat16Int8Features,
            };

            PhysicalDeviceCustomBorderColorFeaturesEXT featuresCustomBorderColor = new()
            {
                SType = StructureType.PhysicalDeviceCustomBorderColorFeaturesExt,
            };

            PhysicalDeviceDepthClipControlFeaturesEXT featuresDepthClipControl = new()
            {
                SType = StructureType.PhysicalDeviceDepthClipControlFeaturesExt,
            };

            PhysicalDevicePortabilitySubsetFeaturesKHR featuresPortabilitySubset = new()
            {
                SType = StructureType.PhysicalDevicePortabilitySubsetFeaturesKhr,
            };

            if (_physicalDevice.IsDeviceExtensionPresent("VK_EXT_primitive_topology_list_restart"))
            {
                features2.PNext = &featuresPrimitiveTopologyListRestart;
            }

            if (_physicalDevice.IsDeviceExtensionPresent("VK_EXT_robustness2"))
            {
                featuresRobustness2.PNext = features2.PNext;
                features2.PNext = &featuresRobustness2;
            }

            if (_physicalDevice.IsDeviceExtensionPresent("VK_KHR_shader_float16_int8"))
            {
                featuresShaderInt8.PNext = features2.PNext;
                features2.PNext = &featuresShaderInt8;
            }

            if (_physicalDevice.IsDeviceExtensionPresent("VK_EXT_custom_border_color"))
            {
                featuresCustomBorderColor.PNext = features2.PNext;
                features2.PNext = &featuresCustomBorderColor;
            }

            bool supportsDepthClipControl = _physicalDevice.IsDeviceExtensionPresent("VK_EXT_depth_clip_control");

            if (supportsDepthClipControl)
            {
                featuresDepthClipControl.PNext = features2.PNext;
                features2.PNext = &featuresDepthClipControl;
            }

            bool usePortability = _physicalDevice.IsDeviceExtensionPresent("VK_KHR_portability_subset");

            if (usePortability)
            {
                propertiesPortabilitySubset.PNext = properties2.PNext;
                properties2.PNext = &propertiesPortabilitySubset;

                featuresPortabilitySubset.PNext = features2.PNext;
                features2.PNext = &featuresPortabilitySubset;
            }

            Api.GetPhysicalDeviceProperties2(_physicalDevice.PhysicalDevice, &properties2);
            Api.GetPhysicalDeviceFeatures2(_physicalDevice.PhysicalDevice, &features2);

            var portabilityFlags = PortabilitySubsetFlags.None;
            uint vertexBufferAlignment = 1;

            if (usePortability)
            {
                vertexBufferAlignment = propertiesPortabilitySubset.MinVertexInputBindingStrideAlignment;

                portabilityFlags |= featuresPortabilitySubset.TriangleFans ? 0 : PortabilitySubsetFlags.NoTriangleFans;
                portabilityFlags |= featuresPortabilitySubset.PointPolygons ? 0 : PortabilitySubsetFlags.NoPointMode;
                portabilityFlags |= featuresPortabilitySubset.ImageView2DOn3DImage ? 0 : PortabilitySubsetFlags.No3DImageView;
                portabilityFlags |= featuresPortabilitySubset.SamplerMipLodBias ? 0 : PortabilitySubsetFlags.NoLodBias;
            }

            bool supportsCustomBorderColor = _physicalDevice.IsDeviceExtensionPresent("VK_EXT_custom_border_color") &&
                                             featuresCustomBorderColor.CustomBorderColors &&
                                             featuresCustomBorderColor.CustomBorderColorWithoutFormat;

            ref var properties = ref properties2.Properties;

            ulong minResourceAlignment = Math.Max(
                Math.Max(
                    properties.Limits.MinStorageBufferOffsetAlignment,
                    properties.Limits.MinUniformBufferOffsetAlignment),
                properties.Limits.MinTexelBufferOffsetAlignment
            );

            SampleCountFlags supportedSampleCounts =
                properties.Limits.FramebufferColorSampleCounts &
                properties.Limits.FramebufferDepthSampleCounts &
                properties.Limits.FramebufferStencilSampleCounts;

            Capabilities = new HardwareCapabilities(
                _physicalDevice.IsDeviceExtensionPresent("VK_EXT_index_type_uint8"),
                supportsCustomBorderColor,
                supportsBlendOperationAdvanced,
                propertiesBlendOperationAdvanced.AdvancedBlendCorrelatedOverlap,
                propertiesBlendOperationAdvanced.AdvancedBlendNonPremultipliedSrcColor,
                propertiesBlendOperationAdvanced.AdvancedBlendNonPremultipliedDstColor,
                _physicalDevice.IsDeviceExtensionPresent(KhrDrawIndirectCount.ExtensionName),
                _physicalDevice.IsDeviceExtensionPresent("VK_EXT_fragment_shader_interlock"),
                _physicalDevice.IsDeviceExtensionPresent("VK_NV_geometry_shader_passthrough"),
                features2.Features.ShaderFloat64,
                featuresShaderInt8.ShaderInt8,
                _physicalDevice.IsDeviceExtensionPresent("VK_EXT_shader_stencil_export"),
                features2.Features.ShaderStorageImageMultisample,
                _physicalDevice.IsDeviceExtensionPresent(ExtConditionalRendering.ExtensionName),
                _physicalDevice.IsDeviceExtensionPresent(ExtExtendedDynamicState.ExtensionName),
                features2.Features.MultiViewport && !(IsMoltenVk && Vendor == Vendor.Amd), // Workaround for AMD on MoltenVK issue
                featuresRobustness2.NullDescriptor || IsMoltenVk,
                supportsPushDescriptors && !IsMoltenVk,
                propertiesPushDescriptor.MaxPushDescriptors,
                featuresPrimitiveTopologyListRestart.PrimitiveTopologyListRestart,
                featuresPrimitiveTopologyListRestart.PrimitiveTopologyPatchListRestart,
                supportsTransformFeedback,
                propertiesTransformFeedback.TransformFeedbackQueries,
                features2.Features.OcclusionQueryPrecise,
                _physicalDevice.PhysicalDeviceFeatures.PipelineStatisticsQuery,
                _physicalDevice.PhysicalDeviceFeatures.GeometryShader,
                _physicalDevice.PhysicalDeviceFeatures.TessellationShader,
                _physicalDevice.IsDeviceExtensionPresent("VK_NV_viewport_array2"),
                _physicalDevice.IsDeviceExtensionPresent(ExtExternalMemoryHost.ExtensionName),
                supportsDepthClipControl && featuresDepthClipControl.DepthClipControl,
                propertiesSubgroup.SubgroupSize,
                supportedSampleCounts,
                portabilityFlags,
                vertexBufferAlignment,
                properties.Limits.SubTexelPrecisionBits,
                minResourceAlignment);

            IsSharedMemory = MemoryAllocator.IsDeviceMemoryShared(_physicalDevice);

            MemoryAllocator = new MemoryAllocator(Api, _physicalDevice, _device);

            Api.TryGetDeviceExtension(_instance.Instance, _device, out ExtExternalMemoryHost hostMemoryApi);
            HostMemoryAllocator = new HostMemoryAllocator(MemoryAllocator, Api, hostMemoryApi, _device);

            CommandBufferPool = new CommandBufferPool(Api, _device, Queue, QueueLock, queueFamilyIndex);

            DescriptorSetManager = new DescriptorSetManager(_device, PipelineBase.DescriptorSetLayouts);

            PipelineLayoutCache = new PipelineLayoutCache();

            BackgroundResources = new BackgroundResources(this, _device);

            BufferManager = new BufferManager(this, _device);

            SyncManager = new SyncManager(this, _device);
            _pipeline = new PipelineFull(this, _device);
            _pipeline.Initialize();

            HelperShader = new HelperShader(this, _device);

            Barriers = new BarrierBatch(this);

            _counters = new Counters(this, _device, _pipeline);
        }

        private void SetupContext(GraphicsDebugLevel logLevel)
        {
            _instance = VulkanInitialization.CreateInstance(Api, logLevel, _getRequiredExtensions());
            _debugMessenger = new VulkanDebugMessenger(Api, _instance.Instance, logLevel);

            if (Api.TryGetInstanceExtension(_instance.Instance, out KhrSurface surfaceApi))
            {
                SurfaceApi = surfaceApi;
            }

            _surface = _getSurface(_instance.Instance, Api);
            _physicalDevice = VulkanInitialization.FindSuitablePhysicalDevice(Api, _instance, _surface, _preferredGpuId);

            var queueFamilyIndex = VulkanInitialization.FindSuitableQueueFamily(Api, _physicalDevice, _surface, out uint maxQueueCount);

            _device = VulkanInitialization.CreateDevice(Api, _physicalDevice, queueFamilyIndex, maxQueueCount);

            if (Api.TryGetDeviceExtension(_instance.Instance, _device, out KhrSwapchain swapchainApi))
            {
                SwapchainApi = swapchainApi;
            }

            Api.GetDeviceQueue(_device, queueFamilyIndex, 0, out var queue);
            Queue = queue;
            QueueLock = new object();

            LoadFeatures(maxQueueCount, queueFamilyIndex);

            QueueFamilyIndex = queueFamilyIndex;

            _window = new Window(this, _surface, _physicalDevice.PhysicalDevice, _device);

            _initialized = true;
        }

        internal int[] GetPushDescriptorReservedBindings(bool isOgl)
        {
            // The first call of this method determines what push descriptor layout is used for all shaders on this renderer.
            // This is chosen to minimize shaders that can't fit their uniforms on the device's max number of push descriptors.
            if (_pdReservedBindings == null)
            {
                if (Capabilities.MaxPushDescriptors <= Constants.MaxUniformBuffersPerStage * 2)
                {
                    _pdReservedBindings = isOgl ? _pdReservedBindingsOgl : _pdReservedBindingsNvn;
                }
                else
                {
                    _pdReservedBindings = Array.Empty<int>();
                }
            }

            return _pdReservedBindings;
        }

        public BufferHandle CreateBuffer(int size, BufferAccess access)
        {
            return BufferManager.CreateWithHandle(this, size, access.HasFlag(BufferAccess.SparseCompatible), access.Convert(), default, access == BufferAccess.Stream);
        }

        public BufferHandle CreateBuffer(int size, BufferAccess access, BufferHandle storageHint)
        {
            return BufferManager.CreateWithHandle(this, size, access.HasFlag(BufferAccess.SparseCompatible), access.Convert(), storageHint);
        }

        public BufferHandle CreateBuffer(nint pointer, int size)
        {
            return BufferManager.CreateHostImported(this, pointer, size);
        }

        public BufferHandle CreateBufferSparse(ReadOnlySpan<BufferRange> storageBuffers)
        {
            return BufferManager.CreateSparse(this, storageBuffers);
        }

        public IProgram CreateProgram(ShaderSource[] sources, ShaderInfo info)
        {
            bool isCompute = sources.Length == 1 && sources[0].Stage == ShaderStage.Compute;

            if (info.State.HasValue || isCompute)
            {
                return new ShaderCollection(this, _device, sources, info.ResourceLayout, info.State ?? default, info.FromCache);
            }

            return new ShaderCollection(this, _device, sources, info.ResourceLayout);
        }

        internal ShaderCollection CreateProgramWithMinimalLayout(ShaderSource[] sources, ResourceLayout resourceLayout, SpecDescription[] specDescription = null)
        {
            return new ShaderCollection(this, _device, sources, resourceLayout, specDescription, isMinimal: true);
        }

        public ISampler CreateSampler(SamplerCreateInfo info)
        {
            return new SamplerHolder(this, _device, info);
        }

        public ITexture CreateTexture(TextureCreateInfo info)
        {
            if (info.Target == Target.TextureBuffer)
            {
                return new TextureBuffer(this, info);
            }

            return CreateTextureView(info);
        }

        internal TextureView CreateTextureView(TextureCreateInfo info)
        {
            // This should be disposed when all views are destroyed.
            var storage = CreateTextureStorage(info);
            return storage.CreateView(info, 0, 0);
        }

        internal TextureStorage CreateTextureStorage(TextureCreateInfo info)
        {
            return new TextureStorage(this, _device, info);
        }

        public void DeleteBuffer(BufferHandle buffer)
        {
            BufferManager.Delete(buffer);
        }

        internal void FlushAllCommands()
        {
            _pipeline?.FlushCommandsImpl();
        }

        internal void RegisterFlush()
        {
            SyncManager.RegisterFlush();

            // Periodically free unused regions of the staging buffer to avoid doing it all at once.
            BufferManager.StagingBuffer.FreeCompleted();
        }

        public PinnedSpan<byte> GetBufferData(BufferHandle buffer, int offset, int size)
        {
            return BufferManager.GetData(buffer, offset, size);
        }

        public unsafe Capabilities GetCapabilities()
        {
            FormatFeatureFlags compressedFormatFeatureFlags =
                FormatFeatureFlags.SampledImageBit |
                FormatFeatureFlags.SampledImageFilterLinearBit |
                FormatFeatureFlags.BlitSrcBit |
                FormatFeatureFlags.TransferSrcBit |
                FormatFeatureFlags.TransferDstBit;

            bool supportsBc123CompressionFormat = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.Bc1RgbaSrgb,
                Format.Bc1RgbaUnorm,
                Format.Bc2Srgb,
                Format.Bc2Unorm,
                Format.Bc3Srgb,
                Format.Bc3Unorm);

            bool supportsBc45CompressionFormat = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.Bc4Snorm,
                Format.Bc4Unorm,
                Format.Bc5Snorm,
                Format.Bc5Unorm);

            bool supportsBc67CompressionFormat = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.Bc6HSfloat,
                Format.Bc6HUfloat,
                Format.Bc7Srgb,
                Format.Bc7Unorm);

            bool supportsEtc2CompressionFormat = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.Etc2RgbaSrgb,
                Format.Etc2RgbaUnorm,
                Format.Etc2RgbPtaSrgb,
                Format.Etc2RgbPtaUnorm,
                Format.Etc2RgbSrgb,
                Format.Etc2RgbUnorm);

            bool supports5BitComponentFormat = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.R5G6B5Unorm,
                Format.R5G5B5A1Unorm,
                Format.R5G5B5X1Unorm,
                Format.B5G6R5Unorm,
                Format.B5G5R5A1Unorm,
                Format.A1B5G5R5Unorm);

            bool supportsR4G4B4A4Format = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.R4G4B4A4Unorm);

            bool supportsAstcFormats = FormatCapabilities.OptimalFormatsSupport(compressedFormatFeatureFlags,
                Format.Astc4x4Unorm,
                Format.Astc5x4Unorm,
                Format.Astc5x5Unorm,
                Format.Astc6x5Unorm,
                Format.Astc6x6Unorm,
                Format.Astc8x5Unorm,
                Format.Astc8x6Unorm,
                Format.Astc8x8Unorm,
                Format.Astc10x5Unorm,
                Format.Astc10x6Unorm,
                Format.Astc10x8Unorm,
                Format.Astc10x10Unorm,
                Format.Astc12x10Unorm,
                Format.Astc12x12Unorm,
                Format.Astc4x4Srgb,
                Format.Astc5x4Srgb,
                Format.Astc5x5Srgb,
                Format.Astc6x5Srgb,
                Format.Astc6x6Srgb,
                Format.Astc8x5Srgb,
                Format.Astc8x6Srgb,
                Format.Astc8x8Srgb,
                Format.Astc10x5Srgb,
                Format.Astc10x6Srgb,
                Format.Astc10x8Srgb,
                Format.Astc10x10Srgb,
                Format.Astc12x10Srgb,
                Format.Astc12x12Srgb);

            PhysicalDeviceVulkan12Features featuresVk12 = new()
            {
                SType = StructureType.PhysicalDeviceVulkan12Features,
            };

            PhysicalDeviceFeatures2 features2 = new()
            {
                SType = StructureType.PhysicalDeviceFeatures2,
                PNext = &featuresVk12,
            };

            Api.GetPhysicalDeviceFeatures2(_physicalDevice.PhysicalDevice, &features2);

            var limits = _physicalDevice.PhysicalDeviceProperties.Limits;
            var mainQueueProperties = _physicalDevice.QueueFamilyProperties[QueueFamilyIndex];

            return new Capabilities(
                api: TargetApi.Vulkan,
                GpuVendor,
                hasFrontFacingBug: IsIntelWindows,
                hasVectorIndexingBug: Vendor == Vendor.Qualcomm,
                needsFragmentOutputSpecialization: IsMoltenVk,
                reduceShaderPrecision: IsMoltenVk,
                supportsAstcCompression: features2.Features.TextureCompressionAstcLdr && supportsAstcFormats,
                supportsBc123Compression: supportsBc123CompressionFormat,
                supportsBc45Compression: supportsBc45CompressionFormat,
                supportsBc67Compression: supportsBc67CompressionFormat,
                supportsEtc2Compression: supportsEtc2CompressionFormat,
                supports3DTextureCompression: true,
                supportsBgraFormat: true,
                supportsR4G4Format: false,
                supportsR4G4B4A4Format: supportsR4G4B4A4Format,
                supportsSnormBufferTextureFormat: true,
                supports5BitComponentFormat: supports5BitComponentFormat,
                supportsSparseBuffer: features2.Features.SparseBinding && mainQueueProperties.QueueFlags.HasFlag(QueueFlags.SparseBindingBit),
                supportsBlendEquationAdvanced: Capabilities.SupportsBlendEquationAdvanced,
                supportsFragmentShaderInterlock: Capabilities.SupportsFragmentShaderInterlock,
                supportsFragmentShaderOrderingIntel: false,
                supportsGeometryShader: Capabilities.SupportsGeometryShader,
                supportsGeometryShaderPassthrough: Capabilities.SupportsGeometryShaderPassthrough,
                supportsTransformFeedback: Capabilities.SupportsTransformFeedback,
                supportsImageLoadFormatted: features2.Features.ShaderStorageImageReadWithoutFormat,
                supportsLayerVertexTessellation: featuresVk12.ShaderOutputLayer,
                supportsMismatchingViewFormat: true,
                supportsCubemapView: !IsAmdGcn,
                supportsNonConstantTextureOffset: false,
                supportsScaledVertexFormats: FormatCapabilities.SupportsScaledVertexFormats(),
                supportsShaderBallot: false,
                supportsShaderBarrierDivergence: Vendor != Vendor.Intel,
                supportsShaderFloat64: Capabilities.SupportsShaderFloat64,
                supportsTextureGatherOffsets: features2.Features.ShaderImageGatherExtended && !IsMoltenVk,
                supportsTextureShadowLod: false,
                supportsVertexStoreAndAtomics: features2.Features.VertexPipelineStoresAndAtomics,
                supportsViewportIndexVertexTessellation: featuresVk12.ShaderOutputViewportIndex,
                supportsViewportMask: Capabilities.SupportsViewportArray2,
                supportsViewportSwizzle: false,
                supportsIndirectParameters: true,
                supportsDepthClipControl: Capabilities.SupportsDepthClipControl,
                maximumUniformBuffersPerStage: Constants.MaxUniformBuffersPerStage,
                maximumStorageBuffersPerStage: Constants.MaxStorageBuffersPerStage,
                maximumTexturesPerStage: Constants.MaxTexturesPerStage,
                maximumImagesPerStage: Constants.MaxImagesPerStage,
                maximumComputeSharedMemorySize: (int)limits.MaxComputeSharedMemorySize,
                maximumSupportedAnisotropy: (int)limits.MaxSamplerAnisotropy,
                shaderSubgroupSize: (int)Capabilities.SubgroupSize,
                storageBufferOffsetAlignment: (int)limits.MinStorageBufferOffsetAlignment,
                textureBufferOffsetAlignment: (int)limits.MinTexelBufferOffsetAlignment,
                gatherBiasPrecision: IsIntelWindows || IsAmdWindows ? (int)Capabilities.SubTexelPrecisionBits : 0);
        }

        public HardwareInfo GetHardwareInfo()
        {
            return new HardwareInfo(GpuVendor, GpuRenderer, GpuDriver);
        }

        /// <summary>
        /// Gets the available Vulkan devices using the default Vulkan API
        /// object returned by <see cref="Vk.GetApi()"/>
        /// </summary>
        /// <returns></returns>
        public static DeviceInfo[] GetPhysicalDevices()
        {
            try
            {
                return VulkanInitialization.GetSuitablePhysicalDevices(Vk.GetApi());
            }
            catch (Exception ex)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Error querying Vulkan devices: {ex.Message}");

                return Array.Empty<DeviceInfo>();
            }
        }

        public static DeviceInfo[] GetPhysicalDevices(Vk api)
        {
            try
            {
                return VulkanInitialization.GetSuitablePhysicalDevices(api);
            }
            catch (Exception)
            {
                // If we got an exception here, Vulkan is most likely not supported.
                return Array.Empty<DeviceInfo>();
            }
        }

        private static string ParseStandardVulkanVersion(uint version)
        {
            return $"{version >> 22}.{(version >> 12) & 0x3FF}.{version & 0xFFF}";
        }

        private static string ParseDriverVersion(ref PhysicalDeviceProperties properties)
        {
            uint driverVersionRaw = properties.DriverVersion;

            // NVIDIA differ from the standard here and uses a different format.
            if (properties.VendorID == 0x10DE)
            {
                return $"{(driverVersionRaw >> 22) & 0x3FF}.{(driverVersionRaw >> 14) & 0xFF}.{(driverVersionRaw >> 6) & 0xFF}.{driverVersionRaw & 0x3F}";
            }

            return ParseStandardVulkanVersion(driverVersionRaw);
        }

        private unsafe void PrintGpuInformation()
        {
            var properties = _physicalDevice.PhysicalDeviceProperties;

            var hasDriverProperties = _physicalDevice.TryGetPhysicalDeviceDriverPropertiesKHR(Api, out var driverProperties);

            string vendorName = VendorUtils.GetNameFromId(properties.VendorID);

            Vendor = VendorUtils.FromId(properties.VendorID);

            IsAmdWindows = Vendor == Vendor.Amd && OperatingSystem.IsWindows();
            IsIntelWindows = Vendor == Vendor.Intel && OperatingSystem.IsWindows();
            IsTBDR =
                Vendor == Vendor.Apple ||
                Vendor == Vendor.Qualcomm ||
                Vendor == Vendor.ARM ||
                Vendor == Vendor.Broadcom ||
                Vendor == Vendor.ImgTec;

            GpuVendor = vendorName;
            GpuDriver = hasDriverProperties ? Marshal.PtrToStringAnsi((IntPtr)driverProperties.DriverName) : vendorName; // Fall back to vendor name if driver name isn't available.
            GpuRenderer = Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName);
            GpuVersion = $"Vulkan v{ParseStandardVulkanVersion(properties.ApiVersion)}, Driver v{ParseDriverVersion(ref properties)}";

            IsAmdGcn = !IsMoltenVk && Vendor == Vendor.Amd && VendorUtils.AmdGcnRegex().IsMatch(GpuRenderer);

            if (Vendor == Vendor.Nvidia)
            {
                var match = VendorUtils.NvidiaConsumerClassRegex().Match(GpuRenderer);

                if (match != null && int.TryParse(match.Groups[2].Value, out int gpuNumber))
                {
                    IsNvidiaPreTuring = gpuNumber < 2000;
                }
                else if (GpuDriver.Contains("TITAN") && !GpuDriver.Contains("RTX"))
                {
                    IsNvidiaPreTuring = true;
                }
            }

            Logger.Notice.Print(LogClass.Gpu, $"{GpuVendor} {GpuRenderer} ({GpuVersion})");
        }

        internal PrimitiveTopology TopologyRemap(PrimitiveTopology topology)
        {
            return topology switch
            {
                PrimitiveTopology.Quads => PrimitiveTopology.Triangles,
                PrimitiveTopology.QuadStrip => PrimitiveTopology.TriangleStrip,
                PrimitiveTopology.TriangleFan or PrimitiveTopology.Polygon => Capabilities.PortabilitySubset.HasFlag(PortabilitySubsetFlags.NoTriangleFans)
                    ? PrimitiveTopology.Triangles
                    : topology,
                _ => topology,
            };
        }

        internal bool TopologyUnsupported(PrimitiveTopology topology)
        {
            return topology switch
            {
                PrimitiveTopology.Quads => true,
                PrimitiveTopology.TriangleFan or PrimitiveTopology.Polygon => Capabilities.PortabilitySubset.HasFlag(PortabilitySubsetFlags.NoTriangleFans),
                _ => false,
            };
        }

        public void Initialize(GraphicsDebugLevel logLevel)
        {
            SetupContext(logLevel);

            PrintGpuInformation();
        }

        internal bool NeedsVertexBufferAlignment(int attrScalarAlignment, out int alignment)
        {
            if (Capabilities.VertexBufferAlignment > 1)
            {
                alignment = (int)Capabilities.VertexBufferAlignment;

                return true;
            }
            else if (Vendor != Vendor.Nvidia)
            {
                // Vulkan requires that vertex attributes are globally aligned by their component size,
                // so buffer strides that don't divide by the largest scalar element are invalid.
                // Guest applications do this, NVIDIA GPUs are OK with it, others are not.

                alignment = attrScalarAlignment;

                return true;
            }

            alignment = 1;

            return false;
        }

        public void PreFrame()
        {
            SyncManager.Cleanup();
        }

        public ICounterEvent ReportCounter(CounterType type, EventHandler<ulong> resultHandler, float divisor, bool hostReserved)
        {
            return _counters.QueueReport(type, resultHandler, divisor, hostReserved);
        }

        public void ResetCounter(CounterType type)
        {
            _counters.QueueReset(type);
        }

        public void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data)
        {
            BufferManager.SetData(buffer, offset, data, _pipeline.CurrentCommandBuffer, _pipeline.EndRenderPassDelegate);
        }

        public void UpdateCounters()
        {
            _counters.Update();
        }

        public void ResetCounterPool()
        {
            _counters.ResetCounterPool();
        }

        public void ResetFutureCounters(CommandBuffer cmd, int count)
        {
            _counters?.ResetFutureCounters(cmd, count);
        }

        public void BackgroundContextAction(Action action, bool alwaysBackground = false)
        {
            action();
        }

        public void CreateSync(ulong id, bool strict)
        {
            SyncManager.Create(id, strict);
        }

        public IProgram LoadProgramBinary(byte[] programBinary, bool isFragment, ShaderInfo info)
        {
            throw new NotImplementedException();
        }

        public void WaitSync(ulong id)
        {
            SyncManager.Wait(id);
        }

        public ulong GetCurrentSync()
        {
            return SyncManager.GetCurrent();
        }

        public void SetInterruptAction(Action<Action> interruptAction)
        {
            InterruptAction = interruptAction;
        }

        public void Screenshot()
        {
            _window.ScreenCaptureRequested = true;
        }

        public void OnScreenCaptured(ScreenCaptureImageInfo bitmap)
        {
            ScreenCaptured?.Invoke(this, bitmap);
        }

        public unsafe void Dispose()
        {
            if (!_initialized)
            {
                return;
            }

            CommandBufferPool.Dispose();
            BackgroundResources.Dispose();
            _counters.Dispose();
            _window.Dispose();
            HelperShader.Dispose();
            _pipeline.Dispose();
            BufferManager.Dispose();
            DescriptorSetManager.Dispose();
            PipelineLayoutCache.Dispose();
            Barriers.Dispose();

            MemoryAllocator.Dispose();

            foreach (var shader in Shaders)
            {
                shader.Dispose();
            }

            foreach (var texture in Textures)
            {
                texture.Release();
            }

            foreach (var sampler in Samplers)
            {
                sampler.Dispose();
            }

            SurfaceApi.DestroySurface(_instance.Instance, _surface, null);

            Api.DestroyDevice(_device, null);

            _debugMessenger.Dispose();

            // Last step destroy the instance
            _instance.Dispose();
        }

        public bool PrepareHostMapping(nint address, ulong size)
        {
            return Capabilities.SupportsHostImportedMemory &&
                HostMemoryAllocator.TryImport(BufferManager.HostImportedBufferMemoryRequirements, BufferManager.DefaultBufferMemoryFlags, address, size);
        }
    }
}
