using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Horizon.Sdk.Hid.Npad;

namespace Ryujinx.HLE.HOS.Services.Nfc.Nfp.NfpManager
{
    class NfpDevice
    {
        public KEvent ActivateEvent;
        public KEvent DeactivateEvent;

        public void SignalActivate() => ActivateEvent.ReadableEvent.Signal();
        public void SignalDeactivate() => DeactivateEvent.ReadableEvent.Signal();

        public NfpDeviceState State = NfpDeviceState.Unavailable;

        public PlayerIndex Handle;
        public NpadIdType NpadIdType;

        public string AmiiboId;

        public bool UseRandomUuid;
    }
}
