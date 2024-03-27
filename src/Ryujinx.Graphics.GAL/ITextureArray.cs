namespace Ryujinx.Graphics.GAL
{
    public interface ITextureArray
    {
        void SetSamplers(int index, ISampler[] samplers);
        void SetTextures(int index, ITexture[] textures);
    }
}
