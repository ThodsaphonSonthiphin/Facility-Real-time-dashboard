using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    public DbSet<ShiftAttendance> ShiftAttendances => Set<ShiftAttendance>();
    public DbSet<BlockedScan> BlockedScans => Set<BlockedScan>();
    public DbSet<CoverAssignment> CoverAssignments => Set<CoverAssignment>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        configurationBuilder.Properties<TimeOnly>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");
    }

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
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.LocationSource).HasConversion(new UpperSnakeEnumConverter<LocationSource>()).HasMaxLength(10);
            e.Property(x => x.RadiusM).HasDefaultValue((short)50);
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

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint(
                "ck_users_login_name",
                "(role = 'ADMIN' AND username IS NOT NULL) OR (role <> 'ADMIN' AND employee_id IS NOT NULL)"));
            e.Property(x => x.Role).HasConversion(new UpperSnakeEnumConverter<UserRole>()).HasMaxLength(20);
            e.Property(x => x.EmployeeId).HasMaxLength(20);
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            e.Property(x => x.SecretHash).HasMaxLength(255).IsRequired();
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);

            // An INT slot instead of database.html's 'area_id:shift' text: same uniqueness, and the SQL runs on MySQL and SQLite.
            // A deactivated account gets NULL, which a UNIQUE index allows any number of times.
            e.Property(x => x.CleanerSlot).HasComputedColumnSql(
                "CASE WHEN role = 'CLEANER' AND is_active = 1 THEN area_id * 2 + (CASE WHEN shift = 'NIGHT' THEN 1 ELSE 0 END) END",
                stored: true);
            e.Property(x => x.SupervisorSlot).HasComputedColumnSql(
                "CASE WHEN role = 'SUPERVISOR' AND is_active = 1 THEN building_id * 2 + (CASE WHEN shift = 'NIGHT' THEN 1 ELSE 0 END) END",
                stored: true);

            e.HasIndex(x => x.EmployeeId).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.CleanerSlot).IsUnique();
            e.HasIndex(x => x.SupervisorSlot).IsUnique();
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
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
            MapGps(e);
            e.HasOne(x => x.CoverAssignment).WithMany().HasForeignKey(x => x.CoverAssignmentId).OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<ShiftAttendance>(e =>
        {
            e.ToTable("shift_attendances");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.EventType).HasConversion(new UpperSnakeEnumConverter<AttendanceEvent>()).HasMaxLength(20);
            e.Property(x => x.Source).HasConversion(new UpperSnakeEnumConverter<AttendanceSource>()).HasMaxLength(20);
            MapGps(e);
            // facility-0026: each event once per shift, the first press wins
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift, x.EventType }).IsUnique();
            e.HasIndex(x => new { x.ShiftDate, x.Shift, x.AreaId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Sign>().WithMany().HasForeignKey(x => x.SignId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BlockedScan>(e =>
        {
            e.ToTable("blocked_scans");
            e.Property(x => x.Reason).HasConversion(new UpperSnakeEnumConverter<BlockReason>()).HasMaxLength(20);
            MapGps(e);
            e.HasIndex(x => x.ScannedAt);
            e.HasIndex(x => new { x.SignId, x.ScannedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Sign>().WithMany().HasForeignKey(x => x.SignId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CoverAssignment>(e =>
        {
            e.ToTable("cover_assignments");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.AreaId, x.ShiftDate, x.Shift });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(300).IsRequired();
            e.Property(x => x.BeforeJson).HasColumnType("json");
            e.Property(x => x.AfterJson).HasColumnType("json");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => x.OccurredAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.UseSnakeCaseColumns();
    }

    /// <summary>database.html GPS column set: DECIMAL(9,6) coordinates; SMALLINT accuracy and distance follow from short.</summary>
    private static void MapGps<T>(EntityTypeBuilder<T> e) where T : class, IGpsStamped
    {
        e.Property(x => x.Latitude).HasPrecision(9, 6);
        e.Property(x => x.Longitude).HasPrecision(9, 6);
    }
}
