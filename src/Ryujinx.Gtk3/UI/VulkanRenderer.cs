using Gdk;
using Ryujinx.Common.Configuration;
using Ryujinx.Input.HLE;
using Ryujinx.UI.Helper;
using SPB.Graphics.Vulkan;
using SPB.Platform.Metal;
using SPB.Platform.Win32;
using SPB.Platform.X11;
using SPB.Windowing;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.UI
{
    public partial class VulkanRenderer : RendererWidgetBase
    {
        public NativeWindowBase NativeWindow { get; private set; }
        private UpdateBoundsCallbackDelegate _updateBoundsCallback;

        public VulkanRenderer(InputManager inputManager, GraphicsDebugLevel glLogLevel) : base(inputManager, glLogLevel) { }

        private NativeWindowBase RetrieveNativeWindow()
        {
            if (OperatingSystem.IsWindows())
            {
                IntPtr windowHandle = gdk_win32_window_get_handle(Window.Handle);

                return new SimpleWin32Window(new NativeHandle(windowHandle));
            }
            else if (OperatingSystem.IsLinux())
            {
                IntPtr displayHandle = gdk_x11_display_get_xdisplay(Display.Handle);
                IntPtr windowHandle = gdk_x11_window_get_xid(Window.Handle);

                return new SimpleX11Window(new NativeHandle(displayHandle), new NativeHandle(windowHandle));
            }
            else if (OperatingSystem.IsMacOS())
            {
                IntPtr metalLayer = MetalHelper.GetMetalLayer(Display, Window, out IntPtr nsView, out _updateBoundsCallback);

                return new SimpleMetalWindow(new NativeHandle(nsView), new NativeHandle(metalLayer));
            }

            throw new NotImplementedException();
        }

        [LibraryImport("libgdk-3-0.dll")]
        private static partial IntPtr gdk_win32_window_get_handle(IntPtr d);

        [LibraryImport("libgdk-3.so.0")]
        private static partial IntPtr gdk_x11_display_get_xdisplay(IntPtr gdkDisplay);

        [LibraryImport("libgdk-3.so.0")]
        private static partial IntPtr gdk_x11_window_get_xid(IntPtr gdkWindow);

        protected override bool OnConfigureEvent(EventConfigure evnt)
        {
            if (NativeWindow == null)
            {
                NativeWindow = RetrieveNativeWindow();

                WaitEvent.Set();
            }

            bool result = base.OnConfigureEvent(evnt);

            _updateBoundsCallback?.Invoke(Window);

            return result;
        }

        public unsafe IntPtr CreateWindowSurface(IntPtr instance)
        {
            return VulkanHelper.CreateWindowSurface(instance, NativeWindow);
        }

        public override void InitializeRenderer() { }

        public override void SwapBuffers() { }

        protected override string GetGpuBackendName()
        {
            return "Vulkan";
        }

        protected override void Dispose(bool disposing)
        {
            Device?.DisposeGpu();

            NpadManager.Dispose();
        }
    }
}
