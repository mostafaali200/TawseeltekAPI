using TawseeltekAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace TawseeltekAPI.Services
{
    public class AppSettingsService
    {
        private readonly AppDbContext _context;
        public AppSettingsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetRideDeductionAmountAsync()
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.KeyName == "RideDeductionAmount");
            return setting != null ? decimal.Parse(setting.Value) : 0.5m;
        }

        public async Task<bool> UpdateSettingAsync(string key, string value)
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.KeyName == key);
            if (setting == null) return false;
            setting.Value = value;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
