namespace TawseeltekAPI.Models
{
    public class City
    {

        public int CityID { get; set; }
        public string CityName { get; set; }

        // تهيئة الـ collections لتجنب NullReferenceException
        public ICollection<Price> PricesFrom { get; set; } = new List<Price>();
        public ICollection<Price> PricesTo { get; set; } = new List<Price>();
        public ICollection<Ride> RidesFrom { get; set; } = new List<Ride>();
        public ICollection<Ride> RidesTo { get; set; } = new List<Ride>();
    }
}
