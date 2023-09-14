using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.HLE.FileSystem;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Helper;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class TitleUpdateWindow : UserControl
    {
        public readonly TitleUpdateViewModel ViewModel;

        public TitleUpdateWindow()
        {
            DataContext = this;

            InitializeComponent();
        }

        public TitleUpdateWindow(VirtualFileSystem virtualFileSystem, ApplicationData applicationData)
        {
            DataContext = ViewModel = new TitleUpdateViewModel(virtualFileSystem, applicationData);

            InitializeComponent();
        }

        public static async Task Show(VirtualFileSystem virtualFileSystem, ApplicationData applicationData)
        {
            ContentDialog contentDialog = new()
            {
                PrimaryButtonText = "",
                SecondaryButtonText = "",
                CloseButtonText = "",
                Content = new TitleUpdateWindow(virtualFileSystem, applicationData),
                Title = LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.GameUpdateWindowHeading, applicationData.Name, applicationData.IdString),
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent).Hide();
        }

        public void Save(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime al)
            {
                foreach (Window window in al.Windows)
                {
                    if (window is MainWindow mainWindow)
                    {
                        mainWindow.LoadApplications();
                    }
                }
            }

            ((ContentDialog)Parent).Hide();
        }

        private void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is TitleUpdateModel model)
                {
                    OpenHelper.LocateFile(model.Path);
                }
            }
        }

        private void RemoveUpdate(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                ViewModel.RemoveUpdate((TitleUpdateModel)button.DataContext);
            }
        }

        private void RemoveAll(object sender, RoutedEventArgs e)
        {
            ViewModel.TitleUpdates.Clear();

            ViewModel.SortUpdates();
        }
    }
}
