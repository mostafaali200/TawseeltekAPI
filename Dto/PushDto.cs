namespace TawseeltekAPI.Models
{
    public class PushDto
    {
        public string DeviceToken { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
