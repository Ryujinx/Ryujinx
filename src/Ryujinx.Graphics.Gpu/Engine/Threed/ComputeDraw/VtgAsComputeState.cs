using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Engine.Types;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Gpu.Shader;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;

namespace Ryujinx.Graphics.Gpu.Engine.Threed.ComputeDraw
{
    /// <summary>
    /// Vertex, tessellation and geometry as compute shader state.
    /// </summary>
    struct VtgAsComputeState
    {
        private const int ComputeLocalSize = 32;

        private readonly GpuContext _context;
        private readonly GpuChannel _channel;
        private readonly DeviceStateWithShadow<ThreedClassState> _state;
        private readonly VtgAsComputeContext _vacContext;
        private readonly ThreedClass _engine;
        private readonly ShaderAsCompute _vertexAsCompute;
        private readonly ShaderAsCompute _geometryAsCompute;
        private readonly IProgram _vertexPassthroughProgram;
        private readonly PrimitiveTopology _topology;
        private readonly int _count;
        private readonly int _instanceCount;
        private readonly int _firstIndex;
        private readonly int _firstVertex;
        private readonly int _firstInstance;
        private readonly bool _indexed;

        private readonly int _vertexDataOffset;
        private readonly int _vertexDataSize;
        private readonly int _geometryVertexDataOffset;
        private readonly int _geometryVertexDataSize;
        private readonly int _geometryIndexDataOffset;
        private readonly int _geometryIndexDataSize;
        private readonly int _geometryIndexDataCount;

        /// <summary>
        /// Creates a new vertex, tessellation and geometry as compute shader state.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="channel">GPU channel</param>
        /// <param name="state">3D engine state</param>
        /// <param name="vacContext">Vertex as compute context</param>
        /// <param name="engine">3D engine</param>
        /// <param name="vertexAsCompute">Vertex shader converted to compute</param>
        /// <param name="geometryAsCompute">Optional geometry shader converted to compute</param>
        /// <param name="vertexPassthroughProgram">Fragment shader with a vertex passthrough shader to feed the compute output into the fragment stage</param>
        /// <param name="topology">Primitive topology of the draw</param>
        /// <param name="count">Index or vertex count of the draw</param>
        /// <param name="instanceCount">Instance count</param>
        /// <param name="firstIndex">First index on the index buffer, for indexed draws</param>
        /// <param name="firstVertex">First vertex on the vertex buffer</param>
        /// <param name="firstInstance">First instance</param>
        /// <param name="indexed">Whether the draw is indexed</param>
        public VtgAsComputeState(
            GpuContext context,
            GpuChannel channel,
            DeviceStateWithShadow<ThreedClassState> state,
            VtgAsComputeContext vacContext,
            ThreedClass engine,
            ShaderAsCompute vertexAsCompute,
            ShaderAsCompute geometryAsCompute,
            IProgram vertexPassthroughProgram,
            PrimitiveTopology topology,
            int count,
            int instanceCount,
            int firstIndex,
            int firstVertex,
            int firstInstance,
            bool indexed)
        {
            _context = context;
            _channel = channel;
            _state = state;
            _vacContext = vacContext;
            _engine = engine;
            _vertexAsCompute = vertexAsCompute;
            _geometryAsCompute = geometryAsCompute;
            _vertexPassthroughProgram = vertexPassthroughProgram;
            _topology = topology;
            _count = count;
            _instanceCount = instanceCount;
            _firstIndex = firstIndex;
            _firstVertex = firstVertex;
            _firstInstance = firstInstance;
            _indexed = indexed;

            int vertexDataSize = vertexAsCompute.Reservations.OutputSizeInBytesPerInvocation * count * instanceCount;

            (_vertexDataOffset, _vertexDataSize) = _vacContext.GetVertexDataBuffer(vertexDataSize);

            if (geometryAsCompute != null)
            {
                int totalPrimitivesCount = VtgAsComputeContext.GetPrimitivesCount(topology, count * instanceCount);
                int maxCompleteStrips = GetMaxCompleteStrips(geometryAsCompute.Info.GeometryVerticesPerPrimitive, geometryAsCompute.Info.GeometryMaxOutputVertices);
                int totalVerticesCount = totalPrimitivesCount * geometryAsCompute.Info.GeometryMaxOutputVertices * geometryAsCompute.Info.ThreadsPerInputPrimitive;
                int geometryVbDataSize = totalVerticesCount * geometryAsCompute.Reservations.OutputSizeInBytesPerInvocation;
                int geometryIbDataCount = totalVerticesCount + totalPrimitivesCount * maxCompleteStrips;
                int geometryIbDataSize = geometryIbDataCount * sizeof(uint);

                (_geometryVertexDataOffset, _geometryVertexDataSize) = vacContext.GetGeometryVertexDataBuffer(geometryVbDataSize);
                (_geometryIndexDataOffset, _geometryIndexDataSize) = vacContext.GetGeometryIndexDataBuffer(geometryIbDataSize);

                _geometryIndexDataCount = geometryIbDataCount;
            }
        }

