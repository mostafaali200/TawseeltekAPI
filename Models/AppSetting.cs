using System.ComponentModel.DataAnnotations;

namespace TawseeltekAPI.Models
{
    public class AppSetting
    {
        [Key] // ✅ هذا السطر هو الحل
        public int SettingID { get; set; }

        [Required]
        public string KeyName { get; set; }

        [Required]
        public string Value { get; set; }

        public string? Description { get; set; }
    }
}
