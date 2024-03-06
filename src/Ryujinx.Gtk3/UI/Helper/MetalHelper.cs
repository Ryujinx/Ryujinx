using Gdk;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.UI.Helper
{
    public delegate void UpdateBoundsCallbackDelegate(Window window);

    [SupportedOSPlatform("macos")]
    static partial class MetalHelper
    {
        private const string LibObjCImport = "/usr/lib/libobjc.A.dylib";

        private readonly struct Selector
        {
            public readonly IntPtr NativePtr;

            public unsafe Selector(string value)
            {
                int size = System.Text.Encoding.UTF8.GetMaxByteCount(value.Length);
                byte* data = stackalloc byte[size];

                fixed (char* pValue = value)
                {
                    System.Text.Encoding.UTF8.GetBytes(pValue, value.Length, data, size);
                }

                NativePtr = sel_registerName(data);
            }

            public static implicit operator Selector(string value) => new(value);
        }

        private static unsafe IntPtr GetClass(string value)
        {
            int size = System.Text.Encoding.UTF8.GetMaxByteCount(value.Length);
            byte* data = stackalloc byte[size];

            fixed (char* pValue = value)
            {
                System.Text.Encoding.UTF8.GetBytes(pValue, value.Length, data, size);
            }

            return objc_getClass(data);
        }

        private struct NsPoint
        {
            public double X;
            public double Y;

            public NsPoint(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        private struct NsRect
        {
            public NsPoint Pos;
            public NsPoint Size;

            public NsRect(double x, double y, double width, double height)
            {
                Pos = new NsPoint(x, y);
                Size = new NsPoint(width, height);
            }
        }

        public static IntPtr GetMetalLayer(Display display, Window window, out IntPtr nsView, out UpdateBoundsCallbackDelegate updateBounds)
        {
            nsView = gdk_quartz_window_get_nsview(window.Handle);

            // Create a new CAMetalLayer.
            IntPtr layerClass = GetClass("CAMetalLayer");
            IntPtr metalLayer = IntPtr_objc_msgSend(layerClass, "alloc");
            objc_msgSend(metalLayer, "init");

            // Create a child NSView to render into.
            IntPtr nsViewClass = GetClass("NSView");
            IntPtr child = IntPtr_objc_msgSend(nsViewClass, "alloc");
            objc_msgSend(child, "init", new NsRect());

            // Add it as a child.
            objc_msgSend(nsView, "addSubview:", child);

            // Make its renderer our metal layer.
            objc_msgSend(child, "setWantsLayer:", (byte)1);
            objc_msgSend(child, "setLayer:", metalLayer);
            objc_msgSend(metalLayer, "setContentsScale:", (double)display.GetMonitorAtWindow(window).ScaleFactor);

            // Set the frame position/location.
            updateBounds = (Window window) =>
            {
                window.GetPosition(out int x, out int y);
                int width = window.Width;
                int height = window.Height;
                objc_msgSend(child, "setFrame:", new NsRect(x, y, width, height));
            };

            updateBounds(window);

            return metalLayer;
        }

        [LibraryImport(LibObjCImport)]
        private static unsafe partial IntPtr sel_registerName(byte* data);

        [LibraryImport(LibObjCImport)]
        private static unsafe partial IntPtr objc_getClass(byte* data);

        [LibraryImport(LibObjCImport)]
        private static partial void objc_msgSend(IntPtr receiver, Selector selector);

        [LibraryImport(LibObjCImport)]
        private static partial void objc_msgSend(IntPtr receiver, Selector selector, byte value);

        [LibraryImport(LibObjCImport)]
        private static partial void objc_msgSend(IntPtr receiver, Selector selector, IntPtr value);

        [LibraryImport(LibObjCImport)]
        private static partial void objc_msgSend(IntPtr receiver, Selector selector, NsRect point);

        [LibraryImport(LibObjCImport)]
        private static partial void objc_msgSend(IntPtr receiver, Selector selector, double value);

        [LibraryImport(LibObjCImport, EntryPoint = "objc_msgSend")]
        private static partial IntPtr IntPtr_objc_msgSend(IntPtr receiver, Selector selector);

        [LibraryImport("libgdk-3.0.dylib")]
        private static partial IntPtr gdk_quartz_window_get_nsview(IntPtr gdkWindow);
    }
}
