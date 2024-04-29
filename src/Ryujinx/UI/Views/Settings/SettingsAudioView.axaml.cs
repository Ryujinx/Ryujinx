using Avalonia.Controls;
using Ryujinx.Ava.UI.ViewModels.Settings;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsAudioView : UserControl
    {
        public SettingsAudioViewModel ViewModel;

        public SettingsAudioView()
        {
            DataContext = ViewModel = new SettingsAudioViewModel();
            InitializeComponent();
        }
    }
}
