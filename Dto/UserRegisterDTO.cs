namespace WebApplication1.Dto
{
    public class UserRegisterDTO
    {
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string BirthDate { get; set; } // يمكن تحويله لاحقًا إلى DateTime
        public string PasswordHash { get; set; }
        public string? ReferralCode { get; set; } // كود الإحالة الاختياري

    }
}
