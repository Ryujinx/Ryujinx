using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.Renderer
{
    struct CreateTextureArrayCommand : IGALCommand, IGALCommand<CreateTextureArrayCommand>
    {
        public readonly CommandType CommandType => CommandType.CreateTextureArray;
        private TableRef<ThreadedTextureArray> _textureArray;
        private int _size;
        private bool _isBuffer;

        public void Set(TableRef<ThreadedTextureArray> textureArray, int size, bool isBuffer)
        {
            _textureArray = textureArray;
            _size = size;
            _isBuffer = isBuffer;
        }

        public static void Run(ref CreateTextureArrayCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            command._textureArray.Get(threaded).Base = renderer.CreateTextureArray(command._size, command._isBuffer);
        }
    }
}
