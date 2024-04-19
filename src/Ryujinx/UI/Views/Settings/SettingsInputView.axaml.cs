using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        private readonly SettingsInputViewModel _viewModel;

        public SettingsInputView(SettingsViewModel viewModel)
        {
            DataContext = _viewModel = new SettingsInputViewModel(this, viewModel);

            InitializeComponent();
        }

        public void SaveCurrentProfile()
        {
            _viewModel.Save();
        }

        public void Dispose()
        {
            _viewModel.Dispose();
        }
    }
}
