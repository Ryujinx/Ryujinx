using Ryujinx.Graphics.Shader.CodeGen;
using System;

namespace Ryujinx.Graphics.Shader
{
    /// <summary>
    /// GPU state access interface.
    /// </summary>
    public interface IGpuAccessor : ILogger
    {
        /// <summary>
        /// Reads data from the constant buffer 1.
        /// </summary>
        /// <param name="offset">Offset in bytes to read from</param>
        /// <returns>Value at the given offset</returns>
        uint ConstantBuffer1Read(int offset)
        {
            return 0;
        }

        /// <summary>
        /// Gets a span of the specified memory location, containing shader code.
        /// </summary>
        /// <param name="address">GPU virtual address of the data</param>
        /// <param name="minimumSize">Minimum size that the returned span may have</param>
        /// <returns>Span of the memory location</returns>
        ReadOnlySpan<ulong> GetCode(ulong address, int minimumSize);

        /// <summary>
        /// Gets the size in bytes of a bound constant buffer for the current shader stage.
        /// </summary>
        /// <param name="slot">The number of the constant buffer to get the size from</param>
        /// <returns>Size in bytes</returns>
        int QueryTextureArrayLengthFromBuffer(int slot);

        /// <summary>
        /// Queries the binding number of a constant buffer.
        /// </summary>
        /// <param name="index">Constant buffer index</param>
        /// <returns>Binding number</returns>
        int CreateConstantBufferBinding(int index);

        /// <summary>
        /// Queries the binding number of an image.
        /// </summary>
        /// <param name="count">For array of images, the number of elements of the array, otherwise it should be 1</param>
        /// <param name="isBuffer">Indicates if the image is a buffer image</param>
        /// <returns>Binding number</returns>
        int CreateImageBinding(int count, bool isBuffer);

        /// <summary>
        /// Queries the binding number of a storage buffer.
        /// </summary>
        /// <param name="index">Storage buffer index</param>
        /// <returns>Binding number</returns>
        int CreateStorageBufferBinding(int index);

        /// <summary>
        /// Queries the binding number of a texture.
        /// </summary>
        /// <param name="count">For array of textures, the number of elements of the array, otherwise it should be 1</param>
        /// <param name="isBuffer">Indicates if the texture is a buffer texture</param>
        /// <returns>Binding number</returns>
        int CreateTextureBinding(int count, bool isBuffer);

        /// <summary>
        /// Queries Local Size X for compute shaders.
        /// </summary>
        /// <returns>Local Size X</returns>
        int QueryComputeLocalSizeX()
        {
            return 1;
        }

        /// <summary>
        /// Queries Local Size Y for compute shaders.
        /// </summary>
        /// <returns>Local Size Y</returns>
        int QueryComputeLocalSizeY()
        {
            return 1;
        }

        /// <summary>
        /// Queries Local Size Z for compute shaders.
        /// </summary>
        /// <returns>Local Size Z</returns>
        int QueryComputeLocalSizeZ()
        {
            return 1;
        }

        /// <summary>
        /// Queries Local Memory size in bytes for compute shaders.
        /// </summary>
        /// <returns>Local Memory size in bytes</returns>
        int QueryComputeLocalMemorySize()
        {
            return 0x1000;
        }

        /// <summary>
        /// Queries Shared Memory size in bytes for compute shaders.
        /// </summary>
        /// <returns>Shared Memory size in bytes</returns>
        int QueryComputeSharedMemorySize()
        {
            return 0xc000;
        }

        /// <summary>
        /// Queries Constant Buffer usage information.
        /// </summary>
        /// <returns>A mask where each bit set indicates a bound constant buffer</returns>
        uint QueryConstantBufferUse()
        {
            return 0;
        }

        /// <summary>
        /// Queries specialized GPU graphics state that the shader depends on.
        /// </summary>
        /// <returns>GPU graphics state</returns>
        GpuGraphicsState QueryGraphicsState()
        {
            return new GpuGraphicsState(
                false,
                InputTopology.Points,
                false,
                TessPatchType.Triangles,
                TessSpacing.EqualSpacing,
                false,
                false,
                false,
                false,
                false,
                1f,
                AlphaTestOp.Always,
                0f,
                default,
                true,
                default,
                false,
                false,
                false);
        }

        /// <summary>
        /// Queries whenever the current draw has written the base vertex and base instance into Constant Buffer 0.
        /// </summary>
        /// <returns>True if the shader translator can assume that the constant buffer contains the base IDs, false otherwise</returns>
        bool QueryHasConstantBufferDrawParameters()
        {
            return false;
        }

