using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using System.Linq;

namespace Ryujinx.Graphics.GAL.Multithreading.Commands.ImageArray
{
    struct ImageArraySetImagesCommand : IGALCommand, IGALCommand<ImageArraySetImagesCommand>
    {
        public readonly CommandType CommandType => CommandType.ImageArraySetImages;
        private TableRef<ThreadedImageArray> _imageArray;
        private int _index;
        private TableRef<ITexture[]> _images;

        public void Set(TableRef<ThreadedImageArray> imageArray, int index, TableRef<ITexture[]> images)
        {
            _imageArray = imageArray;
            _index = index;
            _images = images;
        }

        public static void Run(ref ImageArraySetImagesCommand command, ThreadedRenderer threaded, IRenderer renderer)
        {
            ThreadedImageArray imageArray = command._imageArray.Get(threaded);
            imageArray.Base.SetImages(command._index, command._images.Get(threaded).Select(texture => ((ThreadedTexture)texture)?.Base).ToArray());
        }
    }
}
