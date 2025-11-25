using TawseeltekAPI.Models;

namespace TawseeltekAPI.Models
{
    public class VerificationToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Code { get; set; }
        public DateTime ExpiryTime { get; set; }
        public bool IsUsed { get; set; }
        public User User { get; set; }
    }
}
