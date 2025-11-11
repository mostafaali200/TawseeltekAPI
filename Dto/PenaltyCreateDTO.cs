namespace WebApplication1.Dto
{
    public class PenaltyCreateDTO
    {
        public int? DriverID { get; set; }     // أحدهما مطلوب
        public int? PassengerID { get; set; }  // أحدهما مطلوب

        public decimal Amount { get; set; } = 0;
        public string Reason { get; set; }
        public string PenaltyType { get; set; } // Deduction / Warning / TemporaryBan / PermanentBan
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public int CreatedBy { get; set; } // UserID الأدمن
    }
}
