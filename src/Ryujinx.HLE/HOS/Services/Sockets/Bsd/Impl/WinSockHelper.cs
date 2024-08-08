using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd.Types;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Ryujinx.HLE.HOS.Services.Sockets.Bsd.Impl
{
    static class WinSockHelper
    {
        private static readonly Dictionary<WsaError, LinuxError> _errorMap = new()
        {
            // WSAEINTR
            { WsaError.WSAEINTR,           LinuxError.EINTR },
            // WSAEWOULDBLOCK
            { WsaError.WSAEWOULDBLOCK,     LinuxError.EWOULDBLOCK },
            // WSAEINPROGRESS
            { WsaError.WSAEINPROGRESS,     LinuxError.EINPROGRESS },
            // WSAEALREADY
            { WsaError.WSAEALREADY,        LinuxError.EALREADY },
            // WSAENOTSOCK
            { WsaError.WSAENOTSOCK,        LinuxError.ENOTSOCK },
            // WSAEDESTADDRREQ
            { WsaError.WSAEDESTADDRREQ,    LinuxError.EDESTADDRREQ },
            // WSAEMSGSIZE
            { WsaError.WSAEMSGSIZE,        LinuxError.EMSGSIZE },
            // WSAEPROTOTYPE
            { WsaError.WSAEPROTOTYPE,      LinuxError.EPROTOTYPE },
            // WSAENOPROTOOPT
            { WsaError.WSAENOPROTOOPT,     LinuxError.ENOPROTOOPT },
            // WSAEPROTONOSUPPORT
            { WsaError.WSAEPROTONOSUPPORT, LinuxError.EPROTONOSUPPORT },
            // WSAESOCKTNOSUPPORT
            { WsaError.WSAESOCKTNOSUPPORT, LinuxError.ESOCKTNOSUPPORT },
            // WSAEOPNOTSUPP
            { WsaError.WSAEOPNOTSUPP,      LinuxError.EOPNOTSUPP },
            // WSAEPFNOSUPPORT
            { WsaError.WSAEPFNOSUPPORT,    LinuxError.EPFNOSUPPORT },
            // WSAEAFNOSUPPORT
            { WsaError.WSAEAFNOSUPPORT,    LinuxError.EAFNOSUPPORT },
            // WSAEADDRINUSE
            { WsaError.WSAEADDRINUSE,      LinuxError.EADDRINUSE },
            // WSAEADDRNOTAVAIL
            { WsaError.WSAEADDRNOTAVAIL,   LinuxError.EADDRNOTAVAIL },
            // WSAENETDOWN
            { WsaError.WSAENETDOWN,        LinuxError.ENETDOWN },
            // WSAENETUNREACH
            { WsaError.WSAENETUNREACH,     LinuxError.ENETUNREACH },
            // WSAENETRESET
            { WsaError.WSAENETRESET,       LinuxError.ENETRESET },
            // WSAECONNABORTED
            { WsaError.WSAECONNABORTED,    LinuxError.ECONNABORTED },
            // WSAECONNRESET
            { WsaError.WSAECONNRESET,      LinuxError.ECONNRESET },
            // WSAENOBUFS
            { WsaError.WSAENOBUFS,         LinuxError.ENOBUFS },
            // WSAEISCONN
            { WsaError.WSAEISCONN,         LinuxError.EISCONN },
            // WSAENOTCONN
            { WsaError.WSAENOTCONN,        LinuxError.ENOTCONN },
            // WSAESHUTDOWN
            { WsaError.WSAESHUTDOWN,       LinuxError.ESHUTDOWN },
            // WSAETOOMANYREFS
            { WsaError.WSAETOOMANYREFS,    LinuxError.ETOOMANYREFS },
            // WSAETIMEDOUT
            { WsaError.WSAETIMEDOUT,       LinuxError.ETIMEDOUT },
            // WSAECONNREFUSED
            { WsaError.WSAECONNREFUSED,    LinuxError.ECONNREFUSED },
            // WSAELOOP
            { WsaError.WSAELOOP,           LinuxError.ELOOP },
            // WSAENAMETOOLONG
            { WsaError.WSAENAMETOOLONG,    LinuxError.ENAMETOOLONG },
            // WSAEHOSTDOWN
            { WsaError.WSAEHOSTDOWN,       LinuxError.EHOSTDOWN },
            // WSAEHOSTUNREACH
            { WsaError.WSAEHOSTUNREACH,    LinuxError.EHOSTUNREACH },
            // WSAENOTEMPTY
            { WsaError.WSAENOTEMPTY,       LinuxError.ENOTEMPTY },
            // WSAEUSERS
            { WsaError.WSAEUSERS,          LinuxError.EUSERS },
            // WSAEDQUOT
            { WsaError.WSAEDQUOT,          LinuxError.EDQUOT },
            // WSAESTALE
            { WsaError.WSAESTALE,          LinuxError.ESTALE },
            // WSAEREMOTE
            { WsaError.WSAEREMOTE,         LinuxError.EREMOTE },
            // WSAEINVAL
            { WsaError.WSAEINVAL,          LinuxError.EINVAL },
            // WSAEFAULT
            { WsaError.WSAEFAULT,          LinuxError.EFAULT },
            // NOERROR
            { 0, 0 },
        };

        private static readonly Dictionary<int, LinuxError> _errorMapMacOs = new()
        {
            { 35, LinuxError.EAGAIN },
            { 11, LinuxError.EDEADLOCK },
            { 91, LinuxError.ENOMSG },
            { 90, LinuxError.EIDRM },
            { 77, LinuxError.ENOLCK },
            { 70, LinuxError.ESTALE },
            { 36, LinuxError.EINPROGRESS },
            { 37, LinuxError.EALREADY },
            { 38, LinuxError.ENOTSOCK },
            { 39, LinuxError.EDESTADDRREQ },
            { 40, LinuxError.EMSGSIZE },
            { 41, LinuxError.EPROTOTYPE },
            { 42, LinuxError.ENOPROTOOPT },
            { 43, LinuxError.EPROTONOSUPPORT },
            { 44, LinuxError.ESOCKTNOSUPPORT },
            { 45, LinuxError.EOPNOTSUPP },
            { 46, LinuxError.EPFNOSUPPORT },
            { 47, LinuxError.EAFNOSUPPORT },
            { 48, LinuxError.EADDRINUSE },
            { 49, LinuxError.EADDRNOTAVAIL },
            { 50, LinuxError.ENETDOWN },
            { 51, LinuxError.ENETUNREACH },
            { 52, LinuxError.ENETRESET },
            { 53, LinuxError.ECONNABORTED },
            { 54, LinuxError.ECONNRESET },
            { 55, LinuxError.ENOBUFS },
            { 56, LinuxError.EISCONN },
            { 57, LinuxError.ENOTCONN },
            { 58, LinuxError.ESHUTDOWN },
            { 60, LinuxError.ETIMEDOUT },
            { 61, LinuxError.ECONNREFUSED },
            { 64, LinuxError.EHOSTDOWN },
            { 65, LinuxError.EHOSTUNREACH },
            { 68, LinuxError.EUSERS },
            { 62, LinuxError.ELOOP },
            { 63, LinuxError.ENAMETOOLONG },
            { 66, LinuxError.ENOTEMPTY },
            { 69, LinuxError.EDQUOT },
            { 71, LinuxError.EREMOTE },
            { 78, LinuxError.ENOSYS },
            { 59, LinuxError.ETOOMANYREFS },
            { 92, LinuxError.EILSEQ },
            { 89, LinuxError.ECANCELED },
            { 84, LinuxError.EOVERFLOW },
        };

        private static readonly Dictionary<BsdSocketOption, SocketOptionName> _soSocketOptionMap = new()
        {
            { BsdSocketOption.SoDebug,       SocketOptionName.Debug },
            { BsdSocketOption.SoReuseAddr,   SocketOptionName.ReuseAddress },
            { BsdSocketOption.SoKeepAlive,   SocketOptionName.KeepAlive },
            { BsdSocketOption.SoDontRoute,   SocketOptionName.DontRoute },
            { BsdSocketOption.SoBroadcast,   SocketOptionName.Broadcast },
            { BsdSocketOption.SoUseLoopBack, SocketOptionName.UseLoopback },
            { BsdSocketOption.SoLinger,      SocketOptionName.Linger },
            { BsdSocketOption.SoOobInline,   SocketOptionName.OutOfBandInline },
            { BsdSocketOption.SoReusePort,   SocketOptionName.ReuseAddress },
            { BsdSocketOption.SoSndBuf,      SocketOptionName.SendBuffer },
            { BsdSocketOption.SoRcvBuf,      SocketOptionName.ReceiveBuffer },
            { BsdSocketOption.SoSndLoWat,    SocketOptionName.SendLowWater },
            { BsdSocketOption.SoRcvLoWat,    SocketOptionName.ReceiveLowWater },
            { BsdSocketOption.SoSndTimeo,    SocketOptionName.SendTimeout },
            { BsdSocketOption.SoRcvTimeo,    SocketOptionName.ReceiveTimeout },
            { BsdSocketOption.SoError,       SocketOptionName.Error },
            { BsdSocketOption.SoType,        SocketOptionName.Type },
        };

        private static readonly Dictionary<BsdSocketOption, SocketOptionName> _ipSocketOptionMap = new()
        {
            { BsdSocketOption.IpOptions,              SocketOptionName.IPOptions },
            { BsdSocketOption.IpHdrIncl,              SocketOptionName.HeaderIncluded },
            { BsdSocketOption.IpTtl,                  SocketOptionName.IpTimeToLive },
            { BsdSocketOption.IpMulticastIf,          SocketOptionName.MulticastInterface },
            { BsdSocketOption.IpMulticastTtl,         SocketOptionName.MulticastTimeToLive },
            { BsdSocketOption.IpMulticastLoop,        SocketOptionName.MulticastLoopback },
            { BsdSocketOption.IpAddMembership,        SocketOptionName.AddMembership },
            { BsdSocketOption.IpDropMembership,       SocketOptionName.DropMembership },
            { BsdSocketOption.IpDontFrag,             SocketOptionName.DontFragment },
            { BsdSocketOption.IpAddSourceMembership,  SocketOptionName.AddSourceMembership },
            { BsdSocketOption.IpDropSourceMembership, SocketOptionName.DropSourceMembership },
        };

        private static readonly Dictionary<BsdSocketOption, SocketOptionName> _tcpSocketOptionMap = new()
        {
            { BsdSocketOption.TcpNoDelay,   SocketOptionName.NoDelay },
            { BsdSocketOption.TcpKeepIdle,  SocketOptionName.TcpKeepAliveTime },
            { BsdSocketOption.TcpKeepIntvl, SocketOptionName.TcpKeepAliveInterval },
            { BsdSocketOption.TcpKeepCnt,   SocketOptionName.TcpKeepAliveRetryCount },
        };

        [Flags]
        private enum OptionDir
        {
            Get = 1 << 0,
            Set = 1 << 1,
            GetSet = Get | Set,
        }

        private static readonly Dictionary<BsdSocketOption, OptionDir> _validSoSocketOptionMap = new()
        {
            { BsdSocketOption.SoDebug,         OptionDir.GetSet },
            { BsdSocketOption.SoAcceptConn,    OptionDir.Get },
            { BsdSocketOption.SoReuseAddr,     OptionDir.GetSet },
            { BsdSocketOption.SoKeepAlive,     OptionDir.GetSet },
            { BsdSocketOption.SoDontRoute,     OptionDir.GetSet },
            { BsdSocketOption.SoBroadcast,     OptionDir.GetSet },
            { BsdSocketOption.SoUseLoopBack,   OptionDir.GetSet },
            { BsdSocketOption.SoLinger,        OptionDir.GetSet },
            { BsdSocketOption.SoOobInline,     OptionDir.GetSet },
            { BsdSocketOption.SoReusePort,     OptionDir.GetSet },
            { BsdSocketOption.SoTimestamp,     OptionDir.GetSet },
            { BsdSocketOption.SoNoSigpipe,     OptionDir.GetSet },
            { BsdSocketOption.SoAcceptFilter,  OptionDir.GetSet },
            { BsdSocketOption.SoSndBuf,        OptionDir.GetSet },
            { BsdSocketOption.SoRcvBuf,        OptionDir.GetSet },
            { BsdSocketOption.SoSndLoWat,      OptionDir.GetSet },
            { BsdSocketOption.SoRcvLoWat,      OptionDir.GetSet },
            { BsdSocketOption.SoSndTimeo,      OptionDir.GetSet },
            { BsdSocketOption.SoRcvTimeo,      OptionDir.GetSet },
            { BsdSocketOption.SoError,         OptionDir.Get },
            { BsdSocketOption.SoType,          OptionDir.Get },
            { BsdSocketOption.SoLabel,         OptionDir.Get },
            { BsdSocketOption.SoPeerLabel,     OptionDir.Get },
            { BsdSocketOption.SoListenQLimit,  OptionDir.Get },
            { BsdSocketOption.SoListenQLen,    OptionDir.Get },
            { BsdSocketOption.SoListenIncQLen, OptionDir.Get },
            { BsdSocketOption.SoSetFib,        OptionDir.Set },
            { BsdSocketOption.SoUserCookie,    OptionDir.Set },
            { BsdSocketOption.SoProtocol,      OptionDir.Get },
            { BsdSocketOption.SoBinTime,       OptionDir.GetSet },
            { BsdSocketOption.SoNoOffload,     OptionDir.Set },
            { BsdSocketOption.SoNoDdp,         OptionDir.Set },
            { BsdSocketOption.SoReusePortLb,   OptionDir.GetSet },
        };

        private static readonly Dictionary<BsdSocketOption, OptionDir> _validIpSocketOptionMap = new()
        {
            { BsdSocketOption.IpOptions,              OptionDir.GetSet },
            { BsdSocketOption.IpHdrIncl,              OptionDir.GetSet },
            { BsdSocketOption.IpTos,                  OptionDir.GetSet },
            { BsdSocketOption.IpTtl,                  OptionDir.GetSet },
            { BsdSocketOption.IpRecvOpts,             OptionDir.GetSet },
            { BsdSocketOption.IpRecvRetOpts,          OptionDir.GetSet },
            { BsdSocketOption.IpRecvDstAddr,          OptionDir.GetSet },
            { BsdSocketOption.IpRetOpts,              OptionDir.GetSet },
            { BsdSocketOption.IpMulticastIf,          OptionDir.GetSet },
            { BsdSocketOption.IpMulticastTtl,         OptionDir.GetSet },
            { BsdSocketOption.IpMulticastLoop,        OptionDir.GetSet },
            { BsdSocketOption.IpAddMembership,        OptionDir.GetSet },
            { BsdSocketOption.IpDropMembership,       OptionDir.GetSet },
            { BsdSocketOption.IpMulticastVif,         OptionDir.GetSet },
            { BsdSocketOption.IpRsvpOn,               OptionDir.GetSet },
            { BsdSocketOption.IpRsvpOff,              OptionDir.GetSet },
            { BsdSocketOption.IpRsvpVifOn,            OptionDir.GetSet },
            { BsdSocketOption.IpRsvpVifOff,           OptionDir.GetSet },
            { BsdSocketOption.IpPortRange,            OptionDir.GetSet },
            { BsdSocketOption.IpRecvIf,               OptionDir.GetSet },
            { BsdSocketOption.IpIpsecPolicy,          OptionDir.GetSet },
            { BsdSocketOption.IpOnesBcast,            OptionDir.GetSet },
            { BsdSocketOption.IpBindany,              OptionDir.GetSet },
            { BsdSocketOption.IpBindMulti,            OptionDir.GetSet },
            { BsdSocketOption.IpRssListenBucket,      OptionDir.GetSet },
            { BsdSocketOption.IpOrigDstAddr,          OptionDir.GetSet },
            { BsdSocketOption.IpRecvTtl,              OptionDir.GetSet },
            { BsdSocketOption.IpMinTtl,               OptionDir.GetSet },
            { BsdSocketOption.IpDontFrag,             OptionDir.GetSet },
            { BsdSocketOption.IpRecvTos,              OptionDir.GetSet },
            { BsdSocketOption.IpAddSourceMembership,  OptionDir.GetSet },
            { BsdSocketOption.IpDropSourceMembership, OptionDir.GetSet },
            { BsdSocketOption.IpBlockSource,          OptionDir.GetSet },
            { BsdSocketOption.IpUnblockSource,        OptionDir.GetSet },
        };

        private static readonly Dictionary<BsdSocketOption, OptionDir> _validTcpSocketOptionMap = new()
        {
            { BsdSocketOption.TcpNoDelay,    OptionDir.GetSet },
            { BsdSocketOption.TcpMaxSeg,     OptionDir.GetSet },
            { BsdSocketOption.TcpNoPush,     OptionDir.GetSet },
            { BsdSocketOption.TcpNoOpt,      OptionDir.GetSet },
            { BsdSocketOption.TcpMd5Sig,     OptionDir.GetSet },
            { BsdSocketOption.TcpInfo,       OptionDir.GetSet },
            { BsdSocketOption.TcpCongestion, OptionDir.GetSet },
            { BsdSocketOption.TcpKeepInit,   OptionDir.GetSet },
            { BsdSocketOption.TcpKeepIdle,   OptionDir.GetSet },
            { BsdSocketOption.TcpKeepIntvl,  OptionDir.GetSet },
            { BsdSocketOption.TcpKeepCnt,    OptionDir.GetSet },
        };

        public static LinuxError ConvertError(WsaError errorCode)
        {
            if (OperatingSystem.IsMacOS())
            {
                if (_errorMapMacOs.TryGetValue((int)errorCode, out LinuxError errno))
                {
                    return errno;
                }
            }
            else
            {
                if (_errorMap.TryGetValue(errorCode, out LinuxError errno))
                {
                    return errno;
                }
            }

            return (LinuxError)errorCode;
        }

        public static bool TryConvertSocketOption(BsdSocketOption option, SocketOptionLevel level, out SocketOptionName name)
        {
            var table = level switch
            {
                SocketOptionLevel.Socket => _soSocketOptionMap,
                SocketOptionLevel.IP => _ipSocketOptionMap,
                SocketOptionLevel.Tcp => _tcpSocketOptionMap,
                _ => null,
            };

            if (table == null)
            {
                name = default;
                return false;
            }

            return table.TryGetValue(option, out name);
        }

        public static LinuxError ValidateSocketOption(BsdSocketOption option, SocketOptionLevel level, bool write)
        {
            var table = level switch
            {
                SocketOptionLevel.Socket => _validSoSocketOptionMap,
                SocketOptionLevel.IP => _validIpSocketOptionMap,
                SocketOptionLevel.Tcp => _validTcpSocketOptionMap,
                _ => null,
            };

            OptionDir dir = write ? OptionDir.Set : OptionDir.Get;

            if (table == null || !table.TryGetValue(option, out OptionDir validDir))
            {
                return LinuxError.ENOPROTOOPT;
            }
            else if ((validDir & dir) != dir)
            {
                return LinuxError.EOPNOTSUPP;
            }

            return LinuxError.SUCCESS;
        }

        public static SocketFlags ConvertBsdSocketFlags(BsdSocketFlags bsdSocketFlags)
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
    }
}
