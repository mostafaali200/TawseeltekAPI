using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Dto;
using TawseeltekAPI.Models;
using TawseeltekAPI.Services;

namespace TawseeltekAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly FirebaseV1Service _fcm;
        private readonly AppDbContext _context;

        public NotificationController(FirebaseV1Service fcm, AppDbContext context)
        {
            _fcm = fcm;
            _context = context;
        }

        // ✅ تسجيل الجهاز
        [HttpPost("RegisterDevice")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto)
        {
            var exists = await _context.UserDevices
                .FirstOrDefaultAsync(d => d.UserID == dto.UserID && d.DeviceToken == dto.DeviceToken);

            if (exists == null)
            {
                var device = new UserDevice
                {
                    UserID = dto.UserID,
                    Role = dto.Role,
                    DeviceToken = dto.DeviceToken
                };

                _context.UserDevices.Add(device);
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "✅ Device registered successfully" });
        }

        // ✅ إرسال إشعار لمستخدم واحد
        [HttpPost("SendToDevice")]
        public async Task<IActionResult> SendToDevice([FromBody] PushDto dto)
        {
            var result = await _fcm.SendNotificationAsync(dto.DeviceToken, dto.Title, dto.Message);
            if (!result) return BadRequest("❌ فشل في إرسال الإشعار");
            return Ok(new { message = "✅ تم إرسال الإشعار بنجاح" });
        }

        // ✅ إرسال إشعارات حسب الدور
        [HttpPost("SendToRole/{role}")]
        public async Task<IActionResult> SendToRole(string role, [FromBody] PushDto dto)
        {
            var tokens = await _context.UserDevices
                .Where(d => d.Role == role)
                .Select(d => d.DeviceToken)
                .ToListAsync();

            if (!tokens.Any())
                return NotFound("❌ لا يوجد أجهزة مسجلة لهذا الدور");

            int successCount = 0;
            foreach (var token in tokens)
            {
                var sent = await _fcm.SendNotificationAsync(token, dto.Title, dto.Message);
                if (sent) successCount++;
            }

            return Ok(new { message = $"تم إرسال {successCount}/{tokens.Count} إشعارات بنجاح" });
        }
    }
}
