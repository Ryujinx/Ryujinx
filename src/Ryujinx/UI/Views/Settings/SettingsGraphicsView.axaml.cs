using Avalonia.Controls;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels.Settings;
using Ryujinx.UI.Common.Configuration;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsGraphicsView : UserControl
    {
        public SettingsGraphicsViewModel ViewModel;

        public SettingsGraphicsView()
        {
            DataContext = ViewModel = new SettingsGraphicsViewModel();
            InitializeComponent();
        }

        private void GraphicsBackendMultithreadingIndex_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not ComboBox comboBox)
            {
                return;
            }

            if (comboBox.SelectedIndex != (int)ConfigurationState.Instance.Graphics.BackendThreading.Value)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                    ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningMessage],
                        "",
                        "",
                        LocaleManager.Instance[LocaleKeys.InputDialogOk],
                        LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningTitle],
                        parent: this.VisualRoot as Window)
                );
            }
        }
    }
}
