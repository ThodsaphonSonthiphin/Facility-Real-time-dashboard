# Supervisor Full Scheduling and Production Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix production scheduling (`f9c3644`) where Cleaners assigned after creation show `"ไม่มีข้อมูลของสัปดาห์นี้"`, enable Admin to create Regular Cleaners with a home building (`HomeBuildingId`) and optional initial Shift/Area, empower Supervisors to schedule both Day and Night shifts within their assigned building(s) without relaxing QR inspection shift boundaries, and support setting Regular Cleaner baselines effective from today with Shift, Area, and optional BreakGroup.

**Architecture:** Make `CleanerProfilePeriod.Shift` nullable (`Shift?`) via a schema migration (`MakeCleanerProfileShiftNullable`), run `ScheduleBackfill.EnsureAsync` idempotently at API startup, and share one synchronization helper (`ScheduleSync`) across `AdminAccountEndpoints` and `AdminAreaEndpoints` (`AreaCleaners`). Separate scheduling authorization (`ScheduleAuthority.CanManageSchedule` / `CanCorrectScheduleHistory`, building-scoped across both shifts) from inspection authorization (`ScheduleAuthority.CanManage` / `SupervisorScope.WithoutCover`, building + own shift), extend `SetRegularBaselineHandler` to accept `EffectiveFrom >= businessDate`, `Shift`, and optional `BreakGroup` while enforcing `(AreaId, Shift)` uniqueness and `DayOnly` rules, and update the React Accounts, Schedule Management, and Regular Baseline Editor screens.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core 10 (MySQL & SQLite), xUnit, React 19, TypeScript (strict), Redux Toolkit Query, Tailwind CSS 4, Vitest, Testing Library, Playwright.

**Spec:** [2026-11-21-supervisor-full-scheduling-and-production-fix-design.md](../specs/2026-11-21-supervisor-full-scheduling-and-production-fix-design.md) (Decision Records: [facility-0238](../../adr/facility-0238-schedule-backfill-and-area-sync.md) through [facility-0241](../../adr/facility-0241-regular-baseline-allows-today-start-shift-and-optional-break-group.md))

## Global Constraints

