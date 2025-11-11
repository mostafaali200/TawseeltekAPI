namespace TawseeltekAPI.Models
{
    public class Ride
    {
        public int RideID { get; set; }
        public int DriverID { get; set; }
        public int FromCityID { get; set; }
        public int ToCityID { get; set; }
        public DateTime DepartureTime { get; set; }
        public string Status { get; set; } = "Pending"; // Pending / Ongoing / Completed / Cancelled
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // جديد:
        public string RoutePolyline { get; set; }          // Google encoded polyline
        public int Capacity { get; set; } = 4;             // السعة الكلية
        public int SeatsTaken { get; set; } = 0;           // عدد المقاعد المحجوزة
        public decimal PricePerSeat { get; set; } = 0;     // (اختياري)
        // Navigation properties
        public Driver Driver { get; set; }
        public City FromCity { get; set; }
        public City ToCity { get; set; }
        public ICollection<RidePassenger> RidePassengers { get; set; } = new List<RidePassenger>();
    }
}
