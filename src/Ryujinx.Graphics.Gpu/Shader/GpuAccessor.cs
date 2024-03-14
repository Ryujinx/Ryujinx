using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Shader
{
    /// <summary>
    /// Represents a GPU state and memory accessor.
    /// </summary>
    class GpuAccessor : GpuAccessorBase, IGpuAccessor
    {
        private readonly GpuChannel _channel;
        private readonly GpuAccessorState _state;
        private readonly int _stageIndex;
        private readonly bool _compute;
        private readonly bool _isVulkan;

        /// <summary>
        /// Creates a new instance of the GPU state accessor for graphics shader translation.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="state">Current GPU state</param>
        /// <param name="stageIndex">Graphics shader stage index (0 = Vertex, 4 = Fragment)</param>
        public GpuAccessor(GpuContext context, GpuChannel channel, GpuAccessorState state, int stageIndex) : base(context, state.ResourceCounts, stageIndex)
        {
            _isVulkan = context.Capabilities.Api == TargetApi.Vulkan;
            _channel = channel;
            _state = state;
            _stageIndex = stageIndex;

            if (stageIndex == (int)ShaderStage.Geometry - 1)
            {
                // Only geometry shaders require the primitive topology.
                _state.SpecializationState.RecordPrimitiveTopology();
            }
        }

        /// <summary>
        /// Creates a new instance of the GPU state accessor for compute shader translation.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="state">Current GPU state</param>
        public GpuAccessor(GpuContext context, GpuChannel channel, GpuAccessorState state) : base(context, state.ResourceCounts, 0)
        {
            _channel = channel;
            _state = state;
            _compute = true;
        }

        /// <inheritdoc/>
        public uint ConstantBuffer1Read(int offset)
        {
            ulong baseAddress = _compute
                ? _channel.BufferManager.GetComputeUniformBufferAddress(1)
                : _channel.BufferManager.GetGraphicsUniformBufferAddress(_stageIndex, 1);

            return _channel.MemoryManager.Physical.Read<uint>(baseAddress + (ulong)offset);
        }

        /// <inheritdoc/>
        public void Log(string message)
        {
            Logger.Warning?.Print(LogClass.Gpu, $"Shader translator: {message}");
        }

        /// <inheritdoc/>
        public ReadOnlySpan<ulong> GetCode(ulong address, int minimumSize)
        {
            int size = Math.Max(minimumSize, 0x1000 - (int)(address & 0xfff));

            return MemoryMarshal.Cast<byte, ulong>(_channel.MemoryManager.GetSpan(address, size));
        }

        /// <inheritdoc/>
        public int QueryComputeLocalSizeX() => _state.ComputeState.LocalSizeX;

        /// <inheritdoc/>
        public int QueryComputeLocalSizeY() => _state.ComputeState.LocalSizeY;

        /// <inheritdoc/>
        public int QueryComputeLocalSizeZ() => _state.ComputeState.LocalSizeZ;

        /// <inheritdoc/>
        public int QueryComputeLocalMemorySize() => _state.ComputeState.LocalMemorySize;

        /// <inheritdoc/>
        public int QueryComputeSharedMemorySize() => _state.ComputeState.SharedMemorySize;

        /// <inheritdoc/>
        public uint QueryConstantBufferUse()
        {
            uint useMask = _compute
                ? _channel.BufferManager.GetComputeUniformBufferUseMask()
                : _channel.BufferManager.GetGraphicsUniformBufferUseMask(_stageIndex);

            _state.SpecializationState?.RecordConstantBufferUse(_stageIndex, useMask);
            return useMask;
        }

        /// <inheritdoc/>
        public GpuGraphicsState QueryGraphicsState()
        {
            return _state.GraphicsState.CreateShaderGraphicsState(!_isVulkan, _isVulkan || _state.GraphicsState.YNegateEnabled);
        }

        /// <inheritdoc/>
        public bool QueryHasConstantBufferDrawParameters()
        {
            return _state.GraphicsState.HasConstantBufferDrawParameters;
        }

        /// <inheritdoc/>
        public bool QueryHasUnalignedStorageBuffer()
        {
            return _state.GraphicsState.HasUnalignedStorageBuffer || _state.ComputeState.HasUnalignedStorageBuffer;
        }

        /// <inheritdoc/>
        public SamplerType QuerySamplerType(int handle, int cbufSlot)
        {
            _state.SpecializationState?.RecordTextureSamplerType(_stageIndex, handle, cbufSlot);
            return GetTextureDescriptor(handle, cbufSlot).UnpackTextureTarget().ConvertSamplerType();
        }

        /// <inheritdoc/>
        public int QueryTextureArrayLengthFromBuffer(int slot)
        {
            int size = _compute
                ? _channel.BufferManager.GetComputeUniformBufferSize(slot)
                : _channel.BufferManager.GetGraphicsUniformBufferSize(_stageIndex, slot);

            int arrayLength = size / Constants.TextureHandleSizeInBytes;

            _state.SpecializationState?.RegisterTextureArrayLengthFromBuffer(_stageIndex, 0, slot, arrayLength);

            return arrayLength;
        }

        //// <inheritdoc/>
        public TextureFormat QueryTextureFormat(int handle, int cbufSlot)
        {
            _state.SpecializationState?.RecordTextureFormat(_stageIndex, handle, cbufSlot);
            var descriptor = GetTextureDescriptor(handle, cbufSlot);
            return ConvertToTextureFormat(descriptor.UnpackFormat(), descriptor.UnpackSrgb());
        }

        /// <inheritdoc/>
        public bool QueryTextureCoordNormalized(int handle, int cbufSlot)
        {
            _state.SpecializationState?.RecordTextureCoordNormalized(_stageIndex, handle, cbufSlot);
            return GetTextureDescriptor(handle, cbufSlot).UnpackTextureCoordNormalized();
        }

        /// <summary>
        /// Gets the texture descriptor for a given texture on the pool.
        /// </summary>
        /// <param name="handle">Index of the texture (this is the word offset of the handle in the constant buffer)</param>
        /// <param name="cbufSlot">Constant buffer slot for the texture handle</param>
        /// <returns>Texture descriptor</returns>
        private Image.TextureDescriptor GetTextureDescriptor(int handle, int cbufSlot)
        {
            if (_compute)
            {
                return _channel.TextureManager.GetComputeTextureDescriptor(
                    _state.PoolState.TexturePoolGpuVa,
                    _state.PoolState.TextureBufferIndex,
                    _state.PoolState.TexturePoolMaximumId,
                    handle,
                    cbufSlot);
            }
            else
            {
                return _channel.TextureManager.GetGraphicsTextureDescriptor(
                    _state.PoolState.TexturePoolGpuVa,
                    _state.PoolState.TextureBufferIndex,
                    _state.PoolState.TexturePoolMaximumId,
                    _stageIndex,
                    handle,
                    cbufSlot);
            }
        }

        /// <inheritdoc/>
        public bool QueryTransformFeedbackEnabled()
        {
            return _state.TransformFeedbackDescriptors != null;
        }

        /// <inheritdoc/>
        public ReadOnlySpan<byte> QueryTransformFeedbackVaryingLocations(int bufferIndex)
        {
            return _state.TransformFeedbackDescriptors[bufferIndex].AsSpan();
        }

        /// <inheritdoc/>
        public int QueryTransformFeedbackStride(int bufferIndex)
        {
            return _state.TransformFeedbackDescriptors[bufferIndex].Stride;
        }

        /// <inheritdoc/>
        public void RegisterTexture(int handle, int cbufSlot)
        {
            _state.SpecializationState?.RegisterTexture(_stageIndex, handle, cbufSlot, GetTextureDescriptor(handle, cbufSlot));
        }
    }
}
