using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl
{
    class ManagedSocket : ISocket
    {
        public int Refcount { get; set; }

        public AddressFamily AddressFamily => Socket.AddressFamily;

        public SocketType SocketType => Socket.SocketType;

        public ProtocolType ProtocolType => Socket.ProtocolType;

        public bool Blocking { get => Socket.Blocking; set => Socket.Blocking = value; }

        public IntPtr Handle => Socket.Handle;

        public IPEndPoint RemoteEndPoint => Socket.RemoteEndPoint as IPEndPoint;

        public IPEndPoint LocalEndPoint => Socket.LocalEndPoint as IPEndPoint;

        public Socket Socket { get; }

        public ManagedSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            Socket = new Socket(addressFamily, socketType, protocolType);
            Refcount = 1;
        }

        private ManagedSocket(Socket socket)
        {
            Socket = socket;
            Refcount = 1;
        }

        private static SocketFlags ConvertBsdSocketFlags(BsdSocketFlags bsdSocketFlags)
        {
            SocketFlags socketFlags = SocketFlags.None;

            if (bsdSocketFlags.HasFlag(BsdSocketFlags.Oob))
            {
                socketFlags |= SocketFlags.OutOfBand;
            }

            if (bsdSocketFlags.HasFlag(BsdSocketFlags.Peek))
            {
                socketFlags |= SocketFlags.Peek;
            }

            if (bsdSocketFlags.HasFlag(BsdSocketFlags.DontRoute))
            {
                socketFlags |= SocketFlags.DontRoute;
            }

            if (bsdSocketFlags.HasFlag(BsdSocketFlags.Trunc))
            {
                socketFlags |= SocketFlags.Truncated;
            }

            if (bsdSocketFlags.HasFlag(BsdSocketFlags.CTrunc))
            {
                socketFlags |= SocketFlags.ControlDataTruncated;
            }

            bsdSocketFlags &= ~(BsdSocketFlags.Oob |
                BsdSocketFlags.Peek |
                BsdSocketFlags.DontRoute |
                BsdSocketFlags.DontWait |
                BsdSocketFlags.Trunc |
                BsdSocketFlags.CTrunc);

            if (bsdSocketFlags != BsdSocketFlags.None)
            {
                Logger.Warning?.Print(LogClass.ServiceBsd, $"Unsupported socket flags: {bsdSocketFlags}");
            }

            return socketFlags;
        }

        public LinuxError Accept(out ISocket newSocket)
        {
            try
            {
                newSocket = new ManagedSocket(Socket.Accept());

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                newSocket = null;

                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError Bind(IPEndPoint localEndPoint)
        {
            try
            {
                Socket.Bind(localEndPoint);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public void Close()
        {
            Socket.Close();
        }

        public LinuxError Connect(IPEndPoint remoteEndPoint)
        {
            try
            {
                Socket.Connect(remoteEndPoint);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                if (!Blocking && exception.ErrorCode == (int)WsaError.WSAEWOULDBLOCK)
                {
                    return LinuxError.EINPROGRESS;
                }
                else
                {
                    return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
                }
            }
        }

        public void Disconnect()
        {
            Socket.Disconnect(true);
        }

        public void Dispose()
        {
            Socket.Close();
            Socket.Dispose();
        }

        public LinuxError Listen(int backlog)
        {
            try
            {
                Socket.Listen(backlog);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public bool Poll(int microSeconds, SelectMode mode)
        {
            return Socket.Poll(microSeconds, mode);
        }

        public LinuxError Shutdown(BsdSocketShutdownFlags how)
        {
            try
            {
                Socket.Shutdown((SocketShutdown)how);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError Receive(out int receiveSize, Span<byte> buffer, BsdSocketFlags flags)
        {
            LinuxError result;

            bool shouldBlockAfterOperation = false;

            try
            {
                if (Blocking && flags.HasFlag(BsdSocketFlags.DontWait))
                {
                    Blocking = false;
                    shouldBlockAfterOperation = true;
                }

                receiveSize = Socket.Receive(buffer, ConvertBsdSocketFlags(flags));

                result = LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                receiveSize = -1;

                result = WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }

            if (shouldBlockAfterOperation)
            {
                Blocking = true;
            }

            return result;
        }

        public LinuxError ReceiveFrom(out int receiveSize, Span<byte> buffer, int size, BsdSocketFlags flags, out IPEndPoint remoteEndPoint)
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            LinuxError result;

            bool shouldBlockAfterOperation = false;

            try
            {
                EndPoint temp = new IPEndPoint(IPAddress.Any, 0);

                if (Blocking && flags.HasFlag(BsdSocketFlags.DontWait))
                {
                    Blocking = false;
                    shouldBlockAfterOperation = true;
                }

                if (!Socket.IsBound)
                {
                    receiveSize = -1;

                    return LinuxError.EOPNOTSUPP;
                }

                receiveSize = Socket.ReceiveFrom(buffer[..size], ConvertBsdSocketFlags(flags), ref temp);

                remoteEndPoint = (IPEndPoint)temp;
                result = LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                receiveSize = -1;

                result = WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }

            if (shouldBlockAfterOperation)
            {
                Blocking = true;
            }

            return result;
        }

        public LinuxError Send(out int sendSize, ReadOnlySpan<byte> buffer, BsdSocketFlags flags)
        {
            try
            {
                sendSize = Socket.Send(buffer, ConvertBsdSocketFlags(flags));

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                sendSize = -1;

                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError SendTo(out int sendSize, ReadOnlySpan<byte> buffer, int size, BsdSocketFlags flags, IPEndPoint remoteEndPoint)
        {
            try
            {
                sendSize = Socket.SendTo(buffer[..size], ConvertBsdSocketFlags(flags), remoteEndPoint);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                sendSize = -1;

                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError GetSocketOption(BsdSocketOption option, SocketOptionLevel level, Span<byte> optionValue)
        {
            try
            {
                LinuxError result = WinSockHelper.ValidateSocketOption(option, level, write: false);

                if (result != LinuxError.SUCCESS)
                {
                    Logger.Warning?.Print(LogClass.ServiceBsd, $"Invalid GetSockOpt Option: {option} Level: {level}");

                    return result;
                }

                if (!WinSockHelper.TryConvertSocketOption(option, level, out SocketOptionName optionName))
                {
                    Logger.Warning?.Print(LogClass.ServiceBsd, $"Unsupported GetSockOpt Option: {option} Level: {level}");
                    optionValue.Clear();

                    return LinuxError.SUCCESS;
                }

                byte[] tempOptionValue = new byte[optionValue.Length];

                Socket.GetSocketOption(level, optionName, tempOptionValue);

                tempOptionValue.AsSpan().CopyTo(optionValue);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError SetSocketOption(BsdSocketOption option, SocketOptionLevel level, ReadOnlySpan<byte> optionValue)
        {
            try
            {
                LinuxError result = WinSockHelper.ValidateSocketOption(option, level, write: true);

                if (result != LinuxError.SUCCESS)
                {
                    Logger.Warning?.Print(LogClass.ServiceBsd, $"Invalid SetSockOpt Option: {option} Level: {level}");

                    return result;
                }

                if (!WinSockHelper.TryConvertSocketOption(option, level, out SocketOptionName optionName))
                {
                    Logger.Warning?.Print(LogClass.ServiceBsd, $"Unsupported SetSockOpt Option: {option} Level: {level}");

                    return LinuxError.SUCCESS;
                }

                int value = optionValue.Length >= 4 ? MemoryMarshal.Read<int>(optionValue) : MemoryMarshal.Read<byte>(optionValue);

                if (level == SocketOptionLevel.Socket && option == BsdSocketOption.SoLinger)
                {
                    int value2 = 0;

                    if (optionValue.Length >= 8)
                    {
                        value2 = MemoryMarshal.Read<int>(optionValue[4..]);
                    }

                    Socket.SetSocketOption(level, SocketOptionName.Linger, new LingerOption(value != 0, value2));
                }
                else
                {
                    Socket.SetSocketOption(level, optionName, value);
                }

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError Read(out int readSize, Span<byte> buffer)
        {
            return Receive(out readSize, buffer, BsdSocketFlags.None);
        }

        public LinuxError Write(out int writeSize, ReadOnlySpan<byte> buffer)
        {
            return Send(out writeSize, buffer, BsdSocketFlags.None);
        }

        private bool CanSupportMMsgHdr(BsdMMsgHdr message)
        {
            for (int i = 0; i < message.Messages.Length; i++)
            {
                if (message.Messages[i].Name != null ||
                    message.Messages[i].Control != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static ArraySegment<byte>[] ConvertMessagesToBuffer(BsdMMsgHdr message)
        {
            int segmentCount = 0;
            int index = 0;

            foreach (BsdMsgHdr msgHeader in message.Messages)
            {
                segmentCount += msgHeader.Iov.Length;
            }

            ArraySegment<byte>[] buffers = new ArraySegment<byte>[segmentCount];

            foreach (BsdMsgHdr msgHeader in message.Messages)
            {
                foreach (byte[] iov in msgHeader.Iov)
                {
                    buffers[index++] = new ArraySegment<byte>(iov);
                }

                // Clear the length
                msgHeader.Length = 0;
            }

            return buffers;
        }

        private static void UpdateMessages(out int vlen, BsdMMsgHdr message, int transferedSize)
        {
            int bytesLeft = transferedSize;
            int index = 0;

            while (bytesLeft > 0)
            {
                // First ensure we haven't finished all buffers
                if (index >= message.Messages.Length)
                {
                    break;
                }

                BsdMsgHdr msgHeader = message.Messages[index];

                int possiblyTransferedBytes = 0;

                foreach (byte[] iov in msgHeader.Iov)
                {
                    possiblyTransferedBytes += iov.Length;
                }

                int storedBytes;

                if (bytesLeft > possiblyTransferedBytes)
                {
                    storedBytes = possiblyTransferedBytes;
                    index++;
                }
                else
                {
                    storedBytes = bytesLeft;
                }

                msgHeader.Length = (uint)storedBytes;
                bytesLeft -= storedBytes;
            }

            Debug.Assert(bytesLeft == 0);

            vlen = index + 1;
        }

        // TODO: Find a way to support passing the timeout somehow without changing the socket ReceiveTimeout.
        public LinuxError RecvMMsg(out int vlen, BsdMMsgHdr message, BsdSocketFlags flags, TimeVal timeout)
        {
            vlen = 0;

            if (message.Messages.Length == 0)
            {
                return LinuxError.SUCCESS;
            }

            if (!CanSupportMMsgHdr(message))
            {
                Logger.Warning?.Print(LogClass.ServiceBsd, "Unsupported BsdMMsgHdr");

                return LinuxError.EOPNOTSUPP;
            }

            if (message.Messages.Length == 0)
            {
                return LinuxError.SUCCESS;
            }

            try
            {
                int receiveSize = Socket.Receive(ConvertMessagesToBuffer(message), ConvertBsdSocketFlags(flags), out SocketError socketError);

                if (receiveSize > 0)
                {
                    UpdateMessages(out vlen, message, receiveSize);
                }

                return WinSockHelper.ConvertError((WsaError)socketError);
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError SendMMsg(out int vlen, BsdMMsgHdr message, BsdSocketFlags flags)
        {
            vlen = 0;

            if (message.Messages.Length == 0)
            {
                return LinuxError.SUCCESS;
            }

            if (!CanSupportMMsgHdr(message))
            {
                Logger.Warning?.Print(LogClass.ServiceBsd, "Unsupported BsdMMsgHdr");

                return LinuxError.EOPNOTSUPP;
            }

            if (message.Messages.Length == 0)
            {
                return LinuxError.SUCCESS;
            }

            try
            {
                int sendSize = Socket.Send(ConvertMessagesToBuffer(message), ConvertBsdSocketFlags(flags), out SocketError socketError);

                if (sendSize > 0)
                {
                    UpdateMessages(out vlen, message, sendSize);
                }

                return WinSockHelper.ConvertError((WsaError)socketError);
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }
    }
}
