using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Proxy
{
    public static class ProxyManager
    {
        private static readonly Dictionary<string, EndPoint> _proxyEndpoints = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetKey(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            return string.Join("-", new[] { (int)addressFamily, (int)socketType, (int)protocolType });
        }

        internal static ISocket GetSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            if (_proxyEndpoints.TryGetValue(GetKey(addressFamily, socketType, protocolType), out EndPoint endPoint))
            {
                return new ManagedProxySocket(addressFamily, socketType, protocolType, endPoint);
            }

            return new ManagedSocket(addressFamily, socketType, protocolType);
        }

        public static void AddOrUpdate(EndPoint endPoint,
            AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            _proxyEndpoints[GetKey(addressFamily, socketType, protocolType)] = endPoint;
        }

        public static void AddOrUpdate(IPAddress address, int port,
            AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            _proxyEndpoints[GetKey(addressFamily, socketType, protocolType)] = new IPEndPoint(address, port);
        }

        public static void AddOrUpdate(string host, int port,
            AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            _proxyEndpoints[GetKey(addressFamily, socketType, protocolType)] = new DnsEndPoint(host, port);
        }

        public static bool Remove(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            return _proxyEndpoints.Remove(GetKey(addressFamily, socketType, protocolType));
        }
    }
}
