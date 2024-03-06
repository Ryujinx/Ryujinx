using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Input.HLE;
using SPB.Graphics;
using SPB.Graphics.Exceptions;
using SPB.Graphics.OpenGL;
using SPB.Platform;
using SPB.Platform.GLX;
using SPB.Platform.WGL;
using SPB.Windowing;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.UI
{
    public partial class OpenGLRenderer : RendererWidgetBase
    {
        private readonly GraphicsDebugLevel _glLogLevel;

        private bool _initializedOpenGL;

        private OpenGLContextBase _openGLContext;
        private SwappableNativeWindowBase _nativeWindow;

        public OpenGLRenderer(InputManager inputManager, GraphicsDebugLevel glLogLevel) : base(inputManager, glLogLevel)
        {
            _glLogLevel = glLogLevel;
        }

        protected override bool OnDrawn(Cairo.Context cr)
        {
            if (!_initializedOpenGL)
            {
                IntializeOpenGL();
            }

            return true;
        }

        private void IntializeOpenGL()
        {
            _nativeWindow = RetrieveNativeWindow();

            Window.EnsureNative();

            _openGLContext = PlatformHelper.CreateOpenGLContext(GetGraphicsMode(), 3, 3, _glLogLevel == GraphicsDebugLevel.None ? OpenGLContextFlags.Compat : OpenGLContextFlags.Compat | OpenGLContextFlags.Debug);
            _openGLContext.Initialize(_nativeWindow);
            _openGLContext.MakeCurrent(_nativeWindow);

            // Release the GL exclusivity that SPB gave us as we aren't going to use it in GTK Thread.
            _openGLContext.MakeCurrent(null);

            WaitEvent.Set();

            _initializedOpenGL = true;
        }

        private SwappableNativeWindowBase RetrieveNativeWindow()
        {
            if (OperatingSystem.IsWindows())
            {
                IntPtr windowHandle = gdk_win32_window_get_handle(Window.Handle);

                return new WGLWindow(new NativeHandle(windowHandle));
            }
            else if (OperatingSystem.IsLinux())
            {
                IntPtr displayHandle = gdk_x11_display_get_xdisplay(Display.Handle);
                IntPtr windowHandle = gdk_x11_window_get_xid(Window.Handle);

                return new GLXWindow(new NativeHandle(displayHandle), new NativeHandle(windowHandle));
            }

            throw new NotImplementedException();
        }

        [LibraryImport("libgdk-3-0.dll")]
        private static partial IntPtr gdk_win32_window_get_handle(IntPtr d);

        [LibraryImport("libgdk-3.so.0")]
        private static partial IntPtr gdk_x11_display_get_xdisplay(IntPtr gdkDisplay);

        [LibraryImport("libgdk-3.so.0")]
        private static partial IntPtr gdk_x11_window_get_xid(IntPtr gdkWindow);

        private static FramebufferFormat GetGraphicsMode()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix ? new FramebufferFormat(new ColorFormat(8, 8, 8, 0), 16, 0, ColorFormat.Zero, 0, 2, false) : FramebufferFormat.Default;
        }

        public override void InitializeRenderer()
        {
            // First take exclusivity on the OpenGL context.
            ((Graphics.OpenGL.OpenGLRenderer)Renderer).InitializeBackgroundContext(SPBOpenGLContext.CreateBackgroundContext(_openGLContext));

            _openGLContext.MakeCurrent(_nativeWindow);

            GL.ClearColor(0, 0, 0, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            SwapBuffers();
        }

        public override void SwapBuffers()
        {
            _nativeWindow.SwapBuffers();
        }

        protected override string GetGpuBackendName()
        {
            return "OpenGL";
        }

        protected override void Dispose(bool disposing)
        {
            // Try to bind the OpenGL context before calling the shutdown event.
            try
            {
                _openGLContext?.MakeCurrent(_nativeWindow);
            }
            catch (ContextException e)
            {
                Logger.Warning?.Print(LogClass.UI, $"Failed to bind OpenGL context: {e}");
            }

            Device?.DisposeGpu();
            NpadManager.Dispose();

            // Unbind context and destroy everything.
            try
            {
                _openGLContext?.MakeCurrent(null);
            }
            catch (ContextException e)
            {
                Logger.Warning?.Print(LogClass.UI, $"Failed to unbind OpenGL context: {e}");
            }

            _openGLContext?.Dispose();
        }
    }
}
