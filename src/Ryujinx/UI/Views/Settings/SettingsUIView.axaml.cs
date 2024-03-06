using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsUiView : UserControl
    {
        public SettingsViewModel ViewModel;

        public SettingsUiView()
        {
            InitializeComponent();
        }

        private async void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            string path = PathBox.Text;

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !ViewModel.GameDirectories.Contains(path))
            {
                ViewModel.GameDirectories.Add(path);
                ViewModel.DirectoryChanged = true;
            }
            else
            {
                if (this.GetVisualRoot() is Window window)
                {
                    var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        AllowMultiple = false,
                    });

                    if (result.Count > 0)
                    {
                        ViewModel.GameDirectories.Add(result[0].Path.LocalPath);
                        ViewModel.DirectoryChanged = true;
                    }
                }
            }
        }

        private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
        {
            int oldIndex = GameList.SelectedIndex;

            foreach (string path in new List<string>(GameList.SelectedItems.Cast<string>()))
            {
                ViewModel.GameDirectories.Remove(path);
                ViewModel.DirectoryChanged = true;
            }

            if (GameList.ItemCount > 0)
            {
                GameList.SelectedIndex = oldIndex < GameList.ItemCount ? oldIndex : 0;
            }
        }
    }
}
