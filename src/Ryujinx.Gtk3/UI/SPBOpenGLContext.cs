using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.OpenGL;
using SPB.Graphics;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Windowing;

namespace Ryujinx.UI
{
    class SPBOpenGLContext : IOpenGLContext
    {
        private readonly OpenGLContextBase _context;
        private readonly NativeWindowBase _window;

        private SPBOpenGLContext(OpenGLContextBase context, NativeWindowBase window)
        {
            _context = context;
            _window = window;
        }

        public void Dispose()
        {
            _context.Dispose();
            _window.Dispose();
        }

        public void MakeCurrent()
        {
            _context.MakeCurrent(_window);
        }

        public bool HasContext() => _context.IsCurrent;

        public static SPBOpenGLContext CreateBackgroundContext(OpenGLContextBase sharedContext)
        {
            OpenGLContextBase context = PlatformHelper.CreateOpenGLContext(FramebufferFormat.Default, 3, 3, OpenGLContextFlags.Compat, true, sharedContext);
            NativeWindowBase window = PlatformHelper.CreateOpenGLWindow(FramebufferFormat.Default, 0, 0, 100, 100);

            context.Initialize(window);
            context.MakeCurrent(window);

            GL.LoadBindings(new OpenToolkitBindingsContext(context));

            context.MakeCurrent(null);

            return new SPBOpenGLContext(context, window);
        }
    }
}
