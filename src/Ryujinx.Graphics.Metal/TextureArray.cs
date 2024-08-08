using Ryujinx.Graphics.GAL;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    internal class TextureArray : ITextureArray
    {
        private readonly TextureRef[] _textureRefs;
        private readonly TextureBuffer[] _bufferTextureRefs;

        private readonly bool _isBuffer;
        private readonly Pipeline _pipeline;

        public TextureArray(int size, bool isBuffer, Pipeline pipeline)
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

        public void SetSamplers(int index, ISampler[] samplers)
        {
            for (int i = 0; i < samplers.Length; i++)
            {
                ISampler sampler = samplers[i];

                if (sampler is Sampler samp)
                {
                    _textureRefs[index + i].Sampler = samp;
                }
                else
                {
                    _textureRefs[index + i].Sampler = default;
                }
            }

            SetDirty();
        }

        public void SetTextures(int index, ITexture[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                ITexture texture = textures[i];

                if (texture is TextureBuffer textureBuffer)
                {
                    _bufferTextureRefs[index + i] = textureBuffer;
                }
                else if (texture is Texture tex)
                {
                    _textureRefs[index + i].Storage = tex;
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

        public TextureRef[] GetTextureRefs()
        {
            return _textureRefs;
        }

        public TextureBuffer[] GetBufferTextureRefs()
        {
            return _bufferTextureRefs;
        }

        private void SetDirty()
        {
            _pipeline.DirtyTextures();
        }

        public void Dispose() { }
    }
}
