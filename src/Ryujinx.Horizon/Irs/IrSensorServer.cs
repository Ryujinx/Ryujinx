using Ryujinx.Common.Logging;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using Ryujinx.Horizon.Sdk.Irs;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;

namespace Ryujinx.Horizon.Irs
{
    class IrSensorServer : IIrSensorServer
    {
        [CmifCommand(302)]
        public Result ActivateIrsensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            // NOTE: This seems to initialize the shared memory for irs service.

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id });

            return Result.Success;
        }

        [CmifCommand(303)]
        public Result DeactivateIrsensor(AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            // NOTE: This seems to deinitialize the shared memory for irs service.

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id });

            return Result.Success;
        }

        [CmifCommand(304)]
        public Result GetIrsensorSharedMemoryHandle([CopyHandle] out int arg0, AppletResourceUserId appletResourceUserId, [ClientProcessId] ulong pid)
        {
            // NOTE: Shared memory should use the appletResourceUserId.
            // TODO: Update when HorizonStatic has support for CreateSharedMemory

            arg0 = 0;

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id });

            return Result.Success;
        }

        [CmifCommand(305)]
        public Result StopImageProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType });

            return Result.Success;
        }

        [CmifCommand(306)]
        public Result RunMomentProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedMomentProcessorConfig config, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.ExposureTime });

            return Result.Success;
        }

        [CmifCommand(307)]
        public Result RunClusteringProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedClusteringProcessorConfig config, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.ExposureTime });

            return Result.Success;
        }

        [CmifCommand(308)]
        public Result RunImageTransferProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedImageTransferProcessorConfig config, [CopyHandle] int arg3, ulong arg4, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            // TODO: Handle the Transfer Memory.

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.ExposureTime });

            return Result.Success;
        }

        [CmifCommand(309)]
        public Result GetImageTransferProcessorState(AppletResourceUserId appletResourceUserId, out ImageTransferProcessorState imageTransferProcessorState, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> imageTransferBuffer, IrCameraHandle irCameraHandle, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            // TODO: Uses the buffer to copy the JoyCon IR data (by using a JoyCon driver) and update the following struct.
            imageTransferProcessorState = new ImageTransferProcessorState
            {
                SamplingNumber = 0,
                AmbientNoiseLevel = 0
            };

            if (imageTransferBuffer.Length == 0)
            {
                // InvalidBufferSize
                return new Result(207);
            }

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType });

            return Result.Success;
        }

        [CmifCommand(310)]
        public Result RunTeraPluginProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedTeraPluginProcessorConfig config, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.RequiredMcuVersion });

            return Result.Success;
        }

        [CmifCommand(311)]
        public Result GetNpadIrCameraHandle(out IrCameraHandle irCameraHandle, NpadIdType npadIdType)
        {
            if (npadIdType > NpadIdType.Player8 &&
                npadIdType != NpadIdType.Unknown &&
                npadIdType != NpadIdType.Handheld)
            {
                // NpadIdOutOfRange
                irCameraHandle = new IrCameraHandle();
                return new Result(709);
            }

            irCameraHandle = new IrCameraHandle
            {
                PlayerNumber = (byte)npadIdType
            };

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { npadIdType });

            return Result.Success;
        }

        [CmifCommand(312)]
        public Result RunPointingProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedPointingProcessorConfig config, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.RequiredMcuVersion });

            return Result.Success;
        }

        [CmifCommand(313)]
        public Result SuspendImageProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType });

            return Result.Success;
        }

        [CmifCommand(314)]
        public Result CheckFirmwareVersion(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedMcuVersion mcuVersion, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, mcuVersion });

            return Result.Success;
        }

        [CmifCommand(315)]
        public Result SetFunctionLevel(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedFunctionLevel functionLevel, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, functionLevel.IrSensorFunctionLevel });

            return Result.Success;
        }

        [CmifCommand(316)]
        public Result RunImageTransferExProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedImageTransferProcessorExConfig config, int arg3, ulong arg4, ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.ExposureTime, arg3, arg4 });

            return Result.Success;
        }

        [CmifCommand(317)]
        public Result RunIrLedProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedIrLedProcessorConfig config, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType, config.RequiredMcuVersion });

            return Result.Success;
        }

        [CmifCommand(318)]
        public Result StopImageProcessorAsync(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, [ClientProcessId] ulong pid)
        {
            CheckCameraHandle(irCameraHandle);

            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, irCameraHandle.PlayerNumber, irCameraHandle.DeviceType });

            return Result.Success;
        }

        [CmifCommand(319)]
        public Result ActivateIrsensorWithFunctionLevel(AppletResourceUserId appletResourceUserId, PackedFunctionLevel functionLevel, [ClientProcessId] ulong pid)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId.Id, functionLevel.IrSensorFunctionLevel });

            return Result.Success;
        }

        private Result CheckCameraHandle(IrCameraHandle irCameraHandle)
        {
            if (irCameraHandle.DeviceType == 1 || (PlayerIndex)irCameraHandle.PlayerNumber >= PlayerIndex.Unknown)
            {
                // InvalidCameraHandle
                return new Result(204);
            }

            return Result.Success;
        }
    }
}
