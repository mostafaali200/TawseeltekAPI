namespace TawseeltekAPI.Models
{
    public class Passenger
    {
        public int PassengerID { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public decimal Balance { get; set; }

        // علاقات
        public User User { get; set; }
        public ICollection<RidePassenger> RidePassengers { get; set; }
    }
}
