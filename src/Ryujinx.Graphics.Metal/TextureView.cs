using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.Metal
{
    class TextureView : ITexture, IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public float ScaleFactor { get; }
        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            throw new NotImplementedException();
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData()
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            throw new NotImplementedException();
        }

        public void SetData(SpanOrArray<byte> data)
        {
            throw new NotImplementedException();
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level)
        {
            throw new NotImplementedException();
        }

        public void SetData(SpanOrArray<byte> data, int layer, int level, Rectangle<int> region)
        {
            throw new NotImplementedException();
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotImplementedException();
        }

        public void Release()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}