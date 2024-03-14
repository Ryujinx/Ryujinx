using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.Renderer
{
    struct CreateImageArrayCommand : IGALCommand, IGALCommand<CreateImageArrayCommand>
    {
        public readonly CommandType CommandType => CommandType.CreateImageArray;
        private TableRef<ThreadedImageArray> _imageArray;
        private int _size;
        private bool _isBuffer;

        public void Set(TableRef<ThreadedImageArray> imageArray, int size, bool isBuffer)
        {
            _imageArray = imageArray;
            _size = size;
            _isBuffer = isBuffer;
        }

        public static void Run(ref CreateImageArrayCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            command._imageArray.Get(threaded).Base = renderer.CreateImageArray(command._size, command._isBuffer);
        }
    }
}
