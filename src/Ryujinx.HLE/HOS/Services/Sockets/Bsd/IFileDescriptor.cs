using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd
{
    interface IFileDescriptor : IDisposable
    {
        bool Blocking { get; set; }
        int RefCount { get; set; }

        LinuxError Read(out int readSize, Span<byte> buffer);

        LinuxError Write(out int writeSize, ReadOnlySpan<byte> buffer);
    }
}
