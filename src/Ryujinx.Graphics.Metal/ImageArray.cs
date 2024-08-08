using Ryujinx.Graphics.GAL;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    internal class ImageArray : IImageArray
    {
        private readonly TextureRef[] _textureRefs;
        private readonly TextureBuffer[] _bufferTextureRefs;

        private readonly bool _isBuffer;
        private readonly Pipeline _pipeline;

        public ImageArray(int size, bool isBuffer, Pipeline pipeline)
        {
            if (isBuffer)
            {
                _bufferTextureRefs = new TextureBuffer[size];
            }
            else
            {
                _textureRefs = new TextureRef[size];
            }

            _isBuffer = isBuffer;
            _pipeline = pipeline;
        }

        public void SetFormats(int index, Format[] imageFormats)
        {
            for (int i = 0; i < imageFormats.Length; i++)
            {
                _textureRefs[index + i].ImageFormat = imageFormats[i];
            }

            SetDirty();
        }

        public void SetImages(int index, ITexture[] images)
        {
            for (int i = 0; i < images.Length; i++)
            {
                ITexture image = images[i];

                if (image is TextureBuffer textureBuffer)
                {
                    _bufferTextureRefs[index + i] = textureBuffer;
                }
                else if (image is Texture texture)
                {
                    _textureRefs[index + i].Storage = texture;
                }
                else if (!_isBuffer)
                {
                    _textureRefs[index + i].Storage = null;
                }
                else
                {
                    _bufferTextureRefs[index + i] = null;
                }
            }

            SetDirty();
        }

        private void SetDirty()
        {
            _pipeline.DirtyImages();
        }

        public void Dispose() { }
    }
}
