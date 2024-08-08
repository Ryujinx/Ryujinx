using Avalonia.Controls;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using Logger = Ryujinx.Common.Logging.Logger;

namespace Ryujinx.Ava.UI.Views.Settings
{
    public partial class SettingsGraphicsView : UserControl
    {
        public SettingsGraphicsView()
        {
            InitializeComponent();
        }

        private void GraphicsBackendMultithreadingIndex_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not ComboBox comboBox)
            {
                return;
            }

            if (comboBox.SelectedIndex != (int)ConfigurationState.Instance.Graphics.BackendThreading.Value && comboBox.SelectedIndex >= 0)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                    ContentDialogHelper.CreateInfoDialog(LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningMessage],
                        "",
                        "",
                        LocaleManager.Instance[LocaleKeys.InputDialogOk],
                        LocaleManager.Instance[LocaleKeys.DialogSettingsBackendThreadingWarningTitle],
                        parent: (Window)VisualRoot)
                );
            }
        }
    }
}
