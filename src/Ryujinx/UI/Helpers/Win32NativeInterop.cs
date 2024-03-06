using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Ava.UI.Helpers
{
    [SupportedOSPlatform("windows")]
    internal partial class Win32NativeInterop
    {
        [Flags]
        public enum ClassStyles : uint
        {
            CsClassdc = 0x40,
            CsOwndc = 0x20,
        }

        [Flags]
        public enum WindowStyles : uint
        {
            WsChild = 0x40000000,
        }

        public enum Cursors : uint
        {
            IdcArrow = 32512,
        }

        [SuppressMessage("Design", "CA1069: Enums values should not be duplicated")]
        public enum WindowsMessages : uint
        {
            Mousemove = 0x0200,
            Lbuttondown = 0x0201,
            Lbuttonup = 0x0202,
            Lbuttondblclk = 0x0203,
            Rbuttondown = 0x0204,
            Rbuttonup = 0x0205,
            Rbuttondblclk = 0x0206,
            Mbuttondown = 0x0207,
            Mbuttonup = 0x0208,
            Mbuttondblclk = 0x0209,
            Mousewheel = 0x020A,
            Xbuttondown = 0x020B,
            Xbuttonup = 0x020C,
            Xbuttondblclk = 0x020D,
            Mousehwheel = 0x020E,
            Mouselast = 0x020E,
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate IntPtr WindowProc(IntPtr hWnd, WindowsMessages msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct WndClassEx
        {
            public int cbSize;
            public ClassStyles style;
            public IntPtr lpfnWndProc; // not WndProc
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public IntPtr lpszMenuName;
            public IntPtr lpszClassName;
            public IntPtr hIconSm;

            public WndClassEx()
            {
                cbSize = Marshal.SizeOf<WndClassEx>();
            }
        }

        public static IntPtr CreateEmptyCursor()
        {
            return CreateCursor(IntPtr.Zero, 0, 0, 1, 1, new byte[] { 0xFF }, new byte[] { 0x00 });
        }

        public static IntPtr CreateArrowCursor()
        {
            return LoadCursor(IntPtr.Zero, (IntPtr)Cursors.IdcArrow);
        }

        [LibraryImport("user32.dll")]
        public static partial IntPtr SetCursor(IntPtr handle);

        [LibraryImport("user32.dll")]
        public static partial IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, [In] byte[] pvAndPlane, [In] byte[] pvXorPlane);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassExW")]
        public static partial ushort RegisterClassEx(ref WndClassEx param);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "UnregisterClassW")]
        public static partial short UnregisterClass([MarshalAs(UnmanagedType.LPWStr)] string lpClassName, IntPtr instance);

        [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
        public static partial IntPtr DefWindowProc(IntPtr hWnd, WindowsMessages msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleA")]
        public static partial IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPStr)] string lpModuleName);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyWindow(IntPtr hwnd);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "LoadCursorA")]
        public static partial IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW")]
        public static partial IntPtr CreateWindowEx(
           uint dwExStyle,
           [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
           [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
           WindowStyles dwStyle,
           int x,
           int y,
           int nWidth,
           int nHeight,
           IntPtr hWndParent,
           IntPtr hMenu,
           IntPtr hInstance,
           IntPtr lpParam);
    }
}
