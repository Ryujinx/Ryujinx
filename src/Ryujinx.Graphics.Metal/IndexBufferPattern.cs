using Ryujinx.Graphics.GAL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    internal class IndexBufferPattern : IDisposable
    {
        public int PrimitiveVertices { get; }
        public int PrimitiveVerticesOut { get; }
        public int BaseIndex { get; }
        public int[] OffsetIndex { get; }
        public int IndexStride { get; }
        public bool RepeatStart { get; }

        private readonly MetalRenderer _renderer;
        private int _currentSize;
        private BufferHandle _repeatingBuffer;

        public IndexBufferPattern(MetalRenderer renderer,
            int primitiveVertices,
            int primitiveVerticesOut,
            int baseIndex,
            int[] offsetIndex,
            int indexStride,
            bool repeatStart)
        {
            PrimitiveVertices = primitiveVertices;
            PrimitiveVerticesOut = primitiveVerticesOut;
            BaseIndex = baseIndex;
            OffsetIndex = offsetIndex;
            IndexStride = indexStride;
            RepeatStart = repeatStart;

            _renderer = renderer;
        }

        public int GetPrimitiveCount(int vertexCount)
        {
            return Math.Max(0, (vertexCount - BaseIndex) / IndexStride);
        }

        public int GetConvertedCount(int indexCount)
        {
            int primitiveCount = GetPrimitiveCount(indexCount);
            return primitiveCount * OffsetIndex.Length;
        }

        public IEnumerable<int> GetIndexMapping(int indexCount)
        {
            int primitiveCount = GetPrimitiveCount(indexCount);
            int index = BaseIndex;

            for (int i = 0; i < primitiveCount; i++)
            {
                if (RepeatStart)
                {
                    // Used for triangle fan
                    yield return 0;
                }

                for (int j = RepeatStart ? 1 : 0; j < OffsetIndex.Length; j++)
                {
                    yield return index + OffsetIndex[j];
                }

                index += IndexStride;
            }
        }

        public BufferHandle GetRepeatingBuffer(int vertexCount, out int indexCount)
        {
            int primitiveCount = GetPrimitiveCount(vertexCount);
            indexCount = primitiveCount * PrimitiveVerticesOut;

            int expectedSize = primitiveCount * OffsetIndex.Length;

            if (expectedSize <= _currentSize && _repeatingBuffer != BufferHandle.Null)
            {
                return _repeatingBuffer;
            }

            // Expand the repeating pattern to the number of requested primitives.
            BufferHandle newBuffer = _renderer.BufferManager.CreateWithHandle(expectedSize * sizeof(int));

            // Copy the old data to the new one.
            if (_repeatingBuffer != BufferHandle.Null)
            {
                _renderer.Pipeline.CopyBuffer(_repeatingBuffer, newBuffer, 0, 0, _currentSize * sizeof(int));
                _renderer.BufferManager.Delete(_repeatingBuffer);
            }

            _repeatingBuffer = newBuffer;

            // Add the additional repeats on top.
            int newPrimitives = primitiveCount;
            int oldPrimitives = (_currentSize) / OffsetIndex.Length;

            int[] newData;

            newPrimitives -= oldPrimitives;
            newData = new int[expectedSize - _currentSize];

            int outOffset = 0;
            int index = oldPrimitives * IndexStride + BaseIndex;

            for (int i = 0; i < newPrimitives; i++)
            {
                if (RepeatStart)
                {
                    // Used for triangle fan
                    newData[outOffset++] = 0;
                }

                for (int j = RepeatStart ? 1 : 0; j < OffsetIndex.Length; j++)
                {
                    newData[outOffset++] = index + OffsetIndex[j];
                }

                index += IndexStride;
            }

            _renderer.SetBufferData(newBuffer, _currentSize * sizeof(int), MemoryMarshal.Cast<int, byte>(newData));
            _currentSize = expectedSize;

            return newBuffer;
        }

        public void Dispose()
        {
            if (_repeatingBuffer != BufferHandle.Null)
            {
                _renderer.BufferManager.Delete(_repeatingBuffer);
                _repeatingBuffer = BufferHandle.Null;
            }
        }
    }
}
