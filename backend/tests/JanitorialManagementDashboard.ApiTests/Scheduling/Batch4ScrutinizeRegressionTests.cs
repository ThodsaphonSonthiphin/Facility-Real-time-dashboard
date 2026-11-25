using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Regression tests for Batch 4 audit fixes across scheduling handlers, account/area/building sync,
/// transfer chain/slot revision, retention overnight guards, and API boundary handling.
/// </summary>
public class Batch4ScrutinizeRegressionTests
{
    [Fact]
    public async Task B6_Renaming_area_preserves_multi_area_regular_baseline()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var nightCleanerId = await AdminSetupApi.UserIdAsync(factory, "1000002");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));

        // Mon-Wed in AR01, Thu-Fri in AR03
        await factory.WithDbAsync(async db =>
        {
            var adminUser = await db.Users.SingleAsync(u => u.Username == "admin");
            var baseline = new ScheduleBaseline
            {
                UserId = cleanerId, Kind = BaselineKind.Regular, EffectiveFrom = new DateOnly(2026, 10, 1),
                Revision = 1, CreatedById = adminUser.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            baseline.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Monday, AreaId = ar01, BreakGroup = 1 });
            baseline.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Tuesday, AreaId = ar01, BreakGroup = 1 });
            baseline.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Wednesday, AreaId = ar01, BreakGroup = 1 });
            baseline.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Thursday, AreaId = ar03, BreakGroup = 1 });
            baseline.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Friday, AreaId = ar03, BreakGroup = 1 });
            db.ScheduleBaselines.Add(baseline);
            await db.SaveChangesAsync();
        });

        ScheduleWriteApi.Today(factory); // 2026-11-17
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var renamed = await admin.PutAsJsonAsync($"/api/admin/areas/{ar01}", new
        {
            name = "โซนชั้น 1 ปรับชื่อ",
            buildingId = buildingA,
            shiftPattern = "DayAndNight",
            dayCleanerId = cleanerId,
            nightCleanerId,
        });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        // Renaming AR01 without changing cleaners must leave the cleaner's multi-area baseline untouched.
        await factory.WithDbAsync(async db =>
        {
            var baselines = await db.ScheduleBaselines.Include(b => b.Days)
                .Where(b => b.UserId == cleanerId && b.Kind == BaselineKind.Regular && b.EffectiveUntil == null)
                .ToListAsync();
            var active = Assert.Single(baselines);
            Assert.Equal(ar03, active.Days.Single(d => d.Weekday == DayOfWeek.Thursday).AreaId);
            Assert.Equal(ar01, active.Days.Single(d => d.Weekday == DayOfWeek.Monday).AreaId);
        });
    }

    [Fact]
    public async Task M1_Cross_building_relief_loan_receipt_is_readable_by_destination_supervisor()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        var sourceBuilding = await AdminSetupApi.BuildingIdAsync(factory, "B");
        var destinationArea = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var sourceSupId = await ReliefApi.AddSupervisorAsync(factory, admin, "S2002", "B", Shift.Day);
        var reliefId = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var (destSupervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var (response, answer) = await ReliefApi.AssignAsync(destSupervisor,
            ReliefApi.Assignment(reliefId, destinationArea, ReliefApi.Wednesday, sourceBuilding,
                breakGroup: 1, sourceSupervisorId: sourceSupId, agreementReported: true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(answer);

        // Destination Supervisor does not manage Building B, yet must still read back their own loan receipt.
        var receiptResponse = await destSupervisor.GetAsync($"/api/schedules/commands/{answer!.CommandId}");
        Assert.Equal(HttpStatusCode.OK, receiptResponse.StatusCode);
        var retained = await ScheduleWriteApi.ReadAsync<ScheduleWriteApi.CommandAnswer>(receiptResponse);
        Assert.Equal(ScheduleCommandOutcome.Committed, retained?.Outcome);
    }

    [Fact]
    public async Task M1_Relief_assignment_refuses_replacing_a_regular_cleaner_of_different_shift()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var dayRegularId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var nightReliefId = await ReliefApi.AddReliefAsync(factory, admin, "E3099", "A", Shift.Night);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var (response, answer) = await ScheduleWriteApi.ExecuteAsync(supervisor, ReliefApi.AssignRelief, new
        {
            userId = nightReliefId,
            areaId = ar01,
            shiftDate = ReliefApi.Iso(ReliefApi.Wednesday),
            shift = "Night",
            replacedUserId = dayRegularId,
            breakGroup = 1,
            sourceBuildingId = buildingA,
            agreementReported = true,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("คนละกะ", Assert.Single(answer!.Problems).Message);
    }

    [Fact]
    public async Task M2_Supervisor_who_lost_building_today_cannot_cancel_past_transfer()
    {
        using var factory = TransferApi.NewFactory();
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        // Supervisor held Building A on Oct 8, then moved to Building B on Oct 9.
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 9));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, new DateOnly(2026, 10, 9));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);

        var (cleaner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000001", "0810000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));
        var transferId = await TransferApi.InstructForIdAsync(factory, supervisor, cleanerId, ar01, ar03, TransferApi.Thai(11, 0));

        // On Oct 9, the supervisor no longer holds Building A today -> cancelling Oct 8 transfer must be refused.
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.FromHours(7)));
        var (cancelResponse, _) = await TransferApi.CancelAsync(supervisor, transferId, "ลองยกเลิกหลังย้ายตึก");
        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        Assert.True(buildingB > 0);
    }

    [Fact]
    public async Task M3_Dated_schedule_write_records_dated_building_scope_on_receipt()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddBuildingAsync(factory, "B");
        var arB = await TestData.AddAreaAsync(factory, "ARB1", "B", ShiftPattern.DayAndNight);
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ReliefApi.AddSupervisorAsync(factory, admin, "S2009", "B", Shift.Day);
        // Cleaner's legacy User.BuildingId/AreaId still points to Building A, while dated profile places them in Building B.
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1), "B");
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "ARB1", breakGroup: 1);
        var (supB, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "S2009", ReliefApi.Phone("S2009"));

        var (writeResponse, answer) = await ScheduleWriteApi.ExecuteAsync(supB, ScheduleWriteApi.SetDatedArea, new
        {
            userId = cleanerId,
            date = ReliefApi.Iso(ReliefApi.Wednesday),
            shift = "Day",
            areaId = arB,
        });
        Assert.Equal(HttpStatusCode.OK, writeResponse.StatusCode);

        // Building B supervisor does NOT hold Building A; receipt scope must reflect dated Building B, not legacy A.
        var receiptResponse = await supB.GetAsync($"/api/schedules/commands/{answer!.CommandId}");
        Assert.Equal(HttpStatusCode.OK, receiptResponse.StatusCode);
    }

    [Fact]
    public async Task M4_Regular_baseline_shift_change_validates_subsequent_baseline_versions()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar02DayOnly = await AdminSetupApi.AreaIdAsync(factory, "AR02"); // DayOnly
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        // Current baseline (Oct 1 - Nov 24) on AR03; future baseline (Nov 24+) on DayOnly AR02.
        await factory.WithDbAsync(async db =>
        {
            var adminUser = await db.Users.SingleAsync(u => u.Username == "admin");
            var first = new ScheduleBaseline
            {
                UserId = cleanerId, Kind = BaselineKind.Regular, EffectiveFrom = new DateOnly(2026, 10, 1),
                EffectiveUntil = new DateOnly(2026, 11, 24), Revision = 1, CreatedById = adminUser.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var future = new ScheduleBaseline
            {
                UserId = cleanerId, Kind = BaselineKind.Regular, EffectiveFrom = new DateOnly(2026, 11, 24),
                Revision = 1, CreatedById = adminUser.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var d in Enum.GetValues<DayOfWeek>())
            {
                first.Days.Add(new ScheduleBaselineDay { Weekday = d, AreaId = ar03, BreakGroup = 1 });
                future.Days.Add(new ScheduleBaselineDay { Weekday = d, AreaId = ar02DayOnly, BreakGroup = 1 });
            }
            db.ScheduleBaselines.AddRange(first, future);
            await db.SaveChangesAsync();
        });

        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        // Switching shift to Night effective Nov 18 would also shift Nov 24+ (AR02 DayOnly) to Night -> must refuse.
        var (response, answer) = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = cleanerId,
            effectiveFrom = "2026-11-18",
            shift = "Night",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("AR02", Assert.Single(answer!.Problems).Message);
    }

    [Fact]
    public async Task M5_Relief_baseline_refuses_area_that_does_not_support_relief_shift()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar02DayOnly = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var nightReliefId = await ReliefApi.AddReliefAsync(factory, admin, "E3055", "A", Shift.Night);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var (response, answer) = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetReliefBaseline, new
        {
            rows = new[]
            {
                new
                {
                    userId = nightReliefId,
                    effectiveFrom = ReliefApi.Iso(ReliefApi.Wednesday),
                    days = new[] { new { weekday = "Wednesday", areaId = (int?)ar02DayOnly, isDayOff = false } },
                },
            },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("AR02", Assert.Single(answer!.Problems).Message);
    }

    [Fact]
    public async Task M6_Switching_relief_cleaner_to_regular_closes_active_relief_baseline()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        ScheduleWriteApi.Today(factory); // 2026-11-17
        var reliefId = await ReliefApi.AddReliefAsync(factory, admin, "E3066", "A", Shift.Day);

        await factory.WithDbAsync(async db =>
        {
            var adminUser = await db.Users.SingleAsync(u => u.Username == "admin");
            var reliefBaseline = new ScheduleBaseline
            {
                UserId = reliefId, Kind = BaselineKind.Relief, EffectiveFrom = new DateOnly(2026, 10, 1),
                Revision = 1, CreatedById = adminUser.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            reliefBaseline.Days.Add(new ScheduleBaselineDay { Weekday = DayOfWeek.Wednesday, AreaId = ar03 });
            db.ScheduleBaselines.Add(reliefBaseline);
            await db.SaveChangesAsync();
        });

        var converted = await admin.PutAsJsonAsync($"{AccountApi.Path}/{reliefId}", new
        {
            displayName = "แม่บ้านเปลี่ยนเป็นประจำ",
            cleanerKind = "Regular",
            homeBuildingId = buildingA,
            shift = "Day",
        });
        Assert.Equal(HttpStatusCode.OK, converted.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var reliefBaselines = await db.ScheduleBaselines
                .Where(b => b.UserId == reliefId && b.Kind == BaselineKind.Relief)
                .ToListAsync();
            var closed = Assert.Single(reliefBaselines);
            Assert.Equal(new DateOnly(2026, 11, 17), closed.EffectiveUntil);
        });
    }

    [Fact]
    public async Task M10_Retention_preserves_dated_detail_for_overnight_shift_with_retained_attendance()
    {
        using var factory = new JanitorialApiFactory();
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero); // 07:00 Thai on 2026-11-03 -> earliestRetained = 2025-11-03 (RecordYears = 1)
        factory.Clock.SetUtcNow(now);
        var overnightDate = new DateOnly(2025, 11, 2); // < earliestRetained, but ShiftOut at 07:10 Thai on 2025-11-03 is retained
        var expiredDate = new DateOnly(2025, 10, 20);

        await factory.WithDbAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000002");
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var ar01 = await db.Areas.SingleAsync(a => a.Code == "AR01");
            var ar02 = await db.Areas.SingleAsync(a => a.Code == "AR02");
            var sign = await db.Signs.FirstAsync(s => s.AreaId == ar01.Id && s.ServicePointId == null);

            // Retained attendance on overnightDate Night shift
            db.ShiftAttendances.Add(new Domain.Entities.ShiftAttendance
            {
                UserId = cleaner.Id, AreaId = ar01.Id, SignId = sign.Id, ShiftDate = overnightDate, Shift = Shift.Night,
                EventType = AttendanceEvent.ShiftOut, OccurredAt = new DateTime(2025, 11, 3, 0, 10, 0, DateTimeKind.Utc),
                Source = AttendanceSource.Scan, CreatedAt = DateTime.UtcNow,
            });

            // Dated detail on overnightDate (must survive) AND expiredDate (must be pruned)
            foreach (var date in new[] { overnightDate, expiredDate })
            {
                db.ScheduleExceptions.Add(new ScheduleException
                {
                    UserId = cleaner.Id, Kind = ScheduleExceptionKind.BreakGroup, ShiftDate = date, Shift = Shift.Night,
                    BreakGroup = 1, CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
                db.ReliefAssignments.Add(new ReliefAssignment
                {
                    UserId = cleaner.Id, ShiftDate = date, Shift = Shift.Night, AreaId = ar01.Id, BreakGroup = 1,
                    SourceBuildingId = building.Id, CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
                var transfer = new AreaTransfer
                {
                    PersonId = cleaner.Id, ShiftDate = date, Shift = Shift.Night, FromAreaId = ar01.Id, ToAreaId = ar02.Id,
                    EffectiveAt = date.ToDateTime(new TimeOnly(14, 0), DateTimeKind.Utc), Reason = "ย้ายทดสอบ",
                    CreatedById = admin.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                };
                db.AreaTransfers.Add(transfer);
                await db.SaveChangesAsync();
                db.AreaTransferConfirmations.Add(new AreaTransferConfirmation
                {
                    TransferId = transfer.Id, AreaId = ar02.Id, SignId = sign.Id, ConfirmedById = cleaner.Id,
                    OccurredAt = date.ToDateTime(new TimeOnly(14, 5), DateTimeKind.Utc), Source = AttendanceSource.Scan,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();

            await new RetentionRunner(db, factory.Clock, new RetentionSettings()).RunAsync(default);
            db.ChangeTracker.Clear();

            Assert.Equal(overnightDate, Assert.Single(await db.ScheduleExceptions.ToListAsync()).ShiftDate);
            Assert.Equal(overnightDate, Assert.Single(await db.ReliefAssignments.ToListAsync()).ShiftDate);
            Assert.Equal(overnightDate, Assert.Single(await db.AreaTransfers.ToListAsync()).ShiftDate);
            Assert.Single(await db.AreaTransferConfirmations.ToListAsync());
        });
    }

    [Fact]
    public async Task M11_Building_deactivation_is_blocked_by_active_cleaner_profile_period()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        ScheduleWriteApi.Today(factory); // 2026-11-17
        // Active Relief cleaner assigned to Building B (no Areas, no Supervisors in Building B)
        await ReliefApi.AddReliefAsync(factory, admin, "E3077", "B", Shift.Day);

        var deactivate = await admin.PostAsync($"/api/admin/buildings/{buildingB}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
    }

    [Fact]
    public async Task M18_Area_deactivation_and_day_only_downgrade_blocked_by_future_night_relief()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var nightRelief = await ReliefApi.AddReliefAsync(factory, admin, "E3088", "A", Shift.Night);

        await factory.WithDbAsync(async db =>
        {
            var adminUser = await db.Users.SingleAsync(u => u.Username == "admin");
            db.ReliefAssignments.Add(new ReliefAssignment
            {
                UserId = nightRelief, ShiftDate = ReliefApi.Wednesday, Shift = Shift.Night, AreaId = ar03,
                BreakGroup = 1, SourceBuildingId = buildingA, CreatedById = adminUser.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var downgrade = await admin.PutAsJsonAsync($"/api/admin/areas/{ar03}", new
        {
            name = "AR03",
            buildingId = buildingA,
            shiftPattern = "DayOnly",
        });
        Assert.Equal(HttpStatusCode.Conflict, downgrade.StatusCode);

        var deactivate = await admin.PostAsync($"/api/admin/areas/{ar03}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
    }

    [Fact]
    public async Task Transfer_slot_revision_reflects_latest_cancelled_follow_up_transfer()
    {
        using var factory = TransferApi.NewFactory();
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var ar04 = await TestData.AddAreaAsync(factory, "AR04", "A", ShiftPattern.DayAndNight);
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        await TransferApi.LocateAsync(factory, TransferApi.Destination);

        var (cleaner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000001", "0810000001");
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        // Transfer #1: AR01 -> AR03, confirmed (rev 1)
        var t1 = await TransferApi.InstructForIdAsync(factory, supervisor, cleanerId, ar01, ar03, TransferApi.Thai(10, 0));
        var confirm1 = await TransferApi.ConfirmAsync(factory, cleaner, t1, "1", TransferApi.Destination, TransferApi.Thai(10, 5));
        Assert.Equal(HttpStatusCode.OK, confirm1.StatusCode);

        // Transfer #2: AR03 -> AR04, instructed then cancelled (rev 2)
        var t2 = await TransferApi.InstructForIdAsync(factory, supervisor, cleanerId, ar03, ar04, TransferApi.Thai(13, 0));
        var (cancel2, _) = await TransferApi.CancelAsync(supervisor, t2, "ยกเลิกรอบสอง");
        Assert.Equal(HttpStatusCode.OK, cancel2.StatusCode);

        // Week read's transferSlots must carry rev "2" (matching latest slot transfer) so a new instruct succeeds.
        var weekResponse = await supervisor.GetAsync("/api/schedules?weekStart=2026-10-05");
        Assert.Equal(HttpStatusCode.OK, weekResponse.StatusCode);
        using var weekJson = JsonDocument.Parse(await weekResponse.Content.ReadAsStringAsync());
        var slots = weekJson.RootElement.GetProperty("transferSlots");
        Assert.Equal(1, slots.GetArrayLength());
        var slotRevision = slots[0].GetProperty("revision").GetString()!;
        Assert.Equal("2", slotRevision);

        var (t3Response, _) = await TransferApi.InstructAsync(factory, supervisor, cleanerId, ar03, ar04,
            TransferApi.Thai(15, 0), expected: new Dictionary<string, string> { [TransferApi.Key(cleanerId)] = slotRevision });
        Assert.Equal(HttpStatusCode.OK, t3Response.StatusCode);
    }

    [Fact]
    public async Task Minor_boundary_inputs_return_400_and_audit_summary_truncates_safely()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var detail = (await admin.GetFromJsonAsync<AreaDetailModel>($"/api/admin/areas/{ar01}"))!;
        var pointId = detail.Points[0].Id;

        var nullWindow = await admin.PutAsJsonAsync($"/api/admin/points/{pointId}", new
        {
            name = "จุดทดสอบ",
            roundWindows = new object?[] { null },
        });
        Assert.Equal(HttpStatusCode.BadRequest, nullWindow.StatusCode);

        var maxDateRange = await admin.GetAsync("/api/admin/audit-log?from=9999-12-30&to=9999-12-31");
        Assert.Equal(HttpStatusCode.BadRequest, maxDateRange.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var longSummary = new string('ก', 350);
            AuditTrail.Add(db, null, DateTime.UtcNow, "TEST_LONG_SUMMARY", "buildings", 1, longSummary, null, null);
            await db.SaveChangesAsync();
            var stored = await db.AuditLog.SingleAsync(a => a.Action == "TEST_LONG_SUMMARY");
            Assert.Equal(AuditTrail.MaxSummaryLength, stored.Summary.Length);
            Assert.EndsWith("…", stored.Summary, StringComparison.Ordinal);
        });
    }
}
