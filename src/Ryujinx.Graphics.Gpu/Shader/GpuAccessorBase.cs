using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// GPU accessor.
    /// </summary>
    class GpuAccessorBase
    {
        private readonly GpuContext _context;
        private readonly ResourceCounts _resourceCounts;
        private readonly int _stageIndex;

        private int _reservedConstantBuffers;
        private int _reservedStorageBuffers;
        private int _reservedTextures;
        private int _reservedImages;

        private int _staticTexturesCount;
        private int _staticImagesCount;

        /// <summary>
        /// Creates a new GPU accessor.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="resourceCounts">Counter of GPU resources used by the shader</param>
        /// <param name="stageIndex">Index of the shader stage, 0 for compute</param>
        public GpuAccessorBase(GpuContext context, ResourceCounts resourceCounts, int stageIndex)
        {
            _context = context;
            _resourceCounts = resourceCounts;
            _stageIndex = stageIndex;
        }

        /// <summary>
        /// Initializes counts for bindings that will be reserved for emulator use.
        /// </summary>
        /// <param name="tfEnabled">Indicates if the current graphics shader is used with transform feedback enabled</param>
        /// <param name="vertexAsCompute">Indicates that the vertex shader will be emulated on a compute shader</param>
        public void InitializeReservedCounts(bool tfEnabled, bool vertexAsCompute)
        {
            ResourceReservationCounts rrc = new(!_context.Capabilities.SupportsTransformFeedback && tfEnabled, vertexAsCompute);

            _reservedConstantBuffers = rrc.ReservedConstantBuffers;
            _reservedStorageBuffers = rrc.ReservedStorageBuffers;
            _reservedTextures = rrc.ReservedTextures;
            _reservedImages = rrc.ReservedImages;
        }

        public int CreateConstantBufferBinding(int index)
        {
            int binding;

            if (_context.Capabilities.Api == TargetApi.Vulkan)
            {
                binding = GetBindingFromIndex(index, _context.Capabilities.MaximumUniformBuffersPerStage, "Uniform buffer");
            }
            else
            {
                binding = _resourceCounts.UniformBuffersCount++;
            }

            return binding + _reservedConstantBuffers;
        }

        public int CreateImageBinding(int count, bool isBuffer)
        {
            int binding;

            if (_context.Capabilities.Api == TargetApi.Vulkan)
            {
                if (count == 1)
                {
                    int index = _staticImagesCount++;

                    if (isBuffer)
                    {
                        index += (int)_context.Capabilities.MaximumImagesPerStage;
                    }

                    binding = GetBindingFromIndex(index, _context.Capabilities.MaximumImagesPerStage * 2, "Image");
                }
                else
                {
                    binding = (int)GetDynamicBaseIndexDual(_context.Capabilities.MaximumImagesPerStage) + _resourceCounts.ImagesCount++;
                }
            }
            else
            {
                binding = _resourceCounts.ImagesCount;

                _resourceCounts.ImagesCount += count;
            }

            return binding + _reservedImages;
        }

        public int CreateStorageBufferBinding(int index)
        {
            int binding;

            if (_context.Capabilities.Api == TargetApi.Vulkan)
            {
                binding = GetBindingFromIndex(index, _context.Capabilities.MaximumStorageBuffersPerStage, "Storage buffer");
            }
            else
            {
                binding = _resourceCounts.StorageBuffersCount++;
            }

            return binding + _reservedStorageBuffers;
        }

        public int CreateTextureBinding(int count, bool isBuffer)
        {
            int binding;

            if (_context.Capabilities.Api == TargetApi.Vulkan)
            {
                if (count == 1)
                {
                    int index = _staticTexturesCount++;

                    if (isBuffer)
                    {
                        index += (int)_context.Capabilities.MaximumTexturesPerStage;
                    }

                    binding = GetBindingFromIndex(index, _context.Capabilities.MaximumTexturesPerStage * 2, "Texture");
                }
                else
                {
                    binding = (int)GetDynamicBaseIndexDual(_context.Capabilities.MaximumTexturesPerStage) + _resourceCounts.TexturesCount++;
                }
            }
            else
            {
                binding = _resourceCounts.TexturesCount;

                _resourceCounts.TexturesCount += count;
            }

            return binding + _reservedTextures;
        }

        private int GetBindingFromIndex(int index, uint maxPerStage, string resourceName)
        {
            if ((uint)index >= maxPerStage)
            {
                Logger.Error?.Print(LogClass.Gpu, $"{resourceName} index {index} exceeds per stage limit of {maxPerStage}.");
            }

            return GetStageIndex(_stageIndex) * (int)maxPerStage + index;
        }

        public static int GetStageIndex(int stageIndex)
        {
            // This is just a simple remapping to ensure that most frequently used shader stages
            // have the lowest binding numbers.
            // This is useful because if we need to run on a system with a low limit on the bindings,
            // then we can still get most games working as the most common shaders will have low binding numbers.
            return stageIndex switch
            {
                4 => 1, // Fragment
                3 => 2, // Geometry
                1 => 3, // Tessellation control
                2 => 4, // Tessellation evaluation
                _ => 0, // Vertex/Compute
            };
        }

        private static uint GetDynamicBaseIndexDual(uint maxPerStage)
        {
            return GetDynamicBaseIndex(maxPerStage) * 2;
        }

        private static uint GetDynamicBaseIndex(uint maxPerStage)
        {
            return maxPerStage * Constants.ShaderStages;
        }

        public int QueryHostGatherBiasPrecision() => _context.Capabilities.GatherBiasPrecision;

        public bool QueryHostReducedPrecision() => _context.Capabilities.ReduceShaderPrecision;

        public bool QueryHostHasFrontFacingBug() => _context.Capabilities.HasFrontFacingBug;

        public bool QueryHostHasVectorIndexingBug() => _context.Capabilities.HasVectorIndexingBug;

        public int QueryHostStorageBufferOffsetAlignment() => _context.Capabilities.StorageBufferOffsetAlignment;

        public int QueryHostSubgroupSize() => _context.Capabilities.ShaderSubgroupSize;

        public bool QueryHostSupportsBgraFormat() => _context.Capabilities.SupportsBgraFormat;

        public bool QueryHostSupportsFragmentShaderInterlock() => _context.Capabilities.SupportsFragmentShaderInterlock;

        public bool QueryHostSupportsFragmentShaderOrderingIntel() => _context.Capabilities.SupportsFragmentShaderOrderingIntel;

        public bool QueryHostSupportsGeometryShader() => _context.Capabilities.SupportsGeometryShader;

        public bool QueryHostSupportsGeometryShaderPassthrough() => _context.Capabilities.SupportsGeometryShaderPassthrough;

        public bool QueryHostSupportsImageLoadFormatted() => _context.Capabilities.SupportsImageLoadFormatted;

        public bool QueryHostSupportsLayerVertexTessellation() => _context.Capabilities.SupportsLayerVertexTessellation;

        public bool QueryHostSupportsNonConstantTextureOffset() => _context.Capabilities.SupportsNonConstantTextureOffset;

        public bool QueryHostSupportsScaledVertexFormats() => _context.Capabilities.SupportsScaledVertexFormats;

        public bool QueryHostSupportsShaderBallot() => _context.Capabilities.SupportsShaderBallot;

        public bool QueryHostSupportsShaderBarrierDivergence() => _context.Capabilities.SupportsShaderBarrierDivergence;

        public bool QueryHostSupportsShaderFloat64() => _context.Capabilities.SupportsShaderFloat64;

        public bool QueryHostSupportsSnormBufferTextureFormat() => _context.Capabilities.SupportsSnormBufferTextureFormat;

        public bool QueryHostSupportsTextureGatherOffsets() => _context.Capabilities.SupportsTextureGatherOffsets;

        public bool QueryHostSupportsTextureShadowLod() => _context.Capabilities.SupportsTextureShadowLod;

        public bool QueryHostSupportsTransformFeedback() => _context.Capabilities.SupportsTransformFeedback;

        public bool QueryHostSupportsViewportIndexVertexTessellation() => _context.Capabilities.SupportsViewportIndexVertexTessellation;

        public bool QueryHostSupportsViewportMask() => _context.Capabilities.SupportsViewportMask;

        public bool QueryHostSupportsDepthClipControl() => _context.Capabilities.SupportsDepthClipControl;

        /// <summary>
        /// Converts a packed Maxwell texture format to the shader translator texture format.
        /// </summary>
        /// <param name="format">Packed maxwell format</param>
        /// <param name="formatSrgb">Indicates if the format is sRGB</param>
        /// <returns>Shader translator texture format</returns>
        protected static TextureFormat ConvertToTextureFormat(uint format, bool formatSrgb)
        {
            if (!FormatTable.TryGetTextureFormat(format, formatSrgb, out FormatInfo formatInfo))
            {
                return TextureFormat.Unknown;
            }

            return formatInfo.Format switch
            {
#pragma warning disable IDE0055 // Disable formatting
                Format.R8Unorm           => TextureFormat.R8Unorm,
                Format.R8Snorm           => TextureFormat.R8Snorm,
                Format.R8Uint            => TextureFormat.R8Uint,
                Format.R8Sint            => TextureFormat.R8Sint,
                Format.R16Float          => TextureFormat.R16Float,
                Format.R16Unorm          => TextureFormat.R16Unorm,
                Format.R16Snorm          => TextureFormat.R16Snorm,
                Format.R16Uint           => TextureFormat.R16Uint,
                Format.R16Sint           => TextureFormat.R16Sint,
                Format.R32Float          => TextureFormat.R32Float,
                Format.R32Uint           => TextureFormat.R32Uint,
                Format.R32Sint           => TextureFormat.R32Sint,
                Format.R8G8Unorm         => TextureFormat.R8G8Unorm,
                Format.R8G8Snorm         => TextureFormat.R8G8Snorm,
                Format.R8G8Uint          => TextureFormat.R8G8Uint,
                Format.R8G8Sint          => TextureFormat.R8G8Sint,
                Format.R16G16Float       => TextureFormat.R16G16Float,
                Format.R16G16Unorm       => TextureFormat.R16G16Unorm,
                Format.R16G16Snorm       => TextureFormat.R16G16Snorm,
                Format.R16G16Uint        => TextureFormat.R16G16Uint,
                Format.R16G16Sint        => TextureFormat.R16G16Sint,
                Format.R32G32Float       => TextureFormat.R32G32Float,
                Format.R32G32Uint        => TextureFormat.R32G32Uint,
                Format.R32G32Sint        => TextureFormat.R32G32Sint,
                Format.R8G8B8A8Unorm     => TextureFormat.R8G8B8A8Unorm,
                Format.R8G8B8A8Snorm     => TextureFormat.R8G8B8A8Snorm,
                Format.R8G8B8A8Uint      => TextureFormat.R8G8B8A8Uint,
                Format.R8G8B8A8Sint      => TextureFormat.R8G8B8A8Sint,
                Format.R8G8B8A8Srgb      => TextureFormat.R8G8B8A8Unorm,
                Format.R16G16B16A16Float => TextureFormat.R16G16B16A16Float,
                Format.R16G16B16A16Unorm => TextureFormat.R16G16B16A16Unorm,
                Format.R16G16B16A16Snorm => TextureFormat.R16G16B16A16Snorm,
                Format.R16G16B16A16Uint  => TextureFormat.R16G16B16A16Uint,
                Format.R16G16B16A16Sint  => TextureFormat.R16G16B16A16Sint,
                Format.R32G32B32A32Float => TextureFormat.R32G32B32A32Float,
                Format.R32G32B32A32Uint  => TextureFormat.R32G32B32A32Uint,
                Format.R32G32B32A32Sint  => TextureFormat.R32G32B32A32Sint,
                Format.R10G10B10A2Unorm  => TextureFormat.R10G10B10A2Unorm,
                Format.R10G10B10A2Uint   => TextureFormat.R10G10B10A2Uint,
                Format.R11G11B10Float    => TextureFormat.R11G11B10Float,
                _                        => TextureFormat.Unknown,
#pragma warning restore IDE0055
            };
        }
    }
}
