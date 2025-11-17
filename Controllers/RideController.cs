using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Dto;
using TawseeltekAPI.Hubs; // 👈 Hub للإشعارات
using TawseeltekAPI.Models;
using TawseeltekAPI.Services;
using TawseeltekAPI.Utils;
using WebApplication1.Dto;

[ApiController]
[Route("api/[controller]")]
public class RideController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<RideHub> _rideHub;

    public RideController(AppDbContext context, IHubContext<RideHub> rideHub)
    {
        _context = context;
        _rideHub = rideHub;
    }

    // ===========================================
    // إنشاء رحلة جديدة
    // ===========================================
    [HttpPost("CreateRide")]
    public async Task<ActionResult<Ride>> CreateRide([FromBody] RideCreateDTO dto)
    {
        var driver = await _context.Drivers.FindAsync(dto.DriverID);
        if (driver == null || !driver.Verified)
            return BadRequest("Driver not found or not verified.");

        var ride = new Ride
        {
            DriverID = dto.DriverID,
            FromCityID = dto.FromCityID,
            ToCityID = dto.ToCityID,
            DepartureTime = dto.DepartureTime,
            Status = "Active", // ✅ الرحلة بتظل نشطة، مش Pending
            CreatedAt = DateTime.UtcNow,
            RoutePolyline = dto.RoutePolyline,
            Capacity = dto.Capacity,
            PricePerSeat = dto.PricePerSeat
        };

        _context.Rides.Add(ride);
        await _context.SaveChangesAsync();

        return Ok(ride);
    }

    // ===========================================
    // جلب جميع الرحلات
    // ===========================================
    [HttpGet("AllRides")]
    public async Task<ActionResult<IEnumerable<Ride>>> GetAllRides()
    {
        var rides = await _context.Rides
            .Include(r => r.Driver)
            .ThenInclude(d => d.User)
            .Include(r => r.RidePassengers)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(rides);
    }

    // ===========================================
    // إضافة راكب للرحلة
    // ===========================================
    [HttpPost("AddPassenger")]
    public async Task<ActionResult<RidePassenger>> AddPassenger([FromBody] RidePassengerDTO dto)
    {
        var ride = await _context.Rides
                                 .Include(r => r.RidePassengers)
                                 .FirstOrDefaultAsync(r => r.RideID == dto.RideID);
        if (ride == null) return NotFound("Ride not found.");

        if (ride.SeatsTaken + 1 > ride.Capacity)
            return BadRequest("No available seats.");

        var passenger = await _context.Users.FindAsync(dto.PassengerID);
        if (passenger == null || passenger.Role != "Passenger")
            return BadRequest("Passenger not found or invalid role.");

        var ridePassenger = new RidePassenger
        {
            RideID = dto.RideID,
            PassengerID = dto.PassengerID,
            Fare = dto.Fare,
            Status = "Pending", // ✅ بانتظار موافقة السائق
            CreatedAt = DateTime.UtcNow
        };

        ride.SeatsTaken += 1;
        _context.RidePassengers.Add(ridePassenger);
        await _context.SaveChangesAsync();

        return Ok(ridePassenger);
    }

    // ===========================================
    // API جديدة: الطلبات المعلقة لسائق
    // ===========================================
    [HttpGet("Pending/{driverId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetPendingRidesForDriver(int driverId)
    {
        var rides = await _context.RidePassengers
            .Include(rp => rp.Ride)
            .ThenInclude(r => r.Driver)
            .Where(rp => rp.Ride.DriverID == driverId && rp.Status == "Pending")
            .Select(rp => new
            {
                rp.RidePassengerID,
                rp.PassengerID,
                PassengerName = _context.Users.Where(u => u.UserID == rp.PassengerID).Select(u => u.FullName).FirstOrDefault(),
                rp.RideID,
                rp.Status,
                rp.Fare,
                rp.CreatedAt
            })
            .ToListAsync();

        return Ok(rides);
    }

    // ===========================================
    // ✅ قبول راكب (من طرف السائق)
    // ===========================================
    [HttpPut("AcceptPassenger/{ridePassengerId}")]
    public async Task<IActionResult> AcceptPassenger(
     int ridePassengerId,
     [FromServices] AppSettingsService settingsService)
    {
        var ridePassenger = await _context.RidePassengers.FindAsync(ridePassengerId);
        if (ridePassenger == null) return NotFound("RidePassenger not found.");

        var ride = await _context.Rides.Include(r => r.Driver).FirstOrDefaultAsync(r => r.RideID == ridePassenger.RideID);
        if (ride == null || ride.Driver == null)
            return NotFound("Ride not found or driver missing.");

        // ✅ جلب الخصم من AppSettings
        var deductionAmount = await settingsService.GetRideDeductionAmountAsync();

        // ✅ التحقق من الرصيد
        if (ride.Driver.Balance < deductionAmount)
            return BadRequest("❌ لا يمكن قبول الراكب لأن رصيدك غير كافٍ.");

        // ✅ خصم المبلغ وتسجيل العملية
        ride.Driver.Balance -= deductionAmount;
        _context.DriverBalanceLogs.Add(new DriverBalanceLog
        {
            DriverID = ride.Driver.DriverID,
            Amount = deductionAmount,
            ActionType = "Debit",
            Description = $"خصم تلقائي ({deductionAmount} دينار) عند قبول راكب جديد",
            CreatedAt = DateTime.UtcNow
        });

        ridePassenger.Status = "Accepted";
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"✅ تم قبول الراكب وخصم {deductionAmount} دينار من الرصيد.",
            newBalance = ride.Driver.Balance
        });
    }

    // ===========================================
    // 🔥 FindBestDrivers TURBO — نسخة احترافية مثل Uber
    // ===========================================
    [HttpPost("FindBestDrivers")]
    public async Task<IActionResult> FindBestDriversTurbo([FromBody] FindDriversDTO dto)
    {
        if (dto == null) return BadRequest("Invalid request.");

        double pLat = dto.FromLat;
        double pLng = dto.FromLng;
        DateTime desiredTime = dto.DesiredTime;

        // 🚀 1) جلب السائقين المتاحين
        var drivers = await _context.Drivers
            .Include(d => d.User)
            .Include(d => d.Rides.Where(r => r.Status == "Active"))
                .ThenInclude(r => r.RidePassengers)
            .Where(d =>
                d.Verified &&
                d.AvailabilityStatus == "Available" &&
                d.Latitude != null &&
                d.Longitude != null
            )
            .ToListAsync();

        if (!drivers.Any())
            return Ok(new List<object>());

        // ⛔ لا تسمح بإرجاع صفر — نوسع النطاق تلقائيًا
        double searchRadius = DetectInitialRadius(pLat, pLng, desiredTime);

        var result = new List<dynamic>();

        foreach (var driver in drivers)
        {
            double distanceKm = GeoUtils.Haversine(
                pLat, pLng,
                driver.Latitude!.Value,
                driver.Longitude!.Value
            );

            if (distanceKm > searchRadius) continue;

            // 🔎 تحديد الرحلة الحالية
            var currentRide = driver.Rides.FirstOrDefault();

            double timeScore = 40;
            double directionScore = 40;
            double rideLoadScore = 20;

            if (currentRide != null)
            {
                // عدد الركاب معه الآن
                int passengerCount = currentRide.RidePassengers.Count;
                rideLoadScore = passengerCount switch
                {
                    0 => 20,
                    1 => 15,
                    2 => 10,
                    3 => 5,
                    _ => 0
                };

                // اتجاه مساره
                if (!string.IsNullOrEmpty(currentRide.RoutePolyline))
                {
                    var path = PolylineDecoder.DecodePolyline(currentRide.RoutePolyline);
                    var distToPath = GeoUtils.DistanceToPolyline(pLat, pLng, path);

                    directionScore =
                        distToPath <= 1 ? 50 :
                        distToPath <= 3 ? 25 :
                        5;
                }

                // الفرق الزمني
                double diff = Math.Abs((currentRide.DepartureTime - desiredTime).TotalMinutes);
                timeScore =
                    diff <= 10 ? 50 :
                    diff <= 20 ? 30 :
                    diff <= 40 ? 15 : 5;
            }
            else
            {
                // سائق بدون رحلة — ممتاز
                timeScore = 60;
                directionScore = 60;
                rideLoadScore = 30;
            }

            // قرب السائق
            double distanceScore =
                distanceKm <= 2 ? 60 :
                distanceKm <= 5 ? 40 :
                distanceKm <= 10 ? 20 : 10;

            // نشاط السائق
            double freshnessScore =
                (DateTime.UtcNow - driver.LastUpdated).TotalMinutes <= 2 ? 20 : 5;

            // ⭐ النتيجة النهائية
            double finalScore =
                timeScore +
                directionScore +
                rideLoadScore +
                distanceScore +
                freshnessScore;

            result.Add(new
            {
                driver.DriverID,
                driver.User.FullName,
                driver.VehicleType,
                driver.PlateNumber,
                driver.Latitude,
                driver.Longitude,
                DistanceKm = Math.Round(distanceKm, 2),
                Score = Math.Round(finalScore, 2),
                HasRide = currentRide != null
            });
        }

        // لو أقل من 5 نتائج → توسع تلقائي ذكي مرّة أخرى
        if (!result.Any())
        {
            searchRadius *= 2.5;
            return await RetryWithNewRadius(searchRadius, dto);
        }

        // ترتيب الأفضلية
        return Ok(result.OrderByDescending(r => r.Score).Take(20));
    }


    // ==========================================================
    // 🔥 ذكاء تحديد النطاق الأولي حسب المدينة والوقت
    // ==========================================================
    private double DetectInitialRadius(double lat, double lng, DateTime time)
    {
        double hour = time.Hour;

        bool isNight = hour >= 22 || hour <= 6;

        // 👇 الذكاء
        if (isNight) return 15;         // الليل نوسع المدى
        if (lng < 35.5) return 20;      // الأغوار والقرى
        return 10;                      // المدن الرئيسية
    }

    // ==========================================================
    // 🔁 إعادة البحث بمدى أوسع تلقائيًا
    // ==========================================================
    private async Task<IActionResult> RetryWithNewRadius(double radius, FindDriversDTO dto)
    {
        // هنا تضع إعادة استدعاء نفس الدالة مع radius جديد
        dto.SearchRadius = radius;
        return await FindBestDriversTurbo(dto);
    }




    // ===========================================
    // ✅ رفض راكب (من طرف السائق)
    // ===========================================
    [HttpPut("RejectPassenger/{ridePassengerId}")]
    public async Task<IActionResult> RejectPassenger(int ridePassengerId)
    {
        var ridePassenger = await _context.RidePassengers.FindAsync(ridePassengerId);
        if (ridePassenger == null) return NotFound("RidePassenger not found.");

        if (ridePassenger.Status != "Pending")
            return BadRequest("Passenger request is not pending.");

        ridePassenger.Status = "Rejected";
        await _context.SaveChangesAsync();

        // 👇 إشعار الراكب عبر SignalR
        await _rideHub.Clients.Group($"passenger-{ridePassenger.PassengerID}")
            .SendAsync("RideStatusUpdated", new
            {
                rideId = ridePassenger.RideID,
                passengerId = ridePassenger.PassengerID,
                status = "Rejected",
                timestamp = DateTime.UtcNow
            });


        return Ok(new { ridePassenger.RidePassengerID, ridePassenger.Status });
    }
}
