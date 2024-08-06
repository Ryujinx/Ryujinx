using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Hid.HidServer;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Hid;
using Ryujinx.Horizon.Sdk.Hid.HidDevices;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using Ryujinx.Horizon.Sdk.Hid.SixAxis;
using Ryujinx.Horizon.Sdk.Hid.Vibration;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Hid
{
    [Service("hid")]
    class IHidServer : IpcService
    {
        private readonly KEvent _xpadIdEvent;
        private readonly KEvent _palmaOperationCompleteEvent;

        private int _xpadIdEventHandle;

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
#pragma warning disable CS0649 // Field is never assigned to
        private readonly long _vibrationGcErmCommand;
#pragma warning restore CS0649
        private float _sevenSixAxisSensorFusionStrength;

        private SensorFusionParameters _sensorFusionParams;
        private AccelerometerParameters _accelerometerParams;

        public IHidServer(ServiceCtx context) : base(context.Device.System.HidServer)
        {
            _xpadIdEvent = new KEvent(context.Device.System.KernelContext);
            _palmaOperationCompleteEvent = new KEvent(context.Device.System.KernelContext);

            _npadHandheldActivationMode = NpadHandheldActivationMode.Dual;
            _gyroscopeZeroDriftMode = GyroscopeZeroDriftMode.Standard;

            _isFirmwareUpdateAvailableForSixAxisSensor = false;

            _sensorFusionParams = new SensorFusionParameters();
            _accelerometerParams = new AccelerometerParameters();

            // TODO: signal event at right place
            _xpadIdEvent.ReadableEvent.Signal();

            _vibrationPermitted = true;
        }

        [CommandCmif(0)]
        // CreateAppletResource(nn::applet::AppletResourceUserId) -> object<nn::hid::IAppletResource>
        public ResultCode CreateAppletResource(ServiceCtx context)
        {
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            long appletResourceUserId = context.RequestData.ReadInt64();
#pragma warning restore IDE0059

            MakeObject(context, new IAppletResource(context.Device.System.HidSharedMem));

            return ResultCode.Success;
        }

        [CommandCmif(58)]
        // GetJoyXpadLifoHandle(nn::hid::JoyXpadId) -> nn::sf::NativeHandle
        public ResultCode GetJoyXpadLifoHandle(ServiceCtx context)
        {
            int joyXpadId = context.RequestData.ReadInt32();

            int handle = 0;

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return ResultCode.Success;
        }

        [CommandCmif(62)]
        // GetSixAxisSensorLifoHandle(nn::hid::BasicXpadId) -> nn::sf::NativeHandle
        public ResultCode GetSixAxisSensorLifoHandle(ServiceCtx context)
        {
            int basicXpadId = context.RequestData.ReadInt32();

            int handle = 0;

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { basicXpadId });

            return ResultCode.Success;
        }

        [CommandCmif(65)]
        // GetJoySixAxisSensorLifoHandle(nn::hid::JoyXpadId) -> nn::sf::NativeHandle
        public ResultCode GetJoySixAxisSensorLifoHandle(ServiceCtx context)
        {
            int joyXpadId = context.RequestData.ReadInt32();

            int handle = 0;

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { joyXpadId });

            return ResultCode.Success;
        }

        [CommandCmif(103)]
        // ActivateNpad(nn::applet::AppletResourceUserId)
        public ResultCode ActivateNpad(ServiceCtx context)
        {
            return ActiveNpadImpl(context);
        }

        [CommandCmif(106)]
        // AcquireNpadStyleSetUpdateEventHandle(nn::applet::AppletResourceUserId, uint, ulong) -> nn::sf::NativeHandle
        public ResultCode AcquireNpadStyleSetUpdateEventHandle(ServiceCtx context)
        {
            PlayerIndex npadId = HidUtils.GetIndexFromNpadIdType((NpadIdType)context.RequestData.ReadInt32());
            long appletResourceUserId = context.RequestData.ReadInt64();
            long npadStyleSet = context.RequestData.ReadInt64();

            KEvent evnt = context.Device.Hid.Npads.GetStyleSetUpdateEvent(npadId);
            if (context.Process.HandleTable.GenerateHandle(evnt.ReadableEvent, out int handle) != Result.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            // Games expect this event to be signaled after calling this function
            evnt.ReadableEvent.Signal();

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, npadId, npadStyleSet });

            return ResultCode.Success;
        }

        [CommandCmif(109)] // 5.0.0+
        // ActivateNpadWithRevision(nn::applet::AppletResourceUserId, ulong revision)
        public ResultCode ActivateNpadWithRevision(ServiceCtx context)
        {
            ulong revision = context.RequestData.ReadUInt64();

            return ActiveNpadImpl(context, revision);
        }

        private ResultCode ActiveNpadImpl(ServiceCtx context, ulong revision = 0)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();

            context.Device.Hid.Npads.Active = true;

            // Initialize entries to avoid issues with some games.

            List<GamepadInput> emptyGamepadInputs = new();
            List<SixAxisInput> emptySixAxisInputs = new();

            for (int player = 0; player < NpadDevices.MaxControllers; player++)
            {
                GamepadInput gamepadInput = new();
                SixAxisInput sixaxisInput = new();

                gamepadInput.PlayerId = (PlayerIndex)player;
                sixaxisInput.PlayerId = (PlayerIndex)player;

                sixaxisInput.Orientation = new float[9];

                emptyGamepadInputs.Add(gamepadInput);
                emptySixAxisInputs.Add(sixaxisInput);
            }

            for (int entry = 0; entry < Ryujinx.Horizon.Sdk.Hid.Hid.SharedMemEntryCount; entry++)
            {
                context.Device.Hid.Npads.Update(emptyGamepadInputs);
                context.Device.Hid.Npads.UpdateSixAxis(emptySixAxisInputs);
            }

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, revision });

            return ResultCode.Success;
        }

        [CommandCmif(120)]
        // SetNpadJoyHoldType(nn::applet::AppletResourceUserId, ulong NpadJoyHoldType)
        public ResultCode SetNpadJoyHoldType(ServiceCtx context)
        {
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            long appletResourceUserId = context.RequestData.ReadInt64();
#pragma warning restore IDE0059

            NpadJoyHoldType npadJoyHoldType = (NpadJoyHoldType)context.RequestData.ReadUInt64();

            if (npadJoyHoldType > NpadJoyHoldType.Horizontal)
            {
                throw new InvalidOperationException($"{nameof(npadJoyHoldType)} contains an invalid value: {npadJoyHoldType}");
            }

            foreach (PlayerIndex playerIndex in context.Device.Hid.Npads.GetSupportedPlayers())
            {
                if (HidUtils.GetNpadIdTypeFromIndex(playerIndex) > NpadIdType.Handheld)
                {
                    return ResultCode.InvalidNpadIdType;
                }
            }

            context.Device.Hid.Npads.JoyHold = npadJoyHoldType;

            return ResultCode.Success;
        }

        [CommandCmif(121)]
        // GetNpadJoyHoldType(nn::applet::AppletResourceUserId) -> ulong NpadJoyHoldType
        public ResultCode GetNpadJoyHoldType(ServiceCtx context)
        {
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            long appletResourceUserId = context.RequestData.ReadInt64();
#pragma warning restore IDE0059

            foreach (PlayerIndex playerIndex in context.Device.Hid.Npads.GetSupportedPlayers())
            {
                if (HidUtils.GetNpadIdTypeFromIndex(playerIndex) > NpadIdType.Handheld)
                {
                    return ResultCode.InvalidNpadIdType;
                }
            }

            context.ResponseData.Write((ulong)context.Device.Hid.Npads.JoyHold);

            return ResultCode.Success;
        }

        [CommandCmif(122)]
        // SetNpadJoyAssignmentModeSingleByDefault(uint HidControllerId, nn::applet::AppletResourceUserId)
        public ResultCode SetNpadJoyAssignmentModeSingleByDefault(ServiceCtx context)
        {
            NpadIdType npadIdType = (NpadIdType)context.RequestData.ReadUInt32();
            context.RequestData.BaseStream.Position += 4; // Padding
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            long appletResourceUserId = context.RequestData.ReadInt64();
#pragma warning restore IDE0059

            if (HidUtils.IsValidNpadIdType(npadIdType))
            {
                context.Device.Hid.SharedMemory.Npads[(int)HidUtils.GetIndexFromNpadIdType(npadIdType)].InternalState.JoyAssignmentMode = NpadJoyAssignmentMode.Single;
            }

            return ResultCode.Success;
        }

        [CommandCmif(123)]
        // SetNpadJoyAssignmentModeSingle(uint npadIdType, nn::applet::AppletResourceUserId, uint npadJoyDeviceType)
        public ResultCode SetNpadJoyAssignmentModeSingle(ServiceCtx context)
        {
            NpadIdType npadIdType = (NpadIdType)context.RequestData.ReadUInt32();
            context.RequestData.BaseStream.Position += 4; // Padding
            long appletResourceUserId = context.RequestData.ReadInt64();
            NpadJoyDeviceType npadJoyDeviceType = (NpadJoyDeviceType)context.RequestData.ReadUInt32();

            if (HidUtils.IsValidNpadIdType(npadIdType))
            {
                SetNpadJoyAssignmentModeSingleWithDestinationImpl(context, npadIdType, appletResourceUserId, npadJoyDeviceType, out _, out _);
            }

            return ResultCode.Success;
        }

        [CommandCmif(124)]
        // SetNpadJoyAssignmentModeDual(uint npadIdType, nn::applet::AppletResourceUserId)
        public ResultCode SetNpadJoyAssignmentModeDual(ServiceCtx context)
        {
            NpadIdType npadIdType = (NpadIdType)context.RequestData.ReadUInt32();
            context.RequestData.BaseStream.Position += 4; // Padding
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            long appletResourceUserId = context.RequestData.ReadInt64();
#pragma warning restore IDE0059

            if (HidUtils.IsValidNpadIdType(npadIdType))
            {
                context.Device.Hid.SharedMemory.Npads[(int)HidUtils.GetIndexFromNpadIdType(npadIdType)].InternalState.JoyAssignmentMode = NpadJoyAssignmentMode.Dual;
            }

            return ResultCode.Success;
        }

        [CommandCmif(131)]
        // IsUnintendedHomeButtonInputProtectionEnabled(uint Unknown0, nn::applet::AppletResourceUserId) ->  bool IsEnabled
        public ResultCode IsUnintendedHomeButtonInputProtectionEnabled(ServiceCtx context)
        {
            uint unknown0 = context.RequestData.ReadUInt32();
            long appletResourceUserId = context.RequestData.ReadInt64();

            context.ResponseData.Write(_unintendedHomeButtonInputProtectionEnabled);

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, unknown0, _unintendedHomeButtonInputProtectionEnabled });

            return ResultCode.Success;
        }

        [CommandCmif(132)]
        // EnableUnintendedHomeButtonInputProtection(bool Enable, uint Unknown0, nn::applet::AppletResourceUserId)
        public ResultCode EnableUnintendedHomeButtonInputProtection(ServiceCtx context)
        {
            _unintendedHomeButtonInputProtectionEnabled = context.RequestData.ReadBoolean();
            uint unknown0 = context.RequestData.ReadUInt32();
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { appletResourceUserId, unknown0, _unintendedHomeButtonInputProtectionEnabled });

            return ResultCode.Success;
        }

        [CommandCmif(133)] // 5.0.0+
        // SetNpadJoyAssignmentModeSingleWithDestination(uint npadIdType, uint npadJoyDeviceType, nn::applet::AppletResourceUserId) -> bool npadIdTypeIsSet, uint npadIdTypeSet
        public ResultCode SetNpadJoyAssignmentModeSingleWithDestination(ServiceCtx context)
        {
            NpadIdType npadIdType = (NpadIdType)context.RequestData.ReadInt32();
            NpadJoyDeviceType npadJoyDeviceType = (NpadJoyDeviceType)context.RequestData.ReadInt32();
            context.RequestData.BaseStream.Position += 4; // Padding
            long appletResourceUserId = context.RequestData.ReadInt64();

            if (HidUtils.IsValidNpadIdType(npadIdType))
            {
                SetNpadJoyAssignmentModeSingleWithDestinationImpl(context, npadIdType, appletResourceUserId, npadJoyDeviceType, out NpadIdType npadIdTypeSet, out bool npadIdTypeIsSet);

                if (npadIdTypeIsSet)
                {
                    context.ResponseData.Write(npadIdTypeIsSet);
                    context.ResponseData.Write((uint)npadIdTypeSet);
                }
            }

            return ResultCode.Success;
        }

        private void SetNpadJoyAssignmentModeSingleWithDestinationImpl(ServiceCtx context, NpadIdType npadIdType, long appletResourceUserId, NpadJoyDeviceType npadJoyDeviceType, out NpadIdType npadIdTypeSet, out bool npadIdTypeIsSet)
        {
            npadIdTypeSet = default;
            npadIdTypeIsSet = false;

            context.Device.Hid.SharedMemory.Npads[(int)HidUtils.GetIndexFromNpadIdType(npadIdType)].InternalState.JoyAssignmentMode = NpadJoyAssignmentMode.Single;

            // TODO: Service seems to use the npadJoyDeviceType to find the nearest other Npad available and merge them to dual.
            //       If one is found, it returns the npadIdType of the other Npad and a bool.
            //       If not, it returns nothing.
        }

        [CommandCmif(134)] // 6.1.0+
        // SetNpadUseAnalogStickUseCenterClamp(bool Enable, nn::applet::AppletResourceUserId)
        public ResultCode SetNpadUseAnalogStickUseCenterClamp(ServiceCtx context)
        {
            ulong pid = context.RequestData.ReadUInt64();
            _npadAnalogStickCenterClampEnabled = context.RequestData.ReadUInt32() != 0;
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { pid, appletResourceUserId, _npadAnalogStickCenterClampEnabled });

            return ResultCode.Success;
        }

        [CommandCmif(203)]
        // CreateActiveVibrationDeviceList() -> object<nn::hid::IActiveVibrationDeviceList>
        public ResultCode CreateActiveVibrationDeviceList(ServiceCtx context)
        {
            MakeObject(context, new IActiveApplicationDeviceList());

            return ResultCode.Success;
        }

        [CommandCmif(206)]
        // SendVibrationValues(nn::applet::AppletResourceUserId, buffer<array<nn::hid::VibrationDeviceHandle>, type: 9>, buffer<array<nn::hid::VibrationValue>, type: 9>)
        public ResultCode SendVibrationValues(ServiceCtx context)
        {
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            long appletResourceUserId = context.RequestData.ReadInt64();
#pragma warning restore IDE0059

            byte[] vibrationDeviceHandleBuffer = new byte[context.Request.PtrBuff[0].Size];

            context.Memory.Read(context.Request.PtrBuff[0].Position, vibrationDeviceHandleBuffer);

            byte[] vibrationValueBuffer = new byte[context.Request.PtrBuff[1].Size];

            context.Memory.Read(context.Request.PtrBuff[1].Position, vibrationValueBuffer);

            Span<VibrationDeviceHandle> deviceHandles = MemoryMarshal.Cast<byte, VibrationDeviceHandle>(vibrationDeviceHandleBuffer);
            Span<VibrationValue> vibrationValues = MemoryMarshal.Cast<byte, VibrationValue>(vibrationValueBuffer);

            if (!deviceHandles.IsEmpty && vibrationValues.Length == deviceHandles.Length)
            {
                Dictionary<byte, VibrationValue> dualVibrationValues = new();
                PlayerIndex currentIndex = (PlayerIndex)deviceHandles[0].PlayerId;

                for (int deviceCounter = 0; deviceCounter < deviceHandles.Length; deviceCounter++)
                {
                    PlayerIndex index = (PlayerIndex)deviceHandles[deviceCounter].PlayerId;
                    byte position = deviceHandles[deviceCounter].Position;

                    if (index != currentIndex || dualVibrationValues.Count == 2)
                    {
                        context.Device.Hid.Npads.UpdateRumbleQueue(currentIndex, dualVibrationValues);
                        dualVibrationValues = new Dictionary<byte, VibrationValue>();
                    }

                    dualVibrationValues[position] = vibrationValues[deviceCounter];
                    currentIndex = index;
                }

                context.Device.Hid.Npads.UpdateRumbleQueue(currentIndex, dualVibrationValues);
            }

            return ResultCode.Success;
        }

        [CommandCmif(1004)] // 17.0.0+
        // SetTouchScreenResolution(int width, int height, nn::applet::AppletResourceUserId)
        public ResultCode SetTouchScreenResolution(ServiceCtx context)
        {
            int width = context.RequestData.ReadInt32();
            int height = context.RequestData.ReadInt32();
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceHid, new { width, height, appletResourceUserId });

            return ResultCode.Success;
        }
    }
}
