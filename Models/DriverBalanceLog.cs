using System.ComponentModel.DataAnnotations;

namespace TawseeltekAPI.Models
{
    public class DriverBalanceLog
    {
        [Key]
        public int LogID { get; set; }

        public int DriverID { get; set; }
        public decimal Amount { get; set; }
        public string ActionType { get; set; } // Credit / Debit / Penalty
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByID { get; set; }   // ID الأدمن

        // Navigation properties
        public Driver Driver { get; set; }
        public User CreatedBy { get; set; }    // الأدمن/المشرف
    }
}
