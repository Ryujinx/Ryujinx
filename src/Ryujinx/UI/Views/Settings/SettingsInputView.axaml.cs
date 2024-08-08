using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsInputView : UserControl
    {
        public SettingsInputViewModel ViewModel;

        public SettingsInputView()
        {
            DataContext = ViewModel = new SettingsInputViewModel(this);

            InitializeComponent();
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
