using Ryujinx.Horizon.Sdk.Sf.Hipc;
using Ryujinx.Horizon.Sdk.Sm;

namespace Ryujinx.Horizon.Irs
{
    class IrsIpcServer
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

            _serverManager.RegisterObjectForServer(new IrSensorServer(), ServiceName.Encode("irs"), MaxSessionsCount);
            _serverManager.RegisterObjectForServer(new IrSensorSystemServer(), ServiceName.Encode("irs:sys"), MaxSessionsCount);
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