        /// <summary>
        /// Emulates the vertex stage using compute.
        /// </summary>
        public readonly void RunVertex()
        {
            _context.Renderer.Pipeline.SetProgram(_vertexAsCompute.HostProgram);

            int primitivesCount = VtgAsComputeContext.GetPrimitivesCount(_topology, _count);

            _vacContext.VertexInfoBufferUpdater.SetVertexCounts(_count, _instanceCount, _firstVertex, _firstInstance);
            _vacContext.VertexInfoBufferUpdater.SetGeometryCounts(primitivesCount);

            for (int index = 0; index < Constants.TotalVertexAttribs; index++)
            {
                var vertexAttrib = _state.State.VertexAttribState[index];

                if (!FormatTable.TryGetSingleComponentAttribFormat(vertexAttrib.UnpackFormat(), out Format format, out int componentsCount))
                {
                    Logger.Debug?.Print(LogClass.Gpu, $"Invalid attribute format 0x{vertexAttrib.UnpackFormat():X}.");

                    format = vertexAttrib.UnpackType() switch
                    {
                        VertexAttribType.Sint => Format.R32Sint,
                        VertexAttribType.Uint => Format.R32Uint,
                        _ => Format.R32Float
                    };

                    componentsCount = 4;
                }

                if (vertexAttrib.UnpackIsConstant())
                {
                    _vacContext.VertexInfoBufferUpdater.SetVertexStride(index, 0, componentsCount);
                    _vacContext.VertexInfoBufferUpdater.SetVertexOffset(index, 0, 0);
                    continue;
                }

                int bufferIndex = vertexAttrib.UnpackBufferIndex();

                GpuVa endAddress = _state.State.VertexBufferEndAddress[bufferIndex];
                var vertexBuffer = _state.State.VertexBufferState[bufferIndex];
                bool instanced = _state.State.VertexBufferInstanced[bufferIndex];

                ulong address = vertexBuffer.Address.Pack();

                if (!vertexBuffer.UnpackEnable() || !_channel.MemoryManager.IsMapped(address))
                {
                    _vacContext.VertexInfoBufferUpdater.SetVertexStride(index, 0, componentsCount);
                    _vacContext.VertexInfoBufferUpdater.SetVertexOffset(index, 0, 0);
                    continue;
                }

                int vbStride = vertexBuffer.UnpackStride();
                ulong vbSize = GetVertexBufferSize(address, endAddress.Pack(), vbStride, _indexed, instanced, _firstVertex, _count);

                ulong attributeOffset = (ulong)vertexAttrib.UnpackOffset();
                int componentSize = format.GetScalarSize();

                address += attributeOffset;

                ulong misalign = address & ((ulong)_context.Capabilities.TextureBufferOffsetAlignment - 1);

                vbSize = Align(vbSize - attributeOffset + misalign, componentSize);

                SetBufferTexture(_vertexAsCompute.Reservations, index, format, address - misalign, vbSize);

                _vacContext.VertexInfoBufferUpdater.SetVertexStride(index, vbStride / componentSize, componentsCount);
                _vacContext.VertexInfoBufferUpdater.SetVertexOffset(index, (int)misalign / componentSize, instanced ? vertexBuffer.Divisor : 0);
            }

            if (_indexed)
            {
                SetIndexBufferTexture(_vertexAsCompute.Reservations, _firstIndex, _count, out int ibOffset);
                _vacContext.VertexInfoBufferUpdater.SetIndexBufferOffset(ibOffset);
            }
            else
            {
                SetSequentialIndexBufferTexture(_vertexAsCompute.Reservations, _count);
                _vacContext.VertexInfoBufferUpdater.SetIndexBufferOffset(0);
            }

            int vertexInfoBinding = _vertexAsCompute.Reservations.VertexInfoConstantBufferBinding;
            BufferRange vertexInfoRange = new(_vacContext.VertexInfoBufferUpdater.Handle, 0, VertexInfoBuffer.RequiredSize);
            _context.Renderer.Pipeline.SetUniformBuffers(stackalloc[] { new BufferAssignment(vertexInfoBinding, vertexInfoRange) });

            int vertexDataBinding = _vertexAsCompute.Reservations.VertexOutputStorageBufferBinding;
            BufferRange vertexDataRange = _vacContext.GetVertexDataBufferRange(_vertexDataOffset, _vertexDataSize, write: true);
            _context.Renderer.Pipeline.SetStorageBuffers(stackalloc[] { new BufferAssignment(vertexDataBinding, vertexDataRange) });

            _vacContext.VertexInfoBufferUpdater.Commit();

            _context.Renderer.Pipeline.DispatchCompute(
                BitUtils.DivRoundUp(_count, ComputeLocalSize),
                BitUtils.DivRoundUp(_instanceCount, ComputeLocalSize),
                1);
        }

