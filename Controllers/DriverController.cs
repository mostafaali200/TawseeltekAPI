using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Dto;
using TawseeltekAPI.Models;
using TawseeltekAPI.Services; // ✅ لإحضار AzureBlobStorageService
using WebApplication1.Dto;

namespace TawseeltekAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriverController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly AzureBlobStorageService _storage; // ✅ خدمة Azure Blob

        public DriverController(AppDbContext context, AzureBlobStorageService storage)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
            _storage = storage;
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
        //تسجيل سائق جديد
        [HttpPost("RegisterDriver")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterDriver([FromForm] DriverRegisterDTO dto)
        {
            // التحقق من رقم الهاتف
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber))
                return BadRequest("رقم الهاتف مستخدم مسبقًا.");

            // التحقق من كود الإحالة
            if (!string.IsNullOrEmpty(dto.ReferralCode))
            {
                var validReferral = await _context.Users
                    .AnyAsync(u => u.ReferralCode == dto.ReferralCode);
                if (!validReferral)
                    return BadRequest("❌ كود الإحالة غير صالح.");
            }

            // إنشاء المستخدم
            var user = new User
            {
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                BirthDate = DateTime.TryParse(dto.BirthDate, out var bd) ? bd : (DateTime?)null,
                Role = "Driver",
                Status = "PendingApproval",   // 🔥 مثل الراكب 100%
                CreatedAt = DateTime.UtcNow,
                ReferralCode = GenerateReferralCode(),
                ReferredBy = dto.ReferralCode,
                PasswordHash = null           // ❗ بدون كلمة مرور الآن
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // رفع الملفات على Azure
            async Task<string> SaveFileAsync(IFormFile file, string folder)
            {
                if (file == null) return null;
                return await _storage.UploadAsync(file.OpenReadStream(), file.FileName, file.ContentType, folder);
            }

            var driver = new Driver
            {
                UserID = user.UserID,
                VehicleType = dto.VehicleType,
                PlateNumber = dto.PlateNumber,
                ModelYear = dto.ModelYear,
                LicenseImage = await SaveFileAsync(dto.LicenseImage, "licenses"),
                VehicleLicenseImage = await SaveFileAsync(dto.VehicleLicenseImage, "vehicle_licenses"),
                ProfileImage = await SaveFileAsync(dto.ProfileImage, "profiles"),
                Balance = 0m,
                AvailabilityStatus = "Unavailable",
                Verified = false,
                LastUpdated = DateTime.UtcNow
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();

            // مكافأة الإحالة
            await HandleReferralRewardAsync(dto.ReferralCode, user.FullName);

            return Ok(new
            {
                message = "تم تسجيل السائق ويحتاج موافقة المشرف.",
                userID = user.UserID,
                driverID = driver.DriverID
            });
        }

        [HttpPost("SetDriverPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> SetDriverPassword([FromBody] ResetPasswordDTO dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Role == "Driver");

            if (user == null)
                return BadRequest("رقم الهاتف غير موجود.");

            if (user.Status != "PendingPassword")
                return BadRequest("❌ لا يمكن تعيين كلمة المرور الآن.");

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.Status = "Active";   // 🔥 النهاية

            await _context.SaveChangesAsync();

            return Ok(new { message = "🔐 تم تعيين كلمة المرور بنجاح والحساب أصبح فعالًا." });
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

            // ✅ نفس منطق الحفظ ولكن إلى Azure
            async Task<string> SaveFileAsync(IFormFile file, string folder)
            {
                if (file == null) return null;
                return await _storage.UploadAsync(file.OpenReadStream(), file.FileName, file.ContentType, folder);
            }

            if (dto.ProfileImage != null)
                driver.ProfileImage = await SaveFileAsync(dto.ProfileImage, "profiles");
            if (dto.LicenseImage != null)
                driver.LicenseImage = await SaveFileAsync(dto.LicenseImage, "licenses");
            if (dto.VehicleLicenseImage != null)
                driver.VehicleLicenseImage = await SaveFileAsync(dto.VehicleLicenseImage, "vehicle_licenses");

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


        [HttpPost("SetDriverPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> SetNewPassword([FromBody] ResetPasswordDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null) return BadRequest("رقم الهاتف غير موجود.");

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "🔐 تم تعيين كلمة مرور جديدة بنجاح." });
        }

        // الموافقة والتحقق من السائق
        [HttpPost("VerifyDriver/{driverId}")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> VerifyDriver(int driverId)
        {
            var driver = await _context.Drivers
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DriverID == driverId);

            if (driver == null)
                return NotFound("السائق غير موجود.");

            // تحديث حالة السائق
            driver.Verified = true;
            driver.User.Status = "PendingActivation";   // 🔥 نفس الراكب 100%
            await _context.SaveChangesAsync();

            // حذف الرموز القديمة
            var oldTokens = _context.VerificationTokens
                .Where(t => t.UserId == driver.UserID);
            _context.VerificationTokens.RemoveRange(oldTokens);

            // توليد رمز جديد
            var code = new Random().Next(100000, 999999).ToString();

            var token = new VerificationToken
            {
                UserId = driver.UserID,
                Code = code,
                ExpiryTime = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false
            };

            _context.VerificationTokens.Add(token);
            await _context.SaveChangesAsync();

            Console.WriteLine($"🔥 رمز تفعيل السائق {driver.User.FullName}: {code}");

            return Ok(new
            {
                message = "تمت الموافقة على السائق وتم إنشاء رمز التفعيل.",
                code
            });
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
        [HttpPost("ActivateDriver")]
        [AllowAnonymous]
        public async Task<IActionResult> ActivateDriver([FromBody] ActivationDTO dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Role == "Driver");

            if (user == null) return BadRequest("رقم الهاتف غير موجود.");

            var token = await _context.VerificationTokens
                .FirstOrDefaultAsync(t => t.UserId == user.UserID && t.Code == dto.Code && !t.IsUsed);

            if (token == null)
                return BadRequest("❌ رمز التفعيل غير صحيح.");

            if (token.ExpiryTime < DateTime.UtcNow)
                return BadRequest("⏳ انتهت صلاحية الرمز.");

            // تفعيل الرمز
            token.IsUsed = true;
            user.Status = "PendingPassword";  // 🔥 نفس منطق الراكب
            await _context.SaveChangesAsync();

            return Ok(new { message = "🎉 تم التفعيل. يرجى تعيين كلمة مرور جديدة." });
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

            // ✅ حذف الصور من Azure
            await _storage.DeleteAsync(driver.ProfileImage);
            await _storage.DeleteAsync(driver.LicenseImage);
            await _storage.DeleteAsync(driver.VehicleLicenseImage);

            _context.Users.Remove(driver.User);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
