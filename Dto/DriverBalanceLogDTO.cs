namespace WebApplication1.Dto
{
    public class DriverBalanceLogDTO
    {
        public int LogID { get; set; }
        public int DriverID { get; set; }
        public string DriverName { get; set; }   // اسم السائق
        public decimal Amount { get; set; }
        public string ActionType { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }    // اسم الأدمن
        public DateTime CreatedAt { get; set; }
    }
}