        /// <summary>
        /// Emulates the geometry stage using compute, if it exists, otherwise does nothing.
        /// </summary>
        public readonly void RunGeometry()
        {
            if (_geometryAsCompute == null)
            {
                return;
            }

            int primitivesCount = VtgAsComputeContext.GetPrimitivesCount(_topology, _count);

            _vacContext.VertexInfoBufferUpdater.SetVertexCounts(_count, _instanceCount, _firstVertex, _firstInstance);
            _vacContext.VertexInfoBufferUpdater.SetGeometryCounts(primitivesCount);
            _vacContext.VertexInfoBufferUpdater.Commit();

            int vertexInfoBinding = _vertexAsCompute.Reservations.VertexInfoConstantBufferBinding;
            BufferRange vertexInfoRange = new(_vacContext.VertexInfoBufferUpdater.Handle, 0, VertexInfoBuffer.RequiredSize);
            _context.Renderer.Pipeline.SetUniformBuffers(stackalloc[] { new BufferAssignment(vertexInfoBinding, vertexInfoRange) });

            int vertexDataBinding = _vertexAsCompute.Reservations.VertexOutputStorageBufferBinding;

            // Wait until compute is done.
            // TODO: Batch compute and draw operations to avoid pipeline stalls.
            _context.Renderer.Pipeline.Barrier();
            _context.Renderer.Pipeline.SetProgram(_geometryAsCompute.HostProgram);

            SetTopologyRemapBufferTexture(_geometryAsCompute.Reservations, _topology, _count);

            int geometryVbBinding = _geometryAsCompute.Reservations.GeometryVertexOutputStorageBufferBinding;
            int geometryIbBinding = _geometryAsCompute.Reservations.GeometryIndexOutputStorageBufferBinding;

            BufferRange vertexDataRange = _vacContext.GetVertexDataBufferRange(_vertexDataOffset, _vertexDataSize, write: false);
            BufferRange vertexBuffer = _vacContext.GetGeometryVertexDataBufferRange(_geometryVertexDataOffset, _geometryVertexDataSize, write: true);
            BufferRange indexBuffer = _vacContext.GetGeometryIndexDataBufferRange(_geometryIndexDataOffset, _geometryIndexDataSize, write: true);

            _context.Renderer.Pipeline.SetStorageBuffers(stackalloc[]
            {
                new BufferAssignment(vertexDataBinding, vertexDataRange),
                new BufferAssignment(geometryVbBinding, vertexBuffer),
                new BufferAssignment(geometryIbBinding, indexBuffer),
            });

            _context.Renderer.Pipeline.DispatchCompute(
                BitUtils.DivRoundUp(primitivesCount, ComputeLocalSize),
                BitUtils.DivRoundUp(_instanceCount, ComputeLocalSize),
                _geometryAsCompute.Info.ThreadsPerInputPrimitive);
        }

