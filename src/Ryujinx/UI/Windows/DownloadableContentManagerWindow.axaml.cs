using Avalonia.Controls;
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
    public partial class DownloadableContentManagerWindow : UserControl
    {
        public DownloadableContentManagerViewModel ViewModel;

        public DownloadableContentManagerWindow()
        {
            DataContext = this;

            InitializeComponent();
        }

        public DownloadableContentManagerWindow(VirtualFileSystem virtualFileSystem, ApplicationData applicationData)
        {
            DataContext = ViewModel = new DownloadableContentManagerViewModel(virtualFileSystem, applicationData);

            InitializeComponent();
        }

        public static async Task Show(VirtualFileSystem virtualFileSystem, ApplicationData applicationData)
        {
            ContentDialog contentDialog = new()
            {
                PrimaryButtonText = "",
                SecondaryButtonText = "",
                CloseButtonText = "",
                Content = new DownloadableContentManagerWindow(virtualFileSystem, applicationData),
                Title = string.Format(LocaleManager.Instance[LocaleKeys.DlcWindowTitle], applicationData.Name, applicationData.IdString),
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void SaveAndClose(object sender, RoutedEventArgs routedEventArgs)
        {
            ViewModel.Save();
            ((ContentDialog)Parent).Hide();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent).Hide();
        }

        private void RemoveDLC(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is DownloadableContentModel model)
                {
                    ViewModel.Remove(model);
                }
            }
        }

        private void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is DownloadableContentModel model)
                {
                    OpenHelper.LocateFile(model.ContainerPath);
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var content in e.AddedItems)
            {
                if (content is DownloadableContentModel model)
                {
                    var index = ViewModel.DownloadableContents.IndexOf(model);

                    if (index != -1)
                    {
                        ViewModel.DownloadableContents[index].Enabled = true;
                    }
                }
            }

            foreach (var content in e.RemovedItems)
            {
                if (content is DownloadableContentModel model)
                {
                    var index = ViewModel.DownloadableContents.IndexOf(model);

                    if (index != -1)
                    {
                        ViewModel.DownloadableContents[index].Enabled = false;
                    }
                }
            }
        }
    }
}
