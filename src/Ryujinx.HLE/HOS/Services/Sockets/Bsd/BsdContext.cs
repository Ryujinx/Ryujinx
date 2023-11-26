using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd
{
    class BsdContext
    {
        private static readonly ConcurrentDictionary<ulong, BsdContext> _registry = new();

        private readonly object _lock = new();

        private readonly List<IFileDescriptor> _fds;

        private BsdContext()
        {
            _fds = new List<IFileDescriptor>();
        }

        public ISocket RetrieveSocket(int socketFd)
        {
            IFileDescriptor file = RetrieveFileDescriptor(socketFd);

            if (file is ISocket socket)
            {
                return socket;
            }

            return null;
        }

        public IFileDescriptor RetrieveFileDescriptor(int fd)
        {
            lock (_lock)
            {
                if (fd >= 0 && _fds.Count > fd)
                {
                    return _fds[fd];
                }
            }

            return null;
        }

        public List<IFileDescriptor> RetrieveFileDescriptorsFromMask(ReadOnlySpan<byte> mask)
        {
            List<IFileDescriptor> fds = new();

            for (int i = 0; i < mask.Length; i++)
            {
                byte current = mask[i];

                while (current != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(current);
                    current &= (byte)~(1 << bit);
                    int fd = i * 8 + bit;

                    fds.Add(RetrieveFileDescriptor(fd));
                }
            }

            return fds;
        }

        public int RegisterFileDescriptor(IFileDescriptor file)
        {
            lock (_lock)
            {
                for (int fd = 0; fd < _fds.Count; fd++)
                {
                    if (_fds[fd] == null)
                    {
                        _fds[fd] = file;

                        return fd;
                    }
                }

                _fds.Add(file);

                return _fds.Count - 1;
            }
        }

        public void BuildMask(List<IFileDescriptor> fds, Span<byte> mask)
        {
            foreach (IFileDescriptor descriptor in fds)
            {
                int fd = _fds.IndexOf(descriptor);

                mask[fd >> 3] |= (byte)(1 << (fd & 7));
            }
        }

        public int DuplicateFileDescriptor(int fd)
        {
            IFileDescriptor oldFile = RetrieveFileDescriptor(fd);

            if (oldFile != null)
            {
                lock (_lock)
                {
                    oldFile.RefCount++;

                    return RegisterFileDescriptor(oldFile);
                }
            }

            return -1;
        }

        public bool CloseFileDescriptor(int fd)
        {
            IFileDescriptor file = RetrieveFileDescriptor(fd);

            if (file != null)
            {
                file.RefCount--;

                if (file.RefCount <= 0)
                {
                    file.Dispose();
                }

                lock (_lock)
                {
                    _fds[fd] = null;
                }

                return true;
            }

            return false;
        }

        public LinuxError ShutdownAllSockets(BsdSocketShutdownFlags how)
        {
            lock (_lock)
            {
                foreach (IFileDescriptor file in _fds)
                {
                    if (file is ISocket socket)
                    {
                        LinuxError errno = socket.Shutdown(how);

                        if (errno != LinuxError.SUCCESS)
                        {
                            return errno;
                        }
                    }
                }
            }

            return LinuxError.SUCCESS;
        }

        public static BsdContext GetOrRegister(ulong processId)
        {
            BsdContext context = GetContext(processId);

            if (context == null)
            {
                context = new BsdContext();

                _registry.TryAdd(processId, context);
            }

            return context;
        }

        public static BsdContext GetContext(ulong processId)
        {
            if (!_registry.TryGetValue(processId, out BsdContext processContext))
            {
                return null;
            }

            return processContext;
        }
    }
}
