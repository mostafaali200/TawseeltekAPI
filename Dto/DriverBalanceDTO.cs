namespace WebApplication1.Dto
{
    public class DriverBalanceDTO
    {
        public int DriverID { get; set; }
        public decimal Amount { get; set; }
        public string ActionType { get; set; } // Credit / Debit / Penalty
        public string Description { get; set; }
        public int? CreatedByID { get; set; }  // يجب أن يكون UserID للأدمن
    }
}