- Do not use emojis anywhere in code, UI copy, comments, commits, or documentation; use `lucide-react` exclusively for icons (one named import per icon, `aria-hidden="true"`).
- Preserve `users.AreaId != null => users.Shift != null` invariant so `users.CleanerSlot` computed unique index (`area_id * 2 + (CASE WHEN shift = 'NIGHT' THEN 1 ELSE 0 END)`) never collides `NULL` shift with `DAY`.
- Keep `/my-building` (`MyBuildingEndpoints`) and QR inspection (`/api/inspections/*` using `SupervisorScope.WithoutCover` and `ScheduleAuthority.CanManage`) strictly scoped to the Supervisor's own shift (`facility-0048`, `facility-0141`).
- Keep weekly day-off selection isolated per week in `WeeklyOffEditor` (`set-weekly-days-off`, `facility-0190`, `facility-0191`).
- Keep frontend main JS bundle `<= 200 kB` gzip, zero TypeScript strict errors, zero ESLint errors, and zero horizontal overflow at `320px`, `375px`, and `1280px`.
- Follow `react-structure` (hooks and UI in separate files) and `docs/agents/frontend-standards.md`.
- Update `docs/handoff.md` after finishing implementation (owner's rule).

---

## File Structure

- **Domain & Persistence (Backend):**
  - Modify: `backend/src/JanitorialManagementDashboard.Domain/Entities/Scheduling/CleanerProfilePeriod.cs` (`Shift` -> `Shift?`)
  - Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/SchedulingModel.cs` (`CleanerProfilePeriod.Shift` optional mapping)
  - Create: `backend/src/JanitorialManagementDashboard.Infrastructure/Migrations/<timestamp>_MakeCleanerProfileShiftNullable.cs` (+ Designer & Snapshot via `dotnet ef`)
  - Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleBackfill.cs` (handle Regular Cleaners with `AreaId` or `BuildingId` and nullable `Shift`)
  - Create: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleSync.cs` (shared transaction-safe sync of `CleanerProfilePeriod`, `ScheduleBaseline`, and `users.AreaId`/`users.Shift`)
  - Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/AreaCleaners.cs` (call `ScheduleSync` on Area assignment/removal)
  - Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleStore.cs` (internal `CleanerMembershipSlot` with `Shift?`, building-scoped both-shift Supervisor reads, unassigned-shift Cleaner rows, both-shift `destinations`, fallback in `ResponsibleSupervisorAsync`)
- **Application Layer (Backend):**
  - Modify: `backend/src/JanitorialManagementDashboard.Application/Scheduling/ScheduleAuthority.cs` (add `CanManageSchedule` and `CanCorrectScheduleHistory` keeping `CanManage`/`CanCorrectHistory` unchanged)
- **API Layer (Backend):**
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Program.cs` (invoke `ScheduleBackfill.EnsureAsync` on startup when not `Testing`)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/DTOs/AccountDtos.cs` (clarify Regular `HomeBuildingId` and optional `Shift`)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AccountChecks.cs` (Regular Cleaner `HomeBuildingId`, optional `Shift`/`AreaId`, `CleanerSlot` guard, `ScheduleSync` integration)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AdminAccountEndpoints.cs` (sync `CleanerProfilePeriod` + `ScheduleBaseline` on create/update/activate/deactivate)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AccountView.cs` (expose `HomeBuildingId`/`HomeBuildingCode` and `CleanerKind.Regular` from profile/area)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AdminAreaEndpoints.cs` (pass actor/clock to `AreaCleaners.ApplyAsync` and notify `ScheduleNotifications` after commit)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandHandlers.cs` (switch scheduling handlers to `CanManageSchedule`/`CanCorrectScheduleHistory`, extend `SchedulePayload.BaselineRequest` and `ScheduleBaselineWrite` for `EffectiveFrom >= businessDate`, `Shift`, optional `BreakGroup`, `(AreaId, Shift)` overlap check, and `users` sync)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandService.cs` (`ActorAuthority.HoldsScope` uses `CanManageSchedule`)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleNotifications.cs` (pass `shift: null` to `SupervisorsOfAsync` so both-shift building Supervisors refresh)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ReliefCommandHandlers.cs` (use `CanManageSchedule`/`CanCorrectScheduleHistory` and building-level responsible supervisor fallback)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/TransferCommandHandlers.cs` (use `CanManageSchedule`)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/ScheduleEndpoints.cs` (use `CanManageSchedule`/`CanCorrectScheduleHistory`)
  - Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/TeamAttendanceEndpoints.cs` (use `CanManageSchedule`/`CanCorrectScheduleHistory`)
- **Frontend:**
  - Modify: `frontend/src/pages/accounts/accountsApi.ts`, `accountRules.ts`, `useCreateAccountForm.ts`, `CreateAccountForm.tsx`, `useEditAccountForm.ts`, `EditAccountForm.tsx`, `ShiftChoice.tsx`
  - Modify: `frontend/src/shared/api/scheduleTypes.ts`, `frontend/src/pages/schedules/scheduleRules.ts`
  - Modify: `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx`, `useSchedulePage.ts`, `ScheduleContextBar.tsx`
  - Modify: `frontend/src/pages/schedules/RegularBaselineEditor/type.ts`, `useRegularBaselineEditor.ts`, `RegularBaselineEditor.tsx`
- **Tests & Docs:**
  - Modify/Add Tests:
    - `backend/tests/JanitorialManagementDashboard.UnitTests/Scheduling/ScheduleAuthorityTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/MySqlScheduleMigrationTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AccountEndpointTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AccountEditEndpointTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AreaAdminEndpointTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleReadTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleScopeEndpointTests.cs`
    - `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleWriteTests.cs`
    - `frontend/src/pages/accounts/accountRules.test.ts`, `AccountsPage.test.tsx`, `AccountActions.test.tsx`
    - `frontend/src/pages/schedules/scheduleRules.test.ts`, `SchedulePage.test.tsx`, `RegularBaselineEditor.test.tsx`
    - `frontend/e2e/desktop/scheduling.spec.ts`
  - Docs: `docs/handoff.md`

---

### Task 1: Nullable `CleanerProfilePeriod.Shift` and EF Core Migration

**Files:**
- Modify: `backend/src/JanitorialManagementDashboard.Domain/Entities/Scheduling/CleanerProfilePeriod.cs:25-27`
- Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/SchedulingModel.cs:29-40`
- Create: `backend/src/JanitorialManagementDashboard.Infrastructure/Migrations/<timestamp>_MakeCleanerProfileShiftNullable.cs` (generated by `dotnet ef`)
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/MySqlScheduleMigrationTests.cs`

**Interfaces:**
- Produces: `CleanerProfilePeriod.Shift` as `Shift?` (`nullable`) with matching EF Core schema and migration `MakeCleanerProfileShiftNullable`.

- [ ] **Step 1: Write the failing persistence test in `SchedulePersistenceTests.cs`**

Read the end of `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs` first to see the closing brace and add this `[Fact]` inside `SchedulePersistenceTests`:

```csharp
    [Fact]
    public async Task Regular_cleaner_profile_period_allows_null_shift_when_unassigned()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == "A");
            var cleaner = new User
            {
                Role = UserRole.Cleaner,
                EmployeeId = "E9901",
                DisplayName = "แม่บ้านรอจัดกะ",
                SecretHash = "hash",
                Shift = null,
                AreaId = null,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(cleaner);
            await db.SaveChangesAsync();

            db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
            {
                UserId = cleaner.Id,
                Kind = CleanerKind.Regular,
                HomeBuildingId = building.Id,
                Shift = null,
                EffectiveFrom = new DateOnly(2026, 11, 21),
                Revision = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var stored = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == cleaner.Id);
            Assert.Null(stored.Shift);
            Assert.Equal(building.Id, stored.HomeBuildingId);
        });
    }
```

- [ ] **Step 2: Run the test to verify it fails to compile with non-nullable `Shift`**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~Regular_cleaner_profile_period_allows_null_shift_when_unassigned"
```
Expected: FAIL compilation error (`Cannot convert null to 'Shift' because it is a non-nullable value type`).

- [ ] **Step 3: Make `CleanerProfilePeriod.Shift` nullable and generate EF Core migration**

Update `backend/src/JanitorialManagementDashboard.Domain/Entities/Scheduling/CleanerProfilePeriod.cs`:
```csharp
    /// <summary>The Cleaner's shift, or null when a Regular Cleaner awaits Supervisor shift assignment.</summary>
    public Shift? Shift { get; set; }
```

In `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/SchedulingModel.cs`, verify `CleanerProfilePeriod` maps nullable `Shift?` cleanly using `HasConversion<string>()` or `UpperSnakeEnumConverter<Shift>()`:
Check `UpperSnakeEnumConverter<T>` (in `Infrastructure/Persistence/`): if `UpperSnakeEnumConverter<Shift>` handles `Shift` (non-nullable), EF Core automatically maps `Shift?` as nullable when `.IsRequired(false)` is inferred from `Shift?`, and verify with `HasPendingModelChanges()` in tests! Explicitly set `.IsRequired(false)` on `x.Shift`:
```csharp
        model.Entity<CleanerProfilePeriod>(e =>
        {
            e.ToTable("cleaner_profile_periods", t => t.HasCheckConstraint(
                "ck_cleaner_profile_periods_effective_range", EffectiveRangeShape));
            e.Property(x => x.Kind).HasConversion(new UpperSnakeEnumConverter<CleanerKind>()).HasMaxLength(10);
            e.Property(x => x.Shift).HasConversion(new UpperSnakeEnumConverter<Shift>()).HasMaxLength(10).IsRequired(false);
            e.HasIndex(x => new { x.UserId, x.Kind, x.EffectiveFrom }).IsUnique();
            e.HasIndex(x => x.HomeBuildingId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Building>().WithMany().HasForeignKey(x => x.HomeBuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
```

Generate the EF Core migration:
```bash
dotnet ef migrations add MakeCleanerProfileShiftNullable --project backend/src/JanitorialManagementDashboard.Infrastructure --startup-project backend/src/JanitorialManagementDashboard.Api
```

Inspect the generated migration file (`*_MakeCleanerProfileShiftNullable.cs`) to verify it alters `cleaner_profile_periods.shift` to `nullable: true` with `maxLength: 10` (`varchar(10)` on MySQL) and drops nothing else.

Also update `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/MySqlScheduleMigrationTests.cs` (`Fresh_database_migrates_with_all_scheduling_tables_and_no_pending_changes`) to insert and reload a `CleanerProfilePeriod` with `Shift = null` on real MySQL if `FACILITY_MYSQL_TEST` is configured.

- [ ] **Step 4: Run tests to verify `Shift?` persistence and no pending model changes**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~SchedulePersistenceTests|FullyQualifiedName~MySqlScheduleMigrationTests"
```
Expected: PASS on all `SchedulePersistenceTests` (including `Regular_cleaner_profile_period_allows_null_shift_when_unassigned`) and `MySqlScheduleMigrationTests` (if MySQL test server is available, otherwise skipped via `[MySqlFact]`).

- [ ] **Step 5: Commit Task 1**

```bash
git add backend/src/JanitorialManagementDashboard.Domain/Entities/Scheduling/CleanerProfilePeriod.cs \
  backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/SchedulingModel.cs \
  backend/src/JanitorialManagementDashboard.Infrastructure/Migrations/ \
  backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs \
  backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/MySqlScheduleMigrationTests.cs
git commit -m "feat(scheduling): allow nullable shift on CleanerProfilePeriod"
```

---

### Task 2: Startup Backfill and Account/Area Scheduling Sync

**Files:**
- Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleBackfill.cs:1-93`
- Create: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleSync.cs`
- Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/AreaCleaners.cs:1-35`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Program.cs:236-254`
- Modify: `backend/src/JanitorialManagementDashboard.Api/DTOs/AccountDtos.cs:15-64`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AccountChecks.cs:40-295`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AdminAccountEndpoints.cs:127-298,350-398`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AccountView.cs:20-102`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/AdminAreaEndpoints.cs:71-183`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AccountEndpointTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AccountEditEndpointTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AreaAdminEndpointTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs`

**Interfaces:**
- Consumes: `CleanerProfilePeriod` (`Shift?`), `ScheduleBackfill.EnsureAsync`.
- Produces:
  - `ScheduleSync.SyncCleanerAsync(AppDbContext db, User cleaner, int? homeBuildingId, CleanerKind kind, int? actorId, DateOnly effectiveFrom, DateTime nowUtc, CancellationToken ct = default)` — idempotently updates/creates `CleanerProfilePeriod` and `ScheduleBaseline` inside the caller's active transaction so `AdminAccountEndpoints` and `AdminAreaEndpoints` (`AreaCleaners`) never drift from scheduling projections.
  - `AccountChecks.ReadPlaceAsync` supporting Regular Cleaners with `homeBuildingId` and optional `shift`/`areaId`, enforcing `areaId != null => shift != null`.

- [ ] **Step 1: Write failing tests for Account building-only creation, Area-to-Schedule sync, and Startup Backfill**

Add tests in:
1. `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AccountEndpointTests.cs`:
   - Update `A_cleaner_identity_that_cannot_be_stored_is_refused` line 294: remove `[InlineData("Regular", null, true, false)]` (because Regular + `homeBuildingId` + `areaId: null` is now valid!) and add `[InlineData("Regular", null, false, false)]` (missing both `areaId` and `homeBuildingId` with explicit `"Regular"` is refused 400).
   - Add test `A_regular_cleaner_can_be_created_with_home_building_only_and_null_shift`:
     ```csharp
     [Fact]
     public async Task A_regular_cleaner_can_be_created_with_home_building_only_and_null_shift()
     {
         using var factory = new JanitorialApiFactory();
         var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
         var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
         Scheduling.ScheduleWriteApi.Today(factory);

         var response = await admin.PostAsJsonAsync(AccountApi.Path, new
         {
             role = "Cleaner",
             displayName = "แม่บ้านใหม่ ยังไม่จัดกะ",
             employeeId = "E3099",
             phone = "0893099000",
             cleanerKind = "Regular",
             homeBuildingId = buildingA,
         });

         Assert.Equal(HttpStatusCode.Created, response.StatusCode);
         var created = await AccountApi.ReadAsync(response);
         Assert.Equal((null, (string?)null, "Regular", (int?)buildingA, "A"),
             (created.AreaId, created.Shift, created.CleanerKind, created.HomeBuildingId, created.HomeBuildingCode));

         var profile = await AdminSetupApi.ReadAsync(factory, db =>
             db.CleanerProfilePeriods.SingleAsync(p => p.UserId == created.Id));
         Assert.Equal((CleanerKind.Regular, (int?)buildingA, (Shift?)null, new DateOnly(2026, 11, 17)),
             (profile.Kind, profile.HomeBuildingId, profile.Shift, profile.EffectiveFrom));
     }
     ```
2. `backend/tests/JanitorialManagementDashboard.ApiTests/Admin/AreaAdminEndpointTests.cs`:
   - Add test `Assigning_cleaner_on_area_page_syncs_schedule_profile_and_baseline`:
     ```csharp
     [Fact]
     public async Task Assigning_cleaner_on_area_page_syncs_schedule_profile_and_baseline()
     {
         using var factory = new JanitorialApiFactory();
         var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
         var buildingA = await AdminSetupApi.BuildingIdAsync(factory, "A");
         var cleanerId = await TestData.AddCleanerAsync(factory, "E4001", Shift.Day, phone: "0894001000");

         var create = await admin.PostAsJsonAsync(Areas, new
         {
             code = "AR09",
             name = "โซนใหม่",
             buildingId = buildingA,
             shiftPattern = "DayAndNight",
             dayCleanerId = cleanerId,
         });

         Assert.Equal(HttpStatusCode.Created, create.StatusCode);
         var areaId = (await create.Content.ReadFromJsonAsync<AreaDetailModel>())!.Area.Id;
         await factory.WithDbAsync(async db =>
         {
             var profile = await db.CleanerProfilePeriods.SingleAsync(p => p.UserId == cleanerId);
             Assert.Equal((CleanerKind.Regular, (int?)buildingA, (Shift?)Shift.Day), (profile.Kind, profile.HomeBuildingId, profile.Shift));
             var baseline = await db.ScheduleBaselines.Include(b => b.Days).SingleAsync(b => b.UserId == cleanerId);
             Assert.Equal(7, baseline.Days.Count);
             Assert.All(baseline.Days, d => Assert.Equal(areaId, d.AreaId));
         });
     }
     ```
3. `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs`:
   - Add test `ScheduleBackfill_creates_profiles_and_baselines_idempotently_including_unassigned_shift_building_cleaners` testing `ScheduleBackfill.EnsureAsync` on seeded cleaners + an active Regular Cleaner with `BuildingId` and `Shift = null` + `AreaId = null`.

- [ ] **Step 2: Run the new tests to verify they fail**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~A_regular_cleaner_can_be_created_with_home_building_only_and_null_shift|FullyQualifiedName~Assigning_cleaner_on_area_page_syncs_schedule_profile_and_baseline"
```
Expected: FAIL (`400 BadRequest` on account creation without shift/area, and `InvalidOperationException: Sequence contains no elements` for `CleanerProfilePeriods` on Area create).

- [ ] **Step 3: Implement `ScheduleSync`, `ScheduleBackfill`, `Program.cs` call, and Account/Area endpoint updates**

1. Create `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleSync.cs`:
   - Implement `SyncCleanerAsync(AppDbContext db, User cleaner, int? homeBuildingId, CleanerKind kind, int? actorId, DateOnly effectiveFrom, DateTime nowUtc, CancellationToken ct = default)`:
     - Lock baseline head (`ScheduleStore.LockBaselineHeadAsync` or reuse passed `ScheduleStore`)? Keep `ScheduleSync` accepting `(AppDbContext db, ScheduleStore store, ...)`!
     - Lock baseline head for `cleaner.Id`: `await store.LockBaselineHeadAsync(cleaner.Id, nowUtc, ct, advanceRevision: false)`.
     - **Profile period sync:**
       - Load `CleanerProfilePeriods` for `cleaner.Id` ordered by `EffectiveFrom, Id`.
       - Target `(kind, homeBuildingId, shift: cleaner.Shift)`.
       - If no periods exist and `homeBuildingId is null && cleaner.AreaId is null && kind == CleanerKind.Regular`, return without inventing a profile.
       - Find `current` covering `effectiveFrom` and `pending` with `EffectiveFrom >= effectiveFrom`.
       - If `pending` exists: update `Kind`, `HomeBuildingId`, `Shift`, `Revision += 1`, `UpdatedAt = nowUtc`.
       - Else if `current` is null or `(current.Kind, current.HomeBuildingId, current.Shift) != (kind, homeBuildingId, cleaner.Shift)`:
         - Close `current.EffectiveUntil = effectiveFrom; current.Revision += 1; current.UpdatedAt = nowUtc;` if `current != null`.
         - Insert new `CleanerProfilePeriod` (`EffectiveFrom = effectiveFrom`, `EffectiveUntil = min(> effectiveFrom)`, `Revision = 1`, `CreatedById = actorId`).
     - **Regular Baseline sync (only for `kind == CleanerKind.Regular`):**
       - Load `ScheduleBaselines.Include(b => b.Days)` for `cleaner.Id` and `BaselineKind.Regular` ordered by `EffectiveFrom, Id`.
       - Check latest version covering `effectiveFrom` (`currentBaseline`) and any starting at `effectiveFrom` (`sameDayBaseline`).
       - When `cleaner.IsActive && cleaner.AreaId is int targetAreaId`:
         - If `sameDayBaseline` exists: update any day where `!day.IsDayOff` to `day.AreaId = targetAreaId`, bump `sameDayBaseline.Revision += 1` if changed.
         - Else if `currentBaseline` does not exist OR its non-day-off working days point to a different Area:
           - If `currentBaseline != null && (currentBaseline.EffectiveUntil is null || currentBaseline.EffectiveUntil > effectiveFrom)`: close `currentBaseline.EffectiveUntil = effectiveFrom; currentBaseline.Revision += 1; currentBaseline.UpdatedAt = nowUtc;`.
           - Create `ScheduleBaseline` (`EffectiveFrom = effectiveFrom`, `EffectiveUntil = min(> effectiveFrom)`) copying `BreakGroup` and `IsDayOff` per weekday from `currentBaseline` (or `BreakGroup = null, IsDayOff = false` when no prior baseline exists) and setting `AreaId = d.IsDayOff ? null : targetAreaId`.
           - Bump `ScheduleBaselineAnchor.Revision += 1`.
       - When `!cleaner.IsActive || cleaner.AreaId is null`:
         - If `currentBaseline != null` or `sameDayBaseline != null` had a working `AreaId` (and when called from Area removal / account clear / deactivation): update or split at `effectiveFrom` clearing `AreaId = null, BreakGroup = null` on working days that held the cleared Area (while keeping `IsDayOff` intact), bumping `ScheduleBaselineAnchor.Revision += 1`.

2. Update `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleBackfill.cs`:
   - Load active Cleaners (even if `AreaId == null` or `Shift == null`, as long as `AreaId != null || BuildingId != null`):
     - If `!profiled.Contains(cleaner.Id)` and `(cleaner.Area?.BuildingId ?? cleaner.BuildingId) is int homeId`: create `CleanerProfilePeriod` (`Kind = CleanerKind.Regular`, `HomeBuildingId = homeId`, `Shift = cleaner.Shift`, `EffectiveFrom = historyStartsAt`).
     - If `!baselined.Contains(cleaner.Id)` and `cleaner.AreaId is int areaId`: create `ScheduleBaseline` with 7 `ScheduleBaselineDay` rows (`AreaId = areaId`, `BreakGroup = null`, `IsDayOff = false`).
   - Keep Supervisor backfill unchanged (`BuildingId != null && Shift != null`).

3. Update `backend/src/JanitorialManagementDashboard.Api/Program.cs` lines 249-253 (inside `if (!app.Environment.IsEnvironment("Testing"))`):
   ```csharp
   var historyStart = schedulingSettings.HistoryStartsAt
       ?? ScheduleAuthority.BusinessDate(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
   await ScheduleBackfill.EnsureAsync(db, historyStart, app.Lifetime.ApplicationStopping);
   ```

4. Update `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/AreaCleaners.cs` and `backend/src/JanitorialManagementDashboard.Api/Endpoints/AdminAreaEndpoints.cs`:
   - Keep `AreaCleaners.ApplyAsync(AppDbContext db, int areaId, User? dayCleaner, User? nightCleaner)` backward-compatible or add `ScheduleStore store, int? buildingId, int? actorId, DateOnly businessDate, DateTime nowUtc` parameters (remember NO backward compatibility rule in `AGENTS.md`: update `AreaCleaners.ApplyAsync` and its 2 call sites in `AdminAreaEndpoints.CreateAsync` and `AdminAreaEndpoints.UpdateAsync` in place!).
   - Inside `AreaCleaners.ApplyAsync`: after saving `leaving.AreaId = null` and `cleaner.AreaId = areaId`, call `ScheduleSync.SyncCleanerAsync` for each `leaving` (with `leaving.Area?.BuildingId ?? buildingId` if they had no prior `HomeBuildingId`, or preserving existing `HomeBuildingId`) and each assigned `cleaner` (with `HomeBuildingId = buildingId`).
   - In `AdminAreaEndpoints`: inject `ScheduleStore` and `ScheduleNotifications`; after `overview.CommitAsync(change, ct)`, notify `ScheduleNotifications.ChangedAsync` for affected `(cleanerIds, buildingId)` so open Schedule Management pages refresh immediately!

5. Update `AccountChecks.cs`, `AdminAccountEndpoints.cs`, and `AccountView.cs`:
   - In `AccountChecks.ReadPlaceAsync`:
     - When `role == UserRole.Cleaner` and `cleanerKind != CleanerKind.Relief`:
       - If `areaId is int chosenAreaId`: require `shift is Shift cleanerShift` (do NOT allow `AreaId != null && Shift == null` because of `users.CleanerSlot`!), check active Area, `DayOnly` check, `HolderAsync`, and check `homeBuildingId is null || homeBuildingId == area.BuildingId`; return `AccountPlace(area, null, cleanerShift, area.BuildingId)`.
       - If `areaId is null`: allow `shift` to be `null` OR a valid `Shift` (`Day`/`Night`)! If `cleanerKind == CleanerKind.Regular && homeBuildingId is null`, return `Bad("แม่บ้านประจำต้องเลือกตึกประจำหรือพื้นที่ประจำ")`. If `homeBuildingId is int chosenBuildingId`, verify `building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == chosenBuildingId && b.IsActive)` (`null` -> `Bad("ต้องเลือกตึกประจำที่ใช้งานอยู่")`), and return `AccountPlace(null, building: null, shift, chosenBuildingId)` (or keep `user.BuildingId` clean/null for Cleaners and store `HomeBuildingId` in `CleanerProfilePeriod`, OR set `user.BuildingId = chosenBuildingId` only as a compatibility fallback? Wait: let's check `AccountChecks.ReadPlaceAsync`: `users.SupervisorSlot` ONLY triggers when `role = 'SUPERVISOR'`, and `AdminAccountEndpoints` line 42/250 currently sets `user.BuildingId = place.Building?.Id`. Better: keep `user.BuildingId = null` for Cleaners if `AccountEndpointTests` expects `BuildingId == null` for Cleaners, and ALWAYS store `HomeBuildingId` in `CleanerProfilePeriod` via `ScheduleSync.SyncCleanerAsync` on Create AND Update AND have `ScheduleBackfill` read `HomeBuildingId` or `BuildingId` if present!). Wait — what if `SetActiveAsync(..., true)` runs on a deactivated Cleaner? `CleanerProfilePeriod` already stores `HomeBuildingId`, and `ScheduleBackfill` also checks `CleanerProfilePeriod`! Keeping `user.BuildingId = null` for Cleaners matches `AccountChecks` line 82 (`"แม่บ้านประจำพื้นที่ ไม่ได้ประจำตึก"` is for `request.BuildingId`, NOT `request.HomeBuildingId`!) and keeps `AccountDto` clean (`homeBuildingId` populated, `buildingId` null).
   - In `AdminAccountEndpoints.CreateAsync`, `UpdateAsync`, `SetActiveAsync`:
     - Call `ScheduleSync.SyncCleanerAsync` inside the transaction whenever `role == UserRole.Cleaner` and `(kind == CleanerKind.Relief || place.HomeBuildingId != null || beforeIdentity != null)`.
     - When `UpdateAsync` checks `beforeIdentity != afterIdentity` or `areaId` changed: ensure `ScheduleSync.SyncCleanerAsync` updates `ScheduleBaseline` whenever `AreaId` changed even if `HomeBuildingId` and `Shift` stayed in the same building! (Check lines 241-253 of `AdminAccountEndpoints.cs`: `after != before` is true when `AreaId` changed, and inside `afterSave` or `beforeSave`, call `ScheduleSync.SyncCleanerAsync` whenever `user.Role == UserRole.Cleaner && (identityChanged || before.AreaId != after.AreaId)`!)
     - In `SetActiveAsync`: inject `ScheduleStore` and call `ScheduleSync.SyncCleanerAsync` when deactivating a Cleaner (clearing `AreaId` on baseline from today) or activating (syncing profile if needed).
   - In `AccountView.ToDto`:
     - `var kind = user.Role != UserRole.Cleaner ? (CleanerKind?)null : profile?.Kind ?? (user.AreaId is not null ? CleanerKind.Regular : null);`
     - `var home = profile?.HomeBuildingId ?? user.Area?.BuildingId;` (if `home` is from `user.Area?.BuildingId`, include `user.Area.BuildingId` in the building `codes` dictionary lookup!).

- [ ] **Step 4: Run all Admin Account, Admin Area, and Scheduling Persistence tests**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~Account|FullyQualifiedName~AreaAdmin|FullyQualifiedName~SchedulePersistenceTests"
```
Expected: PASS on all matching tests.

- [ ] **Step 5: Commit Task 2**

```bash
git add backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ \
  backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/AreaCleaners.cs \
  backend/src/JanitorialManagementDashboard.Api/Program.cs \
  backend/src/JanitorialManagementDashboard.Api/DTOs/AccountDtos.cs \
  backend/src/JanitorialManagementDashboard.Api/Endpoints/ \
  backend/tests/JanitorialManagementDashboard.ApiTests/Admin/ \
  backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/SchedulePersistenceTests.cs
git commit -m "feat(scheduling): backfill on startup and sync account/area schedule records"
```

---

### Task 3: Building-Scoped Both-Shift Scheduling Authority

**Files:**
- Modify: `backend/src/JanitorialManagementDashboard.Application/Scheduling/ScheduleAuthority.cs:13-53`
- Modify: `backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleStore.cs:588-1270`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandHandlers.cs:274-292,380-400,665-670,775-785,875-895,958-1035,1285-1295`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandService.cs:459-471`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleNotifications.cs:82-106`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ReliefCommandHandlers.cs:90-100,185-235`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/TransferCommandHandlers.cs:95-105,258-268`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/ScheduleEndpoints.cs:99-118`
- Modify: `backend/src/JanitorialManagementDashboard.Api/Endpoints/TeamAttendanceEndpoints.cs:153-161`
- Test: `backend/tests/JanitorialManagementDashboard.UnitTests/Scheduling/ScheduleAuthorityTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleReadTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleScopeEndpointTests.cs`

**Interfaces:**
- Consumes: `ScopePeriod` (`SupervisorId`, `BuildingId`, `Shift`, `From`, `Until`).
- Produces:
  - `ScheduleAuthority.CanManageSchedule(UserRole role, int actorId, int buildingId, DateOnly date, IReadOnlyCollection<ScopePeriod> periods)`
  - `ScheduleAuthority.CanCorrectScheduleHistory(UserRole role, int actorId, int buildingId, DateOnly editedDate, DateOnly businessDate, IReadOnlyCollection<ScopePeriod> periods)`
  - Preserves `ScheduleAuthority.CanManage` and `ScheduleAuthority.CanCorrectHistory` unchanged for `MyBuildingEndpoints` and `InspectionEndpoints`.

- [ ] **Step 1: Write failing unit & API tests for both-shift scheduling vs own-shift inspection**

1. In `backend/tests/JanitorialManagementDashboard.UnitTests/Scheduling/ScheduleAuthorityTests.cs`, add:
```csharp
    [Fact]
    public void CanManageSchedule_authorizes_supervisor_across_both_shifts_in_assigned_buildings_only()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Day, Day, null)];

        Assert.True(ScheduleAuthority.CanManageSchedule(UserRole.Supervisor, 10, 1, Day, scope));
        Assert.False(ScheduleAuthority.CanManageSchedule(UserRole.Supervisor, 10, 2, Day, scope));
        // Inspection/MyBuilding authority remains single-shift:
        Assert.True(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Day, Day, scope));
        Assert.False(ScheduleAuthority.CanManage(UserRole.Supervisor, 10, 1, Shift.Night, Day, scope));
    }

    [Fact]
    public void CanCorrectScheduleHistory_requires_building_scope_on_both_edited_and_business_dates()
    {
        ScopePeriod[] scope = [new(10, 1, Shift.Night, Day.AddDays(-7), Day), new(10, 2, Shift.Day, Day, null)];

        Assert.False(ScheduleAuthority.CanCorrectScheduleHistory(UserRole.Supervisor, 10, 1, Day.AddDays(-2), Day, scope));
        Assert.True(ScheduleAuthority.CanCorrectScheduleHistory(UserRole.Supervisor, 10, 2, Day, Day, scope));
    }
```
2. In `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleReadTests.cs`:
   - Update `A_supervisor_reads_the_buildings_the_dated_scope_gives_them` (lines 60-78) and `A_supervisor_forging_a_building_or_shift_reads_nothing` (lines 81-92) so building A's Day Supervisor (`2000001`) reads BOTH `dayA`, `dayA2`, and `nightA` (and `shift: Shift.Night` returns `nightA`), while still NEVER seeing `dayB` or `buildingB`!
   - Add test `A_supervisor_reads_unassigned_shift_regular_cleaners_in_their_building`: verify a Regular Cleaner created with `HomeBuildingId = buildingA, Shift = null, AreaId = null` appears on `2000001`'s `/api/schedules` read with `Edit.CanChange == true`, `Day.Shift == null`, and `Availability == "unset"`.
3. In `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleScopeEndpointTests.cs`:
   - Verify existing `An_assigned_building_in_the_other_shift_is_a_blocked_scan_that_stores_no_inspection` stays GREEN (inspection still blocked `OutsideShift`) while adding `A_supervisor_schedules_both_shifts_in_their_building_while_my_building_stays_own_shift`.

- [ ] **Step 2: Run tests to verify failure on existing both-shift schedule expectations**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests --filter "FullyQualifiedName~ScheduleAuthorityTests" && dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~A_supervisor_reads_unassigned_shift_regular_cleaners_in_their_building"
```
Expected: FAIL compilation error (`'ScheduleAuthority' does not contain a definition for 'CanManageSchedule'`).

- [ ] **Step 3: Implement building-scoped both-shift scheduling authority**

1. Add `CanManageSchedule` and `CanCorrectScheduleHistory` to `backend/src/JanitorialManagementDashboard.Application/Scheduling/ScheduleAuthority.cs`:
```csharp
    /// <summary>
    /// ADR facility-0240: scheduling authority covers both Day and Night shifts within the Supervisor's dated building(s).
    /// Inspection and /my-building continue to use <see cref="CanManage"/>.
    /// </summary>
    public static bool CanManageSchedule(
        UserRole role,
        int actorId,
        int buildingId,
        DateOnly date,
        IReadOnlyCollection<ScopePeriod> periods) =>
        role == UserRole.Admin ||
        (role == UserRole.Supervisor && periods.Any(p =>
            p.SupervisorId == actorId && p.BuildingId == buildingId &&
            p.From <= date && (p.Until is null || date < p.Until.Value)));

    public static bool CanCorrectScheduleHistory(
        UserRole role,
        int actorId,
        int buildingId,
        DateOnly editedDate,
        DateOnly businessDate,
        IReadOnlyCollection<ScopePeriod> periods) =>
        CanManageSchedule(role, actorId, buildingId, editedDate, periods) &&
        CanManageSchedule(role, actorId, buildingId, businessDate, periods);
```

2. Update `ScheduleStore.cs`:
   - Replace `ScopePeriod` in `MembershipSlots` and `HomeBuilding` with `private sealed record CleanerMembershipSlot(int BuildingId, Shift? Shift, DateOnly From, DateOnly? Until);`:
     - In `MembershipSlots`: add slot even when `profile.Shift == null` whenever `(profile.HomeBuildingId ?? BaselineBuilding(...)) is int building`. For compatibility pre-profile days (`personProfiles.Count == 0`), fall back to `person.AreaId` -> `areaBuildings` or `person.BuildingId` with `person.Shift` (including `null` if building is known).
   - In `ReadSnapshotAsync`:
     - `authorised` (Supervisor): filter `managementScope` by `(buildingId is null || period.BuildingId == buildingId)` and `dates.Exists(date => ScheduleAuthority.Covers(period, date))` — do NOT drop periods because `period.Shift != shift`.
     - `DatesThisReadCovers`:
       - When `authorised is not null`: include `date` if `slots` has a slot covering `date` (`slot.From <= date && (slot.Until is null || date < slot.Until.Value)`) matching `authorised.Any(p => p.BuildingId == slot.BuildingId && ScheduleAuthority.Covers(p, date))` AND `(shift is null || slot.Shift is null || slot.Shift == shift)`.
       - When `authorised is null` (Admin / Cleaner): match `(buildingId is null || slot.BuildingId == buildingId) && (shift is null || slot.Shift is null || slot.Shift == shift)`.
     - `destinations`: evaluate `CanCorrectScheduleHistory` / `CanManageSchedule` (using `building.Key` and `date`, independent of `slotShift`) so both `Day` and `Night` destinations are emitted (narrowed by `shift` filter only if supplied).
     - `ReadDay`:
       - Compute `building` and `canChange` BEFORE checking `day.Shift is null`:
         ```csharp
         var activeProfile = personProfiles.LastOrDefault(entry =>
             ScheduleResolver.Applies(entry.EffectiveFrom, entry.EffectiveUntil, date));
         var applicable = personBaselines.Where(entry =>
             ScheduleResolver.Applies(entry.EffectiveFrom, entry.EffectiveUntil, date)).ToList();
         var building = activeProfile?.HomeBuildingId;
         if (activeProfile is not null && building is null)
         {
             var targetArea = applicable.SelectMany(entry => entry.Days).OrderBy(cell => cell.Weekday)
                 .FirstOrDefault(cell => cell.AreaId is not null)?.AreaId;
             building = targetArea is int id ? areaBuildings.GetValueOrDefault(id) : null;
         }
         else if (activeProfile is null && personProfiles.Count == 0)
         {
             building = person.AreaId is int id ? areaBuildings.GetValueOrDefault(id) : person.BuildingId;
         }

         var canChange = building is int targetBuilding && (date < businessDate
             ? ScheduleAuthority.CanCorrectScheduleHistory(actor.Role, actor.Id, targetBuilding, date, businessDate, managementScope)
             : ScheduleAuthority.CanManageSchedule(actor.Role, actor.Id, targetBuilding, date, managementScope));
         ```
       - When `actor.Role != UserRole.Cleaner && date >= historyStartsAt && day.Shift is null`: return `day with { Edit = new ScheduleEditFacts(canChange, header?.Choices.Any(c => c.OffDate == date) == true, null, null, false, ScheduleResolver.NoRevision, ScheduleResolver.NoRevision, null, canChange && building is int b ? optionsByBuilding.GetValueOrDefault(b) ?? [] : [], []) }` so unassigned-shift Cleaners can be opened in `RegularBaselineEditor` and `WeeklyOffEditor`!
       - When `day.Shift is Shift slotShift`: compute `canChange` via `CanCorrectScheduleHistory` / `CanManageSchedule` as above, keeping existing `Edit` population.
     - `ReadReliefAsync`: check `authorised.Any(period => period.BuildingId == area.BuildingId && ScheduleAuthority.Covers(period, row.ShiftDate))`.
     - `transferSlots` `CanRead`: check `CanCorrectScheduleHistory` / `CanManageSchedule` on `(building, date)`.
   - In `ResponsibleSupervisorAsync`: check exact `(buildingId, shift, date)` match first; if null, query `SupervisorScopePeriods` for `(buildingId, date)` across any shift (so a single building Supervisor covering both shifts still resolves cleanly).

3. Update scheduling authorization callers:
   - `ScheduleCommandHandlers.cs`:
     - Update `Actor.CanManage` / `Actor.CanChange` to call `ScheduleAuthority.CanManageSchedule` / `ScheduleAuthority.CanCorrectScheduleHistory`, and add `CanManageBuilding(int buildingId, DateOnly date)` / `CanChangeBuilding(int buildingId, DateOnly date)` (same implementation).
   - `ScheduleCommandService.cs`:
     - Update `ActorAuthority.HoldsScope`: `scope.All(key => ScheduleAuthority.CanManageSchedule(Role, ActorId, key.BuildingId, BusinessDate, Periods))`.
   - `ScheduleNotifications.cs`:
     - Update `RecipientsAsync` lines 91-98 to pass `shift: null` to `ScheduleScopeReader.SupervisorsOfAsync`.
   - `ReliefCommandHandlers.cs`, `TransferCommandHandlers.cs`, `ScheduleEndpoints.cs`, `TeamAttendanceEndpoints.cs`:
     - Replace `ScheduleAuthority.CanManage(..., building, parsedShift, shiftDate, periods)` / `CanCorrectHistory(...)` with `CanManageSchedule(..., building, shiftDate, periods)` / `CanCorrectScheduleHistory(...)`.

- [ ] **Step 4: Run Unit and API scheduling tests**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests && dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~ScheduleReadTests|FullyQualifiedName~ScheduleScopeEndpointTests|FullyQualifiedName~ScheduleWriteTests|FullyQualifiedName~Transfer|FullyQualifiedName~Relief|FullyQualifiedName~MissingTeamAttendance"
```
Expected: PASS across all unit tests and scheduling API tests.

- [ ] **Step 5: Commit Task 3**

```bash
git add backend/src/JanitorialManagementDashboard.Application/Scheduling/ScheduleAuthority.cs \
  backend/src/JanitorialManagementDashboard.Infrastructure/Persistence/Scheduling/ScheduleStore.cs \
  backend/src/JanitorialManagementDashboard.Api/Scheduling/ \
  backend/src/JanitorialManagementDashboard.Api/Endpoints/ScheduleEndpoints.cs \
  backend/src/JanitorialManagementDashboard.Api/Endpoints/TeamAttendanceEndpoints.cs \
  backend/tests/JanitorialManagementDashboard.UnitTests/Scheduling/ScheduleAuthorityTests.cs \
  backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleReadTests.cs \
  backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleScopeEndpointTests.cs
git commit -m "feat(scheduling): authorize supervisor scheduling across both shifts in assigned buildings"
```

---

### Task 4: Regular Baseline Command (`EffectiveFrom >= today`, `Shift`, Optional `BreakGroup`, Overlap Check, `users` Sync)

**Files:**
- Modify: `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandHandlers.cs:155-218,381-491,841-1196,1440-1569`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleWriteTests.cs`
- Test: `backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleCommandTests.cs`

**Interfaces:**
- Consumes: `ScheduleAuthority.CanManageSchedule`, `ScheduleSync`, `CleanerProfilePeriod` (`Shift?`).
- Produces:
  - `SchedulePayload.BaselineRequest(int UserId, DateOnly EffectiveFrom, Shift? Shift, IReadOnlyList<BaselineCell> Days)`
  - `SetRegularBaselineHandler` accepting `EffectiveFrom >= actor.BusinessDate`, updating/splitting `CleanerProfilePeriod.Shift`, validating `Area.HasShift(effectiveShift)` and no `(AreaId, effectiveShift)` overlap with another active Regular Cleaner, allowing `BreakGroup == null`, and syncing `users.AreaId`/`users.Shift` when `EffectiveFrom == actor.BusinessDate`.
  - `SetWeeklyDaysOffHandler` allowing weekly day-off selection for Regular Cleaners with `HomeBuildingId` even when `Shift == null`.

- [ ] **Step 1: Write failing tests in `ScheduleWriteTests.cs`**

1. Update `ScheduleWriteTests.A_regular_baseline_start_must_be_in_the_future_and_keeps_the_earlier_version` (lines 284-336):
   - Rename/adjust so **past** dates (`actor.BusinessDate.AddDays(-1)`, e.g. `2026-11-16`) are rejected `409 Conflict` (`RowInvalid`), while **today** (`2026-11-17`) and **future** (`2026-12-02`) succeed (`200 OK`, `Committed`)!
2. Add new `[Fact]` tests in `ScheduleWriteTests.cs`:
   - `Supervisor_assigns_unassigned_cleaner_effective_today_with_shift_area_and_optional_break_group`:
     - Admin creates Regular Cleaner with `HomeBuildingId = buildingA` and `Shift = null, AreaId = null`.
     - Supervisor `2000001` submits `set-regular-baseline` with `effectiveFrom = "2026-11-17"`, `shift = "Night"`, `areaId = ar03`, `breakGroup = null`.
     - Assert `200 OK`, `CleanerProfilePeriod.Shift == Shift.Night`, `ScheduleBaseline` starts `2026-11-17`, and `users.AreaId == ar03`, `users.Shift == Shift.Night`!
   - `Regular_baseline_rejects_day_only_area_on_night_shift_and_rejects_overlapping_area_shift`:
     - Assert `Night` shift on `AR02` (`DayOnly`) returns `409 Rejected` (`RowInvalid`).
     - Assert assigning a second Cleaner to `(AR01, Day)` on `2026-11-17` returns `409 Rejected` (`RowInvalid`) naming the current holder.
   - `Supervisor_sets_weekly_days_off_for_two_cleaners_independently_after_baseline_assignment`:
     - Cleaner 1 on Area 1 (off Sunday), Cleaner 2 on Area 2 (off Saturday) via `set-weekly-days-off` -> verify both saved and isolated.

- [ ] **Step 2: Run the new tests to verify failure**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~Supervisor_assigns_unassigned_cleaner_effective_today_with_shift_area_and_optional_break_group|FullyQualifiedName~Regular_baseline_rejects_day_only_area_on_night_shift_and_rejects_overlapping_area_shift"
```
Expected: FAIL (`409 Rejected`: `"วันเริ่มใช้ต้องเป็นวันถัดจากวันนี้"` or `"ไม่พบกะของคนนี้ในวันที่เลือก"`).

- [ ] **Step 3: Implement `ScheduleBaselineWrite`, `ReadBaselineTargetAsync`, and `SetWeeklyDaysOffHandler` updates**

In `backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandHandlers.cs`:
1. Update `SchedulePayload.BaselineRequest` and `TryReadBaseline`:
   - Add `Shift? Shift` property (`internal sealed record BaselineRequest(int UserId, DateOnly EffectiveFrom, Shift? Shift, IReadOnlyList<BaselineCell> Days);`).
   - In `TryReadBaseline`: if property `"shift"` is present and non-null, parse `Shift` (refuse `return false` if invalid string); if absent or `null`, set `Shift = null`.
2. Add `ReadBaselineTargetAsync(AppDbContext db, int userId, DateOnly date, CancellationToken ct)` on `ScheduleCommandHandler` (returning `(User Person, Shift? Shift, int? BuildingId, CleanerKind? Kind)`):
   - Reads active Cleaner; reads `CleanerProfilePeriods` for `date` even when `profile.Shift == null` (never failing just because `Shift` is not yet chosen!).
3. Update `SetWeeklyDaysOffHandler`:
   - Allow `CheckDateAsync` to resolve `BuildingId` via `ReadBaselineTargetAsync` when `Shift == null` (still requiring `actor.CanChangeBuilding(buildingId, date)`), and in `PlanLocksAsync`, when `target.Shift` is null use `Shift.Day` or a deterministic slot key only if `target.Shift is Shift s` (or lock `BaselineHeads` for `row.UserId` in addition to slots so unassigned-shift weekly day-off writes also serialize cleanly!).
4. Update `ScheduleBaselineWrite.PlanLocksAsync` & `ApplyRowAsync`:
   - In `PlanLocksAsync`: include `ScheduleScopeKey(buildingId, effectiveShift)` (covering `buildingId` of target and each submitted area) and `BaselineHeads` (`row.UserId`).
   - Change effective date guard to `if (payload.EffectiveFrom < actor.BusinessDate)` -> refuse `"วันเริ่มใช้ต้องเป็นตั้งแต่วันนี้ ({Iso(actor.BusinessDate)}) ขึ้นไป"`.
   - Call `ReadBaselineTargetAsync`; resolve `effectiveShift = payload.Shift ?? target.Shift`. For `BaselineKind.Regular`, if `effectiveShift is null`, refuse `"ต้องระบุกะเช้าหรือกะดึก"`.
   - Check building permission via `actor.CanManageSchedule(buildingId, payload.EffectiveFrom)`.
   - For each working cell with `AreaId`:
     - Verify `area.IsActive`, `area.BuildingId == home` (or `actor.CanManageSchedule(area.BuildingId, payload.EffectiveFrom)`), and `area.HasShift(effectiveShift.Value)` (if false, refuse `$"พื้นที่ {area.Code} ทำเฉพาะกะ{ThaiLabels.ShiftName(area.ShiftPattern == ShiftPattern.DayOnly ? Shift.Day : Shift.Day)} เลือกกะดึกไม่ได้"`).
     - **Check `(AreaId, effectiveShift.Value)` overlap:** inspect other active Regular Cleaners (`u.Id != payload.UserId`) whose overlapping baseline versions (`[EffectiveFrom, nextFrom)`) or `users.AreaId == areaId && users.Shift == effectiveShift` (when `EffectiveFrom == actor.BusinessDate` and no baseline excludes `EffectiveFrom`) place them in `(areaId, effectiveShift.Value)` on any shared active day-of-week (`!cell.IsDayOff && !otherCell.IsDayOff`). Refuse with `"พื้นที่ {area.Code} กะ{ThaiLabels.ShiftName(effectiveShift.Value)} มีแม่บ้านประจำอยู่แล้ว ({other.DisplayName})"`.
   - Update `ScheduleBaseline` versions (same logic as before, preserving `BreakGroup == null` when omitted).
   - If `kind == BaselineKind.Regular` and `target.Shift != effectiveShift`: update/split `CleanerProfilePeriod` at `payload.EffectiveFrom` to `Shift = effectiveShift` (updating in place if a period starts on `EffectiveFrom`, otherwise closing previous period `EffectiveUntil = payload.EffectiveFrom` and adding the new `CleanerProfilePeriod`).
   - If `kind == BaselineKind.Regular && payload.EffectiveFrom == actor.BusinessDate`:
     - Load tracked `User` (`await db.Users.SingleAsync(u => u.Id == payload.UserId, ct)`), set `user.Shift = effectiveShift`, determine `todayCell = created.Days.FirstOrDefault(d => d.Weekday == payload.EffectiveFrom.DayOfWeek)`, set `user.AreaId = todayCell is { IsDayOff: false, AreaId: int a } ? a : created.Days.FirstOrDefault(d => !d.IsDayOff && d.AreaId != null)?.AreaId` (always preserving `user.AreaId != null => user.Shift != null`).
   - Also check existing tests that may touch `today`: e.g. `ScheduleHistoryCorrectionTests.cs` lines 33 / 218 to ensure no test assumed `EffectiveFrom == today` is rejected on `set-regular-baseline`!

- [ ] **Step 4: Run all Backend Unit & API Scheduling tests**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests && dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter "FullyQualifiedName~Scheduling"
```
Expected: PASS on all UnitTests and all non-MySQL Scheduling ApiTests (plus MySQL tests if `FACILITY_MYSQL_TEST` is set).

- [ ] **Step 5: Commit Task 4**

```bash
git add backend/src/JanitorialManagementDashboard.Api/Scheduling/ScheduleCommandHandlers.cs \
  backend/tests/JanitorialManagementDashboard.ApiTests/Scheduling/ScheduleWriteTests.cs
git commit -m "feat(scheduling): support same-day regular baseline with shift, optional break group, and overlap validation"
```

---

### Task 5: Frontend Accounts Form — Regular Cleaner Home Building & Optional Shift/Area

**Files:**
- Modify: `frontend/src/pages/accounts/accountsApi.ts:26-40`
- Modify: `frontend/src/pages/accounts/accountRules.ts:40-52,118-246`
- Modify: `frontend/src/pages/accounts/useCreateAccountForm.ts:16-73`
- Modify: `frontend/src/pages/accounts/CreateAccountForm.tsx:111-185`
- Modify: `frontend/src/pages/accounts/useEditAccountForm.ts:16-69`
- Modify: `frontend/src/pages/accounts/EditAccountForm.tsx:46-116`
- Modify: `frontend/src/pages/accounts/ShiftChoice.tsx:1-41`
- Modify: `frontend/src/test/adminFixtures.ts:151-197`
- Test: `frontend/src/pages/accounts/accountRules.test.ts`
- Test: `frontend/src/pages/accounts/AccountsPage.test.tsx`
- Test: `frontend/src/pages/accounts/AccountActions.test.tsx`

**Interfaces:**
- Consumes: `Account`, `CreateAccountRequest`, `UpdateAccountBody` from `accountsApi.ts` matching `AccountDtos.cs`.
- Produces:
  - `ShiftChoice` with optional `allowEmpty?: boolean` and `onChange: (shift: Shift | '') => void` so Regular Cleaners without an Area can leave Shift unassigned (`ยังไม่ระบุกะ (ให้หัวหน้าจัด)`).
  - `CreateAccountForm` and `EditAccountForm` supporting `ตึกประจำ` (`homeBuildingId`), optional `พื้นที่ประจำ` (`areaId`), and optional `กะ` (`shift`) for Regular Cleaners.

- [ ] **Step 1: Write failing tests in `accountRules.test.ts` and `AccountsPage.test.tsx`**

1. In `frontend/src/pages/accounts/accountRules.test.ts`:
   - Add test verifying `createRequest` builds `{ role: 'Cleaner', cleanerKind: 'Regular', homeBuildingId: 1, displayName, employeeId, phone }` when `areaId === ''` and `shift === ''`, and rejects when both `areaId` and `homeBuildingId` are empty.
   - Add test verifying `placeText` displays `'ตึก A · ยังไม่จัดกะและพื้นที่'` when Regular Cleaner has `homeBuildingCode: 'A'`, `areaCode: undefined`, `shift: undefined`.
2. In `frontend/src/pages/accounts/AccountsPage.test.tsx`:
   - Add test `'allows creating a Regular Cleaner with only home building so the Supervisor schedules shift and Area'`: select `ตึกประจำ = '1'`, leave `พื้นที่ประจำ` empty and `กะ` unassigned, submit, and verify `POST /api/admin/accounts` body has `{ role: 'Cleaner', cleanerKind: 'Regular', homeBuildingId: 1 }` without `areaId` or `shift`.

- [ ] **Step 2: Run Vitest on accounts tests to verify failure**

Run:
```bash
NODE_ENV=test npm --prefix frontend test -- src/pages/accounts/
```
Expected: FAIL (`problem: 'เลือกกะก่อน'` on `createRequest` and missing `ตึกประจำ` select on Regular Cleaner form).

- [ ] **Step 3: Implement Accounts UI and validation rules updates**

1. Update `frontend/src/pages/accounts/accountsApi.ts`:
   - In `CreateAccountRequest` `Cleaner` variant: make `shift?: Shift;`.
2. Update `frontend/src/pages/accounts/accountRules.ts`:
   - `placeText`: for Regular Cleaner without `areaCode`, show `${home ? `ตึก ${home} · ` : ''}ยังไม่มีพื้นที่ · ${account.shift ? shiftShortLabel[account.shift] : 'ยังไม่จัดกะ'}` (or `'ตึก A · ยังไม่จัดกะและพื้นที่'` when both missing; keep `'AR01 · กะเช้า'` when Area + Shift exist).
   - `createRequest` & `updateRequest`:
     - If `draft.role === 'Cleaner'` (or `account.role === 'Cleaner'`) and `draft.cleanerKind !== 'Relief'`:
       - Require `draft.areaId` OR `draft.homeBuildingId` (if neither -> `{ problem: 'เลือกตึกประจำหรือพื้นที่ประจำก่อน' }`).
       - If `draft.areaId` is set, require `draft.shift !== ''` (`{ problem: 'เลือกกะก่อน' }`).
       - If `draft.areaId === ''`, allow `draft.shift === ''` (omit `shift` when `''`, include `cleanerKind: 'Regular'`, `homeBuildingId: Number(draft.homeBuildingId)`).
   - `editDraftOf`: populate `homeBuildingId` from `account.homeBuildingId ? String(account.homeBuildingId) : ''` (or area's building if available).
3. Update `frontend/src/pages/accounts/ShiftChoice.tsx`:
   - Add `allowEmpty?: boolean` prop (`onChange: (shift: Shift | '') => void`). When `allowEmpty` is true, render a third radio `"ยังไม่ระบุกะ (ให้หัวหน้าจัด)"` checked when `value === ''`.
4. Update `useCreateAccountForm.ts`, `CreateAccountForm.tsx`, `useEditAccountForm.ts`, `EditAccountForm.tsx`:
   - Show `<Field label="ตึกประจำ">` for both `Regular` and `Relief` Cleaners (for `Regular`, filtering `activeAreas` to that building when selected; when an `Area` is selected via `chooseArea`, auto-set `homeBuildingId` to `String(next.buildingId)`).
   - Keep `ShiftChoice` `allowEmpty={draft.role === 'Cleaner' && draft.cleanerKind !== 'Relief' && draft.areaId === ''}`.
   - Update `frontend/src/test/adminFixtures.ts` and existing test assertions in `AccountActions.test.tsx` / `AccountsPage.test.tsx` if any Regular Cleaner fixture now carries `homeBuildingId: 1, homeBuildingCode: 'A'`.

- [ ] **Step 4: Run frontend accounts tests and typecheck**

Run:
```bash
NODE_ENV=test npm --prefix frontend test -- src/pages/accounts/ && npm --prefix frontend run lint
```
Expected: PASS on all `src/pages/accounts/` tests and 0 lint errors.

- [ ] **Step 5: Commit Task 5**

```bash
git add frontend/src/pages/accounts/ frontend/src/test/adminFixtures.ts
git commit -m "feat(frontend): allow Regular Cleaner accounts with home building and optional shift/area"
```

---

### Task 6: Frontend Schedule Management & Regular Baseline Editor

**Files:**
- Modify: `frontend/src/pages/schedules/scheduleRules.ts:116-128`
- Modify: `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx:116-299,431-455`
- Modify: `frontend/src/pages/schedules/SchedulePage/useSchedulePage.ts:148-152`
- Modify: `frontend/src/pages/schedules/SchedulePage/ScheduleContextBar.tsx:31-35,87-96`
- Modify: `frontend/src/pages/schedules/RegularBaselineEditor/type.ts:4-8`
- Modify: `frontend/src/pages/schedules/RegularBaselineEditor/useRegularBaselineEditor.ts:11-113`
- Modify: `frontend/src/pages/schedules/RegularBaselineEditor/RegularBaselineEditor.tsx:7-44`
- Test: `frontend/src/pages/schedules/scheduleRules.test.ts`
- Test: `frontend/src/pages/schedules/SchedulePage/SchedulePage.test.tsx`
- Test: `frontend/src/pages/schedules/RegularBaselineEditor/RegularBaselineEditor.test.tsx`

**Interfaces:**
- Consumes: `ScheduleWeek`, `SchedulePerson`, `ScheduleDestination`, `Shift` from `shared/api/scheduleTypes.ts` and `types.ts`.
- Produces:
  - `SupervisorShiftFilter` (`ทุกกะ` / `กะเช้า` / `กะดึก`) on `/schedule-management` (both desktop and mobile).
  - `RegularBaselineDraft` `{ effectiveFrom: string; shift: Shift | ''; areaId: string; breakGroup: string }` allowing `today` start, Shift choice, Area filtered by shift & building, and optional BreakGroup.

- [ ] **Step 1: Write failing tests in `SchedulePage.test.tsx` and `RegularBaselineEditor.test.tsx`**

1. In `frontend/src/pages/schedules/RegularBaselineEditor/RegularBaselineEditor.test.tsx`:
   - Update line 15 (`expect(drawer.getByLabelText('วันเริ่มใช้')).toHaveValue('2026-11-18')`) to `'2026-11-17'` (`today`) and verify `min="2026-11-17"`.
   - Add test `'allows Supervisor to assign shift, area, and optional break group starting today for an unassigned cleaner'`:
     - Mock a Regular Cleaner whose 7 days have `shift: undefined`, `area: { sourceKind: 'unset', revision: '0' }`, `edit: { canChange: true, areas: [...] }`, and `destinations` containing `{ date: '2026-11-17', shift: 'Night', buildingId: 1, buildingCode: 'A', areas: [{ id: 4, label: 'AR04 · พื้นที่กะดึก' }] }`.
     - Click `'ตั้งตารางประจำ'`, verify `วันเริ่มใช้` is `'2026-11-17'`, select `กะ = 'Night'`, select `พื้นที่ประจำ = '4'`, leave `กลุ่มพักประจำ` as `''` (`ยังไม่กำหนด`), click `'ตรวจสอบ'`, click `'บันทึก'`, and assert `POST /api/schedules/commands` payload sends `{ userId, effectiveFrom: '2026-11-17', shift: 'Night', days: expect.arrayContaining([{ weekday: 'Tuesday', areaId: 4, breakGroup: null }]) }`.
2. In `frontend/src/pages/schedules/SchedulePage/SchedulePage.test.tsx`:
   - Add test `'shows shift filter (ทุกกะ/กะเช้า/กะดึก) for Supervisor and sends chosen shift to GET /api/schedules'`.
   - Add test `'displays unassigned-shift Regular Cleaner badge and allows opening regular baseline setup'`.

- [ ] **Step 2: Run Vitest on schedules tests to verify failure**

Run:
```bash
NODE_ENV=test npm --prefix frontend test -- src/pages/schedules/
```
Expected: FAIL (`'2026-11-18'` vs `'2026-11-17'`, missing `'ตั้งตารางประจำ'` button, missing `'กะ'` field inside `RegularBaselineEditor`, missing Supervisor shift filter).

- [ ] **Step 3: Implement SchedulePage and RegularBaselineEditor updates**

1. Update `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx`:
   - In `ManagementWeekPage`:
     - Desktop (`FilterToolbar`): render `AdminFilters` when `role === 'admin'`, and render `<ShiftFilter view={view} emptyLabel="ทุกกะ" />` when `role === 'supervisor'`.
     - Mobile: render `MobileAdminFilters` when `role === 'admin'`, and render `<MobileSupervisorShiftFilter view={view} />` (`ทุกกะ`, `กะเช้า`, `กะดึก`) when `role === 'supervisor'`.
   - In `ManagementWeek` row header (`th`):
     - When `person.cleanerKind === 'Regular'`:
       - If `!person.days.some((d) => d.shift != null)`: show `<Badge kind="edited">ยังไม่จัดกะ</Badge>`
       - Else if `person.days.some((d) => d.edit) && person.days.every((d) => d.area.value == null)`: show `<Badge kind="edited">ยังไม่จัดพื้นที่</Badge>`
2. Update `frontend/src/pages/schedules/SchedulePage/ScheduleContextBar.tsx`:
   - Compute `const hasBaseline = person.days.some((one) => one.edit?.baselineAreaId != null);`
   - Set button text to `hasBaseline ? 'แก้ตารางประจำ' : 'ตั้งตารางประจำ'`.
3. Update `frontend/src/pages/schedules/RegularBaselineEditor/type.ts`, `useRegularBaselineEditor.ts`, `RegularBaselineEditor.tsx`:
   - In `type.ts`: add `shift: Shift | '';` to `RegularBaselineDraft`.
   - In `useRegularBaselineEditor.ts`:
     - Replace `tomorrow = offsetDate(clockSlot(new Date()).startDate, 1)` with `today = clockSlot(new Date()).startDate`.
     - Compute `initialDay = props.person.days.find((day) => day.date >= today && day.edit?.canChange)`.
     - Initialize `shift: initialDay?.shift ?? ''`, `effectiveFrom: initialDay?.date ?? today`, `areaId: String(initialDay?.edit?.baselineAreaId ?? '')`, `breakGroup: String(initialDay?.edit?.baselineBreakGroup ?? '')`.
     - Compute `areas`: if `shift` is chosen and `targetWeek?.destinations?.length` has entries matching `date === effectiveFrom && dest.shift === shift && (props.person.homeBuildingId == null || dest.buildingId === props.person.homeBuildingId)`, use those `areas` (so `DayOnly` areas are excluded on `Night` shift!); otherwise fall back to `current?.edit?.areas ?? []`.
     - When changing `shift` via `setField('shift', nextShift)`: clear `areaId` if no longer in the filtered areas for `nextShift`.
     - Validation:
       - `if (effectiveFrom < today) problem('เลือกวันเริ่มใช้ตั้งแต่วันนี้ขึ้นไป');`
       - `if (edited && (!shift || !areaId)) problem('เลือกกะและพื้นที่ก่อนตรวจสอบ');` (do NOT require `breakGroup`!)
     - Build `days` with `breakGroup: breakGroup ? Number(breakGroup) : null` and `command.payload = { userId: props.person.userId, effectiveFrom, shift, days }`.
     - Update `beforeAfter` line text to include Shift label (`shiftShortLabel[shift]`) and `breakGroup ? 'กลุ่มพัก ' + breakGroup : 'ยังไม่กำหนดกลุ่มพัก'`.
   - In `RegularBaselineEditor.tsx`:
     - Set `min={editor.today}` on `<TextInput type="date" ... />`.
     - Add `<Field label="กะ">` `<Select size={size} value={editor.draft.shift} ...>` with `<option value="">เลือกกะ</option>`, `<option value="Day">กะเช้า</option>`, `<option value="Night">กะดึก</option>`.
     - Change `<Field label="กลุ่มพักประจำ">` default option from `เลือกกลุ่มพัก` to `— ยังไม่กำหนด —` (value `""`).

- [ ] **Step 4: Run all frontend tests, lint, and production build**

Run:
```bash
npm --prefix frontend run lint && NODE_ENV=test npm --prefix frontend test && npm --prefix frontend run build
```
Expected: 0 ESLint errors, all Vitest files pass, `tsc -b && vite build` succeeds with main JS bundle `<= 200 kB` gzip.

- [ ] **Step 5: Commit Task 6**

```bash
git add frontend/src/pages/schedules/ frontend/src/shared/api/scheduleTypes.ts
git commit -m "feat(frontend): add supervisor shift filter and same-day shift/area baseline setup"
```

---

### Task 7: End-to-End Verification, E2E Spec, Production Config Check, and `docs/handoff.md`

**Files:**
- Modify: `frontend/e2e/desktop/scheduling.spec.ts`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Complete backend + frontend from Tasks 1–6.
- Produces: End-to-end Playwright test verifying the exact owner scenario (Admin creates 2 Regular Cleaners -> Supervisor assigns Area 1 + Sunday off and Area 2 + Saturday off -> Cleaners see their schedule on `/my-schedule`), full test gate results, and updated `docs/handoff.md`.

- [ ] **Step 1: Add the E2E test in `frontend/e2e/desktop/scheduling.spec.ts`**

Append a new test inside `test.describe('scheduling', ...)` in `frontend/e2e/desktop/scheduling.spec.ts`:
- Log in as Admin, navigate to `/admin/accounts`, create 2 Regular Cleaners selecting only `ตึกประจำ` (building A) without shift or Area.
- Log in as Supervisor (`fixture.supervisorAB`), open `/schedule-management`, select each newly created Cleaner, click `ตั้งตารางประจำ`, set `วันเริ่มใช้ = today`, `กะ = กะเช้า`, `พื้นที่ประจำ` (two different active Areas), save both.
- Click `จัดวันหยุดรายสัปดาห์`, check Sunday for Cleaner 1 and Saturday for Cleaner 2, review and save.
- Open Cleaner 1's `/my-schedule` and Cleaner 2's `/my-schedule` and assert each sees their assigned Area and respective day off.

- [ ] **Step 2: Run full Backend + Frontend + Playwright E2E verification gates**

Run:
```bash
dotnet test backend/tests/JanitorialManagementDashboard.UnitTests && \
dotnet test backend/tests/JanitorialManagementDashboard.ApiTests && \
npm --prefix frontend run lint && \
NODE_ENV=test npm --prefix frontend test && \
npm --prefix frontend run build && \
E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset large scheduling.spec.ts admin-workflows.spec.ts
```
Expected: All UnitTests pass, all non-MySQL ApiTests pass (and MySQL tests pass if `FACILITY_MYSQL_TEST` is configured), frontend lint/test/build pass (`<= 200 kB` gzip), and Playwright `scheduling.spec.ts` + `admin-workflows.spec.ts` pass with 0 failures.

Also verify Production readiness checklist note in `docs/handoff.md`:
- Ensure Production deployment environment defines `Scheduling__CommandSigningKey` (>= 32 bytes) so `/api/schedules/command-tokens` and `/api/schedules/commands` succeed on save.

- [ ] **Step 3: Update `docs/handoff.md` and commit Task 7**

Prepend a dated entry at the top of `docs/handoff.md` summarizing the root cause (`f9c3644`), migration `MakeCleanerProfileShiftNullable`, startup backfill + Area/Account sync, Supervisor both-shift scheduling (`facility-0240`), same-day baseline setup (`facility-0241`), directly observed verification counts, and reminder to verify `Scheduling__CommandSigningKey` on Production.

```bash
git add frontend/e2e/desktop/scheduling.spec.ts docs/handoff.md
git commit -m "test(e2e): verify supervisor scheduling onboarding flow and update handoff"
```

---

## Self-Review Results

1. **Spec coverage:**
   - Production `f9c3644` startup backfill & Area/Account sync (`§1`, `§4`, `facility-0238`) -> Task 2.
   - `CleanerProfilePeriod.Shift` nullable & migration (`§2`, `facility-0239`) -> Task 1.
   - Regular Cleaner `HomeBuildingId` with optional `Shift`/`AreaId` (`§4.2`, `§6.1`, `facility-0239`) -> Tasks 2 & 5.
   - Supervisor both-shift scheduling vs own-shift `/my-building` & QR inspection (`§3`, `facility-0240`) -> Task 3.
   - `set-regular-baseline` with `EffectiveFrom >= businessDate`, `Shift`, optional `BreakGroup`, `(AreaId, Shift)` uniqueness + `DayOnly`, `users` sync (`§5.1`, `§6.2–6.3`, `facility-0241`) -> Tasks 4 & 6.
   - Weekly day-off roster preserved (`§5.2`, `facility-0190`, `facility-0191`) -> Task 4 & Task 7 E2E.
   - Acceptance criteria AC-01..AC-08 and verification plan (`§8`) -> covered across Tasks 1–7.
2. **Placeholder scan:** No "TBD", "TODO", "add validation", or deferred references; every task names exact files, symbols, commands, and expected outputs.
3. **Type consistency:** `CleanerProfilePeriod.Shift` (`Shift?`), `ScheduleSync.SyncCleanerAsync`, `ScheduleAuthority.CanManageSchedule` / `CanCorrectScheduleHistory`, `SchedulePayload.BaselineRequest.Shift`, `ShiftChoice.allowEmpty`, `RegularBaselineDraft.shift` match across all tasks and interfaces.
