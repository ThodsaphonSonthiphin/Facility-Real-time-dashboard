using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;

/// <summary>
/// ADRs facility-0215 and 0222: maps the dated scheduling records. AppDbContext keeps one inline OnModelCreating, so
/// this file supplies that one method instead of per-entity IEntityTypeConfiguration classes.
/// </summary>
public static class SchedulingModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SupervisorScopePeriod>(e =>
        {
            e.ToTable("supervisor_scope_periods", t => t.HasCheckConstraint(
                "ck_supervisor_scope_periods_effective_range", EffectiveRangeShape));
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.SupervisorId, x.BuildingId, x.Shift, x.EffectiveFrom }).IsUnique();
            e.HasIndex(x => new { x.BuildingId, x.Shift, x.EffectiveFrom });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<CleanerProfilePeriod>(e =>
        {
            e.ToTable("cleaner_profile_periods", t => t.HasCheckConstraint(
                "ck_cleaner_profile_periods_effective_range", EffectiveRangeShape));
            e.Property(x => x.Kind).HasConversion(new UpperSnakeEnumConverter<CleanerKind>()).HasMaxLength(10);
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.UserId, x.Kind, x.EffectiveFrom }).IsUnique();
            e.HasIndex(x => x.HomeBuildingId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Building>().WithMany().HasForeignKey(x => x.HomeBuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleBaseline>(e =>
        {
            e.ToTable("schedule_baselines", t => t.HasCheckConstraint(
                "ck_schedule_baselines_effective_range", EffectiveRangeShape));
            e.Property(x => x.Kind).HasConversion(new UpperSnakeEnumConverter<BaselineKind>()).HasMaxLength(10);
            e.HasIndex(x => new { x.UserId, x.Kind, x.EffectiveFrom }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Days).WithOne(x => x.Baseline).HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleBaselineDay>(e =>
        {
            e.ToTable("schedule_baseline_days", t => t.HasCheckConstraint(
                "ck_schedule_baseline_days_shape",
                "(is_day_off = 1 AND area_id IS NULL AND break_group IS NULL) " +
                "OR (is_day_off = 0 AND (break_group IS NULL OR area_id IS NOT NULL))"));
            e.HasIndex(x => new { x.BaselineId, x.Weekday }).IsUnique();
            e.HasIndex(x => x.AreaId);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<WeeklyDayOff>(e =>
        {
            e.ToTable("weekly_days_off");
            e.HasIndex(x => new { x.UserId, x.WeekStart }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Choices).WithOne(x => x.WeeklyDayOff).HasForeignKey(x => x.WeeklyDayOffId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<WeeklyDayOffChoice>(e =>
        {
            e.ToTable("weekly_day_off_choices");
            e.HasIndex(x => new { x.WeeklyDayOffId, x.OffDate }).IsUnique();
        });

        model.Entity<ScheduleException>(e =>
        {
            e.ToTable("schedule_exceptions", t => t.HasCheckConstraint(
                "ck_schedule_exceptions_shape",
                "(kind = 'AREA' AND area_id IS NOT NULL AND break_group IS NULL AND starts_at IS NULL AND ends_at IS NULL) " +
                "OR (kind = 'BREAK_GROUP' AND area_id IS NULL AND break_group IS NOT NULL AND starts_at IS NULL AND ends_at IS NULL) " +
                "OR (kind = 'LEAVE' AND area_id IS NULL AND break_group IS NULL AND starts_at IS NOT NULL AND ends_at IS NOT NULL)"));
            e.Property(x => x.Kind).HasConversion(new UpperSnakeEnumConverter<ScheduleExceptionKind>()).HasMaxLength(20);
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.UserId, x.StartsAt });

            // One live override per slot and kind: the database computes the key so a duplicate cannot be stored, while a
            // cancelled row (and every leave row, which may legitimately overlap) stores NULL and never blocks another.
            e.Property(x => x.ActiveKey).HasComputedColumnSql(
                "CASE WHEN kind IN ('AREA', 'BREAK_GROUP') AND is_cancelled = 0 THEN 1 END", stored: true);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift, x.Kind, x.ActiveKey }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ReliefAssignment>(e =>
        {
            e.ToTable("relief_assignments");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.CancellationReason).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.AreaId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.ReplacedUserId, x.ShiftDate, x.Shift });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReplacedUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Building>().WithMany().HasForeignKey(x => x.SourceBuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SourceSupervisorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AreaTransfer>(e =>
        {
            e.ToTable("area_transfers", t => t.HasCheckConstraint(
                "ck_area_transfers_other_area", "from_area_id <> to_area_id"));
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.CancellationReason).HasMaxLength(500);
            e.HasIndex(x => new { x.PersonId, x.ShiftDate, x.Shift });
            e.HasIndex(x => new { x.ToAreaId, x.ShiftDate, x.Shift });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.FromAreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.ToAreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CancelledById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AreaTransferConfirmation>(e =>
        {
            e.ToTable("area_transfer_confirmations");
            e.Property(x => x.Source).HasConversion(new UpperSnakeEnumConverter<AttendanceSource>()).HasMaxLength(20);
            MapGps(e);
            e.HasIndex(x => x.TransferId).IsUnique();
            e.HasIndex(x => x.AreaId);
            e.HasIndex(x => x.SignId);
            e.HasOne<AreaTransfer>().WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Sign>().WithMany().HasForeignKey(x => x.SignId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AttendanceSlotLifecycle>(e =>
        {
            e.ToTable("attendance_slot_lifecycles");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.Version).HasConversion(new UpperSnakeEnumConverter<AttendanceLifecycleVersion>()).HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.ShiftDate, x.Shift }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActivatedById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<AttendanceGap>(e =>
        {
            e.ToTable("attendance_gaps");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.Property(x => x.EventType).HasConversion(new UpperSnakeEnumConverter<AttendanceEvent>()).HasMaxLength(20);
            e.Property(x => x.Status).HasConversion(new UpperSnakeEnumConverter<AttendanceGapStatus>()).HasMaxLength(10);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.ShiftDate, x.Shift, x.EventType }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Area>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleAnchor>(e =>
        {
            e.ToTable("schedule_anchors");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.PersonId, x.ShiftDate, x.Shift }).IsUnique();
            e.HasIndex(x => new { x.ShiftDate, x.Shift });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleBaselineAnchor>(e =>
        {
            e.ToTable("schedule_baseline_anchors");
            e.HasIndex(x => x.PersonId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleScopeAnchor>(e =>
        {
            e.ToTable("schedule_scope_anchors");
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10);
            e.HasIndex(x => new { x.BuildingId, x.Shift }).IsUnique();
            e.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleCommandReceipt>(e =>
        {
            e.ToTable("schedule_command_receipts");
            e.Property(x => x.Kind).HasMaxLength(50).IsRequired();
            e.Property(x => x.PayloadFingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExpectedRevisionsJson).HasColumnType("json");
            e.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
            e.Property(x => x.ResultJson).HasColumnType("json");
            e.HasIndex(x => x.CommandId).IsUnique();
            e.HasIndex(x => new { x.ActorId, x.CommittedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<ScheduleChange>(e =>
        {
            e.ToTable("schedule_changes");
            e.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
            e.Property(x => x.Operation).HasMaxLength(20).IsRequired();
            e.Property(x => x.BeforeJson).HasColumnType("json");
            e.Property(x => x.AfterJson).HasColumnType("json");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => x.ReceiptId);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Receipt).WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Every effective period is half-open, so an end that is not after the start describes no dates at all. Storing one
    /// would occupy the natural key while protecting nothing; the store rejects it before writing for a readable error.
    /// </summary>
    private const string EffectiveRangeShape = "effective_until IS NULL OR effective_until > effective_from";

    /// <summary>database.html GPS column set, the same mapping AppDbContext's private helper applies to legacy records.</summary>
    private static void MapGps<T>(EntityTypeBuilder<T> e) where T : class, IGpsStamped
    {
        e.Property(x => x.Latitude).HasPrecision(9, 6);
        e.Property(x => x.Longitude).HasPrecision(9, 6);
    }
}
