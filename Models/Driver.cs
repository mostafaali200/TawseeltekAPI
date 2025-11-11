namespace TawseeltekAPI.Models
{
    public class Driver
    {
        public int DriverID { get; set; }
        public int UserID { get; set; }
        public int ModelYear { get; set; }
        public string LicenseImage { get; set; }
        public string VehicleLicenseImage { get; set; }
        public string ProfileImage { get; set; }
        public string VehicleType { get; set; }
        public string PlateNumber { get; set; }
        public decimal Balance { get; set; } = 0;
        public string AvailabilityStatus { get; set; } = "Unavailable"; // Available / Unavailable / Busy
        public bool Verified { get; set; } = false;
        // جديد: موقع حي للسائق
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;


        // علاقات
        public User User { get; set; }
        public ICollection<Ride> Rides { get; set; }
        public ICollection<Penalty> Penalties { get; set; }
        public ICollection<DriverBalanceLog> BalanceLogs { get; set; }
    }
}
