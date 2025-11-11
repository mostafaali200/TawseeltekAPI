namespace TawseeltekAPI.Dto
{
    public class RegisterDeviceDto
    {
        public int UserID { get; set; }
        public string Role { get; set; } = "";
        public string DeviceToken { get; set; } = "";
    }
}
