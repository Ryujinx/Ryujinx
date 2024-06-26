using Ryujinx.Common.Logging;
using System.Collections.Generic;

namespace Ryujinx.UI.Common.Helper
{
    public static class CommandLineState
    {
        public static string[] Arguments { get; private set; }

        public static bool? OverrideDockedMode { get; private set; }
        public static bool? OverrideHardwareAcceleration { get; private set; }
        public static string OverrideGraphicsBackend { get; private set; }
        public static string OverrideHideCursor { get; private set; }
        public static string BaseDirPathArg { get; private set; }
        public static string Profile { get; private set; }
        public static string LaunchPathArg { get; private set; }
        public static string LaunchApplicationId { get; private set; }
        public static bool StartFullscreenArg { get; private set; }

        public static void ParseArguments(string[] args)
        {
            List<string> arguments = new();

            // Parse Arguments.
            for (int i = 0; i < args.Length; ++i)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "-r":
                    case "--root-data-dir":
                        if (i + 1 >= args.Length)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Invalid option '{arg}'");

                            continue;
                        }

                        BaseDirPathArg = args[++i];

                        arguments.Add(arg);
                        arguments.Add(args[i]);
                        break;
                    case "-p":
                    case "--profile":
                        if (i + 1 >= args.Length)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Invalid option '{arg}'");

                            continue;
                        }

                        Profile = args[++i];

                        arguments.Add(arg);
                        arguments.Add(args[i]);
                        break;
                    case "-f":
                    case "--fullscreen":
                        StartFullscreenArg = true;

                        arguments.Add(arg);
                        break;
                    case "-g":
                    case "--graphics-backend":
                        if (i + 1 >= args.Length)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Invalid option '{arg}'");

                            continue;
                        }

                        OverrideGraphicsBackend = args[++i];
                        break;
                    case "-i":
                    case "--application-id":
                        LaunchApplicationId = args[++i];
                        break;
                    case "--docked-mode":
                        OverrideDockedMode = true;
                        break;
                    case "--handheld-mode":
                        OverrideDockedMode = false;
                        break;
                    case "--hide-cursor":
                        if (i + 1 >= args.Length)
                        {
                            Logger.Error?.Print(LogClass.Application, $"Invalid option '{arg}'");

                            continue;
                        }

                        OverrideHideCursor = args[++i];
                        break;
                    case "--software-gui":
                        OverrideHardwareAcceleration = false;
                        break;
                    default:
                        LaunchPathArg = arg;
                        break;
                }
            }

            Arguments = arguments.ToArray();
        }
    }
}
