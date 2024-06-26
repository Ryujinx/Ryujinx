using LibHac.Bcat;
using LibHac.Common;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Bcat;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Threading;

namespace Ryujinx.Horizon.Bcat.Ipc
{
    partial class DeliveryCacheDirectoryService : IDeliveryCacheDirectoryService, IDisposable
    {
        private SharedRef<LibHac.Bcat.Impl.Ipc.IDeliveryCacheDirectoryService> _libHacService;
        private int _disposalState;

        public DeliveryCacheDirectoryService(ref SharedRef<LibHac.Bcat.Impl.Ipc.IDeliveryCacheDirectoryService> libHacService)
        {
            _libHacService = SharedRef<LibHac.Bcat.Impl.Ipc.IDeliveryCacheDirectoryService>.CreateMove(ref libHacService);
        }

        [CmifCommand(0)]
        public Result Open(LibHac.Bcat.DirectoryName directoryName)
        {
            return _libHacService.Get.Open(ref directoryName).ToHorizonResult();
        }

        [CmifCommand(1)]
        public Result Read(out int entriesRead, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<DeliveryCacheDirectoryEntry> entriesBuffer)
        {
            return _libHacService.Get.Read(out entriesRead, entriesBuffer).ToHorizonResult();
        }

        [CmifCommand(2)]
        public Result GetCount(out int count)
        {
            return _libHacService.Get.GetCount(out count).ToHorizonResult();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposalState, 1) == 0)
            {
                _libHacService.Destroy();
            }
        }
    }
}
