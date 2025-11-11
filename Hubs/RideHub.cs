using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace TawseeltekAPI.Hubs
{
    /// <summary>
    /// Hub للرحلات: إشعارات قبول/رفض/تحديث الرحلة لحظيًا.
    /// </summary>
    [Authorize] // 👈 نفس JWT الحالي
    public class RideHub : Hub
    {
        // ربط ConnectionId بالراكب
        private static readonly ConcurrentDictionary<string, int> _passengerConnections = new();

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _passengerConnections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// الراكب يشترك ليستقبل إشعارات تخصه
        /// </summary>
        public async Task SubscribePassenger(int passengerId)
        {
            _passengerConnections[Context.ConnectionId] = passengerId;

            // كل راكب عنده Group خاص
            await Groups.AddToGroupAsync(Context.ConnectionId, $"passenger-{passengerId}");

            await Clients.Caller.SendAsync("SubscribedToPassenger", new
            {
                passengerId,
                message = "✅ تم الاشتراك في إشعارات الرحلات"
            });
        }

        /// <summary>
        /// إشعار موجه لراكب معين (يُستدعى من RideController عند القبول/الرفض)
        /// </summary>
        public async Task NotifyPassenger(int passengerId, string status, int rideId)
        {
            await Clients.Group($"passenger-{passengerId}")
                .SendAsync("RideStatusUpdated", new
                {
                    passengerId,
                    rideId,
                    status,
                    timestamp = DateTime.UtcNow
                });
        }
    }
}
