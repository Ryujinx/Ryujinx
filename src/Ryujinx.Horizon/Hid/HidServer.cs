using Ryujinx.Common.Logging;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Hid;
using Ryujinx.Horizon.Sdk.Hid.HidDevices;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using Ryujinx.Horizon.Sdk.Hid.SixAxis;
using Ryujinx.Horizon.Sdk.Hid.Vibration;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Collections.Generic;

namespace Ryujinx.Horizon.Hid
{
    partial class HidServer : IHidServer
    {
        private SystemEventType _xpadIdEvent;
        private SystemEventType _palmaOperationCompleteEvent;

        private bool _sixAxisSensorFusionEnabled;
        private bool _unintendedHomeButtonInputProtectionEnabled;
        private bool _npadAnalogStickCenterClampEnabled;
        private bool _vibrationPermitted;
        private bool _usbFullKeyControllerEnabled;
        private readonly bool _isFirmwareUpdateAvailableForSixAxisSensor;
        private bool _isSixAxisSensorUnalteredPassthroughEnabled;

        private NpadHandheldActivationMode _npadHandheldActivationMode;
        private GyroscopeZeroDriftMode _gyroscopeZeroDriftMode;

        private long _npadCommunicationMode;
        private uint _accelerometerPlayMode;
        private readonly VibrationGcErmCommand _vibrationGcErmCommand;
        private float _sevenSixAxisSensorFusionStrength;

        private SensorFusionParameters _sensorFusionParams;
        private AccelerometerParameters _accelerometerParams;

        public HidServer()
        {
            Os.CreateSystemEvent(out _xpadIdEvent, EventClearMode.ManualClear, interProcess: true);
            Os.SignalSystemEvent(ref _xpadIdEvent); // TODO: signal event at right place

            Os.CreateSystemEvent(out _palmaOperationCompleteEvent, EventClearMode.ManualClear, interProcess: true);

            _npadHandheldActivationMode = NpadHandheldActivationMode.Dual;
            _gyroscopeZeroDriftMode = GyroscopeZeroDriftMode.Standard;

            _isFirmwareUpdateAvailableForSixAxisSensor = false;

            _sensorFusionParams = new SensorFusionParameters();
            _accelerometerParams = new AccelerometerParameters();

            _vibrationPermitted = true;
        }

        [CmifCommand(0)]
        public Result CreateAppletResource(out IAppletResource arg0, AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(1)]
        public Result ActivateDebugPad(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            // Initialize entries to avoid issues with some games.

            for (int i = 0; i < HorizonStatic.Hid.SharedMemEntryCount; i++)
            {
                HorizonStatic.Hid.DebugPad.Update();
            }

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(11)]
        public Result ActivateTouchScreen(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            HorizonStatic.Hid.Touchscreen.Active = true;

            // Initialize entries to avoid issues with some games.

            for (int i = 0; i < HorizonStatic.Hid.SharedMemEntryCount; i++)
            {
                HorizonStatic.Hid.Touchscreen.Update();
            }

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(21)]
        public Result ActivateMouse(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            HorizonStatic.Hid.Mouse.Active = true;

            // Initialize entries to avoid issues with some games.

            for (int i = 0; i < HorizonStatic.Hid.SharedMemEntryCount; i++)
            {
                HorizonStatic.Hid.Mouse.Update(0, 0);
            }

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(31)]
        public Result ActivateKeyboard(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            HorizonStatic.Hid.Keyboard.Active = true;

            // Initialize entries to avoid issues with some games.

            KeyboardInput emptyInput = new()
            {
                Keys = new ulong[4],
            };

            for (int i = 0; i < HorizonStatic.Hid.SharedMemEntryCount; i++)
            {
                HorizonStatic.Hid.Keyboard.Update(emptyInput);
            }

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(32)]
        public Result SendKeyboardLockKeyEvent(AppletResourceUserId appletResourceUserId, KeyboardLockKeyEvent keyboardLockKeyEvent, [ClientProcessId] ulong pid)
        {
            // NOTE: This signals the keyboard driver about lock events.

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { keyboardLockKeyEvent });

            return Result.Success;
        }

        [CmifCommand(40)]
        public Result AcquireXpadIdEventHandle([CopyHandle] out int handle, ulong xpadId)
        {
            handle = Os.GetReadableHandleOfSystemEvent(ref _xpadIdEvent);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { xpadId });

