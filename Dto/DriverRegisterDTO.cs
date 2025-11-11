namespace TawseeltekAPI.Dto
{
    // ✅ بيانات تسجيل السائق
    public class DriverRegisterDTO
    {
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string BirthDate { get; set; }
        public string PasswordHash { get; set; }

        public int ModelYear { get; set; }

        public IFormFile LicenseImage { get; set; }
        public IFormFile VehicleLicenseImage { get; set; }
        public IFormFile ProfileImage { get; set; }

        public string VehicleType { get; set; }
        public string PlateNumber { get; set; }
        public string? ReferralCode { get; set; }
    }

    // ✅ بيانات تعديل السائق
    public class DriverUpdateDTO
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public int ModelYear { get; set; }

        public IFormFile? LicenseImage { get; set; }
        public IFormFile? VehicleLicenseImage { get; set; }
        public IFormFile? ProfileImage { get; set; }

        public string? VehicleType { get; set; }
        public string? PlateNumber { get; set; }
    }

    // ✅ بيانات السائق الراجعة من الـ API
    public class DriverDTO
    {
        public int DriverID { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string VehicleType { get; set; }
        public string PlateNumber { get; set; }
        public int ModelYear { get; set; }
        public decimal Balance { get; set; }
        public string AvailabilityStatus { get; set; }
        public bool Verified { get; set; }
        public string? ReferralCode { get; set; }

        public string ProfileImage { get; set; }
        public string LicenseImage { get; set; }
        public string VehicleLicenseImage { get; set; }
    }
}
