using Avalonia.Media;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.UI;
using System;

namespace Ryujinx.Ava.UI.Applet
{
    class AvaloniaHostUITheme : IHostUITheme
    {
        public AvaloniaHostUITheme(MainWindow parent)
        {
            FontFamily = OperatingSystem.IsWindows() && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) ? "Segoe UI Variable" : parent.FontFamily.Name;
            DefaultBackgroundColor = BrushToThemeColor(parent.Background);
            DefaultForegroundColor = BrushToThemeColor(parent.Foreground);
            DefaultBorderColor = BrushToThemeColor(parent.BorderBrush);
            SelectionBackgroundColor = BrushToThemeColor(parent.ViewControls.SearchBox.SelectionBrush);
            SelectionForegroundColor = BrushToThemeColor(parent.ViewControls.SearchBox.SelectionForegroundBrush);
        }

        public string FontFamily { get; }

        public ThemeColor DefaultBackgroundColor { get; }
        public ThemeColor DefaultForegroundColor { get; }
        public ThemeColor DefaultBorderColor { get; }
        public ThemeColor SelectionBackgroundColor { get; }
        public ThemeColor SelectionForegroundColor { get; }

        private static ThemeColor BrushToThemeColor(IBrush brush)
        {
            if (brush is SolidColorBrush solidColor)
            {
                return new ThemeColor((float)solidColor.Color.A / 255,
                    (float)solidColor.Color.R / 255,
                    (float)solidColor.Color.G / 255,
                    (float)solidColor.Color.B / 255);
            }

            return new ThemeColor();
        }
    }
}
