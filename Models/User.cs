
namespace TawseeltekAPI.Models
{
    public class User
    {
        public int UserID { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Role { get; set; } // Admin / Supervisor / Driver / Passenger
        public string Status { get; set; } = "Pending"; // Pending / Active / Suspended / Banned
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🎁 نظام الإحالات الجديد
        public string ReferralCode { get; set; } // الكود الخاص بالمستخدم
        public string? ReferredBy { get; set; } // الكود الذي استخدمه عند التسجيل

        // علاقات
        public Driver Driver { get; set; }
        public Passenger Passenger { get; set; }
        public ICollection<RidePassenger> RidePassengers { get; set; }
        public ICollection<Notification> Notifications { get; set; }
        public ICollection<Message> SentMessages { get; set; }
        public ICollection<Message> ReceivedMessages { get; set; }
        public ICollection<Penalty> PenaltiesCreated { get; set; }
        public ICollection<DriverBalanceLog> BalanceLogsCreated { get; set; }
    }
}