        /// <summary>
        /// Performs a draw using the data produced on the vertex, tessellation and geometry stages,
        /// if rasterizer discard is disabled.
        /// </summary>
        public readonly void RunFragment()
        {
            bool tfEnabled = _state.State.TfEnable;

            if (!_state.State.RasterizeEnable && (!tfEnabled || !_context.Capabilities.SupportsTransformFeedback))
            {
                // No need to run fragment if rasterizer discard is enabled,
                // and we are emulating transform feedback or transform feedback is disabled.

                // Note: We might skip geometry shader here, but right now, this is fine,
                // because the only cases that triggers VTG to compute are geometry shader
                // being not supported, or the vertex pipeline doing store operations.
                // If the geometry shader does not do any store and rasterizer discard is enabled, the geometry shader can be skipped.
                // If the geometry shader does have stores, it would have been converted to compute too if stores are not supported.

                return;
            }

            int vertexDataBinding = _vertexAsCompute.Reservations.VertexOutputStorageBufferBinding;

            _context.Renderer.Pipeline.Barrier();

            _vacContext.VertexInfoBufferUpdater.SetVertexCounts(_count, _instanceCount, _firstVertex, _firstInstance);
            _vacContext.VertexInfoBufferUpdater.Commit();

            if (_geometryAsCompute != null)
            {
                BufferRange vertexBuffer = _vacContext.GetGeometryVertexDataBufferRange(_geometryVertexDataOffset, _geometryVertexDataSize, write: false);
                BufferRange indexBuffer = _vacContext.GetGeometryIndexDataBufferRange(_geometryIndexDataOffset, _geometryIndexDataSize, write: false);

                _context.Renderer.Pipeline.SetProgram(_vertexPassthroughProgram);
                _context.Renderer.Pipeline.SetIndexBuffer(indexBuffer, IndexType.UInt);
                _context.Renderer.Pipeline.SetStorageBuffers(stackalloc[] { new BufferAssignment(vertexDataBinding, vertexBuffer) });

                _context.Renderer.Pipeline.SetPrimitiveRestart(true, -1);
                _context.Renderer.Pipeline.SetPrimitiveTopology(GetGeometryOutputTopology(_geometryAsCompute.Info.GeometryVerticesPerPrimitive));

                _context.Renderer.Pipeline.DrawIndexed(_geometryIndexDataCount, 1, 0, 0, 0);

                _engine.ForceStateDirtyByIndex(StateUpdater.IndexBufferStateIndex);
                _engine.ForceStateDirtyByIndex(StateUpdater.PrimitiveRestartStateIndex);
            }
            else
            {
                BufferRange vertexDataRange = _vacContext.GetVertexDataBufferRange(_vertexDataOffset, _vertexDataSize, write: false);

                _context.Renderer.Pipeline.SetProgram(_vertexPassthroughProgram);
                _context.Renderer.Pipeline.SetStorageBuffers(stackalloc[] { new BufferAssignment(vertexDataBinding, vertexDataRange) });
                _context.Renderer.Pipeline.Draw(_count, _instanceCount, 0, 0);
            }
        }

        /// <summary>
        /// Gets a strip primitive topology from the vertices per primitive count.
        /// </summary>
        /// <param name="verticesPerPrimitive">Vertices per primitive count</param>
        /// <returns>Primitive topology</returns>
        private static PrimitiveTopology GetGeometryOutputTopology(int verticesPerPrimitive)
        {
            return verticesPerPrimitive switch
            {
                3 => PrimitiveTopology.TriangleStrip,
                2 => PrimitiveTopology.LineStrip,
                _ => PrimitiveTopology.Points,
            };
        }

