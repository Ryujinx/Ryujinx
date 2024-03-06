using Gtk;
using Ryujinx.HLE.UI;
using System.Diagnostics;

namespace Ryujinx.UI.Applet
{
    internal class GtkHostUITheme : IHostUITheme
    {
        private const int RenderSurfaceWidth = 32;
        private const int RenderSurfaceHeight = 32;

        public string FontFamily { get; private set; }

        public ThemeColor DefaultBackgroundColor { get; }
        public ThemeColor DefaultForegroundColor { get; }
        public ThemeColor DefaultBorderColor { get; }
        public ThemeColor SelectionBackgroundColor { get; }
        public ThemeColor SelectionForegroundColor { get; }

        public GtkHostUITheme(Window parent)
        {
            Entry entry = new();
            entry.SetStateFlags(StateFlags.Selected, true);

            // Get the font and some colors directly from GTK.
            FontFamily = entry.PangoContext.FontDescription.Family;

            // Get foreground colors from the style context.

            var defaultForegroundColor = entry.StyleContext.GetColor(StateFlags.Normal);
            var selectedForegroundColor = entry.StyleContext.GetColor(StateFlags.Selected);

            DefaultForegroundColor = new ThemeColor((float)defaultForegroundColor.Alpha, (float)defaultForegroundColor.Red, (float)defaultForegroundColor.Green, (float)defaultForegroundColor.Blue);
            SelectionForegroundColor = new ThemeColor((float)selectedForegroundColor.Alpha, (float)selectedForegroundColor.Red, (float)selectedForegroundColor.Green, (float)selectedForegroundColor.Blue);

            ListBoxRow row = new();
            row.SetStateFlags(StateFlags.Selected, true);

            // Request the main thread to render some UI elements to an image to get an approximation for the color.
            // NOTE (caian): This will only take the color of the top-left corner of the background, which may be incorrect
            // if someone provides a custom style with a gradient or image.

            using (var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, RenderSurfaceWidth, RenderSurfaceHeight))
            using (var context = new Cairo.Context(surface))
            {
                context.SetSourceRGBA(1, 1, 1, 1);
                context.Rectangle(0, 0, RenderSurfaceWidth, RenderSurfaceHeight);
                context.Fill();

                // The background color must be from the main Window because entry uses a different color.
                parent.StyleContext.RenderBackground(context, 0, 0, RenderSurfaceWidth, RenderSurfaceHeight);

                DefaultBackgroundColor = ToThemeColor(surface.Data);

                context.SetSourceRGBA(1, 1, 1, 1);
                context.Rectangle(0, 0, RenderSurfaceWidth, RenderSurfaceHeight);
                context.Fill();

                // Use the background color of the list box row when selected as the text box frame color because they are the
                // same in the default theme.
                row.StyleContext.RenderBackground(context, 0, 0, RenderSurfaceWidth, RenderSurfaceHeight);

                DefaultBorderColor = ToThemeColor(surface.Data);
            }

            // Use the border color as the text selection color.
            SelectionBackgroundColor = DefaultBorderColor;
        }

        private static ThemeColor ToThemeColor(byte[] data)
        {
            Debug.Assert(data.Length == 4 * RenderSurfaceWidth * RenderSurfaceHeight);

            // Take the center-bottom pixel of the surface.
            int position = 4 * (RenderSurfaceWidth * (RenderSurfaceHeight - 1) + RenderSurfaceWidth / 2);

            if (position + 4 > data.Length)
            {
                return new ThemeColor(1, 0, 0, 0);
            }

            float a = data[position + 3] / 255.0f;
            float r = data[position + 2] / 255.0f;
            float g = data[position + 1] / 255.0f;
            float b = data[position + 0] / 255.0f;

            return new ThemeColor(a, r, g, b);
        }
    }
}
