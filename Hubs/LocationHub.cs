using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TawseeltekAPI.Hubs
{
    [Authorize]
    public class LocationHub : Hub
    {
        public static IHubContext<LocationHub>? _hubContextRef;

        // آخر موقع للسائق (Lat, Lng, Time)
        private static readonly ConcurrentDictionary<int, (double Lat, double Lng, DateTime Ts)>
            _drivers = new();

        // هل تغير موقع السائق؟ (لتقليل الإرسال)
        private static readonly ConcurrentDictionary<int, bool> _dirty = new();

        // اتصالات المستخدمين
        private static readonly ConcurrentDictionary<string, string> _connections = new();

        // مؤقّت لإرسال Batch كل 1 ثانية
        private static readonly Timer _batchTimer;

        // إعدادات
        private static readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions { WriteIndented = false };

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
            1000,   // يبدأ بعد ثانية
            1000);  // يرسل كل ثانية
        }

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
        //  🧭 السائق يحدث موقعه
        // ================================
        public Task UpdateLocation(int driverId, double lat, double lng)
        {
            _drivers[driverId] = (lat, lng, DateTime.UtcNow);
            _dirty[driverId] = true; // علم أنه تغير لتقليل الإرسال
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
        //  🚀 إرسال Batch التغييرات فقط (Delta)
        // ================================
        private static async Task BroadcastBatchUpdates()
        {
            // لو ما في تغييرات، لا ترسل شيء
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

            // تحويل Batch إلى JSON (أسرع من الإرسال واحد واحد)
            string json = JsonSerializer.Serialize(changes);

            // ⚠️ ملاحظة مهمة جداً:
            // لا نستطيع استعمال Clients هنا لأنه static
            // لذلك نستخدم HubContext الذي سنمرره لخارج الهب
            var hubContext = _hubContextRef;

            if (hubContext == null)
                return;

            // إرسال لجميع الأدمن
            await hubContext.Clients.Group("admins")
                .SendAsync("DriverLocationUpdatedBatch", json);

            // إرسال كل سائق لمجموعته
            foreach (var change in changes)
            {
                dynamic obj = change;
                await hubContext.Clients.Group($"driver-{obj.driverId}")
                    .SendAsync("DriverLocationUpdatedBatch", json);
            }
        }

        // ================================
        //  ⚠️ كشف السائق المتوقف
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