        /// <summary>
        /// Queries whenever the current draw uses unaligned storage buffer addresses.
        /// </summary>
        /// <returns>True if any storage buffer address is not aligned to 16 bytes, false otherwise</returns>
        bool QueryHasUnalignedStorageBuffer()
        {
            return false;
        }

        /// <summary>
        /// Queries host's gather operation precision bits for biasing their coordinates. Zero means no bias.
        /// </summary>
        /// <returns>Bits of gather operation precision to use for coordinate bias</returns>
        int QueryHostGatherBiasPrecision()
        {
            return 0;
        }

        /// <summary>
        /// Queries host about whether to reduce precision to improve performance.
        /// </summary>
        /// <returns>True if precision is limited to vertex position, false otherwise</returns>
        bool QueryHostReducedPrecision()
        {
            return false;
        }

        /// <summary>
        /// Queries host about the presence of the FrontFacing built-in variable bug.
        /// </summary>
        /// <returns>True if the bug is present on the host device used, false otherwise</returns>
        bool QueryHostHasFrontFacingBug()
        {
            return false;
        }

        /// <summary>
        /// Queries host about the presence of the vector indexing bug.
        /// </summary>
        /// <returns>True if the bug is present on the host device used, false otherwise</returns>
        bool QueryHostHasVectorIndexingBug()
        {
            return false;
        }

        /// <summary>
        /// Queries host storage buffer alignment required.
        /// </summary>
        /// <returns>Host storage buffer alignment in bytes</returns>
        int QueryHostStorageBufferOffsetAlignment()
        {
            return 16;
        }

        /// <summary>
        /// Queries host shader subgroup size.
        /// </summary>
        /// <returns>Host shader subgroup size in invocations</returns>
        int QueryHostSubgroupSize()
        {
            return 32;
        }

        /// <summary>
        /// Queries host support for texture formats with BGRA component order (such as BGRA8).
        /// </summary>
        /// <returns>True if BGRA formats are supported, false otherwise</returns>
        bool QueryHostSupportsBgraFormat()
        {
            return true;
        }

        /// <summary>
        /// Queries host support for fragment shader ordering critical sections on the shader code.
        /// </summary>
        /// <returns>True if fragment shader interlock is supported, false otherwise</returns>
        bool QueryHostSupportsFragmentShaderInterlock()
        {
            return true;
        }

        /// <summary>
        /// Queries host support for fragment shader ordering scoped critical sections on the shader code.
        /// </summary>
        /// <returns>True if fragment shader ordering is supported, false otherwise</returns>
        bool QueryHostSupportsFragmentShaderOrderingIntel()
        {
            return false;
        }

        /// <summary>
        /// Queries host GPU geometry shader support.
        /// </summary>
        /// <returns>True if the GPU and driver supports geometry shaders, false otherwise</returns>
        bool QueryHostSupportsGeometryShader()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU geometry shader passthrough support.
        /// </summary>
        /// <returns>True if the GPU and driver supports geometry shader passthrough, false otherwise</returns>
        bool QueryHostSupportsGeometryShaderPassthrough()
        {
            return true;
        }

        /// <summary>
        /// Queries host support for readable images without a explicit format declaration on the shader.
        /// </summary>
        /// <returns>True if formatted image load is supported, false otherwise</returns>
        bool QueryHostSupportsImageLoadFormatted()
        {
            return true;
        }

        /// <summary>
        /// Queries host support for writes to the layer from vertex or tessellation shader stages.
        /// </summary>
        /// <returns>True if writes to the layer from vertex or tessellation are supported, false otherwise</returns>
        bool QueryHostSupportsLayerVertexTessellation()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU non-constant texture offset support.
        /// </summary>
        /// <returns>True if the GPU and driver supports non-constant texture offsets, false otherwise</returns>
        bool QueryHostSupportsNonConstantTextureOffset()
        {
            return true;
        }

