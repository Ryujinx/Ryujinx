using Ryujinx.Common.Logging;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Services.Sockets.Bsd;
using Ryujinx.HLE.HOS.Services.Ssl.Types;
using Ryujinx.Memory;
using System;
using System.Text;

namespace Ryujinx.HLE.HOS.Services.Ssl.SslService
{
    class ISslConnection : IpcService, IDisposable
    {
        private bool _doNotClockSocket;
        private bool _getServerCertChain;
        private bool _skipDefaultVerify;
        private bool _enableAlpn;

        private readonly SslVersion _sslVersion;
        private IoMode _ioMode;
        private VerifyOption _verifyOption;
        private SessionCacheMode _sessionCacheMode;
        private string _hostName;

        private SslManagedSocketConnection _connection;
        private BsdContext _bsdContext;
        private readonly ulong _processId;

        private byte[] _nextAplnProto;

        public ISslConnection(ulong processId, SslVersion sslVersion)
        {
            _processId = processId;
            _sslVersion = sslVersion;
            _ioMode = IoMode.Blocking;
            _sessionCacheMode = SessionCacheMode.None;
            _verifyOption = VerifyOption.PeerCa | VerifyOption.HostName;
        }

        [CommandCmif(0)]
        // SetSocketDescriptor(u32) -> u32
        public ResultCode SetSocketDescriptor(ServiceCtx context)
        {
            if (_connection != null)
            {
                return ResultCode.AlreadyInUse;
            }

            _bsdContext = BsdContext.GetContext(_processId);

            if (_bsdContext == null)
            {
                return ResultCode.InvalidSocket;
            }

            int inputFd = context.RequestData.ReadInt32();

            int internalFd = _bsdContext.DuplicateFileDescriptor(inputFd);

            if (internalFd == -1)
            {
                return ResultCode.InvalidSocket;
            }

            InitializeConnection(internalFd);

            int outputFd = inputFd;

            if (_doNotClockSocket)
            {
                outputFd = -1;
            }

            context.ResponseData.Write(outputFd);

            return ResultCode.Success;
        }

        private void InitializeConnection(int socketFd)
        {
            ISocket bsdSocket = _bsdContext.RetrieveSocket(socketFd);

            _connection = new SslManagedSocketConnection(_bsdContext, _sslVersion, socketFd, bsdSocket);
        }

        [CommandCmif(1)]
        // SetHostName(buffer<bytes, 5>)
        public ResultCode SetHostName(ServiceCtx context)
        {
            ulong hostNameDataPosition = context.Request.SendBuff[0].Position;
            ulong hostNameDataSize = context.Request.SendBuff[0].Size;

            byte[] hostNameData = new byte[hostNameDataSize];

            context.Memory.Read(hostNameDataPosition, hostNameData);

            _hostName = Encoding.ASCII.GetString(hostNameData).Trim('\0');

            Logger.Info?.Print(LogClass.ServiceSsl, _hostName);

            return ResultCode.Success;
        }

        [CommandCmif(2)]
        // SetVerifyOption(nn::ssl::sf::VerifyOption)
        public ResultCode SetVerifyOption(ServiceCtx context)
        {
            _verifyOption = (VerifyOption)context.RequestData.ReadUInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { _verifyOption });

            return ResultCode.Success;
        }

        [CommandCmif(3)]
        // SetIoMode(nn::ssl::sf::IoMode)
        public ResultCode SetIoMode(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            _ioMode = (IoMode)context.RequestData.ReadUInt32();

            _connection.Socket.Blocking = _ioMode == IoMode.Blocking;

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { _ioMode });

