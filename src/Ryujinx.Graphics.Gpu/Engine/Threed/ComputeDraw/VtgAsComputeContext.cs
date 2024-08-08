using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Engine.Threed.ComputeDraw
{
    /// <summary>
    /// Vertex, tessellation and geometry as compute shader context.
    /// </summary>
    class VtgAsComputeContext : IDisposable
    {
        private readonly GpuContext _context;

        /// <summary>
        /// Cache of buffer textures used for vertex and index buffers.
        /// </summary>
        private class BufferTextureCache : IDisposable
        {
            private readonly Dictionary<Format, ITexture> _cache;

            /// <summary>
            /// Creates a new instance of the buffer texture cache.
            /// </summary>
            public BufferTextureCache()
            {
                _cache = new();
            }

            /// <summary>
            /// Gets a cached or creates and caches a buffer texture with the specified format.
            /// </summary>
            /// <param name="renderer">Renderer where the texture will be used</param>
            /// <param name="format">Format of the buffer texture</param>
            /// <returns>Buffer texture</returns>
            public ITexture Get(IRenderer renderer, Format format)
            {
                if (!_cache.TryGetValue(format, out ITexture bufferTexture))
                {
                    bufferTexture = renderer.CreateTexture(new TextureCreateInfo(
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        format.GetBytesPerElement(),
                        format,
                        DepthStencilMode.Depth,
                        Target.TextureBuffer,
                        SwizzleComponent.Red,
                        SwizzleComponent.Green,
                        SwizzleComponent.Blue,
                        SwizzleComponent.Alpha));

                    _cache.Add(format, bufferTexture);
                }

                return bufferTexture;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (var texture in _cache.Values)
                    {
                        texture.Release();
                    }

                    _cache.Clear();
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Buffer state.
        /// </summary>
        private struct Buffer
        {
            /// <summary>
            /// Buffer handle.
            /// </summary>
            public BufferHandle Handle;

            /// <summary>
            /// Current free buffer offset.
            /// </summary>
            public int Offset;

            /// <summary>
            /// Total buffer size in bytes.
            /// </summary>
            public int Size;
        }

        /// <summary>
        /// Index buffer state.
        /// </summary>
        private readonly struct IndexBuffer
        {
            /// <summary>
            /// Buffer handle.
            /// </summary>
            public BufferHandle Handle { get; }

            /// <summary>
            /// Index count.
            /// </summary>
            public int Count { get; }

            /// <summary>
            /// Size in bytes.
            /// </summary>
            public int Size { get; }

            /// <summary>
            /// Creates a new index buffer state.
            /// </summary>
            /// <param name="handle">Buffer handle</param>
            /// <param name="count">Index count</param>
            /// <param name="size">Size in bytes</param>
            public IndexBuffer(BufferHandle handle, int count, int size)
            {
                Handle = handle;
                Count = count;
                Size = size;
            }

            /// <summary>
            /// Creates a full range starting from the beggining of the buffer.
            /// </summary>
            /// <returns>Range</returns>
            public readonly BufferRange ToRange()
            {
                return new BufferRange(Handle, 0, Size);
            }

            /// <summary>
            /// Creates a range starting from the beggining of the buffer, with the specified size.
            /// </summary>
            /// <param name="size">Size in bytes of the range</param>
            /// <returns>Range</returns>
            public readonly BufferRange ToRange(int size)
            {
                return new BufferRange(Handle, 0, size);
            }
        }

        private readonly BufferTextureCache[] _bufferTextures;
        private BufferHandle _dummyBuffer;
        private Buffer _vertexDataBuffer;
        private Buffer _geometryVertexDataBuffer;
        private Buffer _geometryIndexDataBuffer;
        private BufferHandle _sequentialIndexBuffer;
        private int _sequentialIndexBufferCount;

        private readonly Dictionary<PrimitiveTopology, IndexBuffer> _topologyRemapBuffers;

        /// <summary>
        /// Vertex information buffer updater.
        /// </summary>
        public VertexInfoBufferUpdater VertexInfoBufferUpdater { get; }

        /// <summary>
        /// Creates a new instance of the vertex, tessellation and geometry as compute shader context.
        /// </summary>
        /// <param name="context"></param>
        public VtgAsComputeContext(GpuContext context)
        {
            _context = context;
            _bufferTextures = new BufferTextureCache[Constants.TotalVertexBuffers + 2];
            _topologyRemapBuffers = new();
            VertexInfoBufferUpdater = new(context.Renderer);
        }

        /// <summary>
        /// Gets the number of complete primitives that can be formed with a given vertex count, for a given topology.
        /// </summary>
        /// <param name="primitiveType">Topology</param>
        /// <param name="count">Vertex count</param>
        /// <returns>Total of complete primitives</returns>
        public static int GetPrimitivesCount(PrimitiveTopology primitiveType, int count)
        {
            return primitiveType switch
            {
                PrimitiveTopology.Lines => count / 2,
                PrimitiveTopology.LinesAdjacency => count / 4,
                PrimitiveTopology.LineLoop => count > 1 ? count : 0,
                PrimitiveTopology.LineStrip => Math.Max(count - 1, 0),
                PrimitiveTopology.LineStripAdjacency => Math.Max(count - 3, 0),
                PrimitiveTopology.Triangles => count / 3,
                PrimitiveTopology.TrianglesAdjacency => count / 6,
                PrimitiveTopology.TriangleStrip or
                PrimitiveTopology.TriangleFan or
                PrimitiveTopology.Polygon => Math.Max(count - 2, 0),
                PrimitiveTopology.TriangleStripAdjacency => Math.Max(count - 2, 0) / 2,
                PrimitiveTopology.Quads => (count / 4) * 2, // In triangles.
                PrimitiveTopology.QuadStrip => Math.Max((count - 2) / 2, 0) * 2, // In triangles.
                _ => count,
            };
        }

        /// <summary>
        /// Gets the total of vertices that a single primitive has, for the specified topology.
        /// </summary>
        /// <param name="primitiveType">Topology</param>
        /// <returns>Vertex count</returns>
        private static int GetVerticesPerPrimitive(PrimitiveTopology primitiveType)
        {
            return primitiveType switch
            {
                PrimitiveTopology.Lines or
                PrimitiveTopology.LineLoop or
                PrimitiveTopology.LineStrip => 2,
                PrimitiveTopology.LinesAdjacency or
                PrimitiveTopology.LineStripAdjacency => 4,
                PrimitiveTopology.Triangles or
                PrimitiveTopology.TriangleStrip or
                PrimitiveTopology.TriangleFan or
                PrimitiveTopology.Polygon => 3,
                PrimitiveTopology.TrianglesAdjacency or
                PrimitiveTopology.TriangleStripAdjacency => 6,
                PrimitiveTopology.Quads or
                PrimitiveTopology.QuadStrip => 3, // 2 triangles.
                _ => 1,
            };
        }

        /// <summary>
        /// Gets a cached or creates a new buffer that can be used to map linear indices to ones
        /// of a specified topology, and build complete primitives.
        /// </summary>
        /// <param name="topology">Topology</param>
        /// <param name="count">Number of input vertices that needs to be mapped using that buffer</param>
        /// <returns>Remap buffer range</returns>
        public BufferRange GetOrCreateTopologyRemapBuffer(PrimitiveTopology topology, int count)
        {
            if (!_topologyRemapBuffers.TryGetValue(topology, out IndexBuffer buffer) || buffer.Count < count)
            {
                if (buffer.Handle != BufferHandle.Null)
                {
                    _context.Renderer.DeleteBuffer(buffer.Handle);
                }

                buffer = CreateTopologyRemapBuffer(topology, count);
                _topologyRemapBuffers[topology] = buffer;

                return buffer.ToRange();
            }

            return buffer.ToRange(Math.Max(GetPrimitivesCount(topology, count) * GetVerticesPerPrimitive(topology), 1) * sizeof(uint));
        }

        /// <summary>
        /// Creates a new topology remap buffer.
        /// </summary>
        /// <param name="topology">Topology</param>
        /// <param name="count">Maximum of vertices that will be accessed</param>
        /// <returns>Remap buffer range</returns>
        private IndexBuffer CreateTopologyRemapBuffer(PrimitiveTopology topology, int count)
        {
            // Size can't be zero as creating zero sized buffers is invalid.
            Span<int> data = new int[Math.Max(GetPrimitivesCount(topology, count) * GetVerticesPerPrimitive(topology), 1)];

            switch (topology)
            {
                case PrimitiveTopology.Points:
                case PrimitiveTopology.Lines:
                case PrimitiveTopology.LinesAdjacency:
                case PrimitiveTopology.Triangles:
                case PrimitiveTopology.TrianglesAdjacency:
                case PrimitiveTopology.Patches:
                    for (int index = 0; index < data.Length; index++)
                    {
                        data[index] = index;
                    }
                    break;
                case PrimitiveTopology.LineLoop:
                    data[^1] = 0;

                    for (int index = 0; index < ((data.Length - 1) & ~1); index += 2)
                    {
                        data[index] = index >> 1;
                        data[index + 1] = (index >> 1) + 1;
                    }
                    break;
                case PrimitiveTopology.LineStrip:
                    for (int index = 0; index < ((data.Length - 1) & ~1); index += 2)
                    {
                        data[index] = index >> 1;
                        data[index + 1] = (index >> 1) + 1;
                    }
                    break;
                case PrimitiveTopology.TriangleStrip:
                    int tsTrianglesCount = data.Length / 3;
                    int tsOutIndex = 3;

                    if (tsTrianglesCount > 0)
                    {
                        data[0] = 0;
                        data[1] = 1;
                        data[2] = 2;
                    }

                    for (int tri = 1; tri < tsTrianglesCount; tri++)
                    {
                        int baseIndex = tri * 3;

                        if ((tri & 1) != 0)
                        {
                            data[baseIndex] = tsOutIndex - 1;
                            data[baseIndex + 1] = tsOutIndex - 2;
                            data[baseIndex + 2] = tsOutIndex++;
                        }
                        else
                        {
                            data[baseIndex] = tsOutIndex - 2;
                            data[baseIndex + 1] = tsOutIndex - 1;
                            data[baseIndex + 2] = tsOutIndex++;
                        }
                    }
                    break;
                case PrimitiveTopology.TriangleFan:
                case PrimitiveTopology.Polygon:
                    int tfTrianglesCount = data.Length / 3;
                    int tfOutIndex = 1;

                    for (int index = 0; index < tfTrianglesCount * 3; index += 3)
                    {
                        data[index] = 0;
                        data[index + 1] = tfOutIndex;
                        data[index + 2] = ++tfOutIndex;
                    }
                    break;
                case PrimitiveTopology.Quads:
                    int qQuadsCount = data.Length / 6;

                    for (int quad = 0; quad < qQuadsCount; quad++)
                    {
                        int index = quad * 6;
                        int qIndex = quad * 4;

                        data[index] = qIndex;
                        data[index + 1] = qIndex + 1;
                        data[index + 2] = qIndex + 2;
                        data[index + 3] = qIndex;
                        data[index + 4] = qIndex + 2;
                        data[index + 5] = qIndex + 3;
                    }
                    break;
                case PrimitiveTopology.QuadStrip:
                    int qsQuadsCount = data.Length / 6;

                    if (qsQuadsCount > 0)
                    {
                        data[0] = 0;
                        data[1] = 1;
                        data[2] = 2;
                        data[3] = 0;
                        data[4] = 2;
                        data[5] = 3;
                    }

                    for (int quad = 1; quad < qsQuadsCount; quad++)
                    {
                        int index = quad * 6;
                        int qIndex = quad * 2;

                        data[index] = qIndex + 1;
                        data[index + 1] = qIndex;
                        data[index + 2] = qIndex + 2;
                        data[index + 3] = qIndex + 1;
                        data[index + 4] = qIndex + 2;
                        data[index + 5] = qIndex + 3;
                    }
                    break;
                case PrimitiveTopology.LineStripAdjacency:
                    for (int index = 0; index < ((data.Length - 3) & ~3); index += 4)
                    {
                        int lIndex = index >> 2;

                        data[index] = lIndex;
                        data[index + 1] = lIndex + 1;
                        data[index + 2] = lIndex + 2;
                        data[index + 3] = lIndex + 3;
                    }
                    break;
                case PrimitiveTopology.TriangleStripAdjacency:
                    int tsaTrianglesCount = data.Length / 6;
                    int tsaOutIndex = 6;

                    if (tsaTrianglesCount > 0)
                    {
                        data[0] = 0;
                        data[1] = 1;
                        data[2] = 2;
                        data[3] = 3;
                        data[4] = 4;
                        data[5] = 5;
                    }

                    for (int tri = 1; tri < tsaTrianglesCount; tri++)
                    {
                        int baseIndex = tri * 6;

                        if ((tri & 1) != 0)
                        {
                            data[baseIndex] = tsaOutIndex - 2;
                            data[baseIndex + 1] = tsaOutIndex - 1;
                            data[baseIndex + 2] = tsaOutIndex - 4;
                            data[baseIndex + 3] = tsaOutIndex - 3;
                            data[baseIndex + 4] = tsaOutIndex++;
                            data[baseIndex + 5] = tsaOutIndex++;
                        }
                        else
                        {
                            data[baseIndex] = tsaOutIndex - 4;
                            data[baseIndex + 1] = tsaOutIndex - 3;
                            data[baseIndex + 2] = tsaOutIndex - 2;
                            data[baseIndex + 3] = tsaOutIndex - 1;
                            data[baseIndex + 4] = tsaOutIndex++;
                            data[baseIndex + 5] = tsaOutIndex++;
                        }
                    }
                    break;
            }

            ReadOnlySpan<byte> dataBytes = MemoryMarshal.Cast<int, byte>(data);

            BufferHandle buffer = _context.Renderer.CreateBuffer(dataBytes.Length, BufferAccess.DeviceMemory);
            _context.Renderer.SetBufferData(buffer, 0, dataBytes);

            return new IndexBuffer(buffer, count, dataBytes.Length);
        }

        /// <summary>
        /// Gets a buffer texture with a given format, for the given index.
        /// </summary>
        /// <param name="index">Index of the buffer texture</param>
        /// <param name="format">Format of the buffer texture</param>
        /// <returns>Buffer texture</returns>
        public ITexture EnsureBufferTexture(int index, Format format)
        {
            return (_bufferTextures[index] ??= new()).Get(_context.Renderer, format);
        }

        /// <summary>
        /// Gets the offset and size of usable storage on the output vertex buffer.
        /// </summary>
        /// <param name="size">Size in bytes that will be used</param>
        /// <returns>Usable offset and size on the buffer</returns>
        public (int, int) GetVertexDataBuffer(int size)
        {
            return EnsureBuffer(ref _vertexDataBuffer, size);
        }

        /// <summary>
        /// Gets the offset and size of usable storage on the output geometry shader vertex buffer.
        /// </summary>
        /// <param name="size">Size in bytes that will be used</param>
        /// <returns>Usable offset and size on the buffer</returns>
        public (int, int) GetGeometryVertexDataBuffer(int size)
        {
            return EnsureBuffer(ref _geometryVertexDataBuffer, size);
        }

        /// <summary>
        /// Gets the offset and size of usable storage on the output geometry shader index buffer.
        /// </summary>
        /// <param name="size">Size in bytes that will be used</param>
        /// <returns>Usable offset and size on the buffer</returns>
        public (int, int) GetGeometryIndexDataBuffer(int size)
        {
            return EnsureBuffer(ref _geometryIndexDataBuffer, size);
        }

        /// <summary>
        /// Gets a range of the output vertex buffer for binding.
        /// </summary>
        /// <param name="offset">Offset of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <param name="write">Indicates if the buffer contents will be modified</param>
        /// <returns>Range</returns>
        public BufferRange GetVertexDataBufferRange(int offset, int size, bool write)
        {
            return new BufferRange(_vertexDataBuffer.Handle, offset, size, write);
        }

        /// <summary>
        /// Gets a range of the output geometry shader vertex buffer for binding.
        /// </summary>
        /// <param name="offset">Offset of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <param name="write">Indicates if the buffer contents will be modified</param>
        /// <returns>Range</returns>
        public BufferRange GetGeometryVertexDataBufferRange(int offset, int size, bool write)
        {
            return new BufferRange(_geometryVertexDataBuffer.Handle, offset, size, write);
        }

        /// <summary>
        /// Gets a range of the output geometry shader index buffer for binding.
        /// </summary>
        /// <param name="offset">Offset of the range</param>
        /// <param name="size">Size of the range in bytes</param>
        /// <param name="write">Indicates if the buffer contents will be modified</param>
        /// <returns>Range</returns>
        public BufferRange GetGeometryIndexDataBufferRange(int offset, int size, bool write)
        {
            return new BufferRange(_geometryIndexDataBuffer.Handle, offset, size, write);
        }

        /// <summary>
        /// Gets the range for a sequential index buffer, with ever incrementing index values.
        /// </summary>
        /// <param name="count">Minimum number of indices that the buffer should have</param>
        /// <returns>Buffer handle</returns>
        public BufferHandle GetSequentialIndexBuffer(int count)
        {
            if (_sequentialIndexBufferCount < count)
            {
                if (_sequentialIndexBuffer != BufferHandle.Null)
                {
                    _context.Renderer.DeleteBuffer(_sequentialIndexBuffer);
                }

                _sequentialIndexBuffer = _context.Renderer.CreateBuffer(count * sizeof(uint), BufferAccess.DeviceMemory);
                _sequentialIndexBufferCount = count;

                Span<int> data = new int[count];

                for (int index = 0; index < count; index++)
                {
                    data[index] = index;
                }

                _context.Renderer.SetBufferData(_sequentialIndexBuffer, 0, MemoryMarshal.Cast<int, byte>(data));
            }

            return _sequentialIndexBuffer;
        }

        /// <summary>
        /// Ensure that a buffer exists, is large enough, and allocates a sub-region of the specified size inside the buffer.
        /// </summary>
        /// <param name="buffer">Buffer state</param>
        /// <param name="size">Required size in bytes</param>
        /// <returns>Allocated offset and size</returns>
        private (int, int) EnsureBuffer(ref Buffer buffer, int size)
        {
            int newSize = buffer.Offset + size;

            if (buffer.Size < newSize)
            {
                if (buffer.Handle != BufferHandle.Null)
                {
                    _context.Renderer.DeleteBuffer(buffer.Handle);
                }

                buffer.Handle = _context.Renderer.CreateBuffer(newSize, BufferAccess.DeviceMemory);
                buffer.Size = newSize;
            }

            int offset = buffer.Offset;

            buffer.Offset = BitUtils.AlignUp(newSize, _context.Capabilities.StorageBufferOffsetAlignment);

            return (offset, size);
        }

        /// <summary>
        /// Frees all buffer sub-regions that were previously allocated.
        /// </summary>
        public void FreeBuffers()
        {
            _vertexDataBuffer.Offset = 0;
            _geometryVertexDataBuffer.Offset = 0;
            _geometryIndexDataBuffer.Offset = 0;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int index = 0; index < _bufferTextures.Length; index++)
                {
                    _bufferTextures[index]?.Dispose();
                    _bufferTextures[index] = null;
                }

                DestroyIfNotNull(ref _dummyBuffer);
                DestroyIfNotNull(ref _vertexDataBuffer.Handle);
                DestroyIfNotNull(ref _geometryVertexDataBuffer.Handle);
                DestroyIfNotNull(ref _geometryIndexDataBuffer.Handle);
                DestroyIfNotNull(ref _sequentialIndexBuffer);

                foreach (var indexBuffer in _topologyRemapBuffers.Values)
                {
                    _context.Renderer.DeleteBuffer(indexBuffer.Handle);
                }

                _topologyRemapBuffers.Clear();
            }
        }

        /// <summary>
        /// Deletes a buffer if the handle is valid (not null), then sets the handle to null.
        /// </summary>
        /// <param name="handle">Buffer handle</param>
        private void DestroyIfNotNull(ref BufferHandle handle)
        {
            if (handle != BufferHandle.Null)
            {
                _context.Renderer.DeleteBuffer(handle);
                handle = BufferHandle.Null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
