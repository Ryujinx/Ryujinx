using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf.Cmif;
using Ryujinx.Horizon.Sdk.Sm;
using System;
using System.Linq;
using System.Threading;

namespace Ryujinx.Horizon.Sdk.Sf.Hipc
{
    class ServerManagerBase : ServerDomainSessionManager
    {
        private readonly SmApi _sm;

        private readonly bool _canDeferInvokeRequest;

        private readonly MultiWait _multiWait;
        private readonly MultiWait _waitList;

        private readonly Lock _multiWaitSelectionLock;
        private readonly Lock _waitListLock;

        private readonly Event _requestStopEvent;
        private readonly Event _notifyEvent;

        private readonly MultiWaitHolderBase _requestStopEventHolder;
        private readonly MultiWaitHolderBase _notifyEventHolder;

        private enum UserDataTag
        {
            Server = 1,
            Session = 2,
        }

        public ServerManagerBase(SmApi sm, ManagerOptions options) : base(options.MaxDomainObjects, options.MaxDomains)
        {
            _sm = sm;
            _canDeferInvokeRequest = options.CanDeferInvokeRequest;

            _multiWait = new MultiWait();
            _waitList = new MultiWait();

            _multiWaitSelectionLock = new Lock();
            _waitListLock = new Lock();

            _requestStopEvent = new Event(EventClearMode.ManualClear);
            _notifyEvent = new Event(EventClearMode.ManualClear);

            _requestStopEventHolder = new MultiWaitHolderOfEvent(_requestStopEvent);
            _multiWait.LinkMultiWaitHolder(_requestStopEventHolder);

            _notifyEventHolder = new MultiWaitHolderOfEvent(_notifyEvent);
            _multiWait.LinkMultiWaitHolder(_notifyEventHolder);
        }

        public void RegisterObjectForServer(IServiceObject staticObject, int portHandle)
        {
            RegisterServerImpl(0, new ServiceObjectHolder(staticObject), portHandle);
        }

        public Result RegisterObjectForServer(IServiceObject staticObject, ServiceName name, int maxSessions)
        {
            return RegisterServerImpl(0, new ServiceObjectHolder(staticObject), name, maxSessions);
        }

        public void RegisterServer(int portIndex, int portHandle)
        {
            RegisterServerImpl(portIndex, null, portHandle);
        }

        public Result RegisterServer(int portIndex, ServiceName name, int maxSessions)
        {
            return RegisterServerImpl(portIndex, null, name, maxSessions);
        }

        private void RegisterServerImpl(int portIndex, ServiceObjectHolder staticHolder, int portHandle)
        {
            Server server = AllocateServer(portIndex, portHandle, ServiceName.Invalid, managed: false, staticHolder);

            RegisterServerImpl(server);
        }

        private Result RegisterServerImpl(int portIndex, ServiceObjectHolder staticHolder, ServiceName name, int maxSessions)
        {
            Result result = _sm.RegisterService(out int portHandle, name, maxSessions, isLight: false);

            if (result.IsFailure)
            {
                return result;
            }

            Server server = AllocateServer(portIndex, portHandle, name, managed: true, staticHolder);

            RegisterServerImpl(server);

            return Result.Success;
        }

        private void RegisterServerImpl(Server server)
        {
            server.UserData = UserDataTag.Server;

            _multiWait.LinkMultiWaitHolder(server);
        }

        protected virtual Result OnNeedsToAccept(int portIndex, Server server)
        {
            throw new NotSupportedException();
        }

        protected Result AcceptImpl(Server server, IServiceObject obj)
        {
            return AcceptSession(server.PortHandle, new ServiceObjectHolder(obj));
        }

        public void ServiceRequests()
        {
            while (WaitAndProcessRequestsImpl())
            {
            }

            // Unlink pending sessions, dispose expects them to be already unlinked.

            ServerSession[] serverSessions = Enumerable.OfType<ServerSession>(_multiWait.MultiWaits).ToArray();

            foreach (ServerSession serverSession in serverSessions)
            {
                if (serverSession.IsLinked)
                {
                    serverSession.UnlinkFromMultiWaitHolder();
                }
            }
        }

        public void WaitAndProcessRequests()
        {
            WaitAndProcessRequestsImpl();
        }