            return ResultCode.Success;
        }

        [CommandCmif(4)]
        // GetSocketDescriptor() -> u32
        public ResultCode GetSocketDescriptor(ServiceCtx context)
        {
            context.ResponseData.Write(_connection.SocketFd);

            return ResultCode.Success;
        }

        [CommandCmif(5)]
        // GetHostName(buffer<bytes, 6>) -> u32
        public ResultCode GetHostName(ServiceCtx context)
        {
            ulong bufferAddress = context.Request.ReceiveBuff[0].Position;
            ulong bufferLen = context.Request.ReceiveBuff[0].Size;

            using (var region = context.Memory.GetWritableRegion(bufferAddress, (int)bufferLen, true))
            {
                Encoding.ASCII.GetBytes(_hostName, region.Memory.Span);
            }

            context.ResponseData.Write((uint)_hostName.Length);

            Logger.Info?.Print(LogClass.ServiceSsl, _hostName);

            return ResultCode.Success;
        }

        [CommandCmif(6)]
        // GetVerifyOption() -> nn::ssl::sf::VerifyOption
        public ResultCode GetVerifyOption(ServiceCtx context)
        {
            context.ResponseData.Write((uint)_verifyOption);

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { _verifyOption });

            return ResultCode.Success;
        }

        [CommandCmif(7)]
        // GetIoMode() -> nn::ssl::sf::IoMode
        public ResultCode GetIoMode(ServiceCtx context)
        {
            context.ResponseData.Write((uint)_ioMode);

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { _ioMode });

            return ResultCode.Success;
        }

        [CommandCmif(8)]
        // DoHandshake()
        public ResultCode DoHandshake(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            return _connection.Handshake(_hostName);
        }

        [CommandCmif(9)]
        // DoHandshakeGetServerCert() -> (u32, u32, buffer<bytes, 6>)
        public ResultCode DoHandshakeGetServerCert(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            ResultCode result = _connection.Handshake(_hostName);

            if (result == ResultCode.Success)
            {
                if (_getServerCertChain)
                {
                    using WritableRegion region = context.Memory.GetWritableRegion(context.Request.ReceiveBuff[0].Position, (int)context.Request.ReceiveBuff[0].Size);

                    result = _connection.GetServerCertificate(_hostName, region.Memory.Span, out uint bufferSize, out uint certificateCount);

                    context.ResponseData.Write(bufferSize);
                    context.ResponseData.Write(certificateCount);
                }
                else
                {
                    context.ResponseData.Write(0);
                    context.ResponseData.Write(0);
                }
            }

            return result;
        }

        [CommandCmif(10)]
        // Read() -> (u32, buffer<bytes, 6>)
        public ResultCode Read(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            ResultCode result;

            using WritableRegion region = context.Memory.GetWritableRegion(context.Request.ReceiveBuff[0].Position, (int)context.Request.ReceiveBuff[0].Size);
            // TODO: Better error management.
            result = _connection.Read(out int readCount, region.Memory);

            if (result == ResultCode.Success)
            {
                context.ResponseData.Write(readCount);
            }

            return result;
        }

        [CommandCmif(11)]
        // Write(buffer<bytes, 5>) -> s32
        public ResultCode Write(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            // We don't dispose as this isn't supposed to be modified
            WritableRegion region = context.Memory.GetWritableRegion(context.Request.SendBuff[0].Position, (int)context.Request.SendBuff[0].Size);

            // TODO: Better error management.
            ResultCode result = _connection.Write(out int writtenCount, region.Memory);

            if (result == ResultCode.Success)
            {
                context.ResponseData.Write(writtenCount);
            }

            return result;
        }

        [CommandCmif(12)]
        // Pending() -> s32
        public ResultCode Pending(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            context.ResponseData.Write(_connection.Pending());

            return ResultCode.Success;
        }

        [CommandCmif(13)]
        // Peek() -> (s32, buffer<bytes, 6>)
        public ResultCode Peek(ServiceCtx context)
        {
            if (_connection == null)
            {
                return ResultCode.NoSocket;
            }

            ResultCode result;

            using WritableRegion region = context.Memory.GetWritableRegion(context.Request.ReceiveBuff[0].Position, (int)context.Request.ReceiveBuff[0].Size);


            // TODO: Better error management.
            result = _connection.Peek(out int peekCount, region.Memory);

            if (result == ResultCode.Success)
            {
                context.ResponseData.Write(peekCount);
            }

            return result;
        }

        [CommandCmif(14)]
        // Poll(nn::ssl::sf::PollEvent poll_event, u32 timeout) -> nn::ssl::sf::PollEvent
        public ResultCode Poll(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(15)]
        // GetVerifyCertError()
        public ResultCode GetVerifyCertError(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(16)]
        // GetNeededServerCertBufferSize() -> u32
        public ResultCode GetNeededServerCertBufferSize(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(17)]
        // SetSessionCacheMode(nn::ssl::sf::SessionCacheMode)
        public ResultCode SetSessionCacheMode(ServiceCtx context)
        {
            SessionCacheMode sessionCacheMode = (SessionCacheMode)context.RequestData.ReadUInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { sessionCacheMode });

            _sessionCacheMode = sessionCacheMode;

            return ResultCode.Success;
        }

        [CommandCmif(18)]
        // GetSessionCacheMode() -> nn::ssl::sf::SessionCacheMode
        public ResultCode GetSessionCacheMode(ServiceCtx context)
        {
            context.ResponseData.Write((uint)_sessionCacheMode);

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { _sessionCacheMode });

            return ResultCode.Success;
        }

        [CommandCmif(19)]
        // FlushSessionCache()
        public ResultCode FlushSessionCache(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(20)]
        // SetRenegotiationMode(nn::ssl::sf::RenegotiationMode)
        public ResultCode SetRenegotiationMode(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(21)]
        // GetRenegotiationMode() -> nn::ssl::sf::RenegotiationMode
        public ResultCode GetRenegotiationMode(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(22)]
        // SetOption(b8 value, nn::ssl::sf::OptionType option)
        public ResultCode SetOption(ServiceCtx context)
        {
            bool value = context.RequestData.ReadUInt32() != 0;
            OptionType option = (OptionType)context.RequestData.ReadUInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { option, value });

            return SetOption(option, value);
        }

        [CommandCmif(23)]
        // GetOption(nn::ssl::sf::OptionType) -> b8
        public ResultCode GetOption(ServiceCtx context)
        {
            OptionType option = (OptionType)context.RequestData.ReadUInt32();

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { option });

            ResultCode result = GetOption(option, out bool value);

            if (result == ResultCode.Success)
            {
                context.ResponseData.Write(value);
            }

            return result;
        }

        [CommandCmif(24)]
        // GetVerifyCertErrors() -> (u32, u32, buffer<bytes, 6>)
        public ResultCode GetVerifyCertErrors(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(25)] // 4.0.0+
        // GetCipherInfo(u32) -> buffer<bytes, 6>
        public ResultCode GetCipherInfo(ServiceCtx context)
        {
            throw new ServiceNotImplementedException(this, context);
        }

        [CommandCmif(26)]
        // SetNextAlpnProto(buffer<bytes, 5>) -> u32
        public ResultCode SetNextAlpnProto(ServiceCtx context)
        {
            ulong inputDataPosition = context.Request.SendBuff[0].Position;
            ulong inputDataSize = context.Request.SendBuff[0].Size;

            _nextAplnProto = new byte[inputDataSize];

            context.Memory.Read(inputDataPosition, _nextAplnProto);

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { inputDataSize });

            return ResultCode.Success;
        }

        [CommandCmif(27)]
        // GetNextAlpnProto(buffer<bytes, 6>) -> u32
        public ResultCode GetNextAlpnProto(ServiceCtx context)
        {
            ulong outputDataPosition = context.Request.ReceiveBuff[0].Position;
            ulong outputDataSize = context.Request.ReceiveBuff[0].Size;

            context.Memory.Write(outputDataPosition, _nextAplnProto);

            context.ResponseData.Write(_nextAplnProto.Length);

            Logger.Stub?.PrintStub(LogClass.ServiceSsl, new { outputDataSize });

            return ResultCode.Success;
        }

        private ResultCode SetOption(OptionType option, bool value)
        {
            switch (option)
            {
                case OptionType.DoNotCloseSocket:
                    _doNotClockSocket = value;
                    break;

                case OptionType.GetServerCertChain:
                    _getServerCertChain = value;
                    break;

                case OptionType.SkipDefaultVerify:
                    _skipDefaultVerify = value;
                    break;

                case OptionType.EnableAlpn:
                    _enableAlpn = value;
                    break;

                default:
                    Logger.Warning?.Print(LogClass.ServiceSsl, $"Unsupported option {option}");
                    return ResultCode.InvalidOption;
            }

            return ResultCode.Success;
        }

        private ResultCode GetOption(OptionType option, out bool value)
        {
            switch (option)
            {
                case OptionType.DoNotCloseSocket:
                    value = _doNotClockSocket;
                    break;

                case OptionType.GetServerCertChain:
                    value = _getServerCertChain;
                    break;

                case OptionType.SkipDefaultVerify:
                    value = _skipDefaultVerify;
                    break;

                case OptionType.EnableAlpn:
                    value = _enableAlpn;
                    break;

                default:
                    Logger.Warning?.Print(LogClass.ServiceSsl, $"Unsupported option {option}");

                    value = false;
                    return ResultCode.InvalidOption;
            }

            return ResultCode.Success;
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
