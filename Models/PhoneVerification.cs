namespace TawseeltekAPI.Models
{
    public class PhoneVerification
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiryTime { get; set; }
        public bool IsVerified { get; set; } = false;
    }
}
