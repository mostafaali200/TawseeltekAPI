using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;

namespace TawseeltekAPI.Hubs
{
    [Authorize]
    public class RideHub : Hub
    {
        // 👉 HubContext لكي نرسل من Controller
        public static IHubContext<RideHub>? HubContextRef;

        // 👉 ربط connectionId بالراكب
        private static readonly ConcurrentDictionary<string, int> _passengerConnections = new();

        // 👉 تجميع الإشعارات قبل إرسالها (Batch)
        private static readonly ConcurrentDictionary<int, List<object>> _pendingNotifications = new();

        // 👉 مؤقت يقوم بإرسال Batch كل ثانية
        private static readonly Timer _batchTimer;

        static RideHub()
        {
            _batchTimer = new Timer(async _ => await FlushBatchAsync(),
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
        }

        // عند الاتصال
        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        // عند الانفصال
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _passengerConnections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        // ======================================================
        // 🟦 اشتراك الراكب
        // ======================================================
        public async Task SubscribePassenger(int passengerId)
        {
            _passengerConnections[Context.ConnectionId] = passengerId;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"passenger-{passengerId}");

            // ❗❗ event name يجب أن يكون camelCase
            await Clients.Caller.SendAsync("subscribedToPassenger", new
            {
                passengerId,
                message = "تم الاشتراك بنجاح 🚀"
            });
        }

        // ======================================================
        // 🔔 إضافة إشعار إلى Buffer
        // ======================================================
        public async Task NotifyPassenger(int passengerId, string status, int rideId)
        {
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

        // ======================================================
        // 🚀 إرسال الإشعارات على شكل Batch
        // ======================================================
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

                if (events.Count == 0) continue;

                string json = JsonSerializer.Serialize(events);

                // ❗❗ event name camelCase وإلا الموبايل لا يجد الحدث
                await hub.Clients.Group($"passenger-{passengerId}")
                    .SendAsync("rideStatusUpdatedBatch", json);

                _pendingNotifications[passengerId] = new List<object>();
            }
        }

        // ======================================================
        // 🎯 استدعاء جاهز من RideController
        // ======================================================
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
