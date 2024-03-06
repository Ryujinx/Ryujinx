using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Common;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Ryujinx.Ava.UI.Helpers
{
    public static class NotificationHelper
    {
        private const int MaxNotifications = 4;
        private const int NotificationDelayInMs = 5000;

        private static WindowNotificationManager _notificationManager;

        private static readonly BlockingCollection<Notification> _notifications = new();

        public static void SetNotificationManager(Window host)
        {
            _notificationManager = new WindowNotificationManager(host)
            {
                Position = NotificationPosition.BottomRight,
                MaxItems = MaxNotifications,
                Margin = new Thickness(0, 0, 15, 40),
            };

            var maybeAsyncWorkQueue = new Lazy<AsyncWorkQueue<Notification>>(
                () => new AsyncWorkQueue<Notification>(notification =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _notificationManager.Show(notification);
                        });
                    },
                    "UI.NotificationThread",
                    _notifications),
                LazyThreadSafetyMode.ExecutionAndPublication);

            _notificationManager.TemplateApplied += (sender, args) =>
            {
                // NOTE: Force creation of the AsyncWorkQueue.
                _ = maybeAsyncWorkQueue.Value;
            };

            host.Closing += (sender, args) =>
            {
                if (maybeAsyncWorkQueue.IsValueCreated)
                {
                    maybeAsyncWorkQueue.Value.Dispose();
                }
            };
        }

        public static void Show(string title, string text, NotificationType type, bool waitingExit = false, Action onClick = null, Action onClose = null)
        {
            var delay = waitingExit ? TimeSpan.FromMilliseconds(0) : TimeSpan.FromMilliseconds(NotificationDelayInMs);

            _notifications.Add(new Notification(title, text, type, delay, onClick, onClose));
        }

        public static void ShowError(string message)
        {
            Show(LocaleManager.Instance[LocaleKeys.DialogErrorTitle], $"{LocaleManager.Instance[LocaleKeys.DialogErrorMessage]}\n\n{message}", NotificationType.Error);
        }
    }
}
