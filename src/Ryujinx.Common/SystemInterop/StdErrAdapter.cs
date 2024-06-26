using Microsoft.Win32.SafeHandles;
using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Ryujinx.Common.SystemInterop
{
    public partial class StdErrAdapter : IDisposable
    {
        private bool _disposable;
        private FileStream _pipeReader;
        private FileStream _pipeWriter;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _worker;

        public StdErrAdapter()
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                RegisterPosix();
            }
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private void RegisterPosix()
        {
            const int StdErrFileno = 2;

            (int readFd, int writeFd) = MakePipe();
            dup2(writeFd, StdErrFileno);

            _pipeReader = CreateFileDescriptorStream(readFd);
            _pipeWriter = CreateFileDescriptorStream(writeFd);

            _cancellationTokenSource = new CancellationTokenSource();
            _worker = Task.Run(async () => await EventWorkerAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _disposable = true;
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task EventWorkerAsync(CancellationToken cancellationToken)
        {
            using StreamReader reader = new(_pipeReader, leaveOpen: true);
            string line;
            while (cancellationToken.IsCancellationRequested == false && (line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                Logger.Error?.PrintRawMsg(line);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_disposable)
            {
                _disposable = false;

                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    _cancellationTokenSource.Cancel();
                    _worker.Wait(0);
                    _pipeReader?.Close();
                    _pipeWriter?.Close();
                }
            }
        }

        [LibraryImport("libc", SetLastError = true)]
        private static partial int dup2(int fd, int fd2);

        [LibraryImport("libc", SetLastError = true)]
        private static partial int pipe(Span<int> pipefd);

        private static (int, int) MakePipe()
        {
            Span<int> pipefd = stackalloc int[2];

            if (pipe(pipefd) == 0)
            {
                return (pipefd[0], pipefd[1]);
            }

            throw new();
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static FileStream CreateFileDescriptorStream(int fd)
        {
            return new FileStream(
                new SafeFileHandle(fd, ownsHandle: true),
                FileAccess.ReadWrite
            );
        }

    }
}
