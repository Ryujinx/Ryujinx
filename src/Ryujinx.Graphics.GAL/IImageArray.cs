namespace Ryujinx.Graphics.GAL
{
    public interface IImageArray
    {
        void SetFormats(int index, Format[] imageFormats);
        void SetImages(int index, ITexture[] images);
    }
}
