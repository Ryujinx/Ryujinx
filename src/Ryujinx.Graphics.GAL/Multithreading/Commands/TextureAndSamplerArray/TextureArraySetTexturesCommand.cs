using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using System.Linq;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.TextureArray
{
    struct TextureArraySetTexturesCommand : IGALCommand, IGALCommand<TextureArraySetTexturesCommand>
    {
        public readonly CommandType CommandType => CommandType.TextureArraySetTextures;
        private TableRef<ThreadedTextureArray> _textureArray;
        private int _index;
        private TableRef<ITexture[]> _textures;

        public void Set(TableRef<ThreadedTextureArray> textureArray, int index, TableRef<ITexture[]> textures)
        {
            _textureArray = textureArray;
            _index = index;
            _textures = textures;
        }

        public static void Run(ref TextureArraySetTexturesCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedTextureArray textureArray = command._textureArray.Get(threaded);
            textureArray.Base.SetTextures(command._index, command._textures.Get(threaded).Select(texture => ((ThreadedTexture)texture)?.Base).ToArray());
        }
    }
}
