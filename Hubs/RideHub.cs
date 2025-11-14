using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;

namespace TawseeltekAPI.Hubs
{
    [Authorize]
    public class RideHub : Hub
    {
        // 🧠 مرجع HubContext للإرسال من الخارج (RideController)
        public static IHubContext<RideHub>? HubContextRef;

        // 🔌 ربط ConnectionId بالراكب
        private static readonly ConcurrentDictionary<string, int> _passengerConnections = new();

        // 🔥 Buffer لتجميع الإشعارات (Batch)
        private static readonly ConcurrentDictionary<int, List<object>> _pendingNotifications = new();

        // ⏱️ مؤقت لإرسال Batch كل 1 ثانية
        private static readonly Timer _batchTimer;

        static RideHub()
        {
            _batchTimer = new Timer(async _ => await FlushBatchAsync(),
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
        }

        // 🔌 عند الاتصال
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        // 🔌 عند الانفصال
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _passengerConnections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        // ✔ الراكب يشترك ليستقبل إشعارات تخصه
        public async Task SubscribePassenger(int passengerId)
        {
            _passengerConnections[Context.ConnectionId] = passengerId;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"passenger-{passengerId}");

            await Clients.Caller.SendAsync("SubscribedToPassenger", new
            {
                passengerId,
                message = "✅ تم الاشتراك في الرحلات (Turbo Mode)"
            });
        }

        // 🔔 إشعار الرحلة → يتم تخزينه مؤقتًا (Batch/Guarantee)
        public async Task NotifyPassenger(int passengerId, string status, int rideId)
        {
            var payload = new
            {
                passengerId,
                rideId,
                status,
                timestamp = DateTime.UtcNow
            };

            // حفظ في الذاكرة
            _pendingNotifications.AddOrUpdate(
                passengerId,
                _ => new List<object> { payload },
                (_, list) =>
                {
                    list.Add(payload);
                    return list;
                });
        }

        // 🚀 إرسال الـ Batch كل ثانية
        private static async Task FlushBatchAsync()
        {
            if (_pendingNotifications.Count == 0)
                return;

            var hub = HubContextRef;
            if (hub == null) return;

            foreach (var kv in _pendingNotifications.ToArray())
            {
                int passengerId = kv.Key;
                var events = kv.Value;

                if (events.Count == 0)
                    continue;

                // تحويل إلى JSON (أخف وأسرع)
                string json = JsonSerializer.Serialize(events);

                await hub.Clients.Group($"passenger-{passengerId}")
                    .SendAsync("RideStatusUpdatedBatch", json);

                // إفراغ بعد الإرسال
                _pendingNotifications[passengerId] = new List<object>();
            }
        }

        // 🎯 دالة جاهزة للإرسال من RideController
        public static async Task PushFromController(int passengerId, string status, int rideId)
        {
            if (HubContextRef == null)
                return;

            var payload = new
            {
                passengerId,
                rideId,
                status,
                timestamp = DateTime.UtcNow
            };

            _pendingNotifications.AddOrUpdate(
                passengerId,
                _ => new List<object> { payload },
                (_, list) =>
                {
                    list.Add(payload);
                    return list;
                });
        }
    }
}
