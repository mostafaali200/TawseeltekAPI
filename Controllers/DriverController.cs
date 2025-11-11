using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Dto;
using TawseeltekAPI.Models;
using WebApplication1.Dto;

namespace TawseeltekAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        public DriverController(AppDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        // ✅ توليد كود إحالة فريد
        private string GenerateReferralCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            string code;
            do
            {
                code = new string(Enumerable.Repeat(chars, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            } while (_context.Users.Any(u => u.ReferralCode == code));

            return code;
        }

        // ✅ مكافأة الإحالة
        private async Task HandleReferralRewardAsync(string referralCode, string newUserName)
        {
            if (string.IsNullOrEmpty(referralCode)) return;

            var referrer = await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode);
            if (referrer == null) return;

            decimal reward = 0.5m;

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == referrer.UserID);
            if (driver != null)
            {
                driver.Balance += reward;
                _context.DriverBalanceLogs.Add(new DriverBalanceLog
                {
                    DriverID = driver.DriverID,
                    Amount = reward,
                    ActionType = "Credit",
                    Description = $"🎁 مكافأة إحالة مستخدم جديد ({newUserName})",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return;
            }

            var passenger = await _context.Passengers.FirstOrDefaultAsync(p => p.UserID == referrer.UserID);
            if (passenger != null)
            {
                passenger.Balance += reward;
                await _context.SaveChangesAsync();
            }
        }

        // 🧍‍♂️ تسجيل سائق جديد
        [HttpPost("RegisterDriver")]
        public async Task<ActionResult<DriverDTO>> RegisterDriver([FromForm] DriverRegisterDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber) ||
                dto.PhoneNumber.Length != 10 ||
                !dto.PhoneNumber.All(char.IsDigit))
                return BadRequest("رقم الهاتف يجب أن يتكون من 10 أرقام صحيحة فقط.");

            if (await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber))
                return BadRequest("رقم الهاتف مستخدم مسبقًا.");

            if (!string.IsNullOrEmpty(dto.ReferralCode))
            {
                var validReferral = await _context.Users
                    .AnyAsync(u => u.ReferralCode == dto.ReferralCode);
                if (!validReferral)
                    return BadRequest("رمز الإحالة غير موجود.");
            }

            var user = new User
            {
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                BirthDate = DateTime.TryParse(dto.BirthDate, out var bd) ? bd : (DateTime?)null,
                Role = "Driver",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                ReferralCode = GenerateReferralCode(),
                ReferredBy = dto.ReferralCode
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.PasswordHash);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ✅ دالة حفظ الملفات
            string SaveFile(IFormFile file, string folder)
            {
                if (file == null) return null;
                var uploads = Path.Combine("wwwroot/uploads", folder);
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);
                return $"/uploads/{folder}/{fileName}";
            }

            var driver = new Driver
            {
                UserID = user.UserID,
                VehicleType = dto.VehicleType,
                PlateNumber = dto.PlateNumber,
                ModelYear = dto.ModelYear,
                LicenseImage = SaveFile(dto.LicenseImage, "licenses"),
                VehicleLicenseImage = SaveFile(dto.VehicleLicenseImage, "vehicle_licenses"),
                ProfileImage = SaveFile(dto.ProfileImage, "profiles"),
                Balance = 0m,
                AvailabilityStatus = "Unavailable",
                Verified = false,
                LastUpdated = DateTime.UtcNow
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();
            await HandleReferralRewardAsync(dto.ReferralCode, user.FullName);

            return CreatedAtAction(nameof(GetDriver), new { id = driver.DriverID }, new DriverDTO
            {
                DriverID = driver.DriverID,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                VehicleType = driver.VehicleType,
                PlateNumber = driver.PlateNumber,
                ModelYear = driver.ModelYear,
                Balance = driver.Balance,
                AvailabilityStatus = driver.AvailabilityStatus,
                Verified = driver.Verified,
                ProfileImage = driver.ProfileImage,
                LicenseImage = driver.LicenseImage,
                VehicleLicenseImage = driver.VehicleLicenseImage,
                ReferralCode = user.ReferralCode
            });
        }

        // ✏️ تعديل بيانات السائق
        [HttpPut("UpdateDriver/{id}")]
        public async Task<IActionResult> UpdateDriver(int id, [FromForm] DriverUpdateDTO dto)
        {
            var driver = await _context.Drivers.Include(d => d.User)
                                               .FirstOrDefaultAsync(d => d.DriverID == id);
            if (driver == null) return NotFound("السائق غير موجود.");

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber);
                if (existingUser != null && existingUser.UserID != driver.UserID)
                    return BadRequest("رقم الهاتف مستخدم مسبقًا.");

                driver.User.PhoneNumber = dto.PhoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                driver.User.FullName = dto.FullName;
            if (!string.IsNullOrWhiteSpace(dto.VehicleType))
                driver.VehicleType = dto.VehicleType;
            if (!string.IsNullOrWhiteSpace(dto.PlateNumber))
                driver.PlateNumber = dto.PlateNumber;
            if (dto.ModelYear > 0)
                driver.ModelYear = dto.ModelYear;

            string SaveFile(IFormFile file, string folder)
            {
                if (file == null) return null;
                var uploads = Path.Combine("wwwroot/uploads", folder);
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);
                return $"/uploads/{folder}/{fileName}";
            }

            if (dto.ProfileImage != null)
                driver.ProfileImage = SaveFile(dto.ProfileImage, "profiles");
            if (dto.LicenseImage != null)
                driver.LicenseImage = SaveFile(dto.LicenseImage, "licenses");
            if (dto.VehicleLicenseImage != null)
                driver.VehicleLicenseImage = SaveFile(dto.VehicleLicenseImage, "vehicle_licenses");

            driver.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // تحديث حالة السائق
        [HttpPut("UpdateStatus/{id}")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusUpdateDTO dto)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound("Driver not found.");

            if (string.IsNullOrEmpty(dto.AvailabilityStatus))
                return BadRequest("AvailabilityStatus is required.");

            driver.AvailabilityStatus = dto.AvailabilityStatus;
            driver.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { driver.DriverID, driver.AvailabilityStatus });
        }

        // الموافقة والتحقق من السائق
        [HttpPut("VerifyDriver/{id}")]
        public async Task<IActionResult> VerifyDriver(int id)
        {
            var driver = await _context.Drivers.Include(d => d.User)
                                               .FirstOrDefaultAsync(d => d.DriverID == id);
            if (driver == null) return NotFound("Driver not found.");

            driver.Verified = true;
            driver.AvailabilityStatus = "Available";
            driver.User.Status = "Active";

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // تعديل رصيد السائق
        [HttpPut("UpdateBalance/{id}")]
        public async Task<IActionResult> UpdateBalance(int id, [FromBody] decimal amount)
        {
            var driver = await _context.Drivers.FindAsync(id);
            if (driver == null) return NotFound("Driver not found.");

            driver.Balance += amount;
            driver.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { driver.DriverID, driver.Balance });
        }

        // جلب بيانات سائق واحد
        [HttpGet("{id}")]
        public async Task<ActionResult<DriverDTO>> GetDriver(int id)
        {
            var driver = await _context.Drivers.Include(d => d.User)
                                               .FirstOrDefaultAsync(d => d.DriverID == id);
            if (driver == null) return NotFound();

            return new DriverDTO
            {
                DriverID = driver.DriverID,
                FullName = driver.User.FullName,
                PhoneNumber = driver.User.PhoneNumber,
                VehicleType = driver.VehicleType,
                PlateNumber = driver.PlateNumber,
                ModelYear = driver.ModelYear,
                Balance = driver.Balance,
                AvailabilityStatus = driver.AvailabilityStatus,
                Verified = driver.Verified,
                ProfileImage = driver.ProfileImage,
                LicenseImage = driver.LicenseImage,
                VehicleLicenseImage = driver.VehicleLicenseImage,
                ReferralCode = driver.User.ReferralCode
            };
        }

        // التصفح والبحث
        [HttpGet("Paged")]
        public async Task<ActionResult<IEnumerable<DriverDTO>>> GetDriversPaged(int page = 1, int pageSize = 10)
        {
            var drivers = await _context.Drivers.Include(d => d.User)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = drivers.Select(d => new DriverDTO
            {
                DriverID = d.DriverID,
                FullName = d.User.FullName,
                PhoneNumber = d.User.PhoneNumber,
                VehicleType = d.VehicleType,
                PlateNumber = d.PlateNumber,
                ModelYear = d.ModelYear,
                Balance = d.Balance,
                AvailabilityStatus = d.AvailabilityStatus,
                Verified = d.Verified,
                ProfileImage = d.ProfileImage,
                LicenseImage = d.LicenseImage,
                VehicleLicenseImage = d.VehicleLicenseImage,
                ReferralCode = d.User.ReferralCode
            });

            return Ok(dtos);
        }

        // حذف سائق
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDriver(int id)
        {
            var driver = await _context.Drivers.Include(d => d.User)
                                               .FirstOrDefaultAsync(d => d.DriverID == id);
            if (driver == null) return NotFound("Driver not found");

            void DeleteFile(string path)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    var filePath = Path.Combine("wwwroot", path.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);
                }
            }

            DeleteFile(driver.ProfileImage);
            DeleteFile(driver.LicenseImage);
            DeleteFile(driver.VehicleLicenseImage);

            _context.Users.Remove(driver.User);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
