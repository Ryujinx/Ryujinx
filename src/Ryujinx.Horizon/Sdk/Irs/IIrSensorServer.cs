using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Irs
{
    interface IIrSensorServer : IServiceObject
    {
        Result ActivateIrsensor(AppletResourceUserId appletResourceUserId, ulong pid);
        Result DeactivateIrsensor(AppletResourceUserId appletResourceUserId, ulong pid);
        Result GetIrsensorSharedMemoryHandle(out int arg0, AppletResourceUserId appletResourceUserId, ulong pid);
        Result StopImageProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, ulong pid);
        Result RunMomentProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedMomentProcessorConfig config, ulong pid);
        Result RunClusteringProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedClusteringProcessorConfig config, ulong pid);
        Result RunImageTransferProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedImageTransferProcessorConfig config, int arg3, ulong arg4, ulong pid);
        Result GetImageTransferProcessorState(AppletResourceUserId appletResourceUserId, out ImageTransferProcessorState imageTransferProcessorState, Span<byte> imageTransferBuffer, IrCameraHandle irCameraHandle, ulong pid);
        Result RunTeraPluginProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedTeraPluginProcessorConfig config, ulong pid);
        Result GetNpadIrCameraHandle(out IrCameraHandle irCameraHandle, NpadIdType npadIdType);
        Result RunPointingProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedPointingProcessorConfig config, ulong pid);
        Result SuspendImageProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, ulong pid);
        Result CheckFirmwareVersion(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedMcuVersion mcuVersion, ulong pid);
        Result SetFunctionLevel(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedFunctionLevel functionLevel, ulong pid);
        Result RunImageTransferExProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedImageTransferProcessorExConfig config, int arg3, ulong arg4, ulong pid);
        Result RunIrLedProcessor(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, PackedIrLedProcessorConfig config, ulong pid);
        Result StopImageProcessorAsync(AppletResourceUserId appletResourceUserId, IrCameraHandle irCameraHandle, ulong pid);
        Result ActivateIrsensorWithFunctionLevel(AppletResourceUserId appletResourceUserId, PackedFunctionLevel functionLevel, ulong pid);
    }
}
