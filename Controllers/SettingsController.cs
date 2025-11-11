using Microsoft.AspNetCore.Mvc;
using TawseeltekAPI.Services;

namespace TawseeltekAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly AppSettingsService _settings;

        public SettingsController(AppSettingsService settings)
        {
            _settings = settings;
        }

        [HttpGet("RideDeduction")]
        public async Task<IActionResult> GetRideDeduction()
        {
            var amount = await _settings.GetRideDeductionAmountAsync();
            return Ok(new { RideDeduction = amount });
        }

        [HttpPut("RideDeduction")]
        public async Task<IActionResult> UpdateRideDeduction([FromBody] decimal newValue)
        {
            var updated = await _settings.UpdateSettingAsync("RideDeductionAmount", newValue.ToString());
            if (!updated) return NotFound("Setting not found");
            return Ok(new { message = $"تم تحديث الخصم إلى {newValue} دينار" });
        }
    }
}
