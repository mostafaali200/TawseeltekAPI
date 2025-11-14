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
    public async Task<IActionResult> FindBestDrivers([FromBody] FindDriversDTO dto)
    {
        if (dto == null)
            return BadRequest("Invalid request.");

        double passengerLat = dto.FromLat;
        double passengerLng = dto.FromLng;
        DateTime desiredTime = dto.DesiredTime;

        // ===============================
        // 1️⃣ تحديد المنطقة + المدى الأساسي
        // ===============================
        string area = DetectArea(passengerLat, passengerLng);

        double baseRadiusKm = area switch
        {
            "Amman" => 7,
            "Irbid" => 7,
            "Zarqa" => 6,
            "Aqaba" => 12,
            "Valleys" => 18,
            "Villages" => 20,
            _ => 10
        };

        // ===============================
        // 2️⃣ TIME SMART BOOST
        // ===============================
        int hour = desiredTime.Hour;

        if (hour >= 7 && hour <= 10) baseRadiusKm *= 0.8;      // Morning Peak
        else if (hour >= 16 && hour <= 19) baseRadiusKm *= 0.8; // Evening Peak
        else if (hour >= 22 || hour <= 5) baseRadiusKm *= 1.7;  // Late Night
        else baseRadiusKm *= 1.2;

        // ===============================
        // 3️⃣ جلب الرحلات الفعّالة
        // ===============================
        var activeRides = await _context.Rides
            .Include(r => r.Driver)
            .ThenInclude(d => d.User)
            .Where(r =>
                r.Status == "Active" &&
                r.Driver.Verified &&
                r.Driver.AvailabilityStatus == "Available" &&
                r.Driver.Latitude != null &&
                r.Driver.Longitude != null
            )
            .ToListAsync();

        if (!activeRides.Any())
            return Ok(new List<object>());

        var result = new List<dynamic>();

        // ===============================
        // 4️⃣ Turbo Scoring System
        // ===============================
        foreach (var ride in activeRides)
        {
            var driver = ride.Driver;

            // 4.1 — Distance Score (Turbo)
            double distanceKm = GeoUtils.Haversine(
                passengerLat, passengerLng,
                driver.Latitude.Value, driver.Longitude.Value
            );

            if (distanceKm > baseRadiusKm)
                continue;

            double distanceScore =
                distanceKm <= 2 ? 60 :
                distanceKm <= 5 ? 40 :
                distanceKm <= 10 ? 20 : 5;

            // 4.2 — Time Score
            double timeDiff = Math.Abs((ride.DepartureTime - desiredTime).TotalMinutes);

            if (timeDiff > 90)
                continue; // ⛔ لا نعرض رحلات الفارق بينها كبير

            double timeScore =
                timeDiff <= 10 ? 60 :
                timeDiff <= 20 ? 40 :
                timeDiff <= 45 ? 20 :
                10;

            // 4.3 — Direction Turbo Score
            double directionScore = 0;

            if (!string.IsNullOrEmpty(ride.RoutePolyline))
            {
                var path = PolylineDecoder.DecodePolyline(ride.RoutePolyline);
                var distToPath = GeoUtils.DistanceToPolyline(passengerLat, passengerLng, path);

                directionScore =
                    distToPath <= 1 ? 50 :
                    distToPath <= 3 ? 25 :
                    10;
            }

            // 4.4 — Driver Freshness Boost (حديث النشاط)
            double freshness = (DateTime.UtcNow - driver.LastUpdated).TotalMinutes;
            double freshnessScore =
                freshness <= 1 ? 15 :
                freshness <= 3 ? 10 :
                freshness <= 5 ? 5 : 0;

            // 4.5 — TURBO FINAL SCORE
            double finalScore = distanceScore + timeScore + directionScore + freshnessScore;

            result.Add(new
            {
                ride.RideID,
                ride.Driver.DriverID,
                ride.Driver.User.FullName,
                ride.Driver.VehicleType,
                ride.Driver.PlateNumber,
                driver.Latitude,
                driver.Longitude,
                RideDeparture = ride.DepartureTime,
                DistanceKm = Math.Round(distanceKm, 2),
                TimeDifference = Math.Round(timeDiff, 1),
                Score = Math.Round(finalScore, 1)
            });
        }

        // ===============================
        // 5️⃣ توسيع المدى لو النتائج قليلة
        // ===============================
        if (result.Count < 3)
        {
            baseRadiusKm *= 1.8;
        }

        // ===============================
        // 6️⃣ ترتيب حسب الأفضلية
        // ===============================
        var sorted = result
            .OrderByDescending(r => r.Score)
            .Take(20)
            .ToList();

        return Ok(sorted);
    }



    // ===========================================
    // 🏙 Detect Area — متوافق مع الأردن بالكامل
    // ===========================================
    private string DetectArea(double lat, double lng)
    {
        if (lat > 31.7 && lat < 32.2 && lng > 35.7 && lng < 36.1)
            return "Amman";

        if (lat > 32.5 && lat < 32.7 && lng > 35.8 && lng < 36.1)
            return "Irbid";

        if (lat > 32.0 && lat < 32.15 && lng > 36.0 && lng < 36.2)
            return "Zarqa";

        if (lat > 29.4 && lat < 29.7 && lng > 34.9 && lng < 35.1)
            return "Aqaba";

        if (lng < 35.5)
            return "Valleys";

        return "Villages";
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
