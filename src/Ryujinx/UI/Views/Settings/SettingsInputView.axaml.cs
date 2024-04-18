using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.ViewModels.Input;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        private InputViewModel ViewModel { get; set; }

        public SettingsInputView(SettingsViewModel viewModel)
        {
            DataContext = ViewModel = new InputViewModel(this, viewModel);

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
