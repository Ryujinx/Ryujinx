using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.OpenGL.Image
{
    class TextureArray : ITextureArray
    {
        private record struct TextureRef
        {
            public TextureBase Texture;
            public Sampler Sampler;
        }

        private readonly TextureRef[] _textureRefs;

        public TextureArray(int size)
        {
            _textureRefs = new TextureRef[size];
        }

        public void SetSamplers(int index, ISampler[] samplers)
        {
            for (int i = 0; i < samplers.Length; i++)
            {
                _textureRefs[index + i].Sampler = samplers[i] as Sampler;
            }
        }

        public void SetTextures(int index, ITexture[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                _textureRefs[index + i].Texture = textures[i] as TextureBase;
            }
        }

        public void Bind(int baseBinding)
        {
            for (int i = 0; i < _textureRefs.Length; i++)
            {
                if (_textureRefs[i].Texture != null)
                {
                    _textureRefs[i].Texture.Bind(baseBinding + i);
                    _textureRefs[i].Sampler?.Bind(baseBinding + i);
                }
                else
                {
                    TextureBase.ClearBinding(baseBinding + i);
                }
            }
        }
    }
}
