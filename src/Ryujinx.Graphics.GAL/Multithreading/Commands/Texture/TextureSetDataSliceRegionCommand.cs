using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using System;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.Texture
{
    struct TextureSetDataSliceRegionCommand : IGALCommand, IGALCommand<TextureSetDataSliceRegionCommand>
    {
        public readonly CommandType CommandType => CommandType.TextureSetDataSliceRegion;
        private TableRef<ThreadedTexture> _texture;
        private TableRef<byte[]> _data;
        private int _layer;
        private int _level;
        private Rectangle<int> _region;

        public void Set(TableRef<ThreadedTexture> texture, TableRef<byte[]> data, int layer, int level, Rectangle<int> region)
        {
            _texture = texture;
            _data = data;
            _layer = layer;
            _level = level;
            _region = region;
        }

        public static void Run(ref TextureSetDataSliceRegionCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedTexture texture = command._texture.Get(threaded);
            texture.Base.SetData(new ReadOnlySpan<byte>(command._data.Get(threaded)), command._layer, command._level, command._region);
        }
    }
}