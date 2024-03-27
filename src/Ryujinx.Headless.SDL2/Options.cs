using CommandLine;
using Ryujinx.Common.Configuration;
using Ryujinx.HLE.HOS.SystemState;

namespace Ryujinx.Headless.SDL2
{
    public class Options
    {
        // General

        [Option("root-data-dir", Required = false, HelpText = "Set the custom folder path for Ryujinx data.")]
        public string BaseDataDir { get; set; }

        [Option("profile", Required = false, HelpText = "Set the user profile to launch the game with.")]
        public string UserProfile { get; set; }

        [Option("display-id", Required = false, Default = 0, HelpText = "Set the display to use - especially helpful for fullscreen mode. [0-n]")]
        public int DisplayId { get; set; }

        [Option("fullscreen", Required = false, Default = false, HelpText = "Launch the game in fullscreen mode.")]
        public bool IsFullscreen { get; set; }

        [Option("exclusive-fullscreen", Required = false, Default = false, HelpText = "Launch the game in exclusive fullscreen mode.")]
        public bool IsExclusiveFullscreen { get; set; }

        [Option("exclusive-fullscreen-width", Required = false, Default = 1920, HelpText = "Set horizontal resolution for exclusive fullscreen mode.")]
        public int ExclusiveFullscreenWidth { get; set; }

        [Option("exclusive-fullscreen-height", Required = false, Default = 1080, HelpText = "Set vertical resolution for exclusive fullscreen mode.")]
        public int ExclusiveFullscreenHeight { get; set; }

        // Input

        [Option("input-profile-1", Required = false, HelpText = "Set the input profile in use for Player 1.")]
        public string InputProfile1Name { get; set; }

        [Option("input-profile-2", Required = false, HelpText = "Set the input profile in use for Player 2.")]
        public string InputProfile2Name { get; set; }

        [Option("input-profile-3", Required = false, HelpText = "Set the input profile in use for Player 3.")]
        public string InputProfile3Name { get; set; }

        [Option("input-profile-4", Required = false, HelpText = "Set the input profile in use for Player 4.")]
        public string InputProfile4Name { get; set; }

        [Option("input-profile-5", Required = false, HelpText = "Set the input profile in use for Player 5.")]
        public string InputProfile5Name { get; set; }

        [Option("input-profile-6", Required = false, HelpText = "Set the input profile in use for Player 6.")]
        public string InputProfile6Name { get; set; }

        [Option("input-profile-7", Required = false, HelpText = "Set the input profile in use for Player 7.")]
        public string InputProfile7Name { get; set; }

        [Option("input-profile-8", Required = false, HelpText = "Set the input profile in use for Player 8.")]
        public string InputProfile8Name { get; set; }

        [Option("input-profile-handheld", Required = false, HelpText = "Set the input profile in use for the Handheld Player.")]
        public string InputProfileHandheldName { get; set; }

        [Option("input-id-1", Required = false, HelpText = "Set the input id in use for Player 1.")]
        public string InputId1 { get; set; }

        [Option("input-id-2", Required = false, HelpText = "Set the input id in use for Player 2.")]
        public string InputId2 { get; set; }

        [Option("input-id-3", Required = false, HelpText = "Set the input id in use for Player 3.")]
        public string InputId3 { get; set; }

        [Option("input-id-4", Required = false, HelpText = "Set the input id in use for Player 4.")]
        public string InputId4 { get; set; }

        [Option("input-id-5", Required = false, HelpText = "Set the input id in use for Player 5.")]
        public string InputId5 { get; set; }

        [Option("input-id-6", Required = false, HelpText = "Set the input id in use for Player 6.")]
        public string InputId6 { get; set; }

        [Option("input-id-7", Required = false, HelpText = "Set the input id in use for Player 7.")]
        public string InputId7 { get; set; }

        [Option("input-id-8", Required = false, HelpText = "Set the input id in use for Player 8.")]
        public string InputId8 { get; set; }

        [Option("input-id-handheld", Required = false, HelpText = "Set the input id in use for the Handheld Player.")]
        public string InputIdHandheld { get; set; }

