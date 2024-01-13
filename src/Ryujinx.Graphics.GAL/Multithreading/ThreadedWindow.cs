using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.GAL.Multithreading.Commands.Window;
using Ryujinx.Graphics.GAL.Multithreading.Model;
using Ryujinx.Graphics.GAL.Multithreading.Resources;
using System;

namespace Ryujinx.Graphics.GAL.Multithreading
{
    public class ThreadedWindow : IWindow
    {
        private readonly ThreadedRenderer _renderer;
        private readonly IRenderer _impl;

        public ThreadedWindow(ThreadedRenderer renderer, IRenderer impl)
        {
            _renderer = renderer;
            _impl = impl;
        }

        public void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            // If there's already a frame in the pipeline, wait for it to be presented first.
            // This is a multithread rate limit - we can't be more than one frame behind the command queue.

            _renderer.WaitForFrame();
            _renderer.New<WindowPresentCommand>().Set(new TableRef<ThreadedTexture>(_renderer, texture as ThreadedTexture), crop, new TableRef<Action>(_renderer, swapBuffersCallback));
            _renderer.QueueCommand();
        }

        public void SetSize(int width, int height)
        {
            _impl.Window.SetSize(width, height);
        }

        public void ChangeVSyncMode(VSyncMode vSyncMode) { }

        public void SetAntiAliasing(AntiAliasing effect) { }

        public void SetScalingFilter(ScalingFilter type) { }

        public void SetScalingFilterLevel(float level) { }

        public void SetColorSpacePassthrough(bool colorSpacePassthroughEnabled) { }
    }
}
