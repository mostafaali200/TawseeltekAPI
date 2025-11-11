using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TawseeltekAPI.Hubs
{
    /// <summary>
    /// Hub للتتبع اللحظي لمواقع السائقين.
    /// - السائق يرسل UpdateLocation(driverId, lat, lng)
    /// - الراكب يشترك بسائق واحد: SubscribeToDriver(driverId)
    /// - الأدمن يشترك بالجميع: SubscribeAdmin()
    /// </summary>
    [Authorize]
    public class LocationHub : Hub
    {
        // تخزين آخر موقع لكل سائق
        private static readonly ConcurrentDictionary<int, (double Lat, double Lng, DateTime Ts)>
            _lastDriverLocation = new();

        // ربط ConnectionId بالمستخدم (اختياري)
        private static readonly ConcurrentDictionary<string, string> _connections = new();

        public override Task OnConnectedAsync()
        {
            _connections[Context.ConnectionId] = Context.User?.Identity?.Name ?? "anonymous";
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _connections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// يشترك العميل لاستقبال مواقع سائق محدد
        /// </summary>
        public async Task SubscribeToDriver(int driverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"driver-{driverId}");

            if (_lastDriverLocation.TryGetValue(driverId, out var loc))
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

        /// <summary>
        /// إلغاء الاشتراك من سائق
        /// </summary>
        public Task UnsubscribeFromDriver(int driverId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver-{driverId}");
        }

        /// <summary>
        /// ✅ الأدمن يشترك ليستقبل جميع السائقين
        /// </summary>
        public async Task SubscribeAdmin()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

            // إرسال كل المواقع المخزنة مباشرة للأدمن عند الاشتراك
            foreach (var kv in _lastDriverLocation)
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

        /// <summary>
        /// ✅ السائق يحدث موقعه
        /// </summary>
        public async Task UpdateLocation(int driverId, double lat, double lng)
        {
            _lastDriverLocation[driverId] = (lat, lng, DateTime.UtcNow);

            // البث لمجموعة السائق (لركاب هذا السائق)
            await Clients.Group($"driver-{driverId}").SendAsync("DriverLocationUpdated", new
            {
                driverId,
                lat,
                lng,
                timestamp = DateTime.UtcNow
            });

            // البث لكل الأدمن
            await Clients.Group("admins").SendAsync("DriverLocationUpdated", new
            {
                driverId,
                lat,
                lng,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
