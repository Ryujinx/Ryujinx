using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels.Settings;
using Ryujinx.Ava.UI.Views.Settings;
using Ryujinx.HLE.FileSystem;
using System;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class SettingsWindow : StyleableWindow
    {
        private SettingsViewModel ViewModel { get; }

        public readonly SettingsUiView UiPage;
        public readonly SettingsInputView InputPage;
        public readonly SettingsHotkeysView HotkeysPage;
        public readonly SettingsSystemView SystemPage;
        public readonly SettingsCpuView CpuPage;
        public readonly SettingsGraphicsView GraphicsPage;
        public readonly SettingsAudioView AudioPage;
        public readonly SettingsNetworkView NetworkPage;
        public readonly SettingsLoggingView LoggingPage;

        public SettingsWindow(VirtualFileSystem virtualFileSystem, ContentManager contentManager)
        {
            Title = $"{LocaleManager.Instance[LocaleKeys.Settings]}";

            AudioPage = new SettingsAudioView();
            CpuPage = new SettingsCpuView();
            GraphicsPage = new SettingsGraphicsView();
            HotkeysPage = new SettingsHotkeysView();
            InputPage = new SettingsInputView();
            LoggingPage = new SettingsLoggingView();
            NetworkPage = new SettingsNetworkView();
            SystemPage = new SettingsSystemView(virtualFileSystem, contentManager);
            UiPage = new SettingsUiView();

            ViewModel = new SettingsViewModel(
                AudioPage.ViewModel,
                CpuPage.ViewModel,
                GraphicsPage.ViewModel,
                HotkeysPage.ViewModel,
                InputPage.ViewModel,
                LoggingPage.ViewModel,
                NetworkPage.ViewModel,
                SystemPage.ViewModel,
                UiPage.ViewModel);

            DataContext = ViewModel;

            ViewModel.CloseWindow += Close;
            ViewModel.DirtyEvent += UpdateDirtyTitle;
            ViewModel.ToggleButtons += ToggleButtons;

            InitializeComponent();
            Load();
        }

        private void UpdateDirtyTitle(bool isDirty)
        {
            if (!IsInitialized)
            {
                return;
            }

            if (isDirty)
            {
                Title = $"{LocaleManager.Instance[LocaleKeys.Settings]} - {LocaleManager.Instance[LocaleKeys.SettingsDirty]}";
                Apply.IsEnabled = true;
            }
            else
            {
                Title = $"{LocaleManager.Instance[LocaleKeys.Settings]}";
                Apply.IsEnabled = false;
            }
        }

        private void ToggleButtons(bool enable)
        {
            Buttons.IsEnabled = enable;
        }

        private void Load()
        {
            NavPanel.SelectionChanged += NavPanelOnSelectionChanged;
            NavPanel.SelectedItem = NavPanel.MenuItems.ElementAt(0);
        }

        private void NavPanelOnSelectionChanged(object sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is NavigationViewItem navItem && navItem.Tag is not null)
            {
                switch (navItem.Tag.ToString())
                {
                    case "UiPage":
                        NavPanel.Content = UiPage;
                        break;
                    case "InputPage":
                        NavPanel.Content = InputPage;
                        break;
                    case "HotkeysPage":
                        NavPanel.Content = HotkeysPage;
                        break;
                    case "SystemPage":
                        NavPanel.Content = SystemPage;
                        break;
                    case "CpuPage":
                        NavPanel.Content = CpuPage;
                        break;
                    case "GraphicsPage":
                        NavPanel.Content = GraphicsPage;
                        break;
                    case "AudioPage":
                        NavPanel.Content = AudioPage;
                        break;
                    case "NetworkPage":
                        NavPanel.Content = NetworkPage;
                        break;
                    case "LoggingPage":
                        NavPanel.Content = LoggingPage;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private async void Cancel_OnClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsModified)
            {
                var result = await ContentDialogHelper.CreateConfirmationDialog(
                    LocaleManager.Instance[LocaleKeys.DialogSettingsUnsavedChangesMessage],
                    LocaleManager.Instance[LocaleKeys.DialogSettingsUnsavedChangesSubMessage],
                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                    LocaleManager.Instance[LocaleKeys.RyujinxConfirm],
                    parent: this);

                if (result != UserResult.Yes)
                {
                    return;
                }
            }

            Close();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (Owner is MainWindow window && UiPage.ViewModel.DirsChanged)
            {
                window.LoadApplications();
            }

            HotkeysPage.Dispose();
            InputPage.Dispose();
            base.OnClosing(e);
        }
    }
}
