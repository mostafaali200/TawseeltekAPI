using Microsoft.AspNetCore.Mvc;
using TawseeltekAPI.Data;
using TawseeltekAPI.Models;
using WebApplication1.Dto;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _context;
    public NotificationController(AppDbContext context)
    {
        _context = context;
    }

    // إرسال إشعار لمستخدم محدد
    [HttpPost]
    public async Task<ActionResult<Notification>> SendNotification([FromBody] NotificationDTO dto)
    {
        var notification = new Notification
        {
            UserID = dto.UserID,
            Title = dto.Title,
            Message = dto.Message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return Ok(notification);
    }

    // جلب كل الإشعارات
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Notification>>> GetAllNotifications()
    {
        var notifications = await _context.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }

    // جلب إشعارات مستخدم محدد
    [HttpGet("{userId}")]
    public async Task<ActionResult<IEnumerable<Notification>>> GetUserNotifications(int userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserID == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
        return Ok(notifications);
    }

    // تعليم إشعار كمقروء
    [HttpPut("MarkAsRead/{id}")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // إرسال لكل الركاب
    [HttpPost("ToAllPassengers")]
    public async Task<IActionResult> SendToAllPassengers([FromBody] NotificationBroadcastDTO dto)
    {
        var passengers = await _context.Users.Where(u => u.Role == "Passenger").ToListAsync();

        foreach (var user in passengers)
        {
            _context.Notifications.Add(new Notification
            {
                UserID = user.UserID,
                Title = dto.Title,
                Message = dto.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { Count = passengers.Count, Message = "تم إرسال الإشعارات لجميع الركاب" });
    }

    // إرسال لكل السائقين
    [HttpPost("ToAllDrivers")]
    public async Task<IActionResult> SendToAllDrivers([FromBody] NotificationBroadcastDTO dto)
    {
        var drivers = await _context.Users.Where(u => u.Role == "Driver").ToListAsync();

        foreach (var user in drivers)
        {
            _context.Notifications.Add(new Notification
            {
                UserID = user.UserID,
                Title = dto.Title,
                Message = dto.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { Count = drivers.Count, Message = "تم إرسال الإشعارات لجميع السائقين" });
    }
}
