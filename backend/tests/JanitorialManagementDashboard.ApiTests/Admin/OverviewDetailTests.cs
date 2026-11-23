using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.ApiTests.Scheduling;
using JanitorialManagementDashboard.Application.Overview;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Admin;

public class OverviewDetailTests
{
    [Fact]
    public async Task Transfer_keeps_entry_evidence_and_responsibility_separate_across_pending_arrival_and_cancel()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var destination = await TestData.AddAreaAsync(factory, "AR03", "A");
        var area4 = await TestData.AddAreaAsync(factory, "AR04", "A");
        await TransferApi.LocateAsync(factory, TransferApi.Destination);
        await OverviewTestData.StartHistoryAsync(factory, TransferApi.Thai(1, 0));

        // 07:05 Check-in at AR01
        await TransferApi.CheckInAsync(factory, cleaner, TransferApi.Thai(7, 5));

        // 13:00 Transfer instruction AR01 -> AR03 (effective 13:00, unconfirmed)
        var transferId = await TransferApi.InstructForIdAsync(
            factory, admin, cleanerAuth.User.Id, origin, destination, TransferApi.Thai(13, 0));

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 1));
        var pendingOrigin = await GetDetailAsync(admin, origin, "2026-10-08", "Day", "entered");
        Assert.Equal("entered", pendingOrigin.GetProperty("area").GetProperty("entry").GetString());
        Assert.Single(pendingOrigin.GetProperty("entryEvidence").EnumerateArray());
        Assert.False(pendingOrigin.TryGetProperty("currentResponsibility", out _));

        var pendingDest = await GetDetailAsync(admin, destination, "2026-10-08", "Day", "covered");
        Assert.Equal("covered", pendingDest.GetProperty("area").GetProperty("entry").GetString());
        Assert.Empty(pendingDest.GetProperty("entryEvidence").EnumerateArray());
        var respPending = pendingDest.GetProperty("currentResponsibility");
        Assert.Equal(cleanerAuth.User.Id, respPending.GetProperty("userId").GetInt32());
        Assert.Equal("instructed", respPending.GetProperty("state").GetString());
        Assert.False(respPending.TryGetProperty("lastConfirmedAreaId", out _));
        Assert.False(respPending.TryGetProperty("confirmedAt", out _));

        // 13:05 Destination arrival confirmed -> destination enters; origin remains entered; Shift-In stays on origin only.
        var confirm = await TransferApi.ConfirmAsync(factory, cleaner, transferId, "1", at: TransferApi.Thai(13, 5));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        factory.Clock.SetUtcNow(TransferApi.Thai(13, 6));
        var confirmedDest = await GetDetailAsync(admin, destination, "2026-10-08", "Day", "entered");
        Assert.Equal("entered", confirmedDest.GetProperty("area").GetProperty("entry").GetString());
        var destEvidence = Assert.Single(confirmedDest.GetProperty("entryEvidence").EnumerateArray());
        Assert.Equal("transfer-arrival", destEvidence.GetProperty("kind").GetString());
        Assert.Equal(transferId, destEvidence.GetProperty("transferId").GetInt32());
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, destEvidence.GetProperty("recordedAt").GetDateTimeOffset().UtcDateTime);

        var respConfirmed = confirmedDest.GetProperty("currentResponsibility");
        Assert.Equal("confirmed", respConfirmed.GetProperty("state").GetString());
        Assert.Equal(destination, respConfirmed.GetProperty("lastConfirmedAreaId").GetInt32());
        Assert.Equal(TransferApi.Thai(13, 5).UtcDateTime, respConfirmed.GetProperty("confirmedAt").GetDateTimeOffset().UtcDateTime);

        // Forward instruction AR03 -> AR04 (effective 15:00): pending at AR04 keeps lastConfirmedAreaId = AR03!
        await TransferApi.InstructForIdAsync(
            factory, admin, cleanerAuth.User.Id, destination, area4, TransferApi.Thai(15, 0));
        factory.Clock.SetUtcNow(TransferApi.Thai(15, 1));
        var pendingArea4 = await GetDetailAsync(admin, area4, "2026-10-08", "Day", "covered");
        var respArea4 = pendingArea4.GetProperty("currentResponsibility");
        Assert.Equal("instructed", respArea4.GetProperty("state").GetString());
        Assert.Equal(destination, respArea4.GetProperty("lastConfirmedAreaId").GetInt32());
        Assert.False(respArea4.TryGetProperty("confirmedAt", out _));

        // Destination facts must NEVER contain the origin's Shift-In attendance row.
        Assert.Empty(confirmedDest.GetProperty("facts").GetProperty("attendance").EnumerateArray());

        // Origin stays entered, and calling detail with group=notEntered returns matchesGroup=false cleanly.
        var originAfter = await GetDetailAsync(admin, origin, "2026-10-08", "Day", "notEntered");
        Assert.Equal("entered", originAfter.GetProperty("area").GetProperty("entry").GetString());
        Assert.False(originAfter.GetProperty("matchesGroup").GetBoolean());
        var originShiftIn = Assert.Single(originAfter.GetProperty("facts").GetProperty("attendance").EnumerateArray());
        Assert.Equal(origin, originShiftIn.GetProperty("areaId").GetInt32());
    }

    [Fact]
    public async Task Cancelled_transfer_and_relief_assignment_keep_entry_and_assignment_distinct()
    {
        using var factory = ReliefApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner, cleanerAuth, _) = await AuthApi.LoggedInAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var sourceB = await TestData.AddBuildingAsync(factory, "B");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A");
        var ar04 = await TestData.AddAreaAsync(factory, "AR04", "A");
        var reliefUserId = await ReliefApi.AddReliefAsync(factory, admin, "E3001", "B", Shift.Day);
        var (reliefClient, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "E3001", ReliefApi.Phone("E3001"));

        var date = DateOnly.FromDateTime(factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).AddDays(1);
        var dateString = ReliefApi.Iso(date);
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "A", Shift.Day, date.AddDays(-2));
        await TestData.AddSupervisorScopeAsync(factory, "2000001", "B", Shift.Day, date.AddDays(-2));
        var (supervisor, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "2000001", "0820000001");

        var assign = await ReliefApi.AssignAsync(supervisor, ReliefApi.Assignment(reliefUserId, ar04, date, sourceB, breakGroup: 1));
        Assert.Equal(ReliefApi.Committed, assign.Answer!.Outcome);

        var baselineAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(1, 0)), TimeSpan.FromHours(7));
        await OverviewTestData.StartHistoryAsync(factory, baselineAt);

        // 07:05: regular cleaner1 checks in at AR01; relief has NOT checked in yet -> AR04 is covered!
        var checkInAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(7, 5)), TimeSpan.FromHours(7));
        await AttendanceApi.CheckInAsync(factory, cleaner, checkInAt);

        // Transfer instructed for 13:00 then cancelled at 12:55 -> AR03 stays unassigned.
        var transferAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(13, 0)), TimeSpan.FromHours(7));
        factory.Clock.SetUtcNow(transferAt.AddMinutes(-10));
        var (_, instAnswer) = await ScheduleWriteApi.ExecuteAsync(admin, TransferApi.Instruct, new
        {
            userId = cleanerAuth.User.Id,
            shiftDate = dateString,
            shift = "Day",
            fromAreaId = ar01,
            toAreaId = ar03,
            effectiveAt = transferAt.ToUniversalTime().ToString("O"),
            reason = "test",
        });
        Assert.Equal(ReliefApi.Committed, instAnswer!.Outcome);
        int transferId = 0;
        await factory.WithDbAsync(async db => transferId = (await db.AreaTransfers.OrderBy(t => t.Id).LastAsync()).Id);

        factory.Clock.SetUtcNow(transferAt.AddMinutes(-5));
        await TransferApi.CancelAsync(admin, transferId,
            expected: new() { [$"transfer:{cleanerAuth.User.Id}:{dateString}:Day"] = "1" });

        factory.Clock.SetUtcNow(transferAt.AddMinutes(5));
        var ar03Detail = await GetDetailAsync(admin, ar03, dateString, "Day", "unassigned");
        Assert.Equal("unassigned", ar03Detail.GetProperty("area").GetProperty("entry").GetString());
        Assert.False(ar03Detail.TryGetProperty("currentResponsibility", out _));

        var ar04Before = await GetDetailAsync(admin, ar04, dateString, "Day", "covered");
        Assert.Equal("covered", ar04Before.GetProperty("area").GetProperty("entry").GetString());
        Assert.Empty(ar04Before.GetProperty("entryEvidence").EnumerateArray());
        _ = buildingA;

        // Relief checks in at AR04 -> AR04 becomes entered!
        await EffectiveAttendanceApi.RecordAsync(reliefClient, "ShiftIn", "token-checkin-ar04");
        var ar04After = await GetDetailAsync(admin, ar04, dateString, "Day", "entered");
        Assert.Equal("entered", ar04After.GetProperty("area").GetProperty("entry").GetString());
        Assert.Single(ar04After.GetProperty("entryEvidence").EnumerateArray());
    }

    [Fact]
    public async Task Area_detail_filters_facts_to_the_selected_area_and_includes_point_metadata()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleaner1, _, _) = await AuthApi.LoggedInAsync(factory);
        var (cleaner3, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000003", "0810000003");
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        await AttendanceApi.CheckInAsync(factory, cleaner1, ThaiClock.At(8, 7, 5), "token-checkin-ar01");
        await AttendanceApi.CheckInAsync(factory, cleaner3, ThaiClock.At(8, 7, 6), "token-checkin-ar02");
        var scan1 = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(8, 7, 20),
            CleaningStatus.Issue, "wet_floor", "น้ำรั่ว <script>alert(1)</script>");
        await TestData.AddInspectionAsync(factory, scan1, InspectionResult.Rework, ThaiClock.At(8, 7, 30));
        await TestData.AddScanAsync(factory, "token-meeting-room", ThaiClock.At(8, 8, 10));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 15));
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var detail = await GetDetailAsync(admin, ar01, "2026-10-08", "Day", "rework");
        OverviewEndpointTests.AssertSafeJsonProperties(detail.GetRawText());

        var facts = detail.GetProperty("facts");
        var submissions = facts.GetProperty("submissions").EnumerateArray().ToArray();
        var inspections = facts.GetProperty("inspections").EnumerateArray().ToArray();
        var attendance = facts.GetProperty("attendance").EnumerateArray().ToArray();
        var people = facts.GetProperty("people").EnumerateArray().ToArray();

        Assert.Single(submissions);
        Assert.Equal("น้ำรั่ว <script>alert(1)</script>", submissions[0].GetProperty("note").GetString());
        Assert.Equal("wet_floor", submissions[0].GetProperty("issueTags").GetString());
        Assert.Single(inspections);
        Assert.Equal("พื้นยังเปียก", inspections[0].GetProperty("defect").GetString());
        Assert.Single(attendance);
        Assert.Equal(ar01, attendance[0].GetProperty("areaId").GetInt32());

        // People must contain ONLY cleaner1 (1000001) and supervisor (2000001), NEVER cleaner3 (1000003).
        var cleaner3Id = await AdminSetupApi.UserIdAsync(factory, "1000003");
        Assert.DoesNotContain(people, p => p.GetProperty("id").GetInt32() == cleaner3Id);
        Assert.Equal(2, people.Length);

        // Point detail includes code, name, sortOrder, roundKey, start, end!
        var points = detail.GetProperty("points").EnumerateArray().ToArray();
        Assert.Equal(2, points.Length);
        Assert.Equal("AR01-01", points[0].GetProperty("code").GetString());
        Assert.Equal("ห้องน้ำชาย ชั้น 1", points[0].GetProperty("name").GetString());
        Assert.Equal(1, points[0].GetProperty("sortOrder").GetInt32());
        Assert.Equal("07:00:00", points[0].GetProperty("start").GetString());
        Assert.Equal("09:00:00", points[0].GetProperty("end").GetString());
        Assert.False(string.IsNullOrEmpty(points[0].GetProperty("roundKey").GetString()));
    }

    [Fact]
    public async Task Historical_moved_and_deactivated_area_and_roundKey_validation_work_across_building_scopes()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var buildingB = await TestData.AddBuildingAsync(factory, "B");
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A");
        var p3 = await AddPointAsync(factory, ar03, "AR03-01", "tok-ar03", 8, 9);
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        // Replace window at 09:30 while still in Building A so 08:00-09:00 finalizes under Building A.
        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 30));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/points/{p3}", new
        {
            name = "จุด AR03-01",
            roundWindows = new[] { new { shift = "Day", start = "11:00", end = "12:00" } },
        })).StatusCode);

        // 10:00: Move AR03 from Building A to Building B, then deactivate AR03 at 14:00.
        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/areas/{ar03}", new
        {
            name = "AR03 Moved",
            buildingId = buildingB,
            shiftPattern = "DayAndNight",
        })).StatusCode);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 14, 0));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/areas/{ar03}/deactivate", null)).StatusCode);

        // Delete the old window so only the revision history knows about it!
        await factory.WithDbAsync(async db =>
        {
            await db.PointRoundWindows.Where(w => w.ServicePointId == p3).ExecuteDeleteAsync();
        });

        factory.Clock.SetUtcNow(ThaiClock.At(8, 15, 0));
        var detail = await GetDetailAsync(admin, ar03, "2026-10-08", "Day", "missedCleaning");
        var rounds = detail.GetProperty("rounds").EnumerateArray().ToArray();
        Assert.Equal(2, rounds.Length);
        var morningRoundA = rounds[0];
        var roundKey = morningRoundA.GetProperty("roundKey").GetString()!;
        Assert.Equal("configurationChanged", morningRoundA.GetProperty("round").GetProperty("finalizedReason").GetString());
        Assert.Equal("notDone", morningRoundA.GetProperty("outcome").GetString());
        Assert.Equal(buildingA, morningRoundA.GetProperty("round").GetProperty("buildingId").GetInt32());

        // Under Building A filter, historical morning round in A is still resolvable!
        var detailInA = await admin.GetAsync(
            $"/api/admin/overview/areas/{ar03}?shiftDate=2026-10-08&shift=Day&buildingId={buildingA}&group=missedCleaning&roundKey={Uri.EscapeDataString(roundKey)}");
        Assert.Equal(HttpStatusCode.OK, detailInA.StatusCode);
        var detailInAJson = await detailInA.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(roundKey, detailInAJson.GetProperty("selectedRoundKey").GetString());

        // Foreign roundKey on AR01 (different Area) -> 404!
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(
            $"/api/admin/overview/areas/{ar01}?shiftDate=2026-10-08&shift=Day&roundKey={Uri.EscapeDataString(roundKey)}")).StatusCode);

        // Foreign roundKey from different slot (2026-10-07) -> 404!
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(
            $"/api/admin/overview/areas/{ar03}?shiftDate=2026-10-07&shift=Day&roundKey={Uri.EscapeDataString(roundKey)}")).StatusCode);

        // Foreign roundKey from Building A when querying Building B scope -> 404!
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(
            $"/api/admin/overview/areas/{ar03}?shiftDate=2026-10-08&shift=Day&buildingId={buildingB}&roundKey={Uri.EscapeDataString(roundKey)}")).StatusCode);
    }

    [Fact]
    public async Task Ambiguous_legacy_origin_emits_unresolved_outcome_and_narrow_unresolved_submissions()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var p1 = await AdminSetupApi.PointIdAsync(factory, "AR01-01");
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        // 08:30: replace window on AR01-01 with a NEW 07:00-09:00 window (new OriginId, same time) and capture revision.
        var transitionAt = ThaiClock.At(8, 8, 30);
        factory.Clock.SetUtcNow(transitionAt);
        await factory.WithDbAsync(async db =>
        {
            var oldWindow = await db.PointRoundWindows.SingleAsync(
                w => w.ServicePointId == p1 && w.Shift == Shift.Day && w.StartTime == new TimeOnly(7, 0));
            db.PointRoundWindows.Remove(oldWindow);
            db.PointRoundWindows.Add(new PointRoundWindow
            {
                ServicePointId = p1,
                Shift = Shift.Day,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(9, 0),
                CreatedAt = transitionAt.UtcDateTime,
            });
            await db.SaveChangesAsync();
            var settings = new JanitorialManagementDashboard.Application.Attendance.AttendanceSettings();
            var recorder = new JanitorialManagementDashboard.Infrastructure.Persistence.Overview.OverviewTargetRecorder(db, settings, factory.Clock);
            await using var lease = await new JanitorialManagementDashboard.Infrastructure.Persistence.Overview.OverviewMutation(db, recorder).BeginAsync(default);
            await lease.CommitAsync(default);
        });

        // Insert a legacy unlinked ScanRecord right at the 08:30 transition (OriginId = null, RoundWindowId = null):
        // matches BOTH the 07:00-09:00 interval ending at 08:30 and the new 07:00-09:00 interval starting at 08:30!
        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var sign = await db.Signs.SingleAsync(s => s.ServicePointId == p1);
            db.ScanRecords.Add(new ScanRecord
            {
                ServicePointId = p1,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Day,
                RoundWindowId = null,
                RoundOriginId = null,
                RoundStart = new TimeOnly(7, 0),
                RoundEnd = new TimeOnly(9, 0),
                Placement = Placement.OnTime,
                Status = CleaningStatus.Normal,
                Note = "should-not-duplicate-in-unresolved-dto",
                SubmittedAt = ThaiClock.At(8, 8, 30).UtcDateTime,
            });
            await db.SaveChangesAsync();
        });

        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        Assert.Equal("partial", summary.GetProperty("availability").GetProperty("work").GetProperty("state").GetString());
        Assert.Contains("originsIncomplete",
            summary.GetProperty("availability").GetProperty("work").GetProperty("reasons").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("groups").GetProperty("missedCleaning").GetProperty("value").ValueKind);

        var detail = await GetDetailAsync(admin, ar01, "2026-10-08", "Day", "incomplete");
        Assert.True(detail.GetProperty("area").GetProperty("originsIncomplete").GetBoolean());
        Assert.False(detail.GetProperty("facts").GetProperty("originsComplete").GetBoolean());

        var round = Assert.Single(detail.GetProperty("rounds").EnumerateArray());
        Assert.Equal(OverviewOutcomes.Unresolved, round.GetProperty("outcome").GetString());
        Assert.True(round.GetProperty("isPartial").GetBoolean());
        var unresolved = Assert.Single(round.GetProperty("unresolvedSubmissions").EnumerateArray());
        Assert.Equal(p1, unresolved.GetProperty("pointId").GetInt32());
        Assert.Equal("OnTime", unresolved.GetProperty("placement").GetString());
        Assert.False(unresolved.TryGetProperty("note", out _));
        Assert.False(unresolved.TryGetProperty("userId", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Batched_issue_lookup_returns_only_latest_submission_across_slots_with_id_tie_break(bool overlappingShifts)
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var p1 = await AdminSetupApi.PointIdAsync(factory, "AR01-01");
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(5, 1, 0));

        // Older scan on Oct 5 (Normal)
        await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(5, 7, 30), CleaningStatus.Normal);

        // Equal timestamps: the higher ID wins even if the lower ID belongs to the selected shift.
        long winningId = 0;
        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var sign = await db.Signs.SingleAsync(s => s.ServicePointId == p1);
            var equalAt = (overlappingShifts ? ThaiClock.At(8, 19, 10) : ThaiClock.At(6, 12, 0)).UtcDateTime;
            var scanDate = new DateOnly(2026, 10, overlappingShifts ? 8 : 6);
            var first = new ScanRecord
            {
                ServicePointId = p1,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = scanDate,
                Shift = Shift.Day,
                Placement = Placement.OffRound,
                Status = CleaningStatus.Normal,
                SubmittedAt = equalAt,
            };
            var second = new ScanRecord
            {
                ServicePointId = p1,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = scanDate,
                Shift = overlappingShifts ? Shift.Night : Shift.Day,
                Placement = Placement.OffRound,
                Status = CleaningStatus.Issue,
                IssueTags = "broken_tap",
                SubmittedAt = equalAt,
            };
            db.ScanRecords.AddRange(first, second);
            await db.SaveChangesAsync();
            winningId = second.Id;
        });

        factory.Clock.SetUtcNow(overlappingShifts ? ThaiClock.At(8, 19, 20) : ThaiClock.At(8, 10, 0));
        var detail = await GetDetailAsync(admin, ar01, "2026-10-08", "Day", "issue");
        Assert.True(detail.GetProperty("area").GetProperty("work").GetProperty("issue").GetBoolean());
        var submissions = detail.GetProperty("facts").GetProperty("submissions").EnumerateArray().ToArray();
        Assert.Equal(overlappingShifts ? 2 : 1, submissions.Length);
        var sub = Assert.Single(submissions, submission => submission.GetProperty("id").GetInt64() == winningId);
        Assert.Equal(winningId, sub.GetProperty("id").GetInt64());
        Assert.Equal("Issue", sub.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pre_baseline_area_detail_is_inspectable_without_building_filter_and_404_with_unproven_building()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
        var ar01 = await AdminSetupApi.AreaIdAsync(factory, "AR01");

        await TestData.AddAttendanceAsync(factory, "1000001", new DateOnly(2026, 10, 6), Shift.Day,
            AttendanceEvent.ShiftIn, ThaiClock.At(6, 7, 5));
        var scan = await TestData.AddScanAsync(factory, "token-restroom-m1", ThaiClock.At(6, 7, 25), CleaningStatus.Issue);
        await TestData.AddInspectionAsync(factory, scan, InspectionResult.Rework, ThaiClock.At(6, 7, 40));
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 0));
        var detail = await GetDetailAsync(admin, ar01, "2026-10-06", "Day", "entered");
        Assert.Equal("partial", detail.GetProperty("availability").GetProperty("work").GetProperty("state").GetString());
        Assert.Equal("AR01", detail.GetProperty("area").GetProperty("code").GetString());
        Assert.Single(detail.GetProperty("facts").GetProperty("attendance").EnumerateArray());
        Assert.Single(detail.GetProperty("facts").GetProperty("submissions").EnumerateArray());
        Assert.Single(detail.GetProperty("facts").GetProperty("inspections").EnumerateArray());
        var point = Assert.Single(detail.GetProperty("points").EnumerateArray());
        Assert.Equal("AR01-01", point.GetProperty("code").GetString());
        Assert.Equal("ห้องน้ำชาย ชั้น 1", point.GetProperty("name").GetString());

        // When building ownership cannot be proved before baseline, building-filtered Area detail returns 404.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(
            $"/api/admin/overview/areas/{ar01}?shiftDate=2026-10-06&shift=Day&buildingId={buildingA}")).StatusCode);
    }

    [Fact]
    public async Task Transfer_temporal_filter_preserves_prior_confirmed_and_uses_persons_own_confirmation_time()
    {
        using var factory = TransferApi.NewFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (cleanerA, cleanerAAuth, _) = await AuthApi.LoggedInAsync(factory);
        var origin = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var destB = await TestData.AddAreaAsync(factory, "AR03", "A");
        var destC = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        await TransferApi.LocateAsync(factory, TransferApi.Destination);
        await TransferApi.LocateAsync(factory, "token-checkin-ar02");
        await OverviewTestData.StartHistoryAsync(factory, TransferApi.Thai(1, 0));

        await TransferApi.CheckInAsync(factory, cleanerA, TransferApi.Thai(7, 5));

        // 08:00 T1 instructed AR01 -> AR03, confirmed at 08:10.
        var (t1Resp, t1Ans) = await TransferApi.InstructAsync(
            factory, admin, cleanerAAuth.User.Id, origin, destB, TransferApi.Thai(8, 0), at: TransferApi.Thai(8, 0));
        Assert.Equal(HttpStatusCode.OK, t1Resp.StatusCode);
        Assert.Equal(ReliefApi.Committed, t1Ans!.Outcome);
        var t1 = (await TransferApi.InstructionsAsync(factory))[^1];
        Assert.Equal(HttpStatusCode.OK,
            (await TransferApi.ConfirmAsync(factory, cleanerA, t1.Id, t1.Revision.ToString(), at: TransferApi.Thai(8, 10))).StatusCode);

        // Cleaner B transfers from AR01 to AR03 (confirmed 09:30), then moves to AR02 (confirmed 09:45),
        // leaving a later TransferArrival in AR03 that belongs to Cleaner B, not Cleaner A.
        var cleanerBId = await TestData.AddCleanerAsync(factory, "E2099", Shift.Day);
        await factory.WithDbAsync(async db =>
        {
            var destBSignId = await db.Signs.Where(s => s.AreaId == destB && s.ServicePointId == null).Select(s => s.Id).FirstOrDefaultAsync();
            var destCSignId = await db.Signs.Where(s => s.AreaId == destC && s.ServicePointId == null).Select(s => s.Id).FirstOrDefaultAsync();
            var anySignId = await db.Signs.Select(s => s.Id).FirstAsync();

            var b1 = new AreaTransfer
            {
                PersonId = cleanerBId,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Day,
                FromAreaId = origin,
                ToAreaId = destB,
                EffectiveAt = TransferApi.Thai(9, 20).UtcDateTime,
                CreatedAt = TransferApi.Thai(9, 20).UtcDateTime,
                UpdatedAt = TransferApi.Thai(9, 20).UtcDateTime,
                CreatedById = cleanerAAuth.User.Id,
            };
            var b2 = new AreaTransfer
            {
                PersonId = cleanerBId,
                ShiftDate = new DateOnly(2026, 10, 8),
                Shift = Shift.Day,
                FromAreaId = destB,
                ToAreaId = destC,
                EffectiveAt = TransferApi.Thai(9, 40).UtcDateTime,
                CreatedAt = TransferApi.Thai(9, 40).UtcDateTime,
                UpdatedAt = TransferApi.Thai(9, 40).UtcDateTime,
                CreatedById = cleanerAAuth.User.Id,
            };
            db.AreaTransfers.AddRange(b1, b2);
            await db.SaveChangesAsync();

            db.AreaTransferConfirmations.AddRange(
                new AreaTransferConfirmation
                {
                    TransferId = b1.Id,
                    AreaId = destB,
                    SignId = destBSignId == 0 ? anySignId : destBSignId,
                    ConfirmedById = cleanerBId,
                    OccurredAt = TransferApi.Thai(9, 30).UtcDateTime,
                    Source = AttendanceSource.Scan,
                    DistanceM = 5,
                    WithinRadius = true,
                    CreatedAt = TransferApi.Thai(9, 30).UtcDateTime,
                },
                new AreaTransferConfirmation
                {
                    TransferId = b2.Id,
                    AreaId = destC,
                    SignId = destCSignId == 0 ? anySignId : destCSignId,
                    ConfirmedById = cleanerBId,
                    OccurredAt = TransferApi.Thai(9, 45).UtcDateTime,
                    Source = AttendanceSource.Scan,
                    DistanceM = 5,
                    WithinRadius = true,
                    CreatedAt = TransferApi.Thai(9, 45).UtcDateTime,
                });
            await db.SaveChangesAsync();
        });

        // 09:50 T2 forward AR03 -> AR02 (effective 10:00), cancelled at 09:55;
        // 10:05 T3 forward AR03 -> AR02, confirmed at 10:20.
        var (t2Resp, t2Ans) = await TransferApi.InstructAsync(
            factory, admin, cleanerAAuth.User.Id, destB, destC, TransferApi.Thai(10, 0), at: TransferApi.Thai(9, 50));
        Assert.Equal(HttpStatusCode.OK, t2Resp.StatusCode);
        Assert.Equal(ReliefApi.Committed, t2Ans!.Outcome);
        var t2 = (await TransferApi.InstructionsAsync(factory))[^1];
        factory.Clock.SetUtcNow(TransferApi.Thai(9, 55));
        var (cancelResp, cancelAns) = await TransferApi.CancelAsync(
            admin, t2.Id, "cancel_forward",
            expected: new() { [TransferApi.Key(cleanerAAuth.User.Id)] = t2.Revision.ToString() });
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);
        Assert.Equal(ReliefApi.Committed, cancelAns!.Outcome);

        var (t3Resp, t3Ans) = await TransferApi.InstructAsync(
            factory, admin, cleanerAAuth.User.Id, destB, destC, TransferApi.Thai(10, 5), at: TransferApi.Thai(10, 5));
        Assert.Equal(HttpStatusCode.OK, t3Resp.StatusCode);
        Assert.Equal(ReliefApi.Committed, t3Ans!.Outcome);
        var t3 = (await TransferApi.InstructionsAsync(factory))[^1];
        Assert.Equal(HttpStatusCode.OK,
            (await TransferApi.ConfirmAsync(
                factory, cleanerA, t3.Id, t3.Revision.ToString(), token: "token-checkin-ar02", at: TransferApi.Thai(10, 20))).StatusCode);

        // At 08:05 (before T1 confirmed at 08:10): instructed only, no leaked LastConfirmedAreaId.
        factory.Clock.SetUtcNow(TransferApi.Thai(8, 5));
        var at0805 = await GetDetailAsync(admin, destB, "2026-10-08", "Day", "covered");
        var resp0805 = at0805.GetProperty("currentResponsibility");
        Assert.Equal("instructed", resp0805.GetProperty("state").GetString());
        Assert.False(resp0805.TryGetProperty("lastConfirmedAreaId", out _));
        Assert.False(resp0805.TryGetProperty("confirmedAt", out _));

        // At 09:00 (after T1 confirmed, before T2/T3 CreatedAt): T1 must NOT be dropped.
        factory.Clock.SetUtcNow(TransferApi.Thai(9, 0));
        var at0900 = await GetDetailAsync(admin, destB, "2026-10-08", "Day", "entered");
        var resp0900 = at0900.GetProperty("currentResponsibility");
        Assert.Equal(cleanerAAuth.User.Id, resp0900.GetProperty("userId").GetInt32());
        Assert.Equal("confirmed", resp0900.GetProperty("state").GetString());
        Assert.Equal(destB, resp0900.GetProperty("lastConfirmedAreaId").GetInt32());
        Assert.Equal(TransferApi.Thai(8, 10).UtcDateTime, resp0900.GetProperty("confirmedAt").GetDateTimeOffset().UtcDateTime);

        // At 10:00 (after T2 cancelled at 09:55, before T3 created at 10:05):
        // AR03 still reports Cleaner A's exact 08:10 confirmation time, NOT Cleaner B's 09:30 arrival!
        factory.Clock.SetUtcNow(TransferApi.Thai(10, 0));
        var at1000 = await GetDetailAsync(admin, destB, "2026-10-08", "Day", "entered");
        var resp1000 = at1000.GetProperty("currentResponsibility");
        Assert.Equal(cleanerAAuth.User.Id, resp1000.GetProperty("userId").GetInt32());
        Assert.Equal("confirmed", resp1000.GetProperty("state").GetString());
        Assert.Equal(destB, resp1000.GetProperty("lastConfirmedAreaId").GetInt32());
        Assert.Equal(TransferApi.Thai(8, 10).UtcDateTime, resp1000.GetProperty("confirmedAt").GetDateTimeOffset().UtcDateTime);
    }

    [Fact]
    public async Task Deactivated_zero_round_area_with_target_history_stays_inspectable_without_joining_work_groups()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03", "A");
        await AddPointAsync(factory, ar03, "AR03-01", "tok-ar03-a", 8, 9);
        await AddPointAsync(factory, ar03, "AR03-02", "tok-ar03-b", 8, 9);

        // Start history at 01:00, then deactivate AR03 at 02:00 before its 08:00-09:00 windows open,
        // so AR03 has 0 target rounds in a target-tracked slot.
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));
        factory.Clock.SetUtcNow(ThaiClock.At(8, 2, 0));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/areas/{ar03}/deactivate", null)).StatusCode);

        var issueScan = await TestData.AddScanAsync(factory, "tok-ar03-a", ThaiClock.At(8, 8, 5), CleaningStatus.Issue, "broken_lock");
        await TestData.AddInspectionAsync(factory, issueScan, InspectionResult.Rework, ThaiClock.At(8, 8, 15));
        await TestData.AddScanAsync(factory, "tok-ar03-b", ThaiClock.At(8, 8, 10), CleaningStatus.Normal);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));
        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        var groups = summary.GetProperty("groups");
        Assert.Equal(0, groups.GetProperty("issue").GetProperty("value").GetInt32());
        Assert.Equal(0, groups.GetProperty("rework").GetProperty("value").GetInt32());
        Assert.Equal(0, groups.GetProperty("pendingInspection").GetProperty("value").GetInt32());

        foreach (var g in new[] { "issue", "rework", "pendingInspection" })
        {
            var list = await admin.GetFromJsonAsync<JsonElement>(
                $"/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group={g}");
            Assert.Equal(0, list.GetProperty("total").GetProperty("value").GetInt32());
            Assert.Empty(list.GetProperty("rows").EnumerateArray());

            var detail = await GetDetailAsync(admin, ar03, "2026-10-08", "Day", g);
            Assert.False(detail.GetProperty("matchesGroup").GetBoolean());
            Assert.Empty(detail.GetProperty("area").GetProperty("reasons").EnumerateArray());
            Assert.True(detail.GetProperty("area").GetProperty("work").GetProperty("issue").GetBoolean());
            Assert.True(detail.GetProperty("area").GetProperty("work").GetProperty("rework").GetBoolean());
            Assert.True(detail.GetProperty("area").GetProperty("work").GetProperty("pendingInspection").GetBoolean());
            Assert.Equal(2, detail.GetProperty("points").EnumerateArray().Count());
            Assert.Equal(2, detail.GetProperty("facts").GetProperty("submissions").EnumerateArray().Count());
        }
    }

    [Fact]
    public async Task Scoped_attendance_keeps_latest_correction_source_and_only_unresolved_stored_skips()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, cleaner, _) = await AuthApi.LoggedInAsync(factory);
        var userId = cleaner.User.Id;
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var otherArea = await AdminSetupApi.AreaIdAsync(factory, "AR02");
        var date = new DateOnly(2026, 10, 8);
        DateTime At(int hour, int minute = 0) => ThaiClock.At(8, hour, minute).UtcDateTime;
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        await factory.WithDbAsync(async db =>
        {
            var entered = new ShiftAttendance
            {
                UserId = userId, AreaId = area, ShiftDate = date, Shift = Shift.Day,
                EventType = AttendanceEvent.Break1In, OccurredAt = At(10),
                CreatedAt = At(10, 5), Source = AttendanceSource.SupervisorAdd,
            };
            var edited = new ShiftAttendance
            {
                UserId = userId, AreaId = area, ShiftDate = date, Shift = Shift.Day,
                EventType = AttendanceEvent.Break2In, OccurredAt = At(11),
                CreatedAt = At(11, 5), Source = AttendanceSource.AdminEdit,
            };
            db.ShiftAttendances.AddRange(entered, edited);
            db.ShiftAttendances.Add(new ShiftAttendance
            {
                UserId = userId, AreaId = otherArea, ShiftDate = date, Shift = Shift.Day,
                EventType = AttendanceEvent.ShiftIn, OccurredAt = At(7),
                CreatedAt = At(7), Source = AttendanceSource.Scan,
            });
            await db.SaveChangesAsync();

            db.AuditLog.AddRange(
                new AuditEntry
                {
                    EntityType = "shift_attendances", EntityId = entered.Id, Action = "ATTENDANCE_ADD",
                    OccurredAt = At(10, 5), Reason = "เหตุผลการเติมเวลา",
                },
                new AuditEntry
                {
                    EntityType = "shift_attendances", EntityId = entered.Id, Action = "ATTENDANCE_EDIT",
                    OccurredAt = At(10, 15), Reason = "แอดมินแก้หลังจากหัวหน้าเติม",
                },
                new AuditEntry
                {
                    EntityType = "shift_attendances", EntityId = edited.Id, Action = "ATTENDANCE_ADD",
                    OccurredAt = At(11, 5), Reason = "เหตุผลเดิม",
                },
                new AuditEntry
                {
                    EntityType = "shift_attendances", EntityId = edited.Id, Action = "ATTENDANCE_EDIT",
                    OccurredAt = At(11, 10), Reason = "เหตุผลล่าสุด",
                });
            AttendanceGap Gap(int areaId, DateOnly shiftDate, AttendanceEvent action, AttendanceGapStatus status) => new()
            {
                UserId = userId, AreaId = areaId, ShiftDate = shiftDate, Shift = Shift.Day,
                EventType = action, Status = status, Reason = "ข้ามรายการ", RecordedAt = At(9, 30), CreatedAt = At(9, 30),
            };
            db.AttendanceGaps.AddRange(
                Gap(area, date, AttendanceEvent.Break1Out, AttendanceGapStatus.Skipped),
                Gap(area, date, AttendanceEvent.Break1In, AttendanceGapStatus.Resolved),
                Gap(otherArea, date, AttendanceEvent.Break2Out, AttendanceGapStatus.Skipped),
                Gap(area, date.AddDays(-1), AttendanceEvent.ShiftOut, AttendanceGapStatus.Skipped));
            db.ScheduleExceptions.Add(new ScheduleException
            {
                UserId = userId, Kind = ScheduleExceptionKind.Leave,
                ShiftDate = date, Shift = Shift.Day, StartsAt = At(15), EndsAt = At(19),
                CreatedById = adminAuth.User.Id, CreatedAt = At(9), UpdatedAt = At(9),
            });
            await db.SaveChangesAsync();
        });

        factory.Clock.SetUtcNow(ThaiClock.At(8, 12, 0));
        var detail = await GetDetailAsync(admin, area, "2026-10-08", "Day", "notEntered");
        var facts = detail.GetProperty("facts");
        Assert.Collection(facts.GetProperty("attendance").EnumerateArray(), actual =>
        {
            Assert.True(actual.GetProperty("edited").GetBoolean());
            Assert.Equal(area, actual.GetProperty("areaId").GetInt32());
            Assert.Equal("SupervisorAdd", actual.GetProperty("source").GetString());
            var addition = actual.GetProperty("addition");
            Assert.Equal("เหตุผลการเติมเวลา", addition.GetProperty("reason").GetString());
            Assert.Equal(At(10, 5), addition.GetProperty("recordedAt").GetDateTime());
            var edit = actual.GetProperty("edit");
            Assert.Equal("แอดมินแก้หลังจากหัวหน้าเติม", edit.GetProperty("reason").GetString());
            Assert.Equal(At(10, 15), edit.GetProperty("recordedAt").GetDateTime());
        }, actual =>
        {
            Assert.Equal(area, actual.GetProperty("areaId").GetInt32());
            Assert.Equal("AdminEdit", actual.GetProperty("source").GetString());
            Assert.Equal("เหตุผลล่าสุด", actual.GetProperty("reason").GetString());
            Assert.Equal(At(11, 10), actual.GetProperty("correctedAt").GetDateTime());
        });
        var gap = Assert.Single(facts.GetProperty("attendanceGaps").EnumerateArray());
        Assert.Equal(area, gap.GetProperty("areaId").GetInt32());
        Assert.Equal("Break1Out", gap.GetProperty("event").GetString());
        Assert.Equal("ข้ามรายการ", gap.GetProperty("reason").GetString());
        Assert.Equal(At(9, 30), gap.GetProperty("recordedAt").GetDateTime());
        Assert.False(gap.TryGetProperty("occurredAt", out _));
        var leave = Assert.Single(facts.GetProperty("attendanceLeaves").EnumerateArray());
        Assert.Equal(userId, leave.GetProperty("userId").GetInt32());
        Assert.Equal(At(15), leave.GetProperty("startsAt").GetDateTime());
        Assert.Empty(detail.GetProperty("entryEvidence").EnumerateArray());
    }

    [Fact]
    public async Task Scoped_leave_reports_a_check_in_conflict_and_exempts_missing_numbered_breaks_after_departure()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, adminAuth, _) = await AuthApi.LoggedInAdminAsync(factory);
        var (_, cleaner, _) = await AuthApi.LoggedInAsync(factory);
        var area = await AdminSetupApi.AreaIdAsync(factory, "AR01");
        var date = new DateOnly(2026, 10, 8);
        DateTime At(int hour, int minute = 0) => ThaiClock.At(8, hour, minute).UtcDateTime;
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        int otherCleaner = 0;
        await factory.WithDbAsync(async db =>
        {
            var userId = cleaner.User.Id;
            otherCleaner = await db.Users.Where(u => u.Role == UserRole.Cleaner && u.Id != userId).Select(u => u.Id).FirstAsync();
            ShiftAttendance Recorded(int who, AttendanceEvent action, DateTime time) => new()
            {
                UserId = who, AreaId = area, ShiftDate = date, Shift = Shift.Day,
                EventType = action, OccurredAt = time, CreatedAt = time, Source = AttendanceSource.Scan,
            };
            ScheduleException Leave(int who, DateTime start, DateTime end) => new()
            {
                UserId = who, ShiftDate = date, Shift = Shift.Day, Kind = ScheduleExceptionKind.Leave,
                StartsAt = start, EndsAt = end, CreatedById = adminAuth.User.Id, CreatedAt = At(1), UpdatedAt = At(1),
            };
            db.ShiftAttendances.AddRange(
                Recorded(userId, AttendanceEvent.ShiftIn, At(7, 5)),
                Recorded(userId, AttendanceEvent.ShiftOut, At(12, 15)),
                Recorded(otherCleaner, AttendanceEvent.ShiftIn, At(7, 30)));
            db.ScheduleExceptions.AddRange(Leave(userId, At(12), At(19)), Leave(otherCleaner, At(7), At(9)));

            await db.SaveChangesAsync();
        });

        factory.Clock.SetUtcNow(ThaiClock.At(8, 15, 0));
        var detail = await GetDetailAsync(admin, area, "2026-10-08", "Day", "entered");
        var facts = detail.GetProperty("facts");
        Assert.Equal(3, facts.GetProperty("attendance").GetArrayLength());
        var effects = facts.GetProperty("attendanceLeaveEffects").EnumerateArray().ToArray();
        var conflict = Assert.Single(effects, e => e.GetProperty("kind").GetString() == "check-in-conflict");
        Assert.Equal(otherCleaner, conflict.GetProperty("userId").GetInt32());
        Assert.Equal("ShiftIn", conflict.GetProperty("event").GetString());
        var exempt = effects.Where(e => e.GetProperty("kind").GetString() == "exempt").ToArray();
        Assert.Equal(new[] { "Break2Out", "Break2In" },
            exempt.Select(e => e.GetProperty("event").GetString()).ToArray());
        Assert.All(exempt, e =>
        {
            Assert.Equal(cleaner.User.Id, e.GetProperty("userId").GetInt32());
            Assert.False(e.TryGetProperty("occurredAt", out _));
        });
    }

    [Fact]
    public async Task Multiple_upcoming_windows_for_a_point_have_distinct_window_ids_until_a_round_opens()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var area = await TestData.AddAreaAsync(factory, "AR03", "A");
        var pointId = await AddPointAsync(factory, area, "AR03-01", "tok-ar03-window-id", 10, 11);
        await factory.WithDbAsync(async db =>
        {
            db.PointRoundWindows.Add(new PointRoundWindow
            {
                ServicePointId = pointId, Shift = Shift.Day, StartTime = new TimeOnly(13, 0), EndTime = new TimeOnly(14, 0),
                CreatedAt = ThaiClock.At(8, 1, 0).UtcDateTime,
            });
            await db.SaveChangesAsync();
        });
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 1, 0));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 0));
        var before = await GetDetailAsync(admin, area, "2026-10-08", "Day", "upcoming");
        var windows = before.GetProperty("points").EnumerateArray().ToArray();
        Assert.Equal(new[] { "10:00:00", "13:00:00" }, windows.Select(p => p.GetProperty("start").GetString()).ToArray());
        Assert.Equal(2, windows.Select(p => p.GetProperty("windowId").GetInt32()).Distinct().Count());
        Assert.All(windows, p => Assert.Equal(pointId, p.GetProperty("work").GetProperty("pointId").GetInt32()));

        factory.Clock.SetUtcNow(ThaiClock.At(8, 10, 30));
        var after = await GetDetailAsync(admin, area, "2026-10-08", "Day", "upcoming");
        var current = Assert.Single(after.GetProperty("points").EnumerateArray());
        Assert.Equal("10:00:00", current.GetProperty("start").GetString());
        Assert.False(string.IsNullOrEmpty(current.GetProperty("roundKey").GetString()));
    }

    private static async Task<JsonElement> GetDetailAsync(
        HttpClient admin, int areaId, string shiftDate, string shift, string group)
    {
        var response = await admin.GetAsync(
            $"/api/admin/overview/areas/{areaId}?shiftDate={shiftDate}&shift={shift}&group={group}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        OverviewEndpointTests.AssertSafeJsonProperties(raw);
        return JsonDocument.Parse(raw).RootElement.Clone();
    }

    private static async Task<int> AddPointAsync(
        JanitorialApiFactory factory, int areaId, string signCode, string qrToken, int startHour, int endHour)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var created = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var point = new Domain.Entities.ServicePoint { AreaId = areaId, Name = $"จุด {signCode}", SortOrder = 1, CreatedAt = created };
            db.Signs.Add(new Sign
            {
                AreaId = areaId,
                ServicePoint = point,
                Code = signCode,
                QrToken = qrToken,
                QrIssuedAt = created,
                Latitude = 13.756300m,
                Longitude = 100.501800m,
                LocationAccuracyM = 10,
                LocationSource = LocationSource.Site,
                LocatedAt = created,
            });
            db.PointRoundWindows.Add(new PointRoundWindow
            {
                ServicePoint = point,
                Shift = Shift.Day,
                StartTime = new TimeOnly(startHour, 0),
                EndTime = new TimeOnly(endHour, 0),
                CreatedAt = created,
            });
            await db.SaveChangesAsync();
            id = point.Id;
        });
        return id;
    }
}
