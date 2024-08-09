using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Helper;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class AboutWindow : UserControl
    {
        public AboutWindow()
        {
            DataContext = new AboutWindowViewModel();

            InitializeComponent();
        }

        public static async Task Show()
        {
            ContentDialog contentDialog = new()
            {
                PrimaryButtonText = "",
                SecondaryButtonText = "",
                CloseButtonText = LocaleManager.Instance[LocaleKeys.UserProfilesClose],
                Content = new AboutWindow(),
            };

            Style closeButton = new(x => x.Name("CloseButton"));
            closeButton.Setters.Add(new Setter(WidthProperty, 80d));

            Style closeButtonParent = new(x => x.Name("CommandSpace"));
            closeButtonParent.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Right));

            contentDialog.Styles.Add(closeButton);
            contentDialog.Styles.Add(closeButtonParent);

            await ContentDialogHelper.ShowAsync(contentDialog);
        }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                OpenHelper.OpenUrl(button.Tag.ToString());
            }
        }

        private void OpenChangelogWindow(object sender, RoutedEventArgs e)
        {
            ChangelogWindow changelogWindow = new ChangelogWindow();

            // Find the parent window to use as the owner for the modal dialog
            var parentWindow = this.FindAncestorOfType<Window>();
            if (parentWindow != null)
            {
                changelogWindow.ShowDialog(parentWindow);  // Pass the parent window as the owner
            }
        }

        private void AmiiboLabel_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock)
            {
                OpenHelper.OpenUrl("https://amiiboapi.com");
            }
        }
    }
}
