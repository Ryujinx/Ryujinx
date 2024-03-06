using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using System;
using System.Diagnostics;

namespace Ryujinx.Ava
{
    public class App : Application
    {
        public override void Initialize()
        {
            Name = $"Ryujinx {Program.Version}";

            AvaloniaXamlLoader.Load(this);

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("/usr/bin/defaults", "write org.ryujinx.Ryujinx ApplePressAndHoldEnabled -bool false");
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();

            if (Program.PreviewerDetached)
            {
                ApplyConfiguredTheme();

                ConfigurationState.Instance.UI.BaseStyle.Event += ThemeChanged_Event;
                ConfigurationState.Instance.UI.CustomThemePath.Event += ThemeChanged_Event;
                ConfigurationState.Instance.UI.EnableCustomTheme.Event += CustomThemeChanged_Event;
            }
        }

        private void CustomThemeChanged_Event(object sender, ReactiveEventArgs<bool> e)
        {
            ApplyConfiguredTheme();
        }

        private void ShowRestartDialog()
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var result = await ContentDialogHelper.CreateConfirmationDialog(
                        LocaleManager.Instance[LocaleKeys.DialogThemeRestartMessage],
                        LocaleManager.Instance[LocaleKeys.DialogThemeRestartSubMessage],
                        LocaleManager.Instance[LocaleKeys.InputDialogYes],
                        LocaleManager.Instance[LocaleKeys.InputDialogNo],
                        LocaleManager.Instance[LocaleKeys.DialogRestartRequiredMessage]);

                    if (result == UserResult.Yes)
                    {
                        var path = Environment.ProcessPath;
                        var proc = Process.Start(path, CommandLineState.Arguments);
                        desktop.Shutdown();
                        Environment.Exit(0);
                    }
                }
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void ThemeChanged_Event(object sender, ReactiveEventArgs<string> e)
        {
            ApplyConfiguredTheme();
        }

        private void ApplyConfiguredTheme()
        {
            try
            {
                string baseStyle = ConfigurationState.Instance.UI.BaseStyle;

                if (string.IsNullOrWhiteSpace(baseStyle))
                {
                    ConfigurationState.Instance.UI.BaseStyle.Value = "Dark";

                    baseStyle = ConfigurationState.Instance.UI.BaseStyle;
                }

                RequestedThemeVariant = baseStyle switch
                {
                    "Light" => ThemeVariant.Light,
                    "Dark" => ThemeVariant.Dark,
                    _ => ThemeVariant.Default,
                };
            }
            catch (Exception)
            {
                Logger.Warning?.Print(LogClass.Application, "Failed to Apply Theme. A restart is needed to apply the selected theme");

                ShowRestartDialog();
            }
        }
    }
}
