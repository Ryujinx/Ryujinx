using Avalonia;
using Avalonia.Controls;
using Ryujinx.Common.Configuration;
using Ryujinx.UI.Common.Configuration;
using System;

namespace Ryujinx.Ava.UI.Renderer
{
    public partial class RendererHost : UserControl, IDisposable
    {
        public readonly EmbeddedWindow EmbeddedWindow;

        public event EventHandler<EventArgs> WindowCreated;
        public event Action<object, Size> BoundsChanged;

        public RendererHost()
        {
            InitializeComponent();

            EmbeddedWindow = ConfigurationState.Instance.Graphics.GraphicsBackend.Value switch
            {
                GraphicsBackend.OpenGl => new EmbeddedWindowOpenGL(),
                GraphicsBackend.Metal => new EmbeddedWindowMetal(),
                GraphicsBackend.Vulkan => new EmbeddedWindowVulkan(),
                _ => throw new NotSupportedException()
            };

            Initialize();
        }

        private void Initialize()
        {
            EmbeddedWindow.WindowCreated += CurrentWindow_WindowCreated;
            EmbeddedWindow.BoundsChanged += CurrentWindow_BoundsChanged;

            Content = EmbeddedWindow;
        }

        public void Dispose()
        {
            if (EmbeddedWindow != null)
            {
                EmbeddedWindow.WindowCreated -= CurrentWindow_WindowCreated;
                EmbeddedWindow.BoundsChanged -= CurrentWindow_BoundsChanged;
            }

            GC.SuppressFinalize(this);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            Dispose();
        }

        private void CurrentWindow_BoundsChanged(object sender, Size e)
        {
            BoundsChanged?.Invoke(sender, e);
        }

        private void CurrentWindow_WindowCreated(object sender, IntPtr e)
        {
            WindowCreated?.Invoke(this, EventArgs.Empty);
        }
    }
}
