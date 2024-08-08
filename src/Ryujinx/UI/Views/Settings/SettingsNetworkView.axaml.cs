using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsNetworkView : UserControl
    {
        public SettingsNetworkViewModel ViewModel;

        public SettingsNetworkView()
        {
            DataContext = ViewModel = new SettingsNetworkViewModel();
            InitializeComponent();
        }
    }
}
