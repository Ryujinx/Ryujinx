using Avalonia.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.ViewModels.Input;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        public SettingsViewModel SettingsViewModel;

        private bool _dialogOpen;
        private InputViewModel ViewModel { get; set; }

        public SettingsInputView(SettingsViewModel viewModel)
        {
            SettingsViewModel = viewModel;

            DataContext = ViewModel = new InputViewModel(this, viewModel);

            InitializeComponent();
        }

        public void SaveCurrentProfile()
        {
            ViewModel.Save();
        }

        private async void PlayerIndexBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsViewModel.IsModified && !_dialogOpen)
            {
                _dialogOpen = true;

                var result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogControllerSettingsModifiedConfirmMessage],
                    LocaleManager.Instance[LocaleKeys.DialogControllerSettingsModifiedConfirmSubMessage],
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm]);

                if (result == UserResult.Yes)
                {
                    ViewModel.Save();
                }

                _dialogOpen = false;

                SettingsViewModel.IsModified = false;

                if (e.AddedItems.Count > 0)
                {
                    var player = (PlayerModel)e.AddedItems[0];
                    ViewModel.PlayerId = player.Id;
                }
            }
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
