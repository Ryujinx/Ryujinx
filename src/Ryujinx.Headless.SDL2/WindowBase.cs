using Ryujinx.Common.Configuration;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.GAL.Multithreading;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.OpenGL;
using Ryujinx.HLE.HOS.Applets;
using Ryujinx.HLE.HOS.Services.Am.AppletOE.ApplicationProxyService.ApplicationProxy.Types;
using Ryujinx.HLE.UI;
using Ryujinx.Input;
using Ryujinx.Input.HLE;
using Ryujinx.SDL2.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static SDL2.SDL;
using AntiAliasing = Ryujinx.Common.Configuration.AntiAliasing;
using ScalingFilter = Ryujinx.Common.Configuration.ScalingFilter;
using Switch = Ryujinx.HLE.Switch;

namespace Ryujinx.Headless.SDL2
{
    abstract partial class WindowBase : IHostUIHandler, IDisposable
    {
        protected const int DefaultWidth = 1280;
        protected const int DefaultHeight = 720;
        private const int TargetFps = 60;
        private SDL_WindowFlags DefaultFlags = SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI | SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS | SDL_WindowFlags.SDL_WINDOW_SHOWN;
        private SDL_WindowFlags FullscreenFlag = 0;

        private static readonly ConcurrentQueue<Action> _mainThreadActions = new();

        [LibraryImport("SDL2")]
        // TODO: Remove this as soon as SDL2-CS was updated to expose this method publicly
        private static partial IntPtr SDL_LoadBMP_RW(IntPtr src, int freesrc);

