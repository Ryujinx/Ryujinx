using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Applet;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.Input.HLE;
using Ryujinx.Input.SDL2;
using Ryujinx.Modules;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Windows
{
    public partial class MainWindow : StyleableWindow
    {
        internal static MainWindowViewModel MainWindowViewModel { get; private set; }

        private bool _isLoading;

        private UserChannelPersistence _userChannelPersistence;
        private static bool _deferLoad;
        private static string _launchPath;
        private static string _launchApplicationId;
        private static bool _startFullscreen;
        internal readonly AvaHostUIHandler UiHandler;

        public VirtualFileSystem VirtualFileSystem { get; private set; }
        public ContentManager ContentManager { get; private set; }
        public AccountManager AccountManager { get; private set; }

        public LibHacHorizonManager LibHacHorizonManager { get; private set; }

        public InputManager InputManager { get; private set; }

        internal MainWindowViewModel ViewModel { get; private set; }
        public SettingsWindow SettingsWindow { get; set; }

        public static bool ShowKeyErrorOnLoad { get; set; }
        public ApplicationLibrary ApplicationLibrary { get; set; }

        public readonly double StatusBarHeight;
        public readonly double MenuBarHeight;

        public MainWindow()
        {
            ViewModel = new MainWindowViewModel();

            MainWindowViewModel = ViewModel;

            DataContext = ViewModel;

            InitializeComponent();
            Load();

            UiHandler = new AvaHostUIHandler(this);

            ViewModel.Title = $"Ryujinx {Program.Version}";

            // NOTE: Height of MenuBar and StatusBar is not usable here, since it would still be 0 at this point.
            StatusBarHeight = StatusBarView.StatusBar.MinHeight;
            MenuBarHeight = MenuBar.MinHeight;
            double barHeight = MenuBarHeight + StatusBarHeight;
            Height = ((Height - barHeight) / Program.WindowScaleFactor) + barHeight;
            Width /= Program.WindowScaleFactor;

            SetWindowSizePosition();

            if (Program.PreviewerDetached)
            {
                InputManager = new InputManager(new AvaloniaKeyboardDriver(this), new SDL2GamepadDriver());

                this.GetObservable(IsActiveProperty).Subscribe(IsActiveChanged);
                this.ScalingChanged += OnScalingChanged;
            }
        }

        /// <summary>
        /// Event handler for detecting OS theme change when using "Follow OS theme" option
        /// </summary>
        private void OnPlatformColorValuesChanged(object sender, PlatformColorValues e)
        {
            if (Application.Current is App app)
            {
                app.ApplyConfiguredTheme();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (PlatformSettings != null)
            {
                /// <summary>
                /// Unsubscribe to the ColorValuesChanged event
                /// </summary>
                PlatformSettings.ColorValuesChanged -= OnPlatformColorValuesChanged;
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            NotificationHelper.SetNotificationManager(this);
        }

        private void IsActiveChanged(bool obj)
        {
            ViewModel.IsActive = obj;
        }

        private void OnScalingChanged(object sender, EventArgs e)
        {
            Program.DesktopScaleFactor = this.RenderScaling;
        }

        private void ApplicationLibrary_ApplicationAdded(object sender, ApplicationAddedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.Applications.Add(e.AppData);
            });
        }

        private void ApplicationLibrary_ApplicationCountUpdated(object sender, ApplicationCountUpdatedEventArgs e)
        {
            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.StatusBarGamesLoaded, e.NumAppsLoaded, e.NumAppsFound);

            Dispatcher.UIThread.Post(() =>
            {
                ViewModel.StatusBarProgressValue = e.NumAppsLoaded;
                ViewModel.StatusBarProgressMaximum = e.NumAppsFound;

                if (e.NumAppsFound == 0)
                {
                    StatusBarView.LoadProgressBar.IsVisible = false;
                }

                if (e.NumAppsLoaded == e.NumAppsFound)
                {
                    StatusBarView.LoadProgressBar.IsVisible = false;
                }
            });
        }

        public void Application_Opened(object sender, ApplicationOpenedEventArgs args)
        {
            if (args.Application != null)
            {
                ViewModel.SelectedIcon = args.Application.Icon;

                ViewModel.LoadApplication(args.Application).Wait();
            }

            args.Handled = true;
        }

        internal static void DeferLoadApplication(string launchPathArg, string launchApplicationId, bool startFullscreenArg)
        {
            _deferLoad = true;
            _launchPath = launchPathArg;
            _launchApplicationId = launchApplicationId;
            _startFullscreen = startFullscreenArg;
        }

        public void SwitchToGameControl(bool startFullscreen = false)
        {
            ViewModel.ShowLoadProgress = false;
            ViewModel.ShowContent = true;
            ViewModel.IsLoadingIndeterminate = false;

            if (startFullscreen && ViewModel.WindowState != WindowState.FullScreen)
            {
                ViewModel.ToggleFullscreen();
            }
        }

        public void ShowLoading(bool startFullscreen = false)
        {
            ViewModel.ShowContent = false;
            ViewModel.ShowLoadProgress = true;
            ViewModel.IsLoadingIndeterminate = true;

            if (startFullscreen && ViewModel.WindowState != WindowState.FullScreen)
            {
                ViewModel.ToggleFullscreen();
            }
        }

        private void Initialize()
        {
            _userChannelPersistence = new UserChannelPersistence();
            VirtualFileSystem = VirtualFileSystem.CreateInstance();
            LibHacHorizonManager = new LibHacHorizonManager();
            ContentManager = new ContentManager(VirtualFileSystem);

            LibHacHorizonManager.InitializeFsServer(VirtualFileSystem);
            LibHacHorizonManager.InitializeArpServer();
            LibHacHorizonManager.InitializeBcatServer();
            LibHacHorizonManager.InitializeSystemClients();

            IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                ? IntegrityCheckLevel.ErrorOnInvalid
                : IntegrityCheckLevel.None;

            ApplicationLibrary = new ApplicationLibrary(VirtualFileSystem, checkLevel);

            // Save data created before we supported extra data in directory save data will not work properly if
            // given empty extra data. Luckily some of that extra data can be created using the data from the
            // save data indexer, which should be enough to check access permissions for user saves.
            // Every single save data's extra data will be checked and fixed if needed each time the emulator is opened.
            // Consider removing this at some point in the future when we don't need to worry about old saves.
            VirtualFileSystem.FixExtraData(LibHacHorizonManager.RyujinxClient);

            AccountManager = new AccountManager(LibHacHorizonManager.RyujinxClient, CommandLineState.Profile);

            VirtualFileSystem.ReloadKeySet();

            ApplicationHelper.Initialize(VirtualFileSystem, AccountManager, LibHacHorizonManager.RyujinxClient);
        }

        [SupportedOSPlatform("linux")]
        private static async Task ShowVmMaxMapCountWarning()
        {
            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LinuxVmMaxMapCountWarningTextSecondary,
                LinuxHelper.VmMaxMapCount, LinuxHelper.RecommendedVmMaxMapCount);

            await ContentDialogHelper.CreateWarningDialog(
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountWarningTextPrimary],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountWarningTextSecondary]
            );
        }

        [SupportedOSPlatform("linux")]
        private static async Task ShowVmMaxMapCountDialog()
        {
            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.LinuxVmMaxMapCountDialogTextPrimary,
                LinuxHelper.RecommendedVmMaxMapCount);

            UserResult response = await ContentDialogHelper.ShowTextDialog(
                $"Ryujinx - {LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogTitle]}",
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogTextPrimary],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogTextSecondary],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogButtonUntilRestart],
                LocaleManager.Instance[LocaleKeys.LinuxVmMaxMapCountDialogButtonPersistent],
                LocaleManager.Instance[LocaleKeys.InputDialogNo],
                (int)Symbol.Help
            );

            int rc;

            switch (response)
            {
                case UserResult.Ok:
                    rc = LinuxHelper.RunPkExec($"echo {LinuxHelper.RecommendedVmMaxMapCount} > {LinuxHelper.VmMaxMapCountPath}");
                    if (rc == 0)
                    {
                        Logger.Info?.Print(LogClass.Application, $"vm.max_map_count set to {LinuxHelper.VmMaxMapCount} until the next restart.");
                    }
                    else
                    {
                        Logger.Error?.Print(LogClass.Application, $"Unable to change vm.max_map_count. Process exited with code: {rc}");
                    }
                    break;
                case UserResult.No:
                    rc = LinuxHelper.RunPkExec($"echo \"vm.max_map_count = {LinuxHelper.RecommendedVmMaxMapCount}\" > {LinuxHelper.SysCtlConfigPath} && sysctl -p {LinuxHelper.SysCtlConfigPath}");
                    if (rc == 0)
                    {
                        Logger.Info?.Print(LogClass.Application, $"vm.max_map_count set to {LinuxHelper.VmMaxMapCount}. Written to config: {LinuxHelper.SysCtlConfigPath}");
                    }
                    else
                    {
                        Logger.Error?.Print(LogClass.Application, $"Unable to write new value for vm.max_map_count to config. Process exited with code: {rc}");
                    }
                    break;
            }
        }

        private async Task CheckLaunchState()
        {
            if (OperatingSystem.IsLinux() && LinuxHelper.VmMaxMapCount < LinuxHelper.RecommendedVmMaxMapCount)
            {
                Logger.Warning?.Print(LogClass.Application, $"The value of vm.max_map_count is lower than {LinuxHelper.RecommendedVmMaxMapCount}. ({LinuxHelper.VmMaxMapCount})");

                if (LinuxHelper.PkExecPath is not null)
                {
                    await Dispatcher.UIThread.InvokeAsync(ShowVmMaxMapCountDialog);
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(ShowVmMaxMapCountWarning);
                }
            }

            if (!ShowKeyErrorOnLoad)
            {
                if (_deferLoad)
                {
                    _deferLoad = false;

                    if (ApplicationLibrary.TryGetApplicationsFromFile(_launchPath, out List<ApplicationData> applications))
                    {
                        ApplicationData applicationData;

                        if (_launchApplicationId != null)
                        {
                            applicationData = applications.Find(application => application.IdString == _launchApplicationId);

                            if (applicationData != null)
                            {
                                await ViewModel.LoadApplication(applicationData, _startFullscreen);
                            }
                            else
                            {
                                Logger.Error?.Print(LogClass.Application, $"Couldn't find requested application id '{_launchApplicationId}' in '{_launchPath}'.");
                                await Dispatcher.UIThread.InvokeAsync(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.ApplicationNotFound));
                            }
                        }
                        else
                        {
                            applicationData = applications[0];
                            await ViewModel.LoadApplication(applicationData, _startFullscreen);
                        }
                    }
                    else
                    {
                        Logger.Error?.Print(LogClass.Application, $"Couldn't find any application in '{_launchPath}'.");
                        await Dispatcher.UIThread.InvokeAsync(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.ApplicationNotFound));
                    }
                }
            }
            else
            {
                ShowKeyErrorOnLoad = false;

                await Dispatcher.UIThread.InvokeAsync(async () => await UserErrorDialog.ShowUserErrorDialog(UserError.NoKeys));
            }

            if (ConfigurationState.Instance.CheckUpdatesOnStart.Value && Updater.CanUpdate(false))
            {
                await Updater.BeginParse(this, false).ContinueWith(task =>
                {
                    Logger.Error?.Print(LogClass.Application, $"Updater Error: {task.Exception}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private void Load()
        {
            StatusBarView.VolumeStatus.Click += VolumeStatus_CheckedChanged;

            ApplicationGrid.ApplicationOpened += Application_Opened;

            ApplicationGrid.DataContext = ViewModel;

            ApplicationList.ApplicationOpened += Application_Opened;

            ApplicationList.DataContext = ViewModel;
        }

        private void SetWindowSizePosition()
        {
            if (!ConfigurationState.Instance.RememberWindowState)
            {
                ViewModel.WindowHeight = (720 + StatusBarHeight + MenuBarHeight) * Program.WindowScaleFactor;
                ViewModel.WindowWidth = 1280 * Program.WindowScaleFactor;

                WindowState = WindowState.Normal;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

                return;
            }

            PixelPoint savedPoint = new(ConfigurationState.Instance.UI.WindowStartup.WindowPositionX,
                                        ConfigurationState.Instance.UI.WindowStartup.WindowPositionY);

            ViewModel.WindowHeight = ConfigurationState.Instance.UI.WindowStartup.WindowSizeHeight * Program.WindowScaleFactor;
            ViewModel.WindowWidth = ConfigurationState.Instance.UI.WindowStartup.WindowSizeWidth * Program.WindowScaleFactor;

            ViewModel.WindowState = ConfigurationState.Instance.UI.WindowStartup.WindowMaximized.Value ? WindowState.Maximized : WindowState.Normal;

            if (CheckScreenBounds(savedPoint))
            {
                Position = savedPoint;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private bool CheckScreenBounds(PixelPoint configPoint)
        {
            for (int i = 0; i < Screens.ScreenCount; i++)
            {
                if (Screens.All[i].Bounds.Contains(configPoint))
                {
                    return true;
                }
            }

            Logger.Warning?.Print(LogClass.Application, "Failed to find valid start-up coordinates. Defaulting to primary monitor center.");
            return false;
        }

        private void SaveWindowSizePosition()
        {
            ConfigurationState.Instance.UI.WindowStartup.WindowMaximized.Value = WindowState == WindowState.Maximized;

            // Only save rectangle properties if the window is not in a maximized state.
            if (WindowState != WindowState.Maximized)
            {
                ConfigurationState.Instance.UI.WindowStartup.WindowSizeHeight.Value = (int)Height;
                ConfigurationState.Instance.UI.WindowStartup.WindowSizeWidth.Value = (int)Width;

                ConfigurationState.Instance.UI.WindowStartup.WindowPositionX.Value = Position.X;
                ConfigurationState.Instance.UI.WindowStartup.WindowPositionY.Value = Position.Y;
            }

            MainWindowViewModel.SaveConfig();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            Initialize();

            /// <summary>
            /// Subscribe to the ColorValuesChanged event
            /// </summary>
            PlatformSettings.ColorValuesChanged += OnPlatformColorValuesChanged;

            ViewModel.Initialize(
                ContentManager,
                StorageProvider,
                ApplicationLibrary,
                VirtualFileSystem,
                AccountManager,
                InputManager,
                _userChannelPersistence,
                LibHacHorizonManager,
                UiHandler,
                ShowLoading,
                SwitchToGameControl,
                SetMainContent,
                this);

            ApplicationLibrary.ApplicationCountUpdated += ApplicationLibrary_ApplicationCountUpdated;
            ApplicationLibrary.ApplicationAdded += ApplicationLibrary_ApplicationAdded;

            ViewModel.RefreshFirmwareStatus();

            LoadApplications();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CheckLaunchState();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private void SetMainContent(Control content = null)
        {
            content ??= GameLibrary;

            if (MainContent.Content != content)
            {
                MainContent.Content = content;
            }
        }

        public static void UpdateGraphicsConfig()
        {
#pragma warning disable IDE0055 // Disable formatting
            GraphicsConfig.ResScale                   = ConfigurationState.Instance.Graphics.ResScale == -1 ? ConfigurationState.Instance.Graphics.ResScaleCustom : ConfigurationState.Instance.Graphics.ResScale;
            GraphicsConfig.MaxAnisotropy              = ConfigurationState.Instance.Graphics.MaxAnisotropy;
            GraphicsConfig.ShadersDumpPath            = ConfigurationState.Instance.Graphics.ShadersDumpPath;
            GraphicsConfig.EnableShaderCache          = ConfigurationState.Instance.Graphics.EnableShaderCache;
            GraphicsConfig.EnableTextureRecompression = ConfigurationState.Instance.Graphics.EnableTextureRecompression;
            GraphicsConfig.EnableMacroHLE             = ConfigurationState.Instance.Graphics.EnableMacroHLE;
#pragma warning restore IDE0055
        }

        private void VolumeStatus_CheckedChanged(object sender, RoutedEventArgs e)
        {
            var volumeSplitButton = sender as ToggleSplitButton;
            if (ViewModel.IsGameRunning)
            {
                if (!volumeSplitButton.IsChecked)
                {
                    ViewModel.AppHost.Device.SetVolume(ViewModel.VolumeBeforeMute);
                }
                else
                {
                    ViewModel.VolumeBeforeMute = ViewModel.AppHost.Device.GetVolume();
                    ViewModel.AppHost.Device.SetVolume(0);
                }

                ViewModel.Volume = ViewModel.AppHost.Device.GetVolume();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!ViewModel.IsClosing && ViewModel.AppHost != null && ConfigurationState.Instance.ShowConfirmExit)
            {
                e.Cancel = true;

                ConfirmExit();

                return;
            }

            ViewModel.IsClosing = true;

            if (ViewModel.AppHost != null)
            {
                ViewModel.AppHost.AppExit -= ViewModel.AppHost_AppExit;
                ViewModel.AppHost.AppExit += (sender, e) =>
                {
                    ViewModel.AppHost = null;

                    Dispatcher.UIThread.Post(() =>
                    {
                        MainContent = null;

                        Close();
                    });
                };
                ViewModel.AppHost?.Stop();

                e.Cancel = true;

                return;
            }

            if (ConfigurationState.Instance.RememberWindowState)
            {
                SaveWindowSizePosition();
            }

            ApplicationLibrary.CancelLoading();
            InputManager.Dispose();
            Program.Exit();

            base.OnClosing(e);
        }

        private void ConfirmExit()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ViewModel.IsClosing = await ContentDialogHelper.CreateExitDialog();

                if (ViewModel.IsClosing)
                {
                    Close();
                }
            });
        }

        public void LoadApplications()
        {
            ViewModel.Applications.Clear();

            StatusBarView.LoadProgressBar.IsVisible = true;
            ViewModel.StatusBarProgressMaximum = 0;
            ViewModel.StatusBarProgressValue = 0;

            LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.StatusBarGamesLoaded, 0, 0);

            ReloadGameList();
        }

        public void ToggleFileType(string fileType)
        {
            _ = fileType switch
            {
#pragma warning disable IDE0055 // Disable formatting
                "NSP"  => ConfigurationState.Instance.UI.ShownFileTypes.NSP.Value  = !ConfigurationState.Instance.UI.ShownFileTypes.NSP,
                "PFS0" => ConfigurationState.Instance.UI.ShownFileTypes.PFS0.Value = !ConfigurationState.Instance.UI.ShownFileTypes.PFS0,
                "XCI"  => ConfigurationState.Instance.UI.ShownFileTypes.XCI.Value  = !ConfigurationState.Instance.UI.ShownFileTypes.XCI,
                "NCA"  => ConfigurationState.Instance.UI.ShownFileTypes.NCA.Value  = !ConfigurationState.Instance.UI.ShownFileTypes.NCA,
                "NRO"  => ConfigurationState.Instance.UI.ShownFileTypes.NRO.Value  = !ConfigurationState.Instance.UI.ShownFileTypes.NRO,
                "NSO"  => ConfigurationState.Instance.UI.ShownFileTypes.NSO.Value  = !ConfigurationState.Instance.UI.ShownFileTypes.NSO,
                _  => throw new ArgumentOutOfRangeException(fileType),
#pragma warning restore IDE0055
            };

            ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);
            LoadApplications();
        }

        private void ReloadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            Thread applicationLibraryThread = new(() =>
            {
                ApplicationLibrary.LoadApplications(ConfigurationState.Instance.UI.GameDirs, ConfigurationState.Instance.System.Language);

                _isLoading = false;
            })
            {
                Name = "GUI.ApplicationLibraryThread",
                IsBackground = true,
            };
            applicationLibraryThread.Start();
        }
    }
}
