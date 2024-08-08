using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    readonly struct ScopedTemporaryBuffer : IDisposable
    {
        private readonly BufferManager _bufferManager;
        private readonly bool _isReserved;

        public readonly BufferRange Range;
        public readonly BufferHolder Holder;

        public BufferHandle Handle => Range.Handle;
        public int Offset => Range.Offset;

        public ScopedTemporaryBuffer(BufferManager bufferManager, BufferHolder holder, BufferHandle handle, int offset, int size, bool isReserved)
        {
            _bufferManager = bufferManager;

            Range = new BufferRange(handle, offset, size);
            Holder = holder;

            _isReserved = isReserved;
        }

        public void Dispose()
        {
            if (!_isReserved)
            {
                _bufferManager.Delete(Range.Handle);
            }
        }
    }

    [SupportedOSPlatform("macos")]
    class BufferManager : IDisposable
    {
        private readonly IdList<BufferHolder> _buffers;

        private readonly MTLDevice _device;
        private readonly MetalRenderer _renderer;
        private readonly Pipeline _pipeline;

        public int BufferCount { get; private set; }

        public StagingBuffer StagingBuffer { get; }

        public BufferManager(MTLDevice device, MetalRenderer renderer, Pipeline pipeline)
        {
            _device = device;
            _renderer = renderer;
            _pipeline = pipeline;
            _buffers = new IdList<BufferHolder>();

            StagingBuffer = new StagingBuffer(_renderer, this);
        }

        public BufferHandle Create(nint pointer, int size)
        {
            // TODO: This is the wrong Metal method, we need no-copy which SharpMetal isn't giving us.
            var buffer = _device.NewBuffer(pointer, (ulong)size, MTLResourceOptions.ResourceStorageModeShared);

            if (buffer == IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create buffer with size 0x{size:X}, and pointer 0x{pointer:X}.");

                return BufferHandle.Null;
            }

            var holder = new BufferHolder(_renderer, _pipeline, buffer, size);

            BufferCount++;

            ulong handle64 = (uint)_buffers.Add(holder);

            return Unsafe.As<ulong, BufferHandle>(ref handle64);
        }

        public BufferHandle CreateWithHandle(int size)
        {
            return CreateWithHandle(size, out _);
        }

        public BufferHandle CreateWithHandle(int size, out BufferHolder holder)
        {
            holder = Create(size);

            if (holder == null)
            {
                return BufferHandle.Null;
            }

            BufferCount++;

            ulong handle64 = (uint)_buffers.Add(holder);

            return Unsafe.As<ulong, BufferHandle>(ref handle64);
        }

        public ScopedTemporaryBuffer ReserveOrCreate(CommandBufferScoped cbs, int size)
        {
            StagingBufferReserved? result = StagingBuffer.TryReserveData(cbs, size);

            if (result.HasValue)
            {
                return new ScopedTemporaryBuffer(this, result.Value.Buffer, StagingBuffer.Handle, result.Value.Offset, result.Value.Size, true);
            }
            else
            {
                // Create a temporary buffer.
                BufferHandle handle = CreateWithHandle(size, out BufferHolder holder);

                return new ScopedTemporaryBuffer(this, holder, handle, 0, size, false);
            }
        }

        public BufferHolder Create(int size)
        {
            var buffer = _device.NewBuffer((ulong)size, MTLResourceOptions.ResourceStorageModeShared);

            if (buffer != IntPtr.Zero)
            {
                return new BufferHolder(_renderer, _pipeline, buffer, size);
            }

            Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create buffer with size 0x{size:X}.");

            return null;
        }

        public Auto<DisposableBuffer> GetBuffer(BufferHandle handle, bool isWrite, out int size)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                size = holder.Size;
                return holder.GetBuffer(isWrite);
            }

            size = 0;
            return null;
        }

        public Auto<DisposableBuffer> GetBuffer(BufferHandle handle, int offset, int size, bool isWrite)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                return holder.GetBuffer(offset, size, isWrite);
            }

            return null;
        }

        public Auto<DisposableBuffer> GetBuffer(BufferHandle handle, bool isWrite)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                return holder.GetBuffer(isWrite);
            }

            return null;
        }

        public Auto<DisposableBuffer> GetBufferI8ToI16(CommandBufferScoped cbs, BufferHandle handle, int offset, int size)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                return holder.GetBufferI8ToI16(cbs, offset, size);
            }

            return null;
        }

        public PinnedSpan<byte> GetData(BufferHandle handle, int offset, int size)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                return holder.GetData(offset, size);
            }

            return new PinnedSpan<byte>();
        }

        public void SetData<T>(BufferHandle handle, int offset, ReadOnlySpan<T> data) where T : unmanaged
        {
            SetData(handle, offset, MemoryMarshal.Cast<T, byte>(data), null);
        }

        public void SetData(BufferHandle handle, int offset, ReadOnlySpan<byte> data, CommandBufferScoped? cbs)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                holder.SetData(offset, data, cbs);
            }
        }

        public void Delete(BufferHandle handle)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                holder.Dispose();
                _buffers.Remove((int)Unsafe.As<BufferHandle, ulong>(ref handle));
            }
        }

        private bool TryGetBuffer(BufferHandle handle, out BufferHolder holder)
        {
            return _buffers.TryGetValue((int)Unsafe.As<BufferHandle, ulong>(ref handle), out holder);
        }

        public void Dispose()
        {
            StagingBuffer.Dispose();

            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }
        }
    }
}
