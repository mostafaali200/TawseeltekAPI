using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Models;
using WebApplication1.Dto;

namespace TawseeltekAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverBalanceController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DriverBalanceController(AppDbContext context)
        {
            _context = context;
        }

        // ➕ إضافة / خصم رصيد
        [HttpPost]
        public async Task<ActionResult> AddBalance([FromBody] DriverBalanceDTO dto)
        {
            var driver = await _context.Drivers.FindAsync(dto.DriverID);
            if (driver == null) return NotFound("Driver not found");

            // ✅ تحديث الرصيد حسب نوع العملية
            switch (dto.ActionType)
            {
                case "Credit":
                    driver.Balance += dto.Amount;
                    break;

                case "Debit":
                case "Penalty":
                    driver.Balance -= dto.Amount;
                    if (driver.Balance < 0) driver.Balance = 0; // لا ينزل تحت الصفر
                    break;

                default:
                    return BadRequest("❌ نوع العملية غير مدعوم");
            }

            // ✅ إنشاء سجل العملية
            var log = new DriverBalanceLog
            {
                DriverID = dto.DriverID,
                Amount = dto.Amount,
                ActionType = dto.ActionType,
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? (dto.ActionType == "Credit" ? "تمت إضافة رصيد" : "تم خصم رصيد")
                    : dto.Description,
                CreatedByID = dto.CreatedByID,
                CreatedAt = DateTime.UtcNow
            };

            _context.DriverBalanceLogs.Add(log);
            await _context.SaveChangesAsync();

            // ✅ إرجاع البيانات بعد التحديث
            return Ok(new
            {
                log.LogID,
                log.CreatedAt,
                driver.Balance
            });
        }

        // 📒 جلب سجل الرصيد لسائق واحد
        [HttpGet("{driverId}")]
        public async Task<ActionResult<IEnumerable<DriverBalanceLogDTO>>> GetDriverLogs(int driverId)
        {
            var logs = await _context.DriverBalanceLogs
                .Include(l => l.Driver).ThenInclude(d => d.User)
                .Include(l => l.CreatedBy)
                .Where(l => l.DriverID == driverId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new DriverBalanceLogDTO
                {
                    LogID = l.LogID,
                    DriverID = l.DriverID,
                    DriverName = l.Driver != null && l.Driver.User != null
                        ? l.Driver.User.FullName
                        : $"ID {l.DriverID}",
                    Amount = l.Amount,
                    ActionType = l.ActionType,
                    Description = l.Description,
                    CreatedBy = l.CreatedBy != null ? l.CreatedBy.FullName : "غير معروف",
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }

        // 📜 جلب جميع السجلات
        [HttpGet("AllLogs")]
        public async Task<ActionResult<IEnumerable<DriverBalanceLogDTO>>> GetAllLogs()
        {
            var logs = await _context.DriverBalanceLogs
                .Include(l => l.Driver).ThenInclude(d => d.User)
                .Include(l => l.CreatedBy)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new DriverBalanceLogDTO
                {
                    LogID = l.LogID,
                    DriverID = l.DriverID,
                    DriverName = l.Driver != null && l.Driver.User != null
                        ? l.Driver.User.FullName
                        : $"ID {l.DriverID}",
                    Amount = l.Amount,
                    ActionType = l.ActionType,
                    Description = l.Description,
                    CreatedBy = l.CreatedBy != null ? l.CreatedBy.FullName : "غير معروف",
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }
    }
}
