using Ryujinx.Common.Logging;
using Ryujinx.Cpu;
using Ryujinx.HLE.HOS.Services.Sockets.Nsd.Manager;
using Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Proxy;
using Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres.Types;
using Ryujinx.Memory;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Sockets.Sfdnsres
{
    [Service("sfdnsres")]
    class IResolver : IpcService
    {
        public IResolver(ServiceCtx context)
        {
            DnsMitmResolver.Instance.ReloadEntries(context);
        }

        [CommandCmif(0)]
        // SetDnsAddressesPrivateRequest(u32, buffer<unknown, 5, 0>)
        public ResultCode SetDnsAddressesPrivateRequest(ServiceCtx context)
        {
            uint cancelHandleRequest = context.RequestData.ReadUInt32();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong bufferPosition = context.Request.SendBuff[0].Position;
            ulong bufferSize = context.Request.SendBuff[0].Size;
#pragma warning restore IDE0059

            // TODO: This is stubbed in 2.0.0+, reverse 1.0.0 version for the sake of completeness.
            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.NotAllocated;
        }

        [CommandCmif(1)]
        // GetDnsAddressPrivateRequest(u32) -> buffer<unknown, 6, 0>
        public ResultCode GetDnsAddressPrivateRequest(ServiceCtx context)
        {
            uint cancelHandleRequest = context.RequestData.ReadUInt32();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong bufferPosition = context.Request.ReceiveBuff[0].Position;
            ulong bufferSize = context.Request.ReceiveBuff[0].Size;
#pragma warning restore IDE0059

            // TODO: This is stubbed in 2.0.0+, reverse 1.0.0 version for the sake of completeness.
            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.NotAllocated;
        }

        [CommandCmif(2)]
        // GetHostByNameRequest(u8, u32, u64, pid, buffer<unknown, 5, 0>) -> (u32, u32, u32, buffer<unknown, 6, 0>)
        public ResultCode GetHostByNameRequest(ServiceCtx context)
        {
            ulong inputBufferPosition = context.Request.SendBuff[0].Position;
            ulong inputBufferSize = context.Request.SendBuff[0].Size;

            ulong outputBufferPosition = context.Request.ReceiveBuff[0].Position;
            ulong outputBufferSize = context.Request.ReceiveBuff[0].Size;

            return GetHostByNameRequestImpl(context, inputBufferPosition, inputBufferSize, outputBufferPosition, outputBufferSize, false, 0, 0);
        }

        [CommandCmif(3)]
        // GetHostByAddrRequest(u32, u32, u32, u64, pid, buffer<unknown, 5, 0>) -> (u32, u32, u32, buffer<unknown, 6, 0>)
        public ResultCode GetHostByAddrRequest(ServiceCtx context)
        {
            ulong inputBufferPosition = context.Request.SendBuff[0].Position;
            ulong inputBufferSize = context.Request.SendBuff[0].Size;

            ulong outputBufferPosition = context.Request.ReceiveBuff[0].Position;
            ulong outputBufferSize = context.Request.ReceiveBuff[0].Size;

            return GetHostByAddrRequestImpl(context, inputBufferPosition, inputBufferSize, outputBufferPosition, outputBufferSize, false, 0, 0);
        }

        [CommandCmif(4)]
        // GetHostStringErrorRequest(u32) -> buffer<unknown, 6, 0>
        public ResultCode GetHostStringErrorRequest(ServiceCtx context)
        {
            ResultCode resultCode = ResultCode.NotAllocated;
            NetDbError errorCode = (NetDbError)context.RequestData.ReadInt32();

            string errorString = errorCode switch
            {
                NetDbError.Success => "Resolver Error 0 (no error)",
                NetDbError.HostNotFound => "Unknown host",
                NetDbError.TryAgain => "Host name lookup failure",
                NetDbError.NoRecovery => "Unknown server error",
                NetDbError.NoData => "No address associated with name",
                _ => (errorCode <= NetDbError.Internal) ? "Resolver internal error" : "Unknown resolver error",
            };

            ulong bufferPosition = context.Request.ReceiveBuff[0].Position;
            ulong bufferSize = context.Request.ReceiveBuff[0].Size;

            if ((ulong)(errorString.Length + 1) <= bufferSize)
            {
                context.Memory.Write(bufferPosition, Encoding.ASCII.GetBytes(errorString + '\0'));

                resultCode = ResultCode.Success;
            }

            return resultCode;
        }

        [CommandCmif(5)]
        // GetGaiStringErrorRequest(u32) -> buffer<byte, 6, 0>
        public ResultCode GetGaiStringErrorRequest(ServiceCtx context)
        {
            ResultCode resultCode = ResultCode.NotAllocated;
            GaiError errorCode = (GaiError)context.RequestData.ReadInt32();

            if (errorCode > GaiError.Max)
            {
                errorCode = GaiError.Max;
            }

            string errorString = errorCode switch
            {
                GaiError.AddressFamily => "Address family for hostname not supported",
                GaiError.Again => "Temporary failure in name resolution",
                GaiError.BadFlags => "Invalid value for ai_flags",
                GaiError.Fail => "Non-recoverable failure in name resolution",
                GaiError.Family => "ai_family not supported",
                GaiError.Memory => "Memory allocation failure",
                GaiError.NoData => "No address associated with hostname",
                GaiError.NoName => "hostname nor servname provided, or not known",
                GaiError.Service => "servname not supported for ai_socktype",
                GaiError.SocketType => "ai_socktype not supported",
                GaiError.System => "System error returned in errno",
                GaiError.BadHints => "Invalid value for hints",
                GaiError.Protocol => "Resolved protocol is unknown",
                GaiError.Overflow => "Argument buffer overflow",
                GaiError.Max => "Unknown error",
                _ => "Success",
            };

            ulong bufferPosition = context.Request.ReceiveBuff[0].Position;
            ulong bufferSize = context.Request.ReceiveBuff[0].Size;

            if ((ulong)(errorString.Length + 1) <= bufferSize)
            {
                context.Memory.Write(bufferPosition, Encoding.ASCII.GetBytes(errorString + '\0'));

                resultCode = ResultCode.Success;
            }

            return resultCode;
        }

        [CommandCmif(6)]
        // GetAddrInfoRequest(bool enable_nsd_resolve, u32, u64 pid_placeholder, pid, buffer<i8, 5, 0> host, buffer<i8, 5, 0> service, buffer<packed_addrinfo, 5, 0> hints) -> (i32 ret, u32 bsd_errno, u32 packed_addrinfo_size, buffer<packed_addrinfo, 6, 0> response)
        public ResultCode GetAddrInfoRequest(ServiceCtx context)
        {
            ulong responseBufferPosition = context.Request.ReceiveBuff[0].Position;
            ulong responseBufferSize = context.Request.ReceiveBuff[0].Size;

            return GetAddrInfoRequestImpl(context, responseBufferPosition, responseBufferSize, false, 0, 0);
        }

        [CommandCmif(7)]
        // GetNameInfoRequest
        public ResultCode GetNameInfoRequest(ServiceCtx context)
        {
            return GetNameInfoRequestImpl(context, false);
        }

        [CommandCmif(8)]
        // GetCancelHandleRequest(u64, pid) -> u32
        public ResultCode GetCancelHandleRequest(ServiceCtx context)
        {
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong pidPlaceHolder = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059
            uint cancelHandleRequest = 0;

            context.ResponseData.Write(cancelHandleRequest);

            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.Success;
        }

        [CommandCmif(9)]
        // CancelRequest(u32, u64, pid)
        public ResultCode CancelRequest(ServiceCtx context)
        {
            uint cancelHandleRequest = context.RequestData.ReadUInt32();
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong pidPlaceHolder = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059

            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { cancelHandleRequest });

            return ResultCode.Success;
        }

        [CommandCmif(10)] // 5.0.0+
        // GetHostByNameRequestWithOptions(u8, u32, u64, pid, buffer<unknown, 21, 0>, buffer<unknown, 21, 0>) -> (u32, u32, u32, buffer<unknown, 22, 0>)
        public ResultCode GetHostByNameRequestWithOptions(ServiceCtx context)
        {
            (ulong inputBufferPosition, ulong inputBufferSize) = context.Request.GetBufferType0x21();
            (ulong outputBufferPosition, ulong outputBufferSize) = context.Request.GetBufferType0x22();
            (ulong optionsBufferPosition, ulong optionsBufferSize) = context.Request.GetBufferType0x21();

            return GetHostByNameRequestImpl(
                context,
                inputBufferPosition,
                inputBufferSize,
                outputBufferPosition,
                outputBufferSize,
                true,
                optionsBufferPosition,
                optionsBufferSize);
        }

        [CommandCmif(11)] // 5.0.0+
        // GetHostByAddrRequestWithOptions(u32, u32, u32, u64, pid, buffer<unknown, 21, 0>, buffer<unknown, 21, 0>) -> (u32, u32, u32, buffer<unknown, 22, 0>)
        public ResultCode GetHostByAddrRequestWithOptions(ServiceCtx context)
        {
            (ulong inputBufferPosition, ulong inputBufferSize) = context.Request.GetBufferType0x21();
            (ulong outputBufferPosition, ulong outputBufferSize) = context.Request.GetBufferType0x22();
            (ulong optionsBufferPosition, ulong optionsBufferSize) = context.Request.GetBufferType0x21();

            return GetHostByAddrRequestImpl(
                context,
                inputBufferPosition,
                inputBufferSize,
                outputBufferPosition,
                outputBufferSize,
                true,
                optionsBufferPosition,
                optionsBufferSize);
        }

        [CommandCmif(12)] // 5.0.0+
        // GetAddrInfoRequestWithOptions(bool enable_nsd_resolve, u32, u64 pid_placeholder, pid, buffer<i8, 5, 0> host, buffer<i8, 5, 0> service, buffer<packed_addrinfo, 5, 0> hints, buffer<unknown, 21, 0>) -> (i32 ret, u32 bsd_errno, u32 unknown, u32 packed_addrinfo_size, buffer<packed_addrinfo, 22, 0> response)
        public ResultCode GetAddrInfoRequestWithOptions(ServiceCtx context)
        {
            (ulong outputBufferPosition, ulong outputBufferSize) = context.Request.GetBufferType0x22();
            (ulong optionsBufferPosition, ulong optionsBufferSize) = context.Request.GetBufferType0x21();

            return GetAddrInfoRequestImpl(context, outputBufferPosition, outputBufferSize, true, optionsBufferPosition, optionsBufferSize);
        }

        [CommandCmif(13)]
        public ResultCode GetNameInfoRequestWithOptions(ServiceCtx context)
        {
            return GetNameInfoRequestImpl(context, true);
        }

        [CommandCmif(14)] // 5.0.0+
        // ResolverSetOptionRequest(buffer<unknown, 5, 0>, u64 unknown, u64 pid_placeholder, pid) -> (i32 ret, u32 bsd_errno)
        public ResultCode ResolverSetOptionRequest(ServiceCtx context)
        {
            ulong bufferPosition = context.Request.SendBuff[0].Position;
            ulong bufferSize = context.Request.SendBuff[0].Size;

            ulong unknown = context.RequestData.ReadUInt64();

            byte[] buffer = new byte[bufferSize];

            context.Memory.Read(bufferPosition, buffer);

            // TODO: Parse and use options.

            Logger.Stub?.PrintStub(LogClass.ServiceSfdnsres, new { unknown });

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError errno = GaiError.Success;

            context.ResponseData.Write((int)errno);
            context.ResponseData.Write((int)netDbErrorCode);

            return ResultCode.Success;
        }

        // Atmosphère extension for dns_mitm
        [CommandCmif(65000)]
        // AtmosphereReloadHostsFile()
        public ResultCode AtmosphereReloadHostsFile(ServiceCtx context)
        {
            DnsMitmResolver.Instance.ReloadEntries(context);

            return ResultCode.Success;
        }

        private static ResultCode GetHostByNameRequestImpl(
            ServiceCtx context,
            ulong inputBufferPosition,
            ulong inputBufferSize,
            ulong outputBufferPosition,
            ulong outputBufferSize,
            bool withOptions,
            ulong optionsBufferPosition,
            ulong optionsBufferSize)
        {
            string host = MemoryHelper.ReadAsciiString(context.Memory, inputBufferPosition, (int)inputBufferSize);

            if (!context.Device.Configuration.EnableInternetAccess)
            {
                Logger.Info?.Print(LogClass.ServiceSfdnsres, $"Guest network access disabled, DNS Blocked: {host}");

                WriteResponse(context, withOptions, 0, GaiError.NoData, NetDbError.HostNotFound);

                return ResultCode.Success;
            }

            // TODO: Use params.
            bool enableNsdResolve = (context.RequestData.ReadInt32() & 1) != 0;
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            int timeOut = context.RequestData.ReadInt32();
            ulong pidPlaceholder = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059

            if (withOptions)
            {
                // TODO: Parse and use options.
            }

            IPHostEntry hostEntry = null;

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError errno = GaiError.Overflow;
            int serializedSize = 0;

            if (host.Length <= byte.MaxValue)
            {
                if (enableNsdResolve)
                {
                    if (FqdnResolver.Resolve(host, out string newAddress) == Nsd.ResultCode.Success)
                    {
                        host = newAddress;
                    }
                }

                string targetHost = host;

                if (DnsBlacklist.IsHostBlocked(host))
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"DNS Blocked: {host}");

                    netDbErrorCode = NetDbError.HostNotFound;
                    errno = GaiError.NoData;
                }
                else
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"Trying to resolve: {host}");

                    try
                    {
                        hostEntry = DnsMitmResolver.Instance.ResolveAddress(targetHost);
                    }
                    catch (SocketException exception)
                    {
                        netDbErrorCode = ConvertSocketErrorCodeToNetDbError(exception.ErrorCode);
                        errno = ConvertSocketErrorCodeToGaiError(exception.ErrorCode, errno);
                    }
                }
            }
            else
            {
                netDbErrorCode = NetDbError.HostNotFound;
            }

            if (hostEntry != null)
            {
                IEnumerable<IPAddress> addresses = GetIpv4Addresses(hostEntry);

                if (!addresses.Any())
                {
                    errno = GaiError.NoData;
                    netDbErrorCode = NetDbError.NoAddress;
                }
                else
                {
                    errno = GaiError.Success;
                    serializedSize = SerializeHostEntries(context, outputBufferPosition, outputBufferSize, hostEntry, addresses);
                }
            }

            WriteResponse(context, withOptions, serializedSize, errno, netDbErrorCode);

            return ResultCode.Success;
        }

        private static ResultCode GetHostByAddrRequestImpl(
            ServiceCtx context,
            ulong inputBufferPosition,
            ulong inputBufferSize,
            ulong outputBufferPosition,
            ulong outputBufferSize,
            bool withOptions,
            ulong optionsBufferPosition,
            ulong optionsBufferSize)
        {
            if (!context.Device.Configuration.EnableInternetAccess)
            {
                Logger.Info?.Print(LogClass.ServiceSfdnsres, "Guest network access disabled, DNS Blocked.");

                WriteResponse(context, withOptions, 0, GaiError.NoData, NetDbError.HostNotFound);

                return ResultCode.Success;
            }

            byte[] rawIp = new byte[inputBufferSize];

            context.Memory.Read(inputBufferPosition, rawIp);

            // TODO: Use params.
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            uint socketLength = context.RequestData.ReadUInt32();
            uint type = context.RequestData.ReadUInt32();
            int timeOut = context.RequestData.ReadInt32();
            ulong pidPlaceholder = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059

            if (withOptions)
            {
                // TODO: Parse and use options.
            }

            IPHostEntry hostEntry = null;

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError errno = GaiError.AddressFamily;
            int serializedSize = 0;

            if (rawIp.Length == 4)
            {
                try
                {
                    IPAddress address = new(rawIp);

                    hostEntry = Dns.GetHostEntry(address);
                }
                catch (SocketException exception)
                {
                    netDbErrorCode = ConvertSocketErrorCodeToNetDbError(exception.ErrorCode);
                    errno = ConvertSocketErrorCodeToGaiError(exception.ErrorCode, errno);
                }
            }
            else
            {
                netDbErrorCode = NetDbError.NoAddress;
            }

            if (hostEntry != null)
            {
                errno = GaiError.Success;
                serializedSize = SerializeHostEntries(context, outputBufferPosition, outputBufferSize, hostEntry, GetIpv4Addresses(hostEntry));
            }

            WriteResponse(context, withOptions, serializedSize, errno, netDbErrorCode);

            return ResultCode.Success;
        }

        private static int SerializeHostEntries(ServiceCtx context, ulong outputBufferPosition, ulong outputBufferSize, IPHostEntry hostEntry, IEnumerable<IPAddress> addresses = null)
        {
            ulong originalBufferPosition = outputBufferPosition;
            ulong bufferPosition = originalBufferPosition;

            string hostName = hostEntry.HostName + '\0';

            // h_name
            context.Memory.Write(bufferPosition, Encoding.ASCII.GetBytes(hostName));
            bufferPosition += (ulong)hostName.Length;

            // h_aliases list size
            context.Memory.Write(bufferPosition, BinaryPrimitives.ReverseEndianness(hostEntry.Aliases.Length));
            bufferPosition += sizeof(int);

            // Actual aliases
            foreach (string alias in hostEntry.Aliases)
            {
                context.Memory.Write(bufferPosition, Encoding.ASCII.GetBytes(alias + '\0'));
                bufferPosition += (ulong)(alias.Length + 1);
            }

            // h_addrtype but it's a short (also only support IPv4)
            context.Memory.Write(bufferPosition, BinaryPrimitives.ReverseEndianness((short)AddressFamily.InterNetwork));
            bufferPosition += sizeof(short);

            // h_length but it's a short
            context.Memory.Write(bufferPosition, BinaryPrimitives.ReverseEndianness((short)4));
            bufferPosition += sizeof(short);

            // Ip address count, we can only support ipv4 (blame Nintendo)
            context.Memory.Write(bufferPosition, addresses != null ? BinaryPrimitives.ReverseEndianness(addresses.Count()) : 0);
            bufferPosition += sizeof(int);

            if (addresses != null)
            {
                foreach (IPAddress ip in addresses)
                {
                    context.Memory.Write(bufferPosition, BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(ip.GetAddressBytes(), 0)));
                    bufferPosition += sizeof(int);
                }
            }

            return (int)(bufferPosition - originalBufferPosition);
        }

        private static ResultCode GetAddrInfoRequestImpl(
            ServiceCtx context,
            ulong responseBufferPosition,
            ulong responseBufferSize,
            bool withOptions,
            ulong optionsBufferPosition,
            ulong optionsBufferSize)
        {
            bool enableNsdResolve = (context.RequestData.ReadInt32() & 1) != 0;
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            uint cancelHandle = context.RequestData.ReadUInt32();
#pragma warning restore IDE0059

            string host = MemoryHelper.ReadAsciiString(context.Memory, context.Request.SendBuff[0].Position, (long)context.Request.SendBuff[0].Size);
            string service = MemoryHelper.ReadAsciiString(context.Memory, context.Request.SendBuff[1].Position, (long)context.Request.SendBuff[1].Size);

            if (!context.Device.Configuration.EnableInternetAccess)
            {
                Logger.Info?.Print(LogClass.ServiceSfdnsres, $"Guest network access disabled, DNS Blocked: {host}");

                WriteResponse(context, withOptions, 0, GaiError.NoData, NetDbError.HostNotFound);

                return ResultCode.Success;
            }

            // NOTE: We ignore hints for now.
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            List<AddrInfoSerialized> hints = DeserializeAddrInfos(context.Memory, context.Request.SendBuff[2].Position, context.Request.SendBuff[2].Size);
#pragma warning restore IDE0059

            if (withOptions)
            {
                // TODO: Find unknown, Parse and use options.
#pragma warning disable IDE0059 // Remove unnecessary value assignment
                uint unknown = context.RequestData.ReadUInt32();
#pragma warning restore IDE0059
            }

#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong pidPlaceHolder = context.RequestData.ReadUInt64();
#pragma warning restore IDE0059

            IPHostEntry hostEntry = null;

            NetDbError netDbErrorCode = NetDbError.Success;
            GaiError errno = GaiError.AddressFamily;
            int serializedSize = 0;

            if (host.Length <= byte.MaxValue)
            {
                if (enableNsdResolve)
                {
                    if (FqdnResolver.Resolve(host, out string newAddress) == Nsd.ResultCode.Success)
                    {
                        host = newAddress;
                    }
                }

                string targetHost = host;

                if (DnsBlacklist.IsHostBlocked(host))
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"DNS Blocked: {host}");

                    netDbErrorCode = NetDbError.HostNotFound;
                    errno = GaiError.NoData;
                }
                else
                {
                    Logger.Info?.Print(LogClass.ServiceSfdnsres, $"Trying to resolve: {host}");

                    try
                    {
                        hostEntry = DnsMitmResolver.Instance.ResolveAddress(targetHost);
                    }
                    catch (SocketException exception)
                    {
                        netDbErrorCode = ConvertSocketErrorCodeToNetDbError(exception.ErrorCode);
                        errno = ConvertSocketErrorCodeToGaiError(exception.ErrorCode, errno);
                    }
                }
            }
            else
            {
                netDbErrorCode = NetDbError.NoAddress;
            }

            if (hostEntry != null)
            {
                if (int.TryParse(service, out int port) || string.IsNullOrEmpty(service))
                {
                    errno = GaiError.Success;
                    serializedSize = SerializeAddrInfos(context, responseBufferPosition, responseBufferSize, hostEntry, port);
                }
                else
                {
                    errno = GaiError.Service;
                }
            }

            WriteResponse(context, withOptions, serializedSize, errno, netDbErrorCode);

            return ResultCode.Success;
        }

        private static List<AddrInfoSerialized> DeserializeAddrInfos(IVirtualMemoryManager memory, ulong address, ulong size)
        {
            List<AddrInfoSerialized> result = new();

            ReadOnlySpan<byte> data = memory.GetSpan(address, (int)size);

            while (!data.IsEmpty)
            {
                AddrInfoSerialized info = AddrInfoSerialized.Read(data, out data);

                if (info == null)
                {
                    break;
                }

                result.Add(info);
            }

            return result;
        }

        private static int SerializeAddrInfos(ServiceCtx context, ulong responseBufferPosition, ulong responseBufferSize, IPHostEntry hostEntry, int port)
        {
            ulong originalBufferPosition = responseBufferPosition;
#pragma warning disable IDE0059 // Remove unnecessary value assignment
            ulong bufferPosition = originalBufferPosition;

            byte[] hostName = Encoding.ASCII.GetBytes(hostEntry.HostName + '\0');
#pragma warning restore IDE0059

            using WritableRegion region = context.Memory.GetWritableRegion(responseBufferPosition, (int)responseBufferSize);

            Span<byte> data = region.Memory.Span;

            for (int i = 0; i < hostEntry.AddressList.Length; i++)
            {
                IPAddress ip = hostEntry.AddressList[i];

                if (ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                // NOTE: 0 = Any
                AddrInfoSerializedHeader header = new(ip, 0);
                AddrInfo4 addr = new(ip, (short)port);
                AddrInfoSerialized info = new(header, addr, null, hostEntry.HostName);

                data = info.Write(data);
            }

            uint sentinel = 0;
            MemoryMarshal.Write(data, in sentinel);
            data = data[sizeof(uint)..];

            return region.Memory.Span.Length - data.Length;
        }

        private static ResultCode GetNameInfoRequestImpl(
            ServiceCtx context,
            bool withOptions)
        {
            if (!context.Device.Configuration.EnableInternetAccess)
            {
                Logger.Info?.Print(LogClass.ServiceSfdnsres, "Guest network access disabled, DNS Blocked.");

                WriteResponse(context, withOptions, 0, GaiError.NoData, NetDbError.NoAddress);

                return ResultCode.Success;
            }

            Logger.Info?.PrintStub(LogClass.ServiceSfdnsres);

            WriteResponse(context, withOptions, 0, GaiError.NoData, NetDbError.NoAddress);

            return ResultCode.Success;
        }

        private static void WriteResponse(
            ServiceCtx context,
            bool withOptions,
            int serializedSize,
            GaiError errno,
            NetDbError netDbErrorCode)
        {
            if (withOptions)
            {
                context.ResponseData.Write(serializedSize);
                context.ResponseData.Write((int)errno);
                context.ResponseData.Write((int)netDbErrorCode);
                context.ResponseData.Write(0);
            }
            else
            {
                context.ResponseData.Write((int)netDbErrorCode);
                context.ResponseData.Write((int)errno);
                context.ResponseData.Write(serializedSize);
            }
        }

        private static IEnumerable<IPAddress> GetIpv4Addresses(IPHostEntry hostEntry)
        {
            return hostEntry.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork);
        }

        private static NetDbError ConvertSocketErrorCodeToNetDbError(int errorCode)
        {
            return errorCode switch
            {
                11001 => NetDbError.HostNotFound,
                11002 => NetDbError.TryAgain,
                11003 => NetDbError.NoRecovery,
                11004 => NetDbError.NoData,
                _ => NetDbError.Internal,
            };
        }

        private static GaiError ConvertSocketErrorCodeToGaiError(int errorCode, GaiError errno)
        {
            return errorCode switch
            {
                11001 => GaiError.NoData,
                10060 => GaiError.Again,
                _ => errno,
            };
        }
    }
}
