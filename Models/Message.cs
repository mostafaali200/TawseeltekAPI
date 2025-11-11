namespace TawseeltekAPI.Models
{
    public class Message
    {
        public int MessageID { get; set; }
        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public string MessageText { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsSupportMessage { get; set; } = false;

        public User Sender { get; set; }
        public User Receiver { get; set; }
    }
}
