using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader.Translation;
using System;
using SharpMetal;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public sealed class MetalRenderer : IRenderer
    {
        private MTLDevice _device;

        private Window _window;

        private Pipeline _pipeline;

        internal MTLCommandQueue Queue { get; private set; }

        internal int BufferCount { get; private set; }

        public event EventHandler<ScreenCaptureImageInfo> ScreenCaptured;
        public bool PreferThreading => true;
        public IPipeline Pipeline => _pipeline;
        public IWindow Window => _window;

        public void Initialize(GraphicsDebugLevel logLevel)
        {
            _device = MTLDevice.MTLCreateSystemDefaultDevice();
            Queue = _device.NewCommandQueueWithMaxCommandBufferCount(Constants.MaxCommandBuffersPerQueue);

            var commandBuffer = Queue.CommandBufferWithDescriptor(new MTLCommandBufferDescriptor { RetainedReferences = true });

            _pipeline = new Pipeline(_device, commandBuffer);
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
            BufferCount++;

            var buffer = _device.NewBufferWithBytesLengthOptions(pointer, (ulong)size, MTLResourceOptions.StorageModeShared);
            var bufferPtr = buffer.NativePtr;
            return Unsafe.As<IntPtr, BufferHandle>(ref bufferPtr);
        }

        public BufferHandle CreateBuffer(int size, BufferAccess access)
        {
            BufferCount++;

            var buffer = _device.NewBufferWithLengthOptions((ulong)size, MTLResourceOptions.StorageModeShared);
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

            var sampler = _device.CreateSamplerState(new MTLSamplerDescriptor
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

            throw new NotImplementedException();
        }

        public ITexture CreateTexture(TextureCreateInfo info, float scale)
        {
            MTLTextureDescriptor descriptor = new()
            {
                PixelFormat = FormatCapabilities.ConvertToMTLFormat(info.Format),
                TextureType = info.Target.Convert(),
                Width = (ulong)info.Width,
                Height = (ulong)info.Height,
                MipmapLevelCount = (ulong)info.Levels,
                SampleCount = (ulong)info.Samples,
            };

            return CreateTextureView(info, scale);
        }

        internal TextureView CreateTextureView(TextureCreateInfo info, float scale)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetBufferData(BufferHandle buffer, int offset, int size)
        {
            throw new NotImplementedException();
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
                supportsImageLoadFormatted: false,
                supportsLayerVertexTessellation: false,
                supportsMismatchingViewFormat: true,
                supportsCubemapView: true,
                supportsNonConstantTextureOffset: false,
                supportsShaderBallot: false,
                supportsTextureShadowLod: false,
                supportsViewportIndexVertexTessellation: false,
                supportsViewportMask: false,
                supportsViewportSwizzle: false,
                supportsIndirectParameters: true,
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

        public void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data)
        {
            throw new NotImplementedException();
        }

        public void UpdateCounters()
        {
            throw new NotImplementedException();
        }

        public void PreFrame()
        {
            throw new NotImplementedException();
        }

        public ICounterEvent ReportCounter(CounterType type, EventHandler<ulong> resultHandler, bool hostReserved)
        {
            throw new NotImplementedException();
        }

        public void ResetCounter(CounterType type)
        {
            throw new NotImplementedException();
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