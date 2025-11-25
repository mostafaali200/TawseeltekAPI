using Microsoft.EntityFrameworkCore;
using TawseeltekAPI.Models;
using WebApplication1.Models;

namespace TawseeltekAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Passenger> Passengers { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Price> Prices { get; set; }
        public DbSet<Ride> Rides { get; set; }
        public DbSet<RidePassenger> RidePassengers { get; set; }
        public DbSet<Penalty> Penalties { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<DriverBalanceLog> DriverBalanceLogs { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<PhoneVerification> PhoneVerifications { get; set; }
        public DbSet<VerificationToken> VerificationTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // ربط DriverBalanceLog مع User (CreatedBy)
            modelBuilder.Entity<DriverBalanceLog>()
                .HasOne(l => l.CreatedBy)
                .WithMany()
                .HasForeignKey(l => l.CreatedByID)
                .OnDelete(DeleteBehavior.Restrict);
            // ربط DriverBalanceLog مع Driver
            modelBuilder.Entity<DriverBalanceLog>()
                .HasOne(l => l.Driver)
                .WithMany()
                .HasForeignKey(l => l.DriverID)
                .OnDelete(DeleteBehavior.Cascade);
            // ✅ تعريف المفتاح الأساسي لـ UserDevice
            modelBuilder.Entity<UserDevice>()
                .HasKey(d => d.DeviceID);

            // ✅ تأكيد إن UserID مرتبط بالـ User
            modelBuilder.Entity<UserDevice>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.Cascade);
            base.OnModelCreating(modelBuilder);

            // Ride.PricePerSeat دقة = 18 خانة، 2 منها بعد الفاصلة
            modelBuilder.Entity<Ride>()
                .Property(r => r.PricePerSeat)
                .HasPrecision(18, 2);
            // === Indexes ===
            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            modelBuilder.Entity<Price>()
                .HasIndex(p => new { p.FromCityID, p.ToCityID })
                .IsUnique();

            // === Decimal Precision Fixes ===
            modelBuilder.Entity<Driver>()
                .Property(d => d.Balance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<DriverBalanceLog>()
                .Property(l => l.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Penalty>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Price>()
                .Property(p => p.PriceValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<RidePassenger>()
                .Property(rp => rp.Fare)
                .HasPrecision(18, 2);

            // === Price ↔ City ===
            modelBuilder.Entity<Price>()
                .HasOne(p => p.FromCity)
                .WithMany(c => c.PricesFrom)
                .HasForeignKey(p => p.FromCityID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Price>()
                .HasOne(p => p.ToCity)
                .WithMany(c => c.PricesTo)
                .HasForeignKey(p => p.ToCityID)
                .OnDelete(DeleteBehavior.Restrict);

            // === Ride ↔ Cities ===
            modelBuilder.Entity<Ride>()
                .HasOne(r => r.FromCity)
                .WithMany(c => c.RidesFrom)
                .HasForeignKey(r => r.FromCityID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ride>()
                .HasOne(r => r.ToCity)
                .WithMany(c => c.RidesTo)
                .HasForeignKey(r => r.ToCityID)
                .OnDelete(DeleteBehavior.Restrict);

            // === Driver ↔ User ===
            modelBuilder.Entity<Driver>()
                .HasOne(d => d.User)
                .WithOne(u => u.Driver)
                .HasForeignKey<Driver>(d => d.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // === Passenger ↔ User ===
            modelBuilder.Entity<Passenger>()
                .HasOne(p => p.User)
                .WithOne(u => u.Passenger)
                .HasForeignKey<Passenger>(p => p.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // === Ride ↔ Driver ===
            modelBuilder.Entity<Ride>()
                .HasOne(r => r.Driver)
                .WithMany(d => d.Rides)
                .HasForeignKey(r => r.DriverID)
                .OnDelete(DeleteBehavior.Cascade);

            // === RidePassenger ↔ Ride & Passenger ===
            modelBuilder.Entity<RidePassenger>()
                .HasOne(rp => rp.Ride)
                .WithMany(r => r.RidePassengers)
                .HasForeignKey(rp => rp.RideID)
                .OnDelete(DeleteBehavior.Restrict); // تجنب Multiple Cascade Paths

            modelBuilder.Entity<RidePassenger>()
                .HasOne(rp => rp.Passenger)
                .WithMany(p => p.RidePassengers)
                .HasForeignKey(rp => rp.PassengerID)
                .OnDelete(DeleteBehavior.Restrict); // تجنب Multiple Cascade Paths

            // === Penalty ↔ Driver & CreatedBy ===
            modelBuilder.Entity<Penalty>()
                .HasOne(p => p.Driver)
                .WithMany(d => d.Penalties)
                .HasForeignKey(p => p.DriverID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Penalty>()
                .HasOne(p => p.CreatedBy)
                .WithMany(u => u.PenaltiesCreated)
                .HasForeignKey(p => p.CreatedByID)
                .OnDelete(DeleteBehavior.Restrict);

            // === Notification ↔ User ===
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            // === Message ↔ Sender & Receiver ===
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverID)
                .OnDelete(DeleteBehavior.Restrict);

            // === DriverBalanceLog ↔ Driver & CreatedBy ===
            modelBuilder.Entity<DriverBalanceLog>()
                .HasKey(l => l.LogID);
            modelBuilder.Entity<Passenger>()
              .Property(p => p.Balance)
              .HasPrecision(18, 2);

            modelBuilder.Entity<DriverBalanceLog>()
                .HasOne(l => l.Driver)
                .WithMany(d => d.BalanceLogs)
                .HasForeignKey(l => l.DriverID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DriverBalanceLog>()
                .HasOne(l => l.CreatedBy)
                .WithMany(u => u.BalanceLogsCreated)
                .HasForeignKey(l => l.CreatedByID)
                .OnDelete(DeleteBehavior.Restrict);
        }


    }
}