        /// <summary>
        /// Gets the maximum number of complete primitive strips for a vertex count.
        /// </summary>
        /// <param name="verticesPerPrimitive">Vertices per primitive count</param>
        /// <param name="maxOutputVertices">Maximum geometry shader output vertices count</param>
        /// <returns>Maximum number of complete primitive strips</returns>
        private static int GetMaxCompleteStrips(int verticesPerPrimitive, int maxOutputVertices)
        {
            return maxOutputVertices / verticesPerPrimitive;
        }

        /// <summary>
        /// Binds a vertex buffer into a buffer texture.
        /// </summary>
        /// <param name="reservations">Shader resource binding reservations</param>
        /// <param name="index">Buffer texture index</param>
        /// <param name="format">Buffer texture format</param>
        /// <param name="address">Address of the vertex buffer</param>
        /// <param name="size">Size of the buffer in bytes</param>
        private readonly void SetBufferTexture(ResourceReservations reservations, int index, Format format, ulong address, ulong size)
        {
            var memoryManager = _channel.MemoryManager;

            BufferRange range = memoryManager.Physical.BufferCache.GetBufferRange(memoryManager.GetPhysicalRegions(address, size), BufferStage.VertexBuffer);

            ITexture bufferTexture = _vacContext.EnsureBufferTexture(index + 2, format);
            bufferTexture.SetStorage(range);

            _context.Renderer.Pipeline.SetTextureAndSampler(ShaderStage.Compute, reservations.GetVertexBufferTextureBinding(index), bufferTexture, null);
        }

        /// <summary>
        /// Binds the index buffer into a buffer texture.
        /// </summary>
        /// <param name="reservations">Shader resource binding reservations</param>
        /// <param name="firstIndex">First index of the index buffer</param>
        /// <param name="count">Index count</param>
        /// <param name="misalignedOffset">Offset that should be added when accessing the buffer texture on the shader</param>
        private readonly void SetIndexBufferTexture(ResourceReservations reservations, int firstIndex, int count, out int misalignedOffset)
        {
            ulong address = _state.State.IndexBufferState.Address.Pack();
            ulong indexOffset = (ulong)firstIndex;
            ulong size = (ulong)count;

            int shift = 0;
            Format format = Format.R8Uint;

            switch (_state.State.IndexBufferState.Type)
            {
                case IndexType.UShort:
                    shift = 1;
                    format = Format.R16Uint;
                    break;
                case IndexType.UInt:
                    shift = 2;
                    format = Format.R32Uint;
                    break;
            }

            indexOffset <<= shift;
            size <<= shift;

            var memoryManager = _channel.MemoryManager;

            ulong misalign = address & ((ulong)_context.Capabilities.TextureBufferOffsetAlignment - 1);
            BufferRange range = memoryManager.Physical.BufferCache.GetBufferRange(
                memoryManager.GetPhysicalRegions(address + indexOffset - misalign, size + misalign),
                BufferStage.IndexBuffer);
            misalignedOffset = (int)misalign >> shift;

            SetIndexBufferTexture(reservations, range, format);
        }

        /// <summary>
        /// Sets the host buffer texture for the index buffer.
        /// </summary>
        /// <param name="reservations">Shader resource binding reservations</param>
        /// <param name="range">Index buffer range</param>
        /// <param name="format">Index buffer format</param>
        private readonly void SetIndexBufferTexture(ResourceReservations reservations, BufferRange range, Format format)
        {
            ITexture bufferTexture = _vacContext.EnsureBufferTexture(0, format);
            bufferTexture.SetStorage(range);

            _context.Renderer.Pipeline.SetTextureAndSampler(ShaderStage.Compute, reservations.IndexBufferTextureBinding, bufferTexture, null);
        }

