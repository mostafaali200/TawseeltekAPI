namespace TawseeltekAPI.Models
{
    public class RidePassenger
    {
        public int RidePassengerID { get; set; }
        public int RideID { get; set; }
        public int PassengerID { get; set; }
        public string Status { get; set; } = "Booked"; // Booked / Cancelled / Completed / Missed
        public decimal Fare { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Ride Ride { get; set; }
        public Passenger Passenger { get; set; }
    }
}
