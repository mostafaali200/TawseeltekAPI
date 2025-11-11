using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TawseeltekAPI.Models
{
    public class UserDevice
    {
        [Key]   // 👈 المفتاح الأساسي
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DeviceID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "";

        [Required]
        [MaxLength(500)]
        public string DeviceToken { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