        /// <summary>
        /// Sets the host buffer texture for the topology remap buffer.
        /// </summary>
        /// <param name="reservations">Shader resource binding reservations</param>
        /// <param name="topology">Input topology</param>
        /// <param name="count">Input vertex count</param>
        private readonly void SetTopologyRemapBufferTexture(ResourceReservations reservations, PrimitiveTopology topology, int count)
        {
            ITexture bufferTexture = _vacContext.EnsureBufferTexture(1, Format.R32Uint);
            bufferTexture.SetStorage(_vacContext.GetOrCreateTopologyRemapBuffer(topology, count));

            _context.Renderer.Pipeline.SetTextureAndSampler(ShaderStage.Compute, reservations.TopologyRemapBufferTextureBinding, bufferTexture, null);
        }

        /// <summary>
        /// Sets the host buffer texture to a generated sequential index buffer.
        /// </summary>
        /// <param name="reservations">Shader resource binding reservations</param>
        /// <param name="count">Vertex count</param>
        private readonly void SetSequentialIndexBufferTexture(ResourceReservations reservations, int count)
        {
            BufferHandle sequentialIndexBuffer = _vacContext.GetSequentialIndexBuffer(count);

            ITexture bufferTexture = _vacContext.EnsureBufferTexture(0, Format.R32Uint);
            bufferTexture.SetStorage(new BufferRange(sequentialIndexBuffer, 0, count * sizeof(uint)));

            _context.Renderer.Pipeline.SetTextureAndSampler(ShaderStage.Compute, reservations.IndexBufferTextureBinding, bufferTexture, null);
        }

        /// <summary>
        /// Gets the size of a vertex buffer based on the current 3D engine state.
        /// </summary>
        /// <param name="vbAddress">Vertex buffer address</param>
        /// <param name="vbEndAddress">Vertex buffer end address (exclusive)</param>
        /// <param name="vbStride">Vertex buffer stride</param>
        /// <param name="indexed">Whether the draw is indexed</param>
        /// <param name="instanced">Whether the draw is instanced</param>
        /// <param name="firstVertex">First vertex index</param>
        /// <param name="vertexCount">Vertex count</param>
        /// <returns>Size of the vertex buffer, in bytes</returns>
        private readonly ulong GetVertexBufferSize(ulong vbAddress, ulong vbEndAddress, int vbStride, bool indexed, bool instanced, int firstVertex, int vertexCount)
        {
            IndexType indexType = _state.State.IndexBufferState.Type;
            bool indexTypeSmall = indexType == IndexType.UByte || indexType == IndexType.UShort;
            ulong vbSize = vbEndAddress - vbAddress + 1;
            ulong size;

            if (indexed || vbStride == 0 || instanced)
            {
                // This size may be (much) larger than the real vertex buffer size.
                // Avoid calculating it this way, unless we don't have any other option.

                size = vbSize;

                if (vbStride > 0 && indexTypeSmall && indexed && !instanced)
                {
                    // If the index type is a small integer type, then we might be still able
                    // to reduce the vertex buffer size based on the maximum possible index value.

                    ulong maxVertexBufferSize = indexType == IndexType.UByte ? 0x100UL : 0x10000UL;

                    maxVertexBufferSize += _state.State.FirstVertex;
                    maxVertexBufferSize *= (uint)vbStride;

                    size = Math.Min(size, maxVertexBufferSize);
                }
            }
            else
            {
                // For non-indexed draws, we can guess the size from the vertex count
                // and stride.

                int firstInstance = (int)_state.State.FirstInstance;

                size = Math.Min(vbSize, (ulong)((firstInstance + firstVertex + vertexCount) * vbStride));
            }

            return size;
        }

        /// <summary>
        /// Aligns a size to a given alignment value.
        /// </summary>
        /// <param name="size">Size</param>
        /// <param name="alignment">Alignment</param>
        /// <returns>Aligned size</returns>
        private static ulong Align(ulong size, int alignment)
        {
            ulong align = (ulong)alignment;

            size += align - 1;

            size /= align;
            size *= align;

            return size;
        }
    }
}