        private bool WaitAndProcessRequestsImpl()
        {
            try
            {
                MultiWaitHolder multiWait = WaitSignaled();

                if (multiWait == null)
                {
                    return false;
                }

                DebugUtil.Assert(Process(multiWait).IsSuccess);

                return HorizonStatic.ThreadContext.Running;
            }
            catch (ThreadTerminatedException)
            {
                return false;
            }
        }

        private MultiWaitHolder WaitSignaled()
        {
            lock (_multiWaitSelectionLock)
            {
                while (true)
                {
                    ProcessWaitList();

                    MultiWaitHolder selected = _multiWait.WaitAny();

                    if (selected == _requestStopEventHolder)
                    {
                        return null;
                    }
                    else if (selected == _notifyEventHolder)
                    {
                        _notifyEvent.Clear();
                    }
                    else
                    {
                        selected.UnlinkFromMultiWaitHolder();

                        return selected;
                    }
                }
            }
        }

        public void ResumeProcessing()
        {
            _requestStopEvent.Clear();
        }

        public void RequestStopProcessing()
        {
            _requestStopEvent.Signal();
        }

        protected override void RegisterSessionToWaitList(ServerSession session)
        {
            session.HasReceived = false;
            session.UserData = UserDataTag.Session;

            RegisterToWaitList(session);
        }

        private void RegisterToWaitList(MultiWaitHolder holder)
        {
            lock (_waitListLock)
            {
                _waitList.LinkMultiWaitHolder(holder);
                _notifyEvent.Signal();
            }
        }

        private void ProcessWaitList()
        {
            lock (_waitListLock)
            {
                _multiWait.MoveAllFrom(_waitList);
            }
        }

        private Result Process(MultiWaitHolder holder)
        {
            return (UserDataTag)holder.UserData switch
            {
                UserDataTag.Server => ProcessForServer(holder),
                UserDataTag.Session => ProcessForSession(holder),
                _ => throw new NotImplementedException(((UserDataTag)holder.UserData).ToString()),
            };
        }

        private Result ProcessForServer(MultiWaitHolder holder)
        {
            DebugUtil.Assert((UserDataTag)holder.UserData == UserDataTag.Server);

            Server server = (Server)holder;

            try
            {
                if (server.StaticObject != null)
                {
                    return AcceptSession(server.PortHandle, server.StaticObject.Clone());
                }
                else
                {
                    return OnNeedsToAccept(server.PortIndex, server);
                }
            }
            finally
            {
                RegisterToWaitList(server);
            }
        }

        private Result ProcessForSession(MultiWaitHolder holder)
        {
            DebugUtil.Assert((UserDataTag)holder.UserData == UserDataTag.Session);

            ServerSession session = (ServerSession)holder;

            using var tlsMessage = HorizonStatic.AddressSpace.GetWritableRegion(HorizonStatic.ThreadContext.TlsAddress, Api.TlsMessageBufferSize);

            Result result;

            if (_canDeferInvokeRequest)
            {
                // If the request is deferred, we save the message on a temporary buffer to process it later.
                using var savedMessage = HorizonStatic.AddressSpace.GetWritableRegion(session.SavedMessage.Address, (int)session.SavedMessage.Size);

                DebugUtil.Assert(tlsMessage.Memory.Length == savedMessage.Memory.Length);

                if (!session.HasReceived)
                {
                    result = ReceiveRequest(session, tlsMessage.Memory.Span);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    session.HasReceived = true;

                    tlsMessage.Memory.Span.CopyTo(savedMessage.Memory.Span);
                }
                else
                {
                    savedMessage.Memory.Span.CopyTo(tlsMessage.Memory.Span);
                }

                result = ProcessRequest(session, tlsMessage.Memory.Span);

                if (result.IsFailure && !SfResult.Invalidated(result))
                {
                    return result;
                }
            }
            else
            {
                if (!session.HasReceived)
                {
                    result = ReceiveRequest(session, tlsMessage.Memory.Span);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    session.HasReceived = true;
                }

                result = ProcessRequest(session, tlsMessage.Memory.Span);

                if (result.IsFailure)
                {
                    // Those results are not valid because the service does not support deferral.
                    if (SfResult.RequestDeferred(result) || SfResult.Invalidated(result))
                    {
                        result.AbortOnFailure();
                    }

                    return result;
                }
            }

            return Result.Success;
        }
    }
}
