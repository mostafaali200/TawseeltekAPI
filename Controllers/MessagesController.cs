using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Dto;
using TawseeltekAPI.Models;
using WebApplication1.Dto;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly AppDbContext _context;
    public MessageController(AppDbContext context)
    {
        _context = context;
    }

    // إرسال رسالة
    [HttpPost]
    public async Task<ActionResult<Message>> SendMessage([FromBody] MessageDTO dto)
    {
        var message = new Message
        {
            SenderID = dto.SenderID,
            ReceiverID = dto.ReceiverID,
            MessageText = dto.MessageText,
            IsSupportMessage = dto.IsSupportMessage,
            SentAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return Ok(message);
    }

    // جلب الرسائل بين مستخدمين
    [HttpGet("Between/{user1}/{user2}")]
    public async Task<ActionResult<IEnumerable<Message>>> GetMessages(int user1, int user2)
    {
        var messages = await _context.Messages
            .Where(m => (m.SenderID == user1 && m.ReceiverID == user2) ||
                        (m.SenderID == user2 && m.ReceiverID == user1))
            .OrderBy(m => m.SentAt)
            .ToListAsync();
        return Ok(messages);
    }
}
