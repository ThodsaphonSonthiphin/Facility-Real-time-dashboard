using FacilityRealtime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ServicePoint> ServicePoints => Set<ServicePoint>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ScanRecord> ScanRecords => Set<ScanRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ServicePoint
        modelBuilder.Entity<ServicePoint>(entity =>
        {
            entity.ToTable("service_points");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Location).HasMaxLength(255).IsRequired();
            entity.Property(e => e.QrToken).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.QrToken).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // ScanRecord (ADR 0006)
        modelBuilder.Entity<ScanRecord>(entity =>
        {
            entity.ToTable("scan_records");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.IssueTags).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            // High performance composite index for latest scan per service point
            entity.HasIndex(e => new { e.ServicePointId, e.ScannedAt });

            entity.HasOne(e => e.ServicePoint)
                  .WithMany()
                  .HasForeignKey(e => e.ServicePointId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
