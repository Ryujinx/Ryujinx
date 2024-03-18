using SharpMetal.QuartzCore;

namespace Ryujinx.Ava.UI.Renderer
{
    public class EmbeddedWindowMetal : EmbeddedWindow
    {
        public CAMetalLayer CreateSurface()
        {
            return new CAMetalLayer(MetalLayer);
        }
    }
}
