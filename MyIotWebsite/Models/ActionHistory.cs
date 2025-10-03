using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyIotWebsite.Models
{
    public class ActionHistory
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("device_name")]
        [Required]
        [StringLength(100)]
        public required string DeviceName { get; set; } = string.Empty;
        
        [Column("ison")]
        public bool IsOn { get; set; }
        
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}