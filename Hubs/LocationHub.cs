using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TawseeltekAPI.Hubs
{
    [Authorize]
    public class LocationHub : Hub
    {
        // 🔥 هذا هو المرجع الذي سنملأه من Program.cs
        public static IHubContext<LocationHub>? HubContextRef { get; set; }

        // آخر موقع للسائق (Lat, Lng, Time)
        private static readonly ConcurrentDictionary<int, (double Lat, double Lng, DateTime Ts)>
            _drivers = new();

        // هل تغير موقع السائق؟
        private static readonly ConcurrentDictionary<int, bool> _dirty = new();

        // اتصالات المستخدمين
        private static readonly ConcurrentDictionary<string, string> _connections = new();

        // مؤقّت لإرسال Batch كل 1 ثانية
        private static readonly Timer _batchTimer;

        static LocationHub()
        {
            _batchTimer = new Timer(async _ =>
            {
                try
                {
                    await BroadcastBatchUpdates();
                    CleanupInactiveDrivers();
                }
                catch { }
            },
            null,
            1000,
            1000);
        }

        // عند الاتصال
        public override Task OnConnectedAsync()
        {
            _connections[Context.ConnectionId] = "connected";
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _connections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        // ================================
        //  🧭 السائق يرسل موقعه
        // ================================
        public Task UpdateLocation(int driverId, double lat, double lng)
        {
            _drivers[driverId] = (lat, lng, DateTime.UtcNow);
            _dirty[driverId] = true;

            return Task.CompletedTask;
        }

        // ================================
        //  🔄 الراكب يشترك في سائق
        // ================================
        public async Task SubscribeToDriver(int driverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"driver-{driverId}");

            if (_drivers.TryGetValue(driverId, out var loc))
            {
                await Clients.Caller.SendAsync("DriverLocationUpdated", new
                {
                    driverId,
                    lat = loc.Lat,
                    lng = loc.Lng,
                    timestamp = loc.Ts
                });
            }
        }

        public Task UnsubscribeFromDriver(int driverId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver-{driverId}");
        }

        // ================================
        //  🛡️ الأدمن يشترك بالجميع
        // ================================
        public async Task SubscribeAdmin()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

            foreach (var kv in _drivers)
            {
                await Clients.Caller.SendAsync("DriverLocationUpdated", new
                {
                    driverId = kv.Key,
                    lat = kv.Value.Lat,
                    lng = kv.Value.Lng,
                    timestamp = kv.Value.Ts
                });
            }
        }

        // ================================
        //  🚀 إرسال التغييرات فقط
        // ================================
        private static async Task BroadcastBatchUpdates()
        {
            var changes = new List<object>();

            foreach (var kv in _dirty.ToArray())
            {
                if (!kv.Value) continue;

                int driverId = kv.Key;

                if (_drivers.TryGetValue(driverId, out var loc))
                {
                    changes.Add(new
                    {
                        driverId,
                        lat = loc.Lat,
                        lng = loc.Lng,
                        timestamp = loc.Ts
                    });
                }

                _dirty[driverId] = false;
            }

            if (changes.Count == 0)
                return;

            string json = JsonSerializer.Serialize(changes);

            var hubContext = HubContextRef;
            if (hubContext == null) return;

            // إرسال للأدمن
            await hubContext.Clients.Group("admins")
                .SendAsync("DriverLocationUpdatedBatch", json);

            // إرسال للراكب المشترك مع سائق محدد
            foreach (var change in changes)
            {
                dynamic obj = change;

                await hubContext.Clients.Group($"driver-{obj.driverId}")
                    .SendAsync("DriverLocationUpdatedBatch", json);
            }
        }

        // ================================
        //  ⚠️ تنظيف السائقين المتوقفين
        // ================================
        private static void CleanupInactiveDrivers()
        {
            var threshold = DateTime.UtcNow.AddSeconds(-15);

            foreach (var kv in _drivers.ToArray())
            {
                if (kv.Value.Ts < threshold)
                {
                    _drivers.TryRemove(kv.Key, out _);
                    _dirty.TryRemove(kv.Key, out _);
                }
            }
        }
    }
}
