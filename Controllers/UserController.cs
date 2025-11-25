using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Models;
using TawseeltekAPI.Services;
using WebApplication1.Dto;

namespace TawseeltekAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwt;
        private readonly PasswordHasher<User> _passwordHasher;

        public UserController(AppDbContext context, JwtService jwt)
        {
            _context = context;
            _jwt = jwt;
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

        // ✅ مكافأة الإحالة (للسائق أو الراكب)
        private async Task HandleReferralRewardAsync(string referralCode, string newUserName)
        {
            if (string.IsNullOrEmpty(referralCode)) return;

            var referrer = await _context.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode);
            if (referrer == null) return;

            decimal reward = 0.5m;

            // 🎯 إذا كان سائقًا
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

            // 🎯 إذا كان راكبًا
            var passenger = await _context.Passengers.FirstOrDefaultAsync(p => p.UserID == referrer.UserID);
            if (passenger != null)
            {
                passenger.Balance += reward;
                await _context.SaveChangesAsync();
            }
        }

        // -----------------------------
        // 📱 إرسال كود التحقق (OTP يدوي)
        // -----------------------------
        [HttpPost("SendVerificationCode")]
        [AllowAnonymous]
        public async Task<IActionResult> SendVerificationCode([FromBody] string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return BadRequest("يرجى إدخال رقم الهاتف.");

            if (await _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber))
                return BadRequest("هذا الرقم مسجل مسبقًا.");

            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            // حذف الأكواد القديمة لنفس الرقم
            var oldCodes = _context.PhoneVerifications.Where(v => v.PhoneNumber == phoneNumber);
            _context.PhoneVerifications.RemoveRange(oldCodes);

            _context.PhoneVerifications.Add(new PhoneVerification
            {
                PhoneNumber = phoneNumber,
                Code = code,
                ExpiryTime = DateTime.UtcNow.AddMinutes(10),
                IsVerified = false
            });
            await _context.SaveChangesAsync();

            // 👀 عرض الكود في Console حتى ترسله يدويًا
            Console.WriteLine($"📲 كود التحقق لرقم {phoneNumber} هو: {code}");

            return Ok(new { message = "✅ تم إنشاء كود التحقق. أرسله يدويًا عبر واتساب." });
        }

        // -----------------------------
        // ✅ التحقق من الكود
        // -----------------------------
        [HttpPost("VerifyCode")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeDTO dto)
        {
            if (string.IsNullOrEmpty(dto.PhoneNumber) || string.IsNullOrEmpty(dto.Code))
                return BadRequest("❌ رقم الهاتف أو الكود غير موجود.");

            var verification = await _context.PhoneVerifications
                .FirstOrDefaultAsync(v => v.PhoneNumber == dto.PhoneNumber && v.Code == dto.Code);

            if (verification == null)
                return BadRequest("❌ الكود غير صحيح.");

            if (verification.ExpiryTime < DateTime.UtcNow)
                return BadRequest("⚠️ الكود منتهي الصلاحية.");

            verification.IsVerified = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ تم التحقق بنجاح، يمكنك الآن التسجيل." });
        }
        [HttpPost("RegisterPassenger")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> RegisterPassenger([FromBody] UserRegisterDTO dto)
        {
            // ✅ تحقق من رقم الهاتف
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber))
                return BadRequest("رقم الهاتف مستخدم مسبقًا.");

            // ✅ تحقق من الكود (تم التحقق من رقم الهاتف)
            var verified = await _context.PhoneVerifications
                .FirstOrDefaultAsync(v => v.PhoneNumber == dto.PhoneNumber && v.IsVerified);
            if (verified == null)
                return BadRequest("❌ يجب التحقق من رقم الهاتف قبل التسجيل.");

            // ✅ تحقق من كود الإحالة إذا تم إدخاله
            if (!string.IsNullOrEmpty(dto.ReferralCode))
            {
                var referrer = await _context.Users
                    .FirstOrDefaultAsync(u => u.ReferralCode == dto.ReferralCode);

                if (referrer == null)
                    return BadRequest("❌ كود الإحالة غير صالح، يرجى التأكد من صحته.");
            }

            // ✅ إنشاء المستخدم الجديد
            var user = new User
            {
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                BirthDate = DateTime.TryParse(dto.BirthDate, out var bd) ? bd : (DateTime?)null,
                Role = "Passenger",
                Status = "PendingActivation",
                CreatedAt = DateTime.UtcNow,
                ReferralCode = GenerateReferralCode(),
                ReferredBy = dto.ReferralCode
            };


            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ✅ إضافة سجل الراكب
            var passenger = new Passenger
            {
                UserID = user.UserID,
                Balance = 0m
            };
            _context.Passengers.Add(passenger);
            await _context.SaveChangesAsync();

            // ✅ معالجة المكافأة (إن وُجد كود إحالة)
            await HandleReferralRewardAsync(dto.ReferralCode, user.FullName);

            // ✅ الاستجابة
            return Ok(new
            {
                userID = user.UserID,
                fullName = user.FullName,
                phoneNumber = user.PhoneNumber,
                role = user.Role,
                referralCode = user.ReferralCode,
                referredBy = user.ReferredBy,
                balance = passenger.Balance
            });
        }

        // -----------------------------
        // 🧑‍💼 تسجيل مسؤول جديد (Admin)
        // -----------------------------
        [HttpPost("RegisterAdmin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<User>> RegisterAdmin([FromBody] UserRegisterDTO dto)
        {
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber))
                return BadRequest("رقم الهاتف مستخدم مسبقًا.");

            var user = new User
            {
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                BirthDate = DateTime.TryParse(dto.BirthDate, out var bd) ? bd : (DateTime?)null,
                Role = "Admin",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.PasswordHash);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.UserID }, user);
        }



        // -----------------------------
        // 🔑 تسجيل الدخول
        // -----------------------------
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> Login([FromBody] UserLoginDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null)
                return Unauthorized(new { message = "❌ رقم الهاتف أو كلمة المرور غير صحيحة." });

            if (user.Status != "Active")
                return Unauthorized(new { message = "⚠️ الحساب غير مفعل أو موقوف." });

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "❌ رقم الهاتف أو كلمة المرور غير صحيحة." });

            var token = _jwt.GenerateToken(user.UserID, user.FullName, user.Role);

            int? driverId = null;
            if (user.Role == "Driver")
            {
                driverId = await _context.Drivers
                    .Where(d => d.UserID == user.UserID)
                    .Select(d => (int?)d.DriverID)
                    .FirstOrDefaultAsync();
            }

            return Ok(new
            {
                success = true,
                token,
                role = user.Role,
                referralCode = user.ReferralCode,
                driverId,
                user = new
                {
                    user.UserID,
                    user.FullName,
                    user.PhoneNumber,
                    user.Role
                }
            });
        }

        // -----------------------------
        // 📋 جميع المستخدمين
        // -----------------------------
        [HttpGet("AllUsers")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<ActionResult<IEnumerable<object>>> GetAllUsers()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new
                {
                    u.UserID,
                    u.FullName,
                    u.PhoneNumber,
                    u.Role,
                    u.Status,
                    u.ReferralCode,
                    u.ReferredBy,
                    CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Ok(users);
        }

        // -----------------------------
        // 📜 جميع أكواد التحقق (لعرضها في لوحة التحكم)
        // -----------------------------
        [HttpGet("AllVerifications")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> GetAllVerifications()
        {
            var data = await _context.PhoneVerifications
                .OrderByDescending(v => v.ExpiryTime)
                .Select(v => new
                {
                    v.PhoneNumber,
                    v.Code,
                    v.IsVerified,
                    v.ExpiryTime
                })
                .ToListAsync();

            return Ok(data);
        }

        // -----------------------------
        // باقي الدوال بدون أي تعديل
        // -----------------------------
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            decimal balance = 0m;

            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == id);
            if (driver != null)
                balance = driver.Balance;

            var passenger = await _context.Passengers.FirstOrDefaultAsync(p => p.UserID == id);
            if (passenger != null)
                balance = passenger.Balance;

            return Ok(new
            {
                userID = user.UserID,
                fullName = user.FullName,
                phoneNumber = user.PhoneNumber,
                role = user.Role,
                referralCode = user.ReferralCode,
                referredBy = user.ReferredBy,
                status = user.Status,
                balance
            });
        }
        // 🗑️ حذف مستخدم (Admin فقط) — مع حماية ضد حذف الأدمن
        [HttpDelete("DeleteUser/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
                if (user == null)
                    return NotFound(new { message = "❌ المستخدم غير موجود." });

                // 🚫 منع حذف أي مستخدم بدور Admin
                if (user.Role == "Admin")
                    return BadRequest(new { message = "⚠️ لا يمكن حذف مستخدم من نوع Admin." });

                // 🚫 منع الأدمن من حذف نفسه
                var currentUserId = int.Parse(User.Claims.First(c => c.Type == "id").Value);
                if (user.UserID == currentUserId)
                    return BadRequest(new { message = "⚠️ لا يمكنك حذف حسابك الشخصي." });

                // 🧹 نحذف الراكب أو السائق إذا وجد
                var passenger = await _context.Passengers.FirstOrDefaultAsync(p => p.UserID == id);
                if (passenger != null)
                    _context.Passengers.Remove(passenger);

                var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.UserID == id);
                if (driver != null)
                    _context.Drivers.Remove(driver);

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"✅ تم حذف المستخدم ({user.FullName}) بنجاح." });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new
                {
                    message = "⚠️ لا يمكن حذف هذا المستخدم لأنه مرتبط ببيانات أخرى (رحلات، حجوزات...).",
                    details = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "❌ خطأ أثناء عملية الحذف.", details = ex.Message });
            }
        }


        [HttpPut("ChangeRole/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRole(int userId, [FromBody] string newRole)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("المستخدم غير موجود");

            var allowedRoles = new[] { "Admin", "Supervisor", "Driver", "Passenger" };
            if (!allowedRoles.Contains(newRole))
                return BadRequest("الدور غير صالح");

            user.Role = newRole;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"✅ تم تغيير دور {user.FullName} إلى {newRole}" });
        }

        [HttpPut("ChangeStatus/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeStatus(int userId, [FromBody] string newStatus)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("المستخدم غير موجود");

            var allowedStatuses = new[] { "Active", "Suspended", "Pending" };
            if (!allowedStatuses.Contains(newStatus))
                return BadRequest("الحالة غير صالحة");

            user.Status = newStatus;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"✅ تم تغيير حالة {user.FullName} إلى {newStatus}" });
        }

        [HttpDelete("DeletePassenger/{id}")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> DeletePassenger(int id)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
                if (user == null)
                    return NotFound(new { message = "❌ المستخدم غير موجود." });

                var passenger = await _context.Passengers.FirstOrDefaultAsync(p => p.UserID == id);
                if (passenger != null)
                    _context.Passengers.Remove(passenger);

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return StatusCode(200, new { success = true, message = $"✅ تم حذف المستخدم ({user.FullName}) بنجاح." });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new
                {
                    message = "⚠️ لا يمكن حذف هذا الراكب لأنه مرتبط ببيانات أخرى (رحلات، حجوزات...).",
                    details = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "❌ خطأ أثناء عملية الحذف.", details = ex.Message });
            }
        }


        // -------------------------------------------------------
        // 🟢 1) توليد رمز تفعيل من المشرف
        // -------------------------------------------------------
        [HttpPost("GenerateActivationCode/{userId}")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> GenerateActivationCode(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null) return NotFound("المستخدم غير موجود.");

            var code = new Random().Next(100000, 999999).ToString();

            // حذف الأكواد القديمة
            var oldTokens = _context.VerificationTokens.Where(t => t.UserId == userId);
            _context.VerificationTokens.RemoveRange(oldTokens);

            // إنشاء رمز جديد
            var token = new VerificationToken
            {
                UserId = userId,
                Code = code,
                ExpiryTime = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false
            };

            _context.VerificationTokens.Add(token);
            await _context.SaveChangesAsync();

            Console.WriteLine($"🔥 رمز تفعيل الراكب {user.FullName}: {code}");

            return Ok(new { message = "تم إنشاء رمز التفعيل", code });
        }

        // -------------------------------------------------------
        // 🟢 2) تفعيل الحساب من قبل المستخدم
        // -------------------------------------------------------
        [HttpPost("ActivateAccount")]
        [AllowAnonymous]
        public async Task<IActionResult> ActivateAccount([FromBody] ActivationDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null) return BadRequest("رقم الهاتف غير موجود.");

            var token = await _context.VerificationTokens
                .FirstOrDefaultAsync(t => t.UserId == user.UserID && t.Code == dto.Code && !t.IsUsed);

            if (token == null) return BadRequest("رمز التفعيل غير صحيح.");
            if (token.ExpiryTime < DateTime.UtcNow) return BadRequest("انتهت صلاحية الرمز.");

            token.IsUsed = true;
            user.Status = "PendingPassword";

            await _context.SaveChangesAsync();

            return Ok(new { message = "🎉 تم تفعيل الحساب. يرجى تعيين كلمة مرور جديدة." });
        }

        // -------------------------------------------------------
        // 🟢 3) المستخدم يعيّن كلمة المرور لأول مرة
        // -------------------------------------------------------
        [HttpPost("SetNewPassword")]
        [AllowAnonymous]
        public async Task<IActionResult> SetNewPassword([FromBody] ResetPasswordDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.PhoneNumber);
            if (user == null) return BadRequest("رقم الهاتف غير موجود.");
            if (user.Status != "PendingPassword")
                return BadRequest("لا يمكن تعيين كلمة مرور الآن.");

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.Status = "Active";

            await _context.SaveChangesAsync();

            return Ok(new { message = "🔐 تم تعيين كلمة مرور جديدة بنجاح." });
        }


        [HttpPut("UpdatePassenger/{id}")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> UpdatePassenger(int id, [FromBody] UserUpdateDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id && u.Role == "Passenger");
            if (user == null) return NotFound("الراكب غير موجود");

            user.FullName = dto.FullName ?? user.FullName;
            user.PhoneNumber = dto.PhoneNumber ?? user.PhoneNumber;
            user.Status = dto.Status ?? user.Status;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"✅ تم تعديل بيانات الراكب ({user.FullName}) بنجاح." });
        }

        [HttpPost("PunishPassenger/{id}")]
        [Authorize(Roles = "Admin,Supervisor")]
        public async Task<IActionResult> PunishPassenger(int id, [FromBody] decimal penalty)
        {
            var passenger = await _context.Passengers.FirstOrDefaultAsync(p => p.UserID == id);
            if (passenger == null) return NotFound("الراكب غير موجود");

            passenger.Balance -= penalty;
            if (passenger.Balance < 0) passenger.Balance = 0;

            await _context.SaveChangesAsync();

            return Ok(new { message = $"⚠️ تم خصم {penalty} دينار من رصيد الراكب ({id})." });
        }

        // ✅ التحقق من وجود رقم هاتف في النظام (لواجهة التسجيل)
        [HttpGet("CheckPhoneExists/{phoneNumber}")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckPhoneExists(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return BadRequest(new { exists = false, message = "رقم الهاتف غير صالح." });

            bool exists = await _context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber);
            return Ok(new { exists });
        }

        [HttpGet("Passengers")]
        [Authorize(Roles = "Admin,Supervisor")]
          public async Task<ActionResult<object>> GetPassengers(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
             [FromQuery] int pageSize = 10)
        {
            var query = _context.Users.Where(u => u.Role == "Passenger");

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(search) ||
                    u.PhoneNumber.Contains(search));
            }

            var total = await query.CountAsync();

            var passengers = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.UserID,
                    u.FullName,
                    u.PhoneNumber,
                    u.Role,
                    u.Status,
                    u.ReferralCode,
                    u.ReferredBy,
                    u.CreatedAt,
                    // ✅ هذا السطر فقط تمت إضافته لعرض الرصيد
                    Balance = _context.Passengers
                        .Where(p => p.UserID == u.UserID)
                        .Select(p => p.Balance)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new { total, passengers });
        }
        [HttpGet("CheckReferral")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckReferral([FromQuery] string code)
        {
            if (string.IsNullOrEmpty(code))
                return BadRequest(new { exists = false, message = "الكود فارغ" });

            bool exists = await _context.Users.AnyAsync(u => u.ReferralCode == code);

            return Ok(new { exists });
        }
    }
}
