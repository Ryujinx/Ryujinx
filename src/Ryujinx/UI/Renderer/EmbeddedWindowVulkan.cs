using Silk.NET.Vulkan;
using SPB.Graphics.Vulkan;
using SPB.Platform.Metal;
using SPB.Platform.Win32;
using SPB.Platform.X11;
using SPB.Windowing;
using System;

namespace Ryujinx.Ava.UI.Renderer
{
    public class EmbeddedWindowVulkan : EmbeddedWindow
    {
        public SurfaceKHR CreateSurface(Instance instance)
        {
            NativeWindowBase nativeWindowBase;

            if (OperatingSystem.IsWindows())
            {
                nativeWindowBase = new SimpleWin32Window(new NativeHandle(WindowHandle));
            }
            else if (OperatingSystem.IsLinux())
            {
                nativeWindowBase = new SimpleX11Window(new NativeHandle(X11Display), new NativeHandle(WindowHandle));
            }
            else if (OperatingSystem.IsMacOS())
            {
                nativeWindowBase = new SimpleMetalWindow(new NativeHandle(NsView), new NativeHandle(MetalLayer));
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return new SurfaceKHR((ulong?)VulkanHelper.CreateWindowSurface(instance.Handle, nativeWindowBase));
        }

        public SurfaceKHR CreateSurface(Instance instance, Vk _)
        {
            return CreateSurface(instance);
        }
    }
}
