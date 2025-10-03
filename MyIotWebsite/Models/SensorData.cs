using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyIotWebsite.Models
{
    public class SensorData
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("temperature")]
        public double Temperature { get; set; }

        [Column("humidity")]
        public double Humidity { get; set; }

        [Column("light")] 
        public double Light { get; set; }

        [Column("timestamp")] 
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}