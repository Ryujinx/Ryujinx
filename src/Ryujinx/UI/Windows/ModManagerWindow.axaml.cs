using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.UI.Common.Helper;
using System.Threading.Tasks;
using Button = Avalonia.Controls.Button;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class ModManagerWindow : UserControl
    {
        public ModManagerViewModel ViewModel;

        public ModManagerWindow()
        {
            DataContext = this;

            InitializeComponent();
        }

        public ModManagerWindow(ulong titleId)
        {
            DataContext = ViewModel = new ModManagerViewModel(titleId);

            InitializeComponent();
        }

        public static async Task Show(ulong titleId, string titleName)
        {
            ContentDialog contentDialog = new()
            {
                PrimaryButtonText = "",
                SecondaryButtonText = "",
                CloseButtonText = "",
                Content = new ModManagerWindow(titleId),
                Title = string.Format(LocaleManager.Instance[LocaleKeys.ModWindowHeading], titleName, titleId.ToString("X16")),
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void SaveAndClose(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();
            ((ContentDialog)Parent).Hide();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent).Hide();
        }

        private async void DeleteMod(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is ModModel model)
                {
                    var result = await ContentDialogHelper.CreateConfirmationDialog(
                        LocaleManager.Instance[LocaleKeys.DialogWarning],
                        LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogModManagerDeletionWarningMessage, model.Name),
                        LocaleManager.Instance[LocaleKeys.InputDialogYes],
                        LocaleManager.Instance[LocaleKeys.InputDialogNo],
                        LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                    if (result == UserResult.Yes)
                    {
                        ViewModel.Delete(model);
                    }
                }
            }
        }

        private async void DeleteAll(object sender, RoutedEventArgs e)
        {
            var result = await ContentDialogHelper.CreateConfirmationDialog(
                LocaleManager.Instance[LocaleKeys.DialogWarning],
                LocaleManager.Instance[LocaleKeys.DialogModManagerDeletionAllWarningMessage],
                LocaleManager.Instance[LocaleKeys.InputDialogYes],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

            if (result == UserResult.Yes)
            {
                ViewModel.DeleteAll();
            }
        }

        private void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is ModModel model)
                {
                    OpenHelper.OpenFolder(model.Path);
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var content in e.AddedItems)
            {
                if (content is ModModel model)
                {
                    var index = ViewModel.Mods.IndexOf(model);

                    if (index != -1)
                    {
                        ViewModel.Mods[index].Enabled = true;
                    }
                }
            }

            foreach (var content in e.RemovedItems)
            {
                if (content is ModModel model)
                {
                    var index = ViewModel.Mods.IndexOf(model);

                    if (index != -1)
                    {
                        ViewModel.Mods[index].Enabled = false;
                    }
                }
            }
        }
    }
}
