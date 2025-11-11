using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Data;
using TawseeltekAPI.Dto;
using TawseeltekAPI.Hubs; // 👈 Hub للإشعارات
using TawseeltekAPI.Models;
using TawseeltekAPI.Services;
using TawseeltekAPI.Utils;

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
