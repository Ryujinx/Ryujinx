using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Sf;

namespace Ryujinx.Horizon.Sdk.Bcat
{
    internal interface IBcatService : IServiceObject
    {
        Result RequestSyncDeliveryCache(out IDeliveryCacheProgressService deliveryCacheProgressService);
        Result RequestSyncDeliveryCacheWithDirectoryName(out IDeliveryCacheProgressService deliveryCacheProgressService, DirectoryName directoryName);
    }
}
