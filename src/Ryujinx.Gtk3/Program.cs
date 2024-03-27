using Gtk;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.GraphicsDriver;
using Ryujinx.Common.Logging;
using Ryujinx.Common.SystemInterop;
using Ryujinx.Modules;
using Ryujinx.SDL2.Common;
using Ryujinx.UI;
using Ryujinx.UI.Common;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Helper;
using Ryujinx.UI.Common.SystemInfo;
using Ryujinx.UI.Widgets;
using SixLabors.ImageSharp.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ryujinx
{
    partial class Program
    {
        public static double WindowScaleFactor { get; private set; }

        public static string Version { get; private set; }

        public static string ConfigurationPath { get; set; }

        public static string CommandLineProfile { get; set; }

        private const string X11LibraryName = "libX11";

        [LibraryImport(X11LibraryName)]
        private static partial int XInitThreads();

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial int MessageBoxA(IntPtr hWnd, [MarshalAs(UnmanagedType.LPStr)] string text, [MarshalAs(UnmanagedType.LPStr)] string caption, uint type);

        [LibraryImport("libc", SetLastError = true)]
        private static partial int setenv([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.LPStr)] string value, int overwrite);

        private const uint MbIconWarning = 0x30;

        static Program()
        {
            if (OperatingSystem.IsLinux())
            {
                NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (name, assembly, path) =>
                {
                    if (name != X11LibraryName)
                    {
                        return IntPtr.Zero;
                    }

                    if (!NativeLibrary.TryLoad("libX11.so.6", assembly, path, out IntPtr result))
                    {
                        if (!NativeLibrary.TryLoad("libX11.so", assembly, path, out result))
                        {
                            return IntPtr.Zero;
                        }
                    }

                    return result;
                });
            }
        }

        static void Main(string[] args)
        {
            Version = ReleaseInformation.Version;

            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17134))
            {
                MessageBoxA(IntPtr.Zero, "You are running an outdated version of Windows.\n\nRyujinx supports Windows 10 version 1803 and newer.\n", $"Ryujinx {Version}", MbIconWarning);
            }

            // Parse arguments
            CommandLineState.ParseArguments(args);

            // Hook unhandled exception and process exit events.
            GLib.ExceptionManager.UnhandledException += (GLib.UnhandledExceptionArgs e) => ProcessUnhandledException(e.ExceptionObject as Exception, e.IsTerminating);
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) => ProcessUnhandledException(e.ExceptionObject as Exception, e.IsTerminating);
            AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => Exit();

            // Make process DPI aware for proper window sizing on high-res screens.
            ForceDpiAware.Windows();
            WindowScaleFactor = ForceDpiAware.GetWindowScaleFactor();

            // Delete backup files after updating.
            Task.Run(Updater.CleanupUpdate);

            Console.Title = $"Ryujinx Console {Version}";

            // NOTE: GTK3 doesn't init X11 in a multi threaded way.
            // This ends up causing race condition and abort of XCB when a context is created by SPB (even if SPB do call XInitThreads).
            if (OperatingSystem.IsLinux())
            {
                if (XInitThreads() == 0)
                {
                    throw new NotSupportedException("Failed to initialize multi-threading support.");
                }

                Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
                setenv("GDK_BACKEND", "x11", 1);
            }

            if (OperatingSystem.IsMacOS())
            {
                string baseDirectory = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                string resourcesDataDir;

                if (Path.GetFileName(baseDirectory) == "MacOS")
                {
                    resourcesDataDir = Path.Combine(Directory.GetParent(baseDirectory).FullName, "Resources");
                }
                else
                {
                    resourcesDataDir = baseDirectory;
                }

                static void SetEnvironmentVariableNoCaching(string key, string value)
                {
                    int res = setenv(key, value, 1);
                    Debug.Assert(res != -1);
                }

                // On macOS, GTK3 needs XDG_DATA_DIRS to be set, otherwise it will try searching for "gschemas.compiled" in system directories.
                SetEnvironmentVariableNoCaching("XDG_DATA_DIRS", Path.Combine(resourcesDataDir, "share"));

                // On macOS, GTK3 needs GDK_PIXBUF_MODULE_FILE to be set, otherwise it will try searching for "loaders.cache" in system directories.
                SetEnvironmentVariableNoCaching("GDK_PIXBUF_MODULE_FILE", Path.Combine(resourcesDataDir, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders.cache"));

                SetEnvironmentVariableNoCaching("GTK_IM_MODULE_FILE", Path.Combine(resourcesDataDir, "lib", "gtk-3.0", "3.0.0", "immodules.cache"));
            }

            string systemPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("Path", $"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin")};{systemPath}");

            // Setup base data directory.
            AppDataManager.Initialize(CommandLineState.BaseDirPathArg);

            // Initialize the configuration.
            ConfigurationState.Initialize();

            // Initialize the logger system.
            LoggerModule.Initialize();

            // Initialize Discord integration.
            DiscordIntegrationModule.Initialize();

            // Initialize SDL2 driver
            SDL2Driver.MainThreadDispatcher = action =>
            {
                Application.Invoke(delegate
                {
                    action();
                });
            };

            // Sets ImageSharp Jpeg Encoder Quality.
            SixLabors.ImageSharp.Configuration.Default.ImageFormatsManager.SetEncoder(JpegFormat.Instance, new JpegEncoder()
            {
                Quality = 100,
            });

            string localConfigurationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ReleaseInformation.ConfigName);
            string appDataConfigurationPath = Path.Combine(AppDataManager.BaseDirPath, ReleaseInformation.ConfigName);

            // Now load the configuration as the other subsystems are now registered
            ConfigurationPath = File.Exists(localConfigurationPath)
                ? localConfigurationPath
                : File.Exists(appDataConfigurationPath)
                    ? appDataConfigurationPath
                    : null;

            if (ConfigurationPath == null)
            {
                // No configuration, we load the default values and save it to disk
                ConfigurationPath = appDataConfigurationPath;

                ConfigurationState.Instance.LoadDefault();
                ConfigurationState.Instance.ToFileFormat().SaveConfig(ConfigurationPath);
            }
            else
            {
                if (ConfigurationFileFormat.TryLoad(ConfigurationPath, out ConfigurationFileFormat configurationFileFormat))
                {
                    ConfigurationState.Instance.Load(configurationFileFormat, ConfigurationPath);
                }
                else
                {
                    ConfigurationState.Instance.LoadDefault();

                    Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to load config! Loading the default config instead.\nFailed config location {ConfigurationPath}");
                }
            }

            // Check if graphics backend was overridden.
            if (CommandLineState.OverrideGraphicsBackend != null)
            {
                if (CommandLineState.OverrideGraphicsBackend.ToLower() == "opengl")
                {
                    ConfigurationState.Instance.Graphics.GraphicsBackend.Value = GraphicsBackend.OpenGl;
                }
                else if (CommandLineState.OverrideGraphicsBackend.ToLower() == "vulkan")
                {
                    ConfigurationState.Instance.Graphics.GraphicsBackend.Value = GraphicsBackend.Vulkan;
                }
            }

            // Check if HideCursor was overridden.
            if (CommandLineState.OverrideHideCursor is not null)
            {
                ConfigurationState.Instance.HideCursor.Value = CommandLineState.OverrideHideCursor!.ToLower() switch
                {
                    "never" => HideCursorMode.Never,
                    "onidle" => HideCursorMode.OnIdle,
                    "always" => HideCursorMode.Always,
                    _ => ConfigurationState.Instance.HideCursor.Value,
                };
            }

            // Check if docked mode was overridden.
            if (CommandLineState.OverrideDockedMode.HasValue)
            {
                ConfigurationState.Instance.System.EnableDockedMode.Value = CommandLineState.OverrideDockedMode.Value;
            }

            // Logging system information.
            PrintSystemInfo();

            // Enable OGL multithreading on the driver, when available.
            BackendThreading threadingMode = ConfigurationState.Instance.Graphics.BackendThreading;
            DriverUtilities.ToggleOGLThreading(threadingMode == BackendThreading.Off);

            // Initialize Gtk.
            Application.Init();

            // Check if keys exists.
            bool hasSystemProdKeys = File.Exists(Path.Combine(AppDataManager.KeysDirPath, "prod.keys"));
            bool hasCommonProdKeys = AppDataManager.Mode == AppDataManager.LaunchMode.UserProfile && File.Exists(Path.Combine(AppDataManager.KeysDirPathUser, "prod.keys"));
            if (!hasSystemProdKeys && !hasCommonProdKeys)
            {
                UserErrorDialog.CreateUserErrorDialog(UserError.NoKeys);
            }

            // Show the main window UI.
            MainWindow mainWindow = new();
            mainWindow.Show();

            if (OperatingSystem.IsLinux())
            {
                int currentVmMaxMapCount = LinuxHelper.VmMaxMapCount;

                if (LinuxHelper.VmMaxMapCount < LinuxHelper.RecommendedVmMaxMapCount)
                {
                    Logger.Warning?.Print(LogClass.Application, $"The value of vm.max_map_count is lower than {LinuxHelper.RecommendedVmMaxMapCount}. ({currentVmMaxMapCount})");

                    if (LinuxHelper.PkExecPath is not null)
                    {
                        var buttonTexts = new Dictionary<int, string>()
                        {
                            { 0, "Yes, until the next restart" },
                            { 1, "Yes, permanently" },
                            { 2, "No" },
                        };

                        ResponseType response = GtkDialog.CreateCustomDialog(
                            "Ryujinx - Low limit for memory mappings detected",
                            $"Would you like to increase the value of vm.max_map_count to {LinuxHelper.RecommendedVmMaxMapCount}?",
                            "Some games might try to create more memory mappings than currently allowed. " +
                            "Ryujinx will crash as soon as this limit gets exceeded.",
                            buttonTexts,
                            MessageType.Question);

                        int rc;

                        switch ((int)response)
                        {
                            case 0:
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
                            case 1:
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
                    else
                    {
                        GtkDialog.CreateWarningDialog(
                            "Max amount of memory mappings is lower than recommended.",
                            $"The current value of vm.max_map_count ({currentVmMaxMapCount}) is lower than {LinuxHelper.RecommendedVmMaxMapCount}." +
                            "Some games might try to create more memory mappings than currently allowed. " +
                            "Ryujinx will crash as soon as this limit gets exceeded.\n\n" +
                            "You might want to either manually increase the limit or install pkexec, which allows Ryujinx to assist with that.");
                    }
                }
            }

            if (CommandLineState.LaunchPathArg != null)
            {
                mainWindow.RunApplication(CommandLineState.LaunchPathArg, CommandLineState.StartFullscreenArg);
            }

            if (ConfigurationState.Instance.CheckUpdatesOnStart.Value && Updater.CanUpdate(false))
            {
                Updater.BeginParse(mainWindow, false).ContinueWith(task =>
                {
                    Logger.Error?.Print(LogClass.Application, $"Updater Error: {task.Exception}");
                }, TaskContinuationOptions.OnlyOnFaulted);
            }

            Application.Run();
        }

        private static void PrintSystemInfo()
        {
            Logger.Notice.Print(LogClass.Application, $"Ryujinx Version: {Version}");
            SystemInfo.Gather().Print();

            var enabledLogs = Logger.GetEnabledLevels();
            Logger.Notice.Print(LogClass.Application, $"Logs Enabled: {(enabledLogs.Count == 0 ? "<None>" : string.Join(", ", enabledLogs))}");

            if (AppDataManager.Mode == AppDataManager.LaunchMode.Custom)
            {
                Logger.Notice.Print(LogClass.Application, $"Launch Mode: Custom Path {AppDataManager.BaseDirPath}");
            }
            else
            {
                Logger.Notice.Print(LogClass.Application, $"Launch Mode: {AppDataManager.Mode}");
            }
        }

        private static void ProcessUnhandledException(Exception ex, bool isTerminating)
        {
            string message = $"Unhandled exception caught: {ex}";

            Logger.Error?.PrintMsg(LogClass.Application, message);

            if (Logger.Error == null)
            {
                Logger.Notice.PrintMsg(LogClass.Application, message);
            }

            if (isTerminating)
            {
                Exit();
            }
        }

        public static void Exit()
        {
            DiscordIntegrationModule.Exit();

            Logger.Shutdown();
        }
    }
}
