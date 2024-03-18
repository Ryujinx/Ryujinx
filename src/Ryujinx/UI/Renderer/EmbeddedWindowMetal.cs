using SharpMetal.QuartzCore;
using System;

namespace Ryujinx.Ava.UI.Renderer
{
    public class EmbeddedWindowMetal : EmbeddedWindow
    {
        public CAMetalLayer CreateSurface()
        {
            if (OperatingSystem.IsMacOS())
            {
                return new CAMetalLayer(MetalLayer);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