        /// <summary>
        /// Queries host support scaled vertex formats, where a integer value is converted to floating-point.
        /// </summary>
        /// <returns>True if the host support scaled vertex formats, false otherwise</returns>
        bool QueryHostSupportsScaledVertexFormats()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU shader ballot support.
        /// </summary>
        /// <returns>True if the GPU and driver supports shader ballot, false otherwise</returns>
        bool QueryHostSupportsShaderBallot()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU shader support for barrier instructions on divergent control flow paths.
        /// </summary>
        /// <returns>True if the GPU supports barriers on divergent control flow paths, false otherwise</returns>
        bool QueryHostSupportsShaderBarrierDivergence()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU support for 64-bit floating point (double precision) operations on the shader.
        /// </summary>
        /// <returns>True if the GPU and driver supports double operations, false otherwise</returns>
        bool QueryHostSupportsShaderFloat64()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU support for signed normalized buffer texture formats.
        /// </summary>
        /// <returns>True if the GPU and driver supports the formats, false otherwise</returns>
        bool QueryHostSupportsSnormBufferTextureFormat()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU texture gather with multiple offsets support.
        /// </summary>
        /// <returns>True if the GPU and driver supports texture gather offsets, false otherwise</returns>
        bool QueryHostSupportsTextureGatherOffsets()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU texture shadow LOD support.
        /// </summary>
        /// <returns>True if the GPU and driver supports texture shadow LOD, false otherwise</returns>
        bool QueryHostSupportsTextureShadowLod()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU transform feedback support.
        /// </summary>
        /// <returns>True if the GPU and driver supports transform feedback, false otherwise</returns>
        bool QueryHostSupportsTransformFeedback()
        {
            return true;
        }

        /// <summary>
        /// Queries host support for writes to the viewport index from vertex or tessellation shader stages.
        /// </summary>
        /// <returns>True if writes to the viewport index from vertex or tessellation are supported, false otherwise</returns>
        bool QueryHostSupportsViewportIndexVertexTessellation()
        {
            return true;
        }

        /// <summary>
        /// Queries host GPU shader viewport mask output support.
        /// </summary>
        /// <returns>True if the GPU and driver supports shader viewport mask output, false otherwise</returns>
        bool QueryHostSupportsViewportMask()
        {
            return true;
        }

        /// <summary>
        /// Queries whether the host supports depth clip control.
        /// </summary>
        /// <returns>True if the GPU and driver supports depth clip control, false otherwise</returns>
        bool QueryHostSupportsDepthClipControl()
        {
            return true;
        }

        /// <summary>
        /// Queries sampler type information.
        /// </summary>
        /// <param name="handle">Texture handle</param>
        /// <param name="cbufSlot">Constant buffer slot for the texture handle</param>
        /// <returns>The sampler type value for the given handle</returns>
        SamplerType QuerySamplerType(int handle, int cbufSlot = -1)
        {
            return SamplerType.Texture2D;
        }

        /// <summary>
        /// Queries texture coordinate normalization information.
        /// </summary>
        /// <param name="handle">Texture handle</param>
        /// <param name="cbufSlot">Constant buffer slot for the texture handle</param>
        /// <returns>True if the coordinates are normalized, false otherwise</returns>
        bool QueryTextureCoordNormalized(int handle, int cbufSlot = -1)
        {
            return true;
        }

        /// <summary>
        /// Queries texture format information, for shaders using image load or store.
        /// </summary>
        /// <remarks>
        /// This only returns non-compressed color formats.
        /// If the format of the texture is a compressed, depth or unsupported format, then a default value is returned.
        /// </remarks>
        /// <param name="handle">Texture handle</param>
        /// <param name="cbufSlot">Constant buffer slot for the texture handle</param>
        /// <returns>Color format of the non-compressed texture</returns>
        TextureFormat QueryTextureFormat(int handle, int cbufSlot = -1)
        {
            return TextureFormat.R8G8B8A8Unorm;
        }

        /// <summary>
        /// Queries transform feedback enable state.
        /// </summary>
        /// <returns>True if the shader uses transform feedback, false otherwise</returns>
        bool QueryTransformFeedbackEnabled()
        {
            return false;
        }

        /// <summary>
        /// Queries the varying locations that should be written to the transform feedback buffer.
        /// </summary>
        /// <param name="bufferIndex">Index of the transform feedback buffer</param>
        /// <returns>Varying locations for the specified buffer</returns>
        ReadOnlySpan<byte> QueryTransformFeedbackVaryingLocations(int bufferIndex)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        /// <summary>
        /// Queries the stride (in bytes) of the per vertex data written into the transform feedback buffer.
        /// </summary>
        /// <param name="bufferIndex">Index of the transform feedback buffer</param>
        /// <returns>Stride for the specified buffer</returns>
        int QueryTransformFeedbackStride(int bufferIndex)
        {
            return 0;
        }

        /// <summary>
        /// Registers a texture used by the shader.
        /// </summary>
        /// <param name="handle">Texture handle word offset</param>
        /// <param name="cbufSlot">Constant buffer slot where the texture handle is located</param>
        void RegisterTexture(int handle, int cbufSlot)
        {
            // Only useful when recording information for a disk shader cache.
        }
    }
}
