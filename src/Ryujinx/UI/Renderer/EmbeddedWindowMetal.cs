using SPB.Windowing;
using SPB.Platform.Metal;
using System;

namespace Ryujinx.Ava.UI.Renderer
{
    public class EmbeddedWindowMetal : EmbeddedWindow
    {
        public SimpleMetalWindow CreateSurface()
        {
            SimpleMetalWindow simpleMetalWindow;

            if (OperatingSystem.IsMacOS())
            {
                simpleMetalWindow = new SimpleMetalWindow(new NativeHandle(NsView), new NativeHandle(MetalLayer));
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return simpleMetalWindow;
        }
    }
}
