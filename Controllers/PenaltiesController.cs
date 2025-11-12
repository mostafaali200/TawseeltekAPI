using Microsoft.AspNetCore.Authorization; // ✅ أضف هذا السطر في الأعلى
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Models;
using WebApplication1.Dto;


[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Supervisor")] // ✅ أضف هذا السطر لحماية جميع الدوال داخل الكنترولر
public class PenaltyController : ControllerBase
{
    private readonly AppDbContext _context;

    public PenaltyController(AppDbContext context)
    {
        _context = context;
    }

    // ✅ إضافة عقوبة (فقط للسائقين)
    [HttpPost]
    public async Task<ActionResult<PenaltyDTO>> AddPenalty([FromBody] PenaltyCreateDTO dto)
    {
        if (dto.DriverID == null)
            return BadRequest("يجب تحديد سائق للعقوبة");

        var penalty = new Penalty
        {
            DriverID = dto.DriverID,
            Amount = dto.Amount,
            Reason = dto.Reason,
            PenaltyType = dto.PenaltyType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedByID = dto.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Penalties.Add(penalty);

        // ✅ تطبيق العقوبة على السائق
        var driver = await _context.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.DriverID == dto.DriverID.Value);

        if (driver != null && driver.User != null)
        {
            switch (dto.PenaltyType)
            {
                case "Deduction":
                    driver.Balance -= dto.Amount;
                    break;
                case "TemporaryBan":
                    driver.User.Status = "Suspended";
                    driver.AvailabilityStatus = "Unavailable"; // 🚫 وقف النشاط
                    break;
                case "PermanentBan":
                    driver.User.Status = "Banned";
                    driver.AvailabilityStatus = "Unavailable"; // 🚫 وقف نهائي
                    break;
                case "Warning":
                    break;
            }
        }

        await _context.SaveChangesAsync();

        // رجّع DTO
        var d2 = await _context.Drivers.Include(x => x.User)
                 .FirstOrDefaultAsync(x => x.DriverID == penalty.DriverID);
        var name = d2?.User?.FullName ?? "غير معروف";

        return Ok(new PenaltyDTO
        {
            PenaltyID = penalty.PenaltyID,
            DriverID = penalty.DriverID,
            Reason = penalty.Reason,
            Amount = penalty.Amount,
            PenaltyType = penalty.PenaltyType,
            StartDate = penalty.StartDate,
            EndDate = penalty.EndDate,
            CreatedAt = penalty.CreatedAt,
            CreatedByID = penalty.CreatedByID,
            UserName = name,
            IsActive = penalty.IsActive
        });
    }

    // ✅ كل العقوبات
    [HttpGet("All")]
    public async Task<ActionResult<IEnumerable<PenaltyDTO>>> GetAllPenalties()
    {
        var penalties = await _context.Penalties
            .Include(p => p.Driver).ThenInclude(d => d.User)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PenaltyDTO
            {
                PenaltyID = p.PenaltyID,
                DriverID = p.DriverID,
                Reason = p.Reason,
                Amount = p.Amount,
                PenaltyType = p.PenaltyType,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedAt = p.CreatedAt,
                CreatedByID = p.CreatedByID,
                UserName = p.Driver != null ? p.Driver.User.FullName : "غير معروف",
                IsActive = p.IsActive
            })
            .ToListAsync();

        return Ok(penalties);
    }

    // ✅ عقوبات سائق واحد
    [HttpGet("Driver/{driverId}")]
    public async Task<ActionResult<IEnumerable<PenaltyDTO>>> GetDriverPenalties(int driverId)
    {
        var penalties = await _context.Penalties
            .Include(p => p.Driver).ThenInclude(d => d.User)
            .Where(p => p.DriverID == driverId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PenaltyDTO
            {
                PenaltyID = p.PenaltyID,
                DriverID = p.DriverID,
                Reason = p.Reason,
                Amount = p.Amount,
                PenaltyType = p.PenaltyType,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedAt = p.CreatedAt,
                CreatedByID = p.CreatedByID,
                UserName = p.Driver != null ? p.Driver.User.FullName : "غير معروف",
                IsActive = p.IsActive
            })
            .ToListAsync();

        return Ok(penalties);
    }

    // ✅ إلغاء عقوبة
    [HttpPut("Cancel/{penaltyId}")]
    public async Task<IActionResult> CancelPenalty(int penaltyId)
    {
        var penalty = await _context.Penalties
            .Include(p => p.Driver).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(p => p.PenaltyID == penaltyId);

        if (penalty == null) return NotFound("Penalty not found");

        penalty.IsActive = false;

        // رجع الحالة لو إيقاف
        if (penalty.PenaltyType == "TemporaryBan" || penalty.PenaltyType == "PermanentBan")
        {
            if (penalty.Driver?.User != null)
                penalty.Driver.User.Status = "Active";
            if (penalty.Driver != null)
                penalty.Driver.AvailabilityStatus = "Available";
        }

        await _context.SaveChangesAsync();
        return Ok("Penalty cancelled successfully");
    }
}
