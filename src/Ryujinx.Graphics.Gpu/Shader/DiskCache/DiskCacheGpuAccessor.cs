using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Shader.DiskCache
{
    /// <summary>
    /// Represents a GPU state and memory accessor.
    /// </summary>
    class DiskCacheGpuAccessor : GpuAccessorBase, IGpuAccessor
    {
        private readonly ReadOnlyMemory<byte> _data;
        private readonly ReadOnlyMemory<byte> _cb1Data;
        private readonly ShaderSpecializationState _oldSpecState;
        private readonly ShaderSpecializationState _newSpecState;
        private readonly int _stageIndex;
        private readonly bool _isVulkan;

        /// <summary>
        /// Creates a new instance of the cached GPU state accessor for shader translation.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="data">The data of the shader</param>
        /// <param name="cb1Data">The constant buffer 1 data of the shader</param>
        /// <param name="oldSpecState">Shader specialization state of the cached shader</param>
        /// <param name="newSpecState">Shader specialization state of the recompiled shader</param>
        /// <param name="counts">Resource counts shared across all shader stages</param>
        /// <param name="stageIndex">Shader stage index</param>
        public DiskCacheGpuAccessor(
            GpuContext context,
            ReadOnlyMemory<byte> data,
            ReadOnlyMemory<byte> cb1Data,
            ShaderSpecializationState oldSpecState,
            ShaderSpecializationState newSpecState,
            ResourceCounts counts,
            int stageIndex) : base(context, counts, stageIndex)
        {
            _data = data;
            _cb1Data = cb1Data;
            _oldSpecState = oldSpecState;
            _newSpecState = newSpecState;
            _stageIndex = stageIndex;
            _isVulkan = context.Capabilities.Api == TargetApi.Vulkan;

            if (stageIndex == (int)ShaderStage.Geometry - 1)
            {
                // Only geometry shaders require the primitive topology.
                newSpecState.RecordPrimitiveTopology();
            }
        }

        /// <inheritdoc/>
        public uint ConstantBuffer1Read(int offset)
        {
            if (offset + sizeof(uint) > _cb1Data.Length)
            {
                throw new DiskCacheLoadException(DiskCacheLoadResult.InvalidCb1DataLength);
            }

            return MemoryMarshal.Cast<byte, uint>(_cb1Data.Span[offset..])[0];
        }

        /// <inheritdoc/>
        public void Log(string message)
        {
            Logger.Warning?.Print(LogClass.Gpu, $"Shader translator: {message}");
        }

        /// <inheritdoc/>
        public ReadOnlySpan<ulong> GetCode(ulong address, int minimumSize)
        {
            return MemoryMarshal.Cast<byte, ulong>(_data.Span[(int)address..]);
        }

        /// <inheritdoc/>
        public int QueryComputeLocalSizeX() => _oldSpecState.ComputeState.LocalSizeX;

        /// <inheritdoc/>
        public int QueryComputeLocalSizeY() => _oldSpecState.ComputeState.LocalSizeY;

        /// <inheritdoc/>
        public int QueryComputeLocalSizeZ() => _oldSpecState.ComputeState.LocalSizeZ;

        /// <inheritdoc/>
        public int QueryComputeLocalMemorySize() => _oldSpecState.ComputeState.LocalMemorySize;

        /// <inheritdoc/>
        public int QueryComputeSharedMemorySize() => _oldSpecState.ComputeState.SharedMemorySize;

        /// <inheritdoc/>
        public uint QueryConstantBufferUse()
        {
            _newSpecState.RecordConstantBufferUse(_stageIndex, _oldSpecState.ConstantBufferUse[_stageIndex]);
            return _oldSpecState.ConstantBufferUse[_stageIndex];
        }

        /// <inheritdoc/>
        public GpuGraphicsState QueryGraphicsState()
        {
            return _oldSpecState.GraphicsState.CreateShaderGraphicsState(!_isVulkan, _isVulkan || _oldSpecState.GraphicsState.YNegateEnabled);
        }

        /// <inheritdoc/>
        public bool QueryHasConstantBufferDrawParameters()
        {
            return _oldSpecState.GraphicsState.HasConstantBufferDrawParameters;
        }

        /// <inheritdoc/>
        public SamplerType QuerySamplerType(int handle, int cbufSlot)
        {
            _newSpecState.RecordTextureSamplerType(_stageIndex, handle, cbufSlot);
            return _oldSpecState.GetTextureTarget(_stageIndex, handle, cbufSlot).ConvertSamplerType();
        }

        /// <inheritdoc/>
        public int QueryTextureArrayLengthFromBuffer(int slot)
        {
            if (!_oldSpecState.TextureArrayFromBufferRegistered(_stageIndex, 0, slot))
            {
                throw new DiskCacheLoadException(DiskCacheLoadResult.MissingTextureArrayLength);
            }

            int arrayLength = _oldSpecState.GetTextureArrayFromBufferLength(_stageIndex, 0, slot);
            _newSpecState.RegisterTextureArrayLengthFromBuffer(_stageIndex, 0, slot, arrayLength);

            return arrayLength;
        }

        /// <inheritdoc/>
        public TextureFormat QueryTextureFormat(int handle, int cbufSlot)
        {
            _newSpecState.RecordTextureFormat(_stageIndex, handle, cbufSlot);
            (uint format, bool formatSrgb) = _oldSpecState.GetFormat(_stageIndex, handle, cbufSlot);
            return ConvertToTextureFormat(format, formatSrgb);
        }

        /// <inheritdoc/>
        public bool QueryTextureCoordNormalized(int handle, int cbufSlot)
        {
            _newSpecState.RecordTextureCoordNormalized(_stageIndex, handle, cbufSlot);
            return _oldSpecState.GetCoordNormalized(_stageIndex, handle, cbufSlot);
        }

        /// <inheritdoc/>
        public bool QueryTransformFeedbackEnabled()
        {
            return _oldSpecState.TransformFeedbackDescriptors != null;
        }

        /// <inheritdoc/>
        public ReadOnlySpan<byte> QueryTransformFeedbackVaryingLocations(int bufferIndex)
        {
            return _oldSpecState.TransformFeedbackDescriptors[bufferIndex].AsSpan();
        }

        /// <inheritdoc/>
        public int QueryTransformFeedbackStride(int bufferIndex)
        {
            return _oldSpecState.TransformFeedbackDescriptors[bufferIndex].Stride;
        }

        /// <inheritdoc/>
        public bool QueryHasUnalignedStorageBuffer()
        {
            return _oldSpecState.GraphicsState.HasUnalignedStorageBuffer || _oldSpecState.ComputeState.HasUnalignedStorageBuffer;
        }

        /// <inheritdoc/>
        public void RegisterTexture(int handle, int cbufSlot)
        {
            if (!_oldSpecState.TextureRegistered(_stageIndex, handle, cbufSlot))
            {
                throw new DiskCacheLoadException(DiskCacheLoadResult.MissingTextureDescriptor);
            }

            (uint format, bool formatSrgb) = _oldSpecState.GetFormat(_stageIndex, handle, cbufSlot);
            TextureTarget target = _oldSpecState.GetTextureTarget(_stageIndex, handle, cbufSlot);
            bool coordNormalized = _oldSpecState.GetCoordNormalized(_stageIndex, handle, cbufSlot);
            _newSpecState.RegisterTexture(_stageIndex, handle, cbufSlot, format, formatSrgb, target, coordNormalized);
        }
    }
}
