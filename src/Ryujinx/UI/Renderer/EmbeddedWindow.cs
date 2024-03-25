using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Ryujinx.Common.Configuration;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using SPB.Graphics;
using SPB.Platform;
using SPB.Platform.GLX;
using SPB.Platform.X11;
using SPB.Windowing;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using static Ryujinx.Ava.UI.Helpers.Win32NativeInterop;

namespace Ryujinx.Ava.UI.Renderer
{
    public class EmbeddedWindow : NativeControlHost
    {
        private WindowProc _wndProcDelegate;
        private string _className;

        protected GLXWindow X11Window { get; set; }

        protected IntPtr WindowHandle { get; set; }
        protected IntPtr X11Display { get; set; }
        protected IntPtr NsView { get; set; }
        protected IntPtr MetalLayer { get; set; }

        public delegate void UpdateBoundsCallbackDelegate(Rect rect);
        private UpdateBoundsCallbackDelegate _updateBoundsCallback;

        public event EventHandler<IntPtr> WindowCreated;
        public event EventHandler<Size> BoundsChanged;

        public EmbeddedWindow()
        {
            this.GetObservable(BoundsProperty).Subscribe(StateChanged);

            Initialized += OnNativeEmbeddedWindowCreated;
        }

        public virtual void OnWindowCreated() { }

        protected virtual void OnWindowDestroyed() { }

        protected virtual void OnWindowDestroying()
        {
            WindowHandle = IntPtr.Zero;
            X11Display = IntPtr.Zero;
            NsView = IntPtr.Zero;
            MetalLayer = IntPtr.Zero;
        }

        private void OnNativeEmbeddedWindowCreated(object sender, EventArgs e)
        {
            OnWindowCreated();

            Task.Run(() =>
            {
                WindowCreated?.Invoke(this, WindowHandle);
            });
        }

        private void StateChanged(Rect rect)
        {
            BoundsChanged?.Invoke(this, rect.Size);
            _updateBoundsCallback?.Invoke(rect);
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle control)
        {
            if (OperatingSystem.IsLinux())
            {
                return CreateLinux(control);
            }

            if (OperatingSystem.IsWindows())
            {
                return CreateWin32(control);
            }

            if (OperatingSystem.IsMacOS())
            {
                return CreateMacOS();
            }

            return base.CreateNativeControlCore(control);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            OnWindowDestroying();

            if (OperatingSystem.IsLinux())
            {
                DestroyLinux();
            }
            else if (OperatingSystem.IsWindows())
            {
                DestroyWin32(control);
            }
            else if (OperatingSystem.IsMacOS())
            {
                DestroyMacOS();
            }
            else
            {
                base.DestroyNativeControlCore(control);
            }

            OnWindowDestroyed();
        }

        [SupportedOSPlatform("linux")]
        private IPlatformHandle CreateLinux(IPlatformHandle control)
        {
            if (ConfigurationState.Instance.Graphics.GraphicsBackend.Value == GraphicsBackend.Vulkan)
            {
                X11Window = new GLXWindow(new NativeHandle(X11.DefaultDisplay), new NativeHandle(control.Handle));
                X11Window.Hide();
            }
            else
            {
                X11Window = PlatformHelper.CreateOpenGLWindow(new FramebufferFormat(new ColorFormat(8, 8, 8, 0), 16, 0, ColorFormat.Zero, 0, 2, false), 0, 0, 100, 100) as GLXWindow;
            }

            WindowHandle = X11Window.WindowHandle.RawHandle;
            X11Display = X11Window.DisplayHandle.RawHandle;

            return new PlatformHandle(WindowHandle, "X11");
        }

        [SupportedOSPlatform("windows")]
        IPlatformHandle CreateWin32(IPlatformHandle control)
        {
            _className = "NativeWindow-" + Guid.NewGuid();

            _wndProcDelegate = delegate (IntPtr hWnd, WindowsMessages msg, IntPtr wParam, IntPtr lParam)
            {
                switch (msg)
                {
                    case WindowsMessages.NcHitTest:
                        return -1;
                }

                return DefWindowProc(hWnd, msg, wParam, lParam);
            };

            WndClassEx wndClassEx = new()
            {
                cbSize = Marshal.SizeOf<WndClassEx>(),
                hInstance = GetModuleHandle(null),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                style = ClassStyles.CsOwndc,
                lpszClassName = Marshal.StringToHGlobalUni(_className),
                hCursor = CreateArrowCursor(),
            };

            RegisterClassEx(ref wndClassEx);

            WindowHandle = CreateWindowEx(0, _className, "NativeWindow", WindowStyles.WsChild, 0, 0, 640, 480, control.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            SetWindowLongPtrW(control.Handle, GWLP_WNDPROC, wndClassEx.lpfnWndProc);

            Marshal.FreeHGlobal(wndClassEx.lpszClassName);

            return new PlatformHandle(WindowHandle, "HWND");
        }

        [SupportedOSPlatform("macos")]
        IPlatformHandle CreateMacOS()
        {
            // Create a new CAMetalLayer.
            ObjectiveC.Object layerObject = new("CAMetalLayer");
            ObjectiveC.Object metalLayer = layerObject.GetFromMessage("alloc");
            metalLayer.SendMessage("init");

            // Create a child NSView to render into.
            ObjectiveC.Object nsViewObject = new("NSView");
            ObjectiveC.Object child = nsViewObject.GetFromMessage("alloc");
            child.SendMessage("init", new ObjectiveC.NSRect(0, 0, 0, 0));

            // Make its renderer our metal layer.
            child.SendMessage("setWantsLayer:", 1);
            child.SendMessage("setLayer:", metalLayer);
            metalLayer.SendMessage("setContentsScale:", Program.DesktopScaleFactor);

            // Ensure the scale factor is up to date.
            _updateBoundsCallback = rect =>
            {
                metalLayer.SendMessage("setContentsScale:", Program.DesktopScaleFactor);
            };

            IntPtr nsView = child.ObjPtr;
            MetalLayer = metalLayer.ObjPtr;
            NsView = nsView;

            return new PlatformHandle(nsView, "NSView");
        }

        [SupportedOSPlatform("Linux")]
        void DestroyLinux()
        {
            X11Window?.Dispose();
        }

        [SupportedOSPlatform("windows")]
        void DestroyWin32(IPlatformHandle handle)
        {
            DestroyWindow(handle.Handle);
            UnregisterClass(_className, GetModuleHandle(null));
        }

        [SupportedOSPlatform("macos")]
#pragma warning disable CA1822 // Mark member as static
        void DestroyMacOS()
        {
            // TODO
        }
#pragma warning restore CA1822
    }
}
