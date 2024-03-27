using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using System.Linq;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.TextureArray
{
    struct TextureArraySetSamplersCommand : IGALCommand, IGALCommand<TextureArraySetSamplersCommand>
    {
        public readonly CommandType CommandType => CommandType.TextureArraySetSamplers;
        private TableRef<ThreadedTextureArray> _textureArray;
        private int _index;
        private TableRef<ISampler[]> _samplers;

        public void Set(TableRef<ThreadedTextureArray> textureArray, int index, TableRef<ISampler[]> samplers)
        {
            _textureArray = textureArray;
            _index = index;
            _samplers = samplers;
        }

        public static void Run(ref TextureArraySetSamplersCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedTextureArray textureArray = command._textureArray.Get(threaded);
            textureArray.Base.SetSamplers(command._index, command._samplers.Get(threaded).Select(sampler => ((ThreadedSampler)sampler)?.Base).ToArray());
        }
    }
}
