using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsCpuView : UserControl
    {
        public SettingsCpuViewModel ViewModel;

        public SettingsCpuView()
        {
            DataContext = ViewModel = new SettingsCpuViewModel();
            InitializeComponent();
        }
    }
}
