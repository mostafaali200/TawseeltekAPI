namespace TawseeltekAPI.Models
{
    public class Penalty
    {
        public int PenaltyID { get; set; }
        public int? DriverID { get; set; }     // سائق اختياري
        public int? PassengerID { get; set; }  // راكب اختياري
        public int? CreatedByID { get; set; }
        public string Reason { get; set; }
        public decimal Amount { get; set; }
        public string PenaltyType { get; set; } // Deduction / Warning / TemporaryBan / PermanentBan
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Navigation
        public Driver Driver { get; set; }
        public Passenger Passenger { get; set; }
        public User CreatedBy { get; set; }
    }
}