        [Option("enable-keyboard", Required = false, Default = false, HelpText = "Enable or disable keyboard support (Independent from controllers binding).")]
        public bool EnableKeyboard { get; set; }

        [Option("enable-mouse", Required = false, Default = false, HelpText = "Enable or disable mouse support.")]
        public bool EnableMouse { get; set; }

        [Option("hide-cursor", Required = false, Default = HideCursorMode.OnIdle, HelpText = "Change when the cursor gets hidden.")]
        public HideCursorMode HideCursorMode { get; set; }

        [Option("list-input-profiles", Required = false, HelpText = "List inputs profiles.")]
        public bool ListInputProfiles { get; set; }

        [Option("list-inputs-ids", Required = false, HelpText = "List inputs ids.")]
        public bool ListInputIds { get; set; }

        // System

        [Option("disable-ptc", Required = false, HelpText = "Disables profiled persistent translation cache.")]
        public bool DisablePTC { get; set; }

        [Option("enable-internet-connection", Required = false, Default = false, HelpText = "Enables guest Internet connection.")]
        public bool EnableInternetAccess { get; set; }

        [Option("disable-fs-integrity-checks", Required = false, HelpText = "Disables integrity checks on Game content files.")]
        public bool DisableFsIntegrityChecks { get; set; }

        [Option("fs-global-access-log-mode", Required = false, Default = 0, HelpText = "Enables FS access log output to the console.")]
        public int FsGlobalAccessLogMode { get; set; }

        [Option("turbo-multiplier", Required = false, Default = 200, HelpText = "The Turbo mode clock speed multiplier.")]
        public long TurboMultiplier { get; set; }

        [Option("disable-vsync", Required = false, HelpText = "Disables Vertical Sync.")]
        public bool DisableVSync { get; set; }

        [Option("disable-shader-cache", Required = false, HelpText = "Disables Shader cache.")]
        public bool DisableShaderCache { get; set; }

        [Option("enable-texture-recompression", Required = false, Default = false, HelpText = "Enables Texture recompression.")]
        public bool EnableTextureRecompression { get; set; }

        [Option("disable-docked-mode", Required = false, HelpText = "Disables Docked Mode.")]
        public bool DisableDockedMode { get; set; }

        [Option("system-language", Required = false, Default = SystemLanguage.AmericanEnglish, HelpText = "Change System Language.")]
        public SystemLanguage SystemLanguage { get; set; }

        [Option("system-region", Required = false, Default = RegionCode.USA, HelpText = "Change System Region.")]
        public RegionCode SystemRegion { get; set; }

        [Option("system-timezone", Required = false, Default = "UTC", HelpText = "Change System TimeZone.")]
        public string SystemTimeZone { get; set; }

        [Option("system-time-offset", Required = false, Default = 0, HelpText = "Change System Time Offset in seconds.")]
        public long SystemTimeOffset { get; set; }

        [Option("memory-manager-mode", Required = false, Default = MemoryManagerMode.HostMappedUnsafe, HelpText = "The selected memory manager mode.")]
        public MemoryManagerMode MemoryManagerMode { get; set; }

        [Option("audio-volume", Required = false, Default = 1.0f, HelpText = "The audio level (0 to 1).")]
        public float AudioVolume { get; set; }

        [Option("use-hypervisor", Required = false, Default = true, HelpText = "Uses Hypervisor over JIT if available.")]
        public bool? UseHypervisor { get; set; }

        [Option("lan-interface-id", Required = false, Default = "0", HelpText = "GUID for the network interface used by LAN.")]
        public string MultiplayerLanInterfaceId { get; set; }

        // Logging

        [Option("disable-file-logging", Required = false, Default = false, HelpText = "Disables logging to a file on disk.")]
        public bool DisableFileLog { get; set; }

        [Option("enable-debug-logs", Required = false, Default = false, HelpText = "Enables printing debug log messages.")]
        public bool LoggingEnableDebug { get; set; }

        [Option("disable-stub-logs", Required = false, HelpText = "Disables printing stub log messages.")]
        public bool LoggingDisableStub { get; set; }

