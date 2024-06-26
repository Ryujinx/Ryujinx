using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using LibHac.Tools.FsSystem;
using Ryujinx.Audio.Backends.Dummy;
using Ryujinx.Audio.Backends.OpenAL;
using Ryujinx.Audio.Backends.SDL2;
using Ryujinx.Audio.Backends.SoundIo;
using Ryujinx.Audio.Integration;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Models;
using Ryujinx.Ava.UI.Renderer;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Multiplayer;
using Ryujinx.Common.Logging;
using Ryujinx.Common.SystemInterop;
using Ryujinx.Common.Utilities;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Multithreading;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.OpenGL;
using Ryujinx.Graphics.Vulkan;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services.Account.Acc;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.Input;
using Ryujinx.Input.HLE;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SPB.Graphics.Vulkan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Ryujinx.Ava.UI.Helpers.Win32NativeInterop;
using AntiAliasing = Ryujinx.Common.Configuration.AntiAliasing;
using Image = SixLabors.ImageSharp.Image;
using InputManager = Ryujinx.Input.HLE.InputManager;
using IRenderer = Ryujinx.Graphics.GAL.IRenderer;
using Key = Ryujinx.Input.Key;
using MouseButton = Ryujinx.Input.MouseButton;
using ScalingFilter = Ryujinx.Common.Configuration.ScalingFilter;
using Size = Avalonia.Size;
using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.Ava
{
    internal class AppHost
    {
        private const int CursorHideIdleTime = 5; // Hide Cursor seconds.
        private const float MaxResolutionScale = 4.0f; // Max resolution hotkeys can scale to before wrapping.
        private const int TargetFps = 60;
        private const float VolumeDelta = 0.05f;

        private static readonly Cursor _invisibleCursor = new(StandardCursorType.None);
        private readonly IntPtr _invisibleCursorWin;
        private readonly IntPtr _defaultCursorWin;

        private readonly long _ticksPerFrame;
        private readonly Stopwatch _chrono;
        private long _ticks;

        private readonly AccountManager _accountManager;
        private readonly UserChannelPersistence _userChannelPersistence;
        private readonly InputManager _inputManager;

        private readonly MainWindowViewModel _viewModel;
        private readonly IKeyboard _keyboardInterface;
        private readonly TopLevel _topLevel;
        public RendererHost RendererHost;

        private readonly GraphicsDebugLevel _glLogLevel;
        private float _newVolume;
        private KeyboardHotkeyState _prevHotkeyState;

        private long _lastCursorMoveTime;
        private bool _isCursorInRenderer = true;
        private bool _ignoreCursorState = false;

        private enum CursorStates
        {
            CursorIsHidden,
            CursorIsVisible,
            ForceChangeCursor
        };

        private CursorStates _cursorState = !ConfigurationState.Shared.Hid.EnableMouse.Value ?
            CursorStates.CursorIsVisible : CursorStates.CursorIsHidden;

        private bool _isStopped;
        private bool _isActive;
        private bool _renderingStarted;

        private readonly ManualResetEvent _gpuDoneEvent;

        private IRenderer _renderer;
        private readonly Thread _renderingThread;
        private readonly CancellationTokenSource _gpuCancellationTokenSource;
        private WindowsMultimediaTimerResolution _windowsMultimediaTimerResolution;

        private bool _dialogShown;
        private readonly bool _isFirmwareTitle;

        private bool UseTitleConfiguration
        {
            get
            {
                return ConfigurationState.HasConfigurationForTitle(_viewModel.SelectedApplication?.TitleId);
            }
        }

        private readonly object _lockObject = new();

        public event EventHandler AppExit;
        public event EventHandler<StatusInitEventArgs> StatusInitEvent;
        public event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;

        public VirtualFileSystem VirtualFileSystem { get; }
        public ContentManager ContentManager { get; }
        public NpadManager NpadManager { get; }
        public TouchScreenManager TouchScreenManager { get; }
        public Switch Device { get; set; }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public string ApplicationPath { get; private set; }
        public bool ScreenshotRequested { get; set; }

        public AppHost(
            RendererHost renderer,
            InputManager inputManager,
            string applicationPath,
            VirtualFileSystem virtualFileSystem,
            ContentManager contentManager,
            AccountManager accountManager,
            UserChannelPersistence userChannelPersistence,
            MainWindowViewModel viewmodel,
            TopLevel topLevel)
        {
            _viewModel = viewmodel;
            _inputManager = inputManager;
            _accountManager = accountManager;
            _userChannelPersistence = userChannelPersistence;
            _renderingThread = new Thread(RenderLoop) { Name = "GUI.RenderThread" };
            _lastCursorMoveTime = Stopwatch.GetTimestamp();
            _glLogLevel = ConfigurationState.Instance(UseTitleConfiguration).Logger.GraphicsDebugLevel;
            _topLevel = topLevel;

            _inputManager.SetMouseDriver(new AvaloniaMouseDriver(_topLevel, renderer));

            _keyboardInterface = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");

            NpadManager = _inputManager.CreateNpadManager();
            TouchScreenManager = _inputManager.CreateTouchScreenManager();
            ApplicationPath = applicationPath;
            VirtualFileSystem = virtualFileSystem;
            ContentManager = contentManager;

            RendererHost = renderer;

            _chrono = new Stopwatch();
            _ticksPerFrame = Stopwatch.Frequency / TargetFps;

            if (ApplicationPath.StartsWith("@SystemContent"))
            {
                ApplicationPath = VirtualFileSystem.SwitchPathToSystemPath(ApplicationPath);

                _isFirmwareTitle = true;
            }

            ConfigurationState.Shared.HideCursor.Event += HideCursorState_Changed;

            _topLevel.PointerMoved += TopLevel_PointerEnteredOrMoved;
            _topLevel.PointerEntered += TopLevel_PointerEnteredOrMoved;
            _topLevel.PointerExited += TopLevel_PointerExited;

            if (OperatingSystem.IsWindows())
            {
                _invisibleCursorWin = CreateEmptyCursor();
                _defaultCursorWin = CreateArrowCursor();
            }

            ConfigurationState.Instance(UseTitleConfiguration).System.IgnoreMissingServices.Event += UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance(UseTitleConfiguration).Graphics.AspectRatio.Event += UpdateAspectRatioState;
            ConfigurationState.Instance(UseTitleConfiguration).System.EnableDockedMode.Event += UpdateDockedModeState;
            ConfigurationState.Instance(UseTitleConfiguration).System.AudioVolume.Event += UpdateAudioVolumeState;
            ConfigurationState.Instance(UseTitleConfiguration).System.EnableDockedMode.Event += UpdateDockedModeState;
            ConfigurationState.Instance(UseTitleConfiguration).System.AudioVolume.Event += UpdateAudioVolumeState;
            ConfigurationState.Instance(UseTitleConfiguration).Graphics.AntiAliasing.Event += UpdateAntiAliasing;
            ConfigurationState.Instance(UseTitleConfiguration).Graphics.ScalingFilter.Event += UpdateScalingFilter;
            ConfigurationState.Instance(UseTitleConfiguration).Graphics.ScalingFilterLevel.Event += UpdateScalingFilterLevel;
            ConfigurationState.Instance(UseTitleConfiguration).Graphics.EnableColorSpacePassthrough.Event += UpdateColorSpacePassthrough;

            ConfigurationState.Instance(UseTitleConfiguration).System.EnableInternetAccess.Event += UpdateEnableInternetAccessState;
            ConfigurationState.Instance(UseTitleConfiguration).Multiplayer.LanInterfaceId.Event += UpdateLanInterfaceIdState;
            ConfigurationState.Instance(UseTitleConfiguration).Multiplayer.Mode.Event += UpdateMultiplayerModeState;

            _gpuCancellationTokenSource = new CancellationTokenSource();
            _gpuDoneEvent = new ManualResetEvent(false);
        }

        private void TopLevel_PointerEnteredOrMoved(object sender, PointerEventArgs e)
        {
            if (!_viewModel.IsActive)
            {
                _isCursorInRenderer = false;
                _ignoreCursorState = false;
                return;
            }

            if (sender is MainWindow window)
            {
                if (ConfigurationState.Shared.HideCursor.Value == HideCursorMode.OnIdle)
                {
                    _lastCursorMoveTime = Stopwatch.GetTimestamp();
                }

                var point = e.GetCurrentPoint(window).Position;
                var bounds = RendererHost.EmbeddedWindow.Bounds;
                var windowYOffset = bounds.Y + window.MenuBarHeight;
                var windowYLimit = (int)window.Bounds.Height - window.StatusBarHeight - 1;

                if (!_viewModel.ShowMenuAndStatusBar)
                {
                    windowYOffset -= window.MenuBarHeight;
                    windowYLimit += window.StatusBarHeight + 1;
                }

                _isCursorInRenderer = point.X >= bounds.X &&
                    Math.Ceiling(point.X) <= (int)window.Bounds.Width &&
                    point.Y >= windowYOffset &&
                    point.Y <= windowYLimit &&
                    !_viewModel.IsSubMenuOpen;

                _ignoreCursorState = false;
            }
        }

        private void TopLevel_PointerExited(object sender, PointerEventArgs e)
        {
            _isCursorInRenderer = false;

            if (sender is MainWindow window)
            {
                var point = e.GetCurrentPoint(window).Position;
                var bounds = RendererHost.EmbeddedWindow.Bounds;
                var windowYOffset = bounds.Y + window.MenuBarHeight;
                var windowYLimit = (int)window.Bounds.Height - window.StatusBarHeight - 1;

                if (!_viewModel.ShowMenuAndStatusBar)
                {
                    windowYOffset -= window.MenuBarHeight;
                    windowYLimit += window.StatusBarHeight + 1;
                }

                _ignoreCursorState = (point.X == bounds.X ||
                    Math.Ceiling(point.X) == (int)window.Bounds.Width) &&
                    point.Y >= windowYOffset &&
                    point.Y <= windowYLimit;
            }

            _cursorState = CursorStates.ForceChangeCursor;
        }

        private void UpdateScalingFilterLevel(object sender, ReactiveEventArgs<int> e)
        {
            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);
            _renderer.Window?.SetScalingFilter((Graphics.GAL.ScalingFilter)config.Graphics.ScalingFilter.Value);
            _renderer.Window?.SetScalingFilterLevel(config.Graphics.ScalingFilterLevel.Value);
        }

        private void UpdateScalingFilter(object sender, ReactiveEventArgs<ScalingFilter> e)
        {
            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);
            _renderer.Window?.SetScalingFilter((Graphics.GAL.ScalingFilter)config.Graphics.ScalingFilter.Value);
            _renderer.Window?.SetScalingFilterLevel(config.Graphics.ScalingFilterLevel.Value);
        }

        private void UpdateColorSpacePassthrough(object sender, ReactiveEventArgs<bool> e)
        {
            _renderer.Window?.SetColorSpacePassthrough((bool)ConfigurationState.Instance(UseTitleConfiguration).Graphics.EnableColorSpacePassthrough.Value);
        }

        private void ShowCursor()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _viewModel.Cursor = Cursor.Default;

                if (OperatingSystem.IsWindows())
                {
                    if (_cursorState != CursorStates.CursorIsHidden && !_ignoreCursorState)
                    {
                        SetCursor(_defaultCursorWin);
                    }
                }
            });

            _cursorState = CursorStates.CursorIsVisible;
        }

        private void HideCursor()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _viewModel.Cursor = _invisibleCursor;

                if (OperatingSystem.IsWindows())
                {
                    SetCursor(_invisibleCursorWin);
                }
            });

            _cursorState = CursorStates.CursorIsHidden;
        }

        private void SetRendererWindowSize(Size size)
        {
            if (_renderer != null)
            {
                double scale = _topLevel.RenderScaling;

                _renderer.Window?.SetSize((int)(size.Width * scale), (int)(size.Height * scale));
            }
        }

        private void Renderer_ScreenCaptured(object sender, ScreenCaptureImageInfo e)
        {
            if (e.Data.Length > 0 && e.Height > 0 && e.Width > 0)
            {
                Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        string applicationName = Device.Processes.ActiveApplication.Name;
                        string sanitizedApplicationName = FileSystemUtils.SanitizeFileName(applicationName);
                        DateTime currentTime = DateTime.Now;

                        string filename = $"{sanitizedApplicationName}_{currentTime.Year}-{currentTime.Month:D2}-{currentTime.Day:D2}_{currentTime.Hour:D2}-{currentTime.Minute:D2}-{currentTime.Second:D2}.png";

                        string directory = AppDataManager.Mode switch
                        {
                            AppDataManager.LaunchMode.Portable or AppDataManager.LaunchMode.Custom => Path.Combine(AppDataManager.BaseDirPath, "screenshots"),
                            _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Ryujinx"),
                        };

                        string path = Path.Combine(directory, filename);

                        try
                        {
                            Directory.CreateDirectory(directory);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Failed to create directory at path {directory}. Error : {ex.GetType().Name}", "Screenshot");

                            return;
                        }

                        Image image = e.IsBgra ? Image.LoadPixelData<Bgra32>(e.Data, e.Width, e.Height)
                                               : Image.LoadPixelData<Rgba32>(e.Data, e.Width, e.Height);

                        if (e.FlipX)
                        {
                            image.Mutate(x => x.Flip(FlipMode.Horizontal));
                        }

                        if (e.FlipY)
                        {
                            image.Mutate(x => x.Flip(FlipMode.Vertical));
                        }

                        image.SaveAsPng(path, new PngEncoder
                        {
                            ColorType = PngColorType.Rgb,
                        });

                        image.Dispose();

                        Logger.Notice.Print(LogClass.Application, $"Screenshot saved to {path}", "Screenshot");
                    }
                });
            }
            else
            {
                Logger.Error?.Print(LogClass.Application, $"Screenshot is empty. Size : {e.Data.Length} bytes. Resolution : {e.Width}x{e.Height}", "Screenshot");
            }
        }

        public void Start()
        {
            if (OperatingSystem.IsWindows())
            {
                _windowsMultimediaTimerResolution = new WindowsMultimediaTimerResolution(1);
            }

            DisplaySleep.Prevent();

            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);

            NpadManager.Initialize(Device, config.Hid.InputConfig, config.Hid.EnableKeyboard, config.Hid.EnableMouse);
            TouchScreenManager.Initialize(Device);

            _viewModel.IsGameRunning = true;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _viewModel.Title = TitleHelper.ActiveApplicationTitle(Device.Processes.ActiveApplication, Program.Version);
            });

            _viewModel.SetUiProgressHandlers(Device);

            RendererHost.BoundsChanged += Window_BoundsChanged;

            _isActive = true;

            _renderingThread.Start();

            _viewModel.Volume = config.System.AudioVolume.Value;

            MainLoop();

            Exit();
        }

        private void UpdateIgnoreMissingServicesState(object sender, ReactiveEventArgs<bool> args)
        {
            if (Device != null)
            {
                Device.Configuration.IgnoreMissingServices = args.NewValue;
            }
        }

        private void UpdateAspectRatioState(object sender, ReactiveEventArgs<AspectRatio> args)
        {
            if (Device != null)
            {
                Device.Configuration.AspectRatio = args.NewValue;
            }
        }

        private void UpdateAntiAliasing(object sender, ReactiveEventArgs<AntiAliasing> e)
        {
            _renderer?.Window?.SetAntiAliasing((Graphics.GAL.AntiAliasing)e.NewValue);
        }

        private void UpdateDockedModeState(object sender, ReactiveEventArgs<bool> e)
        {
            Device?.System.ChangeDockedModeState(e.NewValue);
        }

        private void UpdateAudioVolumeState(object sender, ReactiveEventArgs<float> e)
        {
            Device?.SetVolume(e.NewValue);

            Dispatcher.UIThread.Post(() =>
            {
                _viewModel.Volume = e.NewValue;
            });
        }

        private void UpdateEnableInternetAccessState(object sender, ReactiveEventArgs<bool> e)
        {
            Device.Configuration.EnableInternetAccess = e.NewValue;
        }

        private void UpdateLanInterfaceIdState(object sender, ReactiveEventArgs<string> e)
        {
            Device.Configuration.MultiplayerLanInterfaceId = e.NewValue;
        }

        private void UpdateMultiplayerModeState(object sender, ReactiveEventArgs<MultiplayerMode> e)
        {
            Device.Configuration.MultiplayerMode = e.NewValue;
        }

        public void ToggleVSync()
        {
            Device.EnableDeviceVsync = !Device.EnableDeviceVsync;
            _renderer.Window.ChangeVSyncMode(Device.EnableDeviceVsync);
        }

        public void Stop()
        {
            _isActive = false;
        }

        private void Exit()
        {
            (_keyboardInterface as AvaloniaKeyboard)?.Clear();

            if (_isStopped)
            {
                return;
            }

            _isStopped = true;
            _isActive = false;
        }

        public void DisposeContext()
        {
            Dispose();

            _isActive = false;

            // NOTE: The render loop is allowed to stay alive until the renderer itself is disposed, as it may handle resource dispose.
            // We only need to wait for all commands submitted during the main gpu loop to be processed.
            _gpuDoneEvent.WaitOne();
            _gpuDoneEvent.Dispose();

            DisplaySleep.Restore();

            NpadManager.Dispose();
            TouchScreenManager.Dispose();
            Device.Dispose();

            DisposeGpu();

            AppExit?.Invoke(this, EventArgs.Empty);
        }

        private void Dispose()
        {
            if (Device.Processes != null)
            {
                MainWindowViewModel.UpdateGameMetadata(Device.Processes.ActiveApplication.ProgramIdText);
            }

            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);

            config.System.IgnoreMissingServices.Event -= UpdateIgnoreMissingServicesState;
            config.Graphics.AspectRatio.Event -= UpdateAspectRatioState;
            config.System.EnableDockedMode.Event -= UpdateDockedModeState;
            config.System.AudioVolume.Event -= UpdateAudioVolumeState;
            config.Graphics.ScalingFilter.Event -= UpdateScalingFilter;
            config.Graphics.ScalingFilterLevel.Event -= UpdateScalingFilterLevel;
            config.Graphics.AntiAliasing.Event -= UpdateAntiAliasing;
            config.Graphics.EnableColorSpacePassthrough.Event -= UpdateColorSpacePassthrough;

            _topLevel.PointerMoved -= TopLevel_PointerEnteredOrMoved;
            _topLevel.PointerEntered -= TopLevel_PointerEnteredOrMoved;
            _topLevel.PointerExited -= TopLevel_PointerExited;

            _gpuCancellationTokenSource.Cancel();
            _gpuCancellationTokenSource.Dispose();

            _chrono.Stop();
        }

        public void DisposeGpu()
        {
            if (OperatingSystem.IsWindows())
            {
                _windowsMultimediaTimerResolution?.Dispose();
                _windowsMultimediaTimerResolution = null;
            }

            if (RendererHost.EmbeddedWindow is EmbeddedWindowOpenGL openGlWindow)
            {
                // Try to bind the OpenGL context before calling the shutdown event.
                openGlWindow.MakeCurrent(false, false);

                Device.DisposeGpu();

                // Unbind context and destroy everything.
                openGlWindow.MakeCurrent(true, false);
            }
            else
            {
                Device.DisposeGpu();
            }
        }

        private void HideCursorState_Changed(object sender, ReactiveEventArgs<HideCursorMode> state)
        {
            if (state.NewValue == HideCursorMode.OnIdle)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();
            }

            _cursorState = CursorStates.ForceChangeCursor;
        }

        public async Task<bool> LoadGuestApplication()
        {
            InitializeSwitchInstance();
            MainWindow.UpdateGraphicsConfig();

            SystemVersion firmwareVersion = ContentManager.GetCurrentFirmwareVersion();

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (!SetupValidator.CanStartApplication(ContentManager, ApplicationPath, out UserError userError))
                {
                    {
                        if (SetupValidator.CanFixStartApplication(ContentManager, ApplicationPath, userError, out firmwareVersion))
                        {
                            if (userError == UserError.NoFirmware)
                            {
                                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(
                                    LocaleManager.Instance[LocaleKeys.DialogFirmwareNoFirmwareInstalledMessage],
                                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallEmbeddedMessage, firmwareVersion.VersionString),
                                    LocaleManager.Instance[LocaleKeys.InputDialogYes],
                                    LocaleManager.Instance[LocaleKeys.InputDialogNo],
                                    "");

                                if (result != UserResult.Yes)
                                {
                                    await UserErrorDialog.ShowUserErrorDialog(userError);
                                    Device.Dispose();

                                    return false;
                                }
                            }

                            if (!SetupValidator.TryFixStartApplication(ContentManager, ApplicationPath, userError, out _))
                            {
                                await UserErrorDialog.ShowUserErrorDialog(userError);
                                Device.Dispose();

                                return false;
                            }

                            // Tell the user that we installed a firmware for them.
                            if (userError == UserError.NoFirmware)
                            {
                                firmwareVersion = ContentManager.GetCurrentFirmwareVersion();

                                _viewModel.RefreshFirmwareStatus();

                                await ContentDialogHelper.CreateInfoDialog(
                                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstalledMessage, firmwareVersion.VersionString),
                                    LocaleManager.Instance.UpdateAndGetDynamicValue(LocaleKeys.DialogFirmwareInstallEmbeddedSuccessMessage, firmwareVersion.VersionString),
                                    LocaleManager.Instance[LocaleKeys.InputDialogOk],
                                    "",
                                    LocaleManager.Instance[LocaleKeys.RyujinxInfo]);
                            }
                        }
                        else
                        {
                            await UserErrorDialog.ShowUserErrorDialog(userError);
                            Device.Dispose();

                            return false;
                        }
                    }
                }
            }

            Logger.Notice.Print(LogClass.Application, $"Using Firmware Version: {firmwareVersion?.VersionString}");

            if (_isFirmwareTitle)
            {
                Logger.Info?.Print(LogClass.Application, "Loading as Firmware Title (NCA).");

                if (!Device.LoadNca(ApplicationPath))
                {
                    Device.Dispose();

                    return false;
                }
            }
            else if (Directory.Exists(ApplicationPath))
            {
                string[] romFsFiles = Directory.GetFiles(ApplicationPath, "*.istorage");

                if (romFsFiles.Length == 0)
                {
                    romFsFiles = Directory.GetFiles(ApplicationPath, "*.romfs");
                }

                if (romFsFiles.Length > 0)
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart with RomFS.");

                    if (!Device.LoadCart(ApplicationPath, romFsFiles[0]))
                    {
                        Device.Dispose();

                        return false;
                    }
                }
                else
                {
                    Logger.Info?.Print(LogClass.Application, "Loading as cart WITHOUT RomFS.");

                    if (!Device.LoadCart(ApplicationPath))
                    {
                        Device.Dispose();

                        return false;
                    }
                }
            }
            else if (File.Exists(ApplicationPath))
            {
                switch (Path.GetExtension(ApplicationPath).ToLowerInvariant())
                {
                    case ".xci":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as XCI.");

                            if (!Device.LoadXci(ApplicationPath))
                            {
                                Device.Dispose();

                                return false;
                            }

                            break;
                        }
                    case ".nca":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NCA.");

                            if (!Device.LoadNca(ApplicationPath))
                            {
                                Device.Dispose();

                                return false;
                            }

                            break;
                        }
                    case ".nsp":
                    case ".pfs0":
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as NSP.");

                            if (!Device.LoadNsp(ApplicationPath))
                            {
                                Device.Dispose();

                                return false;
                            }

                            break;
                        }
                    default:
                        {
                            Logger.Info?.Print(LogClass.Application, "Loading as homebrew.");

                            try
                            {
                                if (!Device.LoadProgram(ApplicationPath))
                                {
                                    Device.Dispose();

                                    return false;
                                }
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                Logger.Error?.Print(LogClass.Application, "The specified file is not supported by Ryujinx.");

                                Device.Dispose();

                                return false;
                            }

                            break;
                        }
                }
            }
            else
            {
                Logger.Warning?.Print(LogClass.Application, "Please specify a valid XCI/NCA/NSP/PFS0/NRO file.");

                Device.Dispose();

                return false;
            }

            DiscordIntegrationModule.SwitchToPlayingState(Device.Processes.ActiveApplication.ProgramIdText, Device.Processes.ActiveApplication.Name);

            ApplicationLibrary.LoadAndSaveMetaData(Device.Processes.ActiveApplication.ProgramIdText, appMetadata =>
            {
                appMetadata.UpdatePreGame();
            });

            return true;
        }

        internal void Resume()
        {
            Device?.System.TogglePauseEmulation(false);

            _viewModel.IsPaused = false;
            _viewModel.Title = TitleHelper.ActiveApplicationTitle(Device?.Processes.ActiveApplication, Program.Version);
            Logger.Info?.Print(LogClass.Emulation, "Emulation was resumed");
        }

        internal void Pause()
        {
            Device?.System.TogglePauseEmulation(true);

            _viewModel.IsPaused = true;
            _viewModel.Title = TitleHelper.ActiveApplicationTitle(Device?.Processes.ActiveApplication, Program.Version, LocaleManager.Instance[LocaleKeys.Paused]);
            Logger.Info?.Print(LogClass.Emulation, "Emulation was paused");
        }

        private void InitializeSwitchInstance()
        {
            // Initialize KeySet.
            VirtualFileSystem.ReloadKeySet();

            // Initialize Renderer.
            IRenderer renderer;

            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);

            if (config.Graphics.GraphicsBackend.Value == GraphicsBackend.Vulkan)
            {
                renderer = new VulkanRenderer(
                    Vk.GetApi(),
                    (RendererHost.EmbeddedWindow as EmbeddedWindowVulkan).CreateSurface,
                    VulkanHelper.GetRequiredInstanceExtensions,
                    config.Graphics.PreferredGpu.Value);
            }
            else
            {
                renderer = new OpenGLRenderer();
            }

            BackendThreading threadingMode = config.Graphics.BackendThreading;

            var isGALThreaded = threadingMode == BackendThreading.On || (threadingMode == BackendThreading.Auto && renderer.PreferThreading);
            if (isGALThreaded)
            {
                renderer = new ThreadedRenderer(renderer);
            }

            Logger.Info?.PrintMsg(LogClass.Gpu, $"Backend Threading ({threadingMode}): {isGALThreaded}");

            // Initialize Configuration.
            var memoryConfiguration = config.System.ExpandRam.Value ? MemoryConfiguration.MemoryConfiguration6GiB : MemoryConfiguration.MemoryConfiguration4GiB;

            HLEConfiguration configuration = new(VirtualFileSystem,
                                                 _viewModel.LibHacHorizonManager,
                                                 ContentManager,
                                                 _accountManager,
                                                 _userChannelPersistence,
                                                 renderer,
                                                 InitializeAudio(),
                                                 memoryConfiguration,
                                                 _viewModel.UiHandler,
                                                 (SystemLanguage)config.System.Language.Value,
                                                 (RegionCode)config.System.Region.Value,
                                                 config.Graphics.EnableVsync,
                                                 config.System.EnableDockedMode,
                                                 config.System.EnablePtc,
                                                 config.System.EnableInternetAccess,
                                                 config.System.EnableFsIntegrityChecks ? IntegrityCheckLevel.ErrorOnInvalid : IntegrityCheckLevel.None,
                                                 config.System.FsGlobalAccessLogMode,
                                                 config.System.SystemTimeOffset,
                                                 config.System.TimeZone,
                                                 config.System.MemoryManagerMode,
                                                 config.System.IgnoreMissingServices,
                                                 config.Graphics.AspectRatio,
                                                 config.System.AudioVolume,
                                                 config.System.UseHypervisor,
                                                 config.Multiplayer.LanInterfaceId.Value,
                                                 config.Multiplayer.Mode);

            Device = new Switch(configuration);
        }

        private IHardwareDeviceDriver InitializeAudio()
        {
            var availableBackends = new List<AudioBackend>
            {
                AudioBackend.SDL2,
                AudioBackend.SoundIo,
                AudioBackend.OpenAl,
                AudioBackend.Dummy,
            };

            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);

            AudioBackend preferredBackend = config.System.AudioBackend.Value;

            for (int i = 0; i < availableBackends.Count; i++)
            {
                if (availableBackends[i] == preferredBackend)
                {
                    availableBackends.RemoveAt(i);
                    availableBackends.Insert(0, preferredBackend);
                    break;
                }
            }

            static IHardwareDeviceDriver InitializeAudioBackend<T>(AudioBackend backend, AudioBackend nextBackend) where T : IHardwareDeviceDriver, new()
            {
                if (T.IsSupported)
                {
                    return new T();
                }

                Logger.Warning?.Print(LogClass.Audio, $"{backend} is not supported, falling back to {nextBackend}.");

                return null;
            }

            IHardwareDeviceDriver deviceDriver = null;

            for (int i = 0; i < availableBackends.Count; i++)
            {
                AudioBackend currentBackend = availableBackends[i];
                AudioBackend nextBackend = i + 1 < availableBackends.Count ? availableBackends[i + 1] : AudioBackend.Dummy;

                deviceDriver = currentBackend switch
                {
                    AudioBackend.SDL2 => InitializeAudioBackend<SDL2HardwareDeviceDriver>(AudioBackend.SDL2, nextBackend),
                    AudioBackend.SoundIo => InitializeAudioBackend<SoundIoHardwareDeviceDriver>(AudioBackend.SoundIo, nextBackend),
                    AudioBackend.OpenAl => InitializeAudioBackend<OpenALHardwareDeviceDriver>(AudioBackend.OpenAl, nextBackend),
                    _ => new DummyHardwareDeviceDriver(),
                };

                if (deviceDriver != null)
                {
                    config.System.AudioBackend.Value = currentBackend;
                    break;
                }
            }

            MainWindowViewModel.SaveConfig();

            return deviceDriver;
        }

        private void Window_BoundsChanged(object sender, Size e)
        {
            Width = (int)e.Width;
            Height = (int)e.Height;

            SetRendererWindowSize(e);
        }

        private void MainLoop()
        {
            while (_isActive)
            {
                UpdateFrame();

                // Polling becomes expensive if it's not slept.
                Thread.Sleep(1);
            }
        }

        private void RenderLoop()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_viewModel.StartGamesInFullscreen)
                {
                    _viewModel.WindowState = WindowState.FullScreen;
                }

                if (_viewModel.WindowState == WindowState.FullScreen)
                {
                    _viewModel.ShowMenuAndStatusBar = false;
                }
            });

            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);

            _renderer = Device.Gpu.Renderer is ThreadedRenderer tr ? tr.BaseRenderer : Device.Gpu.Renderer;

            _renderer.ScreenCaptured += Renderer_ScreenCaptured;

            (RendererHost.EmbeddedWindow as EmbeddedWindowOpenGL)?.InitializeBackgroundContext(_renderer);

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            _renderer?.Window?.SetAntiAliasing((Graphics.GAL.AntiAliasing)config.Graphics.AntiAliasing.Value);
            _renderer?.Window?.SetScalingFilter((Graphics.GAL.ScalingFilter)config.Graphics.ScalingFilter.Value);
            _renderer?.Window?.SetScalingFilterLevel(config.Graphics.ScalingFilterLevel.Value);
            _renderer?.Window?.SetColorSpacePassthrough(config.Graphics.EnableColorSpacePassthrough.Value);

            Width = (int)RendererHost.Bounds.Width;
            Height = (int)RendererHost.Bounds.Height;

            _renderer.Window.SetSize((int)(Width * _topLevel.RenderScaling), (int)(Height * _topLevel.RenderScaling));

            _chrono.Start();

            Device.Gpu.Renderer.RunLoop(() =>
            {
                Device.Gpu.SetGpuThread();
                Device.Gpu.InitializeShaderCache(_gpuCancellationTokenSource.Token);

                _renderer.Window.ChangeVSyncMode(Device.EnableDeviceVsync);

                while (_isActive)
                {
                    _ticks += _chrono.ElapsedTicks;

                    _chrono.Restart();

                    if (Device.WaitFifo())
                    {
                        Device.Statistics.RecordFifoStart();
                        Device.ProcessFrame();
                        Device.Statistics.RecordFifoEnd();
                    }

                    while (Device.ConsumeFrameAvailable())
                    {
                        if (!_renderingStarted)
                        {
                            _renderingStarted = true;
                            _viewModel.SwitchToRenderer(false);
                            InitStatus();
                        }

                        Device.PresentFrame(() => (RendererHost.EmbeddedWindow as EmbeddedWindowOpenGL)?.SwapBuffers());
                    }

                    if (_ticks >= _ticksPerFrame)
                    {
                        UpdateStatus();
                    }
                }

                // Make sure all commands in the run loop are fully executed before leaving the loop.
                if (Device.Gpu.Renderer is ThreadedRenderer threaded)
                {
                    threaded.FlushThreadedCommands();
                }

                _gpuDoneEvent.Set();
            });

            (RendererHost.EmbeddedWindow as EmbeddedWindowOpenGL)?.MakeCurrent(true);
        }

        public void InitStatus()
        {
            StatusInitEvent?.Invoke(this, new StatusInitEventArgs(
                ConfigurationState.Instance(UseTitleConfiguration).Graphics.GraphicsBackend.Value switch
                {
                    GraphicsBackend.Vulkan => "Vulkan",
                    GraphicsBackend.OpenGl => "OpenGL",
                    _ => throw new NotImplementedException()
                },
                $"GPU: {_renderer.GetHardwareInfo().GpuDriver}"));
        }

        public void UpdateStatus()
        {
            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);
            // Run a status update only when a frame is to be drawn. This prevents from updating the ui and wasting a render when no frame is queued.
            string dockedMode = config.System.EnableDockedMode ? LocaleManager.Instance[LocaleKeys.Docked] : LocaleManager.Instance[LocaleKeys.Handheld];

            if (GraphicsConfig.ResScale != 1)
            {
                dockedMode += $" ({GraphicsConfig.ResScale}x)";
            }

            StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                Device.EnableDeviceVsync,
                LocaleManager.Instance[LocaleKeys.VolumeShort] + $": {(int)(Device.GetVolume() * 100)}%",
                dockedMode,
                config.Graphics.AspectRatio.Value.ToText(),
                LocaleManager.Instance[LocaleKeys.Game] + $": {Device.Statistics.GetGameFrameRate():00.00} FPS ({Device.Statistics.GetGameFrameTime():00.00} ms)",
                $"FIFO: {Device.Statistics.GetFifoPercent():00.00} %"));
        }

        public async Task ShowExitPrompt()
        {
            bool shouldExit = !ConfigurationState.Shared.ShowConfirmExit;
            if (!shouldExit)
            {
                if (_dialogShown)
                {
                    return;
                }

                _dialogShown = true;

                shouldExit = await ContentDialogHelper.CreateStopEmulationDialog();

                _dialogShown = false;
            }

            if (shouldExit)
            {
                Stop();
            }
        }

        private bool UpdateFrame()
        {
            if (!_isActive)
            {
                return false;
            }

            ConfigurationState config = ConfigurationState.Instance(UseTitleConfiguration);

            NpadManager.Update(config.Graphics.AspectRatio.Value.ToFloat());

            if (_viewModel.IsActive)
            {
                bool isCursorVisible = true;

                if (_isCursorInRenderer && !_viewModel.ShowLoadProgress)
                {
                    if (config.Hid.EnableMouse.Value)
                    {
                        isCursorVisible = config.HideCursor.Value == HideCursorMode.Never;
                    }
                    else
                    {
                        isCursorVisible = config.HideCursor.Value == HideCursorMode.Never ||
                            (config.HideCursor.Value == HideCursorMode.OnIdle &&
                            Stopwatch.GetTimestamp() - _lastCursorMoveTime < CursorHideIdleTime * Stopwatch.Frequency);
                    }
                }

                if (_cursorState != (isCursorVisible ? CursorStates.CursorIsVisible : CursorStates.CursorIsHidden))
                {
                    if (isCursorVisible)
                    {
                        ShowCursor();
                    }
                    else
                    {
                        HideCursor();
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (_keyboardInterface.GetKeyboardStateSnapshot().IsPressed(Key.Delete) && _viewModel.WindowState != WindowState.FullScreen)
                    {
                        Device.Processes.ActiveApplication.DiskCacheLoadState?.Cancel();
                    }
                });

                KeyboardHotkeyState currentHotkeyState = GetHotkeyState();

                if (currentHotkeyState != _prevHotkeyState)
                {
                    switch (currentHotkeyState)
                    {
                        case KeyboardHotkeyState.ToggleVSync:
                            ToggleVSync();
                            break;
                        case KeyboardHotkeyState.Screenshot:
                            ScreenshotRequested = true;
                            break;
                        case KeyboardHotkeyState.ShowUI:
                            _viewModel.ShowMenuAndStatusBar = !_viewModel.ShowMenuAndStatusBar;
                            break;
                        case KeyboardHotkeyState.Pause:
                            if (_viewModel.IsPaused)
                            {
                                Resume();
                            }
                            else
                            {
                                Pause();
                            }
                            break;
                        case KeyboardHotkeyState.ToggleMute:
                            if (Device.IsAudioMuted())
                            {
                                Device.SetVolume(_viewModel.VolumeBeforeMute);
                            }
                            else
                            {
                                _viewModel.VolumeBeforeMute = Device.GetVolume();
                                Device.SetVolume(0);
                            }

                            _viewModel.Volume = Device.GetVolume();
                            break;
                        case KeyboardHotkeyState.ResScaleUp:
                            GraphicsConfig.ResScale = GraphicsConfig.ResScale % MaxResolutionScale + 1;
                            break;
                        case KeyboardHotkeyState.ResScaleDown:
                            GraphicsConfig.ResScale =
                            (MaxResolutionScale + GraphicsConfig.ResScale - 2) % MaxResolutionScale + 1;
                            break;
                        case KeyboardHotkeyState.VolumeUp:
                            _newVolume = MathF.Round((Device.GetVolume() + VolumeDelta), 2);
                            Device.SetVolume(_newVolume);

                            _viewModel.Volume = Device.GetVolume();
                            break;
                        case KeyboardHotkeyState.VolumeDown:
                            _newVolume = MathF.Round((Device.GetVolume() - VolumeDelta), 2);
                            Device.SetVolume(_newVolume);

                            _viewModel.Volume = Device.GetVolume();
                            break;
                        case KeyboardHotkeyState.None:
                            (_keyboardInterface as AvaloniaKeyboard).Clear();
                            break;
                    }
                }

                _prevHotkeyState = currentHotkeyState;

                if (ScreenshotRequested)
                {
                    ScreenshotRequested = false;
                    _renderer.Screenshot();
                }
            }

            // Touchscreen.
            bool hasTouch = false;

            if (_viewModel.IsActive && !config.Hid.EnableMouse.Value)
            {
                hasTouch = TouchScreenManager.Update(true, (_inputManager.MouseDriver as AvaloniaMouseDriver).IsButtonPressed(MouseButton.Button1), config.Graphics.AspectRatio.Value.ToFloat());
            }

            if (!hasTouch)
            {
                Device.Hid.Touchscreen.Update();
            }

            Device.Hid.DebugPad.Update();

            return true;
        }

        private KeyboardHotkeyState GetHotkeyState()
        {
            KeyboardHotkeyState state = KeyboardHotkeyState.None;

            ReactiveObject<KeyboardHotkeys> hotkeys = ConfigurationState.Instance(UseTitleConfiguration).Hid.Hotkeys;
            if (hotkeys.Value == null)
            {
                Thread.Sleep(500);
            }

            if (_keyboardInterface.IsPressed((Key)hotkeys.Value!.ToggleVsync))
            {
                state = KeyboardHotkeyState.ToggleVSync;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.Screenshot))
            {
                state = KeyboardHotkeyState.Screenshot;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.ShowUI))
            {
                state = KeyboardHotkeyState.ShowUI;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.Pause))
            {
                state = KeyboardHotkeyState.Pause;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.ToggleMute))
            {
                state = KeyboardHotkeyState.ToggleMute;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.ResScaleUp))
            {
                state = KeyboardHotkeyState.ResScaleUp;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.ResScaleDown))
            {
                state = KeyboardHotkeyState.ResScaleDown;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.VolumeUp))
            {
                state = KeyboardHotkeyState.VolumeUp;
            }
            else if (_keyboardInterface.IsPressed((Key)hotkeys.Value.VolumeDown))
            {
                state = KeyboardHotkeyState.VolumeDown;
            }

            return state;
        }
    }
}