        public static void QueueMainThreadAction(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        public NpadManager NpadManager { get; }
        public TouchScreenManager TouchScreenManager { get; }
        public Switch Device { get; private set; }
        public IRenderer Renderer { get; private set; }

        public event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;

        protected IntPtr WindowHandle { get; set; }

        public IHostUITheme HostUITheme { get; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int DisplayId { get; set; }
        public bool IsFullscreen { get; set; }
        public bool IsExclusiveFullscreen { get; set; }
        public int ExclusiveFullscreenWidth { get; set; }
        public int ExclusiveFullscreenHeight { get; set; }
        public AntiAliasing AntiAliasing { get; set; }
        public ScalingFilter ScalingFilter { get; set; }
        public int ScalingFilterLevel { get; set; }

        protected SDL2MouseDriver MouseDriver;
        private readonly InputManager _inputManager;
        private readonly IKeyboard _keyboardInterface;
        private readonly GraphicsDebugLevel _glLogLevel;
        private readonly Stopwatch _chrono;
        private readonly long _ticksPerFrame;
        private readonly CancellationTokenSource _gpuCancellationTokenSource;
        private readonly ManualResetEvent _exitEvent;
        private readonly ManualResetEvent _gpuDoneEvent;

        private long _ticks;
        private bool _isActive;
        private bool _isStopped;
        private uint _windowId;

        private string _gpuDriverName;

        private readonly AspectRatio _aspectRatio;
        private readonly bool _enableMouse;

        public WindowBase(
            InputManager inputManager,
            GraphicsDebugLevel glLogLevel,
            AspectRatio aspectRatio,
            bool enableMouse,
            HideCursorMode hideCursorMode)
        {
            MouseDriver = new SDL2MouseDriver(hideCursorMode);
            _inputManager = inputManager;
            _inputManager.SetMouseDriver(MouseDriver);
            NpadManager = _inputManager.CreateNpadManager();
            TouchScreenManager = _inputManager.CreateTouchScreenManager();
            _keyboardInterface = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");
            _glLogLevel = glLogLevel;
            _chrono = new Stopwatch();
            _ticksPerFrame = Stopwatch.Frequency / TargetFps;
            _gpuCancellationTokenSource = new CancellationTokenSource();
            _exitEvent = new ManualResetEvent(false);
            _gpuDoneEvent = new ManualResetEvent(false);
            _aspectRatio = aspectRatio;
            _enableMouse = enableMouse;
            HostUITheme = new HeadlessHostUiTheme();

            SDL2Driver.Instance.Initialize();
        }

        public void Initialize(Switch device, List<InputConfig> inputConfigs, bool enableKeyboard, bool enableMouse)
        {
            Device = device;

            IRenderer renderer = Device.Gpu.Renderer;

            if (renderer is ThreadedRenderer tr)
            {
                renderer = tr.BaseRenderer;
            }

            Renderer = renderer;

            NpadManager.Initialize(device, inputConfigs, enableKeyboard, enableMouse);
            TouchScreenManager.Initialize(device);
        }

        private void SetWindowIcon()
        {
            Stream iconStream = typeof(WindowBase).Assembly.GetManifestResourceStream("Ryujinx.Headless.SDL2.Ryujinx.bmp");
            byte[] iconBytes = new byte[iconStream!.Length];

            if (iconStream.Read(iconBytes, 0, iconBytes.Length) != iconBytes.Length)
            {
                Logger.Error?.Print(LogClass.Application, "Failed to read icon to byte array.");
                iconStream.Close();

                return;
            }

            iconStream.Close();

            unsafe
            {
                fixed (byte* iconPtr = iconBytes)
                {
                    IntPtr rwOpsStruct = SDL_RWFromConstMem((IntPtr)iconPtr, iconBytes.Length);
                    IntPtr iconHandle = SDL_LoadBMP_RW(rwOpsStruct, 1);

                    SDL_SetWindowIcon(WindowHandle, iconHandle);
                    SDL_FreeSurface(iconHandle);
                }
            }
        }

        private void InitializeWindow()
        {
            var activeProcess = Device.Processes.ActiveApplication;
            var nacp = activeProcess.ApplicationControlProperties;
            int desiredLanguage = (int)Device.System.State.DesiredTitleLanguage;

            string titleNameSection = string.IsNullOrWhiteSpace(nacp.Title[desiredLanguage].NameString.ToString()) ? string.Empty : $" - {nacp.Title[desiredLanguage].NameString.ToString()}";
            string titleVersionSection = string.IsNullOrWhiteSpace(nacp.DisplayVersionString.ToString()) ? string.Empty : $" v{nacp.DisplayVersionString.ToString()}";
            string titleIdSection = string.IsNullOrWhiteSpace(activeProcess.ProgramIdText) ? string.Empty : $" ({activeProcess.ProgramIdText.ToUpper()})";
            string titleArchSection = activeProcess.Is64Bit ? " (64-bit)" : " (32-bit)";

            Width = DefaultWidth;
            Height = DefaultHeight;

            if (IsExclusiveFullscreen)
            {
                Width = ExclusiveFullscreenWidth;
                Height = ExclusiveFullscreenHeight;

                DefaultFlags = SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI;
                FullscreenFlag = SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;
            }
            else if (IsFullscreen)
            {
                DefaultFlags = SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI;
                FullscreenFlag = SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;
            }

            WindowHandle = SDL_CreateWindow($"Ryujinx {Program.Version}{titleNameSection}{titleVersionSection}{titleIdSection}{titleArchSection}", SDL_WINDOWPOS_CENTERED_DISPLAY(DisplayId), SDL_WINDOWPOS_CENTERED_DISPLAY(DisplayId), Width, Height, DefaultFlags | FullscreenFlag | GetWindowFlags());

            if (WindowHandle == IntPtr.Zero)
            {
                string errorMessage = $"SDL_CreateWindow failed with error \"{SDL_GetError()}\"";

                Logger.Error?.Print(LogClass.Application, errorMessage);

                throw new Exception(errorMessage);
            }

            SetWindowIcon();

            _windowId = SDL_GetWindowID(WindowHandle);
            SDL2Driver.Instance.RegisterWindow(_windowId, HandleWindowEvent);
        }

        private void HandleWindowEvent(SDL_Event evnt)
        {
            if (evnt.type == SDL_EventType.SDL_WINDOWEVENT)
            {
                switch (evnt.window.windowEvent)
                {
                    case SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
                        // Unlike on Windows, this event fires on macOS when triggering fullscreen mode.
                        // And promptly crashes the process because `Renderer?.window.SetSize` is undefined.
                        // As we don't need this to fire in either case we can test for fullscreen.
                        if (!IsFullscreen && !IsExclusiveFullscreen)
                        {
                            Width = evnt.window.data1;
                            Height = evnt.window.data2;
                            Renderer?.Window.SetSize(Width, Height);
                            MouseDriver.SetClientSize(Width, Height);
                        }
                        break;

                    case SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
                        Exit();
                        break;
                }
            }
            else
            {
                MouseDriver.Update(evnt);
            }
        }

        protected abstract void InitializeWindowRenderer();

        protected abstract void InitializeRenderer();

        protected abstract void FinalizeWindowRenderer();

        protected abstract void SwapBuffers();

        public abstract SDL_WindowFlags GetWindowFlags();

        private string GetGpuDriverName()
        {
            return Renderer.GetHardwareInfo().GpuDriver;
        }

        private void SetAntiAliasing()
        {
            Renderer?.Window.SetAntiAliasing((Graphics.GAL.AntiAliasing)AntiAliasing);
        }

        private void SetScalingFilter()
        {
            Renderer?.Window.SetScalingFilter((Graphics.GAL.ScalingFilter)ScalingFilter);
            Renderer?.Window.SetScalingFilterLevel(ScalingFilterLevel);
        }

        public void Render()
        {
            InitializeWindowRenderer();

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            InitializeRenderer();

            SetAntiAliasing();

            SetScalingFilter();

            _gpuDriverName = GetGpuDriverName();

            Device.Gpu.Renderer.RunLoop(() =>
            {
                Device.Gpu.SetGpuThread();
                Device.Gpu.InitializeShaderCache(_gpuCancellationTokenSource.Token);

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
                        string dockedMode = Device.System.State.DockedMode ? "Docked" : "Handheld";
                        float scale = GraphicsConfig.ResScale;
                        if (scale != 1)
                        {
                            dockedMode += $" ({scale}x)";
                        }

                        StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                            Device.EnableDeviceVsync,
                            dockedMode,
                            Device.Configuration.AspectRatio.ToText(),
                            $"Game: {Device.Statistics.GetGameFrameRate():00.00} FPS ({Device.Statistics.GetGameFrameTime():00.00} ms)"
                                + (Device.TurboMode ? $" Turbo ({Device.Configuration.TurboMultiplier}%)" : ""),
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

            FinalizeWindowRenderer();
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
            _isActive = false;

            _exitEvent.WaitOne();
            _exitEvent.Dispose();
        }

        public static void ProcessMainThreadQueue()
        {
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                action();
            }
        }

        public void MainLoop()
        {
            while (_isActive)
            {
                UpdateFrame();

                SDL_PumpEvents();

                ProcessMainThreadQueue();

                // Polling becomes expensive if it's not slept
                Thread.Sleep(1);
            }

            _exitEvent.Set();
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

                ThreadPool.QueueUserWorkItem(state => { });
                Thread.Sleep(300);
            }
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

            NpadManager.Update();

            // Touchscreen
            bool hasTouch = false;

            // Get screen touch position
            if (!_enableMouse)
            {
                hasTouch = TouchScreenManager.Update(true, (_inputManager.MouseDriver as SDL2MouseDriver).IsButtonPressed(MouseButton.Button1), _aspectRatio.ToFloat());
            }

            if (!hasTouch)
            {
                TouchScreenManager.Update(false);
            }

            Device.Hid.DebugPad.Update();

            // TODO: Replace this with MouseDriver.CheckIdle() when mouse motion events are received on every supported platform.
            MouseDriver.UpdatePosition();

            return true;
        }

        public void Execute()
        {
            _chrono.Restart();
            _isActive = true;

            InitializeWindow();

            Thread renderLoopThread = new(Render)
            {
                Name = "GUI.RenderLoop",
            };
            renderLoopThread.Start();

            Thread nvidiaStutterWorkaround = null;
            if (Renderer is OpenGLRenderer)
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

        public bool DisplayInputDialog(SoftwareKeyboardUIArgs args, out string userText)
        {
            // SDL2 doesn't support input dialogs
            userText = "Ryujinx";

            return true;
        }

        public bool DisplayMessageDialog(string title, string message)
        {
            SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_INFORMATION, title, message, WindowHandle);

            return true;
        }

        public bool DisplayMessageDialog(ControllerAppletUIArgs args)
        {
            string playerCount = args.PlayerCountMin == args.PlayerCountMax ? $"exactly {args.PlayerCountMin}" : $"{args.PlayerCountMin}-{args.PlayerCountMax}";

            string message = $"Application requests {playerCount} player(s) with:\n\n"
                           + $"TYPES: {args.SupportedStyles}\n\n"
                           + $"PLAYERS: {string.Join(", ", args.SupportedPlayers)}\n\n"
                           + (args.IsDocked ? "Docked mode set. Handheld is also invalid.\n\n" : "")
                           + "Please reconfigure Input now and then press OK.";

            return DisplayMessageDialog("Controller Applet", message);
        }

        public IDynamicTextInputHandler CreateDynamicTextInputHandler()
        {
            return new HeadlessDynamicTextInputHandler();
        }

        public void ExecuteProgram(Switch device, ProgramSpecifyKind kind, ulong value)
        {
            device.Configuration.UserChannelPersistence.ExecuteProgram(kind, value);

            Exit();
        }

        public bool DisplayErrorAppletDialog(string title, string message, string[] buttonsText)
        {
            SDL_MessageBoxData data = new()
            {
                title = title,
                message = message,
                buttons = new SDL_MessageBoxButtonData[buttonsText.Length],
                numbuttons = buttonsText.Length,
                window = WindowHandle,
            };

            for (int i = 0; i < buttonsText.Length; i++)
            {
                data.buttons[i] = new SDL_MessageBoxButtonData
                {
                    buttonid = i,
                    text = buttonsText[i],
                };
            }

            SDL_ShowMessageBox(ref data, out int _);

            return true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isActive = false;
                TouchScreenManager?.Dispose();
                NpadManager.Dispose();

                SDL2Driver.Instance.UnregisterWindow(_windowId);

                SDL_DestroyWindow(WindowHandle);

                SDL2Driver.Instance.Dispose();
            }
        }
    }
}