        [Option("disable-info-logs", Required = false, HelpText = "Disables printing info log messages.")]
        public bool LoggingDisableInfo { get; set; }

        [Option("disable-warning-logs", Required = false, HelpText = "Disables printing warning log messages.")]
        public bool LoggingDisableWarning { get; set; }

        [Option("disable-error-logs", Required = false, HelpText = "Disables printing error log messages.")]
        public bool LoggingEnableError { get; set; }

        [Option("enable-trace-logs", Required = false, Default = false, HelpText = "Enables printing trace log messages.")]
        public bool LoggingEnableTrace { get; set; }

        [Option("disable-guest-logs", Required = false, HelpText = "Disables printing guest log messages.")]
        public bool LoggingDisableGuest { get; set; }

        [Option("enable-fs-access-logs", Required = false, Default = false, HelpText = "Enables printing FS access log messages.")]
        public bool LoggingEnableFsAccessLog { get; set; }

        [Option("graphics-debug-level", Required = false, Default = GraphicsDebugLevel.None, HelpText = "Change Graphics API debug log level.")]
        public GraphicsDebugLevel LoggingGraphicsDebugLevel { get; set; }

        // Graphics

        [Option("resolution-scale", Required = false, Default = 1, HelpText = "Resolution Scale. A floating point scale applied to applicable render targets.")]
        public float ResScale { get; set; }

        [Option("max-anisotropy", Required = false, Default = -1, HelpText = "Max Anisotropy. Values range from 0 - 16. Set to -1 to let the game decide.")]
        public float MaxAnisotropy { get; set; }

        [Option("aspect-ratio", Required = false, Default = AspectRatio.Fixed16x9, HelpText = "Aspect Ratio applied to the renderer window.")]
        public AspectRatio AspectRatio { get; set; }

        [Option("backend-threading", Required = false, Default = BackendThreading.Auto, HelpText = "Whether or not backend threading is enabled. The \"Auto\" setting will determine whether threading should be enabled at runtime.")]
        public BackendThreading BackendThreading { get; set; }

        [Option("disable-macro-hle", Required = false, HelpText = "Disables high-level emulation of Macro code. Leaving this enabled improves performance but may cause graphical glitches in some games.")]
        public bool DisableMacroHLE { get; set; }

        [Option("graphics-shaders-dump-path", Required = false, HelpText = "Dumps shaders in this local directory. (Developer only)")]
        public string GraphicsShadersDumpPath { get; set; }

        [Option("graphics-backend", Required = false, Default = GraphicsBackend.OpenGl, HelpText = "Change Graphics Backend to use.")]
        public GraphicsBackend GraphicsBackend { get; set; }

        [Option("preferred-gpu-vendor", Required = false, Default = "", HelpText = "When using the Vulkan backend, prefer using the GPU from the specified vendor.")]
        public string PreferredGPUVendor { get; set; }

        [Option("anti-aliasing", Required = false, Default = AntiAliasing.None, HelpText = "Set the type of anti aliasing being used. [None|Fxaa|SmaaLow|SmaaMedium|SmaaHigh|SmaaUltra]")]
        public AntiAliasing AntiAliasing { get; set; }

        [Option("scaling-filter", Required = false, Default = ScalingFilter.Bilinear, HelpText = "Set the scaling filter. [Bilinear|Nearest|Fsr]")]
        public ScalingFilter ScalingFilter { get; set; }

        [Option("scaling-filter-level", Required = false, Default = 0, HelpText = "Set the scaling filter intensity (currently only applies to FSR). [0-100]")]
        public int ScalingFilterLevel { get; set; }

        // Hacks

        [Option("expand-ram", Required = false, Default = false, HelpText = "Expands the RAM amount on the emulated system from 4GiB to 6GiB.")]
        public bool ExpandRAM { get; set; }

        [Option("ignore-missing-services", Required = false, Default = false, HelpText = "Enable ignoring missing services.")]
        public bool IgnoreMissingServices { get; set; }

        // Values

        [Value(0, MetaName = "input", HelpText = "Input to load.", Required = true)]
        public string InputPath { get; set; }
    }
}
