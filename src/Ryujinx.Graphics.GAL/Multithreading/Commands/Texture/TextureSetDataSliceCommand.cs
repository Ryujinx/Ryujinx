using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.Texture
{
    struct TextureSetDataSliceCommand : IGALCommand, IGALCommand<TextureSetDataSliceCommand>
    {
        public readonly CommandType CommandType => CommandType.TextureSetDataSlice;
        private TableRef<ThreadedTexture> _texture;
        private TableRef<MemoryOwner<byte>> _data;
        private int _layer;
        private int _level;

        public void Set(TableRef<ThreadedTexture> texture, TableRef<MemoryOwner<byte>> data, int layer, int level)
        {
            _texture = texture;
            _data = data;
            _layer = layer;
            _level = level;
        }

        public static void Run(ref TextureSetDataSliceCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedTexture texture = command._texture.Get(threaded);
            texture.Base.SetData(command._data.Get(threaded), command._layer, command._level);
        }
    }
}
