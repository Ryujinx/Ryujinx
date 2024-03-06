using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.UI.Common.Configuration;
using System.IO;
using System.Reflection;

namespace Ryujinx.Ava.UI.Windows
{
    public class StyleableWindow : Window
    {
        public Bitmap IconImage { get; set; }

        public StyleableWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };

            using Stream stream = Assembly.GetAssembly(typeof(ConfigurationState)).GetManifestResourceStream("Ryujinx.UI.Common.Resources.Logo_Ryujinx.png");

            Icon = new WindowIcon(stream);
            stream.Position = 0;
            IconImage = new Bitmap(stream);

            LocaleManager.Instance.LocaleChanged += LocaleChanged;
            LocaleChanged();
        }

        private void LocaleChanged()
        {
            FlowDirection = LocaleManager.Instance.IsRTL() ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.SystemChrome | ExtendClientAreaChromeHints.OSXThickTitleBar;
        }
    }
}
