using Ryujinx.Graphics.GAL.Multithreading.Commands.TextureArray;
using Ryujinx.Graphics.GAL.Multithreading.Model;
using System.Linq;

namespace Ryujinx.Graphics.GAL.Multithreading.Resources
{
    /// <summary>
    /// Threaded representation of a texture and sampler array.
    /// </summary>
    class ThreadedTextureArray : ITextureArray
    {
        private readonly ThreadedRenderer _renderer;
        public ITextureArray Base;

        public ThreadedTextureArray(ThreadedRenderer renderer)
        {
            _renderer = renderer;
        }

        private TableRef<T> Ref<T>(T reference)
        {
            return new TableRef<T>(_renderer, reference);
        }

        public void SetSamplers(int index, ISampler[] samplers)
        {
            _renderer.New<TextureArraySetSamplersCommand>().Set(Ref(this), index, Ref(samplers.ToArray()));
            _renderer.QueueCommand();
        }

        public void SetTextures(int index, ITexture[] textures)
        {
            _renderer.New<TextureArraySetTexturesCommand>().Set(Ref(this), index, Ref(textures.ToArray()));
            _renderer.QueueCommand();
        }
    }
}
