using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Applet;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    interface IHidbusServer : IServiceObject
    {
        Result GetBusHandle(out BusHandle handle, out bool hasHandle, uint npadIdType, ulong busType, AppletResourceUserId resourceUserId);
        Result IsExternalDeviceConnected(out bool isAttached, BusHandle handle);
        Result Initialize(BusHandle handle, AppletResourceUserId resourceUserId);
        Result Finalize(BusHandle handle, AppletResourceUserId resourceUserId);
        Result EnableExternalDevice(BusHandle handle, bool isEnabled, ulong version, AppletResourceUserId resourceUserId);
        Result GetExternalDeviceId(out uint deviceId, BusHandle handle);
        Result SendCommandAsync(ReadOnlySpan<byte> buffer, BusHandle handle);
        Result GetSendCommandAsyncResult(out uint outSize, Span<byte> buffer, BusHandle handle);
        Result SetEventForSendCommandAsyncResult(out int eventHandle, BusHandle handle);
        Result GetSharedMemoryHandle(out int sharedMemoryHandle);
        Result EnableJoyPollingReceiveMode(ReadOnlySpan<byte> buffer, int transferMemoryHandle, uint size, uint joyPollingMode, BusHandle handle);
        Result DisableJoyPollingReceiveMode(BusHandle handle);
        Result SetStatusManagerType(uint statusManagerType);
    }
}
