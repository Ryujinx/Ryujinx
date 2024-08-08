using Ryujinx.Graphics.GAL;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    internal class PersistentFlushBuffer : IDisposable
    {
        private readonly MetalRenderer _renderer;

        private BufferHolder _flushStorage;

        public PersistentFlushBuffer(MetalRenderer renderer)
        {
            _renderer = renderer;
        }

        private BufferHolder ResizeIfNeeded(int size)
        {
            var flushStorage = _flushStorage;

            if (flushStorage == null || size > _flushStorage.Size)
            {
                flushStorage?.Dispose();

                flushStorage = _renderer.BufferManager.Create(size);
                _flushStorage = flushStorage;
            }

            return flushStorage;
        }

        public Span<byte> GetBufferData(CommandBufferPool cbp, BufferHolder buffer, int offset, int size)
        {
            var flushStorage = ResizeIfNeeded(size);
            Auto<DisposableBuffer> srcBuffer;

            using (var cbs = cbp.Rent())
            {
                srcBuffer = buffer.GetBuffer();
                var dstBuffer = flushStorage.GetBuffer();

                if (srcBuffer.TryIncrementReferenceCount())
                {
                    BufferHolder.Copy(cbs, srcBuffer, dstBuffer, offset, 0, size, registerSrcUsage: false);
                }
                else
                {
                    // Source buffer is no longer alive, don't copy anything to flush storage.
                    srcBuffer = null;
                }
            }

            flushStorage.WaitForFences();
            srcBuffer?.DecrementReferenceCount();
            return flushStorage.GetDataStorage(0, size);
        }

        public Span<byte> GetTextureData(CommandBufferPool cbp, Texture view, int size)
        {
            TextureCreateInfo info = view.Info;

            var flushStorage = ResizeIfNeeded(size);

            using (var cbs = cbp.Rent())
            {
                var buffer = flushStorage.GetBuffer().Get(cbs).Value;
                var image = view.GetHandle();

                view.CopyFromOrToBuffer(cbs, buffer, image, size, true, 0, 0, info.GetLayers(), info.Levels, singleSlice: false);
            }

            flushStorage.WaitForFences();
            return flushStorage.GetDataStorage(0, size);
        }

        public Span<byte> GetTextureData(CommandBufferPool cbp, Texture view, int size, int layer, int level)
        {
            var flushStorage = ResizeIfNeeded(size);

            using (var cbs = cbp.Rent())
            {
                var buffer = flushStorage.GetBuffer().Get(cbs).Value;
                var image = view.GetHandle();

                view.CopyFromOrToBuffer(cbs, buffer, image, size, true, layer, level, 1, 1, singleSlice: true);
            }

            flushStorage.WaitForFences();
            return flushStorage.GetDataStorage(0, size);
        }

        public void Dispose()
        {
            _flushStorage.Dispose();
        }
    }
}
