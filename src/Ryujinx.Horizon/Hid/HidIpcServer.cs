using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Sm;

namespace Ryujinx.Horizon.Hid
{
    class HidIpcServer
    {
        // TODO: RE These values
        private const int MaxSessionsCount = 30;

        private const int PointerBufferSize = 0xB40;
        private const int MaxDomains = 0;
        private const int MaxDomainObjects = 0;
        private const int MaxPortsCount = 1;

        private static readonly ManagerOptions _options = new(PointerBufferSize, MaxDomains, MaxDomainObjects, false);

        private SmApi _sm;
        private ServerManager _serverManager;

        public void Initialize()
        {
            HeapAllocator allocator = new();

            _sm = new SmApi();
            _sm.Initialize().AbortOnFailure();

            _serverManager = new ServerManager(allocator, _sm, MaxPortsCount, _options, MaxSessionsCount);

            _serverManager.RegisterObjectForServer(new HidServer(), ServiceName.Encode("hid"), MaxSessionsCount);
            // _serverManager.RegisterObjectForServer(, ServiceName.Encode("hidbus"), MaxSessionsCount);
            // _serverManager.RegisterObjectForServer(, ServiceName.Encode("hid:sys"), MaxSessionsCount);
            // _serverManager.RegisterObjectForServer(, ServiceName.Encode("hid:dbg"), MaxSessionsCount);
        }

        public void ServiceRequests()
        {
            _serverManager.ServiceRequests();
        }

        public void Shutdown()
        {
            _serverManager.Dispose();
            _sm.Dispose();
        }
    }
}
