using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf.Cmif;
using Ryujinx.Horizon.Sdk.Sm;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Ryujinx.Horizon.Sdk.Sf.Hipc
{
    class ServerManager : ServerManagerBase, IDisposable
    {
        private readonly SmApi _sm;
        private readonly int _pointerBufferSize;
        private readonly bool _canDeferInvokeRequest;
        private readonly int _maxSessions;

        private readonly ulong _pointerBuffersBaseAddress;
        private readonly ulong _savedMessagesBaseAddress;

        private readonly Lock _resourceLock;
        private readonly ulong[] _sessionAllocationBitmap;
        private readonly HashSet<ServerSession> _sessions;
        private readonly HashSet<Server> _servers;

        public ServerManager(HeapAllocator allocator, SmApi sm, int maxPorts, ManagerOptions options, int maxSessions) : base(sm, options)
        {
            _sm = sm;
            _pointerBufferSize = options.PointerBufferSize;
            _canDeferInvokeRequest = options.CanDeferInvokeRequest;
            _maxSessions = maxSessions;

            if (allocator != null)
            {
                if (options.PointerBufferSize != 0)
                {
                    _pointerBuffersBaseAddress = allocator.Allocate((ulong)maxSessions * (ulong)options.PointerBufferSize);
                }

                if (options.CanDeferInvokeRequest)
                {
                    _savedMessagesBaseAddress = allocator.Allocate((ulong)maxSessions * Api.TlsMessageBufferSize);
                }
            }

            _resourceLock = new Lock();
            _sessionAllocationBitmap = new ulong[(maxSessions + 63) / 64];
            _sessions = new HashSet<ServerSession>();
            _servers = new HashSet<Server>();
        }

        private static PointerAndSize GetObjectBySessionIndex(ServerSession session, ulong baseAddress, ulong size)
        {
            return new PointerAndSize(baseAddress + (ulong)session.SessionIndex * size, size);
        }

        protected override ServerSession AllocateSession(int sessionHandle, ServiceObjectHolder obj)
        {
            int sessionIndex = -1;

            lock (_resourceLock)
            {
                if (_sessions.Count >= _maxSessions)
                {
                    return null;
                }

                for (int i = 0; i < _sessionAllocationBitmap.Length; i++)
                {
                    ref ulong mask = ref _sessionAllocationBitmap[i];

                    if (mask != ulong.MaxValue)
                    {
                        int bit = BitOperations.TrailingZeroCount(~mask);
                        sessionIndex = i * 64 + bit;
                        mask |= 1UL << bit;

                        break;
                    }
                }

                if (sessionIndex == -1)
                {
                    return null;
                }

                ServerSession session = new(sessionIndex, sessionHandle, obj);

                _sessions.Add(session);

                return session;
            }
        }

        protected override void FreeSession(ServerSession session)
        {
            if (session.ServiceObjectHolder.ServiceObject is IDisposable disposableObj)
            {
                disposableObj.Dispose();
            }

            lock (_resourceLock)
            {
                _sessionAllocationBitmap[session.SessionIndex / 64] &= ~(1UL << (session.SessionIndex & 63));
                _sessions.Remove(session);
            }
        }

        protected override Server AllocateServer(
            int portIndex,
            int portHandle,
            ServiceName name,
            bool managed,
            ServiceObjectHolder staticHoder)
        {
            lock (_resourceLock)
            {
                Server server = new(portIndex, portHandle, name, managed, staticHoder);

                _servers.Add(server);

                return server;
            }
        }

        protected override void DestroyServer(Server server)
        {
            lock (_resourceLock)
            {
                server.UnlinkFromMultiWaitHolder();
                Os.FinalizeMultiWaitHolder(server);

                if (server.Managed)
                {
                    // We should AbortOnFailure, but sometimes SM is already gone when this is called,
                    // so let's just ignore potential errors.
                    _sm.UnregisterService(server.Name);

                    HorizonStatic.Syscall.CloseHandle(server.PortHandle);
                }

                _servers.Remove(server);
            }
        }

        protected override PointerAndSize GetSessionPointerBuffer(ServerSession session)
        {
            if (_pointerBufferSize > 0)
            {
                return GetObjectBySessionIndex(session, _pointerBuffersBaseAddress, (ulong)_pointerBufferSize);
            }

            return PointerAndSize.Empty;
        }

        protected override PointerAndSize GetSessionSavedMessageBuffer(ServerSession session)
        {
            if (_canDeferInvokeRequest)
            {
                return GetObjectBySessionIndex(session, _savedMessagesBaseAddress, Api.TlsMessageBufferSize);
            }

            return PointerAndSize.Empty;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_resourceLock)
                {
                    ServerSession[] sessionsToClose = new ServerSession[_sessions.Count];

                    _sessions.CopyTo(sessionsToClose);

                    foreach (ServerSession session in sessionsToClose)
                    {
                        CloseSessionImpl(session);
                    }

                    Server[] serversToClose = new Server[_servers.Count];

                    _servers.CopyTo(serversToClose);

                    foreach (Server server in serversToClose)
                    {
                        DestroyServer(server);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
