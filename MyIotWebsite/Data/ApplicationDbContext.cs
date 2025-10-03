using Microsoft.EntityFrameworkCore;
using MyIotWebsite.Models;

namespace MyIotWebsite.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<SensorData> SensorData { get; set; }
        public DbSet<ActionHistory> ActionHistories { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SensorData>().ToTable("sensordata");
            modelBuilder.Entity<ActionHistory>().ToTable("actionhistories_new");
        }
    }
}