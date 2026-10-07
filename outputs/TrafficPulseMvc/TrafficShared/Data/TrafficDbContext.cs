using Microsoft.EntityFrameworkCore;
using TrafficShared.Models;

namespace TrafficShared.Data;

public class TrafficDbContext : DbContext
{
    public TrafficDbContext(DbContextOptions<TrafficDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Road> Roads => Set<Road>();
    public DbSet<TrafficReading> Readings => Set<TrafficReading>();
    public DbSet<TrafficAlert> Alerts => Set<TrafficAlert>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // שמות, אורכי עמודות, מפתחות וקשרים. אין SQL ידני.
        var users = modelBuilder.Entity<AppUser>();
        users.ToTable("Users");
        users.HasKey(u => u.Id);
        users.HasIndex(u => u.Email).IsUnique();
        users.Property(u => u.Email).HasMaxLength(254).IsRequired();
        users.Property(u => u.Name).HasMaxLength(80).IsRequired();
        users.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        users.Property(u => u.Role).HasMaxLength(16).IsRequired();
        users.Property(u => u.SecurityStamp).HasMaxLength(36).IsRequired();

        var roads = modelBuilder.Entity<Road>();
        roads.ToTable("Roads");
        roads.HasKey(r => r.Id);
        roads.Property(r => r.Name).HasMaxLength(80).IsRequired();
        roads.Property(r => r.Version).IsConcurrencyToken();

        var readings = modelBuilder.Entity<TrafficReading>();
        readings.ToTable("Readings");
        readings.HasKey(r => r.Id);
        readings.HasIndex(r => new { r.RoadId, r.CollectedAtUtc });
        readings.Property(r => r.Source).HasMaxLength(16).IsRequired();
        readings.HasOne<Road>().WithMany().HasForeignKey(r => r.RoadId).OnDelete(DeleteBehavior.Restrict);
        readings.Property(r => r.CollectedAtUtc).HasConversion(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var alerts = modelBuilder.Entity<TrafficAlert>();
        alerts.ToTable("Alerts");
        alerts.HasKey(a => a.Id);
        alerts.HasIndex(a => a.ReadingId).IsUnique();
        alerts.HasIndex(a => new { a.Status, a.CreatedAtUtc });
        alerts.Property(a => a.Reason).HasMaxLength(500).IsRequired();
        alerts.Property(a => a.Status).HasMaxLength(20).IsRequired();
        alerts.Property(a => a.Version).IsConcurrencyToken();
        alerts.HasOne<TrafficReading>().WithMany().HasForeignKey(a => a.ReadingId).OnDelete(DeleteBehavior.Restrict);
        alerts.HasOne<Road>().WithMany().HasForeignKey(a => a.RoadId).OnDelete(DeleteBehavior.Restrict);
        alerts.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UpdatedBy).OnDelete(DeleteBehavior.Restrict);
        alerts.Property(a => a.CreatedAtUtc).HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        alerts.Property(a => a.UpdatedAtUtc).HasConversion(value => value, value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        var favorites = modelBuilder.Entity<Favorite>();
        favorites.ToTable("Favorites");
        favorites.HasKey(f => new { f.UserId, f.RoadId });
        favorites.HasOne<AppUser>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);
        favorites.HasOne<Road>().WithMany().HasForeignKey(f => f.RoadId).OnDelete(DeleteBehavior.Restrict);
    }
}
