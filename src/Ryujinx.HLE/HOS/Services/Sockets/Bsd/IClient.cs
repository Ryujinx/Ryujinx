using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd
{
    [Service("bsd:s", true)]
    [Service("bsd:u", false)]
    class IClient : IpcService
    {
        private static readonly List<IPollManager> _pollManagers = new()
        {
            EventFileDescriptorPollManager.Instance,
            ManagedSocketPollManager.Instance,
        };

        private BsdContext _context;
        private readonly bool _isPrivileged;

        public IClient(ServiceCtx context, bool isPrivileged) : base(context.Device.System.BsdServer)
        {
            _isPrivileged = isPrivileged;
        }

        private ResultCode WriteBsdResult(ServiceCtx context, int result, LinuxError errorCode = LinuxError.SUCCESS)
        {
            if (errorCode != LinuxError.SUCCESS)
            {
                result = -1;
            }

            context.ResponseData.Write(result);
            context.ResponseData.Write((int)errorCode);

            return ResultCode.Success;
        }

        private static AddressFamily ConvertBsdAddressFamily(BsdAddressFamily family)
        {
            return family switch
            {
                BsdAddressFamily.Unspecified => AddressFamily.Unspecified,
                BsdAddressFamily.InterNetwork => AddressFamily.InterNetwork,
                BsdAddressFamily.InterNetworkV6 => AddressFamily.InterNetworkV6,
                BsdAddressFamily.Unknown => AddressFamily.Unknown,
                _ => throw new NotImplementedException(family.ToString()),
            };
        }

        private LinuxError SetResultErrno(IFileDescriptor socket, int result)
        {
            return result == 0 && !socket.Blocking ? LinuxError.EWOULDBLOCK : LinuxError.SUCCESS;
        }

        private ResultCode SocketInternal(ServiceCtx context, bool exempt)
        {
            BsdAddressFamily domain = (BsdAddressFamily)context.RequestData.ReadInt32();
            BsdSocketType type = (BsdSocketType)context.RequestData.ReadInt32();
            ProtocolType protocol = (ProtocolType)context.RequestData.ReadInt32();

            BsdSocketCreationFlags creationFlags = (BsdSocketCreationFlags)((int)type >> (int)BsdSocketCreationFlags.FlagsShift);
            type &= BsdSocketType.TypeMask;

            if (domain == BsdAddressFamily.Unknown)
            {
                return WriteBsdResult(context, -1, LinuxError.EPROTONOSUPPORT);
            }
            else if ((type == BsdSocketType.Seqpacket || type == BsdSocketType.Raw) && !_isPrivileged)
            {
                if (domain != BsdAddressFamily.InterNetwork || type != BsdSocketType.Raw || protocol != ProtocolType.Icmp)
                {
                    return WriteBsdResult(context, -1, LinuxError.ENOENT);
                }
            }

            AddressFamily netDomain = ConvertBsdAddressFamily(domain);

            if (protocol == ProtocolType.IP)
            {
                if (type == BsdSocketType.Stream)
                {
                    protocol = ProtocolType.Tcp;
                }
                else if (type == BsdSocketType.Dgram)
                {
                    protocol = ProtocolType.Udp;
                }
            }

            ManagedSocket newBsdSocket = new(netDomain, (SocketType)type, protocol)
            {
                Blocking = !creationFlags.HasFlag(BsdSocketCreationFlags.NonBlocking),
            };

            LinuxError errno = LinuxError.SUCCESS;

            int newSockFd = _context.RegisterFileDescriptor(newBsdSocket);

            if (newSockFd == -1)
            {
                errno = LinuxError.EBADF;
            }

            if (exempt)
            {
                newBsdSocket.Disconnect();
            }

            return WriteBsdResult(context, newSockFd, errno);
        }

        private void WriteSockAddr(ServiceCtx context, ulong bufferPosition, ISocket socket, bool isRemote)
        {
            IPEndPoint endPoint = isRemote ? socket.RemoteEndPoint : socket.LocalEndPoint;

            if (endPoint != null)
            {
                context.Memory.Write(bufferPosition, BsdSockAddr.FromIPEndPoint(endPoint));
            }
            else
            {
                context.Memory.Write(bufferPosition, new BsdSockAddr());
            }
        }

        [CommandCmif(0)]
        // Initialize(nn::socket::BsdBufferConfig config, u64 pid, u64 transferMemorySize, KObject<copy, transfer_memory>, pid) -> u32 bsd_errno
        public ResultCode RegisterClient(ServiceCtx context)
        {
            _context = BsdContext.GetOrRegister(context.Request.HandleDesc.PId);

            /*
            typedef struct  {
                u32 version;                // Observed 1 on 2.0 LibAppletWeb, 2 on 3.0.
                u32 tcp_tx_buf_size;        // Size of the TCP transfer (send) buffer (initial or fixed).
                u32 tcp_rx_buf_size;        // Size of the TCP recieve buffer (initial or fixed).
                u32 tcp_tx_buf_max_size;    // Maximum size of the TCP transfer (send) buffer. If it is 0, the size of the buffer is fixed to its initial value.
                u32 tcp_rx_buf_max_size;    // Maximum size of the TCP receive buffer. If it is 0, the size of the buffer is fixed to its initial value.
                u32 udp_tx_buf_size;        // Size of the UDP transfer (send) buffer (typically 0x2400 bytes).
                u32 udp_rx_buf_size;        // Size of the UDP receive buffer (typically 0xA500 bytes).
                u32 sb_efficiency;          // Number of buffers for each socket (standard values range from 1 to 8).
            } BsdBufferConfig;
            */

            // bsd_error
            context.ResponseData.Write(0);

            Logger.Stub?.PrintStub(LogClass.ServiceBsd);

            // Close transfer memory immediately as we don't use it.
            context.Device.System.KernelContext.Syscall.CloseHandle(context.Request.HandleDesc.ToCopy[0]);

            return ResultCode.Success;
        }

        [CommandCmif(1)]
        // StartMonitoring(u64, pid)
        public ResultCode StartMonitoring(ServiceCtx context)
        {
            ulong unknown0 = context.RequestData.ReadUInt64();

            Logger.Stub?.PrintStub(LogClass.ServiceBsd, new { unknown0 });

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // Socket(u32 domain, u32 type, u32 protocol) -> (i32 ret, u32 bsd_errno)
        public ResultCode Socket(ServiceCtx context)
        {
            return SocketInternal(context, false);
        }

        [CommandCmif(3)]
        // SocketExempt(u32 domain, u32 type, u32 protocol) -> (i32 ret, u32 bsd_errno)
        public ResultCode SocketExempt(ServiceCtx context)
        {
            return SocketInternal(context, true);
        }

        [CommandCmif(4)]
        // Open(u32 flags, array<unknown, 0x21> path) -> (i32 ret, u32 bsd_errno)
        public ResultCode Open(ServiceCtx context)
        {
            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x21();

            int flags = context.RequestData.ReadInt32();

            byte[] rawPath = new byte[bufferSize];

            context.Memory.Read(bufferPosition, rawPath);

            string path = Encoding.ASCII.GetString(rawPath);

            WriteBsdResult(context, -1, LinuxError.EOPNOTSUPP);

            Logger.Stub?.PrintStub(LogClass.ServiceBsd, new { path, flags });

            return ResultCode.Success;
        }

        [CommandCmif(5)]
        // Select(u32 nfds, nn::socket::timeval timeout, buffer<nn::socket::fd_set, 0x21, 0> readfds_in, buffer<nn::socket::fd_set, 0x21, 0> writefds_in, buffer<nn::socket::fd_set, 0x21, 0> errorfds_in)
        // -> (i32 ret, u32 bsd_errno, buffer<nn::socket::fd_set, 0x22, 0> readfds_out, buffer<nn::socket::fd_set, 0x22, 0> writefds_out, buffer<nn::socket::fd_set, 0x22, 0> errorfds_out)
        public ResultCode Select(ServiceCtx context)
        {
            int fdsCount = context.RequestData.ReadInt32();
            int timeout = context.RequestData.ReadInt32();

            (ulong readFdsInBufferPosition, ulong readFdsInBufferSize) = context.Request.GetBufferType0x21(0);
            (ulong writeFdsInBufferPosition, ulong writeFdsInBufferSize) = context.Request.GetBufferType0x21(1);
            (ulong errorFdsInBufferPosition, ulong errorFdsInBufferSize) = context.Request.GetBufferType0x21(2);

            (ulong readFdsOutBufferPosition, ulong readFdsOutBufferSize) = context.Request.GetBufferType0x22(0);
            (ulong writeFdsOutBufferPosition, ulong writeFdsOutBufferSize) = context.Request.GetBufferType0x22(1);
            (ulong errorFdsOutBufferPosition, ulong errorFdsOutBufferSize) = context.Request.GetBufferType0x22(2);

            List<IFileDescriptor> readFds = _context.RetrieveFileDescriptorsFromMask(context.Memory.GetSpan(readFdsInBufferPosition, (int)readFdsInBufferSize));
            List<IFileDescriptor> writeFds = _context.RetrieveFileDescriptorsFromMask(context.Memory.GetSpan(writeFdsInBufferPosition, (int)writeFdsInBufferSize));
            List<IFileDescriptor> errorFds = _context.RetrieveFileDescriptorsFromMask(context.Memory.GetSpan(errorFdsInBufferPosition, (int)errorFdsInBufferSize));

            int actualFdsCount = readFds.Count + writeFds.Count + errorFds.Count;

            if (fdsCount == 0 || actualFdsCount == 0)
            {
                WriteBsdResult(context, 0);

                return ResultCode.Success;
            }

            PollEvent[] events = new PollEvent[actualFdsCount];

            int index = 0;

            foreach (IFileDescriptor fd in readFds)
            {
                events[index] = new PollEvent(new PollEventData { InputEvents = PollEventTypeMask.Input }, fd);

                index++;
            }

            foreach (IFileDescriptor fd in writeFds)
            {
                events[index] = new PollEvent(new PollEventData { InputEvents = PollEventTypeMask.Output }, fd);

                index++;
            }

            foreach (IFileDescriptor fd in errorFds)
            {
                events[index] = new PollEvent(new PollEventData { InputEvents = PollEventTypeMask.Error }, fd);

                index++;
            }

            List<PollEvent>[] eventsByPollManager = new List<PollEvent>[_pollManagers.Count];

            for (int i = 0; i < eventsByPollManager.Length; i++)
            {
                eventsByPollManager[i] = new List<PollEvent>();

                foreach (PollEvent evnt in events)
                {
                    if (_pollManagers[i].IsCompatible(evnt))
                    {
                        eventsByPollManager[i].Add(evnt);
                    }
                }
            }

            int updatedCount = 0;

            for (int i = 0; i < _pollManagers.Count; i++)
            {
                if (eventsByPollManager[i].Count > 0)
                {
                    _pollManagers[i].Select(eventsByPollManager[i], timeout, out int updatedPollCount);
                    updatedCount += updatedPollCount;
                }
            }

            readFds.Clear();
            writeFds.Clear();
            errorFds.Clear();

            foreach (PollEvent pollEvent in events)
            {
                for (int i = 0; i < _pollManagers.Count; i++)
                {
                    if (eventsByPollManager[i].Contains(pollEvent))
                    {
                        if (pollEvent.Data.OutputEvents.HasFlag(PollEventTypeMask.Input))
                        {
                            readFds.Add(pollEvent.FileDescriptor);
                        }

                        if (pollEvent.Data.OutputEvents.HasFlag(PollEventTypeMask.Output))
                        {
                            writeFds.Add(pollEvent.FileDescriptor);
                        }

                        if (pollEvent.Data.OutputEvents.HasFlag(PollEventTypeMask.Error))
                        {
                            errorFds.Add(pollEvent.FileDescriptor);
                        }
                    }
                }
            }

            using var readFdsOut = context.Memory.GetWritableRegion(readFdsOutBufferPosition, (int)readFdsOutBufferSize);
            using var writeFdsOut = context.Memory.GetWritableRegion(writeFdsOutBufferPosition, (int)writeFdsOutBufferSize);
            using var errorFdsOut = context.Memory.GetWritableRegion(errorFdsOutBufferPosition, (int)errorFdsOutBufferSize);

            _context.BuildMask(readFds, readFdsOut.Memory.Span);
            _context.BuildMask(writeFds, writeFdsOut.Memory.Span);
            _context.BuildMask(errorFds, errorFdsOut.Memory.Span);

            WriteBsdResult(context, updatedCount);

            return ResultCode.Success;
        }

        [CommandCmif(6)]
        // Poll(u32 nfds, u32 timeout, buffer<unknown, 0x21, 0> fds) -> (i32 ret, u32 bsd_errno, buffer<unknown, 0x22, 0>)
        public ResultCode Poll(ServiceCtx context)
        {
            int fdsCount = context.RequestData.ReadInt32();
            int timeout = context.RequestData.ReadInt32();

            (ulong inputBufferPosition, ulong inputBufferSize) = context.Request.GetBufferType0x21();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong outputBufferPosition, ulong outputBufferSize) = context.Request.GetBufferType0x22();
#pragma warning restore IDE0059

            if (timeout < -1 || fdsCount < 0 || (ulong)(fdsCount * 8) > inputBufferSize)
            {
                return WriteBsdResult(context, -1, LinuxError.EINVAL);
            }

            PollEvent[] events = new PollEvent[fdsCount];

            for (int i = 0; i < fdsCount; i++)
            {
                PollEventData pollEventData = context.Memory.Read<PollEventData>(inputBufferPosition + (ulong)(i * Unsafe.SizeOf<PollEventData>()));

                IFileDescriptor fileDescriptor = _context.RetrieveFileDescriptor(pollEventData.SocketFd);

                if (fileDescriptor == null)
                {
                    return WriteBsdResult(context, -1, LinuxError.EBADF);
                }

                events[i] = new PollEvent(pollEventData, fileDescriptor);
            }

            List<PollEvent> discoveredEvents = new();
            List<PollEvent>[] eventsByPollManager = new List<PollEvent>[_pollManagers.Count];

            for (int i = 0; i < eventsByPollManager.Length; i++)
            {
                eventsByPollManager[i] = new List<PollEvent>();

                foreach (PollEvent evnt in events)
                {
                    if (_pollManagers[i].IsCompatible(evnt))
                    {
                        eventsByPollManager[i].Add(evnt);
                        discoveredEvents.Add(evnt);
                    }
                }
            }

            foreach (PollEvent evnt in events)
            {
                if (!discoveredEvents.Contains(evnt))
                {
                    Logger.Error?.Print(LogClass.ServiceBsd, $"Poll operation is not supported for {evnt.FileDescriptor.GetType().Name}!");

                    return WriteBsdResult(context, -1, LinuxError.EBADF);
                }
            }

            int updateCount = 0;

            LinuxError errno = LinuxError.SUCCESS;

            if (fdsCount != 0)
            {
                static bool IsUnexpectedLinuxError(LinuxError error)
                {
                    return error != LinuxError.SUCCESS && error != LinuxError.ETIMEDOUT;
                }

                // Hybrid approach
                long budgetLeftMilliseconds;

                if (timeout == -1)
                {
                    budgetLeftMilliseconds = PerformanceCounter.ElapsedMilliseconds + uint.MaxValue;
                }
                else
                {
                    budgetLeftMilliseconds = PerformanceCounter.ElapsedMilliseconds + timeout;
                }

                do
                {
                    for (int i = 0; i < eventsByPollManager.Length; i++)
                    {
                        if (eventsByPollManager[i].Count == 0)
                        {
                            continue;
                        }

                        errno = _pollManagers[i].Poll(eventsByPollManager[i], 0, out updateCount);

                        if (IsUnexpectedLinuxError(errno))
                        {
                            break;
                        }

                        if (updateCount > 0)
                        {
                            break;
                        }
                    }

                    if (updateCount > 0)
                    {
                        break;
                    }

                    // If we are here, that mean nothing was available, sleep for 50ms
                    context.Device.System.KernelContext.Syscall.SleepThread(50 * 1000000);
                    context.Thread.HandlePostSyscall();
                }
                while (context.Thread.Context.Running && PerformanceCounter.ElapsedMilliseconds < budgetLeftMilliseconds);
            }
            else if (timeout == -1)
            {
                // FIXME: If we get a timeout of -1 and there is no fds to wait on, this should kill the KProcess. (need to check that with re)
                throw new InvalidOperationException();
            }
            else
            {
                context.Device.System.KernelContext.Syscall.SleepThread(timeout);
            }

            // TODO: Spanify
            for (int i = 0; i < fdsCount; i++)
            {
                context.Memory.Write(outputBufferPosition + (ulong)(i * Unsafe.SizeOf<PollEventData>()), events[i].Data);
            }

            // In case of non blocking call timeout should not be returned.
            if (timeout == 0 && errno == LinuxError.ETIMEDOUT)
            {
                errno = LinuxError.SUCCESS;
            }

            return WriteBsdResult(context, updateCount, errno);
        }

        [CommandCmif(7)]
        // Sysctl(buffer<unknown, 0x21, 0>, buffer<unknown, 0x21, 0>) -> (i32 ret, u32 bsd_errno, u32, buffer<unknown, 0x22, 0>)
        public ResultCode Sysctl(ServiceCtx context)
        {
            WriteBsdResult(context, -1, LinuxError.EOPNOTSUPP);

            Logger.Stub?.PrintStub(LogClass.ServiceBsd);

            return ResultCode.Success;
        }

        [CommandCmif(8)]
        // Recv(u32 socket, u32 flags) -> (i32 ret, u32 bsd_errno, array<i8, 0x22> message)
        public ResultCode Recv(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            BsdSocketFlags socketFlags = (BsdSocketFlags)context.RequestData.ReadInt32();

            (ulong receivePosition, ulong receiveLength) = context.Request.GetBufferType0x22();

            WritableRegion receiveRegion = context.Memory.GetWritableRegion(receivePosition, (int)receiveLength);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            int result = -1;

            if (socket != null)
            {
                errno = socket.Receive(out result, receiveRegion.Memory.Span, socketFlags);

                if (errno == LinuxError.SUCCESS)
                {
                    SetResultErrno(socket, result);

                    receiveRegion.Dispose();
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(9)]
        // RecvFrom(u32 sock, u32 flags) -> (i32 ret, u32 bsd_errno, u32 addrlen, buffer<i8, 0x22, 0> message, buffer<nn::socket::sockaddr_in, 0x22, 0x10>)
        public ResultCode RecvFrom(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            BsdSocketFlags socketFlags = (BsdSocketFlags)context.RequestData.ReadInt32();

            (ulong receivePosition, ulong receiveLength) = context.Request.GetBufferType0x22(0);
            (ulong sockAddrOutPosition, ulong sockAddrOutSize) = context.Request.GetBufferType0x22(1);

            WritableRegion receiveRegion = context.Memory.GetWritableRegion(receivePosition, (int)receiveLength);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            int result = -1;

            if (socket != null)
            {
                errno = socket.ReceiveFrom(out result, receiveRegion.Memory.Span, receiveRegion.Memory.Span.Length, socketFlags, out IPEndPoint endPoint);

                if (errno == LinuxError.SUCCESS)
                {
                    SetResultErrno(socket, result);

                    receiveRegion.Dispose();

                    if (sockAddrOutSize != 0 && sockAddrOutSize >= (ulong)Unsafe.SizeOf<BsdSockAddr>())
                    {
                        context.Memory.Write(sockAddrOutPosition, BsdSockAddr.FromIPEndPoint(endPoint));
                    }
                    else
                    {
                        errno = LinuxError.ENOMEM;
                    }
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(10)]
        // Send(u32 socket, u32 flags, buffer<i8, 0x21, 0>) -> (i32 ret, u32 bsd_errno)
        public ResultCode Send(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            BsdSocketFlags socketFlags = (BsdSocketFlags)context.RequestData.ReadInt32();

            (ulong sendPosition, ulong sendSize) = context.Request.GetBufferType0x21();

            ReadOnlySpan<byte> sendBuffer = context.Memory.GetSpan(sendPosition, (int)sendSize);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            int result = -1;

            if (socket != null)
            {
                errno = socket.Send(out result, sendBuffer, socketFlags);

                if (errno == LinuxError.SUCCESS)
                {
                    SetResultErrno(socket, result);
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(11)]
        // SendTo(u32 socket, u32 flags, buffer<i8, 0x21, 0>, buffer<nn::socket::sockaddr_in, 0x21, 0x10>) -> (i32 ret, u32 bsd_errno)
        public ResultCode SendTo(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            BsdSocketFlags socketFlags = (BsdSocketFlags)context.RequestData.ReadInt32();

            (ulong sendPosition, ulong sendSize) = context.Request.GetBufferType0x21(0);
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x21(1);
#pragma warning restore IDE0059

            ReadOnlySpan<byte> sendBuffer = context.Memory.GetSpan(sendPosition, (int)sendSize);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            int result = -1;

            if (socket != null)
            {
                IPEndPoint endPoint = context.Memory.Read<BsdSockAddr>(bufferPosition).ToIPEndPoint();

                errno = socket.SendTo(out result, sendBuffer, sendBuffer.Length, socketFlags, endPoint);

                if (errno == LinuxError.SUCCESS)
                {
                    SetResultErrno(socket, result);
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(12)]
        // Accept(u32 socket) -> (i32 ret, u32 bsd_errno, u32 addrlen, buffer<nn::socket::sockaddr_in, 0x22, 0x10> addr)
        public ResultCode Accept(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong bufferPos, ulong bufferSize) = context.Request.GetBufferType0x22();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = socket.Accept(out ISocket newSocket);

                if (newSocket == null && errno == LinuxError.SUCCESS)
                {
                    errno = LinuxError.EWOULDBLOCK;
                }
                else if (errno == LinuxError.SUCCESS)
                {
                    int newSockFd = _context.RegisterFileDescriptor(newSocket);

                    if (newSockFd == -1)
                    {
                        errno = LinuxError.EBADF;
                    }
                    else
                    {
                        WriteSockAddr(context, bufferPos, newSocket, true);
                    }

                    WriteBsdResult(context, newSockFd, errno);

                    context.ResponseData.Write(0x10);

                    return ResultCode.Success;
                }
            }

            return WriteBsdResult(context, -1, errno);
        }

        [CommandCmif(13)]
        // Bind(u32 socket, buffer<nn::socket::sockaddr_in, 0x21, 0x10> addr) -> (i32 ret, u32 bsd_errno)
        public ResultCode Bind(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x21();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                IPEndPoint endPoint = context.Memory.Read<BsdSockAddr>(bufferPosition).ToIPEndPoint();

                errno = socket.Bind(endPoint);
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(14)]
        // Connect(u32 socket, buffer<nn::socket::sockaddr_in, 0x21, 0x10>) -> (i32 ret, u32 bsd_errno)
        public ResultCode Connect(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x21();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                IPEndPoint endPoint = context.Memory.Read<BsdSockAddr>(bufferPosition).ToIPEndPoint();

                errno = socket.Connect(endPoint);
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(15)]
        // GetPeerName(u32 socket) -> (i32 ret, u32 bsd_errno, u32 addrlen, buffer<nn::socket::sockaddr_in, 0x22, 0x10> addr)
        public ResultCode GetPeerName(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x22();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            if (socket != null)
            {
                errno = LinuxError.ENOTCONN;

                if (socket.RemoteEndPoint != null)
                {
                    errno = LinuxError.SUCCESS;

                    WriteSockAddr(context, bufferPosition, socket, true);
                    WriteBsdResult(context, 0, errno);
                    context.ResponseData.Write(Unsafe.SizeOf<BsdSockAddr>());
                }
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(16)]
        // GetSockName(u32 socket) -> (i32 ret, u32 bsd_errno, u32 addrlen, buffer<nn::socket::sockaddr_in, 0x22, 0x10> addr)
        public ResultCode GetSockName(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            (ulong bufferPos, ulong bufferSize) = context.Request.GetBufferType0x22();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = LinuxError.SUCCESS;

                WriteSockAddr(context, bufferPos, socket, false);
                WriteBsdResult(context, 0, errno);
                context.ResponseData.Write(Unsafe.SizeOf<BsdSockAddr>());
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(17)]
        // GetSockOpt(u32 socket, u32 level, u32 option_name) -> (i32 ret, u32 bsd_errno, u32, buffer<unknown, 0x22, 0>)
        public ResultCode GetSockOpt(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            SocketOptionLevel level = (SocketOptionLevel)context.RequestData.ReadInt32();
            BsdSocketOption option = (BsdSocketOption)context.RequestData.ReadInt32();

            (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x22();
            WritableRegion optionValue = context.Memory.GetWritableRegion(bufferPosition, (int)bufferSize);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = socket.GetSocketOption(option, level, optionValue.Memory.Span);

                if (errno == LinuxError.SUCCESS)
                {
                    optionValue.Dispose();
                }
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(18)]
        // Listen(u32 socket, u32 backlog) -> (i32 ret, u32 bsd_errno)
        public ResultCode Listen(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            int backlog = context.RequestData.ReadInt32();

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = socket.Listen(backlog);
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(19)]
        // Ioctl(u32 fd, u32 request, u32 bufcount, buffer<unknown, 0x21, 0>, buffer<unknown, 0x21, 0>, buffer<unknown, 0x21, 0>, buffer<unknown, 0x21, 0>) -> (i32 ret, u32 bsd_errno, buffer<unknown, 0x22, 0>, buffer<unknown, 0x22, 0>, buffer<unknown, 0x22, 0>, buffer<unknown, 0x22, 0>)
        public ResultCode Ioctl(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            BsdIoctl cmd = (BsdIoctl)context.RequestData.ReadInt32();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            int bufferCount = context.RequestData.ReadInt32();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                switch (cmd)
                {
                    case BsdIoctl.AtMark:
                        errno = LinuxError.SUCCESS;

#pragma warning disable IDE0059 // Remove unnecessary value assignment
                        (ulong bufferPosition, ulong bufferSize) = context.Request.GetBufferType0x22();
#pragma warning restore IDE0059

                        // FIXME: OOB not implemented.
                        context.Memory.Write(bufferPosition, 0);
                        break;

                    default:
                        errno = LinuxError.EOPNOTSUPP;

                        Logger.Warning?.Print(LogClass.ServiceBsd, $"Unsupported Ioctl Cmd: {cmd}");
                        break;
                }
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(20)]
        // Fcntl(u32 socket, u32 cmd, u32 arg) -> (i32 ret, u32 bsd_errno)
        public ResultCode Fcntl(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            int cmd = context.RequestData.ReadInt32();
            int arg = context.RequestData.ReadInt32();

            int result = 0;
            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = LinuxError.SUCCESS;

                if (cmd == 0x3)
                {
                    result = !socket.Blocking ? 0x800 : 0;
                }
                else if (cmd == 0x4 && arg == 0x800)
                {
                    socket.Blocking = false;
                    result = 0;
                }
                else
                {
                    errno = LinuxError.EOPNOTSUPP;
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(21)]
        // SetSockOpt(u32 socket, u32 level, u32 option_name, buffer<unknown, 0x21, 0> option_value) -> (i32 ret, u32 bsd_errno)
        public ResultCode SetSockOpt(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            SocketOptionLevel level = (SocketOptionLevel)context.RequestData.ReadInt32();
            BsdSocketOption option = (BsdSocketOption)context.RequestData.ReadInt32();

            (ulong bufferPos, ulong bufferSize) = context.Request.GetBufferType0x21();

            ReadOnlySpan<byte> optionValue = context.Memory.GetSpan(bufferPos, (int)bufferSize);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = socket.SetSocketOption(option, level, optionValue);
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(22)]
        // Shutdown(u32 socket, u32 how) -> (i32 ret, u32 bsd_errno)
        public ResultCode Shutdown(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            int how = context.RequestData.ReadInt32();

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);

            if (socket != null)
            {
                errno = LinuxError.EINVAL;

                if (how >= 0 && how <= 2)
                {
                    errno = socket.Shutdown((BsdSocketShutdownFlags)how);
                }
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(23)]
        // ShutdownAllSockets(u32 how) -> (i32 ret, u32 bsd_errno)
        public ResultCode ShutdownAllSockets(ServiceCtx context)
        {
            int how = context.RequestData.ReadInt32();

            LinuxError errno = LinuxError.EINVAL;

            if (how >= 0 && how <= 2)
            {
                errno = _context.ShutdownAllSockets((BsdSocketShutdownFlags)how);
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(24)]
        // Write(u32 fd, buffer<i8, 0x21, 0> message) -> (i32 ret, u32 bsd_errno)
        public ResultCode Write(ServiceCtx context)
        {
            int fd = context.RequestData.ReadInt32();

            (ulong sendPosition, ulong sendSize) = context.Request.GetBufferType0x21();

            ReadOnlySpan<byte> sendBuffer = context.Memory.GetSpan(sendPosition, (int)sendSize);

            LinuxError errno = LinuxError.EBADF;
            IFileDescriptor file = _context.RetrieveFileDescriptor(fd);
            int result = -1;

            if (file != null)
            {
                errno = file.Write(out result, sendBuffer);

                if (errno == LinuxError.SUCCESS)
                {
                    SetResultErrno(file, result);
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(25)]
        // Read(u32 fd) -> (i32 ret, u32 bsd_errno, buffer<i8, 0x22, 0> message)
        public ResultCode Read(ServiceCtx context)
        {
            int fd = context.RequestData.ReadInt32();

            (ulong receivePosition, ulong receiveLength) = context.Request.GetBufferType0x22();

            WritableRegion receiveRegion = context.Memory.GetWritableRegion(receivePosition, (int)receiveLength);

            LinuxError errno = LinuxError.EBADF;
            IFileDescriptor file = _context.RetrieveFileDescriptor(fd);
            int result = -1;

            if (file != null)
            {
                errno = file.Read(out result, receiveRegion.Memory.Span);

                if (errno == LinuxError.SUCCESS)
                {
                    SetResultErrno(file, result);

                    receiveRegion.Dispose();
                }
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(26)]
        // Close(u32 fd) -> (i32 ret, u32 bsd_errno)
        public ResultCode Close(ServiceCtx context)
        {
            int fd = context.RequestData.ReadInt32();

            LinuxError errno = LinuxError.EBADF;

            if (_context.CloseFileDescriptor(fd))
            {
                errno = LinuxError.SUCCESS;
            }

            return WriteBsdResult(context, 0, errno);
        }

        [CommandCmif(27)]
        // DuplicateSocket(u32 fd, u64 reserved) -> (i32 ret, u32 bsd_errno)
        public ResultCode DuplicateSocket(ServiceCtx context)
        {
            int fd = context.RequestData.ReadInt32();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong reserved = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059

            LinuxError errno = LinuxError.ENOENT;
            int newSockFd = -1;

            if (_isPrivileged)
            {
                errno = LinuxError.SUCCESS;

                newSockFd = _context.DuplicateFileDescriptor(fd);

                if (newSockFd == -1)
                {
                    errno = LinuxError.EBADF;
                }
            }

            return WriteBsdResult(context, newSockFd, errno);
        }


        [CommandCmif(29)] // 7.0.0+
        // RecvMMsg(u32 fd, u32 vlen, u32 flags, u32 reserved, nn::socket::TimeVal timeout) -> (i32 ret, u32 bsd_errno, buffer<bytes, 6> message);
        public ResultCode RecvMMsg(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            int vlen = context.RequestData.ReadInt32();
            BsdSocketFlags socketFlags = (BsdSocketFlags)context.RequestData.ReadInt32();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            uint reserved = context.RequestData.ReadUInt32();
#pragma warning restore IDE0059
            TimeVal timeout = context.RequestData.ReadStruct<TimeVal>();

            ulong receivePosition = context.Request.ReceiveBuff[0].Position;
            ulong receiveLength = context.Request.ReceiveBuff[0].Size;

            WritableRegion receiveRegion = context.Memory.GetWritableRegion(receivePosition, (int)receiveLength);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            int result = -1;

            if (socket != null)
            {
                errno = BsdMMsgHdr.Deserialize(out BsdMMsgHdr message, receiveRegion.Memory.Span, vlen);

                if (errno == LinuxError.SUCCESS)
                {
                    errno = socket.RecvMMsg(out result, message, socketFlags, timeout);

                    if (errno == LinuxError.SUCCESS)
                    {
                        errno = BsdMMsgHdr.Serialize(receiveRegion.Memory.Span, message);
                    }
                }
            }

            if (errno == LinuxError.SUCCESS)
            {
                SetResultErrno(socket, result);
                receiveRegion.Dispose();
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(30)] // 7.0.0+
        // SendMMsg(u32 fd, u32 vlen, u32 flags) -> (i32 ret, u32 bsd_errno, buffer<bytes, 6> message);
        public ResultCode SendMMsg(ServiceCtx context)
        {
            int socketFd = context.RequestData.ReadInt32();
            int vlen = context.RequestData.ReadInt32();
            BsdSocketFlags socketFlags = (BsdSocketFlags)context.RequestData.ReadInt32();

            ulong receivePosition = context.Request.ReceiveBuff[0].Position;
            ulong receiveLength = context.Request.ReceiveBuff[0].Size;

            WritableRegion receiveRegion = context.Memory.GetWritableRegion(receivePosition, (int)receiveLength);

            LinuxError errno = LinuxError.EBADF;
            ISocket socket = _context.RetrieveSocket(socketFd);
            int result = -1;

            if (socket != null)
            {
                errno = BsdMMsgHdr.Deserialize(out BsdMMsgHdr message, receiveRegion.Memory.Span, vlen);

                if (errno == LinuxError.SUCCESS)
                {
                    errno = socket.SendMMsg(out result, message, socketFlags);

                    if (errno == LinuxError.SUCCESS)
                    {
                        errno = BsdMMsgHdr.Serialize(receiveRegion.Memory.Span, message);
                    }
                }
            }

            if (errno == LinuxError.SUCCESS)
            {
                SetResultErrno(socket, result);
                receiveRegion.Dispose();
            }

            return WriteBsdResult(context, result, errno);
        }

        [CommandCmif(31)] // 7.0.0+
        // EventFd(nn::socket::EventFdFlags flags, u64 initval) -> (i32 ret, u32 bsd_errno)
        public ResultCode EventFd(ServiceCtx context)
        {
            EventFdFlags flags = (EventFdFlags)context.RequestData.ReadUInt32();
            context.RequestData.BaseStream.Position += 4; // Padding
            ulong initialValue = context.RequestData.ReadUInt64();

            EventFileDescriptor newEventFile = new(initialValue, flags);

            LinuxError errno = LinuxError.SUCCESS;

            int newSockFd = _context.RegisterFileDescriptor(newEventFile);

            if (newSockFd == -1)
            {
                errno = LinuxError.EBADF;
            }

            return WriteBsdResult(context, newSockFd, errno);
        }
    }
}
