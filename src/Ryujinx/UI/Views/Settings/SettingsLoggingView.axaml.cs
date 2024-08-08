using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsLoggingView : UserControl
    {
        public SettingsLoggingViewModel ViewModel;

        public SettingsLoggingView()
        {
            DataContext = ViewModel = new SettingsLoggingViewModel();
            InitializeComponent();
        }
    }
}
