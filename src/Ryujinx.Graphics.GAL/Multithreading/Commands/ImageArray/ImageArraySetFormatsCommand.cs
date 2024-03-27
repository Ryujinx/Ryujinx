using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.ImageArray
{
    struct ImageArraySetFormatsCommand : IGALCommand, IGALCommand<ImageArraySetFormatsCommand>
    {
        public readonly CommandType CommandType => CommandType.ImageArraySetFormats;
        private TableRef<ThreadedImageArray> _imageArray;
        private int _index;
        private TableRef<Format[]> _imageFormats;

        public void Set(TableRef<ThreadedImageArray> imageArray, int index, TableRef<Format[]> imageFormats)
        {
            _imageArray = imageArray;
            _index = index;
            _imageFormats = imageFormats;
        }

        public static void Run(ref ImageArraySetFormatsCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedImageArray imageArray = command._imageArray.Get(threaded);
            imageArray.Base.SetFormats(command._index, command._imageFormats.Get(threaded));
        }
    }
}
