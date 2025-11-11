using System.ComponentModel.DataAnnotations.Schema;

namespace TawseeltekAPI.Models
{
    public class Price
    {
        public int PriceID { get; set; }
        public int FromCityID { get; set; }
        public int ToCityID { get; set; }
        public decimal PriceValue { get; set; } // غيرنا الاسم لتجنب التعارض

        public int CreatedByID { get; set; }  // FK صحيح
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public City FromCity { get; set; }
        public City ToCity { get; set; }

        [ForeignKey("CreatedByID")]
        public User Creator { get; set; }  // EF Core الآن يعرف أن CreatedByID هو المفتاح
                                           // Navigation properties
        public int CreatedBy { get; set; }

    }
}
