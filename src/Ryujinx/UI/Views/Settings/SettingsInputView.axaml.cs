using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        public SettingsViewModel ViewModel;

        public SettingsInputView(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
        }

        public void Dispose()
        {
            InputView.Dispose();
        }
    }
}
