using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.OpenGL.Image
{
    class ImageArray : IImageArray
    {
        private record struct TextureRef
        {
            public int Handle;
            public Format Format;
        }

        private readonly TextureRef[] _images;

        public ImageArray(int size)
        {
            _images = new TextureRef[size];
        }

        public void SetFormats(int index, GAL.Format[] imageFormats)
        {
            for (int i = 0; i < imageFormats.Length; i++)
            {
                _images[index + i].Format = imageFormats[i];
            }
        }

        public void SetImages(int index, ITexture[] images)
        {
            for (int i = 0; i < images.Length; i++)
            {
                ITexture image = images[i];

                if (image is TextureBase imageBase)
                {
                    _images[index + i].Handle = imageBase.Handle;
                }
                else
                {
                    _images[index + i].Handle = 0;
                }
            }
        }

        public void Bind(int baseBinding)
        {
            for (int i = 0; i < _images.Length; i++)
            {
                if (_images[i].Handle == 0)
                {
                    GL.BindImageTexture(baseBinding + i, 0, 0, true, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba8);
                }
                else
                {
                    SizedInternalFormat format = FormatTable.GetImageFormat(_images[i].Format);

                    if (format != 0)
                    {
                        GL.BindImageTexture(baseBinding + i, _images[i].Handle, 0, true, 0, TextureAccess.ReadWrite, format);
                    }
                }
            }
        }
    }
}
