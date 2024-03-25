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

        private readonly object _lockObject = new();

        public event EventHandler AppExit;
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
            _glLogLevel = ConfigurationState.Instance.Logger.GraphicsDebugLevel;
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

            ConfigurationState.Instance.HideCursor.Event += HideCursorState_Changed;

            _topLevel.PointerMoved += TopLevel_PointerEnteredOrMoved;
            _topLevel.PointerEntered += TopLevel_PointerEnteredOrMoved;
            _topLevel.PointerExited += TopLevel_PointerExited;

            if (OperatingSystem.IsWindows())
            {
                _invisibleCursorWin = CreateEmptyCursor();
                _defaultCursorWin = CreateArrowCursor();
            }

            ConfigurationState.Instance.System.IgnoreMissingServices.Event += UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance.Graphics.AspectRatio.Event += UpdateAspectRatioState;
            ConfigurationState.Instance.System.EnableDockedMode.Event += UpdateDockedModeState;
            ConfigurationState.Instance.System.AudioVolume.Event += UpdateAudioVolumeState;
            ConfigurationState.Instance.System.EnableDockedMode.Event += UpdateDockedModeState;
            ConfigurationState.Instance.System.AudioVolume.Event += UpdateAudioVolumeState;
            ConfigurationState.Instance.Graphics.AntiAliasing.Event += UpdateAntiAliasing;
            ConfigurationState.Instance.Graphics.ScalingFilter.Event += UpdateScalingFilter;
            ConfigurationState.Instance.Graphics.ScalingFilterLevel.Event += UpdateScalingFilterLevel;
            ConfigurationState.Instance.Graphics.EnableColorSpacePassthrough.Event += UpdateColorSpacePassthrough;

            ConfigurationState.Instance.System.EnableInternetAccess.Event += UpdateEnableInternetAccessState;
            ConfigurationState.Instance.Multiplayer.LanInterfaceId.Event += UpdateLanInterfaceIdState;
            ConfigurationState.Instance.Multiplayer.Mode.Event += UpdateMultiplayerModeState;

            _gpuCancellationTokenSource = new CancellationTokenSource();
            _gpuDoneEvent = new ManualResetEvent(false);
        }

        private void TopLevel_PointerEnteredOrMoved(object sender, PointerEventArgs e)
        {
            if (sender is MainWindow window)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();

                var point = e.GetCurrentPoint(window).Position;
                var bounds = RendererHost.EmbeddedWindow.Bounds;

                _isCursorInRenderer = point.X >= bounds.X &&
                                      point.X <= bounds.Width + bounds.X &&
                                      point.Y >= bounds.Y &&
                                      point.Y <= bounds.Height + bounds.Y;
            }
        }

        private void TopLevel_PointerExited(object sender, PointerEventArgs e)
        {
            _isCursorInRenderer = false;
        }

        private void UpdateScalingFilterLevel(object sender, ReactiveEventArgs<int> e)
        {
            _renderer.Window?.SetScalingFilter((Graphics.GAL.ScalingFilter)ConfigurationState.Instance.Graphics.ScalingFilter.Value);
            _renderer.Window?.SetScalingFilterLevel(ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value);
        }

        private void UpdateScalingFilter(object sender, ReactiveEventArgs<ScalingFilter> e)
        {
            _renderer.Window?.SetScalingFilter((Graphics.GAL.ScalingFilter)ConfigurationState.Instance.Graphics.ScalingFilter.Value);
            _renderer.Window?.SetScalingFilterLevel(ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value);
        }

        private void UpdateColorSpacePassthrough(object sender, ReactiveEventArgs<bool> e)
        {
            _renderer.Window?.SetColorSpacePassthrough((bool)ConfigurationState.Instance.Graphics.EnableColorSpacePassthrough.Value);
        }

        private void ShowCursor()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _viewModel.Cursor = Cursor.Default;

                if (OperatingSystem.IsWindows())
                {
                    SetCursor(_defaultCursorWin);
                }
            });
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

            NpadManager.Initialize(Device, ConfigurationState.Instance.Hid.InputConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);
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

            _viewModel.Volume = ConfigurationState.Instance.System.AudioVolume.Value;

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

            ConfigurationState.Instance.System.IgnoreMissingServices.Event -= UpdateIgnoreMissingServicesState;
            ConfigurationState.Instance.Graphics.AspectRatio.Event -= UpdateAspectRatioState;
            ConfigurationState.Instance.System.EnableDockedMode.Event -= UpdateDockedModeState;
            ConfigurationState.Instance.System.AudioVolume.Event -= UpdateAudioVolumeState;
            ConfigurationState.Instance.Graphics.ScalingFilter.Event -= UpdateScalingFilter;
            ConfigurationState.Instance.Graphics.ScalingFilterLevel.Event -= UpdateScalingFilterLevel;
            ConfigurationState.Instance.Graphics.AntiAliasing.Event -= UpdateAntiAliasing;
            ConfigurationState.Instance.Graphics.EnableColorSpacePassthrough.Event -= UpdateColorSpacePassthrough;

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

            if (ConfigurationState.Instance.Graphics.GraphicsBackend.Value == GraphicsBackend.Vulkan)
            {
                renderer = new VulkanRenderer(
                    Vk.GetApi(),
                    (RendererHost.EmbeddedWindow as EmbeddedWindowVulkan).CreateSurface,
                    VulkanHelper.GetRequiredInstanceExtensions,
                    ConfigurationState.Instance.Graphics.PreferredGpu.Value);
            }
            else
            {
                renderer = new OpenGLRenderer();
            }

            BackendThreading threadingMode = ConfigurationState.Instance.Graphics.BackendThreading;

            var isGALThreaded = threadingMode == BackendThreading.On || (threadingMode == BackendThreading.Auto && renderer.PreferThreading);
            if (isGALThreaded)
            {
                renderer = new ThreadedRenderer(renderer);
            }

            Logger.Info?.PrintMsg(LogClass.Gpu, $"Backend Threading ({threadingMode}): {isGALThreaded}");

            // Initialize Configuration.
            var memoryConfiguration = ConfigurationState.Instance.System.ExpandRam.Value ? MemoryConfiguration.MemoryConfiguration6GiB : MemoryConfiguration.MemoryConfiguration4GiB;

            HLEConfiguration configuration = new(VirtualFileSystem,
                                                     _viewModel.LibHacHorizonManager,
                                                     ContentManager,
                                                     _accountManager,
                                                     _userChannelPersistence,
                                                     renderer,
                                                     InitializeAudio(),
                                                     memoryConfiguration,
                                                     _viewModel.UiHandler,
                                                     (SystemLanguage)ConfigurationState.Instance.System.Language.Value,
                                                     (RegionCode)ConfigurationState.Instance.System.Region.Value,
                                                     ConfigurationState.Instance.Graphics.EnableVsync,
                                                     ConfigurationState.Instance.System.EnableDockedMode,
                                                     ConfigurationState.Instance.System.EnablePtc,
                                                     ConfigurationState.Instance.System.EnableInternetAccess,
                                                     ConfigurationState.Instance.System.EnableFsIntegrityChecks ? IntegrityCheckLevel.ErrorOnInvalid : IntegrityCheckLevel.None,
                                                     ConfigurationState.Instance.System.FsGlobalAccessLogMode,
                                                     ConfigurationState.Instance.System.SystemTimeOffset,
                                                     ConfigurationState.Instance.System.TimeZone,
                                                     ConfigurationState.Instance.System.MemoryManagerMode,
                                                     ConfigurationState.Instance.System.IgnoreMissingServices,
                                                     ConfigurationState.Instance.Graphics.AspectRatio,
                                                     ConfigurationState.Instance.System.AudioVolume,
                                                     ConfigurationState.Instance.System.UseHypervisor,
                                                     ConfigurationState.Instance.Multiplayer.LanInterfaceId.Value,
                                                     ConfigurationState.Instance.Multiplayer.Mode);

            Device = new Switch(configuration);
        }

        private static IHardwareDeviceDriver InitializeAudio()
        {
            var availableBackends = new List<AudioBackend>
            {
                AudioBackend.SDL2,
                AudioBackend.SoundIo,
                AudioBackend.OpenAl,
                AudioBackend.Dummy,
            };

            AudioBackend preferredBackend = ConfigurationState.Instance.System.AudioBackend.Value;

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
                    ConfigurationState.Instance.System.AudioBackend.Value = currentBackend;
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

            _renderer = Device.Gpu.Renderer is ThreadedRenderer tr ? tr.BaseRenderer : Device.Gpu.Renderer;

            _renderer.ScreenCaptured += Renderer_ScreenCaptured;

            (RendererHost.EmbeddedWindow as EmbeddedWindowOpenGL)?.InitializeBackgroundContext(_renderer);

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            _renderer?.Window?.SetAntiAliasing((Graphics.GAL.AntiAliasing)ConfigurationState.Instance.Graphics.AntiAliasing.Value);
            _renderer?.Window?.SetScalingFilter((Graphics.GAL.ScalingFilter)ConfigurationState.Instance.Graphics.ScalingFilter.Value);
            _renderer?.Window?.SetScalingFilterLevel(ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value);
            _renderer?.Window?.SetColorSpacePassthrough(ConfigurationState.Instance.Graphics.EnableColorSpacePassthrough.Value);

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

        public void UpdateStatus()
        {
            // Run a status update only when a frame is to be drawn. This prevents from updating the ui and wasting a render when no frame is queued.
            string dockedMode = ConfigurationState.Instance.System.EnableDockedMode ? LocaleManager.Instance[LocaleKeys.Docked] : LocaleManager.Instance[LocaleKeys.Handheld];

            if (GraphicsConfig.ResScale != 1)
            {
                dockedMode += $" ({GraphicsConfig.ResScale}x)";
            }

            StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                Device.EnableDeviceVsync,
                LocaleManager.Instance[LocaleKeys.VolumeShort] + $": {(int)(Device.GetVolume() * 100)}%",
                ConfigurationState.Instance.Graphics.GraphicsBackend.Value == GraphicsBackend.Vulkan ? "Vulkan" : "OpenGL",
                dockedMode,
                ConfigurationState.Instance.Graphics.AspectRatio.Value.ToText(),
                LocaleManager.Instance[LocaleKeys.Game] + $": {Device.Statistics.GetGameFrameRate():00.00} FPS ({Device.Statistics.GetGameFrameTime():00.00} ms)",
                $"FIFO: {Device.Statistics.GetFifoPercent():00.00} %",
                $"GPU: {_renderer.GetHardwareInfo().GpuDriver}"));
        }

        public async Task ShowExitPrompt()
        {
            bool shouldExit = !ConfigurationState.Instance.ShowConfirmExit;
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

            NpadManager.Update(ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());

            if (_viewModel.IsActive)
            {
                if (_isCursorInRenderer)
                {
                    if (ConfigurationState.Instance.Hid.EnableMouse)
                    {
                        HideCursor();
                    }
                    else
                    {
                        switch (ConfigurationState.Instance.HideCursor.Value)
                        {
                            case HideCursorMode.Never:
                                ShowCursor();
                                break;
                            case HideCursorMode.OnIdle:
                                if (Stopwatch.GetTimestamp() - _lastCursorMoveTime >= CursorHideIdleTime * Stopwatch.Frequency)
                                {
                                    HideCursor();
                                }
                                else
                                {
                                    ShowCursor();
                                }
                                break;
                            case HideCursorMode.Always:
                                HideCursor();
                                break;
                        }
                    }
                }
                else
                {
                    ShowCursor();
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
                            Device.EnableDeviceVsync = !Device.EnableDeviceVsync;

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

            if (_viewModel.IsActive && !ConfigurationState.Instance.Hid.EnableMouse)
            {
                hasTouch = TouchScreenManager.Update(true, (_inputManager.MouseDriver as AvaloniaMouseDriver).IsButtonPressed(MouseButton.Button1), ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());
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

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleVsync))
            {
                state = KeyboardHotkeyState.ToggleVSync;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Screenshot))
            {
                state = KeyboardHotkeyState.Screenshot;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ShowUI))
            {
                state = KeyboardHotkeyState.ShowUI;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Pause))
            {
                state = KeyboardHotkeyState.Pause;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleMute))
            {
                state = KeyboardHotkeyState.ToggleMute;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ResScaleUp))
            {
                state = KeyboardHotkeyState.ResScaleUp;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ResScaleDown))
            {
                state = KeyboardHotkeyState.ResScaleDown;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.VolumeUp))
            {
                state = KeyboardHotkeyState.VolumeUp;
            }
            else if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.VolumeDown))
            {
                state = KeyboardHotkeyState.VolumeDown;
            }

            return state;
        }
    }
}
