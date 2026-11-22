using System.Net;
using System.Net.Http.Json;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.ApiTests.Admin;
using JanitorialManagementDashboard.ApiTests.Attendance;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Points;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Regression tests for defects identified in the independent code review of feat/supervisor-full-scheduling.
/// </summary>
public class SupervisorFullSchedulingReviewRegressionTests
{
    private record BoardAnswer(List<BoardRowAnswer> Rows);
    private record BoardRowAnswer(int AreaId, string AreaCode, string State, BoardCleanerAnswer? Cleaner);
    private record BoardCleanerAnswer(int Id, string EmployeeId, string DisplayName);
    private record CleanerPickerAnswer(int Id, string EmployeeId, string DisplayName, string? Shift, int? AreaId, string? AreaCode);

    [Fact]
    public async Task Finding1_Renaming_cleaner_with_future_baseline_preserves_dated_shift_and_baseline()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        var created = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner",
            displayName = "สมหญิง เดิม",
            employeeId = "E5001",
            phone = "0895001000",
            cleanerKind = "Regular",
            homeBuildingId = buildingA,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var cleaner = await AccountApi.ReadAsync(created);

        // Supervisor assigns shift + area starting tomorrow (2026-11-18). users.Shift and users.AreaId stay null.
        var scheduled = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = cleaner.Id,
            effectiveFrom = "2026-11-18",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, scheduled.Response.StatusCode);

        // Advance clock to 2026-11-18 (Thai 09:00 = 02:00 UTC); reuse admin logged in at wall clock so JWT is valid.
        factory.Clock.SetUtcNow(new DateTime(2026, 11, 18, 2, 0, 0, DateTimeKind.Utc));

        var accounts = (await admin.GetFromJsonAsync<List<AccountModel>>(AccountApi.Path))!;
        var view = accounts.Single(a => a.Id == cleaner.Id);
        Assert.Equal("Day", view.Shift);
        Assert.Equal(ar03, view.AreaId);
        Assert.Equal("AR03", view.AreaCode);

        // Admin edits only displayName using the values from AccountView.
        var edit = await admin.PutAsJsonAsync($"{AccountApi.Path}/{cleaner.Id}", new
        {
            displayName = "สมหญิง แก้ชื่อ",
            areaId = view.AreaId,
            shift = view.Shift,
            homeBuildingId = view.AreaId is null ? view.HomeBuildingId : (int?)null,
            cleanerKind = view.AreaId is null ? "Regular" : (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        // Verify dated profile shift and baseline Area were NOT wiped.
        await factory.WithDbAsync(async db =>
        {
            var profile = await db.CleanerProfilePeriods
                .Where(p => p.UserId == cleaner.Id && p.EffectiveFrom <= new DateOnly(2026, 11, 18)
                    && (p.EffectiveUntil == null || new DateOnly(2026, 11, 18) < p.EffectiveUntil.Value))
                .OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id)
                .LastAsync();
            Assert.Equal(Shift.Day, profile.Shift);

            var baseline = await db.ScheduleBaselines.Include(b => b.Days)
                .Where(b => b.UserId == cleaner.Id && b.Kind == BaselineKind.Regular
                    && b.EffectiveFrom <= new DateOnly(2026, 11, 18)
                    && (b.EffectiveUntil == null || new DateOnly(2026, 11, 18) < b.EffectiveUntil.Value))
                .OrderBy(b => b.EffectiveFrom).ThenBy(b => b.Id)
                .LastAsync();
            Assert.All(baseline.Days, d => Assert.Equal(ar03, d.AreaId));
        });
    }

    [Fact]
    public async Task Finding2_Team_attendance_rejects_wrong_shift_and_day_only_night_slot()
    {
        using var factory = new JanitorialApiFactory();
        var starts = new DateOnly(2026, 10, 1);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, starts);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, starts);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", starts, "AR01", breakGroup: 1);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000003", CleanerKind.Regular, Shift.Day, starts);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000003", starts, "AR02", breakGroup: 1);

        var dayCleanerAr01 = await AdminSetupApi.UserIdAsync(factory, "1000001");
        var dayCleanerAr02 = await AdminSetupApi.UserIdAsync(factory, "1000003"); // AR02 is DayOnly
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(8, 22, 0));

        // Supervisor reading or adding Night attendance for a Day-shift cleaner must be refused.
        var supRead = await TeamAttendanceApi.StateAsync(supervisor, dayCleanerAr01, "2026-10-08", "Night");
        Assert.Equal(HttpStatusCode.Forbidden, supRead.StatusCode);

        var nightRev1 = await TeamAttendanceApi.RevisionAsync(factory, dayCleanerAr01, Shift.Night);
        var supAdd1 = await TeamAttendanceApi.AddAsync(supervisor, dayCleanerAr01, "ShiftIn", "19:10",
            expectedRevision: nightRev1, shiftDate: "2026-10-08", shift: "Night");
        Assert.Equal(HttpStatusCode.Forbidden, supAdd1.StatusCode);

        // Admin also cannot add Night attendance for a Day-shift cleaner on a DayOnly area.
        var nightRev2 = await TeamAttendanceApi.RevisionAsync(factory, dayCleanerAr02, Shift.Night);
        var adminAdd2 = await TeamAttendanceApi.AddAsync(admin, dayCleanerAr02, "ShiftIn", "19:10",
            expectedRevision: nightRev2, shiftDate: "2026-10-08", shift: "Night");
        Assert.Equal(HttpStatusCode.BadRequest, adminAdd2.StatusCode);

        Assert.Empty(await EffectiveAttendanceApi.FactsAsync(factory));
    }

    [Fact]
    public async Task Finding3_Account_and_area_endpoints_reject_future_baseline_overlap_and_exclude_relief_cleaners()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        // Cleaner 1 gets future baseline on (AR03, Day) starting 2026-11-18.
        var c1Resp = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน 1", employeeId = "E5010", phone = "0895010000",
            cleanerKind = "Regular", homeBuildingId = buildingA,
        });
        var c1 = await AccountApi.ReadAsync(c1Resp);
        var baseline = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = c1.Id,
            effectiveFrom = "2026-11-18",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, baseline.Response.StatusCode);

        // Creating another Cleaner directly on (AR03, Day) must fail with 409 Conflict.
        var dupCreate = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน 2", employeeId = "E5011", phone = "0895011000",
            cleanerKind = "Regular", areaId = ar03, shift = "Day",
        });
        Assert.Equal(HttpStatusCode.Conflict, dupCreate.StatusCode);

        // Creating an unassigned Day cleaner and trying to assign them to AR03 via PUT /api/admin/areas/{id} must also conflict.
        var c2Resp = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน 2", employeeId = "E5012", phone = "0895012000",
            cleanerKind = "Regular", homeBuildingId = buildingA, shift = "Day",
        });
        Assert.Equal(HttpStatusCode.Created, c2Resp.StatusCode);
        var c2 = await AccountApi.ReadAsync(c2Resp);

        var dupArea = await admin.PutAsJsonAsync($"/api/admin/areas/{ar03}", new
        {
            name = "Area 3", buildingId = buildingA, shiftPattern = "DayAndNight",
            dayCleanerId = c2.Id, nightCleanerId = (int?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, dupArea.StatusCode);

        // Relief cleaner must NOT appear in /api/admin/cleaners and must be refused by PUT /api/admin/areas/{id}.
        var reliefResp = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "สแปร์ A", employeeId = "E5013", phone = "0895013000",
            cleanerKind = "Relief", homeBuildingId = buildingA, shift = "Day",
        });
        Assert.Equal(HttpStatusCode.Created, reliefResp.StatusCode);
        var relief = await AccountApi.ReadAsync(reliefResp);

        var picker = (await admin.GetFromJsonAsync<List<CleanerPickerAnswer>>("/api/admin/cleaners"))!;
        Assert.DoesNotContain(picker, c => c.Id == relief.Id);

        var ar04 = await TestData.AddAreaAsync(factory, "AR04", "A", ShiftPattern.DayAndNight);
        var reliefOnArea = await admin.PutAsJsonAsync($"/api/admin/areas/{ar04}", new
        {
            name = "Area 4", buildingId = buildingA, shiftPattern = "DayAndNight",
            dayCleanerId = relief.Id, nightCleanerId = (int?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, reliefOnArea.StatusCode);

        // Relief profile kind must remain Relief.
        await factory.WithDbAsync(async db =>
        {
            var profile = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == relief.Id);
            Assert.Equal(CleanerKind.Relief, profile.Kind);
        });
    }

    [Fact]
    public async Task Finding4_Night_to_day_transition_resolves_todays_day_shift_during_overlap_window()
    {
        using var factory = new JanitorialApiFactory();
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var cleanerId = await TestData.AddCleanerAsync(factory, "E5020", Shift.Night, areaCode: null);

        await factory.WithDbAsync(async db =>
        {
            db.CleanerProfilePeriods.AddRange(
                new CleanerProfilePeriod
                {
                    UserId = cleanerId, Kind = CleanerKind.Regular, HomeBuildingId = buildingA,
                    Shift = Shift.Night, EffectiveFrom = new DateOnly(2026, 11, 20), EffectiveUntil = new DateOnly(2026, 11, 22),
                    Revision = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                },
                new CleanerProfilePeriod
                {
                    UserId = cleanerId, Kind = CleanerKind.Regular, HomeBuildingId = buildingA,
                    Shift = Shift.Day, EffectiveFrom = new DateOnly(2026, 11, 22), EffectiveUntil = null,
                    Revision = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
            var baseline = new ScheduleBaseline
            {
                UserId = cleanerId, Kind = BaselineKind.Regular, EffectiveFrom = new DateOnly(2026, 11, 22),
                Revision = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            foreach (var w in Enum.GetValues<DayOfWeek>())
            {
                baseline.Days.Add(new ScheduleBaselineDay { Weekday = w, AreaId = ar03, BreakGroup = 1 });
            }
            db.ScheduleBaselines.Add(baseline);
            await db.SaveChangesAsync();
        });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<AttendanceSettings>();
        var user = await db.Users.SingleAsync(u => u.Id == cleanerId);

        // At 07:05 Thai on 2026-11-22 (00:05 UTC), Day shift has ALREADY started; must resolve (2026-11-22, Day), not yesterday's Night.
        var at0705Thai = new DateTime(2026, 11, 22, 0, 5, 0, DateTimeKind.Utc);
        var slot0705 = await CleanerShiftResolver.ResolveSlotAsync(db, user, at0705Thai, settings);
        Assert.Equal(new ShiftSlot(new DateOnly(2026, 11, 22), Shift.Day), slot0705);

        // At 06:30 Thai on 2026-11-22 (23:30 UTC on Nov 21), yesterday's Night shift is still running (19:00-07:00); must resolve (2026-11-21, Night).
        var at0630Thai = new DateTime(2026, 11, 21, 23, 30, 0, DateTimeKind.Utc);
        var slot0630 = await CleanerShiftResolver.ResolveSlotAsync(db, user, at0630Thai, settings);
        Assert.Equal(new ShiftSlot(new DateOnly(2026, 11, 21), Shift.Night), slot0630);
    }

    [Fact]
    public async Task Finding5_Shift_change_in_regular_baseline_is_refused_with_live_leave_or_attendance_or_future_profile_revert()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        // Case A: Live Day Leave exists on 2026-11-17 -> changing shift to Night effective 2026-11-17 must be refused.
        await EffectiveAttendanceApi.AddLeaveAsync(factory, "1000001", new DateOnly(2026, 11, 17), Shift.Day,
            new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

        var changeWithLeave = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = cleanerId,
            effectiveFrom = "2026-11-17",
            shift = "Night",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.Conflict, changeWithLeave.Response.StatusCode);

        // Case B: Already checked in on Day shift 2026-11-18 (without leave) -> changing shift to Night on 2026-11-18 must be refused.
        await TestData.AddAttendanceAsync(factory, "1000001", new DateOnly(2026, 11, 18), Shift.Day, AttendanceEvent.ShiftIn,
            new DateTimeOffset(2026, 11, 18, 0, 5, 0, TimeSpan.Zero));

        var changeWithAttendance = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = cleanerId,
            effectiveFrom = "2026-11-18",
            shift = "Night",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.Conflict, changeWithAttendance.Response.StatusCode);

        // Case C: Future profile period exists on 2026-11-22 with Day shift -> open-ended Night baseline starting 2026-11-19 must be refused.
        await factory.WithDbAsync(async db =>
        {
            var existing = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == cleanerId);
            existing.EffectiveUntil = new DateOnly(2026, 11, 22);
            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = cleanerId, Kind = CleanerKind.Regular, HomeBuildingId = existing.HomeBuildingId,
                Shift = Shift.Day, EffectiveFrom = new DateOnly(2026, 11, 22), EffectiveUntil = null,
                Revision = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var changeWithFutureProfile = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = cleanerId,
            effectiveFrom = "2026-11-19",
            shift = "Night",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.Conflict, changeWithFutureProfile.Response.StatusCode);
    }

    [Fact]
    public async Task Finding6_Attendance_board_area_deactivation_and_cover_honor_future_baseline_on_effective_date()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        var created = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน อนาคต", employeeId = "E5030", phone = "0895030000",
            cleanerKind = "Regular", homeBuildingId = buildingA,
        });
        var cleaner = await AccountApi.ReadAsync(created);

        var scheduled = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = cleaner.Id,
            effectiveFrom = "2026-11-18",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, scheduled.Response.StatusCode);

        // Log cleaner in at wall-clock time so JWT Bearer exp validation succeeds, then move business clock to 2026-11-18 08:05 Thai.
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        var (cleanerClient, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E5030", "0895030000");
        factory.Clock.SetUtcNow(new DateTime(2026, 11, 18, 1, 5, 0, DateTimeKind.Utc));

        await AttendanceApi.CheckInAsync(factory, cleanerClient, new DateTime(2026, 11, 18, 1, 5, 0, DateTimeKind.Utc), "token-checkin-ar03");

        // Attendance board must show AR03 as Working with E5030 (not Empty).
        var board = (await admin.GetFromJsonAsync<BoardAnswer>("/api/admin/attendance?shiftDate=2026-11-18&shift=Day"))!;
        var row = board.Rows.Single(r => r.AreaCode == "AR03");
        Assert.Equal("Working", row.State);
        Assert.NotNull(row.Cleaner);
        Assert.Equal("E5030", row.Cleaner!.EmployeeId);

        // Area deactivation must be refused while cleaner holds AR03 via baseline.
        var deactivate = await admin.PostAsync($"/api/admin/areas/{ar03}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);

        // Cover assignment must refuse covering the cleaner's own effective Area AR03.
        var coverSelf = await admin.PostAsJsonAsync("/api/admin/cover-assignments", new
        {
            userId = cleaner.Id, areaId = ar03, shiftDate = "2026-11-18", shift = "Day",
        });
        Assert.Equal(HttpStatusCode.BadRequest, coverSelf.StatusCode);
    }

    [Fact]
    public async Task Finding7_Deactivating_cleaner_clears_future_regular_baselines()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        var c1 = await AccountApi.ReadAsync(await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน ลาออก", employeeId = "E5040", phone = "0895040000",
            cleanerKind = "Regular", homeBuildingId = buildingA,
        }));
        var scheduled = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = c1.Id,
            effectiveFrom = "2026-11-20",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, scheduled.Response.StatusCode);

        // Deactivate Cleaner 1 today -> future baseline on 2026-11-20 must be cleared.
        var deactivated = await admin.PostAsync($"{AccountApi.Path}/{c1.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        // Assign Cleaner 2 to AR03, then reactivate Cleaner 1 -> Cleaner 1 must NOT resurrect AR03 on 2026-11-20.
        var c2Resp = await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน ใหม่", employeeId = "E5041", phone = "0895041000",
            cleanerKind = "Regular", areaId = ar03, shift = "Day",
        });
        Assert.Equal(HttpStatusCode.Created, c2Resp.StatusCode);

        var reactivated = await admin.PostAsync($"{AccountApi.Path}/{c1.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var futureBaseline = await db.ScheduleBaselines.Include(b => b.Days)
                .Where(b => b.UserId == c1.Id && b.EffectiveFrom >= new DateOnly(2026, 11, 20))
                .ToListAsync();
            Assert.All(futureBaseline.SelectMany(b => b.Days), d => Assert.Null(d.AreaId));
        });
    }

    [Fact]
    public async Task Finding8_Relief_baseline_keeps_tomorrow_start_and_rejects_mismatched_shift()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        var relief = await AccountApi.ReadAsync(await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "สแปร์ ทดสอบ", employeeId = "E5050", phone = "0895050000",
            cleanerKind = "Relief", homeBuildingId = buildingA, shift = "Day",
        }));

        // Spec §7: Relief baseline still requires effectiveFrom > today (tomorrow or later).
        var todayRelief = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetReliefBaseline, new
        {
            userId = relief.Id,
            effectiveFrom = "2026-11-17",
            days = new[] { new { weekday = "Tuesday", areaId = (int?)ar01, isDayOff = false } },
        });
        Assert.Equal(HttpStatusCode.Conflict, todayRelief.Response.StatusCode);

        // Sending a mismatched shift on set-relief-baseline must be refused.
        var wrongShiftRelief = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetReliefBaseline, new
        {
            userId = relief.Id,
            effectiveFrom = "2026-11-18",
            shift = "Night",
            days = new[] { new { weekday = "Wednesday", areaId = (int?)ar01, isDayOff = false } },
        });
        Assert.Equal(HttpStatusCode.Conflict, wrongShiftRelief.Response.StatusCode);
    }

    [Fact]
    public async Task Finding10_Dated_area_override_rejects_day_only_area_on_night_shift()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar02 = await AdminSetupApi.AreaIdAsync(factory, "AR02"); // DayOnly
        var nightCleaner = await AdminSetupApi.UserIdAsync(factory, "1000002");
        await ScheduleWriteApi.AddProfileAsync(factory, "1000002", CleanerKind.Regular, Shift.Night, new DateOnly(2026, 10, 1));
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000002", new DateOnly(2026, 10, 1), "AR01", breakGroup: 1);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        var dated = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetDatedArea, new
        {
            userId = nightCleaner,
            date = "2026-11-18",
            shift = "Night",
            areaId = ar02,
        });
        Assert.Equal(HttpStatusCode.Conflict, dated.Response.StatusCode);
    }

    [Fact]
    public async Task Rereview1_Replacing_area_cleaner_clears_future_baseline_for_that_area_and_relief_switch_unblocks_deactivation()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var ar04 = await TestData.AddAreaAsync(factory, "AR04", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        // Cleaner 1 holds AR03 today and has a future baseline starting 2026-11-20 still on AR03.
        var c1 = await AccountApi.ReadAsync(await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน เดิม", employeeId = "E5060", phone = "0895060000",
            cleanerKind = "Regular", areaId = ar03, shift = "Day",
        }));
        var c1Future = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = c1.Id,
            effectiveFrom = "2026-11-20",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)2, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, c1Future.Response.StatusCode);

        // Cleaner 2 is unassigned Day Regular cleaner. Admin replaces AR03's dayCleaner with Cleaner 2.
        var c2 = await AccountApi.ReadAsync(await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน ใหม่", employeeId = "E5061", phone = "0895061000",
            cleanerKind = "Regular", homeBuildingId = buildingA, shift = "Day",
        }));
        var replace = await admin.PutAsJsonAsync($"/api/admin/areas/{ar03}", new
        {
            name = "Area 3", buildingId = buildingA, shiftPattern = "DayAndNight",
            dayCleanerId = c2.Id, nightCleanerId = (int?)null,
        });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);

        // Cleaner 1's future baseline on 2026-11-20 must NOT keep AR03 (no dual ownership).
        await factory.WithDbAsync(async db =>
        {
            var futureC1 = await db.ScheduleBaselines.Include(b => b.Days)
                .SingleAsync(b => b.UserId == c1.Id && b.EffectiveFrom == new DateOnly(2026, 11, 20));
            Assert.All(futureC1.Days, d => Assert.Null(d.AreaId));
        });

        // Cleaner 3 holds a future baseline on AR04, then Admin switches Cleaner 3 to Relief -> AR04 can be deactivated.
        var c3 = await AccountApi.ReadAsync(await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน เปลี่ยนสแปร์", employeeId = "E5062", phone = "0895062000",
            cleanerKind = "Regular", homeBuildingId = buildingA,
        }));
        var c3Future = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = c3.Id,
            effectiveFrom = "2026-11-20",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar04, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, c3Future.Response.StatusCode);

        var toRelief = await admin.PutAsJsonAsync($"{AccountApi.Path}/{c3.Id}", new
        {
            displayName = "แม่บ้าน เปลี่ยนสแปร์",
            cleanerKind = "Relief",
            homeBuildingId = buildingA,
            shift = "Day",
        });
        Assert.Equal(HttpStatusCode.OK, toRelief.StatusCode);

        var deactivateAr04 = await admin.PostAsync($"/api/admin/areas/{ar04}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateAr04.StatusCode);
    }

    [Fact]
    public async Task Rereview2_Deactivating_cleaner_with_stale_null_user_shift_preserves_dated_profile_shift()
    {
        using var factory = ScheduleWriteApi.NewFactory();
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, new DateOnly(2026, 10, 1));
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory);
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        ScheduleWriteApi.Today(factory); // 2026-11-17

        var c1 = await AccountApi.ReadAsync(await admin.PostAsJsonAsync(AccountApi.Path, new
        {
            role = "Cleaner", displayName = "แม่บ้าน กะอนาคต", employeeId = "E5070", phone = "0895070000",
            cleanerKind = "Regular", homeBuildingId = buildingA,
        }));
        var scheduled = await ScheduleWriteApi.ExecuteAsync(supervisor, ScheduleWriteApi.SetRegularBaseline, new
        {
            userId = c1.Id,
            effectiveFrom = "2026-11-18",
            shift = "Day",
            days = Enum.GetNames<DayOfWeek>().Select(d => new { weekday = d, areaId = (int?)ar03, breakGroup = (int?)1, isDayOff = false }),
        });
        Assert.Equal(HttpStatusCode.OK, scheduled.Response.StatusCode);

        // Move clock to 2026-11-18 (users.Shift is still null) and deactivate the Cleaner.
        factory.Clock.SetUtcNow(new DateTime(2026, 11, 18, 2, 0, 0, DateTimeKind.Utc));
        var deactivated = await admin.PostAsync($"{AccountApi.Path}/{c1.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        await factory.WithDbAsync(async db =>
        {
            var profile = await db.CleanerProfilePeriods
                .SingleAsync(p => p.UserId == c1.Id && p.EffectiveFrom == new DateOnly(2026, 11, 18));
            Assert.Equal(Shift.Day, profile.Shift);
        });
    }

    [Fact]
    public void Rereview3_SchedulingSettings_validates_minimum_32_utf8_bytes_for_command_signing_key()
    {
        Assert.False(new Application.Scheduling.SchedulingSettings(null).HasValidCommandSigningKey);
        Assert.False(new Application.Scheduling.SchedulingSettings("   ").HasValidCommandSigningKey);
        var shortKey = new Application.Scheduling.SchedulingSettings("short-key-under-32-bytes");
        Assert.True(shortKey.HasCommandSigningKey);
        Assert.False(shortKey.HasValidCommandSigningKey);
        var validKey = new Application.Scheduling.SchedulingSettings(new string('k', 32));
        Assert.True(validKey.HasCommandSigningKey);
        Assert.True(validKey.HasValidCommandSigningKey);
    }

    [Fact]
    public async Task Rereview4_Team_attendance_allows_historical_read_and_fill_when_area_was_later_deactivated()
    {
        using var factory = new JanitorialApiFactory();
        var starts = new DateOnly(2026, 10, 1);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, starts);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A", ShiftPattern.DayAndNight);
        await ScheduleWriteApi.AddProfileAsync(factory, "1000001", CleanerKind.Regular, Shift.Day, starts);
        await ScheduleWriteApi.AddWeekdayBaselineAsync(factory, "1000001", starts, "AR03", breakGroup: 1);
        var cleanerId = await AdminSetupApi.UserIdAsync(factory, "1000001");

        // Deactivate AR03 after yesterday's shift (2026-10-08).
        await factory.WithDbAsync(async db =>
        {
            var area = await db.Areas.SingleAsync(a => a.Id == ar03);
            area.IsActive = false;
            await db.SaveChangesAsync();
        });

        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");
        factory.Clock.SetUtcNow(EffectiveAttendanceApi.Thai(9, 10, 0)); // 2026-10-09 10:00 Thai

        var read = await TeamAttendanceApi.StateAsync(supervisor, cleanerId, "2026-10-08", "Day");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var rev = await TeamAttendanceApi.RevisionAsync(factory, cleanerId, Shift.Day);
        var add = await TeamAttendanceApi.AddAsync(supervisor, cleanerId, "ShiftIn", "07:05",
            expectedRevision: rev, shiftDate: "2026-10-08", shift: "Day");
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
    }
}
