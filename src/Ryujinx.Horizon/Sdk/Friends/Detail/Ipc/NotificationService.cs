using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Account;
using Ryujinx.Horizon.Sdk.OsTypes;
using Ryujinx.Horizon.Sdk.Sf;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Horizon.Sdk.Friends.Detail.Ipc
{
    partial class NotificationService : INotificationService, IDisposable
    {
        private readonly NotificationEventHandler _notificationEventHandler;
        private readonly Uid _userId;
        private readonly FriendsServicePermissionLevel _permissionLevel;

        private readonly Lock _lock = new();

        private SystemEventType _notificationEvent;

        private readonly LinkedList<SizedNotificationInfo> _notifications;

        private bool _hasNewFriendRequest;
        private bool _hasFriendListUpdate;

        public NotificationService(NotificationEventHandler notificationEventHandler, Uid userId, FriendsServicePermissionLevel permissionLevel)
        {
            _notificationEventHandler = notificationEventHandler;
            _userId = userId;
            _permissionLevel = permissionLevel;
            _notifications = new LinkedList<SizedNotificationInfo>();
            Os.CreateSystemEvent(out _notificationEvent, EventClearMode.AutoClear, interProcess: true).AbortOnFailure();

            _hasNewFriendRequest = false;
            _hasFriendListUpdate = false;

            notificationEventHandler.RegisterNotificationService(this);
        }

        [CmifCommand(0)]
        public Result GetEvent([CopyHandle] out int eventHandle)
        {
            eventHandle = Os.GetReadableHandleOfSystemEvent(ref _notificationEvent);

            return Result.Success;
        }

        [CmifCommand(1)]
        public Result Clear()
        {
            lock (_lock)
            {
                _hasNewFriendRequest = false;
                _hasFriendListUpdate = false;

                _notifications.Clear();
            }

            return Result.Success;
        }

        [CmifCommand(2)]
        public Result Pop(out SizedNotificationInfo sizedNotificationInfo)
        {
            lock (_lock)
            {
                if (_notifications.Count >= 1)
                {
                    sizedNotificationInfo = _notifications.First.Value;
                    _notifications.RemoveFirst();

                    if (sizedNotificationInfo.Type == NotificationEventType.FriendListUpdate)
                    {
                        _hasFriendListUpdate = false;
                    }
                    else if (sizedNotificationInfo.Type == NotificationEventType.NewFriendRequest)
                    {
                        _hasNewFriendRequest = false;
                    }

                    return Result.Success;
                }
            }

            sizedNotificationInfo = default;

            return FriendResult.NotificationQueueEmpty;
        }

        public void SignalFriendListUpdate(Uid targetId)
        {
            lock (_lock)
            {
                if (_userId == targetId)
                {
                    if (!_hasFriendListUpdate)
                    {
                        SizedNotificationInfo friendListNotification = new();

                        if (_notifications.Count != 0)
                        {
                            friendListNotification = _notifications.First.Value;
                            _notifications.RemoveFirst();
                        }

                        friendListNotification.Type = NotificationEventType.FriendListUpdate;
                        _hasFriendListUpdate = true;

                        if (_hasNewFriendRequest)
                        {
                            SizedNotificationInfo newFriendRequestNotification = new();

                            if (_notifications.Count != 0)
                            {
                                newFriendRequestNotification = _notifications.First.Value;
                                _notifications.RemoveFirst();
                            }

                            newFriendRequestNotification.Type = NotificationEventType.NewFriendRequest;
                            _notifications.AddFirst(newFriendRequestNotification);
                        }

                        // We defer this to make sure we are on top of the queue.
                        _notifications.AddFirst(friendListNotification);
                    }

                    Os.SignalSystemEvent(ref _notificationEvent);
                }
            }
        }

        public void SignalNewFriendRequest(Uid targetId)
        {
            lock (_lock)
            {
                if (_permissionLevel.HasFlag(FriendsServicePermissionLevel.ViewerMask) && _userId == targetId)
                {
                    if (!_hasNewFriendRequest)
                    {
                        if (_notifications.Count == 100)
                        {
                            SignalFriendListUpdate(targetId);
                        }

                        SizedNotificationInfo newFriendRequestNotification = new()
                        {
                            Type = NotificationEventType.NewFriendRequest,
                        };

                        _notifications.AddLast(newFriendRequestNotification);
                        _hasNewFriendRequest = true;
                    }

                    Os.SignalSystemEvent(ref _notificationEvent);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notificationEventHandler.UnregisterNotificationService(this);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
