# Overview Entry Availability Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decouple Overview `Entry` (`พื้นที่ที่ลงเวลาเข้าแล้ว`) availability from pre-shift opening policy capture (`FirstPossibleOpen`) while keeping `History` availability independent and preserving all existing retention safeguards and business rules (`facility-0237`).

**Architecture:** Update `OverviewAvailability.Calculate` to separate closing-policy knowledge (`closingKnown`) from pre-shift opening-policy knowledge (`openingKnown`). `Entry` checks `closingKnown` and the current target snapshot at `EvaluationUtc` while `openingKnown` continues to tighten the 1-year `attendancePartlyExpired` cutoff with its conservative `FirstPossibleOpen` fallback; `History` remains independent and continues to require target coverage from `StartUtc`.

**Tech Stack:** .NET 10, C#, xUnit, EF Core (SQLite in API tests).

**Spec:** [2026-11-21-overview-entry-availability-fix-design.md](../specs/2026-11-21-overview-entry-availability-fix-design.md) (Decision Record: [facility-0237](../../adr/facility-0237-overview-entry-decoupled-from-preshift-opening-policy.md))

## Global Constraints

- Modify only `OverviewAvailability.Calculate` for production logic; do not alter `AdminOverviewEndpoints.cs`, `OverviewProjection.cs`, `OverviewFactLoader.cs`, `OverviewTime.cs`, `OverviewTimeline.cs`, or any frontend file.
- Preserve all Entry partition rules (`entered`/`covered`/`unassigned`), `ScheduleEntryEvidence`, 1-year `attendancePartlyExpired`/`attendanceExpired` retention safeguards (with `firstPossibleOpen` fallback), `workArchived`, `CalculateSupportedBounds` requiring all 5 sections complete, and pre-baseline `receivingPolicyUnknown` (`value: null`) behavior.
- Update `docs/handoff.md` after implementation (owner's rule).
- Do not use emojis in code, comments, commit messages, or docs.

---

## File Structure

- Modify: `backend/src/JanitorialManagementDashboard.Application/Overview/OverviewAvailability.cs` (lines 48-87)
- Test (Unit): `backend/tests/JanitorialManagementDashboard.UnitTests/OverviewAvailabilityTests.cs`
- Test (API): `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/OverviewEndpointTests.cs`
- Docs: `docs/handoff.md`

---

### Task 1: Decouple Entry and History availability in `OverviewAvailability.Calculate`

**Files:**
- Modify: `backend/src/JanitorialManagementDashboard.Application/Overview/OverviewAvailability.cs:48-87`
- Test: `backend/tests/JanitorialManagementDashboard.UnitTests/OverviewAvailabilityTests.cs`

**Interfaces:**
- Consumes: `OverviewMoment` (`Phase`, `EvaluationUtc`, `CloseUtc`, `PolicyComplete`), `TargetTimeline` (`CurrentComplete`, `HistoryComplete`, `ReceivingHistory`, `StructuralGaps`), `DetailCoverage` (`AttendanceAfterUtc`, `WorkFromShiftDate`, `TargetCaptureFromUtc`, `IssueStateKnown`, `Gaps`, `UnreadableTargetIntervals`, `ReadAtUtc`).
- Produces: `OverviewAvailability.Calculate(ShiftSlot slot, OverviewMoment moment, TargetTimeline timeline, DetailCoverage coverage) : SectionAvailability` (existing signature unchanged).

- [ ] **Step 1: Write and update the unit tests in `OverviewAvailabilityTests.cs`**

In `backend/tests/JanitorialManagementDashboard.UnitTests/OverviewAvailabilityTests.cs`, update `Mid_shift_baseline_cannot_certify_prior_rounds` (lines 16-28) and add the three new regression tests right after it:

```csharp
    [Fact]
    public void Mid_shift_baseline_cannot_certify_prior_rounds()
    {
        var slot = new ShiftSlot(new(2026, 11, 16), Shift.Day);
        var start = ShiftCalendar.StartUtc(slot);
        var moment = new OverviewMoment("running", start.AddHours(4), start.AddHours(13), true);
        var target = new TargetTimeline(new(1, new(60, 60), [], [], [], []), 1, [], true, false);
        var coverage = new DetailCoverage(start.AddYears(-1), new(2025, 12, 1),
            start.AddHours(3), true, []);
        var result = OverviewAvailability.Calculate(slot, moment, target, coverage);
        Assert.Equal("complete", result.Entry.State);
        Assert.Empty(result.Entry.Reasons);
        Assert.Equal("complete", result.Work.State);
        Assert.Equal("partial", result.History.State);
        Assert.Contains("targetCoverageMissing", result.History.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.History.Reasons);
    }

    [Fact]
    public void Pre_shift_capture_after_first_possible_open_completes_entry_and_history()
    {
        var preShift = Start.AddMinutes(-30);
        var revisions = new[] { Revision(1, preShift) };
        var coverage = Retained with { TargetCaptureFromUtc = preShift };

        var result = Calculate(revisions, coverage);

        Assert.All(Sections(result), section =>
        {
            Assert.Equal("complete", section.State);
            Assert.Empty(section.Reasons);
        });
    }

    [Fact]
    public void Unknown_closing_policy_after_shift_end_marks_entry_and_history_partial()
    {
        var end = ShiftCalendar.EndUtc(Slot);
        var postShiftStart = end.AddMinutes(10);
        var readAt = end.AddMinutes(20);
        var revisions = new[] { Revision(1, postShiftStart) };
        var coverage = Retained with { TargetCaptureFromUtc = postShiftStart, ReadAtUtc = readAt };

        var result = Calculate(revisions, coverage, readAt);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("receivingPolicyUnknown", result.Entry.Reasons);
        Assert.Equal("partial", result.History.State);
        Assert.Contains("receivingPolicyUnknown", result.History.Reasons);
    }

    [Fact]
    public void Unknown_preshift_opening_policy_falls_back_to_first_possible_open_for_retention_expiry()
    {
        var preShift = Start.AddMinutes(-30);
        var firstPossibleOpen = Start.AddMinutes(-360);
        var revisions = new[] { Revision(1, preShift) };
        var coverage = Retained with
        {
            TargetCaptureFromUtc = preShift,
            AttendanceAfterUtc = firstPossibleOpen,
        };

        var result = Calculate(revisions, coverage);

        Assert.Equal("partial", result.Entry.State);
        Assert.Contains("attendancePartlyExpired", result.Entry.Reasons);
        Assert.DoesNotContain("receivingPolicyUnknown", result.Entry.Reasons);
    }
```

- [ ] **Step 2: Run the unit tests to verify they fail before the fix**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests --filter "FullyQualifiedName~OverviewAvailabilityTests"
```
Expected: FAIL on `Mid_shift_baseline_cannot_certify_prior_rounds`, `Pre_shift_capture_after_first_possible_open_completes_entry_and_history`, and `Unknown_preshift_opening_policy_falls_back_to_first_possible_open_for_retention_expiry` because `Entry.State` is currently `"partial"` with `"receivingPolicyUnknown"`.

- [ ] **Step 3: Update `OverviewAvailability.Calculate` in `OverviewAvailability.cs`**

In `backend/src/JanitorialManagementDashboard.Application/Overview/OverviewAvailability.cs`, replace lines 48-87 with:

```csharp
        var start = ShiftCalendar.StartUtc(slot);
        var evaluation = moment.EvaluationUtc;
        var firstPossibleOpen = FirstPossibleOpen(slot);
        var openingUnreadable = Intersects(coverage.UnreadableTargetIntervals, firstPossibleOpen, start);
        var historyUnreadable = Intersects(coverage.UnreadableTargetIntervals, start, evaluation);
        var earliestAttendance = EarliestAttendance(start, evaluation, timeline.ReceivingHistory);
        var openingKnown = earliestAttendance.HasValue && timeline.ReceivingHistory[0].EffectiveAt <= firstPossibleOpen
            && coverage.TargetCaptureFromUtc.GetValueOrDefault() <= firstPossibleOpen
            && !openingUnreadable;
        var gaps = coverage.Gaps.Concat(timeline.StructuralGaps);
        var currentGap = Intersects(gaps, evaluation, evaluation);
        var currentUnreadable = Intersects(coverage.UnreadableTargetIntervals,
            moment.Phase == "running" ? evaluation : ShiftCalendar.EndUtc(slot), evaluation);
        var closingKnown = moment.PolicyComplete && !(moment.Phase != "running" && currentUnreadable);
        var current = new List<string>();
        if (!timeline.CurrentComplete || coverage.TargetCaptureFromUtc > evaluation)
            current.Add("targetCoverageMissing");
        if (currentGap) current.Add("targetCoverageGap");
        if (currentUnreadable) current.Add("targetRevisionUnreadable");

        var entryReasons = new List<string>(current);
        if (!closingKnown) entryReasons.Add("receivingPolicyUnknown");
        if ((openingKnown ? earliestAttendance : firstPossibleOpen) <= coverage.AttendanceAfterUtc)
            entryReasons.Add("attendancePartlyExpired");
        // An unreadable policy could have kept receiving open after the provisional evaluation time.
        var readAt = coverage.ReadAtUtc ?? (moment.Phase == "closed" ? DateTime.MaxValue : evaluation);
        var lastPossibleAttendance = moment is { PolicyComplete: true, CloseUtc: { } close } && !currentUnreadable
            ? Earlier(evaluation, close) : Earlier(readAt, LastPossibleClose(slot));
        var entry = lastPossibleAttendance <= coverage.AttendanceAfterUtc
            ? new Availability("unavailable", ["attendanceExpired"])
            : FromReasons(entryReasons);

        var workRetained = slot.ShiftDate >= coverage.WorkFromShiftDate;
        var work = workRetained ? FromReasons(current) : new Availability("unavailable", ["workArchived"]);

        var historyReasons = new List<string>();
        if (!timeline.HistoryComplete || coverage.TargetCaptureFromUtc > start) historyReasons.Add("targetCoverageMissing");
        if (!closingKnown) historyReasons.Add("receivingPolicyUnknown");
        if (Intersects(gaps, start, evaluation)) historyReasons.Add("targetCoverageGap");
        if (historyUnreadable) historyReasons.Add("targetRevisionUnreadable");
        var history = workRetained ? FromReasons(historyReasons) : work;
```

- [ ] **Step 4: Run Overview unit tests to verify they pass**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests --filter "FullyQualifiedName~Overview"
```
Expected: PASS across all Overview unit test classes (`OverviewAvailabilityTests`, `OverviewProjectionTests`, `OverviewRulesTests`, `OverviewTimeTests`, `OverviewTimelineTests`).

- [ ] **Step 5: Commit Task 1**

```bash
git add backend/src/JanitorialManagementDashboard.Application/Overview/OverviewAvailability.cs backend/tests/JanitorialManagementDashboard.UnitTests/OverviewAvailabilityTests.cs
git commit -m "fix(overview): decouple entry availability from pre-shift opening policy (facility-0237)"
```

---

### Task 2: Add API regression test for mid-shift deployment and verify full backend suite

**Files:**
- Modify: `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/OverviewEndpointTests.cs`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `GET /api/admin/overview`, `GET /api/admin/overview/areas`, `POST /api/admin/areas`, `OverviewTestData.StartHistoryAsync`, `TestData.AddCleanerAsync`, `AuthApi.LoggedInAsEmployeeAsync`, `AdminSetupApi.BuildingIdAsync`, `AttendanceApi.CheckInAsync`, `ThaiClock.At`.
- Produces: End-to-end API regression test `Mid_shift_first_deployment_reports_complete_entry_while_keeping_history_partial`.

- [ ] **Step 1: Add the API regression test in `OverviewEndpointTests.cs`**

In `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/OverviewEndpointTests.cs`, add `Mid_shift_first_deployment_reports_complete_entry_while_keeping_history_partial` before `AddPointAsync`:

```csharp
    [Fact]
    public async Task Mid_shift_first_deployment_reports_complete_entry_while_keeping_history_partial()
    {
        using var factory = new JanitorialApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        // First deployment starts mid-shift at 09:00 Thai on 2026-10-08 (Day shift 07:00-19:00).
        await OverviewTestData.StartHistoryAsync(factory, ThaiClock.At(8, 9, 0));
        var cleanerId = await TestData.AddCleanerAsync(factory, "1000099", Shift.Day, phone: "0810000099");
        var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");

        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 10));
        var createArea = await admin.PostAsJsonAsync("/api/admin/areas", new
        {
            code = "AR03",
            name = "Canteen",
            buildingId = buildingA,
            shiftPattern = "DayOnly",
            dayCleanerId = cleanerId,
        });
        Assert.Equal(HttpStatusCode.Created, createArea.StatusCode);
        var createdJson = await createArea.Content.ReadFromJsonAsync<JsonElement>();
        var qrToken = createdJson.GetProperty("checkInSign").GetProperty("qrToken").GetString()!;

        var (cleaner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, "1000099", "0810000099");
        await AttendanceApi.CheckInAsync(factory, cleaner, ThaiClock.At(8, 9, 40), qrToken);

        factory.Clock.SetUtcNow(ThaiClock.At(8, 9, 41));
        var summary = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-08&shift=Day");
        var avail = summary.GetProperty("availability");

        Assert.Equal("complete", avail.GetProperty("entry").GetProperty("state").GetString());
        Assert.Empty(avail.GetProperty("entry").GetProperty("reasons").EnumerateArray());
        Assert.Equal("partial", avail.GetProperty("history").GetProperty("state").GetString());
        var historyReasons = avail.GetProperty("history").GetProperty("reasons").EnumerateArray()
            .Select(r => r.GetString()).ToArray();
        Assert.Contains("targetCoverageMissing", historyReasons);
        Assert.DoesNotContain("receivingPolicyUnknown", historyReasons);

        Assert.Equal(1, summary.GetProperty("entry").GetProperty("numerator").GetProperty("value").GetInt32());
        Assert.Equal(3, summary.GetProperty("entry").GetProperty("denominator").GetProperty("value").GetInt32());
        Assert.Equal(1, summary.GetProperty("groups").GetProperty("entered").GetProperty("value").GetInt32());
        Assert.Equal(2, summary.GetProperty("groups").GetProperty("notEntered").GetProperty("value").GetInt32());

        var list = await admin.GetFromJsonAsync<JsonElement>(
            "/api/admin/overview/areas?shiftDate=2026-10-08&shift=Day&group=entered");
        Assert.Equal(1, list.GetProperty("total").GetProperty("value").GetInt32());
        var row = Assert.Single(list.GetProperty("rows").EnumerateArray());
        Assert.Equal("AR03", row.GetProperty("code").GetString());

        // A shift that ended before first deployment still reports partial entry with unknown closing policy.
        var previousShift = await admin.GetFromJsonAsync<JsonElement>("/api/admin/overview?shiftDate=2026-10-07&shift=Day");
        var prevEntryAvail = previousShift.GetProperty("availability").GetProperty("entry");
        Assert.Equal("partial", prevEntryAvail.GetProperty("state").GetString());
        Assert.Contains("receivingPolicyUnknown",
            prevEntryAvail.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(JsonValueKind.Null,
            previousShift.GetProperty("entry").GetProperty("numerator").GetProperty("value").ValueKind);
    }
```

- [ ] **Step 2: Run the Overview API test suite**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~Overview"
```
Expected: PASS (all Overview API tests, including `Mid_shift_first_deployment_reports_complete_entry_while_keeping_history_partial`).

- [ ] **Step 3: Run the full .NET UnitTests suite**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests
```
Expected: PASS (all unit tests, 0 failed, 0 skipped).

- [ ] **Step 4: Update `docs/handoff.md`**

Prepend an implementation verification entry under `## Overview Entry availability fix` in `docs/handoff.md` recording the exact commands and passing test counts observed in Steps 2 and 3.

- [ ] **Step 5: Commit Task 2**

```bash
git add backend/tests/JanitorialManagementDashboard.ApiTests/Admin/OverviewEndpointTests.cs docs/handoff.md
git commit -m "test(overview): add mid-shift deployment entry availability regression test"
```
