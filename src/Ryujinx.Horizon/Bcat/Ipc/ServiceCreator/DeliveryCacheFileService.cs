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
    partial class DeliveryCacheFileService : IDeliveryCacheFileService, IDisposable
    {
        private SharedRef<LibHac.Bcat.Impl.Ipc.IDeliveryCacheFileService> _libHacService;
        private int _disposalState;

        public DeliveryCacheFileService(ref SharedRef<LibHac.Bcat.Impl.Ipc.IDeliveryCacheFileService> libHacService)
        {
            _libHacService = SharedRef<LibHac.Bcat.Impl.Ipc.IDeliveryCacheFileService>.CreateMove(ref libHacService);
        }

        [CmifCommand(0)]
        public Result Open(LibHac.Bcat.DirectoryName directoryName, FileName fileName)
        {
            return _libHacService.Get.Open(ref directoryName, ref fileName).ToHorizonResult();
        }

        [CmifCommand(1)]
        public Result Read(long offset, out long bytesRead, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.MapAlias)] Span<byte> data)
        {
            return _libHacService.Get.Read(out bytesRead, offset, data).ToHorizonResult();
        }

        [CmifCommand(2)]
        public Result GetSize(out long size)
        {
            return _libHacService.Get.GetSize(out size).ToHorizonResult();
        }

        [CmifCommand(3)]
        public Result GetDigest(out Digest digest)
        {
            return _libHacService.Get.GetDigest(out digest).ToHorizonResult();
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
