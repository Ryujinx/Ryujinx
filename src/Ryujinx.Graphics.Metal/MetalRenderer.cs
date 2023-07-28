using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public sealed class MetalRenderer : IRenderer
    {
        private readonly MTLDevice _device;
        private readonly Window _window;
        private readonly Pipeline _pipeline;
        private readonly MTLCommandQueue _queue;

        public event EventHandler<ScreenCaptureImageInfo> ScreenCaptured;
        public bool PreferThreading => true;
        public IPipeline Pipeline => _pipeline;
        public IWindow Window => _window;

        public MetalRenderer()
        {
            _device = MTLDevice.CreateSystemDefaultDevice();
            _queue = _device.NewCommandQueue();
            _pipeline = new Pipeline(_device, _queue);
            _window = new Window(this);
        }

        public void Initialize(GraphicsDebugLevel logLevel)
        {
        }

        public void BackgroundContextAction(Action action, bool alwaysBackground = false)
        {
            throw new NotImplementedException();
        }

        public BufferHandle CreateBuffer(int size, BufferHandle storageHint)
        {
            return CreateBuffer(size, BufferAccess.Default);
        }

        public BufferHandle CreateBuffer(IntPtr pointer, int size)
        {
            var buffer = _device.NewBuffer(pointer, (ulong)size, MTLResourceOptions.ResourceStorageModeShared);
            var bufferPtr = buffer.NativePtr;
            return Unsafe.As<IntPtr, BufferHandle>(ref bufferPtr);
        }

        public BufferHandle CreateBuffer(int size, BufferAccess access)
        {
            var buffer = _device.NewBuffer((ulong)size, MTLResourceOptions.ResourceStorageModeShared);

            if (access == BufferAccess.FlushPersistent)
            {
                buffer.SetPurgeableState(MTLPurgeableState.NonVolatile);
            }

            var bufferPtr = buffer.NativePtr;
            return Unsafe.As<IntPtr, BufferHandle>(ref bufferPtr);
        }

        public IProgram CreateProgram(ShaderSource[] shaders, ShaderInfo info)
        {
            var library = _device.NewDefaultLibrary();
            throw new NotImplementedException();
        }

        public ISampler CreateSampler(SamplerCreateInfo info)
        {
            (MTLSamplerMinMagFilter minFilter, MTLSamplerMipFilter mipFilter) = info.MinFilter.Convert();

            var sampler = _device.NewSamplerState(new MTLSamplerDescriptor
            {
                BorderColor = MTLSamplerBorderColor.TransparentBlack,
                MinFilter = minFilter,
                MagFilter = info.MagFilter.Convert(),
                MipFilter = mipFilter,
                CompareFunction = info.CompareOp.Convert(),
                LodMinClamp = info.MinLod,
                LodMaxClamp = info.MaxLod,
                LodAverage = false,
                MaxAnisotropy = (uint)info.MaxAnisotropy,
                SAddressMode = info.AddressU.Convert(),
                TAddressMode = info.AddressV.Convert(),
                RAddressMode = info.AddressP.Convert()
            });

            return new Sampler(sampler);
        }

        public ITexture CreateTexture(TextureCreateInfo info)
        {
            return new Texture(_device, _pipeline, info);
        }

        public bool PrepareHostMapping(IntPtr address, ulong size)
        {
            // TODO: Metal Host Mapping
            return false;
        }

        public void CreateSync(ulong id, bool strict)
        {
            throw new NotImplementedException();
        }

        public void DeleteBuffer(BufferHandle buffer)
        {
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref buffer));
            mtlBuffer.SetPurgeableState(MTLPurgeableState.Empty);
        }

        public unsafe PinnedSpan<byte> GetBufferData(BufferHandle buffer, int offset, int size)
        {
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref buffer));
            return new PinnedSpan<byte>(IntPtr.Add(mtlBuffer.Contents, offset).ToPointer(), size);
        }

        public Capabilities GetCapabilities()
        {
            // TODO: Finalize these values
            return new Capabilities(
                api: TargetApi.Metal,
                vendorName: HardwareInfoTools.GetVendor(),
                hasFrontFacingBug: false,
                hasVectorIndexingBug: true,
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
                supportsSnormBufferTextureFormat: true,
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
                supportsShaderBallot: false,
                supportsShaderBarrierDivergence: false,
                supportsShaderFloat64: false,
                supportsTextureShadowLod: false,
                supportsViewportIndexVertexTessellation: false,
                supportsViewportMask: false,
                supportsViewportSwizzle: false,
                supportsIndirectParameters: true,
                supportsDepthClipControl: false,
                maximumUniformBuffersPerStage: Constants.MaxUniformBuffersPerStage,
                maximumStorageBuffersPerStage: Constants.MaxStorageBuffersPerStage,
                maximumTexturesPerStage: Constants.MaxTexturesPerStage,
                maximumImagesPerStage: Constants.MaxTextureBindings,
                maximumComputeSharedMemorySize: (int)_device.MaxThreadgroupMemoryLength,
                maximumSupportedAnisotropy: 0,
                storageBufferOffsetAlignment: 0,
                gatherBiasPrecision: 0
            );
        }

        public ulong GetCurrentSync()
        {
            throw new NotImplementedException();
        }

        public HardwareInfo GetHardwareInfo()
        {
            return new HardwareInfo(HardwareInfoTools.GetVendor(), HardwareInfoTools.GetModel());
        }

        public IProgram LoadProgramBinary(byte[] programBinary, bool hasFragmentShader, ShaderInfo info)
        {
            throw new NotImplementedException();
        }

        public unsafe void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data)
        {
            MTLBuffer mtlBuffer = new(Unsafe.As<BufferHandle, IntPtr>(ref buffer));
            var span = new Span<byte>(mtlBuffer.Contents.ToPointer(), (int)mtlBuffer.Length);
            data.CopyTo(span.Slice(offset));
            mtlBuffer.DidModifyRange(new NSRange
            {
                location = (ulong)offset,
                length = (ulong)data.Length
            });
        }

        public void UpdateCounters()
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
        }

        public void PreFrame()
        {

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
            throw new NotImplementedException();
        }

        public void SetInterruptAction(Action<Action> interruptAction)
        {
            // Not needed for now
        }

        public void Screenshot()
        {
            // TODO: Screenshots
        }

        public void Dispose()
        {
            _window.Dispose();
            _pipeline.Dispose();
        }
    }
}
