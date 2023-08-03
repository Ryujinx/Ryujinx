using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.ObjectiveCCore;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Window : IWindow, IDisposable
    {
        private readonly MetalRenderer _renderer;
        private readonly CAMetalLayer _metalLayer;

        public Window(MetalRenderer renderer, CAMetalLayer metalLayer)
        {
            _renderer = renderer;
            _metalLayer = metalLayer;
        }

        // TODO: Handle ImageCrop
        public void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            if (_renderer.Pipeline is Pipeline pipeline && texture is Texture tex)
            {
                var drawable = new CAMetalDrawable(ObjectiveC.IntPtr_objc_msgSend(_metalLayer, "nextDrawable"));
                pipeline.Present(drawable, tex);
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
