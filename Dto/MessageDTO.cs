namespace WebApplication1.Dto
{
    public class MessageDTO
    {
        public int SenderID { get; set; }
        public int ReceiverID { get; set; }
        public string MessageText { get; set; }
        public bool IsSupportMessage { get; set; } = false;
    }
}
