using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

/// <summary>The tables of docs/design/database.html that the pilot's first features use. Nothing is ever deleted: rows are deactivated.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<ServicePoint> ServicePoints => Set<ServicePoint>();
    public DbSet<Sign> Signs => Set<Sign>();
    public DbSet<PointRoundWindow> PointRoundWindows => Set<PointRoundWindow>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ScanRecord> ScanRecords => Set<ScanRecord>();
    public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Building>(e =>
        {
            e.ToTable("buildings");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Area>(e =>
        {
            e.ToTable("areas");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.ShiftPattern).HasConversion(new UpperSnakeEnumConverter<ShiftPattern>()).HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServicePoint>(e =>
        {
            e.ToTable("service_points");
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(x => new { x.AreaId, x.SortOrder });
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sign>(e =>
        {
            e.ToTable("signs");
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.QrToken).HasMaxLength(100).IsRequired();
            e.Property(x => x.CheckinAreaId)
                .HasComputedColumnSql("CASE WHEN service_point_id IS NULL THEN area_id END", stored: true);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.QrToken).IsUnique();
            e.HasIndex(x => x.ServicePointId).IsUnique();
            e.HasIndex(x => x.CheckinAreaId).IsUnique();
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServicePoint).WithMany().HasForeignKey(x => x.ServicePointId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PointRoundWindow>(e =>
        {
            e.ToTable("point_round_windows");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.ServicePointId, x.Shift, x.StartTime });
            e.HasOne(x => x.ServicePoint).WithMany().HasForeignKey(x => x.ServicePointId).OnDelete(DeleteBehavior.Restrict);
        });

        // Phase-1 shape until the accounts task reshapes it (Task 5)
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
        });

        // ADR facility-0014
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanRecord>(e =>
        {
            e.ToTable("scan_records");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.Placement).HasConversion(new UpperSnakeEnumConverter<Placement>()).HasMaxLength(20);
            e.Property(x => x.Status).HasConversion(new UpperSnakeEnumConverter<CleaningStatus>()).HasMaxLength(10);
            e.Property(x => x.IssueTags).HasMaxLength(200);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex(x => new { x.ServicePointId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.UserId, x.SubmittedAt });
            e.HasIndex(x => x.SubmittedAt);
            e.HasOne(x => x.ServicePoint).WithMany().HasForeignKey(x => x.ServicePointId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sign).WithMany().HasForeignKey(x => x.SignId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RoundWindow).WithMany().HasForeignKey(x => x.RoundWindowId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InspectionRecord>(e =>
        {
            e.ToTable("inspections", t => t.HasCheckConstraint(
                "ck_inspections_rework_has_defect", "result <> 'REWORK' OR defect IS NOT NULL"));
            e.Property(x => x.Result).HasConversion(new UpperSnakeEnumConverter<InspectionResult>()).HasMaxLength(10);
            e.Property(x => x.Defect).HasMaxLength(1000);
            e.HasIndex(x => new { x.ServicePointId, x.InspectedAt });
            e.HasIndex(x => new { x.SupervisorId, x.InspectedAt });
            e.HasOne(x => x.ScanRecord).WithMany().HasForeignKey(x => x.ScanRecordId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ServicePoint>().WithMany().HasForeignKey(x => x.ServicePointId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.UseSnakeCaseColumns();
    }
}
