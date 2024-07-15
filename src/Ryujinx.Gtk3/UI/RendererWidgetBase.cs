using Gdk;
using Gtk;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Multithreading;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Input;
using Ryujinx.Input.GTK3;
using Ryujinx.Input.HLE;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using Ryujinx.UI.Widgets;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Key = Ryujinx.Input.Key;
using ScalingFilter = Ryujinx.Graphics.GAL.ScalingFilter;
using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.UI
{
    public abstract class RendererWidgetBase : DrawingArea
    {
        private const int SwitchPanelWidth = 1280;
        private const int SwitchPanelHeight = 720;
        private const int TargetFps = 60;
        private const float MaxResolutionScale = 4.0f; // Max resolution hotkeys can scale to before wrapping.
        private const float VolumeDelta = 0.05f;

        public ManualResetEvent WaitEvent { get; set; }
        public NpadManager NpadManager { get; }
        public TouchScreenManager TouchScreenManager { get; }
        public Switch Device { get; private set; }
        public IRenderer Renderer { get; private set; }

        public bool ScreenshotRequested { get; set; }
        protected int WindowWidth { get; private set; }
        protected int WindowHeight { get; private set; }

        public static event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;

        private bool _isActive;
        private bool _isStopped;

        private bool _toggleFullscreen;
        private bool _toggleDockedMode;

        private readonly long _ticksPerFrame;

        private long _ticks = 0;
        private float _newVolume;

        private readonly Stopwatch _chrono;

        private KeyboardHotkeyState _prevHotkeyState;

        private readonly ManualResetEvent _exitEvent;
        private readonly ManualResetEvent _gpuDoneEvent;

        private readonly CancellationTokenSource _gpuCancellationTokenSource;

        // Hide Cursor
        const int CursorHideIdleTime = 5; // seconds
        private static readonly Cursor _invisibleCursor = new(Display.Default, CursorType.BlankCursor);
        private long _lastCursorMoveTime;
        private HideCursorMode _hideCursorMode;
        private readonly InputManager _inputManager;
        private readonly IKeyboard _keyboardInterface;
        private readonly GraphicsDebugLevel _glLogLevel;
        private string _gpuBackendName;
        private string _gpuDriverName;
        private bool _isMouseInClient;

        public RendererWidgetBase(InputManager inputManager, GraphicsDebugLevel glLogLevel)
        {
            var mouseDriver = new GTK3MouseDriver(this);

            _inputManager = inputManager;
            _inputManager.SetMouseDriver(mouseDriver);
            NpadManager = _inputManager.CreateNpadManager();
            TouchScreenManager = _inputManager.CreateTouchScreenManager();
            _keyboardInterface = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");

            WaitEvent = new ManualResetEvent(false);

            _glLogLevel = glLogLevel;

            Destroyed += Renderer_Destroyed;

            _chrono = new Stopwatch();

            _ticksPerFrame = Stopwatch.Frequency / TargetFps;

            AddEvents((int)(EventMask.ButtonPressMask
                          | EventMask.ButtonReleaseMask
                          | EventMask.PointerMotionMask
                          | EventMask.ScrollMask
                          | EventMask.EnterNotifyMask
                          | EventMask.LeaveNotifyMask
                          | EventMask.KeyPressMask
                          | EventMask.KeyReleaseMask));

            _exitEvent = new ManualResetEvent(false);
            _gpuDoneEvent = new ManualResetEvent(false);

            _gpuCancellationTokenSource = new CancellationTokenSource();

            _hideCursorMode = ConfigurationState.Instance.HideCursor;
            _lastCursorMoveTime = Stopwatch.GetTimestamp();

            ConfigurationState.Instance.HideCursor.Event += HideCursorStateChanged;
            ConfigurationState.Instance.Graphics.AntiAliasing.Event += UpdateAnriAliasing;
            ConfigurationState.Instance.Graphics.ScalingFilter.Event += UpdateScalingFilter;
            ConfigurationState.Instance.Graphics.ScalingFilterLevel.Event += UpdateScalingFilterLevel;
        }

        private void UpdateScalingFilterLevel(object sender, ReactiveEventArgs<int> e)
        {
            Renderer.Window.SetScalingFilter((ScalingFilter)ConfigurationState.Instance.Graphics.ScalingFilter.Value);
            Renderer.Window.SetScalingFilterLevel(ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value);
        }

        private void UpdateScalingFilter(object sender, ReactiveEventArgs<Ryujinx.Common.Configuration.ScalingFilter> e)
        {
            Renderer.Window.SetScalingFilter((ScalingFilter)ConfigurationState.Instance.Graphics.ScalingFilter.Value);
            Renderer.Window.SetScalingFilterLevel(ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value);
        }

        public abstract void InitializeRenderer();

        public abstract void SwapBuffers();

        protected abstract string GetGpuBackendName();

        private string GetGpuDriverName()
        {
            return Renderer.GetHardwareInfo().GpuDriver;
        }

        private void HideCursorStateChanged(object sender, ReactiveEventArgs<HideCursorMode> state)
        {
            Application.Invoke(delegate
            {
                _hideCursorMode = state.NewValue;

                switch (_hideCursorMode)
                {
                    case HideCursorMode.Never:
                        Window.Cursor = null;
                        break;
                    case HideCursorMode.OnIdle:
                        _lastCursorMoveTime = Stopwatch.GetTimestamp();
                        break;
                    case HideCursorMode.Always:
                        Window.Cursor = _invisibleCursor;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));
                }
            });
        }

        private void Renderer_Destroyed(object sender, EventArgs e)
        {
            ConfigurationState.Instance.HideCursor.Event -= HideCursorStateChanged;
            ConfigurationState.Instance.Graphics.AntiAliasing.Event -= UpdateAnriAliasing;
            ConfigurationState.Instance.Graphics.ScalingFilter.Event -= UpdateScalingFilter;
            ConfigurationState.Instance.Graphics.ScalingFilterLevel.Event -= UpdateScalingFilterLevel;

            NpadManager.Dispose();
            Dispose();
        }

        private void UpdateAnriAliasing(object sender, ReactiveEventArgs<Ryujinx.Common.Configuration.AntiAliasing> e)
        {
            Renderer?.Window.SetAntiAliasing((Graphics.GAL.AntiAliasing)e.NewValue);
        }

        protected override bool OnMotionNotifyEvent(EventMotion evnt)
        {
            if (_hideCursorMode == HideCursorMode.OnIdle)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();
            }

            if (ConfigurationState.Instance.Hid.EnableMouse)
            {
                Window.Cursor = _invisibleCursor;
            }

            _isMouseInClient = true;

            return false;
        }

        protected override bool OnEnterNotifyEvent(EventCrossing evnt)
        {
            Window.Cursor = ConfigurationState.Instance.Hid.EnableMouse ? _invisibleCursor : null;

            _isMouseInClient = true;

            return base.OnEnterNotifyEvent(evnt);
        }

        protected override bool OnLeaveNotifyEvent(EventCrossing evnt)
        {
            Window.Cursor = null;

            _isMouseInClient = false;

            return base.OnLeaveNotifyEvent(evnt);
        }

        protected override void OnGetPreferredHeight(out int minimumHeight, out int naturalHeight)
        {
            Gdk.Monitor monitor = Display.GetMonitorAtWindow(Window);

            // If the monitor is at least 1080p, use the Switch panel size as minimal size.
            if (monitor.Geometry.Height >= 1080)
            {
                minimumHeight = SwitchPanelHeight;
            }
            // Otherwise, we default minimal size to 480p 16:9.
            else
            {
                minimumHeight = 480;
            }

            naturalHeight = minimumHeight;
        }

        protected override void OnGetPreferredWidth(out int minimumWidth, out int naturalWidth)
        {
            Gdk.Monitor monitor = Display.GetMonitorAtWindow(Window);

            // If the monitor is at least 1080p, use the Switch panel size as minimal size.
            if (monitor.Geometry.Height >= 1080)
            {
                minimumWidth = SwitchPanelWidth;
            }
            // Otherwise, we default minimal size to 480p 16:9.
            else
            {
                minimumWidth = 854;
            }

            naturalWidth = minimumWidth;
        }

        protected override bool OnConfigureEvent(EventConfigure evnt)
        {
            bool result = base.OnConfigureEvent(evnt);

            Gdk.Monitor monitor = Display.GetMonitorAtWindow(Window);

            WindowWidth = evnt.Width * monitor.ScaleFactor;
            WindowHeight = evnt.Height * monitor.ScaleFactor;

            Renderer?.Window?.SetSize(WindowWidth, WindowHeight);

            return result;
        }

        private void HandleScreenState(KeyboardStateSnapshot keyboard)
        {
            bool toggleFullscreen = keyboard.IsPressed(Key.F11)
                                || ((keyboard.IsPressed(Key.AltLeft)
                                || keyboard.IsPressed(Key.AltRight))
                                && keyboard.IsPressed(Key.Enter))
                                || keyboard.IsPressed(Key.Escape);

            bool fullScreenToggled = ParentWindow.State.HasFlag(WindowState.Fullscreen);

            if (toggleFullscreen != _toggleFullscreen)
            {
                if (toggleFullscreen)
                {
                    if (fullScreenToggled)
                    {
                        ParentWindow.Unfullscreen();
                        (Toplevel as MainWindow)?.ToggleExtraWidgets(true);
                    }
                    else
                    {
                        if (keyboard.IsPressed(Key.Escape))
                        {
                            if (!ConfigurationState.Instance.ShowConfirmExit || GtkDialog.CreateExitDialog())
                            {
                                Exit();
                            }
                        }
                        else
                        {
                            ParentWindow.Fullscreen();
                            (Toplevel as MainWindow)?.ToggleExtraWidgets(false);
                        }
                    }
                }
            }

            _toggleFullscreen = toggleFullscreen;

            bool toggleDockedMode = keyboard.IsPressed(Key.F9);

            if (toggleDockedMode != _toggleDockedMode)
            {
                if (toggleDockedMode)
                {
                    ConfigurationState.Instance.System.EnableDockedMode.Value =
                        !ConfigurationState.Instance.System.EnableDockedMode.Value;
                }
            }

            _toggleDockedMode = toggleDockedMode;

            if (_isMouseInClient)
            {
                if (ConfigurationState.Instance.Hid.EnableMouse.Value)
                {
                    Window.Cursor = _invisibleCursor;
                }
                else
                {
                    switch (_hideCursorMode)
                    {
                        case HideCursorMode.OnIdle:
                            long cursorMoveDelta = Stopwatch.GetTimestamp() - _lastCursorMoveTime;
                            Window.Cursor = (cursorMoveDelta >= CursorHideIdleTime * Stopwatch.Frequency) ? _invisibleCursor : null;
                            break;
                        case HideCursorMode.Always:
                            Window.Cursor = _invisibleCursor;
                            break;
                        case HideCursorMode.Never:
                            Window.Cursor = null;
                            break;
                    }
                }
            }
        }

        public void Initialize(Switch device)
        {
            Device = device;

            IRenderer renderer = Device.Gpu.Renderer;

            if (renderer is ThreadedRenderer tr)
            {
                renderer = tr.BaseRenderer;
            }

            Renderer = renderer;
            Renderer?.Window?.SetSize(WindowWidth, WindowHeight);

            if (Renderer != null)
            {
                Renderer.ScreenCaptured += Renderer_ScreenCaptured;
            }

            NpadManager.Initialize(device, ConfigurationState.Instance.Hid.InputConfig, ConfigurationState.Instance.Hid.EnableKeyboard, ConfigurationState.Instance.Hid.EnableMouse);
            TouchScreenManager.Initialize(device);
        }

        private unsafe void Renderer_ScreenCaptured(object sender, ScreenCaptureImageInfo e)
        {
            if (e.Data.Length > 0 && e.Height > 0 && e.Width > 0)
            {
                Task.Run(() =>
                {
                    lock (this)
                    {
                        string applicationName = Device.Processes.ActiveApplication.Name;
                        string sanitizedApplicationName = FileSystemUtils.SanitizeFileName(applicationName);
                        DateTime currentTime = DateTime.Now;

                        string filename = $"{sanitizedApplicationName}_{currentTime.Year}-{currentTime.Month:D2}-{currentTime.Day:D2}_{currentTime.Hour:D2}-{currentTime.Minute:D2}-{currentTime.Second:D2}.png";

                        string directory = AppDataManager.Mode switch
                        {
                            AppDataManager.LaunchMode.Portable or AppDataManager.LaunchMode.Custom => System.IO.Path.Combine(AppDataManager.BaseDirPath, "screenshots"),
                            _ => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Ryujinx"),
                        };

                        string path = System.IO.Path.Combine(directory, filename);

                        try
                        {
                            Directory.CreateDirectory(directory);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Failed to create directory at path {directory}. Error : {ex.GetType().Name}", "Screenshot");

                            return;
                        }

                        var colorType = e.IsBgra ? SKColorType.Bgra8888 : SKColorType.Rgba8888;
                        using var image = new SKBitmap(new SKImageInfo(e.Width, e.Height, colorType, SKAlphaType.Premul));

                        Marshal.Copy(e.Data, 0, image.GetPixels(), e.Data.Length);
                        using var surface = SKSurface.Create(image.Info);
                        var canvas = surface.Canvas;

                        if (e.FlipX || e.FlipY)
                        {
                            canvas.Clear(SKColors.Transparent);

                            float scaleX = e.FlipX ? -1 : 1;
                            float scaleY = e.FlipY ? -1 : 1;

                            var matrix = SKMatrix.CreateScale(scaleX, scaleY, image.Width / 2f, image.Height / 2f);

                            canvas.SetMatrix(matrix);
                        }
                        canvas.DrawBitmap(image, new SKPoint());

                        surface.Flush();
                        using var snapshot = surface.Snapshot();
                        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 80);
                        using var file = File.OpenWrite(path);
                        encoded.SaveTo(file);

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

        public void Render()
        {
            Gtk.Window parent = Toplevel as Gtk.Window;
            parent.Present();

            InitializeRenderer();

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            Renderer.Window.SetAntiAliasing((Graphics.GAL.AntiAliasing)ConfigurationState.Instance.Graphics.AntiAliasing.Value);
            Renderer.Window.SetScalingFilter((Graphics.GAL.ScalingFilter)ConfigurationState.Instance.Graphics.ScalingFilter.Value);
            Renderer.Window.SetScalingFilterLevel(ConfigurationState.Instance.Graphics.ScalingFilterLevel.Value);

            _gpuBackendName = GetGpuBackendName();
            _gpuDriverName = GetGpuDriverName();

            Device.Gpu.Renderer.RunLoop(() =>
            {
                Device.Gpu.SetGpuThread();
                Device.Gpu.InitializeShaderCache(_gpuCancellationTokenSource.Token);

                Renderer.Window.ChangeVSyncMode(Device.EnableDeviceVsync);

                (Toplevel as MainWindow)?.ActivatePauseMenu();

                while (_isActive)
                {
                    if (_isStopped)
                    {
                        return;
                    }

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
                        Device.PresentFrame(SwapBuffers);
                    }

                    if (_ticks >= _ticksPerFrame)
                    {
                        string dockedMode = ConfigurationState.Instance.System.EnableDockedMode ? "Docked" : "Handheld";
                        float scale = GraphicsConfig.ResScale;
                        if (scale != 1)
                        {
                            dockedMode += $" ({scale}x)";
                        }

                        StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                            Device.EnableDeviceVsync,
                            Device.GetVolume(),
                            _gpuBackendName,
                            dockedMode,
                            ConfigurationState.Instance.Graphics.AspectRatio.Value.ToText(),
                            $"Game: {Device.Statistics.GetGameFrameRate():00.00} FPS ({Device.Statistics.GetGameFrameTime():00.00} ms)",
                            $"FIFO: {Device.Statistics.GetFifoPercent():0.00} %",
                            $"GPU: {_gpuDriverName}"));

                        _ticks = Math.Min(_ticks - _ticksPerFrame, _ticksPerFrame);
                    }
                }

                // Make sure all commands in the run loop are fully executed before leaving the loop.
                if (Device.Gpu.Renderer is ThreadedRenderer threaded)
                {
                    threaded.FlushThreadedCommands();
                }

                _gpuDoneEvent.Set();
            });
        }

        public void Start()
        {
            _chrono.Restart();

            _isActive = true;

            Gtk.Window parent = Toplevel as Gtk.Window;

            Application.Invoke(delegate
            {
                parent.Present();

                var activeProcess = Device.Processes.ActiveApplication;

                parent.Title = TitleHelper.ActiveApplicationTitle(activeProcess, Program.Version);
            });

            Thread renderLoopThread = new(Render)
            {
                Name = "GUI.RenderLoop",
            };
            renderLoopThread.Start();

            Thread nvidiaStutterWorkaround = null;
            if (Renderer is Graphics.OpenGL.OpenGLRenderer)
            {
                nvidiaStutterWorkaround = new Thread(NvidiaStutterWorkaround)
                {
                    Name = "GUI.NvidiaStutterWorkaround",
                };
                nvidiaStutterWorkaround.Start();
            }

            MainLoop();

            // NOTE: The render loop is allowed to stay alive until the renderer itself is disposed, as it may handle resource dispose.
            // We only need to wait for all commands submitted during the main gpu loop to be processed.
            _gpuDoneEvent.WaitOne();
            _gpuDoneEvent.Dispose();
            nvidiaStutterWorkaround?.Join();

            Exit();
        }

        public void Exit()
        {
            TouchScreenManager?.Dispose();
            NpadManager?.Dispose();

            if (_isStopped)
            {
                return;
            }

            _gpuCancellationTokenSource.Cancel();

            _isStopped = true;

            if (_isActive)
            {
                _isActive = false;

                _exitEvent.WaitOne();
                _exitEvent.Dispose();
            }
        }

        private void NvidiaStutterWorkaround()
        {
            while (_isActive)
            {
                // When NVIDIA Threaded Optimization is on, the driver will snapshot all threads in the system whenever the application creates any new ones.
                // The ThreadPool has something called a "GateThread" which terminates itself after some inactivity.
                // However, it immediately starts up again, since the rules regarding when to terminate and when to start differ.
                // This creates a new thread every second or so.
                // The main problem with this is that the thread snapshot can take 70ms, is on the OpenGL thread and will delay rendering any graphics.
                // This is a little over budget on a frame time of 16ms, so creates a large stutter.
                // The solution is to keep the ThreadPool active so that it never has a reason to terminate the GateThread.

                // TODO: This should be removed when the issue with the GateThread is resolved.

                ThreadPool.QueueUserWorkItem((state) => { });
                Thread.Sleep(300);
            }
        }

        public void MainLoop()
        {
            while (_isActive)
            {
                UpdateFrame();

                // Polling becomes expensive if it's not slept
                Thread.Sleep(1);
            }

            _exitEvent.Set();
        }

        private bool UpdateFrame()
        {
            if (!_isActive)
            {
                return true;
            }

            if (_isStopped)
            {
                return false;
            }

            if ((Toplevel as MainWindow).IsFocused)
            {
                Application.Invoke(delegate
                {
                    KeyboardStateSnapshot keyboard = _keyboardInterface.GetKeyboardStateSnapshot();

                    HandleScreenState(keyboard);

                    if (keyboard.IsPressed(Key.Delete))
                    {
                        if (!ParentWindow.State.HasFlag(WindowState.Fullscreen))
                        {
                            Device.Processes.ActiveApplication.DiskCacheLoadState?.Cancel();
                        }
                    }
                });
            }

            NpadManager.Update(ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());

            if ((Toplevel as MainWindow).IsFocused)
            {
                KeyboardHotkeyState currentHotkeyState = GetHotkeyState();

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ToggleVSync) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ToggleVSync))
                {
                    Device.EnableDeviceVsync = !Device.EnableDeviceVsync;
                }

                if ((currentHotkeyState.HasFlag(KeyboardHotkeyState.Screenshot) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.Screenshot)) || ScreenshotRequested)
                {
                    ScreenshotRequested = false;

                    Renderer.Screenshot();
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ShowUI) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ShowUI))
                {
                    (Toplevel as MainWindow).ToggleExtraWidgets(true);
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.Pause) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.Pause))
                {
                    (Toplevel as MainWindow)?.TogglePause();
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ToggleMute) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ToggleMute))
                {
                    if (Device.IsAudioMuted())
                    {
                        Device.SetVolume(ConfigurationState.Instance.System.AudioVolume);
                    }
                    else
                    {
                        Device.SetVolume(0);
                    }
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ResScaleUp) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ResScaleUp))
                {
                    GraphicsConfig.ResScale = GraphicsConfig.ResScale % MaxResolutionScale + 1;
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ResScaleDown) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ResScaleDown))
                {
                    GraphicsConfig.ResScale =
                    (MaxResolutionScale + GraphicsConfig.ResScale - 2) % MaxResolutionScale + 1;
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.VolumeUp) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.VolumeUp))
                {
                    _newVolume = MathF.Round((Device.GetVolume() + VolumeDelta), 2);
                    Device.SetVolume(_newVolume);
                }

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.VolumeDown) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.VolumeDown))
                {
                    _newVolume = MathF.Round((Device.GetVolume() - VolumeDelta), 2);
                    Device.SetVolume(_newVolume);
                }

                _prevHotkeyState = currentHotkeyState;
            }

            // Touchscreen
            bool hasTouch = false;

            // Get screen touch position
            if ((Toplevel as MainWindow).IsFocused && !ConfigurationState.Instance.Hid.EnableMouse)
            {
                hasTouch = TouchScreenManager.Update(true, (_inputManager.MouseDriver as GTK3MouseDriver).IsButtonPressed(MouseButton.Button1), ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat());
            }

            if (!hasTouch)
            {
                TouchScreenManager.Update(false);
            }

            Device.Hid.DebugPad.Update();

            return true;
        }

        [Flags]
        private enum KeyboardHotkeyState
        {
            None = 0,
            ToggleVSync = 1 << 0,
            Screenshot = 1 << 1,
            ShowUI = 1 << 2,
            Pause = 1 << 3,
            ToggleMute = 1 << 4,
            ResScaleUp = 1 << 5,
            ResScaleDown = 1 << 6,
            VolumeUp = 1 << 7,
            VolumeDown = 1 << 8,
        }

        private KeyboardHotkeyState GetHotkeyState()
        {
            KeyboardHotkeyState state = KeyboardHotkeyState.None;

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleVsync))
            {
                state |= KeyboardHotkeyState.ToggleVSync;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Screenshot))
            {
                state |= KeyboardHotkeyState.Screenshot;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ShowUI))
            {
                state |= KeyboardHotkeyState.ShowUI;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.Pause))
            {
                state |= KeyboardHotkeyState.Pause;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleMute))
            {
                state |= KeyboardHotkeyState.ToggleMute;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ResScaleUp))
            {
                state |= KeyboardHotkeyState.ResScaleUp;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ResScaleDown))
            {
                state |= KeyboardHotkeyState.ResScaleDown;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.VolumeUp))
            {
                state |= KeyboardHotkeyState.VolumeUp;
            }

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.VolumeDown))
            {
                state |= KeyboardHotkeyState.VolumeDown;
            }

            return state;
        }
    }
}
