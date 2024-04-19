using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        private SettingsInputViewModel ViewModel { get; set; }

        public SettingsInputView(SettingsViewModel viewModel)
        {
            DataContext = ViewModel = new SettingsInputViewModel(this, viewModel);

            InitializeComponent();
        }

        public void SaveCurrentProfile()
        {
            ViewModel.Save();
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
