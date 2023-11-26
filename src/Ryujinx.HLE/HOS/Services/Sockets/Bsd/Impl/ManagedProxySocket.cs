using Ryujinx.Common.Logging;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Auth;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy.Packets;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl
{
    class ManagedProxySocket : ISocket
    {
        private static readonly IPEndPoint _endpointZero = new(IPAddress.Any, 0);
        private readonly EndPoint _proxyEndpoint;

        private IProxyAuth _proxyAuth;
        private bool _ready;

        private IPEndPoint _udpEndpoint;
        private Socket _udpSocket;

        public Socket Socket { get; private set; } = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public int RefCount { get; set; }

        public AddressFamily AddressFamily { get; }
        public SocketType SocketType { get; }
        public ProtocolType ProtocolType { get; }

        public bool Blocking {
            get
            {
                return _udpEndpoint != null ? _udpSocket.Blocking : Socket.Blocking;
            }

            set
            {
                if (_udpEndpoint != null)
                {
                    _udpSocket.Blocking = value;
                }
                else
                {
                    Socket.Blocking = value;
                }
            }
        }

        public IntPtr Handle => _udpEndpoint != null ? _udpSocket.Handle : Socket.Handle;

        public IPEndPoint RemoteEndPoint { get; private set; }
        public IPEndPoint LocalEndPoint { get; private set; }

        public ManagedProxySocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, EndPoint proxyEndpoint)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
            _proxyEndpoint = proxyEndpoint;
            RefCount = 1;
        }

        private ManagedProxySocket(ManagedProxySocket oldSocket)
        {
            AddressFamily = oldSocket.AddressFamily;
            SocketType = oldSocket.SocketType;
            ProtocolType = oldSocket.ProtocolType;
            LocalEndPoint = oldSocket.LocalEndPoint;
            RemoteEndPoint = oldSocket.RemoteEndPoint;
            _proxyEndpoint = oldSocket._proxyEndpoint;
            Socket = oldSocket.Socket;
            RefCount = 1;
        }

        #region Proxy methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureVersionIsValid(byte version)
        {
            if (version != ProxyConsts.Version)
            {
                throw new InvalidDataException($"Invalid proxy version: {version}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureSuccessReply(ReplyField replyField)
        {
            if (replyField != ReplyField.Succeeded)
            {
                throw new ProxyException(replyField);
            }
        }

        private TResp SendAndReceive<TReq, TResp>(TReq request)
            where TReq: unmanaged
            where TResp: unmanaged
        {
            byte[] requestData = new byte[Marshal.SizeOf<TReq>()];
            byte[] responseData = new byte[Marshal.SizeOf<TResp>() + (_proxyAuth?.WrapperLength ?? 0)];

            MemoryMarshal.Write(requestData, request);

            int expectedSentBytes;
            int sentBytes;

            if (_proxyAuth != null)
            {
                expectedSentBytes = requestData.Length + _proxyAuth.WrapperLength;
                sentBytes = Socket.Send(_proxyAuth.Wrap(requestData));
            }
            else
            {
                expectedSentBytes = requestData.Length;
                sentBytes = Socket.Send(requestData);
            }

            if (sentBytes < expectedSentBytes)
            {
                throw new InvalidOperationException($"Failed to send the full proxy request: {sentBytes} of {expectedSentBytes} bytes");
            }

            int expectedReceivedBytes = responseData.Length;
            int receivedBytes = Socket.Receive(responseData);
            if (receivedBytes < expectedReceivedBytes)
            {
                throw new InvalidOperationException($"Proxy response size is invalid. Expected {expectedReceivedBytes} bytes, got {receivedBytes}.");
            }

            if (_proxyAuth != null)
            {
                return MemoryMarshal.Read<TResp>(_proxyAuth.Unwrap(responseData));
            }
            else
            {
                return MemoryMarshal.Read<TResp>(responseData);
            }
        }

        /// <summary>
        /// Get the authentication method chosen by the server.
        /// </summary>
        private AuthMethod GetAuthenticationMethod()
        {
            var response = SendAndReceive<MethodSelectionRequest1, MethodSelectionResponse>(new MethodSelectionRequest1
            {
                Version = ProxyConsts.Version,
                NumOfMethods = 1,
                Methods = new Array1<AuthMethod> { [0] = AuthMethod.NoAuthenticationRequired },
            });

            EnsureVersionIsValid(response.Version);

            return response.Method;
        }

        /// <summary>
        /// Authenticate to the server using a method-specific sub-negotiation.
        /// </summary>
        /// <param name="method">The authentication method to use.</param>
        /// <exception cref="NotImplementedException">The provided authentication method is not implemented.</exception>
        /// <exception cref="AuthenticationException">Authentication failed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The provided authentication method is invalid.</exception>
        private void Authenticate(AuthMethod method)
        {
            switch (method)
            {
                case AuthMethod.NoAuthenticationRequired:
                case AuthMethod.GSSAPI:
                case AuthMethod.UsernameAndPassword:
                    _proxyAuth = method.GetAuth();
                    _proxyAuth.Authenticate();
                    return;
                case AuthMethod.NoAcceptableMethods:
                    throw new AuthenticationException("No acceptable authentication method found.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }
        }

        /// <summary>
        /// Connect to a remote endpoint.
        /// </summary>
        /// <remarks>
        /// In the response from the proxy server
        /// <see cref="SocksIpv4Response.BoundAddress"/> maps to the associated IP address,
        /// while <see cref="SocksIpv4Response.BoundPort"/> maps to the port assigned to connect to the target host.
        /// </remarks>
        /// <param name="endpoint">The endpoint to connect to.</param>
        /// <returns>The endpoint the server assigned to connect to the target host.</returns>
        /// <exception cref="ProxyException">The connection to the specified endpoint failed.</exception>
        private IPEndPoint ProxyConnect(IPEndPoint endpoint)
        {
            Socket.Connect(_proxyEndpoint);
            Authenticate(GetAuthenticationMethod());

            var response = SendAndReceive<SocksIpv4Request, SocksIpv4Response>(new SocksIpv4Request
            {
                Version = ProxyConsts.Version,
                Command = ProxyCommand.Connect,
                Reserved = 0x00,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = endpoint.Address,
                DestinationPort = (ushort)endpoint.Port,
            });

            EnsureVersionIsValid(response.Version);
            EnsureSuccessReply(response.ReplyField);

            _ready = true;

            return new IPEndPoint(response.BoundAddress, response.BoundPort);
        }

        /// <summary>
        /// Listen for an incoming connection from the specified endpoint.
        /// The specified endpoint may be 0 if it's not known beforehand.
        /// </summary>
        /// <remarks>
        /// The specified endpoint is only used to restrict
        /// which clients are allowed to connect to the endpoint associated to this request.
        /// </remarks>
        /// <param name="endpoint">The endpoint of the incoming connection.</param>
        /// <returns>The endpoint the server uses to listen for an incoming connection.</returns>
        private IPEndPoint ProxyBind(IPEndPoint endpoint)
        {
            Socket.Connect(_proxyEndpoint);
            Authenticate(GetAuthenticationMethod());

            var response = SendAndReceive<SocksIpv4Request, SocksIpv4Response>(new SocksIpv4Request
            {
                Version = ProxyConsts.Version,
                Command = ProxyCommand.Bind,
                Reserved = 0x00,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = endpoint.Address,
                DestinationPort = (ushort)endpoint.Port,
            });

            EnsureVersionIsValid(response.Version);
            EnsureSuccessReply(response.ReplyField);

            return new IPEndPoint(response.BoundAddress, response.BoundPort);
        }

        /// <summary>
        /// Get the anticipated incoming connection.
        /// </summary>
        /// <returns>The endpoint of the incoming connection.</returns>
        /// <exception cref="InvalidOperationException">The response length is too small.</exception>
        private IPEndPoint WaitForIncomingConnection()
        {
            byte[] responseData = new byte[Marshal.SizeOf<SocksIpv4Response>() + _proxyAuth.WrapperLength];
            int expectedReceivedBytes = responseData.Length;
            int receivedBytes = Socket.Receive(responseData);
            if (receivedBytes < expectedReceivedBytes)
            {
                throw new InvalidOperationException($"Proxy response size is invalid. Expected {expectedReceivedBytes} bytes, got {receivedBytes}.");
            }

            var response = MemoryMarshal.Read<SocksIpv4Response>(_proxyAuth.Unwrap(responseData));

            EnsureVersionIsValid(response.Version);
            EnsureSuccessReply(response.ReplyField);

            _ready = true;

            return new IPEndPoint(response.BoundAddress, response.BoundPort);
        }

        /// <summary>
        /// Create a UDP relay.
        /// The specified endpoint may be 0 if it's not known beforehand.
        /// </summary>
        /// <remarks>
        /// The specified endpoint is only used to restrict which clients are allowed to use the relay.
        /// </remarks>
        /// <param name="endpoint">The endpoint used to send UDP datagrams to the relay.</param>
        private void AssociateUdp(IPEndPoint endpoint)
        {
            Socket.Connect(_proxyEndpoint);
            Authenticate(GetAuthenticationMethod());

            var response = SendAndReceive<SocksIpv4Request, SocksIpv4Response>(new SocksIpv4Request
            {
                Version = ProxyConsts.Version,
                Command = ProxyCommand.UdpAssociate,
                Reserved = 0x00,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = endpoint.Address,
                DestinationPort = (ushort)endpoint.Port,
            });

            EnsureVersionIsValid(response.Version);
            EnsureSuccessReply(response.ReplyField);

            _udpEndpoint = new IPEndPoint(response.BoundAddress, response.BoundPort);
            _udpSocket = new Socket(AddressFamily, SocketType, ProtocolType);
            _udpSocket.Blocking = Socket.Blocking;
            _udpSocket.Bind(endpoint);

            _ready = true;
        }

        #endregion

        public LinuxError Send(out int sendSize, ReadOnlySpan<byte> buffer, BsdSocketFlags flags)
        {
            if (!_ready)
            {
                throw new InvalidOperationException("No connection has been established. Issue a proxy command before sending data.");
            }

            if (_udpEndpoint != null)
            {
                throw new InvalidOperationException($"UDP packets can only be sent using {nameof(SendTo)}.");
            }

            try
            {
                sendSize = Socket.Send(_proxyAuth.Wrap(buffer), ManagedSocket.ConvertBsdSocketFlags(flags)) - _proxyAuth.WrapperLength;

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
            if (!_ready || _udpEndpoint == null)
            {
                throw new InvalidOperationException("No connection has been established. Issue a proxy command before sending data.");
            }

            byte[] data = new byte[Marshal.SizeOf<SocksUdpIpv4Header>() + buffer.Length];
            var header = new SocksUdpIpv4Header
            {
                Reserved = 0,
                Fragment = 0,
                AddressType = AddressType.Ipv4Address,
                DestinationAddress = remoteEndPoint.Address,
                DestinationPort = (ushort)remoteEndPoint.Port,
            };

            MemoryMarshal.Write(data, header);
            buffer[..size].CopyTo(data.AsSpan()[Marshal.SizeOf<SocksUdpIpv4Header>()..]);

            try
            {
                sendSize = _udpSocket.SendTo(_proxyAuth.Wrap(data), _udpEndpoint) -
                           Marshal.SizeOf<SocksUdpIpv4Header>() - _proxyAuth.WrapperLength;

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                sendSize = -1;

                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError Receive(out int receiveSize, Span<byte> buffer, BsdSocketFlags flags)
        {
            LinuxError result;
            bool shouldBlockAfterOperation = false;

            if (Blocking && flags.HasFlag(BsdSocketFlags.DontWait))
            {
                Blocking = false;
                shouldBlockAfterOperation = true;
            }

            byte[] data = new byte[buffer.Length + _proxyAuth.WrapperLength];

            try
            {
                receiveSize = Socket.Receive(data) - _proxyAuth.WrapperLength;
                _proxyAuth.Unwrap(data).CopyTo(buffer);

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
            LinuxError result;
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            bool shouldBlockAfterOperation = false;
            
            byte[] data = new byte[size + _proxyAuth.WrapperLength + Marshal.SizeOf<SocksUdpIpv4Header>()];
            EndPoint udpEndpoint = _udpEndpoint;
            
            if (_udpSocket is not { IsBound: true })
            {
                receiveSize = -1;

                return LinuxError.EOPNOTSUPP;
            }

            if (Blocking && flags.HasFlag(BsdSocketFlags.DontWait))
            {
                Blocking = false;
                shouldBlockAfterOperation = true;
            }

            try
            {
                receiveSize = _udpSocket.ReceiveFrom(data, ref udpEndpoint) - _proxyAuth.WrapperLength - Marshal.SizeOf<SocksUdpIpv4Header>();
                data = _proxyAuth.Unwrap(data).ToArray();

                var header = MemoryMarshal.Read<SocksUdpIpv4Header>(data);
            
                // An implementation that doesn't support fragmentation must drop any fragmented datagram
                // TODO: Implement support for fragmentation
                if (header.Fragment != 0)
                {
                    if (shouldBlockAfterOperation)
                    {
                        Blocking = true;
                    }

                    receiveSize = -1;
                    
                    return LinuxError.EOPNOTSUPP;
                }

                remoteEndPoint = new IPEndPoint(header.DestinationAddress, header.DestinationPort);
                data.AsSpan()[Marshal.SizeOf<SocksUdpIpv4Header>()..].CopyTo(buffer[..size]);

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

        public LinuxError Bind(IPEndPoint localEndPoint)
        {
            switch (ProtocolType)
            {
                case ProtocolType.Tcp:
                    try
                    {
                        Socket.Bind(localEndPoint);
                        LocalEndPoint = ProxyBind(_endpointZero);

                        return LinuxError.SUCCESS;
                    }
                    catch (SocketException exception)
                    {
                        return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
                    }
                case ProtocolType.Udp:
                    try
                    {
                        AssociateUdp(localEndPoint);
                        LocalEndPoint = localEndPoint;

                        return LinuxError.SUCCESS;
                    }
                    catch (SocketException exception)
                    {
                        return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
                    }
                default:
                    return LinuxError.EOPNOTSUPP;
            }
        }

        public LinuxError Listen(int backlog)
        {
            if (backlog > 1)
            {
                return LinuxError.EOPNOTSUPP;
            }

            return LinuxError.SUCCESS;
        }

        public LinuxError Accept(out ISocket newSocket)
        {
            try
            {
                RemoteEndPoint = WaitForIncomingConnection();
                newSocket = new ManagedProxySocket(this);
                LocalEndPoint = null;
                RemoteEndPoint = null;
                _ready = false;
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                newSocket = null;

                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public LinuxError Connect(IPEndPoint remoteEndPoint)
        {
            try
            {
                LocalEndPoint = ProxyConnect(remoteEndPoint);
                RemoteEndPoint = remoteEndPoint;

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

        public bool Poll(int microSeconds, SelectMode mode)
        {
            if (_udpEndpoint != null)
            {
                return _udpSocket.Poll(microSeconds, mode);
            }
            else
            {
                return Socket.Poll(microSeconds, mode);
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

                if (_udpEndpoint != null)
                {
                    _udpSocket.GetSocketOption(level, optionName, tempOptionValue);
                }
                else
                {
                    Socket.GetSocketOption(level, optionName, tempOptionValue);
                }

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

                    if (_udpEndpoint != null)
                    {
                        _udpSocket.SetSocketOption(level, SocketOptionName.Linger, new LingerOption(value != 0, value2));
                    }
                    else
                    {
                        Socket.SetSocketOption(level, SocketOptionName.Linger, new LingerOption(value != 0, value2));
                    }
                }
                else
                {
                    if (_udpEndpoint != null)
                    {
                        _udpSocket.SetSocketOption(level, optionName, value);
                    }
                    else
                    {
                        Socket.SetSocketOption(level, optionName, value);
                    }
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

        public LinuxError Shutdown(BsdSocketShutdownFlags how)
        {
            try
            {
                _udpSocket?.Shutdown((SocketShutdown)how);
                Socket.Shutdown((SocketShutdown)how);

                return LinuxError.SUCCESS;
            }
            catch (SocketException exception)
            {
                return WinSockHelper.ConvertError((WsaError)exception.ErrorCode);
            }
        }

        public void Disconnect()
        {
            Socket.Disconnect(true);
            _udpEndpoint = null;
            RemoteEndPoint = null;
            LocalEndPoint = _endpointZero;
            _ready = false;
        }

        public void Close()
        {
            _udpSocket?.Close();
            Socket.Close();
        }

        public void Dispose()
        {
            _udpSocket?.Close();
            _udpSocket?.Dispose();
            Socket.Close();
            Socket.Dispose();
        }

        public LinuxError RecvMMsg(out int vlen, BsdMMsgHdr message, BsdSocketFlags flags, TimeVal timeout)
        {
            throw new NotImplementedException();
        }

        public LinuxError SendMMsg(out int vlen, BsdMMsgHdr message, BsdSocketFlags flags)
        {
            throw new NotImplementedException();
        }
    }
}
