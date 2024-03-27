using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Graphics.OpenGL.Queries
{
    class BufferedQuery : IDisposable
    {
        private const int MaxQueryRetries = 5000;
        private const long DefaultValue = -1;
        private const ulong HighMask = 0xFFFFFFFF00000000;

        public int Query { get; }

        private readonly int _buffer;
        private readonly IntPtr _bufferMap;
        private readonly QueryTarget _type;

        private readonly Counters _parent;

        public BufferedQuery(Counters parent, QueryTarget type)
        {
            _parent = parent;
            _buffer = GL.GenBuffer();
            Query = GL.GenQuery();
            _type = type;

            GL.BindBuffer(BufferTarget.QueryBuffer, _buffer);

            unsafe
            {
                long defaultValue = DefaultValue;
                GL.BufferStorage(BufferTarget.QueryBuffer, sizeof(long), (IntPtr)(&defaultValue), BufferStorageFlags.MapReadBit | BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit);
            }
            _bufferMap = GL.MapBufferRange(BufferTarget.QueryBuffer, IntPtr.Zero, sizeof(long), BufferAccessMask.MapReadBit | BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit);
        }

        public void Reset()
        {
            GL.EndQuery(_type);
            GL.BeginQuery(_type, Query);
        }

        public void Begin()
        {
            Marshal.WriteInt64(_bufferMap, -1L);
            GL.BeginQuery(_type, Query);
        }

        public unsafe void CopyQueryResult(bool withBarrier)
        {
            GL.BindBuffer(BufferTarget.QueryBuffer, _buffer);
            GL.GetQueryObject(Query, GetQueryObjectParam.QueryResult, (long*)0);

            if (withBarrier)
            {
                GL.MemoryBarrier(MemoryBarrierFlags.QueryBufferBarrierBit | MemoryBarrierFlags.ClientMappedBufferBarrierBit);
            }
        }

        public void End(bool withResult)
        {
            GL.EndQuery(_type);

            if (withResult)
            {
                if (!_parent.QueueCopy(this))
                {
                    CopyQueryResult(true);
                }
            }
            else
            {
                // Dummy result, just return 0.
                Marshal.WriteInt64(_bufferMap, 0L);
            }
        }

        private static bool WaitingForValue(long data)
        {
            return data == DefaultValue ||
                ((ulong)data & HighMask) == (unchecked((ulong)DefaultValue) & HighMask);
        }

        public bool TryGetResult(out long result)
        {
            result = Marshal.ReadInt64(_bufferMap);

            return !WaitingForValue(result);
        }

        public long AwaitResult(AutoResetEvent wakeSignal = null)
        {
            long data = DefaultValue;

            if (wakeSignal == null)
            {
                while (WaitingForValue(data))
                {
                    data = Marshal.ReadInt64(_bufferMap);
                }
            }
            else
            {
                int iterations = 0;
                while (WaitingForValue(data) && iterations++ < MaxQueryRetries)
                {
                    data = Marshal.ReadInt64(_bufferMap);
                    if (WaitingForValue(data))
                    {
                        wakeSignal.WaitOne(1);
                    }
                }

                if (iterations >= MaxQueryRetries)
                {
                    Logger.Error?.Print(LogClass.Gpu, $"Error: Query result timed out. Took more than {MaxQueryRetries} tries.");
                }
            }

            return data;
        }

        public void Dispose()
        {
            GL.BindBuffer(BufferTarget.QueryBuffer, _buffer);
            GL.UnmapBuffer(BufferTarget.QueryBuffer);
            GL.DeleteBuffer(_buffer);
            GL.DeleteQuery(Query);
        }
    }
}
