using Ryujinx.Graphics.GAL;
using System;
using SharpMetal;

namespace Ryujinx.Graphics.Metal
{
    public class Window : IWindow, IDisposable
    {

        public Window()
        {
            /*var viewport = new MTLViewport
            {

            };*/
        }

        public void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            throw new NotImplementedException();
        }

        public void SetSize(int width, int height)
        {
            // Not needed as we can get the size from the surface.
        }

        public void ChangeVSyncMode(bool vsyncEnabled)
        {
            throw new NotImplementedException();
        }

        public void SetAntiAliasing(AntiAliasing antialiasing)
        {
            throw new NotImplementedException();
        }

        public void SetScalingFilter(ScalingFilter type)
        {
            throw new NotImplementedException();
        }

        public void SetScalingFilterLevel(float level)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {

        }
    }
}