            return Result.Success;
        }

        [CmifCommand(41)]
        public Result ReleaseXpadIdEventHandle(ulong xpadId)
        {
            Os.DetachReadableHandleOfSystemEvent(ref _xpadIdEvent);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { xpadId });

            return Result.Success;
        }

        [CmifCommand(51)]
        public Result ActivateXpad(AppletResourceUserId appletResourceUserId, uint basicXpadId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, basicXpadId });

            return Result.Success;
        }

        [CmifCommand(55)]
        public Result GetXpadIds(out long idCount, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer)] Span<uint> basicXpadIds)
        {
            // There aren't any Xpads, so we return 0 and write nothing inside the buffer.
            idCount = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(56)]
        public Result ActivateJoyXpad(uint joyXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return Result.Success;
        }

        [CmifCommand(58)]
        public Result GetJoyXpadLifoHandle([CopyHandle] out int arg0, uint joyXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return Result.Success;
        }

        [CmifCommand(59)]
        public Result GetJoyXpadIds(out long idCount, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer)] Span<uint> joyXpadIds)
        {
            // There aren't any JoyXpad, so we return 0 and write nothing inside the buffer.
            idCount = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(60)]
        public Result ActivateSixAxisSensor(uint basicXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { basicXpadId });

            return Result.Success;
        }

        [CmifCommand(61)]
        public Result DeactivateSixAxisSensor(uint basicXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { basicXpadId });

            return Result.Success;
        }

        [CmifCommand(62)]
        public Result GetSixAxisSensorLifoHandle([CopyHandle] out int arg0, uint basicXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { basicXpadId });

            return Result.Success;
        }

        [CmifCommand(63)]
        public Result ActivateJoySixAxisSensor(uint joyXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return Result.Success;
        }

        [CmifCommand(64)]
        public Result DeactivateJoySixAxisSensor(uint joyXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return Result.Success;
        }

        [CmifCommand(65)]
        public Result GetJoySixAxisSensorLifoHandle([CopyHandle] out int arg0, uint joyXpadId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return Result.Success;
        }

        [CmifCommand(66)]
        public Result StartSixAxisSensor(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(67)]
        public Result StopSixAxisSensor(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(68)]
        public Result IsSixAxisSensorFusionEnabled(out bool sixAxisSensorFusionEnabled, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            sixAxisSensorFusionEnabled = _sixAxisSensorFusionEnabled;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _sixAxisSensorFusionEnabled });

            return Result.Success;
        }

        [CmifCommand(69)]
        public Result EnableSixAxisSensorFusion(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, bool sixAxisSensorFusionEnabled, [ClientProcessId] ulong pid)
        {
            _sixAxisSensorFusionEnabled = sixAxisSensorFusionEnabled;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _sixAxisSensorFusionEnabled });

            return Result.Success;
        }

        [CmifCommand(70)]
        public Result SetSixAxisSensorFusionParameters(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, float revisePower, float reviseRange, [ClientProcessId] ulong pid)
        {
            _sensorFusionParams = new SensorFusionParameters
            {
                RevisePower = revisePower,
                ReviseRange = reviseRange
            };

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _sensorFusionParams.RevisePower, _sensorFusionParams.ReviseRange });

            return Result.Success;
        }

        [CmifCommand(71)]
        public Result GetSixAxisSensorFusionParameters(out float revisePower, out float reviseRange, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            revisePower = _sensorFusionParams.RevisePower;
            reviseRange = _sensorFusionParams.ReviseRange;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _sensorFusionParams.RevisePower, _sensorFusionParams.ReviseRange });

            return Result.Success;
        }

        [CmifCommand(72)]
        public Result ResetSixAxisSensorFusionParameters(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            _sensorFusionParams.RevisePower = 0;
            _sensorFusionParams.ReviseRange = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _sensorFusionParams.RevisePower, _sensorFusionParams.ReviseRange });

            return Result.Success;
        }

        [CmifCommand(73)]
        public Result SetAccelerometerParameters(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, float x, float y, [ClientProcessId] ulong pid)
        {
            _accelerometerParams = new AccelerometerParameters
            {
                X = x,
                Y = y,
            };

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _accelerometerParams.X, _accelerometerParams.Y });

            return Result.Success;
        }

        [CmifCommand(74)]
        public Result GetAccelerometerParameters(out float x, out float y, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            x = _accelerometerParams.X;
            y = _accelerometerParams.Y;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _accelerometerParams.X, _accelerometerParams.Y });

            return Result.Success;
        }

        [CmifCommand(75)]
        public Result ResetAccelerometerParameters(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            _accelerometerParams.X = 0;
            _accelerometerParams.Y = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _accelerometerParams.X, _accelerometerParams.Y });

            return Result.Success;
        }

        [CmifCommand(76)]
        public Result SetAccelerometerPlayMode(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, uint accelerometerPlayMode, [ClientProcessId] ulong pid)
        {
            _accelerometerPlayMode = accelerometerPlayMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _accelerometerPlayMode });

            return Result.Success;
        }

        [CmifCommand(77)]
        public Result GetAccelerometerPlayMode(out uint accelerometerPlayMode, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            accelerometerPlayMode = _accelerometerPlayMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _accelerometerPlayMode });

            return Result.Success;
        }

        [CmifCommand(78)]
        public Result ResetAccelerometerPlayMode(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            _accelerometerPlayMode = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _accelerometerPlayMode });

            return Result.Success;
        }

        [CmifCommand(79)]
        public Result SetGyroscopeZeroDriftMode(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, GyroscopeZeroDriftMode gyroscopeZeroDriftMode, [ClientProcessId] ulong pid)
        {
            _gyroscopeZeroDriftMode = gyroscopeZeroDriftMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _gyroscopeZeroDriftMode });

            return Result.Success;
        }

        [CmifCommand(80)]
        public Result GetGyroscopeZeroDriftMode(out GyroscopeZeroDriftMode gyroscopeZeroDriftMode, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            gyroscopeZeroDriftMode = _gyroscopeZeroDriftMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _gyroscopeZeroDriftMode });

            return Result.Success;
        }

        [CmifCommand(81)]
        public Result ResetGyroscopeZeroDriftMode(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            _gyroscopeZeroDriftMode = GyroscopeZeroDriftMode.Standard;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _gyroscopeZeroDriftMode });

            return Result.Success;
        }

        [CmifCommand(82)]
        public Result IsSixAxisSensorAtRest(out bool isAtRest, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            isAtRest = true;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, isAtRest });

            return Result.Success;
        }

        [CmifCommand(83)]
        public Result IsFirmwareUpdateAvailableForSixAxisSensor(out bool isFirmwareUpdateAvailableForSixAxisSensor, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            isFirmwareUpdateAvailableForSixAxisSensor = _isFirmwareUpdateAvailableForSixAxisSensor;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _isFirmwareUpdateAvailableForSixAxisSensor });

            return Result.Success;
        }

        [CmifCommand(84)]
        public Result EnableSixAxisSensorUnalteredPassthrough(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, bool sixAxisSensorUnalteredPassthrough, [ClientProcessId] ulong pid)
        {
            _isSixAxisSensorUnalteredPassthroughEnabled = sixAxisSensorUnalteredPassthrough;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle, _isSixAxisSensorUnalteredPassthroughEnabled });

            return Result.Success;
        }

        [CmifCommand(85)]
        public Result IsSixAxisSensorUnalteredPassthroughEnabled(out bool sixAxisSensorUnalteredPassthrough, AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            sixAxisSensorUnalteredPassthrough = _isSixAxisSensorUnalteredPassthroughEnabled;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(86)]
        public Result StoreSixAxisSensorCalibrationParameter(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias, 0x744)] in SixAxisSensorCalibrationParameter sixAxisSensorCalibrationParameter, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { sixAxisSensorHandle, sixAxisSensorCalibrationParameter });

            return Result.Success;
        }

        [CmifCommand(87)]
        public Result LoadSixAxisSensorCalibrationParameter(AppletResourceUserId appletResourceUserId, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias, 0x744)] out SixAxisSensorCalibrationParameter sixAxisSensorCalibrationParameter, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            // TODO: CalibrationParameter have to be determined.

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(88)]
        public Result GetSixAxisSensorIcInformation(AppletResourceUserId appletResourceUserId, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0xC8)] out SixAxisSensorIcInformation sixAxisSensorIcInformation, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            // TODO: IcInformation have to be determined.

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, sixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(89)]
        public Result ResetIsSixAxisSensorDeviceNewlyAssigned(AppletResourceUserId appletResourceUserId, SixAxisSensorHandle sixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(91)]
        public Result ActivateGesture(AppletResourceUserId appletResourceUserId, int unknown, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, unknown });

            return Result.Success;
        }

        [CmifCommand(100)]
        public Result SetSupportedNpadStyleSet(AppletResourceUserId appletResourceUserId, NpadStyleTag supportedStyleSets, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { pid, appletResourceUserId, supportedStyleSets });

            HorizonStatic.Hid.Npads.SupportedStyleSets = supportedStyleSets;

            return Result.Success;
        }

        [CmifCommand(101)]
        public Result GetSupportedNpadStyleSet(AppletResourceUserId appletResourceUserId, out NpadStyleTag supportedStyleSets, [ClientProcessId] ulong pid)
        {
            supportedStyleSets = HorizonStatic.Hid.Npads.SupportedStyleSets;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, HorizonStatic.Hid.Npads.SupportedStyleSets });

            return Result.Success;
        }

        [CmifCommand(102)]
        public Result SetSupportedNpadIdType(AppletResourceUserId appletResourceUserId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<NpadIdType> npadIds, [ClientProcessId] ulong pid)
        {
            HorizonStatic.Hid.Npads.ClearSupportedPlayers();

            for (int i = 0; i < npadIds.Length; i++)
            {
                if (IsValidNpadIdType(npadIds[i]))
                {
                    HorizonStatic.Hid.Npads.SetSupportedPlayer(GetIndexFromNpadIdType(npadIds[i]));
                }
            }

            Logger.Stub?.PrintStub(LogClass.ServiceHid, $"{npadIds.Length} Players: " + string.Join(",", npadIds.ToArray()));

            return Result.Success;
        }

        [CmifCommand(103)]
        public Result ActivateNpad(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(104)]
        public Result DeactivateNpad(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            HorizonStatic.Hid.Npads.Active = false;
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(106)]
        public Result AcquireNpadStyleSetUpdateEventHandle(AppletResourceUserId appletResourceUserId, [CopyHandle] out int arg1, uint arg2, ulong arg3, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(107)]
        public Result DisconnectNpad(AppletResourceUserId appletResourceUserId, NpadIdType npadIdType, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, npadIdType });

            return Result.Success;
        }

        [CmifCommand(108)]
        public Result GetPlayerLedPattern(out ulong ledPattern, NpadIdType npadId)
        {
            ledPattern = npadId switch
            {
                NpadIdType.Player1 => 0b0001,
                NpadIdType.Player2 => 0b0011,
                NpadIdType.Player3 => 0b0111,
                NpadIdType.Player4 => 0b1111,
                NpadIdType.Player5 => 0b1001,
                NpadIdType.Player6 => 0b0101,
                NpadIdType.Player7 => 0b1101,
                NpadIdType.Player8 => 0b0110,
                NpadIdType.Unknown => 0b0000,
                NpadIdType.Handheld => 0b0000,
                _ => throw new InvalidOperationException($"{nameof(npadId)} contains an invalid value: {npadId}"),
            };

            return Result.Success;
        }

        [CmifCommand(109)]
        public Result ActivateNpadWithRevision(AppletResourceUserId appletResourceUserId, int arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(120)]
        public Result SetNpadJoyHoldType(AppletResourceUserId appletResourceUserId, long arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(121)]
        public Result GetNpadJoyHoldType(AppletResourceUserId appletResourceUserId, out long arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(122)]
        public Result SetNpadJoyAssignmentModeSingleByDefault(AppletResourceUserId appletResourceUserId, uint arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(123)]
        public Result SetNpadJoyAssignmentModeSingle(AppletResourceUserId appletResourceUserId, uint arg1, long arg2, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(124)]
        public Result SetNpadJoyAssignmentModeDual(AppletResourceUserId appletResourceUserId, NpadIdType npadIdType, [ClientProcessId] ulong pid)
        {
            if (IsValidNpadIdType(npadIdType))
            {
                // context.Device.Hid.SharedMemory.Npads[(int)HidUtils.GetIndexFromNpadIdType(npadIdType)].InternalState.JoyAssignmentMode = NpadJoyAssignmentMode.Dual;
            }

            return Result.Success;
        }

        [CmifCommand(125)]
        public Result MergeSingleJoyAsDualJoy(AppletResourceUserId appletResourceUserId, NpadIdType npadIdType0, NpadIdType npadIdType1, [ClientProcessId] ulong pid)
        {
            if (IsValidNpadIdType(npadIdType0) && IsValidNpadIdType(npadIdType1))
            {
                Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, npadIdType0, npadIdType1 });
            }

            return Result.Success;
        }

        [CmifCommand(126)]
        public Result StartLrAssignmentMode(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(127)]
        public Result StopLrAssignmentMode(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(128)]
        public Result SetNpadHandheldActivationMode(AppletResourceUserId appletResourceUserId, NpadHandheldActivationMode npadHandheldActivationMode, [ClientProcessId] ulong pid)
        {
            _npadHandheldActivationMode = npadHandheldActivationMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, _npadHandheldActivationMode });

            return Result.Success;
        }

        [CmifCommand(129)]
        public Result GetNpadHandheldActivationMode(AppletResourceUserId appletResourceUserId, out NpadHandheldActivationMode npadHandheldActivationMode, [ClientProcessId] ulong pid)
        {
            npadHandheldActivationMode = _npadHandheldActivationMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, _npadHandheldActivationMode });

            return Result.Success;
        }

        [CmifCommand(130)]
        public Result SwapNpadAssignment(AppletResourceUserId appletResourceUserId, uint oldNpadAssignment, uint newNpadAssignment, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, oldNpadAssignment, newNpadAssignment });

            return Result.Success;
        }

        [CmifCommand(131)]
        public Result IsUnintendedHomeButtonInputProtectionEnabled(out bool arg0, AppletResourceUserId appletResourceUserId, uint arg2, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(132)]
        public Result EnableUnintendedHomeButtonInputProtection(AppletResourceUserId appletResourceUserId, uint arg1, bool arg2, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(133)]
        public Result SetNpadJoyAssignmentModeSingleWithDestination(out bool arg0, out uint arg1, AppletResourceUserId appletResourceUserId, uint arg3, long arg4, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(134)]
        public Result SetNpadAnalogStickUseCenterClamp(AppletResourceUserId appletResourceUserId, bool arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(135)]
        public Result SetNpadCaptureButtonAssignment(AppletResourceUserId appletResourceUserId, NpadStyleTag arg1, NpadButton arg2, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(136)]
        public Result ClearNpadCaptureButtonAssignment(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(200)]
        public Result GetVibrationDeviceInfo(out VibrationDeviceInfoForIpc vibrationDeviceInfoForIpc, VibrationDeviceHandle vibrationDeviceHandle)
        {
            NpadStyleIndex deviceType = vibrationDeviceHandle.DeviceType;
            NpadIdType npadIdType = vibrationDeviceHandle.PlayerId;
            vibrationDeviceInfoForIpc = new();

            if (deviceType < NpadStyleIndex.System || deviceType >= NpadStyleIndex.FullKey)
            {
                if (!IsValidNpadIdType(npadIdType))
                {
                    return HidResult.InvalidNpadIdType;
                }

                if (vibrationDeviceHandle.Position > 1)
                {
                    return HidResult.InvalidDeviceIndex;
                }

                VibrationDeviceType vibrationDeviceType = VibrationDeviceType.None;

                if (Enum.IsDefined(deviceType))
                {
                    vibrationDeviceType = VibrationDeviceType.LinearResonantActuator;
                }
                else if ((uint)deviceType == 8)
                {
                    vibrationDeviceType = VibrationDeviceType.GcErm;
                }

                VibrationDevicePosition vibrationDevicePosition = VibrationDevicePosition.None;

                if (vibrationDeviceType == VibrationDeviceType.LinearResonantActuator)
                {
                    if (vibrationDeviceHandle.Position == 0)
                    {
                        vibrationDevicePosition = VibrationDevicePosition.Left;
                    }
                    else if (vibrationDeviceHandle.Position == 1)
                    {
                        vibrationDevicePosition = VibrationDevicePosition.Right;
                    }
                    else
                    {
                        throw new InvalidOperationException($"{nameof(vibrationDeviceHandle.Position)} contains an invalid value: {vibrationDeviceHandle.Position}");
                    }
                }

                vibrationDeviceInfoForIpc = new()
                {
                    DeviceType = vibrationDeviceType,
                    Position = vibrationDevicePosition,
                };

                return Result.Success;
            }

            return HidResult.InvalidNpadDeviceType;
        }

        [CmifCommand(201)]
        public Result SendVibrationValue(AppletResourceUserId appletResourceUserId, VibrationDeviceHandle vibrationDeviceHandle, VibrationValue vibrationValue, [ClientProcessId] ulong pid)
        {
            Dictionary<byte, VibrationValue> dualVibrationValues = new()
            {
                [vibrationDeviceHandle.Position] = vibrationValue,
            };

            HorizonStatic.Hid.Npads.UpdateRumbleQueue(vibrationDeviceHandle.PlayerId, dualVibrationValues);

            return Result.Success;
        }

        [CmifCommand(202)]
        public Result GetActualVibrationValue(out VibrationValue vibrationValue, AppletResourceUserId appletResourceUserId, VibrationDeviceHandle vibrationDeviceHandle, [ClientProcessId] ulong pid)
        {
            vibrationValue = HorizonStatic.Hid.Npads.GetLastVibrationValue(vibrationDeviceHandle.PlayerId, vibrationDeviceHandle.Position);

            return Result.Success;
        }

        [CmifCommand(203)]
        public Result CreateActiveVibrationDeviceList(out IActiveVibrationDeviceList arg0)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(204)]
        public Result PermitVibration(bool vibrationPermitted)
        {
            _vibrationPermitted = vibrationPermitted;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { _vibrationPermitted });

            return Result.Success;
        }

        [CmifCommand(205)]
        public Result IsVibrationPermitted(out bool vibrationPermitted)
        {
            vibrationPermitted = _vibrationPermitted;

            return Result.Success;
        }

        [CmifCommand(206)]
        public Result SendVibrationValues(AppletResourceUserId appletResourceUserId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<VibrationDeviceHandle> vibrationDeviceHandles, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<VibrationValue> vibrationValues)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(207)]
        public Result SendVibrationGcErmCommand(AppletResourceUserId appletResourceUserId, VibrationDeviceHandle vibrationDeviceHandle, VibrationGcErmCommand vibrationGcErmCommand, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, vibrationDeviceHandle, vibrationGcErmCommand });

            return Result.Success;
        }

        [CmifCommand(208)]
        public Result GetActualVibrationGcErmCommand(out VibrationGcErmCommand vibrationGcErmCommand, AppletResourceUserId appletResourceUserId, VibrationDeviceHandle vibrationDeviceHandle, [ClientProcessId] ulong pid)
        {
            vibrationGcErmCommand = _vibrationGcErmCommand;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, vibrationDeviceHandle, _vibrationGcErmCommand });

            return Result.Success;
        }

        [CmifCommand(209)]
        public Result BeginPermitVibrationSession(AppletResourceUserId appletResourceUserId)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(210)]
        public Result EndPermitVibrationSession()
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(211)]
        public Result IsVibrationDeviceMounted(out bool isVibrationDeviceMounted, AppletResourceUserId appletResourceUserId, VibrationDeviceHandle vibrationDeviceHandle, [ClientProcessId] ulong pid)
        {
            // NOTE: Service use vibrationDeviceHandle to get the PlayerIndex.
            //       And return false if (npadIdType >= (NpadIdType)8 && npadIdType != NpadIdType.Handheld && npadIdType != NpadIdType.Unknown)

            isVibrationDeviceMounted = true;

            return Result.Success;
        }

        [CmifCommand(212)]
        public Result SendVibrationValueInBool(AppletResourceUserId appletResourceUserId, VibrationDeviceHandle vibrationDeviceHandle, bool arg2, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(300)]
        public Result ActivateConsoleSixAxisSensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(301)]
        public Result StartConsoleSixAxisSensor(AppletResourceUserId appletResourceUserId, ConsoleSixAxisSensorHandle consoleSixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, consoleSixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(302)]
        public Result StopConsoleSixAxisSensor(AppletResourceUserId appletResourceUserId, ConsoleSixAxisSensorHandle consoleSixAxisSensorHandle, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, consoleSixAxisSensorHandle });

            return Result.Success;
        }

        [CmifCommand(303)]
        public Result ActivateSevenSixAxisSensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(304)]
        public Result StartSevenSixAxisSensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(305)]
        public Result StopSevenSixAxisSensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(306)]
        public Result InitializeSevenSixAxisSensor(AppletResourceUserId appletResourceUserId, [CopyHandle] int nativeHandle0, ulong counter0, [CopyHandle] int nativeHandle1, ulong counter1, [ClientProcessId] ulong pid)
        {
            // TODO: Determine if array<nn::sf::NativeHandle> is a buffer or not...

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, counter0, counter1 });

            return Result.Success;
        }

        [CmifCommand(307)]
        public Result FinalizeSevenSixAxisSensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(308)]
        public Result SetSevenSixAxisSensorFusionStrength(AppletResourceUserId appletResourceUserId, float sevenSixAxisSensorFusionStrength, [ClientProcessId] ulong pid)
        {
            _sevenSixAxisSensorFusionStrength = sevenSixAxisSensorFusionStrength;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, _sevenSixAxisSensorFusionStrength });

            return Result.Success;
        }

        [CmifCommand(309)]
        public Result GetSevenSixAxisSensorFusionStrength(out float sevenSixSensorFusionStrength, AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            sevenSixSensorFusionStrength = _sevenSixAxisSensorFusionStrength;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, _sevenSixAxisSensorFusionStrength });

            return Result.Success;
        }

        [CmifCommand(310)]
        public Result ResetSevenSixAxisSensorTimestamp(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(400)]
        public Result IsUsbFullKeyControllerEnabled(out bool isUsbFullKeyControllerEnabled)
        {
            isUsbFullKeyControllerEnabled = _usbFullKeyControllerEnabled;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { _usbFullKeyControllerEnabled });

            return Result.Success;
        }

        [CmifCommand(401)]
        public Result EnableUsbFullKeyController(bool usbFullKeyControllerEnabled)
        {
            _usbFullKeyControllerEnabled = usbFullKeyControllerEnabled;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { _usbFullKeyControllerEnabled });

            return Result.Success;
        }

        [CmifCommand(402)]
        public Result IsUsbFullKeyControllerConnected(out bool isConnected, uint unknown)
        {
            isConnected = true; // FullKeyController is always connected?

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { unknown, Connected = true });

            return Result.Success;
        }

        [CmifCommand(403)]
        public Result HasBattery(out bool hasBattery, uint npadId)
        {
            hasBattery = true; // Npad always has a battery?

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { npadId, HasBattery = true });

            return Result.Success;
        }

        [CmifCommand(404)]
        public Result HasLeftRightBattery(out bool hasLeftBattery, out bool hasRightBattery, uint npadId)
        {
            hasLeftBattery = true; // Npad always has a left battery?
            hasRightBattery = true; // Npad always has a right battery?

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { npadId, HasLeftBattery = true, HasRightBattery = true });

            return Result.Success;
        }

        [CmifCommand(405)]
        public Result GetNpadInterfaceType(out byte npadInterfaceType, uint npadId)
        {
            npadInterfaceType = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { npadId, NpadInterfaceType = 0 });

            return Result.Success;
        }

        [CmifCommand(406)]
        public Result GetNpadLeftRightInterfaceType(out byte leftInterfaceType, out byte rightInterfaceType, uint npadId)
        {
            leftInterfaceType = 0;
            rightInterfaceType = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { npadId, LeftInterfaceType = 0, RightInterfaceType = 0 });

            return Result.Success;
        }

        [CmifCommand(500)]
        public Result GetPalmaConnectionHandle(out PalmaConnectionHandle palmaConnectionHandle, uint unknown, AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, unknown, palmaConnectionHandle });

            return Result.Success;
        }

        [CmifCommand(501)]
        public Result InitializePalma(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle });

            Os.SignalSystemEvent(ref _palmaOperationCompleteEvent);

            return Result.Success;
        }

        [CmifCommand(502)]
        public Result AcquirePalmaOperationCompleteEvent([CopyHandle] out int handle, PalmaConnectionHandle palmaConnectionHandle)
        {
            handle = Os.GetReadableHandleOfSystemEvent(ref _palmaOperationCompleteEvent);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle });

            return Result.Success;
        }

        [CmifCommand(503)]
        public Result GetPalmaOperationInfo(out ulong unknown, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> arg1, PalmaConnectionHandle palmaConnectionHandle)
        {
            unknown = 0; // Counter?

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle, unknown });

            return Result.Success;
        }

        [CmifCommand(504)]
        public Result PlayPalmaActivity(PalmaConnectionHandle palmaConnectionHandle, ulong unknown)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle, unknown });

            Os.SignalSystemEvent(ref _palmaOperationCompleteEvent);

            return Result.Success;
        }

        [CmifCommand(505)]
        public Result SetPalmaFrModeType(PalmaConnectionHandle palmaConnectionHandle, ulong frModeType)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle, frModeType });

            Os.SignalSystemEvent(ref _palmaOperationCompleteEvent);

            return Result.Success;
        }

        [CmifCommand(506)]
        public Result ReadPalmaStep(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle });

            return Result.Success;
        }

        [CmifCommand(507)]
        public Result EnablePalmaStep(PalmaConnectionHandle palmaConnectionHandle, bool enabledPalmaStep)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle, enabledPalmaStep });

            Os.SignalSystemEvent(ref _palmaOperationCompleteEvent);

            return Result.Success;
        }

        [CmifCommand(508)]
        public Result ResetPalmaStep(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle });

            Os.SignalSystemEvent(ref _palmaOperationCompleteEvent);

            return Result.Success;
        }

        [CmifCommand(509)]
        public Result ReadPalmaApplicationSection(PalmaConnectionHandle palmaConnectionHandle, ulong unknown0, ulong unknown1)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle, unknown0, unknown1 });

            return Result.Success;
        }

        [CmifCommand(510)]
        public Result WritePalmaApplicationSection(PalmaConnectionHandle palmaConnectionHandle, ulong unknown0, ulong unknown1, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x100)] in PalmaApplicationSectionAccessBuffer palmaApplicationSectionAccessBuffer)
        {
            // nn::hid::PalmaApplicationSectionAccessBuffer cast is unknown
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle, unknown0, unknown1 });

            Os.SignalSystemEvent(ref _palmaOperationCompleteEvent);

            return Result.Success;
        }

        [CmifCommand(511)]
        public Result ReadPalmaUniqueCode(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle });

            return Result.Success;
        }

        [CmifCommand(512)]
        public Result SetPalmaUniqueCodeInvalid(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { palmaConnectionHandle });

            return Result.Success;
        }

        [CmifCommand(513)]
        public Result WritePalmaActivityEntry(PalmaConnectionHandle palmaConnectionHandle, ulong arg1, ulong arg2, ulong arg3, ulong arg4)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(514)]
        public Result WritePalmaRgbLedPatternEntry(PalmaConnectionHandle palmaConnectionHandle, ulong arg1, [Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<byte> arg2)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(515)]
        public Result WritePalmaWaveEntry(PalmaConnectionHandle palmaConnectionHandle, PalmaWaveSet palmaWaveSet, ulong arg2, [CopyHandle] int arg3, ulong arg4, ulong arg5)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(516)]
        public Result SetPalmaDataBaseIdentificationVersion(PalmaConnectionHandle palmaConnectionHandle, int arg1)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(517)]
        public Result GetPalmaDataBaseIdentificationVersion(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(518)]
        public Result SuspendPalmaFeature(PalmaConnectionHandle palmaConnectionHandle, PalmaFeature palmaFeature)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(519)]
        public Result GetPalmaOperationResult(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(520)]
        public Result ReadPalmaPlayLog(PalmaConnectionHandle palmaConnectionHandle, ushort arg1)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(521)]
        public Result ResetPalmaPlayLog(PalmaConnectionHandle palmaConnectionHandle, ushort arg1)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(522)]
        public Result SetIsPalmaAllConnectable(AppletResourceUserId appletResourceUserId, bool unknownBool, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, unknownBool });

            return Result.Success;
        }

        [CmifCommand(523)]
        public Result SetIsPalmaPairedConnectable(AppletResourceUserId appletResourceUserId, bool arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(524)]
        public Result PairPalma(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(525)]
        public Result SetPalmaBoostMode(bool arg0)
        {
            // NOTE: Stubbed in system module.

            return Result.Success;
        }

        [CmifCommand(526)]
        public Result CancelWritePalmaWaveEntry(PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(527)]
        public Result EnablePalmaBoostMode(AppletResourceUserId appletResourceUserId, bool arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(528)]
        public Result GetPalmaBluetoothAddress(out Address arg0, PalmaConnectionHandle palmaConnectionHandle)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(529)]
        public Result SetDisallowedPalmaConnection(AppletResourceUserId appletResourceUserId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer)] ReadOnlySpan<Address> arg1, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        [CmifCommand(1000)]
        public Result SetNpadCommunicationMode(AppletResourceUserId appletResourceUserId, long npadCommunicationMode, [ClientProcessId] ulong pid)
        {
            _npadCommunicationMode = npadCommunicationMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, _npadCommunicationMode });

            return Result.Success;
        }

        [CmifCommand(1001)]
        public Result GetNpadCommunicationMode(out long npadCommunicationMode)
        {
            npadCommunicationMode = _npadCommunicationMode;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { _npadCommunicationMode });

            return Result.Success;
        }

        [CmifCommand(1002)]
        public Result SetTouchScreenConfiguration(AppletResourceUserId appletResourceUserId, TouchScreenConfigurationForNx touchScreenConfigurationForNx, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, touchScreenConfigurationForNx });

            return Result.Success;
        }

        [CmifCommand(1003)]
        public Result IsFirmwareUpdateNeededForNotification(out bool isFirmwareUpdateNeededForNotification, int unknown, AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            isFirmwareUpdateNeededForNotification = false;

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { IsFirmwareUpdateNeededForNotification = false, unknown, appletResourceUserId });

            return Result.Success;
        }

        [CmifCommand(2000)]
        public Result ActivateDigitizer(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceHid);

            return Result.Success;
        }

        public static PlayerIndex GetIndexFromNpadIdType(NpadIdType npadIdType)
            => npadIdType switch
            {
#pragma warning disable IDE0055 // Disable formatting
                NpadIdType.Player1  => PlayerIndex.Player1,
                NpadIdType.Player2  => PlayerIndex.Player2,
                NpadIdType.Player3  => PlayerIndex.Player3,
                NpadIdType.Player4  => PlayerIndex.Player4,
                NpadIdType.Player5  => PlayerIndex.Player5,
                NpadIdType.Player6  => PlayerIndex.Player6,
                NpadIdType.Player7  => PlayerIndex.Player7,
                NpadIdType.Player8  => PlayerIndex.Player8,
                NpadIdType.Handheld => PlayerIndex.Handheld,
                NpadIdType.Unknown  => PlayerIndex.Unknown,
                _                   => throw new ArgumentOutOfRangeException(nameof(npadIdType)),
#pragma warning restore IDE0055
            };

        public static NpadIdType GetNpadIdTypeFromIndex(PlayerIndex index)
            => index switch
            {
#pragma warning disable IDE0055 // Disable formatting
                PlayerIndex.Player1  => NpadIdType.Player1,
                PlayerIndex.Player2  => NpadIdType.Player2,
                PlayerIndex.Player3  => NpadIdType.Player3,
                PlayerIndex.Player4  => NpadIdType.Player4,
                PlayerIndex.Player5  => NpadIdType.Player5,
                PlayerIndex.Player6  => NpadIdType.Player6,
                PlayerIndex.Player7  => NpadIdType.Player7,
                PlayerIndex.Player8  => NpadIdType.Player8,
                PlayerIndex.Handheld => NpadIdType.Handheld,
                PlayerIndex.Unknown  => NpadIdType.Unknown,
                _                    => throw new ArgumentOutOfRangeException(nameof(index)),
#pragma warning restore IDE0055
            };

        private static bool IsValidNpadIdType(NpadIdType npadIdType)
        {
            return (npadIdType >= NpadIdType.Player1 && npadIdType <= NpadIdType.Player8) ||
                   npadIdType == NpadIdType.Handheld ||
                   npadIdType == NpadIdType.Unknown;
        }
    }
}
