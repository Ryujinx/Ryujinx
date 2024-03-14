using Ryujinx.Graphics.GAL.Multithreading.Commands.ImageArray;
using Ryujinx.Graphics.GAL.Multithreading.Model;

namespace Ryujinx.Graphics.GAL.Multithreading.Resources
{
    /// <summary>
    /// Threaded representation of a image array.
    /// </summary>
    class ThreadedImageArray : IImageArray
    {
        private readonly ThreadedRenderer _renderer;
        public IImageArray Base;

        public ThreadedImageArray(ThreadedRenderer renderer)
        {
            _renderer = renderer;
        }

        private TableRef<T> Ref<T>(T reference)
        {
            return new TableRef<T>(_renderer, reference);
        }

        public void SetFormats(int index, Format[] imageFormats)
        {
            _renderer.New<ImageArraySetFormatsCommand>().Set(Ref(this), index, Ref(imageFormats));
            _renderer.QueueCommand();
        }

        public void SetImages(int index, ITexture[] images)
        {
            _renderer.New<ImageArraySetImagesCommand>().Set(Ref(this), index, Ref(images));
            _renderer.QueueCommand();
        }
    }
}
