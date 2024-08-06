using Ryujinx.Common;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Memory;
using Ryujinx.Horizon.Sdk.Hid.HidDevices;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PlayerIndex = Ryujinx.Horizon.Sdk.Hid.Npad.PlayerIndex;

namespace Ryujinx.Horizon.Sdk.Hid
{
    public class Hid
    {
        internal const int HidSize = 0x40000;
        private readonly SharedMemoryStorage _storage;

        internal ref SharedMemory SharedMemory => ref _storage.GetRef<SharedMemory>(0);

        internal readonly int SharedMemEntryCount = 17;

        public DebugPadDevice DebugPad;
        public TouchDevice Touchscreen;
        public MouseDevice Mouse;
        public KeyboardDevice Keyboard;
        public NpadDevices Npads;

        private static void CheckTypeSizeOrThrow<T>(int expectedSize)
        {
            if (Unsafe.SizeOf<T>() != expectedSize)
            {
                throw new InvalidStructLayoutException<T>(expectedSize);
            }
        }

        static Hid()
        {
            CheckTypeSizeOrThrow<RingLifo<DebugPadState>>(0x2c8);
            CheckTypeSizeOrThrow<RingLifo<TouchScreenState>>(0x2C38);
            CheckTypeSizeOrThrow<RingLifo<MouseState>>(0x350);
            CheckTypeSizeOrThrow<RingLifo<KeyboardState>>(0x3D8);
            CheckTypeSizeOrThrow<Array10<NpadState>>(0x32000);
            CheckTypeSizeOrThrow<SharedMemory>(HidSize);
        }

        internal Hid(SharedMemoryStorage storage)
        {
            _storage = storage;

            SharedMemory = SharedMemory.Create();

            InitDevices();
        }

        private void InitDevices()
        {
            DebugPad = new DebugPadDevice(true);
            Touchscreen = new TouchDevice(true);
            Mouse = new MouseDevice(false);
            Keyboard = new KeyboardDevice(false);
            Npads = new NpadDevices(true);
        }

        public void RefreshInputConfig(List<InputConfig> inputConfig)
        {
            ControllerConfig[] npadConfig = new ControllerConfig[inputConfig.Count];

            for (int i = 0; i < npadConfig.Length; ++i)
            {
                npadConfig[i].Player = (PlayerIndex)inputConfig[i].PlayerIndex;
                npadConfig[i].Type = inputConfig[i].ControllerType;
            }

            Npads.Configure(npadConfig);
        }

        public ControllerKeys UpdateStickButtons(JoystickPosition leftStick, JoystickPosition rightStick)
        {
            const int StickButtonThreshold = short.MaxValue / 2;
            ControllerKeys result = 0;

#pragma warning disable IDE0055 // Disable formatting
            result |= (leftStick.Dx < -StickButtonThreshold) ? ControllerKeys.LStickLeft  : result;
            result |= (leftStick.Dx > StickButtonThreshold)  ? ControllerKeys.LStickRight : result;
            result |= (leftStick.Dy < -StickButtonThreshold) ? ControllerKeys.LStickDown  : result;
            result |= (leftStick.Dy > StickButtonThreshold)  ? ControllerKeys.LStickUp    : result;

            result |= (rightStick.Dx < -StickButtonThreshold) ? ControllerKeys.RStickLeft  : result;
            result |= (rightStick.Dx > StickButtonThreshold)  ? ControllerKeys.RStickRight : result;
            result |= (rightStick.Dy < -StickButtonThreshold) ? ControllerKeys.RStickDown  : result;
            result |= (rightStick.Dy > StickButtonThreshold)  ? ControllerKeys.RStickUp    : result;
#pragma warning restore IDE0055

            return result;
        }

        internal ulong GetTimestampTicks()
        {
            return (ulong)PerformanceCounter.ElapsedMilliseconds * 19200;
        }
    }
}
