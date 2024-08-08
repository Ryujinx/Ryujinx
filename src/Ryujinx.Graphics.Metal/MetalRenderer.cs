using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public sealed class MetalRenderer : IRenderer
    {
        public const int TotalSets = 4;

        private readonly MTLDevice _device;
        private readonly MTLCommandQueue _queue;
        private readonly Func<CAMetalLayer> _getMetalLayer;

        private Pipeline _pipeline;
        private Window _window;

        public event EventHandler<ScreenCaptureImageInfo> ScreenCaptured;
        public bool PreferThreading => true;
        public IPipeline Pipeline => _pipeline;
        public IWindow Window => _window;

        internal MTLCommandQueue BackgroundQueue { get; private set; }
        internal HelperShader HelperShader { get; private set; }
        internal BufferManager BufferManager { get; private set; }
        internal CommandBufferPool CommandBufferPool { get; private set; }
        internal BackgroundResources BackgroundResources { get; private set; }
        internal Action<Action> InterruptAction { get; private set; }
        internal SyncManager SyncManager { get; private set; }

        public MetalRenderer(Func<CAMetalLayer> metalLayer)
        {
            _device = MTLDevice.CreateSystemDefaultDevice();

            if (_device.ArgumentBuffersSupport != MTLArgumentBuffersTier.Tier2)
            {
                throw new NotSupportedException("Metal backend requires Tier 2 Argument Buffer support.");
            }

            _queue = _device.NewCommandQueue(CommandBufferPool.MaxCommandBuffers + 1);
            BackgroundQueue = _device.NewCommandQueue(CommandBufferPool.MaxCommandBuffers);

            _getMetalLayer = metalLayer;
        }

        public void Initialize(GraphicsDebugLevel logLevel)
        {
            var layer = _getMetalLayer();
            layer.Device = _device;
            layer.FramebufferOnly = false;

            CommandBufferPool = new CommandBufferPool(_queue);
            _window = new Window(this, layer);
            _pipeline = new Pipeline(_device, this);
            BufferManager = new BufferManager(_device, this, _pipeline);

            _pipeline.InitEncoderStateManager(BufferManager);

            BackgroundResources = new BackgroundResources(this);
            HelperShader = new HelperShader(_device, this, _pipeline);
            SyncManager = new SyncManager(this);
        }

        public void BackgroundContextAction(Action action, bool alwaysBackground = false)
        {
            // GetData methods should be thread safe, so we can call this directly.
            // Texture copy (scaled) may also happen in here, so that should also be thread safe.

            action();
        }

        public BufferHandle CreateBuffer(int size, BufferAccess access)
        {
            return BufferManager.CreateWithHandle(size);
        }

        public BufferHandle CreateBuffer(IntPtr pointer, int size)
        {
            return BufferManager.Create(pointer, size);
        }

        public BufferHandle CreateBufferSparse(ReadOnlySpan<BufferRange> storageBuffers)
        {
            throw new NotImplementedException();
        }

        public IImageArray CreateImageArray(int size, bool isBuffer)
        {
            return new ImageArray(size, isBuffer, _pipeline);
        }

        public IProgram CreateProgram(ShaderSource[] shaders, ShaderInfo info)
        {
            return new Program(shaders, info.ResourceLayout, _device, info.ComputeLocalSize);
        }

        public ISampler CreateSampler(SamplerCreateInfo info)
        {
            return new Sampler(_device, info);
        }

        public ITexture CreateTexture(TextureCreateInfo info)
        {
            if (info.Target == Target.TextureBuffer)
            {
                return new TextureBuffer(_device, this, _pipeline, info);
            }

            return new Texture(_device, this, _pipeline, info);
        }

        public ITextureArray CreateTextureArray(int size, bool isBuffer)
        {
            return new TextureArray(size, isBuffer, _pipeline);
        }

        public bool PrepareHostMapping(IntPtr address, ulong size)
        {
            // TODO: Metal Host Mapping
            return false;
        }

        public void CreateSync(ulong id, bool strict)
        {
            SyncManager.Create(id, strict);
        }

        public void DeleteBuffer(BufferHandle buffer)
        {
            BufferManager.Delete(buffer);
        }

        public PinnedSpan<byte> GetBufferData(BufferHandle buffer, int offset, int size)
        {
            return BufferManager.GetData(buffer, offset, size);
        }

        public Capabilities GetCapabilities()
        {
            // TODO: Finalize these values
            return new Capabilities(
                api: TargetApi.Metal,
                vendorName: HardwareInfoTools.GetVendor(),
                SystemMemoryType.UnifiedMemory,
                hasFrontFacingBug: false,
                hasVectorIndexingBug: false,
                needsFragmentOutputSpecialization: true,
                reduceShaderPrecision: true,
                supportsAstcCompression: true,
                supportsBc123Compression: true,
                supportsBc45Compression: true,
                supportsBc67Compression: true,
                supportsEtc2Compression: true,
                supports3DTextureCompression: true,
                supportsBgraFormat: true,
                supportsR4G4Format: false,
                supportsR4G4B4A4Format: true,
                supportsScaledVertexFormats: false,
                supportsSnormBufferTextureFormat: true,
                supportsSparseBuffer: false,
                supports5BitComponentFormat: true,
                supportsBlendEquationAdvanced: false,
                supportsFragmentShaderInterlock: true,
                supportsFragmentShaderOrderingIntel: false,
                supportsGeometryShader: false,
                supportsGeometryShaderPassthrough: false,
                supportsTransformFeedback: false,
                supportsImageLoadFormatted: false,
                supportsLayerVertexTessellation: false,
                supportsMismatchingViewFormat: true,
                supportsCubemapView: true,
                supportsNonConstantTextureOffset: false,
                supportsQuads: false,
                supportsSeparateSampler: true,
                supportsShaderBallot: false,
                supportsShaderBarrierDivergence: false,
                supportsShaderFloat64: false,
                supportsTextureGatherOffsets: false,
                supportsTextureShadowLod: false,
                supportsVertexStoreAndAtomics: false,
                supportsViewportIndexVertexTessellation: false,
                supportsViewportMask: false,
                supportsViewportSwizzle: false,
                supportsIndirectParameters: true,
                supportsDepthClipControl: false,
                uniformBufferSetIndex: (int)Constants.ConstantBuffersSetIndex,
                storageBufferSetIndex: (int)Constants.StorageBuffersSetIndex,
                textureSetIndex: (int)Constants.TexturesSetIndex,
                imageSetIndex: (int)Constants.ImagesSetIndex,
                extraSetBaseIndex: TotalSets,
                maximumExtraSets: (int)Constants.MaximumExtraSets,
                maximumUniformBuffersPerStage: Constants.MaxUniformBuffersPerStage,
                maximumStorageBuffersPerStage: Constants.MaxStorageBuffersPerStage,
                maximumTexturesPerStage: Constants.MaxTexturesPerStage,
                maximumImagesPerStage: Constants.MaxImagesPerStage,
                maximumComputeSharedMemorySize: (int)_device.MaxThreadgroupMemoryLength,
                maximumSupportedAnisotropy: 0,
                shaderSubgroupSize: 256,
                storageBufferOffsetAlignment: 16,
                textureBufferOffsetAlignment: 16,
                gatherBiasPrecision: 0
            );
        }

        public ulong GetCurrentSync()
        {
            return SyncManager.GetCurrent();
        }

        public HardwareInfo GetHardwareInfo()
        {
            return new HardwareInfo(HardwareInfoTools.GetVendor(), HardwareInfoTools.GetModel(), "Apple");
        }

        public IProgram LoadProgramBinary(byte[] programBinary, bool hasFragmentShader, ShaderInfo info)
        {
            throw new NotImplementedException();
        }

        public void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data)
        {
            BufferManager.SetData(buffer, offset, data, _pipeline.Cbs);
        }

        public void UpdateCounters()
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
        }

        public void PreFrame()
        {
            SyncManager.Cleanup();
        }

        public ICounterEvent ReportCounter(CounterType type, EventHandler<ulong> resultHandler, float divisor, bool hostReserved)
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
            var counterEvent = new CounterEvent();
            resultHandler?.Invoke(counterEvent, type == CounterType.SamplesPassed ? (ulong)1 : 0);
            return counterEvent;
        }

        public void ResetCounter(CounterType type)
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
        }

        public void WaitSync(ulong id)
        {
            SyncManager.Wait(id);
        }

        public void FlushAllCommands()
        {
            _pipeline.FlushCommandsImpl();
        }

        public void RegisterFlush()
        {
            SyncManager.RegisterFlush();

            // Periodically free unused regions of the staging buffer to avoid doing it all at once.
            BufferManager.StagingBuffer.FreeCompleted();
        }

        public void SetInterruptAction(Action<Action> interruptAction)
        {
            InterruptAction = interruptAction;
        }

        public void Screenshot()
        {
            // TODO: Screenshots
        }

        public void Dispose()
        {
            BackgroundResources.Dispose();
            _pipeline.Dispose();
            _window.Dispose();
        }
    }
}
