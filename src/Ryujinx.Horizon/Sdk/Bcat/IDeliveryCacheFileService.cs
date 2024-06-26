using LibHac.Bcat;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Bcat
{
    internal interface IDeliveryCacheFileService : IServiceObject
    {
        Result GetDigest(out Digest digest);
        Result GetSize(out long size);
        Result Open(LibHac.Bcat.DirectoryName directoryName, FileName fileName);
        Result Read(long offset, out long bytesRead, Span<byte> data);
    }
}
