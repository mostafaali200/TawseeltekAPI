namespace WebApplication1.Dto
{
    public class NotificationDTO
    {
        public int UserID { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
    }
    public class NotificationBroadcastDTO
    {
        public string Title { get; set; }
        public string Message { get; set; }
    }
}
