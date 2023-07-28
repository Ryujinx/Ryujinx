using Ryujinx.Graphics.GAL;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class Window : IWindow, IDisposable
    {
        private readonly MetalRenderer _renderer;

        public Window(MetalRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            if (_renderer.Pipeline is Pipeline pipeline)
            {
                pipeline.Present();
            }
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
