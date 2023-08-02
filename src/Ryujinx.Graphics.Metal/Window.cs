using Ryujinx.Common.Logging;
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
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void ChangeVSyncMode(bool vsyncEnabled)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetAntiAliasing(AntiAliasing antialiasing)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetScalingFilter(ScalingFilter type)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void SetScalingFilterLevel(float level)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public void Dispose()
        {

        }
    }
}
