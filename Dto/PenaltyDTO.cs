using TawseeltekAPI.Models;

namespace WebApplication1.Dto
{
    public class PenaltyDTO
    {
        public int PenaltyID { get; set; }

        public int? DriverID { get; set; }
        public int? PassengerID { get; set; }

        public string Reason { get; set; }
        public decimal Amount { get; set; }
        public string PenaltyType { get; set; } // Deduction / Warning / TemporaryBan / PermanentBan
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedByID { get; set; }

        public string UserName { get; set; } // اسم السائق/الراكب للعرض
        public bool IsActive { get; set; }
    }
    
}
