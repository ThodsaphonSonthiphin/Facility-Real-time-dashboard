# UX Fix List Implementation Plan (plan 13)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the items the owner kept in facility-0074, as settled in facility-0075 to 0085:
- the Cleaner phone problems
- D6 "แก้เบอร์โทร" and D4 round-window times
- D3 covers up to 7 days ahead
- the words on screen: what people on site say, not the team's glossary

**Architecture:**
- **Mostly frontend.** The server changes in three places:
  - `GET /api/my-work` also sends the Cleaner's own shift and the clock times its attendance window opens and closes (facility-0077).
  - A new `GET /api/attendance/signs/{token}` says only whether a check-in sign works (facility-0082).
  - Server messages, audit summaries and Excel headers take the approved words (facility-0085).
- **D3 ahead** uses the attendance board the page already reads: `GET /api/admin/attendance?shiftDate&shift` for a shift that has not started lists every Area that works it, with its regular Cleaner (facility-0084). No server change.
- **Order:** the behaviour fixes first, phone pages before Admin pages (facility-0070). Then the word sweep: phone pages, Admin live and report pages, Admin setup pages, then the server. Text a task adds is written in the approved words from the start. The sweep changes only text that was already there.

**Tech Stack:**
- Backend: .NET 10 Minimal API, EF Core 10, xUnit (SQLite in memory).
- Frontend: React 19.2, TypeScript 6.0, Vite 8.3, `@reduxjs/toolkit` 2.13.0 (RTK Query), `react-router` 7.18.4, Vitest 5.0 on jsdom, Testing Library, eslint 9 with boundaries.
- **No new package.**

**Spec:** `docs/adr/facility-0074-ux-pass-becomes-a-fixed-fix-list.md` as amended on 2026-10-22, and the ADRs it points to:
- `facility-0075-covers-assigned-up-to-7-days-ahead.md` and `facility-0084-advance-cover-picks-area-from-searchable-list.md`: D3 picks a date and shift up to 7 days ahead. A shift not started lists every Area with its regular Cleaner, with a search box. The covers box follows the pick.
- `facility-0076-scan-warning-clears-when-complete.md`: the "เลือกปัญหาที่พบ…" warning shows once ส่งงาน was pressed, and only while nothing is chosen.
- `facility-0077-off-shift-pages-show-own-shift-time.md`: outside the own shift, My Work shows the own shift and when check-in opens, with no counts and no cards. The check-in page shows the own shift and the times attendance can be recorded.
- `facility-0078-check-in-row-shows-only-recorded-times.md`: the "วันนี้" row shows only recorded times. No "ถัดไป".
- `facility-0079-submit-result-status-as-one-line.md`: the result page shows the point status as one line. No card.
- `facility-0080-wrong-area-sign-keeps-block-on-press.md`: another Area's sign still blocks on press. **Nothing to build.**
- `facility-0081-phone-form-says-old-number-not-needed.md`: D6's phone form shows who is being edited and says the old number is not needed.
- `facility-0082-unusable-check-in-sign-told-on-open.md`: an unusable check-in sign is told when the page opens, with no button.
- `facility-0083-round-window-time-split-hour-minute.md`: D4 round-window times are typed as an hour box and a minute box.
- `facility-0085-ui-words-from-the-users-not-the-glossary.md`: the approved word list.
- Also in force: `facility-0063` (plain CSS), `0066` (React text only), `0059` (no link to a sign page), `0068` (nothing stored on the device), `0062` (bundle budget), `0071` (keep the current look), `0073` (Admin pages are for a computer).

## Global Constraints

- **Branch:** `feat/ux-fix-list`, created from `master` at `7ced464` (PR #65 merged). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - In zsh, quote globs: `grep --include='*.tsx'`.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
  - An untracked `package-lock.json` sits at the repo root. It is not part of this plan. Never `git add` it. Add files by name; never `git add -A` or `git add .`.
- **No new package.** Neither `package.json` nor any `.csproj` changes.
- **No migration.** The database schema does not change.
- **Approved words (facility-0085).** Every task follows this table for text the users see. That includes pages, server messages, Excel headers and values, and printed signs.

  | Old | New |
  |---|---|
  | Area | พื้นที่ (e.g. "พื้นที่ของคุณ", "รหัสพื้นที่") |
  | Admin | แอดมิน |
  | GPS / Location | ตำแหน่ง |
  | ติดธง | ไกลป้าย |
  | เลยรอบ | เลยเวลา |
  | งานนอกรอบ / นอกรอบ | ส่งเพิ่ม (ไม่นับรอบ) |
  | ข้อบกพร่อง | สิ่งที่ต้องแก้ |
  | Dashboard | หน้าสถานะจุด |
  | สแกนนอก Area / ตึก / กะ (ถูกบล็อก), สแกนแล้วถูกบล็อก | สแกนผิดพื้นที่ / สแกนผิดตึก / สแกนนอกกะ |
  | ออก QR ใหม่ | สร้าง QR ใหม่ |
  | ธง, ตรวจที่ติดธง GPS | ไกลป้าย, ตรวจตอนอยู่ไกลป้าย |
  | พิกัด "หน้างาน" / "จากแผนที่" | วัดที่ป้ายจริง / ปักจากแผนที่ |
  | ประวัติ Admin | ประวัติการแก้ไขของแอดมิน |
  | Issue (Excel) | แจ้งปัญหา |

  - Kept as they are: QR, สแกน, จุด, รอบ, ป้ายลงเวลา, เข้างาน/ออกพัก/กลับจากพัก/เลิกงาน, ส่งงาน, ทำแทน, รอตรวจ, ตรวจผ่าน, ไม่สะอาด, หัวหน้าให้แก้, กะเช้า/กะดึก, Excel, รัศมี, "หน้างานของฉัน" (the page name, not a place).
  - Thai has no spaces between words, so "แจ้ง Admin" becomes "แจ้งแอดมิน" and "Area นี้" becomes "พื้นที่นี้". A space stays next to a code or a number: "พื้นที่ AR01", "3 พื้นที่".
  - Unchanged: names in code, API fields, enum values, database names, CONTEXT.md and ADR terms (CONTEXT.md already pairs them with the screen words).
  - Data typed by people stays as it is. That includes Area names such as "Area 1 ชั้น 1" in `DbInitializer.cs` and `'Area ทดลอง'` in the test fixtures, and audit rows already written.
- **Text added by Tasks 1–7** is written in the approved words. Text that already exists is changed only by Tasks 8–11.
- **Styles:**
  - Plain CSS (facility-0063), no `style={...}`.
  - Page prefixes as they are (`checkin-`, `mywork-`, `scan-`, `accounts-`, `areas-`, `alerts-`). Shared pieces use `app-`.
  - No new token in `src/index.css`.
- **Text people type and server text** are rendered as React text only (facility-0066). No `dangerouslySetInnerHTML`.
- **No link to a sign page** (`/scan/…`, `/check-in/…`) from any page (facility-0059).
- **Nothing is stored on the device** (facility-0068): no `localStorage`, `sessionStorage` or IndexedDB. The D3 search and pick live in `useState`.
- **Thai time:** every time and date on screen goes through `shared/utils/thaiTime.ts`. Never call `toLocaleString`, `toLocaleTimeString` or `toLocaleDateString` elsewhere.
- **Missing fields:** the server leaves out null fields. Types mark them optional, code tests them with truthiness or `== null`, and fixtures leave them out.
- **Bundle (facility-0062):** the JavaScript `dist/index.html` loads must stay ≤ 200 kB gzip.
  - Tasks 4, 8 and 12 measure it with `npm run build`, then delete `frontend/dist`.
  - Where a handoff entry or an ADR line says `<size>`, write the measured number.
  - If it goes over, stop and report.
- **Tests:**
  - Vitest on jsdom. Only `fetch` (`src/test/fakeServer.ts`) and SignalR (`src/test/fakeSignalR.ts`, through `test.alias`) are faked.
  - No MSW, no `vi.mock`, no module mocks of our own code (regression-net #32).
  - Find elements by role, label or text, never by CSS class.
  - Backend: xUnit, `FacilityApiFactory` (SQLite in memory), `ThaiClock.At(day, hour, minute)` on 2026-10-day. Log in before moving the clock.
- **Public repo:** made-up names only, as in the fixtures. No secret in any file.
- **MySQL** is used only in Task 12's controller run.
  - Only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - On this Mac MySQL has no root password. If it asks for one, ask the owner. Never write it into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry under `## Task log (plan 13)` in `docs/handoff.md`. Task 1 creates the section.
  - The entry is in English: the date, the task, what changed, the test result, the bundle size where measured, and what comes next.
  - No secret, phone number or real name.
  - The entry is committed with the task.
- **Commits:**
  - Each commit ends with the attribution trailer your session gives you.
  - The husky pre-commit runs frontend lint, tsc and tests. If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `7ced464`:**
  - frontend: 385 tests pass
  - backend: 299 unit tests pass; API tests 531 pass and 5 are skipped (the MySQL ones)
  - main JS: 157.53 kB gzip

### Decisions made while planning (report them to the owner in the PR)

1. **Off shift (facility-0077):**
   - `GET /api/my-work` gets `ownShift { shift, opensAt, closesAt }`. The times are Thai wall-clock `"HH:mm:ss"` from `AttendanceCalendar.WindowClock`, so a change to the 60-minute settings shows on the phone.
   - The server still sends the points off shift. My Work hides the counts and cards itself. The check-in and scan pages keep using the own Area's name.
   - `ownShift` is missing for an account with no shift. The pages then show only "ตอนนี้ไม่อยู่ในกะของคุณ" or "ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ".
2. **Check-in sign on open (facility-0082):**
   - `GET /api/attendance/signs/{token}` is Cleaner-only.
   - It answers 204 for an active check-in sign of an active Area, whoever's Area it is. Another Area's sign is still told on the press (facility-0080).
   - It answers 404 `{ message: "ไม่พบป้ายลงเวลานี้" }` for a point sign, an unknown token or a deactivated Area: the same rule as `POST /api/attendance`.
   - It records nothing.
   - The page shows its buttons only after a fresh 204. A 404 shows "ป้ายนี้ใช้ไม่ได้แล้ว" with no button. Any other failure shows the error with no button.
3. **Hour and minute boxes (facility-0083):**
   - The hour is 0–23, in one or two digits ("7" is 07).
   - The minute is empty (00) or two digits 00–59.
   - Non-digits are dropped as typed.
   - Two hour digits move the cursor to that time's minute box. Enter in any box adds the window.
   - The refusal is "ชั่วโมงใส่ 0–23 นาทีใส่ 00–59".
4. **D3 ahead (facility-0075, 0084):**
   - One picker, "วันและกะ", lists the shift on the clock ("(กะนี้)") through the Night shift 7 days later, with the Thai weekday: "กะเช้า ศ. 16/10/2026". That is 16 choices from a Day shift and 15 from a Night shift.
   - The list starts from the shift the server's board reports.
   - A shift not started shows every Area that works it with "แม่บ้านประจำ: …", and a search by Cleaner name, Area code or Area name.
   - The cover form and the candidates follow the chosen shift. For a shift ahead the candidates show no Shift-In, because nobody has checked in yet.
   - The covers box follows the pick, and a cover ahead is cancelled with "ยกเลิก".
   - The search also works on the shift on the clock. It searches only the rows on screen.
5. **Words (facility-0085):**
   - Audit rows already written keep their words: `audit_log` is append-only (facility-0057). New rows use the new words.
   - Seed data and fixture names are data, not words, so they stay.
   - The blocked-scan alert name in audit summaries now follows the scan's reason ("สแกนผิดพื้นที่", "สแกนผิดตึก", "สแกนนอกกะ"). This closes the parked item "ThaiLabels.AlertName(BlockedScan) is still 'สแกนนอก Area'".
   - Excel headers change ("พื้นที่", "ตำแหน่ง", "แจ้งปัญหา", "สิ่งที่ต้องแก้"), so a sheet someone built on the old headers needs its headers renamed.
6. **Nothing is built for facility-0080.** Another Area's sign still blocks on press, on both sign pages.

---

## File structure after this plan

```
backend/src/
├── FacilityRealtime.Application/Attendance/AttendanceCalendar.cs   + WindowClock
├── FacilityRealtime.Api/DTOs/MyWorkDtos.cs                         + OwnShiftDto, MyWorkDto.OwnShift
├── FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs               sends ownShift
├── FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs           + GET /api/attendance/signs/{token}
├── FacilityRealtime.Api/Endpoints/ThaiLabels.cs                    words; BlockedName; AlertName(kind, reason)
├── FacilityRealtime.Api/Endpoints/AdminAlertEndpoints.cs           AlertTarget carries the block reason
└── (Task 11) messages and summaries in 12 endpoint files, ExportSheets headers, RoundWindowRules message
frontend/src/
├── shared/api/types.ts                     + OwnShift, MyWork.ownShift
├── shared/utils/thaiTime.ts (+ test)       + formatWeekday
├── pages/check-in/                         sign check on open, off-shift note, "วันนี้" without ถัดไป
├── pages/my-work/MyWorkPage.tsx            off shift: own shift, no counts or cards
├── pages/scan/                             warning clears, result status as one line
├── pages/accounts/SecretForms.tsx          phone form: who, "ไม่ต้องรู้เบอร์เดิม"
├── pages/areas/                            hour and minute boxes (PointEditor, pointRules)
├── pages/alerts/                           วันและกะ picker, search, covers ahead
├── test/cleanerFixtures.ts                 + dayShift, nightShift, myWork(…, ownShift)
├── test/liveFixtures.ts                    + aheadBoard
└── (Tasks 8–10) words in about 60 files and their tests
```

---

### Task 1: Server: the own shift on My Work, and the check-in sign check

**Files:**
- Modify: `docs/handoff.md`
- Modify: `backend/src/FacilityRealtime.Application/Attendance/AttendanceCalendar.cs`
- Test: `backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/MyWorkDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs`
- Modify: `backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Points/MyWorkEndpointTests.cs`
- Test: `backend/tests/FacilityRealtime.ApiTests/Attendance/AttendanceEndpointTests.cs`

**Interfaces:**
- Consumes: `ShiftCalendar.DayStart`, `ShiftCalendar.NightStart`, `AttendanceSettings`, `CurrentUser.LoadAsync`, `ApiResults.Message`.
- Produces:
  - `AttendanceCalendar.WindowClock(Shift shift, AttendanceSettings settings): (TimeOnly Opens, TimeOnly Closes)`
  - `record OwnShiftDto(Shift Shift, TimeOnly OpensAt, TimeOnly ClosesAt)`
  - `MyWorkDto(AttendanceStateDto Attendance, IReadOnlyList<MyWorkAreaDto> Areas, OwnShiftDto? OwnShift)`
  - JSON for later tasks: `GET /api/my-work` → `{ attendance, areas, ownShift?: { shift: "Day" | "Night", opensAt: "06:00:00", closesAt: "20:00:00" } }`.
  - `GET /api/attendance/signs/{token}` → 204 with no body, or 404 `{ message: "ไม่พบป้ายลงเวลานี้" }`. 401 when logged out, and 403 `{ message }` for anyone but a Cleaner.

- [ ] **Step 1: Check out the branch**

The branch `feat/ux-fix-list` already exists. It was created from `master` at `7ced464`, and its first commit holds this plan and the handoff.

```bash
git checkout feat/ux-fix-list
git log --oneline -2   # expect the plan commit "docs: plan 13 …", then 7ced464
git status --short     # expect only the untracked package-lock.json
```

- [ ] **Step 2: Bring the handoff up to date**

In `docs/handoff.md`:

1. Below the plan 12 row of the Status table, add:

```markdown
| 13 | UX fix list: facility-0074 as amended (facility-0075 to 0085) | in progress (branch `feat/ux-fix-list`): `docs/superpowers/plans/2026-10-22-ux-fix-list.md` |
```

2. In `## Pending work (ask the owner before starting)`, replace the line that starts with `- UX fix list (facility-0074, font done;` with:

```markdown
- UX fix list (facility-0074 as amended): plan 13, in progress on `feat/ux-fix-list`.
```

3. Just above `## Sharing a test build (colleagues on other networks)`, add the heading and a blank line:

```markdown
## Task log (plan 13)

```

Step 11 adds the first entry under it.

- [ ] **Step 3: Write the failing unit test**

In `backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs`, add inside the class (it already has `Settings` with 60 and 60):

```csharp
    [Theory]
    [InlineData(Shift.Day, 6, 20)]
    [InlineData(Shift.Night, 18, 8)]
    public void Window_clock_is_the_thai_time_the_window_opens_and_closes(Shift shift, int opensHour, int closesHour)
    {
        var (opens, closes) = AttendanceCalendar.WindowClock(shift, Settings);

        Assert.Equal(new TimeOnly(opensHour, 0), opens);
        Assert.Equal(new TimeOnly(closesHour, 0), closes);
    }

    [Fact]
    public void Window_clock_follows_the_settings()
    {
        var (opens, closes) = AttendanceCalendar.WindowClock(
            Shift.Night, new AttendanceSettings { OpensMinutesBeforeShift = 90, ClosesMinutesAfterShift = 30 });

        Assert.Equal(new TimeOnly(17, 30), opens);
        Assert.Equal(new TimeOnly(7, 30), closes);
    }
```

- [ ] **Step 4: Run it to make sure it fails**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AttendanceCalendarTests"`
Expected: build error, `'AttendanceCalendar' does not contain a definition for 'WindowClock'`.

- [ ] **Step 5: Implement `WindowClock`**

In `backend/src/FacilityRealtime.Application/Attendance/AttendanceCalendar.cs`, add after `SlotFor`:

```csharp
    /// <summary>
    /// facility-0077: the Thai wall-clock times a shift's attendance window opens and closes, as a phone shows them:
    /// Day 06:00 and 20:00, Night 18:00 and 08:00 (the next morning) with the default settings.
    /// </summary>
    public static (TimeOnly Opens, TimeOnly Closes) WindowClock(Shift shift, AttendanceSettings settings)
    {
        var start = shift == Shift.Day ? ShiftCalendar.DayStart : ShiftCalendar.NightStart;
        return (start.AddMinutes(-settings.OpensMinutesBeforeShift), start.AddHours(12).AddMinutes(settings.ClosesMinutesAfterShift));
    }
```

(`TimeOnly` arithmetic wraps past midnight, which is what a wall clock does.)

- [ ] **Step 6: Run it to make sure it passes**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --filter "FullyQualifiedName~AttendanceCalendarTests"`
Expected: PASS.

- [ ] **Step 7: Write the failing API tests**

In `backend/tests/FacilityRealtime.ApiTests/Points/MyWorkEndpointTests.cs`, replace the `MyWorkModel` record with these two:

```csharp
public record OwnShiftModel(string Shift, TimeOnly OpensAt, TimeOnly ClosesAt);

public record MyWorkModel(AttendanceStateModel Attendance, List<MyWorkAreaModel> Areas, OwnShiftModel? OwnShift = null);
```

Then add to the class:

```csharp
    [Theory]
    [InlineData("E1001", "0810000001", 5, "Day", 6, 20)]     // 05:00: before the Day window
    [InlineData("E1002", "0810000002", 14, "Night", 18, 8)]  // 14:00: between two Night windows
    public async Task Outside_the_window_my_work_still_names_the_own_shift_and_its_window(
        string employeeId, string phone, int thaiHour, string shift, int opensHour, int closesHour)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsEmployeeAsync(factory, employeeId, phone);
        factory.Clock.SetUtcNow(ThaiClock.At(8, thaiHour, 0));

        var work = (await cleaner.GetFromJsonAsync<MyWorkModel>("/api/my-work"))!;

        Assert.Null(work.Attendance.Shift);
        Assert.Equal(new OwnShiftModel(shift, new TimeOnly(opensHour, 0), new TimeOnly(closesHour, 0)), work.OwnShift);
    }

    [Fact]
    public async Task Inside_the_window_my_work_also_names_the_own_shift()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 8, 30));

        var work = (await cleaner.GetFromJsonAsync<MyWorkModel>("/api/my-work"))!;

        Assert.Equal("Day", work.Attendance.Shift);
        Assert.Equal(new OwnShiftModel("Day", new TimeOnly(6, 0), new TimeOnly(20, 0)), work.OwnShift);
    }
```

In `backend/tests/FacilityRealtime.ApiTests/Attendance/AttendanceEndpointTests.cs`, add `using System.Text.Json;` to the usings and add to the class:

```csharp
    [Theory]
    [InlineData("token-checkin-ar01")]
    [InlineData("token-checkin-ar02")] // another Area's sign: told on the press, as a Blocked Scan (facility-0080)
    public async Task A_working_check_in_sign_answers_no_content_and_records_nothing(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        factory.Clock.SetUtcNow(ThaiClock.At(8, 7, 0));

        var response = await cleaner.GetAsync($"/api/attendance/signs/{token}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Empty(await AllAsync(factory, db => db.BlockedScans));
        Assert.Empty(await AllAsync(factory, db => db.ShiftAttendances));
    }

    [Theory]
    [InlineData("token-restroom-m1")]
    [InlineData("no-such-token")]
    public async Task Point_signs_and_unknown_tokens_fail_the_check_in_sign_check(string token)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await cleaner.GetAsync($"/api/attendance/signs/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ไม่พบป้ายลงเวลานี้", body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_sign_of_a_deactivated_area_fails_the_check()
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Areas.SingleAsync(a => a.Code == "AR02")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await cleaner.GetAsync("/api/attendance/signs/token-checkin-ar02");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Only_a_logged_in_cleaner_checks_a_check_in_sign()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);

        var anonymous = await factory.CreateApiClient().GetAsync("/api/attendance/signs/token-checkin-ar01");
        var byAdmin = await admin.GetAsync("/api/attendance/signs/token-checkin-ar01");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
    }
```

- [ ] **Step 8: Run them to make sure they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~MyWorkEndpointTests|FullyQualifiedName~AttendanceEndpointTests"`
Expected: FAIL. The My Work tests fail because `OwnShift` is null. The sign tests fail with 404 or 405 where 204 is expected, because the route does not exist yet.

- [ ] **Step 9: Implement the DTO and both endpoints**

In `backend/src/FacilityRealtime.Api/DTOs/MyWorkDtos.cs`, replace the `MyWorkDto` record (keep `MyWorkAreaDto`). The file has no `using` yet, and `Shift` is in `FacilityRealtime.Domain.Enums`, so add that line at the top:

```csharp
using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Api.DTOs;

public record MyWorkAreaDto(int AreaId, string AreaCode, string AreaName, bool IsCover, IReadOnlyList<PointStatusDto> Points);

/// <summary>facility-0077: the Cleaner's regular shift and the Thai wall-clock times its attendance window opens and closes.</summary>
public record OwnShiftDto(Shift Shift, TimeOnly OpensAt, TimeOnly ClosesAt);

/// <summary>
/// facility-0052: the Cleaner's page — attendance buttons, then the own Area, then covered Areas. OwnShift is null
/// for an account with no shift; the phone uses it to say when the own shift is (facility-0077).
/// </summary>
public record MyWorkDto(AttendanceStateDto Attendance, IReadOnlyList<MyWorkAreaDto> Areas, OwnShiftDto? OwnShift);
```

In `backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs`, replace the last statement of `GetAsync`:

```csharp
        return Results.Ok(new MyWorkDto(await AttendanceEndpoints.StateForAsync(db, user, now, settings), areas));
```

with:

```csharp
        var ownShift = user.Shift is { } regular ? OwnShiftOf(regular, settings) : null;
        return Results.Ok(new MyWorkDto(await AttendanceEndpoints.StateForAsync(db, user, now, settings), areas, ownShift));
    }

    private static OwnShiftDto OwnShiftOf(Shift shift, AttendanceSettings settings)
    {
        var (opens, closes) = AttendanceCalendar.WindowClock(shift, settings);
        return new OwnShiftDto(shift, opens, closes);
```

(The method's closing brace that followed the old `return` now closes `OwnShiftOf`. `Shift` comes from `FacilityRealtime.Domain.Enums`, already imported.)

In `backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs`:

1. Under `private const string CleanersOnly = …;` add:

```csharp
    private const string SignNotFound = "ไม่พบป้ายลงเวลานี้";
```

2. In `MapAttendanceEndpoints`, after the `MapGet("/api/attendance/me", …)` line, add:

```csharp
        app.MapGet("/api/attendance/signs/{token}", CheckSignAsync).RequireAuthorization();
```

3. In `RecordAsync`, replace:

```csharp
        var sign = await db.Signs.Include(s => s.Area)
            .FirstOrDefaultAsync(s => s.QrToken == request.QrToken && s.ServicePointId == null);
        if (sign?.Area is not { IsActive: true } area)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, "ไม่พบป้ายลงเวลานี้");
        }
```

with:

```csharp
        var sign = await FindCheckInSignAsync(db, request.QrToken);
        if (sign is null)
        {
            return ApiResults.Message(StatusCodes.Status404NotFound, SignNotFound);
        }

        var area = sign.Area!;
```

4. Add these two methods after `GetMineAsync`:

```csharp
    /// <summary>
    /// facility-0082: the check-in page asks on opening whether its sign still works. It records nothing, and it never
    /// says whose sign it is: another Area's sign is told on the press, as a Blocked Scan (facility-0080).
    /// </summary>
    private static async Task<IResult> CheckSignAsync(string token, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = await CurrentUser.LoadAsync(principal, db);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.Role != UserRole.Cleaner)
        {
            return ApiResults.Message(StatusCodes.Status403Forbidden, CleanersOnly);
        }

        return await FindCheckInSignAsync(db, token) is null
            ? ApiResults.Message(StatusCodes.Status404NotFound, SignNotFound)
            : Results.NoContent();
    }

    /// <summary>An Area's check-in sign by its QR Token, with its Area, while the Area is active; otherwise null.</summary>
    private static Task<Sign?> FindCheckInSignAsync(AppDbContext db, string token) =>
        db.Signs.Include(s => s.Area)
            .FirstOrDefaultAsync(s => s.QrToken == token && s.ServicePointId == null && s.Area!.IsActive);
```

`Sign` is in `FacilityRealtime.Domain.Entities`, which the file already imports.

- [ ] **Step 10: Run the backend suite**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: unit 302 passed (3 new cases); API 540 passed, 5 skipped (9 new cases: 3 for My Work, 6 for the sign check).

- [ ] **Step 11: Log the task in the handoff and commit**

Under `## Task log (plan 13)` in `docs/handoff.md` add:

```markdown
- 2026-10-22 · Task 1 done: `GET /api/my-work` sends `ownShift { shift, opensAt, closesAt }` (AttendanceCalendar.WindowClock; facility-0077); new `GET /api/attendance/signs/{token}` (Cleaner only; 204 for an active check-in sign of an active Area, 404 "ไม่พบป้ายลงเวลานี้" otherwise; records nothing; facility-0082). Unit 302; API 540 + 5 skipped. Next: Task 2 (check-in page).
```

```bash
git add docs/handoff.md \
  backend/src/FacilityRealtime.Application/Attendance/AttendanceCalendar.cs \
  backend/tests/FacilityRealtime.UnitTests/AttendanceCalendarTests.cs \
  backend/src/FacilityRealtime.Api/DTOs/MyWorkDtos.cs \
  backend/src/FacilityRealtime.Api/Endpoints/MyWorkEndpoints.cs \
  backend/src/FacilityRealtime.Api/Endpoints/AttendanceEndpoints.cs \
  backend/tests/FacilityRealtime.ApiTests/Points/MyWorkEndpointTests.cs \
  backend/tests/FacilityRealtime.ApiTests/Attendance/AttendanceEndpointTests.cs
git commit -m "feat(api): own shift on My Work and the check-in sign check (facility-0077, 0082)"
```

---

### Task 2: Check-in page: unusable sign on open, own shift off shift, "วันนี้" without ถัดไป

**Files:**
- Modify: `frontend/src/shared/api/types.ts`
- Modify: `frontend/src/test/cleanerFixtures.ts`
- Modify: `frontend/src/pages/check-in/checkInApi.ts`
- Modify: `frontend/src/pages/check-in/CheckInPage.tsx`
- Modify: `frontend/src/pages/check-in/AttendanceTimeline.tsx`
- Modify: `frontend/src/pages/check-in/checkIn.css`
- Test: `frontend/src/pages/check-in/CheckInPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (Task 1): `GET /api/my-work` → `ownShift?: { shift, opensAt: "HH:mm:ss", closesAt: "HH:mm:ss" }`. `GET /api/attendance/signs/{token}` → 204, or 404 `{ message }`.
- Produces:
  - In `shared/api/types.ts`:
    - `interface OwnShift { shift: Shift; opensAt: string; closesAt: string }`
    - `MyWork.ownShift?: OwnShift`
  - In `test/cleanerFixtures.ts`:
    - `dayShift: OwnShift` (06:00–20:00)
    - `nightShift: OwnShift` (18:00–08:00)
    - `myWork(attendanceState?, areas?, ownShift: OwnShift = dayShift): MyWork`
  - In `pages/check-in/checkInApi.ts`: `useCheckSignQuery(token: string, options)`. Its data is `null` on 204.

- [ ] **Step 1: Add the type and the fixtures**

In `frontend/src/shared/api/types.ts`, replace the `MyWork` interface with:

```ts
/** OwnShiftDto (facility-0077): the Cleaner's regular shift and the Thai wall-clock times its attendance opens and closes, "HH:mm:ss". */
export interface OwnShift {
  shift: Shift;
  opensAt: string;
  closesAt: string;
}

/** MyWorkDto (facility-0052): the attendance, then the own Area, then covered Areas. ownShift is missing for an account with no shift. */
export interface MyWork {
  attendance: AttendanceState;
  areas: MyWorkArea[];
  ownShift?: OwnShift;
}
```

In `frontend/src/test/cleanerFixtures.ts`:

1. Add `OwnShift` to the type import from `'../shared/api/types'` (alphabetical: after `MyWorkArea`).
2. Replace the `myWork` function with:

```ts
/** The own shift as the server sends it with the default settings: attendance from 60 minutes before to 60 after. */
export const dayShift: OwnShift = { shift: 'Day', opensAt: '06:00:00', closesAt: '20:00:00' };
export const nightShift: OwnShift = { shift: 'Night', opensAt: '18:00:00', closesAt: '08:00:00' };

export function myWork(
  attendanceState: AttendanceState = onDuty,
  areas: MyWorkArea[] = [area()],
  ownShift: OwnShift = dayShift,
): MyWork {
  return { attendance: attendanceState, areas, ownShift };
}
```

- [ ] **Step 2: Write the failing tests**

In `frontend/src/pages/check-in/CheckInPage.test.tsx`:

1. Add `area` and `nightShift` to the import from `'../../test/cleanerFixtures'`. Add `waitFor` to the `@testing-library/react` import: `import { screen, waitFor, within } from '@testing-library/react';`. Change the `fakeServer` import line to `import { fakeServer, type FakeResponse, type FakeRoutes } from '../../test/fakeServer';`.
2. In `serve`, answer the sign check by default. Put it before `...routes` so a test can override it:

```ts
function serve(routes: FakeRoutes, work = myWork(notCheckedIn)) {
  return fakeServer({
    ...cleanerSession,
    'GET /api/my-work': () => ({ status: 200, body: work }),
    'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 204 }),
    ...routes,
  });
}
```

3. facility-0078: no step says "ถัดไป" any more. In the first test change:

```ts
    expect(steps()).toEqual(['เข้างาน ถัดไป', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
```

to:

```ts
    expect(steps()).toEqual(['เข้างาน –', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
```

In the second test, which is the one that presses เข้างาน and gets `['BreakOut', 'ShiftOut']`, change:

```ts
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก ถัดไป', 'กลับจากพัก –', 'เลิกงาน ถัดไป']);
```

to:

```ts
    // facility-0078: two events may come next, and only the buttons say so
    expect(steps()).toEqual(['เข้างาน 06:52', 'ออกพัก –', 'กลับจากพัก –', 'เลิกงาน –']);
    expect(screen.queryByText('ถัดไป')).not.toBeInTheDocument();
```

4. Replace the test `'outside the attendance window there is no button (facility-0069)'` with:

```ts
  it('outside the attendance window: no button, the own shift and the times it can be recorded (facility-0069, 0077)', async () => {
    serve({}, myWork(outsideShift, [area()], nightShift));
    await renderApp(SIGN);

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ')).toBeInTheDocument();
    expect(screen.getByText('กะของคุณ: กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(screen.getByText('ลงเวลาได้ตั้งแต่ 18:00 ถึง 08:00')).toBeInTheDocument();
    expect(screen.queryByText(/60 นาที/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });
```

5. Add these tests at the end of the `describe` block:

```ts
  it('a sign that no longer works is told on opening, with no button, and nothing is sent (facility-0082)', async () => {
    const server = serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => ({ status: 404, body: { message: 'ไม่พบป้ายลงเวลานี้' } }),
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('heading', { name: 'ป้ายนี้ใช้ไม่ได้แล้ว' })).toBeInTheDocument();
    expect(
      screen.getByText('ยังไม่ได้ลงเวลา ป้ายนี้อาจถูกสร้าง QR ใหม่แล้ว หรือไม่มีในระบบ แจ้งแอดมิน'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ดูงานของฉัน' })).toHaveAttribute('href', '/my-work');
    expect(server.calls.some((c) => c.path === '/api/attendance')).toBe(false);
  });

  it('the buttons wait until the server says the sign works, asked once per opening (facility-0082)', async () => {
    let answer: (response: FakeResponse) => void = () => {};
    const server = serve({
      'GET /api/attendance/signs/token-checkin-ar01': () =>
        new Promise<FakeResponse>((resolve) => {
          answer = resolve;
        }),
    });
    await renderApp(SIGN);

    expect(await screen.findByText('กะเช้า 07:00–19:00')).toBeInTheDocument();
    await waitFor(() => expect(server.calls.some((c) => c.path === '/api/attendance/signs/token-checkin-ar01')).toBe(true));
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();

    answer({ status: 204 });

    expect(await screen.findByRole('button', { name: 'เข้างาน' })).toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/attendance/signs/token-checkin-ar01')).toHaveLength(1);
  });

  it('if the sign cannot be checked, the page says why and offers no button', async () => {
    serve({
      'GET /api/attendance/signs/token-checkin-ar01': () => {
        throw new TypeError('Failed to fetch');
      },
    });
    await renderApp(SIGN);

    expect(await screen.findByRole('alert')).toHaveTextContent('เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่');
    expect(screen.queryByRole('button', { name: 'เข้างาน' })).not.toBeInTheDocument();
  });
```

- [ ] **Step 3: Run the tests to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/check-in`
Expected: FAIL. The steps still say "ถัดไป". The off-shift texts and "ป้ายนี้ใช้ไม่ได้แล้ว" are not found. The button shows before the sign check answers.

- [ ] **Step 4: Add the sign check to the API layer**

In `frontend/src/pages/check-in/checkInApi.ts`, add this endpoint after `recordAttendance` (inside `endpoints`):

```ts
    // facility-0082: 204 when the sign works, 404 when it does not. It records nothing and never says whose sign it is
    checkSign: build.query<null, string>({
      query: (token) => `/attendance/signs/${encodeURIComponent(token)}`,
    }),
```

and change the export to:

```ts
export const { useRecordAttendanceMutation, useCheckSignQuery } = checkInApi;
```

- [ ] **Step 5: Rewrite the check-in page**

Replace `frontend/src/pages/check-in/CheckInPage.tsx` with the following. Only these parts change: the sign check, `loadError`, `OffShiftNote`, and the imports they need. `press` and `AnsweredNote` are unchanged.

```tsx
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { freshOnOpen } from '../../shared/api/baseApi';
import { useGetMyWorkQuery } from '../../shared/api/myWorkApi';
import { blockedScanOf, isNotSent, statusOf } from '../../shared/api/scanAnswers';
import type { AttendanceEvent, AttendanceState, BlockedScan, OwnShift } from '../../shared/api/types';
import { BlockedScanScreen } from '../../shared/components/BlockedScanScreen';
import { LocationHelp } from '../../shared/components/LocationHelp';
import { StatusScreen } from '../../shared/components/StatusScreen';
import { readLocation, type LocationProblem } from '../../shared/geo/readLocation';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { eventLabel, shiftLabel } from '../../shared/utils/labels';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
import { AttendanceTimeline } from './AttendanceTimeline';
import { useCheckSignQuery, useRecordAttendanceMutation } from './checkInApi';
import './checkIn.css';

type Outcome =
  | { kind: 'answered'; eventType: AttendanceEvent; state: AttendanceState; accuracyM: number }
  | { kind: 'failed'; message: string }
  | { kind: 'blocked'; blocked: BlockedScan }
  | { kind: 'location'; problem: LocationProblem; eventType: AttendanceEvent };

/**
 * Wireframes M1, M2, M3, M8 and M10: the page an Area's check-in sign opens (/check-in/<token>, plan 8 decision 1).
 * On opening it asks whether the sign still works (facility-0082). The buttons are the events the server allows next
 * (facility-0031). The server records, flags or blocks (facility-0035, 0036, 0041) and never computes late or early
 * (facility-0050).
 */
export function CheckInPage() {
  const { token = '' } = useParams();
  const signQuery = useCheckSignQuery(token, freshOnOpen);
  const workQuery = useGetMyWorkQuery(undefined, freshOnOpen);
  const [record] = useRecordAttendanceMutation();
  const [pressed, setPressed] = useState<AttendanceEvent | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  const work = workQuery.data;
  const attendance = work?.attendance;
  const ownArea = work?.areas.find((area) => !area.isCover);
  // A 404 is a sign that no longer works; any other failure of the check shows like a failed load
  const signGone = statusOf(signQuery.error) === 404;
  const loadError = workQuery.error ?? (signGone ? undefined : signQuery.error);
  // Buttons only from fresh answers, once the server has said the sign works: a cached answer may be from before
  // another device recorded something
  const signWorks = signQuery.isSuccess && !signQuery.isFetching;
  const liveAttendance = workQuery.isFetching || !signWorks ? undefined : attendance;

  const press = async (eventType: AttendanceEvent) => {
    setPressed(eventType);
    try {
      const reading = await readLocation();
      if (reading.kind !== 'fix') {
        setOutcome({ kind: 'location', problem: reading.kind, eventType });
        return;
      }
      const answer = await record({ qrToken: token, eventType, ...reading.fix });
      if (answer.data) {
        setOutcome({ kind: 'answered', eventType, state: answer.data, accuracyM: reading.fix.accuracyM });
        return;
      }
      const blocked = blockedScanOf(answer.error);
      if (blocked) {
        setOutcome({ kind: 'blocked', blocked });
        return;
      }
      setOutcome({
        kind: 'failed',
        message: isNotSent(answer.error)
          ? `ยังไม่ได้บันทึกเวลา ${getErrorMessage(answer.error)}`
          : getErrorMessage(answer.error),
      });
      if (statusOf(answer.error) === 409) {
        void workQuery.refetch(); // out of order or outside the window: show what the server allows now
      }
    } finally {
      setPressed(null);
    }
  };

  if (signGone) {
    return (
      <StatusScreen
        title="ป้ายนี้ใช้ไม่ได้แล้ว"
        detail="ยังไม่ได้ลงเวลา ป้ายนี้อาจถูกสร้าง QR ใหม่แล้ว หรือไม่มีในระบบ แจ้งแอดมิน"
        linkTo="/my-work"
        linkText="ดูงานของฉัน"
      />
    );
  }
  if (outcome?.kind === 'location') {
    const { problem, eventType } = outcome;
    return <LocationHelp problem={problem} action="ลงเวลา" busy={pressed !== null} onRetry={() => void press(eventType)} />;
  }
  if (outcome?.kind === 'blocked') {
    return <BlockedScanScreen blocked={outcome.blocked} sign="check-in" ownAreaName={ownArea?.areaName} />;
  }

  return (
    <main className="checkin-page">
      <header className="checkin-heading">
        <span className="checkin-kicker">ป้ายลงเวลา</span>
        <h1>ลงเวลา</h1>
        {attendance?.shift && <p className="checkin-sub">{shiftLabel[attendance.shift]}</p>}
        {ownArea && <p className="checkin-area">{`Area ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด`}</p>}
      </header>
      {workQuery.isLoading && <p className="checkin-hint">กำลังโหลด...</p>}
      {loadError != null && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(loadError)}
        </p>
      )}
      {attendance && !attendance.shift && <OffShiftNote ownShift={work?.ownShift} />}
      {attendance?.shift && <AttendanceTimeline attendance={attendance} />}
      {outcome?.kind === 'answered' && (
        <AnsweredNote eventType={outcome.eventType} state={outcome.state} accuracyM={outcome.accuracyM} />
      )}
      {outcome?.kind === 'failed' && (
        <p className="app-note app-note--error" role="alert">
          {outcome.message}
        </p>
      )}
      {liveAttendance?.shift && liveAttendance.nextEvents.length === 0 && (
        <p className="app-note app-note--info">ลงเวลาครบแล้วสำหรับกะนี้</p>
      )}
      {liveAttendance && liveAttendance.nextEvents.length > 0 && (
        <div className="checkin-actions">
          {liveAttendance.nextEvents.map((eventType, index) => (
            <button
              key={eventType}
              type="button"
              className={index === 0 ? 'checkin-press checkin-press--main' : 'checkin-press'}
              disabled={pressed !== null}
              onClick={() => void press(eventType)}
            >
              {pressed === eventType ? 'กำลังบันทึก...' : eventLabel[eventType]}
            </button>
          ))}
        </div>
      )}
      <p className="checkin-hint">ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน</p>
      <Link className="app-button" to="/my-work">
        ดูงานของฉัน
      </Link>
    </main>
  );
}

/** facility-0077: outside the attendance window the Cleaner is told their own shift and when it can be recorded. */
function OffShiftNote({ ownShift }: { ownShift?: OwnShift }) {
  return (
    <div className="app-note app-note--warn">
      <strong>ตอนนี้ไม่อยู่ในช่วงลงเวลาของกะคุณ</strong>
      {ownShift && <span>{`กะของคุณ: ${shiftLabel[ownShift.shift]}`}</span>}
      {ownShift && (
        <span>{`ลงเวลาได้ตั้งแต่ ${formatClock(ownShift.opensAt)} ถึง ${formatClock(ownShift.closesAt)}`}</span>
      )}
    </div>
  );
}

interface AnsweredNoteProps {
  eventType: AttendanceEvent;
  state: AttendanceState;
  accuracyM: number;
}

/** What the server did with the press: recorded (maybe flagged, M3), already recorded, or too soon (facility-0026, 0031). */
function AnsweredNote({ eventType, state, accuracyM }: AnsweredNoteProps) {
  const label = eventLabel[eventType];
  const recorded = state.events.find((e) => e.eventType === eventType);
  const time = recorded ? formatTime(recorded.occurredAt) : '';

  if (state.outcome === 'TooSoon') {
    return (
      <p className="app-note app-note--warn" role="status">
        {`ยังไม่บันทึก${label} เพราะเพิ่งลงเวลาไปไม่ถึง 5 นาที กดซ้ำภายใน 5 นาทีไม่นับ`}
      </p>
    );
  }
  if (state.outcome === 'AlreadyRecorded') {
    return (
      <p className="app-note app-note--info" role="status">
        {`${label}: ลงเวลาไว้แล้วตอน ${time} ระบบเก็บเวลาแรกไว้`}
      </p>
    );
  }
  if (recorded?.withinRadius === false) {
    return (
      <div className="app-note app-note--warn" role="status">
        <strong>{`บันทึกเวลา${label}แล้ว ${time} แต่อยู่ไกลจากป้าย · ติดธง`}</strong>
        <span>{`ความแม่นยำของตำแหน่ง ±${Math.round(accuracyM)} ม.`}</span>
        <span>Admin จะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้ง Admin ได้เลย</span>
      </div>
    );
  }
  return (
    <p className="app-note app-note--info" role="status">
      {`บันทึกเวลา${label}แล้ว ${time}`}
    </p>
  );
}
```

- [ ] **Step 6: Drop "ถัดไป" from the "วันนี้" row**

Replace `frontend/src/pages/check-in/AttendanceTimeline.tsx` with:

```tsx
import type { AttendanceState } from '../../shared/api/types';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/**
 * The "วันนี้" row of M1 and M10: what is already recorded, with its time and a far-from-sign mark (facility-0035).
 * Every other step reads "–": the buttons below are the only thing that says what may come next (facility-0078).
 */
export function AttendanceTimeline({ attendance }: { attendance: AttendanceState }) {
  return (
    <ol className="checkin-timeline" aria-label="วันนี้">
      {attendanceEvents.map((eventType) => {
        const recorded = attendance.events.find((e) => e.eventType === eventType);
        return (
          <li key={eventType} className={recorded ? 'checkin-step checkin-step--done' : 'checkin-step'}>
            <span>{eventLabel[eventType]}</span> <span>{recorded ? formatTime(recorded.occurredAt) : '–'}</span>
            {recorded?.withinRadius === false && (
              <>
                {' '}
                <span className="checkin-flag">ติดธง</span>
              </>
            )}
          </li>
        );
      })}
    </ol>
  );
}
```

In `frontend/src/pages/check-in/checkIn.css`, delete the whole `.checkin-step--next { … }` rule. Nothing uses it now.

- [ ] **Step 7: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/check-in && npx vitest run`
Expected: PASS, the check-in file and the whole suite (388 tests: 385 + 3 new; the replaced test keeps the count).

Then `npm run lint && npx tsc -b`. Expected: no errors.

- [ ] **Step 8: Log the task in the handoff and commit**

Add under `## Task log (plan 13)`:

```markdown
- 2026-10-22 · Task 2 done: the check-in page asks `GET /api/attendance/signs/{token}` on opening and shows buttons only after a fresh 204; a 404 shows "ป้ายนี้ใช้ไม่ได้แล้ว" with no button (facility-0082); off shift it shows "กะของคุณ: …" and "ลงเวลาได้ตั้งแต่ … ถึง …" from `ownShift` (facility-0077); the "วันนี้" row shows only recorded times (facility-0078). Types `OwnShift`, fixtures `dayShift` / `nightShift`. Frontend 388 tests. Next: Task 3 (My Work off shift).
```

```bash
git add docs/handoff.md frontend/src/shared/api/types.ts frontend/src/test/cleanerFixtures.ts \
  frontend/src/pages/check-in/checkInApi.ts frontend/src/pages/check-in/CheckInPage.tsx \
  frontend/src/pages/check-in/AttendanceTimeline.tsx frontend/src/pages/check-in/checkIn.css \
  frontend/src/pages/check-in/CheckInPage.test.tsx
git commit -m "feat(frontend): check-in sign checked on open, own shift off shift, no ถัดไป (facility-0077, 0078, 0082)"
```

---

### Task 3: My Work off shift shows the own shift, with no counts and no cards

**Files:**
- Modify: `frontend/src/pages/my-work/MyWorkPage.tsx`
- Test: `frontend/src/pages/my-work/MyWorkPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (Task 2): `OwnShift` and `MyWork.ownShift` from `shared/api/types.ts`; `nightShift` and `myWork(…, ownShift)` from `test/cleanerFixtures.ts`.
- Produces: nothing new for later tasks.

- [ ] **Step 1: Write the failing test**

In `frontend/src/pages/my-work/MyWorkPage.test.tsx`, add `nightShift` to the import from `'../../test/cleanerFixtures'`. Replace the test `'outside the attendance window the shift line says so (facility-0069)'` with:

```ts
  it('outside the own shift: the shift and when check-in opens, with no counts and no cards (facility-0077)', async () => {
    serveMyWork(myWork(outsideShift, [area([point({ status: 'Passed' })])], nightShift));
    await renderApp('/my-work');

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในกะของคุณ')).toBeInTheDocument();
    expect(screen.getByText('กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(screen.getByText('ลงเวลาเข้างานได้ตั้งแต่ 18:00')).toBeInTheDocument();
    // another shift's work is not this Cleaner's: no "ส่งแล้ว" count and no card
    expect(screen.queryByRole('list', { name: 'สรุปงาน' })).not.toBeInTheDocument();
    expect(screen.queryByText('ห้องน้ำชาย')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'อัปเดต' })).toBeEnabled();
  });
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `cd frontend && npx vitest run src/pages/my-work`
Expected: FAIL. `'กะดึก 19:00–07:00'` is not found, and the summary list is still there.

- [ ] **Step 3: Implement**

In `frontend/src/pages/my-work/MyWorkPage.tsx`:

1. Change the imports:

```tsx
import type { AttendanceState, MyWork, OwnShift, PointStatus, Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatClock, formatTime } from '../../shared/utils/thaiTime';
```

2. Replace the `MyWorkPage` function and `attendanceLine` with:

```tsx
export function MyWorkPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyWorkQuery(undefined, freshOnOpen);
  const shift = data?.attendance.shift;

  return (
    <main className="mywork-page">
      <header className="mywork-heading">
        <h1>งานของฉัน</h1>
        {data && shift && <p className="mywork-sub">{attendanceLine(shift, data.attendance)}</p>}
      </header>
      {isLoading && <p className="mywork-muted">กำลังโหลดงาน...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && (shift ? <WorkList work={data} /> : <OffShift ownShift={data.ownShift} />)}
      <footer className="mywork-footer">
        <span>ไม่มีแจ้งเตือนเด้งขึ้น เปิดหน้านี้หรือกด "อัปเดต" เพื่อดูงานล่าสุด</span>
        <button type="button" className="app-button" disabled={isFetching} onClick={() => void refetch()}>
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </button>
      </footer>
    </main>
  );
}

function attendanceLine(shift: Shift, attendance: AttendanceState): string {
  const shiftIn = attendance.events.find((e) => e.eventType === 'ShiftIn');
  const shiftOut = attendance.events.find((e) => e.eventType === 'ShiftOut');
  return [
    shiftLabel[shift],
    shiftIn ? `เข้างาน ${formatTime(shiftIn.occurredAt)}` : 'ยังไม่ได้ลงเวลาเข้างาน',
    shiftOut ? `เลิกงาน ${formatTime(shiftOut.occurredAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

/**
 * facility-0077: outside the own shift's attendance window the server answers with the shift on the clock, which is
 * someone else's work. So the page shows no counts and no cards, only the own shift and when its check-in opens.
 */
function OffShift({ ownShift }: { ownShift?: OwnShift }) {
  return (
    <div className="app-note app-note--info">
      <strong>ตอนนี้ไม่อยู่ในกะของคุณ</strong>
      {ownShift && <span>{shiftLabel[ownShift.shift]}</span>}
      {ownShift && <span>{`ลงเวลาเข้างานได้ตั้งแต่ ${formatClock(ownShift.opensAt)}`}</span>}
    </div>
  );
}
```

`WorkList` stays as it is.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/my-work && npx vitest run`
Expected: PASS (388 tests). Then `npm run lint && npx tsc -b`, clean.

- [ ] **Step 5: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 3 done: My Work off shift shows "ตอนนี้ไม่อยู่ในกะของคุณ", the own shift and "ลงเวลาเข้างานได้ตั้งแต่ …" from `ownShift`, with no counts and no cards (facility-0077); the server still sends the clock's points, the page hides them (plan decision 1). Frontend 388 tests. Next: Task 4 (scan page).
```

```bash
git add docs/handoff.md frontend/src/pages/my-work/MyWorkPage.tsx frontend/src/pages/my-work/MyWorkPage.test.tsx
git commit -m "feat(frontend): My Work off shift shows the own shift only (facility-0077)"
```

---

### Task 4: Scan page: the warning clears, the result status is one line

**Files:**
- Modify: `frontend/src/pages/scan/SubmitForm.tsx`
- Modify: `frontend/src/pages/scan/SubmitResult.tsx`
- Test: `frontend/src/pages/scan/ScanPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `isComplete(draft)` from `pages/scan/scanRules.ts` (unchanged). `.scan-status` in `scan.css` (already used by `PointHeading`).
- Produces: nothing new for later tasks.

- [ ] **Step 1: Write the failing tests**

In `frontend/src/pages/scan/ScanPage.test.tsx`, add inside the first `describe` block, after the test `'พบปัญหา needs a tag or a note before anything is sent'`:

```ts
  it('the warning shows only after ส่งงาน, goes once a tag or a note is given, and comes back when emptied (facility-0076)', async () => {
    fakeGps(nearSign);
    serve();
    const { user } = await renderApp(SIGN);
    const warning = 'เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ';

    await user.click(await screen.findByRole('radio', { name: 'พบปัญหา' }));
    expect(screen.queryByText(warning)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ส่งงาน' }));
    expect(screen.getByRole('alert')).toHaveTextContent(warning);

    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    expect(screen.queryByText(warning)).not.toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: 'ขยะล้น' }));
    expect(screen.getByRole('alert')).toHaveTextContent(warning);

    await user.type(screen.getByLabelText('หมายเหตุ'), 'ก๊อกรั่ว');
    expect(screen.queryByText(warning)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ส่งงาน' })).toBeEnabled();
  });

  it('the result shows the point status as one line under the heading, with no card (facility-0079)', async () => {
    fakeGps(nearSign);
    serve({ 'POST /api/scan-records': () => ({ status: 201, body: saved() }) });
    const { user } = await renderApp(SIGN);

    await user.click(await screen.findByRole('button', { name: 'ส่งงาน' }));
    await screen.findByRole('heading', { name: 'ส่งงานแล้ว 10:05' });

    expect(screen.getByText('สถานะของจุดนี้')).toHaveTextContent('สถานะของจุดนี้ รอตรวจ');
    expect(screen.queryByRole('heading', { name: 'สถานะของจุดนี้' })).not.toBeInTheDocument();
  });
```

- [ ] **Step 2: Run them to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/scan`
Expected: FAIL. The warning is still shown after ขยะล้น is ticked. "สถานะของจุดนี้" is still a heading.

- [ ] **Step 3: Implement the warning rule**

In `frontend/src/pages/scan/SubmitForm.tsx`, replace:

```tsx
  const [incomplete, setIncomplete] = useState(false);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!isComplete(draft)) {
      setIncomplete(true);
      return;
    }
    setIncomplete(false);
    onSend();
  };
```

with:

```tsx
  // facility-0076: the warning shows once ส่งงาน was pressed, and only while พบปัญหา still has no tag and no note
  const [tried, setTried] = useState(false);
  const incomplete = tried && !isComplete(draft);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!isComplete(draft)) {
      setTried(true);
      return;
    }
    onSend();
  };
```

The JSX that renders `{incomplete && (…)}` stays as it is.

- [ ] **Step 4: Implement the one-line status**

In `frontend/src/pages/scan/SubmitResult.tsx`:

1. In the `<header className="scan-heading">`, after the `<p className="scan-sub">…</p>` line, add:

```tsx
        <p className="scan-status">
          สถานะของจุดนี้ <StatusChip status={result.newPointStatus} />
        </p>
```

2. Delete the whole panel:

```tsx
      <section className="app-panel">
        <h2>สถานะของจุดนี้</h2>
        <p>
          <StatusChip status={result.newPointStatus} />
        </p>
      </section>
```

3. Change the component's comment to:

```tsx
/**
 * Wireframes M5 and M13: the server placed the submission (facility-0047 rule 3) and judged the distance
 * (facility-0037). The point's status is one line under the heading, not a card (facility-0079).
 */
```

- [ ] **Step 5: Run the tests to make sure they pass, and measure the bundle**

Run: `cd frontend && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS (390 tests), clean.

Run: `npm run build` and read the gzip size of the main `index-*.js` in the output.
Expected: ≤ 200 kB (about 158 kB). Delete `frontend/dist` afterwards (`rm -rf dist`).

- [ ] **Step 6: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 4 done: the scan form's "เลือกปัญหาที่พบ…" warning shows after ส่งงาน only while nothing is chosen, and goes as soon as a tag or a note is given (facility-0076); the result page shows "สถานะของจุดนี้ [chip]" as one line under the heading, no card (facility-0079). Frontend 390 tests; main JS <size> kB gzip. Next: Task 5 (D6 phone form).
```

(Write the measured size in place of `<size>`.)

```bash
git add docs/handoff.md frontend/src/pages/scan/SubmitForm.tsx frontend/src/pages/scan/SubmitResult.tsx \
  frontend/src/pages/scan/ScanPage.test.tsx
git commit -m "feat(frontend): scan warning clears when complete, status as one line (facility-0076, 0079)"
```

---

### Task 5: D6 "แก้เบอร์โทร" shows who is being edited and says the old number is not needed

**Files:**
- Modify: `frontend/src/pages/accounts/SecretForms.tsx`
- Modify: `frontend/src/pages/accounts/accounts.css`
- Test: `frontend/src/pages/accounts/AccountActions.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `accountRoleLabel` and `placeText(account)` from `pages/accounts/accountRules.ts` (unchanged). `.app-facts` from `shared/components/shared.css`.
- Produces: the phone field's label is now `เบอร์โทรที่คนนี้ใช้ตอนนี้` (was `เบอร์โทรใหม่`).

- [ ] **Step 1: Write the failing test**

In `frontend/src/pages/accounts/AccountActions.test.tsx`, in the test `'changes a phone: checked first, sent once, and never kept on the page (facility-0054)'`:

1. After `const form = screen.getByRole('form', { name: 'แก้เบอร์โทร · แม่บ้านทดสอบ' });` add:

```ts
    // facility-0081: who is being edited, and that the old number is not needed (it is stored as a hash)
    expect(within(form).getByText('650001')).toBeInTheDocument();
    expect(within(form).getByText('AR01 · กะเช้า')).toBeInTheDocument();
    expect(within(form).getByText(/^ไม่ต้องรู้เบอร์เดิม ใส่เบอร์ที่คนนี้ใช้ตอนนี้แล้วกดบันทึก/)).toBeInTheDocument();
```

2. Replace each of the three `getByLabelText('เบอร์โทรใหม่')` in that test with `getByLabelText('เบอร์โทรที่คนนี้ใช้ตอนนี้')`.

- [ ] **Step 2: Run it to make sure it fails**

Run: `cd frontend && npx vitest run src/pages/accounts/AccountActions.test.tsx`
Expected: FAIL. `'650001'` is not found inside the form.

- [ ] **Step 3: Implement**

In `frontend/src/pages/accounts/SecretForms.tsx`:

1. Change the rules import to:

```tsx
import {
  accountRoleLabel,
  MAX_PASSWORD,
  MIN_PASSWORD,
  normalizePhone,
  PASSWORD_PROBLEM,
  PHONE_PROBLEM,
  placeText,
} from './accountRules';
```

2. Change `PhoneForm`'s comment to:

```tsx
/**
 * facility-0054 rule 4: the Admin types a Cleaner's or Supervisor's new phone. It is sent once, then cleared; every
 * device of the person must log in again. The old number cannot be shown, because it is stored as a hash, and it is
 * not needed. So the form says so and shows who is being edited (facility-0081).
 */
```

3. In `PhoneForm`'s JSX, replace everything from `<h2 id={titleId}>` through the closing `</div>` of the `app-field` with:

```tsx
      <h2 id={titleId}>{`แก้เบอร์โทร · ${account.displayName}`}</h2>
      <dl className="app-facts accounts-who">
        <dt>ชื่อ</dt>
        <dd>{account.displayName}</dd>
        <dt>รหัสพนักงาน</dt>
        <dd>{account.loginName}</dd>
        <dt>บทบาท</dt>
        <dd>{accountRoleLabel[account.role]}</dd>
        <dt>ประจำที่</dt>
        <dd>{placeText(account)}</dd>
      </dl>
      <div className="app-field">
        <label htmlFor={`${titleId}-input`}>เบอร์โทรที่คนนี้ใช้ตอนนี้</label>
        <input
          id={`${titleId}-input`}
          type="tel"
          inputMode="numeric"
          className="app-input"
          autoComplete="off"
          maxLength={16}
          value={phone}
          disabled={isLoading}
          onChange={(event) => setPhone(event.target.value)}
        />
        <span className="app-field__help">
          ไม่ต้องรู้เบอร์เดิม ใส่เบอร์ที่คนนี้ใช้ตอนนี้แล้วกดบันทึก · เบอร์ใช้แทนรหัสผ่าน ระบบจึงเก็บแบบที่เปิดดูไม่ได้ ·
          บันทึกแล้วทุกเครื่องของคนนี้ต้องเข้าสู่ระบบใหม่ · ถ้าใส่เบอร์เดิม ระบบไม่เปลี่ยนอะไรและเครื่องเดิมยังใช้งานได้
        </span>
      </div>
```

In `frontend/src/pages/accounts/accounts.css`, add at the end:

```css
/* facility-0081: who the phone form is for, large enough to catch the wrong row */
.accounts-who dd {
  font-size: 18px;
  font-weight: 700;
}
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS (390 tests), clean.

- [ ] **Step 5: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 5 done: D6 "แก้เบอร์โทร" shows the name, employee ID, role and place in large type, the field reads "เบอร์โทรที่คนนี้ใช้ตอนนี้", and the help starts "ไม่ต้องรู้เบอร์เดิม …" (facility-0081). Frontend 390 tests. Next: Task 6 (D4 hour and minute boxes).
```

```bash
git add docs/handoff.md frontend/src/pages/accounts/SecretForms.tsx frontend/src/pages/accounts/accounts.css \
  frontend/src/pages/accounts/AccountActions.test.tsx
git commit -m "feat(frontend): phone form names the person, old number not needed (facility-0081)"
```

---

### Task 6: D4 round-window times as hour and minute boxes

**Files:**
- Modify: `frontend/src/pages/areas/pointRules.ts`
- Test: `frontend/src/pages/areas/pointRules.test.ts`
- Modify: `frontend/src/pages/areas/PointEditor.tsx`
- Modify: `frontend/src/pages/areas/areas.css`
- Test: `frontend/src/pages/areas/PointEditor.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: `sameWindow`, `sortWindows`, `WindowDraft` from `pointRules.ts` (unchanged).
- Produces:
  - `clockOf(hour: string, minute: string): string | null`, which replaces `isClock`.
  - Each shift's fieldset has four boxes named `เริ่ม ชั่วโมง`, `เริ่ม นาที`, `ถึง ชั่วโมง` and `ถึง นาที`. Task 10's tests use these names.

- [ ] **Step 1: Write the failing rule test**

In `frontend/src/pages/areas/pointRules.test.ts`, change the import to `import { clockOf, sortWindows } from './pointRules';`. Replace the test `'accepts only a 24-hour "HH:mm", as the server does'` with:

```ts
  it('clockOf joins an hour box and a minute box as the server\'s "HH:mm" (facility-0083)', () => {
    expect(clockOf('7', '00')).toBe('07:00');
    expect(clockOf('07', '30')).toBe('07:30');
    expect(clockOf('10', '')).toBe('10:00');
    expect(clockOf(' 23 ', ' 59 ')).toBe('23:59');
    expect(clockOf('0', '05')).toBe('00:05');
    expect(clockOf('24', '00')).toBeNull();
    expect(clockOf('', '00')).toBeNull();
    expect(clockOf('07', '5')).toBeNull();
    expect(clockOf('07', '60')).toBeNull();
    expect(clockOf('0700', '')).toBeNull();
    expect(clockOf('7a', '00')).toBeNull();
  });
```

- [ ] **Step 2: Write the failing page tests**

In `frontend/src/pages/areas/PointEditor.test.tsx`:

1. Add the import `import type { UserEvent } from '@testing-library/user-event';` and, below `serve`, this helper:

```ts
/** Types one Round Window into a shift's four boxes (facility-0083); an empty string leaves that box empty. */
async function typeWindow(user: UserEvent, shift: HTMLElement, from: [string, string], to: [string, string]) {
  const boxes: [string, string][] = [
    ['เริ่ม ชั่วโมง', from[0]],
    ['เริ่ม นาที', from[1]],
    ['ถึง ชั่วโมง', to[0]],
    ['ถึง นาที', to[1]],
  ];
  for (const [name, text] of boxes) {
    const box = within(shift).getByLabelText(name);
    await user.clear(box);
    if (text !== '') {
      await user.type(box, text);
    }
  }
}
```

2. In `'refuses to save while times are typed but not added, and sends nothing'`, replace the two `user.type(within(day).getByLabelText('เริ่ม'|'ถึง'), …)` lines with:

```ts
    await typeWindow(user, day, ['08', '00'], ['10', '00']);
```

3. In `'edits a point’s name and windows and sends the full list in shift order'`, replace the two lines that type `'23:00'` and `'01:00'` with:

```ts
    await typeWindow(user, night, ['23', '00'], ['01', '00']);
```

4. In `'adds a point to a day-only Area: no night shift, and a name is required'`, replace the two lines that type `'08:00'` and `'10:00{Enter}'` with:

```ts
    await typeWindow(user, day, ['08', '00'], ['10', '00']);
    await user.keyboard('{Enter}'); // Enter in a box adds the window
```

5. Replace the whole test `'checks the time format before adding a window, and refuses the same window twice'` with:

```ts
  it('hour and minute boxes: two hour digits move on, an empty minute is 00, a bad time is refused (facility-0083)', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');
    await user.click(await screen.findByRole('button', { name: 'แก้ไข AR01-01' }));
    const day = screen.getByRole('group', { name: 'กะเช้า' });

    await user.type(within(day).getByLabelText('เริ่ม ชั่วโมง'), '10');
    expect(within(day).getByLabelText('เริ่ม นาที')).toHaveFocus();
    await user.keyboard('3x0');
    expect(within(day).getByLabelText('เริ่ม นาที')).toHaveValue('30');
    await user.type(within(day).getByLabelText('ถึง ชั่วโมง'), '12');
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByText('10:30–12:00')).toBeInTheDocument();
    expect((within(day).getByLabelText('เริ่ม ชั่วโมง') as HTMLInputElement).value).toBe(''); // added, so the boxes empty

    await typeWindow(user, day, ['24', '00'], ['25', '00']);
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByRole('alert')).toHaveTextContent('ชั่วโมงใส่ 0–23 นาทีใส่ 00–59');

    await typeWindow(user, day, ['7', '00'], ['9', '']); // 07:00–09:00 is already there
    await user.click(within(day).getByRole('button', { name: 'เพิ่ม' }));
    expect(within(day).getByRole('alert')).toHaveTextContent('มีช่วงนี้แล้ว');
  });
```

6. In `'shows the server’s refusal and keeps the draft'`, replace the two lines that type `'18:00'` and `'20:00'` with:

```ts
    await typeWindow(user, day, ['18', '00'], ['20', '00']);
```

- [ ] **Step 3: Run them to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/areas`
Expected: FAIL. `clockOf` is not exported, and there is no box labelled `เริ่ม ชั่วโมง`.

- [ ] **Step 4: Implement the rule**

In `frontend/src/pages/areas/pointRules.ts`, replace the `isClock` function (and its comment) with:

```ts
/**
 * facility-0083: a time typed as an hour box and a minute box, as the server's two-digit 24-hour "HH:mm"
 * (RoundWindowRules.TryParseTime). The hour is 0–23 in one or two digits; the minute is empty (00) or two digits
 * 00–59. Anything else is null.
 */
export function clockOf(hour: string, minute: string): string | null {
  const h = hour.trim();
  const m = minute.trim() === '' ? '00' : minute.trim();
  if (!/^\d{1,2}$/.test(h) || !/^\d{2}$/.test(m) || Number(h) > 23 || Number(m) > 59) {
    return null;
  }
  return `${h.padStart(2, '0')}:${m}`;
}
```

- [ ] **Step 5: Implement the boxes**

In `frontend/src/pages/areas/PointEditor.tsx`:

1. Change the first import to `import { useRef, useState, type FormEvent, type KeyboardEvent } from 'react';`. Change the rules import to `import { clockOf, sameWindow, sortWindows, type WindowDraft } from './pointRules';`.

2. Replace:

```tsx
interface Typed {
  start: string;
  end: string;
}

const noTyped: Typed = { start: '', end: '' };
```

with:

```tsx
/** What is typed in one shift's four boxes but not yet added (facility-0083). */
interface Typed {
  startHour: string;
  startMinute: string;
  endHour: string;
  endMinute: string;
}

const noTyped: Typed = { startHour: '', startMinute: '', endHour: '', endMinute: '' };
```

3. In `save`, replace:

```tsx
    if (shifts.some((shift) => pending[shift].start.trim() !== '' || pending[shift].end.trim() !== '')) {
```

with:

```tsx
    if (shifts.some((shift) => Object.values(pending[shift]).some((box) => box.trim() !== ''))) {
```

4. Change the comment above `const [pending, setPending]` to `// What is typed in the boxes but not yet added, per shift: a save must not drop it silently`.

5. Replace the whole `ShiftWindows` function (from its doc comment `/** One shift's windows …` to the end of the file) with:

```tsx
/** One shift's windows (D4: "07:00–09:00 ×", then เริ่ม [ชม.]:[นาที] ถึง [ชม.]:[นาที] เพิ่ม). Enter in a box adds, not saves. */
function ShiftWindows({ shift, windows, disabled, typed, onTyped, onAdd, onRemove }: ShiftWindowsProps) {
  const [problem, setProblem] = useState<string | null>(null);
  const label = shiftShortLabel[shift];
  const mine = sortWindows(windows.filter((w) => w.shift === shift));

  const add = () => {
    const start = clockOf(typed.startHour, typed.startMinute);
    const end = clockOf(typed.endHour, typed.endMinute);
    if (!start || !end) {
      setProblem('ชั่วโมงใส่ 0–23 นาทีใส่ 00–59');
      return;
    }
    const draft = { shift, start, end };
    if (windows.some((w) => sameWindow(w, draft))) {
      setProblem('มีช่วงนี้แล้ว');
      return;
    }
    setProblem(null);
    onAdd(draft);
    onTyped(noTyped);
  };

  return (
    <fieldset className="areas-shift" disabled={disabled}>
      <legend>{label}</legend>
      <ul className="areas-shift__windows" aria-label={`ช่วงรอบ${label}`}>
        {mine.length === 0 && <li className="app-muted">ยังไม่มีช่วงรอบ</li>}
        {mine.map((w) => (
          <li key={`${w.start}-${w.end}`} className="areas-shift__chip">
            <span>{`${w.start}–${w.end}`}</span>
            <button
              type="button"
              className="app-link-button"
              aria-label={`ลบช่วง ${w.start}–${w.end}`}
              onClick={() => onRemove(w)}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
      <div className="areas-shift__add">
        <TimeBoxes
          label="เริ่ม"
          hour={typed.startHour}
          minute={typed.startMinute}
          hint={['07', '00']}
          onChange={(startHour, startMinute) => onTyped({ ...typed, startHour, startMinute })}
          onEnter={add}
        />
        <TimeBoxes
          label="ถึง"
          hour={typed.endHour}
          minute={typed.endMinute}
          hint={['09', '00']}
          onChange={(endHour, endMinute) => onTyped({ ...typed, endHour, endMinute })}
          onEnter={add}
        />
        <button type="button" className="app-button" onClick={add}>
          เพิ่ม
        </button>
      </div>
      {problem && (
        <p className="app-note app-note--error" role="alert">
          {problem}
        </p>
      )}
    </fieldset>
  );
}

interface TimeBoxesProps {
  label: string;
  hour: string;
  minute: string;
  /** Placeholders for the hour and the minute, e.g. ['07', '00']. */
  hint: [string, string];
  onChange: (hour: string, minute: string) => void;
  onEnter: () => void;
}

/**
 * facility-0083: one time as an hour box and a minute box, never one "HH:mm" string. Only digits are kept, and the
 * second hour digit moves the cursor to the minute box.
 */
function TimeBoxes({ label, hour, minute, hint, onChange, onEnter }: TimeBoxesProps) {
  const minuteBox = useRef<HTMLInputElement>(null);
  const enterAdds = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      onEnter();
    }
  };

  return (
    <span className="areas-time">
      <span className="areas-time__label">{label}</span>
      <input
        className="app-input areas-time__box"
        aria-label={`${label} ชั่วโมง`}
        inputMode="numeric"
        maxLength={2}
        placeholder={hint[0]}
        value={hour}
        onChange={(event) => {
          const digits = event.target.value.replace(/\D/g, '');
          onChange(digits, minute);
          if (digits.length === 2) {
            minuteBox.current?.focus();
          }
        }}
        onKeyDown={enterAdds}
      />
      <span aria-hidden="true">:</span>
      <input
        ref={minuteBox}
        className="app-input areas-time__box"
        aria-label={`${label} นาที`}
        inputMode="numeric"
        maxLength={2}
        placeholder={hint[1]}
        value={minute}
        onChange={(event) => onChange(hour, event.target.value.replace(/\D/g, ''))}
        onKeyDown={enterAdds}
      />
    </span>
  );
}
```

(`ShiftWindowsProps` stays as it is above `ShiftWindows`.)

In `frontend/src/pages/areas/areas.css`, replace:

```css
.areas-shift__add {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
}

.areas-shift__time {
  width: 90px;
}
```

with:

```css
.areas-shift__add {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}

/* facility-0083: [ชม.]:[นาที] */
.areas-time {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.areas-time__label {
  margin-right: 4px;
  font-size: 14px;
}

.areas-time__box {
  width: 3.5em;
  text-align: center;
}
```

- [ ] **Step 6: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/areas && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS (390 tests: the replaced rule test and page test keep the count), clean. `grep -rn "isClock\|areas-shift__time" src` finds nothing.

- [ ] **Step 7: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 6 done: D4 round-window times are typed as an hour box and a minute box ("เริ่ม ชั่วโมง" … "ถึง นาที"); the hour 0–23 in one or two digits, the minute empty (00) or 00–59, digits only, two hour digits move on to the minute, Enter adds; `clockOf` replaces `isClock` (facility-0083). Frontend 390 tests. Next: Task 7 (D3 covers ahead).
```

```bash
git add docs/handoff.md frontend/src/pages/areas/pointRules.ts frontend/src/pages/areas/pointRules.test.ts \
  frontend/src/pages/areas/PointEditor.tsx frontend/src/pages/areas/areas.css frontend/src/pages/areas/PointEditor.test.tsx
git commit -m "feat(frontend): round-window times as hour and minute boxes (facility-0083)"
```

---

### Task 7: D3 covers up to 7 days ahead, with a search

**Files:**
- Modify: `frontend/src/shared/utils/thaiTime.ts`
- Test: `frontend/src/shared/utils/thaiTime.test.ts`
- Modify: `frontend/src/pages/alerts/alertRules.ts`
- Test: `frontend/src/pages/alerts/alertRules.test.ts`
- Modify: `frontend/src/pages/alerts/AlertsPage.tsx`
- Modify: `frontend/src/pages/alerts/EmptyAreas.tsx`
- Modify: `frontend/src/pages/alerts/CoverForm.tsx`
- Modify: `frontend/src/pages/alerts/CoversThisShift.tsx`
- Modify: `frontend/src/test/liveFixtures.ts`
- Test: `frontend/src/pages/alerts/AlertCovers.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes:
  - `useGetAttendanceBoardQuery(args: BoardArgs | skipToken, options)` and the types `AttendanceBoard`, `BoardRow` from `shared/api/attendanceBoardApi.ts`
  - `addDays(date, days)` from `shared/utils/dateRange.ts`
  - `formatShiftDate` from `shared/utils/thaiTime.ts`
  - `shiftShortLabel` from `shared/utils/labels.ts`
- Produces:
  - `formatWeekday(date: string): string` in `shared/utils/thaiTime.ts` ("2026-10-16" → "ศ.").
  - In `pages/alerts/alertRules.ts`:
    - `interface CoverSlot { shiftDate: string; shift: Shift }`
    - `AHEAD_DAYS = 7`
    - `coverSlots(current: CoverSlot): CoverSlot[]`
    - `sameSlot(a, b): boolean`
    - `slotKey(slot): string`
    - `slotText(slot): string`
    - `matchesSearch(row: BoardRow, search: string): boolean`
    - `candidateText(candidate, ahead = false)`
  - `aheadBoard: AttendanceBoard` in `test/liveFixtures.ts` (Day shift of 2026-10-16, three Empty rows).
  - Visible text Task 9 must leave alone, because it is already in the approved words:
    - "วันและกะ", "ค้นหาชื่อแม่บ้านหรือพื้นที่"
    - "พื้นที่ของกะที่ยังไม่เริ่ม (n)", "… ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้พร้อมแม่บ้านประจำ"
    - "ไม่พบพื้นที่ที่ตรงกับที่ค้นหา", "ไม่มีพื้นที่ที่ทำกะนี้"
    - "แม่บ้านประจำ: …", "คนทำแทน<slot> (n)"

- [ ] **Step 1: Write the failing rule tests**

In `frontend/src/shared/utils/thaiTime.test.ts`, add `formatWeekday` to the import list (after `formatTime`) and add inside the `describe` block:

```ts
  it('formatWeekday names a shift date\'s Thai weekday, short', () => {
    expect(formatWeekday('2026-10-15')).toBe('พฤ.');
    expect(formatWeekday('2026-10-16')).toBe('ศ.');
    expect(formatWeekday('2026-10-17')).toBe('ส.');
    expect(formatWeekday('2026-10-18')).toBe('อา.');
    expect(formatWeekday('2026-10-19')).toBe('จ.');
  });
```

In `frontend/src/pages/alerts/alertRules.test.ts`, add `emptyRow` and `noCleanerRow` to the import from `'../../test/liveFixtures'`. Add `coverSlots`, `matchesSearch` and `slotText` to the import from `'./alertRules'`. Then add inside the `describe` block:

```ts
  it('coverSlots runs from the shift on the clock to the Night shift 7 days later (facility-0075)', () => {
    const fromDay = coverSlots({ shiftDate: '2026-10-15', shift: 'Day' });
    expect(fromDay).toHaveLength(16);
    expect(fromDay.slice(0, 3)).toEqual([
      { shiftDate: '2026-10-15', shift: 'Day' },
      { shiftDate: '2026-10-15', shift: 'Night' },
      { shiftDate: '2026-10-16', shift: 'Day' },
    ]);
    expect(fromDay[fromDay.length - 1]).toEqual({ shiftDate: '2026-10-22', shift: 'Night' });

    const fromNight = coverSlots({ shiftDate: '2026-10-31', shift: 'Night' });
    expect(fromNight).toHaveLength(15);
    expect(fromNight.slice(0, 2)).toEqual([
      { shiftDate: '2026-10-31', shift: 'Night' },
      { shiftDate: '2026-11-01', shift: 'Day' },
    ]);
    expect(fromNight[fromNight.length - 1]).toEqual({ shiftDate: '2026-11-07', shift: 'Night' });
  });

  it('slotText names the shift, the weekday and the date', () => {
    expect(slotText({ shiftDate: '2026-10-17', shift: 'Night' })).toBe('กะดึก ส. 17/10/2026');
  });

  it('matchesSearch finds a row by its regular Cleaner, Area code or Area name, ignoring case and spaces (facility-0084)', () => {
    expect(matchesSearch(emptyRow, '  ลา ')).toBe(true);
    expect(matchesSearch(emptyRow, 'ar12')).toBe(true);
    expect(matchesSearch(emptyRow, 'สิบสอง')).toBe(true);
    expect(matchesSearch(emptyRow, 'ทดสอบ')).toBe(false);
    expect(matchesSearch(noCleanerRow, '')).toBe(true);
    expect(matchesSearch(noCleanerRow, 'แม่บ้าน')).toBe(false);
  });
```

- [ ] **Step 2: Run them to make sure they fail**

Run: `cd frontend && npx vitest run src/shared/utils/thaiTime.test.ts src/pages/alerts/alertRules.test.ts`
Expected: FAIL. `formatWeekday`, `coverSlots`, `slotText` and `matchesSearch` are not exported.

- [ ] **Step 3: Implement the rules**

In `frontend/src/shared/utils/thaiTime.ts`, add after `formatShiftDate`:

```ts
const weekdays = ['อา.', 'จ.', 'อ.', 'พ.', 'พฤ.', 'ศ.', 'ส.'];

/** A shift date ("2026-10-16", already a Thai calendar date) as its short Thai weekday, "ศ." (D3's shift picker). */
export function formatWeekday(date: string): string {
  const [year, month, day] = date.split('-').map(Number);
  return weekdays[new Date(Date.UTC(year, month - 1, day)).getUTCDay()];
}
```

In `frontend/src/pages/alerts/alertRules.ts`:

1. Change the imports at the top to:

```ts
import type { Alert, AlertKind } from '../../shared/api/alertListApi';
import type { BoardRow } from '../../shared/api/attendanceBoardApi';
import type { Shift } from '../../shared/api/types';
import { addDays } from '../../shared/utils/dateRange';
import { eventLabel, shiftShortLabel } from '../../shared/utils/labels';
import { formatDate, formatShiftDate, formatTime, formatWeekday } from '../../shared/utils/thaiTime';
import type { CoverAssignment, CoverCandidate } from './alertsApi';
```

2. Replace `candidateText` with:

```ts
/**
 * One choice of "ให้ใครทำแทน": who, their own Area, their Shift-In, and whether they already scanned this Area's signs.
 * For a shift not started yet nobody has checked in or scanned, so only who and their Area are shown.
 */
export function candidateText(candidate: CoverCandidate, ahead = false): string {
  if (ahead) {
    return `${candidate.displayName} · ${candidate.areaCode}`;
  }
  return [
    candidate.displayName,
    candidate.areaCode,
    candidate.shiftInAt ? `เข้างานแล้ว ${formatTime(candidate.shiftInAt)}` : 'ยังไม่ลงเวลา',
    candidate.scannedThisAreaAt ? `สแกนป้าย Area นี้เมื่อ ${formatTime(candidate.scannedThisAreaAt)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}
```

3. Add at the end of the file:

```ts
/** A shift the cover box shows (facility-0075). */
export interface CoverSlot {
  shiftDate: string;
  shift: Shift;
}

/** facility-0075: covers are assigned up to 7 days ahead, so the night and the weekend never wait for an Admin. */
export const AHEAD_DAYS = 7;

/** From the shift on the clock to the Night shift 7 days later, in the order they come. */
export function coverSlots(current: CoverSlot): CoverSlot[] {
  const slots: CoverSlot[] = [];
  for (let day = 0; day <= AHEAD_DAYS; day += 1) {
    const shiftDate = addDays(current.shiftDate, day);
    for (const shift of ['Day', 'Night'] as const) {
      // On the first date a Night shift on the clock has already passed that date's Day shift
      if (day > 0 || current.shift === 'Day' || shift === 'Night') {
        slots.push({ shiftDate, shift });
      }
    }
  }
  return slots;
}

export function sameSlot(a: CoverSlot, b: CoverSlot): boolean {
  return a.shiftDate === b.shiftDate && a.shift === b.shift;
}

/** The picker's value, e.g. "2026-10-16:Night". */
export function slotKey(slot: CoverSlot): string {
  return `${slot.shiftDate}:${slot.shift}`;
}

/** "กะดึก ศ. 16/10/2026": the weekday matters, because the Admin thinks "Saturday", not a date. */
export function slotText(slot: CoverSlot): string {
  return `${shiftShortLabel[slot.shift]} ${formatWeekday(slot.shiftDate)} ${formatShiftDate(slot.shiftDate)}`;
}

/** facility-0084: the cover box's search, by the regular Cleaner's name or the Area's code or name. */
export function matchesSearch(row: BoardRow, search: string): boolean {
  const words = search.trim().toLowerCase();
  if (words === '') {
    return true;
  }
  return [row.areaCode, row.areaName, row.cleaner?.displayName ?? ''].some((text) => text.toLowerCase().includes(words));
}
```

- [ ] **Step 4: Run the rule tests to make sure they pass**

Run: `cd frontend && npx vitest run src/shared/utils/thaiTime.test.ts src/pages/alerts/alertRules.test.ts`
Expected: PASS.

- [ ] **Step 5: Add the fixture and write the failing page tests**

In `frontend/src/test/liveFixtures.ts`, add right after the `attendanceBoard` function:

```ts
/**
 * The Day shift of 16/10/2026, not started (facility-0084): nobody has checked in, so every Area that works it is
 * Empty, in the server's order (Areas with nobody first, then by code).
 */
export const aheadBoard: AttendanceBoard = attendanceBoard(
  [{ ...workingRow, state: 'Empty', events: [] }, noCleanerRow, emptyRow],
  { shiftDate: '2026-10-16', shift: 'Day' },
);
```

In `frontend/src/pages/alerts/AlertCovers.test.tsx`, add `aheadBoard`, `coveredRow` and `emptyRow` to the import from `'../../test/liveFixtures'`. Then add a new `describe` block at the end of the file:

```ts
describe('covers ahead on the alert page (D3, facility-0075, 0084)', () => {
  /** The board of now, or the board of 16/10 when the page asks for it. */
  function serveAhead(routes: FakeRoutes = {}, ahead = aheadBoard) {
    return serve({
      'GET /api/admin/attendance': (request) => ({
        status: 200,
        body: request.search === '?shiftDate=2026-10-16&shift=Day' ? ahead : attendanceBoard(),
      }),
      ...routes,
    });
  }

  it('the picker runs from this shift to the Night shift 7 days on, and comes back to this shift', async () => {
    serveAhead();
    const { user } = await renderApp('/admin/alerts');

    const picker = await screen.findByRole('combobox', { name: 'วันและกะ' });
    const options = within(picker).getAllByRole('option').map((option) => option.textContent);
    expect(options).toHaveLength(16);
    expect(options.slice(0, 3)).toEqual(['กะเช้า พฤ. 15/10/2026 (กะนี้)', 'กะดึก พฤ. 15/10/2026', 'กะเช้า ศ. 16/10/2026']);
    expect(options[15]).toBe('กะดึก พฤ. 22/10/2026');
    expect(picker).toHaveValue('2026-10-15:Day');

    await user.selectOptions(picker, 'กะเช้า ศ. 16/10/2026');
    expect(await screen.findByRole('region', { name: 'พื้นที่ของกะที่ยังไม่เริ่ม (3)' })).toBeInTheDocument();

    await user.selectOptions(screen.getByRole('combobox', { name: 'วันและกะ' }), 'กะเช้า พฤ. 15/10/2026 (กะนี้)');
    expect(await screen.findByRole('region', { name: 'Area ที่ยังไม่มีคน (2)' })).toBeInTheDocument();
  });

  it('a shift not started lists every Area with its regular Cleaner, and the cover is for that shift', async () => {
    const server = serveAhead({
      'POST /api/admin/cover-assignments': () => ({
        status: 201,
        body: {
          id: 80,
          userId: 25,
          personName: 'แม่บ้านที่อื่น',
          areaId: 12,
          areaCode: 'AR12',
          shiftDate: '2026-10-16',
          shift: 'Day',
          assignedAt: '2026-10-15T03:20:00Z',
        },
      }),
    });
    const { user } = await renderApp('/admin/alerts');
    await user.selectOptions(await screen.findByRole('combobox', { name: 'วันและกะ' }), 'กะเช้า ศ. 16/10/2026');

    const box = await screen.findByRole('region', { name: 'พื้นที่ของกะที่ยังไม่เริ่ม (3)' });
    expect(
      within(box).getByText('กะเช้า ศ. 16/10/2026 ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้พร้อมแม่บ้านประจำ'),
    ).toBeInTheDocument();
    expect(within(box).getByText('แม่บ้านประจำ: แม่บ้านทดสอบ')).toBeInTheDocument();
    expect(within(box).getByText('แม่บ้านประจำ: แม่บ้านลา')).toBeInTheDocument();
    expect(within(box).getByText('ไม่มีแม่บ้านประจำ')).toBeInTheDocument();

    await user.click(within(box).getByRole('button', { name: 'มอบหมายคนทำแทน AR12' }));
    const form = screen.getByRole('region', { name: 'มอบหมายคนทำแทน · AR12' });
    expect(within(form).getByText('แม่บ้านประจำ: แม่บ้านลา')).toBeInTheDocument();
    expect(within(form).getByText('มีผลเฉพาะกะเช้า 16/10/2026 · หมดสิทธิ์เองเมื่อจบกะ ไม่ต้องกดยกเลิก')).toBeInTheDocument();
    const who = await within(form).findByRole('combobox', { name: 'ให้ใครทำแทน' });
    expect(within(who).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'แม่บ้านที่อื่น · AR03',
      'แม่บ้านทดสอบ · AR01',
      'แม่บ้านมาสาย · AR08',
    ]);
    expect(server.calls.find((call) => call.path === '/api/admin/cover-assignments/candidates')?.search).toBe(
      '?areaId=12&shiftDate=2026-10-16&shift=Day',
    );

    await user.click(within(form).getByRole('button', { name: 'ยืนยันมอบหมาย' }));

    expect(await screen.findByText('มอบหมาย แม่บ้านที่อื่น ทำแทน AR12 แล้ว')).toBeInTheDocument();
    expect(posts(server).map((call) => call.body)).toEqual([
      { userId: 25, areaId: 12, shiftDate: '2026-10-16', shift: 'Day' },
    ]);
  });

  it('the search narrows the box by Cleaner, Area code or Area name', async () => {
    serveAhead();
    const { user } = await renderApp('/admin/alerts');
    await user.selectOptions(await screen.findByRole('combobox', { name: 'วันและกะ' }), 'กะเช้า ศ. 16/10/2026');
    const box = await screen.findByRole('region', { name: 'พื้นที่ของกะที่ยังไม่เริ่ม (3)' });
    const search = within(box).getByRole('searchbox', { name: 'ค้นหาชื่อแม่บ้านหรือพื้นที่' });
    const shown = () =>
      within(box)
        .getAllByRole('button', { name: /^มอบหมายคนทำแทน / })
        .map((button) => button.getAttribute('aria-label'));

    await user.type(search, 'ลา');
    expect(shown()).toEqual(['มอบหมายคนทำแทน AR12']);

    await user.clear(search);
    await user.type(search, 'ar09');
    expect(shown()).toEqual(['มอบหมายคนทำแทน AR09']);

    await user.clear(search);
    await user.type(search, 'ไม่มีใคร');
    expect(within(box).getByText('ไม่พบพื้นที่ที่ตรงกับที่ค้นหา')).toBeInTheDocument();
  });

  it('the covers box follows the chosen shift, and a cover ahead is cancelled with ยกเลิก', async () => {
    const ahead = attendanceBoard([{ ...emptyRow, state: 'Covered', covers: [{ ...coveredRow.covers[0], id: 81 }] }], {
      shiftDate: '2026-10-16',
      shift: 'Day',
    });
    const server = serveAhead({ 'DELETE /api/admin/cover-assignments/81': () => ({ status: 204 }) }, ahead);
    const { user } = await renderApp('/admin/alerts');
    expect(await screen.findByRole('region', { name: 'คนทำแทนกะนี้ (1)' })).toBeInTheDocument();

    await user.selectOptions(screen.getByRole('combobox', { name: 'วันและกะ' }), 'กะเช้า ศ. 16/10/2026');

    const covers = await screen.findByRole('region', { name: 'คนทำแทนกะเช้า ศ. 16/10/2026 (1)' });
    const cancel = within(covers).getByRole('button', { name: 'ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR12' });
    expect(cancel.textContent).toBe('ยกเลิก'); // not "ยกเลิกก่อนหมดกะ": the shift has not started
    await user.click(cancel);

    expect(await screen.findByText('ยกเลิกการทำแทนของ แม่บ้านที่อื่น ที่ AR12 แล้ว')).toBeInTheDocument();
    expect(server.calls.some((call) => call.method === 'DELETE' && call.path === '/api/admin/cover-assignments/81')).toBe(true);
  });
});
```

- [ ] **Step 6: Run them to make sure they fail**

Run: `cd frontend && npx vitest run src/pages/alerts`
Expected: FAIL. There is no combobox named `วันและกะ` yet. The tests that were already there still pass.

- [ ] **Step 7: Implement the box**

Replace `frontend/src/pages/alerts/EmptyAreas.tsx` with:

```tsx
import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { matchesSearch, sameSlot, slotKey, slotText, type CoverSlot } from './alertRules';

interface EmptyAreasProps {
  board?: AttendanceBoard;
  error?: unknown;
  /** The shifts the Admin may pick, the one on the clock first (facility-0075). */
  slots: CoverSlot[];
  /** The shift picked. */
  slot?: CoverSlot;
  /** A shift not started: every Area of it is listed with its regular Cleaner (facility-0084). */
  ahead: boolean;
  onSlot: (slot: CoverSlot) => void;
  /** The Area whose cover form is open. */
  chosen: number | null;
  onChoose: (areaId: number) => void;
  /** The cover form, shown under the list while an Area is chosen. */
  children?: ReactNode;
}

/**
 * Wireframe D3's first box. On the shift on the clock: the Areas with nobody, from the same board as the attendance
 * page (facility-0050). On a shift not started: every Area that works it, with its regular Cleaner, so the Admin can
 * cover a known absence before leaving (facility-0075, 0084). The search only narrows the rows on screen.
 */
export function EmptyAreas({ board, error, slots, slot, ahead, onSlot, chosen, onChoose, children }: EmptyAreasProps) {
  const [search, setSearch] = useState('');
  const rows = board ? board.rows.filter((row) => ahead || row.state === 'Empty') : [];
  const shown = rows.filter((row) => matchesSearch(row, search));
  const current = slots[0];

  return (
    <section className="alerts-section" aria-labelledby="alerts-empty">
      <h2 id="alerts-empty">{ahead ? `พื้นที่ของกะที่ยังไม่เริ่ม (${rows.length})` : `Area ที่ยังไม่มีคน (${rows.length})`}</h2>
      {ahead && board ? (
        <span className="app-muted">{`${slotText(board)} ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้พร้อมแม่บ้านประจำ`}</span>
      ) : (
        <span className="app-muted">
          ข้อมูลเดียวกับ <Link to="/admin/attendance">การเข้างาน</Link>
          {board ? ` · ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}` : ''}
        </span>
      )}
      <div className="alerts-filters">
        {current && slot && (
          <label className="alerts-type">
            วันและกะ
            <select
              className="app-input"
              value={slotKey(slot)}
              onChange={(event) => onSlot(slots.find((option) => slotKey(option) === event.target.value) ?? current)}
            >
              {slots.map((option) => (
                <option key={slotKey(option)} value={slotKey(option)}>
                  {sameSlot(option, current) ? `${slotText(option)} (กะนี้)` : slotText(option)}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="alerts-type">
          ค้นหาชื่อแม่บ้านหรือพื้นที่
          <input type="search" className="app-input" value={search} onChange={(event) => setSearch(event.target.value)} />
        </label>
      </div>
      {error != null && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && !ahead && rows.length === 0 && <p className="app-note app-note--info">ทุก Area มีคนแล้ว</p>}
      {board && ahead && rows.length === 0 && <p className="app-note app-note--info">ไม่มีพื้นที่ที่ทำกะนี้</p>}
      {rows.length > 0 && shown.length === 0 && <p className="app-note app-note--info">ไม่พบพื้นที่ที่ตรงกับที่ค้นหา</p>}
      <ul className="alerts-rows">
        {shown.map((row) => (
          <li key={row.areaId} className="alerts-row">
            <span>
              <strong>{`${row.areaCode} ${row.areaName}`}</strong>
              {` · ตึก ${row.buildingCode}`}
            </span>
            <span className="app-muted">{cleanerText(row, ahead)}</span>
            <button
              type="button"
              className="app-button"
              aria-label={`มอบหมายคนทำแทน ${row.areaCode}`}
              disabled={chosen === row.areaId}
              onClick={() => onChoose(row.areaId)}
            >
              {chosen === row.areaId ? 'กำลังมอบหมาย ›' : 'มอบหมายคนทำแทน'}
            </button>
          </li>
        ))}
      </ul>
      {children}
    </section>
  );
}

/** On the shift on the clock the regular Cleaner has not checked in; ahead, nobody has yet. */
function cleanerText(row: BoardRow, ahead: boolean): string {
  if (!row.cleaner) {
    return 'ไม่มีแม่บ้านประจำ';
  }
  return ahead ? `แม่บ้านประจำ: ${row.cleaner.displayName}` : `${row.cleaner.displayName} ยังไม่ลงเวลา`;
}
```

- [ ] **Step 8: Make the form and the covers box follow the pick**

In `frontend/src/pages/alerts/CoverForm.tsx`:

1. Add `ahead: boolean;` with the comment `/** A shift not started: nobody has checked in yet (facility-0084). */` to `CoverFormProps`, and `ahead` to the destructured props.
2. Replace:

```tsx
      <span>{row.cleaner ? `แม่บ้านประจำ: ${row.cleaner.displayName} · ยังไม่ลงเวลา` : 'Area นี้ไม่มีแม่บ้านประจำกะนี้'}</span>
```

with:

```tsx
      <span>
        {row.cleaner
          ? `แม่บ้านประจำ: ${row.cleaner.displayName}${ahead ? '' : ' · ยังไม่ลงเวลา'}`
          : 'Area นี้ไม่มีแม่บ้านประจำกะนี้'}
      </span>
```

3. Replace `{candidateText(candidate)}` with `{candidateText(candidate, ahead)}`.
4. Change the component's comment to:

```tsx
/**
 * Wireframe D3 "มอบหมายคนทำแทน" (facility-0041): the server's candidates, best first, for the shift on screen, which
 * may be up to 7 days ahead (facility-0075). The choice lives here only (facility-0024); one send at a time.
 */
```

In `frontend/src/pages/alerts/CoversThisShift.tsx`:

1. Add `import { slotText } from './alertRules';` to the imports.
2. Add `/** A shift not started (facility-0075). */ ahead: boolean;` to `CoversThisShiftProps`, and destructure it: `export function CoversThisShift({ board, ahead, onNotice }: CoversThisShiftProps) {`.
3. Replace the heading line with:

```tsx
      <h2 id="alerts-covers">{`${ahead ? `คนทำแทน${slotText(board)}` : 'คนทำแทนกะนี้'} (${covers.length})`}</h2>
```

4. Replace the button's text `ยกเลิกก่อนหมดกะ` with `{ahead ? 'ยกเลิก' : 'ยกเลิกก่อนหมดกะ'}`. Its `aria-label` stays.
5. Change the component's comment to:

```tsx
/**
 * Wireframe D3 "คนทำแทนกะนี้": every Area cover of the shift on screen, from its board: the shift on the clock, or one
 * picked up to 7 days ahead (facility-0075, 0084). Each ends with the shift by itself, or is cancelled in one press.
 * Supervisors' building covers have no list endpoint (plan 11 decision 10).
 */
```

- [ ] **Step 9: Wire the pick into the page**

Replace `frontend/src/pages/alerts/AlertsPage.tsx` with the following. The alert list part is unchanged.

```tsx
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useState } from 'react';
import { useGetAlertsQuery } from '../../shared/api/alertListApi';
import { useGetAttendanceBoardQuery } from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AlertItem, type Notice } from './AlertItem';
import {
  alertGroupLabel,
  coverSlots,
  filterAlerts,
  sameSlot,
  type AlertGroup,
  type CoverSlot,
  type ReviewTab,
} from './alertRules';
import { CoverForm } from './CoverForm';
import { CoversThisShift } from './CoversThisShift';
import { EmptyAreas } from './EmptyAreas';
import './alerts.css';

const GROUPS: readonly AlertGroup[] = ['all', 'blocked', 'far', 'missing'];

/**
 * Wireframe D3: the Areas with nobody this shift and their covers (facility-0041, 0048), and the alerts of yesterday and
 * today for the Admin to talk through (facility-0035, 0037, 0051). A shift up to 7 days ahead can be picked, so covers
 * for the night and the weekend are given before the Admin leaves (facility-0075, 0084). The boards follow the hub like
 * the attendance page; no hub event carries alerts, so they are asked on every open, every 30 s and after each action.
 */
export function AlertsPage() {
  const alerts = useGetAlertsQuery(undefined, livePoll);
  const nowBoard = useGetAttendanceBoardQuery({}, livePoll);
  const [picked, setPicked] = useState<CoverSlot | null>(null);
  const current = nowBoard.data ? { shiftDate: nowBoard.data.shiftDate, shift: nowBoard.data.shift } : undefined;
  const slots = current ? coverSlots(current) : [];
  // A pick the clock has reached or passed shows the board of now again
  const aheadSlot =
    picked && current && !sameSlot(picked, current) && slots.some((slot) => sameSlot(slot, picked)) ? picked : undefined;
  const aheadBoard = useGetAttendanceBoardQuery(aheadSlot ?? skipToken, livePoll);
  const ahead = aheadSlot !== undefined;
  const board = ahead ? aheadBoard : nowBoard;
  // currentData: never the board of the shift picked before while the new one loads
  const boardNow = board.currentData;
  const [tab, setTab] = useState<ReviewTab>('open');
  const [group, setGroup] = useState<AlertGroup>('all');
  const [notice, setNotice] = useState<Notice | null>(null);
  // The Area and the slot the cover form was opened for: a board of another shift closes it
  const [cover, setCover] = useState<{ areaId: number; shiftDate: string; shift: Shift } | null>(null);
  const list = alerts.data ?? [];
  const openCount = list.filter((alert) => !alert.reviewed).length;
  const shown = filterAlerts(list, tab, group);
  // The form closes by itself once the Area has someone (a cover made here or elsewhere) or another shift is shown
  if (cover && boardNow && (boardNow.shiftDate !== cover.shiftDate || boardNow.shift !== cover.shift)) {
    setCover(null);
  }
  const coverRow = cover
    ? boardNow?.rows.find((row) => row.areaId === cover.areaId && (ahead || row.state === 'Empty'))
    : undefined;
  // Closes only the form the send belongs to: the Admin may have opened another since
  const closeCover = (areaId: number, shiftDate: string, shift: Shift) =>
    setCover((open) => (open && open.areaId === areaId && open.shiftDate === shiftDate && open.shift === shift ? null : open));
  const tabs: { tab: ReviewTab; label: string }[] = [
    { tab: 'open', label: `ยังไม่ได้คุย (${openCount})` },
    { tab: 'reviewed', label: `คุยแล้ว (${list.length - openCount})` },
    { tab: 'all', label: 'ทั้งหมด' },
  ];

  return (
    <main className="alerts-page">
      <header>
        <span className="app-muted">เมื่อวานและวันนี้ · Admin เท่านั้น</span>
        <h1>รายการที่ขึ้นเตือน</h1>
      </header>
      {notice && (
        <p
          className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
          role={notice.kind === 'ok' ? 'status' : 'alert'}
        >
          {notice.text}
        </p>
      )}
      <EmptyAreas
        board={boardNow}
        error={board.error}
        slots={slots}
        slot={aheadSlot ?? current}
        ahead={ahead}
        onSlot={(slot) => {
          setNotice(null);
          setPicked(current && sameSlot(slot, current) ? null : slot);
        }}
        chosen={coverRow ? coverRow.areaId : null}
        onChoose={(areaId) => {
          setNotice(null);
          if (boardNow) {
            setCover({ areaId, shiftDate: boardNow.shiftDate, shift: boardNow.shift });
          }
        }}
      >
        {boardNow && coverRow && (
          <CoverForm
            key={`${boardNow.shiftDate}:${boardNow.shift}:${coverRow.areaId}`}
            board={boardNow}
            row={coverRow}
            ahead={ahead}
            onDone={(text) => {
              setNotice({ kind: 'ok', text });
              closeCover(coverRow.areaId, boardNow.shiftDate, boardNow.shift);
            }}
            onCancel={() => closeCover(coverRow.areaId, boardNow.shiftDate, boardNow.shift)}
          />
        )}
      </EmptyAreas>
      <section className="alerts-section" aria-labelledby="alerts-list">
        <h2 id="alerts-list">เตือนจากการสแกน</h2>
        <div className="alerts-filters">
          <div className="alerts-tabs" role="group" aria-label="การคุย">
            {tabs.map((item) => (
              <button
                key={item.tab}
                type="button"
                className="alerts-tab"
                aria-pressed={tab === item.tab}
                onClick={() => setTab(item.tab)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <label className="alerts-type">
            ประเภท
            <select className="app-input" value={group} onChange={(event) => setGroup(event.target.value as AlertGroup)}>
              {GROUPS.map((value) => (
                <option key={value} value={value}>
                  {alertGroupLabel[value]}
                </option>
              ))}
            </select>
          </label>
        </div>
        {alerts.isLoading && <p className="app-muted">กำลังโหลด...</p>}
        {alerts.error && (
          <p className="app-note app-note--error" role="alert">
            {getErrorMessage(alerts.error)}
          </p>
        )}
        {alerts.data && shown.length === 0 && <p className="app-note app-note--info">ไม่มีรายการ</p>}
        <ul className="alerts-list">
          {shown.map((alert) => (
            // The same record id can come with two kinds (a far Shift-In and its missing Shift-Out)
            <AlertItem key={`${alert.kind}-${alert.recordId}`} alert={alert} onNotice={setNotice} />
          ))}
        </ul>
        <p className="app-muted">
          ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติของ Admin
        </p>
      </section>
      {boardNow && <CoversThisShift board={boardNow} ahead={ahead} onNotice={setNotice} />}
    </main>
  );
}
```

`skipToken` comes from `@reduxjs/toolkit/query/react`, the same entry `baseApi.ts` imports `createApi` from.

- [ ] **Step 10: Run the tests to make sure they pass**

Run: `cd frontend && npx vitest run src/pages/alerts && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS, clean. The full suite is 390 + 1 (thaiTime) + 3 (alertRules) + 4 (AlertCovers) = 398 tests. The tests already in `AlertCovers.test.tsx` and `AlertsPage.test.tsx` pass unchanged: on the shift on the clock the box works as before.

- [ ] **Step 11: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 7 done: D3 "วันและกะ" picks the shift on the clock ("(กะนี้)") to the Night shift 7 days on, with the Thai weekday; a shift not started shows every Area of it with "แม่บ้านประจำ: …" (the board's `shiftDate` and `shift`, no server change); a search by Cleaner, Area code or name; the cover form, its candidates and the covers box follow the pick (facility-0075, 0084). `formatWeekday`, `coverSlots`, `slotText`, `matchesSearch`. Frontend 398 tests. Next: Task 8 (words on the phone pages).
```

```bash
git add docs/handoff.md frontend/src/shared/utils/thaiTime.ts frontend/src/shared/utils/thaiTime.test.ts \
  frontend/src/pages/alerts/alertRules.ts frontend/src/pages/alerts/alertRules.test.ts \
  frontend/src/pages/alerts/AlertsPage.tsx frontend/src/pages/alerts/EmptyAreas.tsx \
  frontend/src/pages/alerts/CoverForm.tsx frontend/src/pages/alerts/CoversThisShift.tsx \
  frontend/src/test/liveFixtures.ts frontend/src/pages/alerts/AlertCovers.test.tsx
git commit -m "feat(frontend): D3 covers up to 7 days ahead with a search (facility-0075, 0084)"
```

---

### How Tasks 8–11 work (read once)

Each word task has a table: file, the old text, the new text. Each old text is a fragment copied from the file. It appears once in that file unless the row says "every". Replace it exactly; nothing else in the line changes. Each task follows the same steps:

1. **Tests first.**
   - Run the task's grep over the test files.
   - Every expectation that matches an old fragment from the table changes to the new fragment.
   - Test titles (`it('…')`) may keep the old words.
   - Fixture *names* such as `'Area ทดลอง'` are data and stay.
2. **Run the tests to make sure they fail** on exactly those expectations.
3. **Apply the table** to the source files.
4. **Run the whole suite.**
   - A test that still fails because it expects an old fragment that this task's table changes is fixed in the test, by the same table. Never change what a test checks, only the words.
   - Run the task's gate grep. Each line it prints may hold an old word only inside a code name (`ownArea`, `areaName`, `AdminNav`), never in text a person sees.
5. **Handoff entry and commit.**

---

### Task 8: Words on the phone pages, the login pages and the shared pieces

**Files:**
- Modify:
  - `frontend/src/shared/utils/labels.ts`
  - `frontend/src/shared/auth/roles.ts`
  - `frontend/src/shared/components/BlockedScanScreen.tsx`
  - `frontend/src/shared/components/LocationHelp.tsx`
  - `frontend/src/pages/check-in/CheckInPage.tsx`
  - `frontend/src/pages/check-in/AttendanceTimeline.tsx`
  - `frontend/src/pages/scan/SubmitResult.tsx`
  - `frontend/src/pages/scan/SendFailed.tsx`
  - `frontend/src/pages/scan/ScanPage.tsx`
  - `frontend/src/pages/scan/OffDuty.tsx`
  - `frontend/src/pages/my-work/MyWorkPage.tsx`
  - `frontend/src/pages/inspect/InspectResult.tsx`
  - `frontend/src/pages/inspect/InspectForm.tsx`
  - `frontend/src/pages/inspect/inspectRules.ts`
  - `frontend/src/pages/inspect/DistanceLine.tsx`
  - `frontend/src/pages/inspect/InspectHeading.tsx`
  - `frontend/src/pages/inspect/InspectPage.tsx`
  - `frontend/src/pages/my-building/MyBuildingPage.tsx`
  - `frontend/src/pages/auth/LoginPage.tsx`
  - `frontend/src/pages/auth/AdminLoginPage.tsx`
  - `frontend/src/test/cleanerFixtures.ts`: `otherAreaBlocked.message` mirrors the server's new text from Task 11
- Test: the test files the grep in Step 1 finds, at least:
  - `CheckInPage.test.tsx`, `ScanPage.test.tsx`, `MyWorkPage.test.tsx`, `InspectPage.test.tsx`, `MyBuildingPage.test.tsx`, `LoginPage.test.tsx`
  - `router.test.tsx`
  - any test that shows the status chip `เลยรอบ` (the Dashboard's)
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Tasks 2–4's pages.
- Produces:
  - `pointStatusLabel.Overdue` is `'เลยเวลา'`.
  - `roleLabel.admin` is `'แอดมิน'`.
  - `otherAreaBlocked.message` is `'ป้ายนี้ไม่ใช่พื้นที่ของคุณ'`.

- [ ] **Step 1: Change the test expectations**

Run: `cd frontend && grep -rnE "Area ของ|ติดธง|เลยรอบ|นอกรอบ|Location|ข้อบกพร่อง|แจ้ง Admin|Admin เห็น|Admin จะ|Admin มอบหมาย|Admin เข้า|หน้า Admin|ไม่ใช่ Area|Area ·|Area ก่อน|ได้รับ Area|เป็นAdmin|สำหรับAdmin|Area ตัวเอง|Admin อีก|Admin เข้าสู่ระบบ" --include='*.test.ts' --include='*.test.tsx' src`

Change each expectation by the table in Step 3. These are the ones known at planning time:

| Test file | Old | New |
|---|---|---|
| `check-in/CheckInPage.test.tsx` | `'Area ของคุณ: Area ทดลอง · 1 จุด'` | `'พื้นที่ของคุณ: Area ทดลอง · 1 จุด'` |
| | `'บันทึกเวลาเข้างานแล้ว 06:52 แต่อยู่ไกลจากป้าย · ติดธง'` | `'บันทึกเวลาเข้างานแล้ว 06:52 แต่อยู่ไกลป้าย'` |
| | `'เข้างาน 06:52 ติดธง'` | `'เข้างาน 06:52 ไกลป้าย'` |
| | `'เปิด Location ก่อนลงเวลา'` | `'เปิดตำแหน่งก่อนลงเวลา'` |
| | `'ป้ายนี้ไม่ใช่ Area ของคุณ'` | `'ป้ายนี้ไม่ใช่พื้นที่ของคุณ'` |
| `scan/ScanPage.test.tsx` | `'บันทึกเป็นงานนอกรอบ 13:20'` | `'ส่งเพิ่มแล้ว (ไม่นับรอบ) 13:20'` |
| | `'ติดธง: ห่างป้าย 85 ม. Admin จะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้ง Admin ได้เลย'` | `'ไกลป้าย: ห่างป้าย 85 ม. แอดมินจะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้งแอดมินได้เลย'` |
| | `'เปิด Location ก่อนส่งงาน'` | `'เปิดตำแหน่งก่อนส่งงาน'` |
| | `'หน้านี้สำหรับแม่บ้าน หรือ หัวหน้างาน บัญชีนี้เป็นAdmin'` | `'หน้านี้สำหรับแม่บ้าน หรือ หัวหน้างาน บัญชีนี้เป็นแอดมิน'` |
| | `'ป้ายนี้ไม่ใช่ Area ของคุณ'` | `'ป้ายนี้ไม่ใช่พื้นที่ของคุณ'` |
| | `'Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณทำแทน Area ถัดไป เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ'` | `'แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณทำแทน Area ถัดไป เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ'` |
| `my-work/MyWorkPage.test.tsx` | `'Area ทดลอง (Area ของฉัน)'` (every) | `'Area ทดลอง (พื้นที่ของฉัน)'` |
| | `'1 เลยรอบ'` | `'1 เลยเวลา'` |
| | `'ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับ Area หรือ Area ยังไม่มีจุด แจ้ง Admin'` | `'ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับพื้นที่ หรือพื้นที่ยังไม่มีจุด แจ้งแอดมิน'` |
| `inspect/InspectPage.test.tsx` | `'ข้อบกพร่อง'` as a label (every) | `'สิ่งที่ต้องแก้'` |
| | `'ใส่ข้อบกพร่องก่อนบันทึก'` | `'ใส่สิ่งที่ต้องแก้ก่อนบันทึก'` |
| | `'แม่บ้านทดสอบเห็นข้อบกพร่องนี้ทันทีในหน้างานของฉัน …'` | `'แม่บ้านทดสอบเห็นสิ่งที่ต้องแก้นี้ทันทีในหน้างานของฉัน …'` (rest unchanged) |
| | `'ห่างป้าย 80 ม. · ±40 ม. · นอกรัศมี ผลตรวจจากตรงนี้จะติดธงให้ Admin เห็น'` | `'ห่างป้าย 80 ม. · ±40 ม. · นอกรัศมี ผลตรวจจากตรงนี้จะขึ้นว่าไกลป้ายให้แอดมินเห็น'` |
| | `'ติดธง: ห่างป้าย 80 ม. Admin จะเห็นรายการนี้'` | `'ไกลป้าย: ห่างป้าย 80 ม. แอดมินจะเห็นรายการนี้'` |
| | `'ตรวจแทนตึก A ในกะนี้ (Admin มอบหมาย)'` | `'ตรวจแทนตึก A ในกะนี้ (แอดมินมอบหมาย)'` |
| | `'เปิด Location ก่อนตรวจงาน'`, `'เปิด Location ก่อนบันทึกผลตรวจ'` (every) | `'เปิดตำแหน่งก่อนตรวจงาน'`, `'เปิดตำแหน่งก่อนบันทึกผลตรวจ'` |
| | `'… ยังไม่ได้ตรวจงาน แจ้ง Admin'` | `'… ยังไม่ได้ตรวจงาน แจ้งแอดมิน'` |
| | `'Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณตรวจแทนตึก B ในกะนี้ เมื่อ Admin กดมอบหมาย …'` | `'แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณตรวจแทนตึก B ในกะนี้ เมื่อแอดมินกดมอบหมาย …'` |
| `my-building/MyBuildingPage.test.tsx` | `'2 Area · 4 จุด'` | `'2 พื้นที่ · 4 จุด'` |
| | `'บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้ง Admin'` | `'บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้งแอดมิน'` |
| `auth/LoginPage.test.tsx` | link name `'หน้า Admin'` | `'หน้าแอดมิน'` |
| `router.test.tsx` | `'หน้านี้สำหรับAdmin บัญชีนี้เป็นแม่บ้าน'` | `'หน้านี้สำหรับแอดมิน บัญชีนี้เป็นแม่บ้าน'` |

Also: `AdminLoginPage`'s heading `'Admin เข้าสู่ระบบ'` → `'แอดมินเข้าสู่ระบบ'`, wherever a test names it.

In `frontend/src/test/cleanerFixtures.ts`, change `message: 'ป้ายนี้ไม่ใช่ Area ของคุณ',` to `message: 'ป้ายนี้ไม่ใช่พื้นที่ของคุณ',`. It is the server's text, which Task 11 changes. `areaName: 'Area ถัดไป'` is data and stays.

- [ ] **Step 2: Run them to make sure they fail**

Run: `cd frontend && npx vitest run`
Expected: FAIL, only on the expectations changed in Step 1. The blocked-scan headings already pass, because they show the fixture's message.

- [ ] **Step 3: Change the source text**

| File | Old | New |
|---|---|---|
| `shared/utils/labels.ts` | `Overdue: 'เลยรอบ',` | `Overdue: 'เลยเวลา',` |
| `shared/auth/roles.ts` | `admin: 'Admin',` | `admin: 'แอดมิน',` |
| `shared/components/BlockedScanScreen.tsx` | `<dt>Area ของคุณ</dt>` | `<dt>พื้นที่ของคุณ</dt>` |
| | `ลงเวลาที่ป้ายของ Area ตัวเองเท่านั้น วันที่ทำแทน Area อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ Admin เห็นการสแกนครั้งนี้แล้ว` | `ลงเวลาที่ป้ายของพื้นที่ตัวเองเท่านั้น วันที่ทำแทนพื้นที่อื่นก็ลงเวลาที่ป้ายของตัวเองตามปกติ แอดมินเห็นการสแกนครั้งนี้แล้ว` |
| | `` `Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณทำแทน ${blocked.areaName} เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ` `` | `` `แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณทำแทน ${blocked.areaName} เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ` `` |
| | `` `Admin เห็นการสแกนครั้งนี้แล้ว ให้แจ้ง Admin ว่าคุณตรวจแทนตึก ${blocked.buildingCode} ในกะนี้ เมื่อ Admin กดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ` `` | `` `แอดมินเห็นการสแกนครั้งนี้แล้ว ให้แจ้งแอดมินว่าคุณตรวจแทนตึก ${blocked.buildingCode} ในกะนี้ เมื่อแอดมินกดมอบหมาย สแกนป้ายนี้ใหม่ได้ทันทีจนหมดกะ` `` |
| `shared/components/LocationHelp.tsx` | `` ยังไม่ได้${action} แจ้ง Admin` `` | `` ยังไม่ได้${action} แจ้งแอดมิน` `` |
| | `` `เปิด Location ก่อน${action}` `` | `` `เปิดตำแหน่งก่อน${action}` `` |
| `pages/check-in/CheckInPage.tsx` | `` `Area ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด` `` | `` `พื้นที่ของคุณ: ${ownArea.areaName} · ${ownArea.points.length} จุด` `` |
| | `` แต่อยู่ไกลจากป้าย · ติดธง` `` | `` แต่อยู่ไกลป้าย` `` |
| | `<span>Admin จะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้ง Admin ได้เลย</span>` | `<span>แอดมินจะเห็นรายการนี้ ถ้าคุณยืนอยู่หน้าป้ายจริง แจ้งแอดมินได้เลย</span>` |
| `pages/check-in/AttendanceTimeline.tsx` | `<span className="checkin-flag">ติดธง</span>` | `<span className="checkin-flag">ไกลป้าย</span>` |
| `pages/scan/SubmitResult.tsx` | `OffRound: 'บันทึกเป็นงานนอกรอบ',` | `OffRound: 'ส่งเพิ่มแล้ว (ไม่นับรอบ)',` |
| | `` `ติดธง: ห่างป้าย ${result.distanceM} ม. Admin จะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้ง Admin ได้เลย` `` | `` `ไกลป้าย: ห่างป้าย ${result.distanceM} ม. แอดมินจะเห็นรายการนี้ ถ้าคุณอยู่ที่จุดนี้จริง แจ้งแอดมินได้เลย` `` |
| `pages/scan/SendFailed.tsx` | `การส่งงานอาจติดธง` | `การส่งงานจะขึ้นว่าไกลป้าย` |
| `pages/scan/ScanPage.tsx` | `detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"` | `detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้งแอดมิน"` |
| `pages/scan/OffDuty.tsx` | `'ต้องสแกนป้ายลงเวลาของ Area ก่อน แล้วค่อยสแกนจุดนี้'` | `'ต้องสแกนป้ายลงเวลาของพื้นที่ก่อน แล้วค่อยสแกนจุดนี้'` |
| | `แจ้ง Admin ให้เพิ่มเวลาเข้างานให้ Admin ต้องใส่เหตุผลทุกครั้ง` | `แจ้งแอดมินให้เพิ่มเวลาเข้างานให้ แอดมินต้องใส่เหตุผลทุกครั้ง` |
| `pages/my-work/MyWorkPage.tsx` | `ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับ Area หรือ Area ยังไม่มีจุด แจ้ง Admin` | `ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับพื้นที่ หรือพื้นที่ยังไม่มีจุด แจ้งแอดมิน` |
| | `<strong>{count('Overdue')}</strong> เลยรอบ` | `<strong>{count('Overdue')}</strong> เลยเวลา` |
| | `` `${area.areaName} (Area ของฉัน)` `` | `` `${area.areaName} (พื้นที่ของฉัน)` `` |
| `pages/inspect/InspectResult.tsx` | `เห็นข้อบกพร่องนี้ทันทีในหน้างานของฉัน` | `เห็นสิ่งที่ต้องแก้นี้ทันทีในหน้างานของฉัน` |
| | `` `ติดธง: ห่างป้าย ${result.distanceM} ม. Admin จะเห็นรายการนี้` `` | `` `ไกลป้าย: ห่างป้าย ${result.distanceM} ม. แอดมินจะเห็นรายการนี้` `` |
| `pages/inspect/InspectForm.tsx` | `<label htmlFor="inspect-defect">ข้อบกพร่อง</label>` | `<label htmlFor="inspect-defect">สิ่งที่ต้องแก้</label>` |
| | `ไม่สะอาด = แม่บ้านเห็นข้อบกพร่องทันที` | `ไม่สะอาด = แม่บ้านเห็นสิ่งที่ต้องแก้ทันที` |
| `pages/inspect/inspectRules.ts` | `'ใส่ข้อบกพร่องก่อนบันทึก'` | `'ใส่สิ่งที่ต้องแก้ก่อนบันทึก'` |
| `pages/inspect/DistanceLine.tsx` | `นอกรัศมี ผลตรวจจากตรงนี้จะติดธงให้ Admin เห็น` | `นอกรัศมี ผลตรวจจากตรงนี้จะขึ้นว่าไกลป้ายให้แอดมินเห็น` |
| `pages/inspect/InspectHeading.tsx` | `(Admin มอบหมาย)` | `(แอดมินมอบหมาย)` |
| `pages/inspect/InspectPage.tsx` | `detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้ง Admin"` | `detail="ลองสแกนป้ายอีกครั้ง ถ้ายังเปิดไม่ได้ แจ้งแอดมิน"` |
| `pages/my-building/MyBuildingPage.tsx` | `` `${areas.size} Area · ${points.length} จุด` `` | `` `${areas.size} พื้นที่ · ${points.length} จุด` `` |
| | `บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้ง Admin` | `บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้งแอดมิน` |
| `pages/auth/LoginPage.tsx` | `เบอร์ที่แจ้ง Admin ไว้ตอนสร้างบัญชี` | `เบอร์ที่แจ้งแอดมินไว้ตอนสร้างบัญชี` |
| | `เปลี่ยนเบอร์โทร: แจ้ง Admin ให้แก้` | `เปลี่ยนเบอร์โทร: แจ้งแอดมินให้แก้` |
| | `Admin เข้าด้วยชื่อผู้ใช้และรหัสผ่าน ·` | `แอดมินเข้าด้วยชื่อผู้ใช้และรหัสผ่าน ·` |
| | `หน้า Admin` (the link text) | `หน้าแอดมิน` |
| `pages/auth/AdminLoginPage.tsx` | `<h1>Admin เข้าสู่ระบบ</h1>` | `<h1>แอดมินเข้าสู่ระบบ</h1>` |
| | `ลืมรหัสผ่าน: ให้ Admin อีกคนรีเซ็ต` | `ลืมรหัสผ่าน: ให้แอดมินอีกคนรีเซ็ต` |

- [ ] **Step 4: Run the tests and the gate**

Run: `cd frontend && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS (398 tests), clean. A test that still fails on `เลยรอบ` (the status chip on another page) changes to `เลยเวลา`.

Gate: `cd frontend/src && grep -rnE "Area|Admin|GPS|Location|ติดธง|เลยรอบ|นอกรอบ|ข้อบกพร่อง" shared pages/check-in pages/scan pages/my-work pages/inspect pages/my-building pages/auth --include='*.ts' --include='*.tsx' | grep -v '\.test\.' | grep -vE ':[0-9]+:\s*(//|\*|/\*\*)' | grep -E '[ก-๙]'`
Expected: each printed line has the old word only inside a code name. For example, `ownArea` in the `พื้นที่ของคุณ` line passes. The "Admin" pieces under `shared/components/AdminNav.tsx` belong to Task 9 and may print here.

Measure the bundle: `npm run build`, read the main `index-*.js` gzip size (≤ 200 kB), then `rm -rf dist`.

- [ ] **Step 5: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 8 done: approved words (facility-0085) on the phone pages, the login pages and the shared pieces: พื้นที่, แอดมิน, ตำแหน่ง, ไกลป้าย, เลยเวลา, ส่งเพิ่ม (ไม่นับรอบ), สิ่งที่ต้องแก้; tests changed with them. Frontend 398 tests; main JS <size> kB gzip. Next: Task 9 (words on the Admin live and report pages).
```

```bash
git add docs/handoff.md frontend/src/shared frontend/src/pages/check-in frontend/src/pages/scan \
  frontend/src/pages/my-work frontend/src/pages/inspect frontend/src/pages/my-building frontend/src/pages/auth \
  frontend/src/test/cleanerFixtures.ts frontend/src/router.test.tsx
git status --short   # anything else changed (e.g. a dashboard test for the เลยเวลา chip)? add it by name
git commit -m "feat(frontend): users' words on the phone and login pages (facility-0085)"
```

---

### Task 9: Words on the Admin live board, alert, report pages and the menu

**Files:**
- Modify:
  - `frontend/src/shared/components/AdminNav.tsx`
  - `frontend/src/pages/dashboard/DashboardPage.tsx`, `frontend/src/pages/dashboard/PointTile.tsx`
  - `frontend/src/pages/attendance/AttendancePage.tsx`, `AttendanceTable.tsx`, `CorrectionForm.tsx`, `BoardControls.tsx`, `attendanceRules.ts`
  - `frontend/src/pages/alerts/AlertsPage.tsx`, `EmptyAreas.tsx`, `CoverForm.tsx`, `CoversThisShift.tsx`, `alertRules.ts`
  - `frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx`, `SummaryTable.tsx`
  - `frontend/src/pages/export/ExportPage.tsx`, `exportRules.ts`
  - `frontend/src/pages/history/HistoryPage.tsx`, `HistoryTable.tsx`, `historyRules.ts`
- Test: the files the grep in Step 1 finds. Expected: `router.test.tsx`, and the tests of `dashboard`, `attendance`, `alerts`, `inspection-summary`, `export` and `history`.
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 7's D3 components, whose new text is already in the approved words.
- Produces:
  - The menu reads: สถานะจุด, การเข้างาน, รายการที่ขึ้นเตือน, สรุปการตรวจ, ส่งออก Excel, จัดการพื้นที่และป้าย, บัญชีผู้ใช้, ประวัติการแก้ไขของแอดมิน.
  - Its `aria-label` is `เมนูแอดมิน`.

- [ ] **Step 1: Change the test expectations**

Run: `cd frontend && grep -rnE "Area|Admin|GPS|ธง|นอกรอบ|Dashboard|สแกนนอก|ถูกบล็อก|ข้อบกพร่อง|ออก QR|Issue|หน้างาน|แผนที่" --include='*.test.ts' --include='*.test.tsx' src/router.test.tsx src/pages/dashboard src/pages/attendance src/pages/alerts src/pages/inspection-summary src/pages/export src/pages/history`

Change each expectation that shows one of Step 3's old fragments to its new one. Leave alone:
- fixture data (`'Area ทดลอง'`, `'Office ทดลอง'`, `role: 'Admin'`, `AdminAdd`, `adminSession`)
- the server summaries in `reportFixtures.ts` (old audit rows keep their words, decision 5)
- the history test's search text, if it searches a summary

Two known examples:
- `'ทำแทน: แม่บ้านที่อื่น (AR03) · Admin มอบหมาย 07:10 · เข้างาน 06:50'` → `'ทำแทน: แม่บ้านที่อื่น (AR03) · แอดมินมอบหมาย 07:10 · เข้างาน 06:50'`
- the region `'Area ที่ยังไม่มีคน (2)'` → `'พื้นที่ที่ยังไม่มีคน (2)'` (also in Task 7's new test, which comes back to this shift)

- [ ] **Step 2: Run them to make sure they fail**

Run: `cd frontend && npx vitest run`
Expected: FAIL only on the changed expectations.

- [ ] **Step 3: Change the source text**

| File | Old | New |
|---|---|---|
| `shared/components/AdminNav.tsx` | `label: 'จัดการ Area และป้าย'` | `label: 'จัดการพื้นที่และป้าย'` |
| | `label: 'ประวัติ Admin'` | `label: 'ประวัติการแก้ไขของแอดมิน'` |
| | `aria-label="เมนู Admin"` | `aria-label="เมนูแอดมิน"` |
| `pages/dashboard/DashboardPage.tsx` | `= เลยรอบ ·` | `= เลยเวลา ·` |
| | `เพิ่มได้ที่หน้าจัดการ Area และป้าย` | `เพิ่มได้ที่หน้าจัดการพื้นที่และป้าย` |
| `pages/dashboard/PointTile.tsx` | `OffRound: ' · นอกรอบ'` | `OffRound: ' · ส่งเพิ่ม (ไม่นับรอบ)'` |
| `pages/attendance/AttendancePage.tsx` | `อาจย้าย Area หรือปิดใช้งานแล้ว` | `อาจย้ายพื้นที่หรือปิดใช้งานแล้ว` |
| | `<span>Area ที่ต้องมีคนกะนี้</span>` | `<span>พื้นที่ที่ต้องมีคนกะนี้</span>` |
| | `ไม่รวม Area ที่ปิดกะนี้` | `ไม่รวมพื้นที่ที่ปิดกะนี้` |
| | `Admin มอบหมายเฉพาะกะนี้` | `แอดมินมอบหมายเฉพาะกะนี้` |
| | `หน้านี้ดูว่าแต่ละ Area มีคนอยู่แล้วหรือยัง` | `หน้านี้ดูว่าแต่ละพื้นที่มีคนอยู่แล้วหรือยัง` |
| | `เรียง Area ที่ยังไม่มีคนขึ้นก่อน · Area ที่ทำเฉพาะกะเช้าไม่นับเป็น` | `เรียงพื้นที่ที่ยังไม่มีคนขึ้นก่อน · พื้นที่ที่ทำเฉพาะกะเช้าไม่นับเป็น` |
| | `ให้ Admin เติม` | `ให้แอดมินเติม` |
| `pages/attendance/AttendanceTable.tsx` | `ไม่พบ Area ที่ตรงกับที่ค้นหา` | `ไม่พบพื้นที่ที่ตรงกับที่ค้นหา` |
| | `aria-label="การเข้างานราย Area"` | `aria-label="การเข้างานรายพื้นที่"` |
| | `<th scope="col">Area</th>` | `<th scope="col">พื้นที่</th>` |
| | `<th scope="col">ธง</th>` | `<th scope="col">ไกลป้าย</th>` |
| | `<span className="attendance-badge">แก้โดย Admin</span>` | `<span className="attendance-badge">แก้โดยแอดมิน</span>` |
| `pages/attendance/CorrectionForm.tsx` | `รายการนี้จะมีป้าย "แก้โดย Admin"` | `รายการนี้จะมีป้าย "แก้โดยแอดมิน"` |
| `pages/attendance/BoardControls.tsx` | `placeholder="ชื่อ Area หรือแม่บ้าน"` | `placeholder="ชื่อพื้นที่หรือแม่บ้าน"` |
| `pages/attendance/attendanceRules.ts` | `` `Admin มอบหมาย ${formatTime(cover.assignedAt)}` `` | `` `แอดมินมอบหมาย ${formatTime(cover.assignedAt)}` `` |
| `pages/alerts/AlertsPage.tsx` | `เมื่อวานและวันนี้ · Admin เท่านั้น` | `เมื่อวานและวันนี้ · แอดมินเท่านั้น` |
| | `ทุกการกดในหน้านี้เก็บลงประวัติของ Admin` | `ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน` |
| `pages/alerts/EmptyAreas.tsx` | `` `Area ที่ยังไม่มีคน (${rows.length})` `` | `` `พื้นที่ที่ยังไม่มีคน (${rows.length})` `` |
| | `ทุก Area มีคนแล้ว` | `ทุกพื้นที่มีคนแล้ว` |
| `pages/alerts/CoverForm.tsx` | `'Area นี้ไม่มีแม่บ้านประจำกะนี้'` | `'พื้นที่นี้ไม่มีแม่บ้านประจำกะนี้'` |
| | `แม่บ้านที่ทำแทนลงเวลาที่ป้าย Area ของตัวเองตามปกติ แล้วสแกนจุดของ Area นี้ได้` | `แม่บ้านที่ทำแทนลงเวลาที่ป้ายของพื้นที่ตัวเองตามปกติ แล้วสแกนจุดของพื้นที่นี้ได้` |
| `pages/alerts/CoversThisShift.tsx` | `· Admin มอบหมาย ${formatTime(cover.assignedAt)}` | `· แอดมินมอบหมาย ${formatTime(cover.assignedAt)}` |
| `pages/alerts/alertRules.ts` | `blocked: 'สแกนแล้วถูกบล็อก',` | `blocked: 'สแกนผิดพื้นที่ / ผิดตึก / นอกกะ',` |
| | `return 'สแกนนอกตึก (ถูกบล็อก)';` | `return 'สแกนผิดตึก';` |
| | `'สแกนนอกกะ (ถูกบล็อก)' : 'สแกนนอก Area (ถูกบล็อก)'` | `'สแกนนอกกะ' : 'สแกนผิดพื้นที่'` |
| | `` `Area ของ${alert.person.displayName}คือ ${alert.person.areaCode}` `` | `` `พื้นที่ของ${alert.person.displayName}คือ ${alert.person.areaCode}` `` |
| | `` `สแกนป้าย Area นี้เมื่อ ${formatTime(candidate.scannedThisAreaAt)}` `` | `` `สแกนป้ายพื้นที่นี้เมื่อ ${formatTime(candidate.scannedThisAreaAt)}` `` |
| | the comment `so a Supervisor's is never "สแกนนอก Area"` | `so a Supervisor's is never "สแกนผิดพื้นที่"` |
| `pages/inspection-summary/InspectionSummaryPage.tsx` | `: 'Admin เท่านั้น'` | `: 'แอดมินเท่านั้น'` |
| | `` `Admin เท่านั้น · ${asked `` | `` `แอดมินเท่านั้น · ${asked `` |
| | `หน้านี้แสดงข้อมูลให้ Admin เห็น` | `หน้านี้แสดงข้อมูลให้แอดมินเห็น` |
| | `· "ตรวจที่ติดธง GPS"` | `· "ตรวจตอนอยู่ไกลป้าย"` |
| `pages/inspection-summary/SummaryTable.tsx` | `<th scope="col">ตรวจที่ติดธง GPS</th>` | `<th scope="col">ตรวจตอนอยู่ไกลป้าย</th>` |
| `pages/export/ExportPage.tsx` | `Admin เท่านั้น · ช่วงทดลองใช้ไฟล์ Excel แทนหน้ารายงาน` | `แอดมินเท่านั้น · ช่วงทดลองใช้ไฟล์ Excel แทนหน้ารายงาน` |
| | `ไม่มีพิกัด GPS ดิบ มีแค่ระยะห่างจากป้าย` | `ไม่มีพิกัดตำแหน่งดิบ มีแค่ระยะห่างจากป้าย` |
| | `การส่งออกเก็บลงประวัติ Admin (ใคร` | `การส่งออกเก็บลงประวัติการแก้ไขของแอดมิน (ใคร` |
| | `‹ กลับไป Dashboard` | `‹ กลับไปหน้าสถานะจุด` |
| `pages/export/exportRules.ts` | `'คน · Area · กะ · เวลา 4 ครั้ง · ที่มา (สแกนเอง / แก้โดย Admin) · ธง GPS'` | `'คน · พื้นที่ · กะ · เวลา 4 ครั้ง · ที่มา (สแกนเอง / แก้โดยแอดมิน) · ไกลป้าย'` |
| | `'จุด · รอบ · เวลา · ผล (ตรงเวลา / ส่งช้า / นอกรอบ) · Issue · ทำแทนหรือไม่ · ธง GPS · รอบที่ไม่ได้ทำ'` | `'จุด · รอบ · เวลา · ผล (ตรงเวลา / ส่งช้า / ส่งเพิ่ม ไม่นับรอบ) · แจ้งปัญหา · ทำแทนหรือไม่ · ไกลป้าย · รอบที่ไม่ได้ทำ'` |
| | `'หัวหน้า · จุด · รอบ · ผล · ข้อบกพร่อง · ธง GPS · รอบที่ไม่ได้ตรวจ / ไม่ได้แก้'` | `'หัวหน้า · จุด · รอบ · ผล · สิ่งที่ต้องแก้ · ไกลป้าย · รอบที่ไม่ได้ตรวจ / ไม่ได้แก้'` |
| `pages/history/HistoryPage.tsx` | `Admin เท่านั้น · อ่านอย่างเดียว` | `แอดมินเท่านั้น · อ่านอย่างเดียว` |
| | `<h1>ประวัติการเปลี่ยนแปลงของ Admin</h1>` | `<h1>ประวัติการแก้ไขของแอดมิน</h1>` |
| `pages/history/HistoryTable.tsx` | `aria-label="ประวัติของ Admin"` | `aria-label="ประวัติการแก้ไขของแอดมิน"` |
| `pages/history/historyRules.ts` | `Setup: 'Area · จุด · รอบ · ป้าย',` | `Setup: 'พื้นที่ · จุด · รอบ · ป้าย',` |
| | `QrToken: 'ออก QR ใหม่',` | `QrToken: 'สร้าง QR ใหม่',` |
| | `AREA_CREATE: 'เพิ่ม Area',` | `AREA_CREATE: 'เพิ่มพื้นที่',` |
| | `AREA_UPDATE: 'แก้ Area',` | `AREA_UPDATE: 'แก้พื้นที่',` |
| | `AREA_ACTIVATE: 'เปิดใช้ Area',` | `AREA_ACTIVATE: 'เปิดใช้พื้นที่',` |
| | `AREA_DEACTIVATE: 'ปิดใช้ Area',` | `AREA_DEACTIVATE: 'ปิดใช้พื้นที่',` |
| | `QR_REGENERATE: 'ออก QR ใหม่',` | `QR_REGENERATE: 'สร้าง QR ใหม่',` |
| | `QrIssuedAt: 'ออก QR',` | `QrIssuedAt: 'สร้าง QR',` |
| | `AreaId: 'Area (รหัสในระบบ)',` | `AreaId: 'พื้นที่ (รหัสในระบบ)',` |
| | `ShiftPattern: 'กะของ Area',` | `ShiftPattern: 'กะของพื้นที่',` |
| | `BlockedScanId: 'สแกนที่ถูกบล็อก (รหัสในระบบ)',` | `BlockedScanId: 'สแกนผิดพื้นที่ / ผิดตึก / นอกกะ (รหัสในระบบ)',` |
| | `Source: { Scan: 'สแกนเอง', AdminAdd: 'Admin เพิ่ม', AdminEdit: 'แก้โดย Admin' },` | `Source: { Scan: 'สแกนเอง', AdminAdd: 'แอดมินเพิ่ม', AdminEdit: 'แก้โดยแอดมิน' },` |
| | `Role: { Cleaner: 'แม่บ้าน', Supervisor: 'หัวหน้า', Admin: 'Admin' },` | `Role: { Cleaner: 'แม่บ้าน', Supervisor: 'หัวหน้า', Admin: 'แอดมิน' },` |
| | `LocationSource: { Site: 'หน้างาน', Map: 'แผนที่' },` | `LocationSource: { Site: 'วัดที่ป้ายจริง', Map: 'ปักจากแผนที่' },` |
| | `BlockedScan: 'สแกนที่ถูกบล็อก',` | `BlockedScan: 'สแกนผิดพื้นที่ / ผิดตึก / นอกกะ',` |

- [ ] **Step 4: Run the tests and the gate**

Run: `cd frontend && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS (398 tests), clean.

Gate: `cd frontend/src && grep -rnE "Area|Admin|GPS|ธง|นอกรอบ|Dashboard|สแกนนอก|ถูกบล็อก|ข้อบกพร่อง|ออก QR|Issue|หน้างาน" shared/components/AdminNav.tsx pages/dashboard pages/attendance pages/alerts pages/inspection-summary pages/export pages/history --include='*.ts' --include='*.tsx' | grep -v '\.test\.' | grep -vE ':[0-9]+:\s*(//|\*|/\*\*)' | grep -E '[ก-๙]'`
Expected: only lines where the word is a code name, e.g. `row.areaCode`, `AdminAdd:` and `Admin:` as object keys, `scannedThisAreaAt`.

- [ ] **Step 5: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 9 done: approved words (facility-0085) on D1, D2, D3, D7, D8, D9 and the menu (จัดการพื้นที่และป้าย, ประวัติการแก้ไขของแอดมิน; blocked scans named สแกนผิดพื้นที่ / ผิดตึก / นอกกะ; ไกลป้าย for flags; ตรวจตอนอยู่ไกลป้าย on D7); tests changed with them. Frontend 398 tests. Next: Task 10 (words on the Admin setup pages).
```

```bash
git add docs/handoff.md frontend/src/shared/components/AdminNav.tsx frontend/src/router.test.tsx \
  frontend/src/pages/dashboard frontend/src/pages/attendance frontend/src/pages/alerts \
  frontend/src/pages/inspection-summary frontend/src/pages/export frontend/src/pages/history
git commit -m "feat(frontend): users' words on the Admin live and report pages (facility-0085)"
```

---

### Task 10: Words on the Admin setup pages (D4, D5, D6, D10, M17)

**Files:**
- Modify:
  - `frontend/src/shared/utils/labels.ts` (`signPlaceLabel`)
  - `frontend/src/pages/areas/PointEditor.tsx`, `AreasPage.tsx`, `AreaList.tsx`, `AreaDetailPanel.tsx`, `SignTable.tsx`, `SignPanel.tsx`
  - `frontend/src/pages/capture/CapturePage.tsx`
  - `frontend/src/pages/print-signs/PrintSignsPage.tsx`, `SignCard.tsx`, `printRules.ts`
  - `frontend/src/pages/area-form/AreaFormPage.tsx`, `AreaForm.tsx`, `AreaActiveSection.tsx`, `areaFormRules.ts`
  - `frontend/src/pages/accounts/accountRules.ts`, `AccountsPage.tsx`, `CreateAccountForm.tsx`, `EditAccountForm.tsx`, `DeactivateConfirm.tsx`
- Test: the files the grep in Step 1 finds. Expected: the tests of `areas`, `area-form`, `capture`, `print-signs`, `accounts`, plus `shared/utils/labels.test.ts`.
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: Task 5's phone form (it shows `placeText`, which changes here) and Task 6's boxes.
- Produces:
  - `accountRoleLabel.Admin` and `filterLabel.Admin` are `'แอดมิน'`.
  - `placeText` says `'ยังไม่มีพื้นที่'`.
  - The printed sign says `พื้นที่ AR01 · ตึก A` and `สร้าง QR <date>`.

- [ ] **Step 1: Change the test expectations**

Run: `cd frontend && grep -rnE "Area|Admin|GPS|Location|ติดธง|Dashboard|ออก QR|หน้างาน|จากแผนที่" --include='*.test.ts' --include='*.test.tsx' src/pages/areas src/pages/area-form src/pages/capture src/pages/print-signs src/pages/accounts src/shared/utils`

Change each expectation that shows one of Step 3's old fragments to its new one. Leave alone fixture data (`'Area ทดลอง'`, `'Office ทดลอง'`), `role: 'Admin'` and request bodies. Known examples:
- `'+ เพิ่มจุดทำความสะอาดใน Area นี้'` → `'+ เพิ่มจุดทำความสะอาดในพื้นที่นี้'` (PointEditor and AreasPage tests)
- `'Area ประจำ'` → `'พื้นที่ประจำ'`
- `'เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area'` → `'เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่'`
- `cellsOf('ผู้ดูแลคนที่สอง')`: `['admin2', 'Admin', '—', 'ใช้งาน']` → `['admin2', 'แอดมิน', '—', 'ใช้งาน']`
- the role radio `{ name: 'Admin' }` → `{ name: 'แอดมิน' }`. The request body keeps `role: 'Admin'`.

- [ ] **Step 2: Run them to make sure they fail**

Run: `cd frontend && npx vitest run`
Expected: FAIL only on the changed expectations.

- [ ] **Step 3: Change the source text**

| File | Old | New |
|---|---|---|
| `shared/utils/labels.ts` | `` `หน้างาน ±${sign.locationAccuracyM ?? 0} ม.` `` | `` `วัดที่ป้ายจริง ±${sign.locationAccuracyM ?? 0} ม.` `` |
| | `'ยังไม่ยืนยันหน้างาน (จากแผนที่)'` | `'ปักจากแผนที่ (ยังไม่ได้วัดที่ป้ายจริง)'` |
| `pages/areas/PointEditor.tsx` | `· Area ที่ทำเฉพาะกะเช้าจะไม่มีช่องกะดึก` | `· พื้นที่ที่ทำเฉพาะกะเช้าจะไม่มีช่องกะดึก` |
| | `ปิดใช้งานแล้วจุดและป้ายหายจาก Dashboard และสแกนไม่ได้` | `ปิดใช้งานแล้วจุดและป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้` |
| `pages/areas/AreasPage.tsx` | `<h1>จัดการ Area จุด และพิกัดป้าย</h1>` | `<h1>จัดการพื้นที่ จุด และพิกัดป้าย</h1>` |
| | `ไม่พบ Area นี้` | `ไม่พบพื้นที่นี้` |
| | `ยังไม่มี Area กด "+ เพิ่ม Area" เพื่อเริ่ม` | `ยังไม่มีพื้นที่ กด "+ เพิ่มพื้นที่" เพื่อเริ่ม` |
| | `กำลังโหลด Area...` | `กำลังโหลดพื้นที่...` |
| | `แม่บ้านสแกนได้เฉพาะป้ายของ Area ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อ Area ระบบสร้างให้เองเมื่อเพิ่ม Area ·` | `แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ ·` |
| | `ทุกการแก้ในหน้านี้เก็บลงประวัติ Admin` | `ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน` |
| `pages/areas/AreaList.tsx` | `` `Area ทั้งหมด (${areas?.length ?? 0})` `` | `` `พื้นที่ทั้งหมด (${areas?.length ?? 0})` `` |
| | `+ เพิ่ม Area` | `+ เพิ่มพื้นที่` |
| | `กำลังโหลด Area...` | `กำลังโหลดพื้นที่...` |
| `pages/areas/AreaDetailPanel.tsx` | `` `Area ที่เลือก · ตึก ${area.buildingCode}` `` | `` `พื้นที่ที่เลือก · ตึก ${area.buildingCode}` `` |
| | `แก้ไข Area` (the link text, line 37) | `แก้ไขพื้นที่` |
| | `พิมพ์ป้ายของ Area นี้ ›` | `พิมพ์ป้ายของพื้นที่นี้ ›` |
| | `Area นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไข Area"` | `พื้นที่นี้ปิดใช้งาน ป้ายทุกป้ายสแกนไม่ได้ เปิดใช้งานได้ที่ "แก้ไขพื้นที่"` |
| `pages/areas/SignTable.tsx` | `aria-label="ป้ายของ Area นี้"` | `aria-label="ป้ายของพื้นที่นี้"` |
| | `ป้ายลงเวลา 1 ป้ายต่อ Area` | `ป้ายลงเวลา 1 ป้ายต่อพื้นที่` |
| | `Area นี้ยังไม่มีจุดทำความสะอาด` | `พื้นที่นี้ยังไม่มีจุดทำความสะอาด` |
| | `+ เพิ่มจุดทำความสะอาดใน Area นี้` | `+ เพิ่มจุดทำความสะอาดในพื้นที่นี้` |
| `pages/areas/SignPanel.tsx` | `'บันทึกพิกัดจากแผนที่แล้ว ป้ายนี้ยังไม่ยืนยันหน้างาน'` | `'บันทึกพิกัดที่ปักจากแผนที่แล้ว ป้ายนี้ยังไม่ได้วัดที่ป้ายจริง'` |
| | `ระบบอ่าน GPS ต่อเนื่อง 30 วินาที` | `ระบบอ่านตำแหน่งต่อเนื่อง 30 วินาที` |
| | `ยังไม่ได้ไปหน้างาน? ใส่พิกัดจากแผนที่ไปก่อน` | `ยังไม่ได้ไปที่ป้าย? ปักพิกัดจากแผนที่ไปก่อน` |
| | the button text `บันทึกพิกัดจากแผนที่` (line 97, the whole line) | `บันทึกพิกัดที่ปักจากแผนที่` |
| | `คัดลอกพิกัดจากแอปแผนที่ พิกัดจากแผนที่จะขึ้นว่า "ยังไม่ยืนยันหน้างาน" จนกว่าจะมาเก็บที่ป้ายจริง` | `คัดลอกพิกัดจากแอปแผนที่ พิกัดที่ปักจากแผนที่จะขึ้นว่า "ยังไม่ได้วัดที่ป้ายจริง" จนกว่าจะมาวัดที่ป้าย` |
| | `สแกนห่างจากป้ายเกินรัศมีจะติดธง ค่าเริ่ม 50 ม.` | `สแกนห่างจากป้ายเกินรัศมีจะขึ้นว่าไกลป้าย ค่าเริ่ม 50 ม.` |
| | `` `ออก QR ล่าสุด ${formatDate(sign.qrIssuedAt)} `` | `` `สร้าง QR ล่าสุด ${formatDate(sign.qrIssuedAt)} `` |
| | `เปิดใช้งานก่อนจึงออก QR ใหม่ได้` | `เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้` |
| | the button text `ออก QR ใหม่` (line 187, the whole line) | `สร้าง QR ใหม่` |
| | `` `ออก QR ใหม่ของป้าย ${sign.code} ${label}?` `` | `` `สร้าง QR ใหม่ของป้าย ${sign.code} ${label}?` `` |
| | `'กำลังออก QR...' : 'ยืนยัน ออก QR ใหม่'` | `'กำลังสร้าง QR...' : 'ยืนยัน สร้าง QR ใหม่'` |
| | `<span>ออก QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว</span>` | `<span>สร้าง QR ใหม่แล้ว ป้ายเดิมสแกนไม่ได้แล้ว</span>` |
| `pages/capture/CapturePage.tsx` | `‹ กลับไปหน้าจัดการ Area และป้าย` | `‹ กลับไปหน้าจัดการพื้นที่และป้าย` |
| | `` `Admin · เก็บพิกัดป้าย · `` | `` `แอดมิน · เก็บพิกัดป้าย · `` |
| | `ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติ Admin` | `ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติการแก้ไขของแอดมิน` |
| | `ป้ายของ Area นี้` (every: the heading and the list's `aria-label`) | `ป้ายของพื้นที่นี้` |
| | `` `กำลังอ่าน GPS ${phase.progress.seconds} `` | `` `กำลังอ่านตำแหน่ง ${phase.progress.seconds} `` |
| | `` `อ่าน GPS ได้แค่ ${result.readings} ครั้ง `` | `` `อ่านตำแหน่งได้แค่ ${result.readings} ครั้ง `` |
| | `เปิด Location และอนุญาตให้เว็บนี้อ่านตำแหน่ง` | `เปิดตำแหน่งและอนุญาตให้เว็บนี้อ่านตำแหน่ง` |
| | `เปิดหน้านี้บนมือถือที่มี GPS` | `เปิดหน้านี้บนมือถือที่อ่านตำแหน่งได้` |
| `pages/print-signs/PrintSignsPage.tsx` | `which === 'all' ? 'ทุก Area'` | `which === 'all' ? 'ทุกพื้นที่'` |
| | `‹ กลับไปหน้าจัดการ Area และป้าย` | `‹ กลับไปหน้าจัดการพื้นที่และป้าย` |
| | `<label htmlFor="print-area">Area</label>` | `<label htmlFor="print-area">พื้นที่</label>` |
| | `เลือก Area` (the first option's text, line 51) | `เลือกพื้นที่` |
| | `` `ทุก Area (${activeAreas.length})` `` | `` `ทุกพื้นที่ (${activeAreas.length})` `` |
| | `พิมพ์ทั้ง Area ›` | `พิมพ์ทั้งพื้นที่ ›` |
| | `เลือก Area ที่จะพิมพ์` | `เลือกพื้นที่ที่จะพิมพ์` |
| `pages/print-signs/SignCard.tsx` | `` · ออก QR ${formatDate(sign.issuedAt)}` `` | `` · สร้าง QR ${formatDate(sign.issuedAt)}` `` |
| `pages/print-signs/printRules.ts` | `` `Area ${area.code} · ตึก ${area.buildingCode}` `` | `` `พื้นที่ ${area.code} · ตึก ${area.buildingCode}` `` |
| | `'Area นี้ปิดใช้งาน ป้ายสแกนไม่ได้ จึงไม่มีป้ายให้พิมพ์'` | `'พื้นที่นี้ปิดใช้งาน ป้ายสแกนไม่ได้ จึงไม่มีป้ายให้พิมพ์'` |
| `pages/area-form/AreaFormPage.tsx` | `id === undefined ? 'เพิ่ม Area'` | `id === undefined ? 'เพิ่มพื้นที่'` |
| | `` `แก้ไข Area · ${detail.area.code} `` | `` `แก้ไขพื้นที่ · ${detail.area.code} `` |
| | `: 'แก้ไข Area';` | `: 'แก้ไขพื้นที่';` |
| | `‹ จัดการ Area และป้าย` | `‹ จัดการพื้นที่และป้าย` |
| | `การบันทึกทุกครั้งเก็บลงประวัติ Admin` | `การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน` |
| `pages/area-form/AreaForm.tsx` | `<label htmlFor="areaform-code">รหัส Area</label>` | `<label htmlFor="areaform-code">รหัสพื้นที่</label>` |
| | the comment `the field's name stays "รหัส Area"` | `the field's name stays "รหัสพื้นที่"` |
| | `พิมพ์บนทุกป้ายของ Area แก้ทีหลังไม่ได้` | `พิมพ์บนทุกป้ายของพื้นที่ แก้ทีหลังไม่ได้` |
| | `` `รหัส Area ${detail.area.code} (แก้ไม่ได้ `` | `` `รหัสพื้นที่ ${detail.area.code} (แก้ไม่ได้ `` |
| | `ชื่อ Area` (the label text, line 68) | `ชื่อพื้นที่` |
| | `Area ละ 1 คนต่อกะ · รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มี Area ในกะนั้น` | `พื้นที่ละ 1 คนต่อกะ · รายชื่อแสดงเฉพาะแม่บ้านที่ยังไม่มีพื้นที่ในกะนั้น` |
| | `ของทุกจุดใน Area นี้จะถูกลบ และแม่บ้านกะดึกออกจาก Area` | `ของทุกจุดในพื้นที่นี้จะถูกลบ และแม่บ้านกะดึกออกจากพื้นที่` |
| `pages/area-form/AreaActiveSection.tsx` | `<h2 id="areaform-signs">ป้ายของ Area นี้</h2>` | `<h2 id="areaform-signs">ป้ายของพื้นที่นี้</h2>` |
| | `ระบบสร้างให้เองตอนเพิ่ม Area` | `ระบบสร้างให้เองตอนเพิ่มพื้นที่` |
| | `เพิ่มหรือแก้ที่หน้า จัดการ Area และป้าย` | `เพิ่มหรือแก้ที่หน้า จัดการพื้นที่และป้าย` |
| | `'ปิดใช้งาน Area' : 'เปิดใช้งาน Area'` | `'ปิดใช้งานพื้นที่' : 'เปิดใช้งานพื้นที่'` |
| | `Area และทุกป้ายหายจาก Dashboard และสแกนไม่ได้` | `พื้นที่และทุกป้ายหายจากหน้าสถานะจุดและสแกนไม่ได้` |
| | `Area นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้` | `พื้นที่นี้ปิดใช้งานอยู่ ป้ายทุกป้ายสแกนไม่ได้` |
| | `'ปิดใช้งาน Area นี้' : 'เปิดใช้งาน Area นี้'` | `'ปิดใช้งานพื้นที่นี้' : 'เปิดใช้งานพื้นที่นี้'` |
| `pages/area-form/areaFormRules.ts` | `'ใส่รหัส Area ก่อน'` | `'ใส่รหัสพื้นที่ก่อน'` |
| | `'ใส่ชื่อ Area ก่อน'` | `'ใส่ชื่อพื้นที่ก่อน'` |
| `pages/accounts/accountRules.ts` | `  Admin: 'Admin',` (every: `filterLabel` and `accountRoleLabel`) | `  Admin: 'แอดมิน',` |
| | `'ยังไม่มี Area'` | `'ยังไม่มีพื้นที่'` |
| | `'Area นี้ทำเฉพาะกะเช้า'` | `'พื้นที่นี้ทำเฉพาะกะเช้า'` |
| `pages/accounts/AccountsPage.tsx` | `ยังไม่มี Area เลือก Area ได้ที่ "แก้ไข"` | `ยังไม่มีพื้นที่ เลือกพื้นที่ได้ที่ "แก้ไข"` |
| | `ทุกการแก้ในหน้านี้เก็บลงประวัติของ Admin` | `ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน` |
| `pages/accounts/CreateAccountForm.tsx` | `Admin: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว` | `แอดมิน: ชื่อผู้ใช้ + รหัสผ่านชั่วคราว` |
| | `<label htmlFor="accounts-create-area">Area ประจำ</label>` | `<label htmlFor="accounts-create-area">พื้นที่ประจำ</label>` |
| | `— ยังไม่กำหนด Area —` | `— ยังไม่กำหนดพื้นที่ —` |
| | `'Area ละ 1 คนต่อกะ · ถ้ากะนี้มีคนประจำแล้วจะเลือกไม่ได้ · Area ที่ทำเฉพาะกะเช้าเลือกได้แค่กะเช้า · ยังไม่กำหนด Area ได้ เลือกทีหลังที่ปุ่ม "แก้ไข"'` | `'พื้นที่ละ 1 คนต่อกะ · ถ้ากะนี้มีคนประจำแล้วจะเลือกไม่ได้ · พื้นที่ที่ทำเฉพาะกะเช้าเลือกได้แค่กะเช้า · ยังไม่กำหนดพื้นที่ได้ เลือกทีหลังที่ปุ่ม "แก้ไข"'` |
| `pages/accounts/EditAccountForm.tsx` | `>Area ประจำ</label>` | `>พื้นที่ประจำ</label>` |
| | `— ยังไม่กำหนด Area —` | `— ยังไม่กำหนดพื้นที่ —` |
| | `เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area` | `เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่` |
| `pages/accounts/DeactivateConfirm.tsx` | `เปิดใช้งานกลับแล้วต้องเลือก Area ใหม่` | `เปิดใช้งานกลับแล้วต้องเลือกพื้นที่ใหม่` |

- [ ] **Step 4: Run the tests and the gate**

Run: `cd frontend && npx vitest run && npm run lint && npx tsc -b`
Expected: PASS (398 tests), clean.

Gate: `cd frontend/src && grep -rnE "Area|Admin|GPS|Location|ติดธง|Dashboard|ออก QR|หน้างาน|จากแผนที่" shared/utils/labels.ts pages/areas pages/capture pages/print-signs pages/area-form pages/accounts --include='*.ts' --include='*.tsx' | grep -v '\.test\.' | grep -vE ':[0-9]+:\s*(//|\*|/\*\*)' | grep -E '[ก-๙]'`
Expected: only code names, and the text `ปักจากแผนที่` / `ปักพิกัดจากแผนที่` (the approved word).

- [ ] **Step 5: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 10 done: approved words (facility-0085) on D4, D5 (printed signs: "พื้นที่ AR01 · ตึก A", "สร้าง QR"), D6, D10 and M17 (วัดที่ป้ายจริง / ปักจากแผนที่, สร้าง QR ใหม่, แอดมิน); tests changed with them. Frontend 398 tests. Next: Task 11 (server words).
```

```bash
git add docs/handoff.md frontend/src/shared/utils frontend/src/pages/areas frontend/src/pages/capture \
  frontend/src/pages/print-signs frontend/src/pages/area-form frontend/src/pages/accounts
git commit -m "feat(frontend): users' words on the Admin setup pages and printed signs (facility-0085)"
```

---

### Task 11: Words in server messages, audit summaries and the Excel file

**Files:**
- Modify:
  - `backend/src/FacilityRealtime.Api/Endpoints/ThaiLabels.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AdminAlertEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/CoverAssignmentEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/ExportSheets.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/GpsInput.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AdminPointEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AdminAccountEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AccountChecks.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AdminAttendanceEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AdminAreaEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/InspectionEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/AdminSignEndpoints.cs`
  - `backend/src/FacilityRealtime.Api/Endpoints/ScanRecordEndpoints.cs`
  - `backend/src/FacilityRealtime.Application/Rounds/RoundWindowRules.cs`
- Test: the files Step 1's grep finds, at least:
  - `ApiTests/Admin/ExportEndpointTests.cs`, `InspectionExportTests.cs`, `SignAdminEndpointTests.cs`
  - `AccountEditEndpointTests.cs`, `AccountSecretEndpointTests.cs`, `AreaAdminEndpointTests.cs`
  - `SupervisorCoverTests.cs`, `AlertEndpointTests.cs`
  - a unit test of `RoundWindowRules` if it asserts the message
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `ThaiLabels.BlockedName(BlockReason reason): string`
  - `ThaiLabels.AlertName(AlertKind kind, BlockReason? reason = null): string`
  - The phone's blocked message is `"ป้ายนี้ไม่ใช่พื้นที่ของคุณ"`. Task 8 already put it in the frontend fixture.

- [ ] **Step 1: Change the test expectations**

Run: `grep -rnE 'Area นี้|ไม่พบ Area|เลือก Area|บัญชี Admin|ประจำ Area|รหัส Area|ชื่อ Area|Area ที่ทำ|จาก Area|ป้ายของ Area|ไม่ใช่ Area|เป็น Area|ระบุ Area|เปิด Location|ข้อบกพร่อง|หน้างาน|ออก QR|ถูกบล็อก|Admin เติม|แก้โดย Admin|มี Issue|"นอกรอบ"|สแกนนอก|"Area"|"GPS"|"Issue"|เพิ่ม Area|แก้ Area|ใช้งาน Area|ยังไม่มี Area|Area คุณ' backend/tests --include='*.cs'`

Change each expectation by Step 3's table. Known ones:

| Test file | Old | New |
|---|---|---|
| `Admin/ExportEndpointTests.cs` | header rows `"Area"`, `"GPS"`, `"Issue"` | `"พื้นที่"`, `"ตำแหน่ง"`, `"แจ้งปัญหา"` |
| | `"แก้โดย Admin: เลิกงาน"` | `"แก้โดยแอดมิน: เลิกงาน"` |
| | `"มี Issue"` | `"แจ้งปัญหา"` |
| | `"นอกรอบ"` (every) | `"ส่งเพิ่ม (ไม่นับรอบ)"` |
| `Admin/InspectionExportTests.cs` | `"Area"`, `"ข้อบกพร่อง"`, `"GPS"` in the header | `"พื้นที่"`, `"สิ่งที่ต้องแก้"`, `"ตำแหน่ง"` |
| `Admin/SignAdminEndpointTests.cs` | `"ต้องส่งพิกัด (ละติจูด ลองจิจูด) และความแม่นยำเมื่อเก็บที่หน้างาน"` | `"ต้องส่งพิกัด (ละติจูด ลองจิจูด) และความแม่นยำเมื่อวัดที่ป้ายจริง"` |
| `Admin/AccountEditEndpointTests.cs` | `"ต้องเลือก Area ที่ใช้งานอยู่"` | `"ต้องเลือกพื้นที่ที่ใช้งานอยู่"` |
| | `"เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area"` | `"เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่"` |
| `Admin/AccountSecretEndpointTests.cs` | `"บัญชี Admin ไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน"` | `"บัญชีแอดมินไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน"` |
| `Admin/AreaAdminEndpointTests.cs` | `"ไม่พบ Area นี้"` | `"ไม่พบพื้นที่นี้"` |
| `Admin/SupervisorCoverTests.cs` | `"ปิดเตือนสแกนนอก Area ของ สมปอง ตรวจดี เวลา 08:30: มอบหมายตรวจแทนตึก B แล้ว"` | `"ปิดเตือนสแกนผิดตึก ของ สมปอง ตรวจดี เวลา 08:30: มอบหมายตรวจแทนตึก B แล้ว"` |

Test method names such as `Location_is_required` stay.

Also add one assertion to `A_blocked_scan_can_be_reviewed` in `Admin/AlertEndpointTests.cs`, after `Assert.Equal(alert.RecordId, entry.EntityId);`:

```csharp
        Assert.StartsWith("ปิดเตือนสแกนผิดพื้นที่ ของ สมชาย ใจดี เวลา ", entry.Summary); // named by its reason (facility-0085)
```

(If the audit model there names the field differently, use the name `SupervisorCoverTests` reads: `audit[1].Summary`.)

- [ ] **Step 2: Run them to make sure they fail**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: FAIL only on the changed expectations.

- [ ] **Step 3: Change the server text**

`ThaiLabels.cs`:

1. In `RoleName`, `_ => "Admin",` → `_ => "แอดมิน",`.
2. Replace `AlertName` with:

```csharp
    /// <summary>facility-0085: a Blocked Scan named by why it was refused, as the Admin's pages say it.</summary>
    public static string BlockedName(BlockReason reason) => reason switch
    {
        BlockReason.OtherBuilding => "สแกนผิดตึก",
        BlockReason.OutsideShift => "สแกนนอกกะ",
        _ => "สแกนผิดพื้นที่",
    };

    public static string AlertName(AlertKind kind, BlockReason? reason = null) => kind switch
    {
        AlertKind.BlockedScan => BlockedName(reason ?? BlockReason.OtherArea),
        AlertKind.AttendanceGps => "ลงเวลาไกลจากป้าย",
        AlertKind.SubmissionGps => "ส่งงานไกลจากป้าย",
        AlertKind.InspectionGps => "ตรวจงานไกลจากป้าย",
        _ => "ไม่ได้ลงเวลาเลิกงาน",
    };
```

3. In `SubmissionResult`, `_ => "นอกรอบ",` → `_ => "ส่งเพิ่ม (ไม่นับรอบ)",`.
4. In `BlockedMessage`, `_ => "ป้ายนี้ไม่ใช่ Area ของคุณ",` → `_ => "ป้ายนี้ไม่ใช่พื้นที่ของคุณ",`.

`AdminAlertEndpoints.cs`:

1. `private sealed record AlertTarget(string Table, string Name, DateTime At);` → `private sealed record AlertTarget(string Table, string Name, DateTime At, BlockReason? Reason = null);`
2. In `FindAsync`'s Blocked Scan branch, `(b, u) => new AlertTarget("blocked_scans", u.DisplayName, b.ScannedAt)` → `(b, u) => new AlertTarget("blocked_scans", u.DisplayName, b.ScannedAt, b.Reason)`.
3. In `ReviewAsync`'s summary, `` $"ปิดเตือน{ThaiLabels.AlertName(kind)} ของ `` → `` $"ปิดเตือน{ThaiLabels.AlertName(kind, target.Reason)} ของ ``.
4. `"เตือนนี้ปิดเองเมื่อ Admin เติมเวลาที่ขาด"` → `"เตือนนี้ปิดเองเมื่อแอดมินเติมเวลาที่ขาด"`.

`CoverAssignmentEndpoints.cs`:

1. In `SaveAsync`, `` $"ปิดเตือน{ThaiLabels.AlertName(AlertKind.BlockedScan)} ของ {person.DisplayName} `` → `` $"ปิดเตือน{ThaiLabels.BlockedName(scan.Reason)} ของ {person.DisplayName} ``. If `using FacilityRealtime.Application.Alerts;` is then unused in the file, delete it.
2. The messages:

| Old | New |
|---|---|
| `"แม่บ้านทำแทนเป็น Area ไม่ต้องระบุตึก"` | `"แม่บ้านทำแทนเป็นพื้นที่ ไม่ต้องระบุตึก"` |
| `"ไม่พบ Area นี้ หรือปิดใช้งานแล้ว"` (every) | `"ไม่พบพื้นที่นี้ หรือปิดใช้งานแล้ว"` |
| `"Area นี้เป็น Area ประจำของแม่บ้านคนนี้อยู่แล้ว"` | `"พื้นที่นี้เป็นพื้นที่ประจำของแม่บ้านคนนี้อยู่แล้ว"` |
| `"Area นี้ไม่มีกะนี้"` (every) | `"พื้นที่นี้ไม่มีกะนี้"` |
| `"ไม่พบการสแกนที่ถูกบล็อกนี้"` (every) | `"ไม่พบการสแกนนี้"` |
| `"การสแกนที่ถูกบล็อกนี้ไม่ใช่ของแม่บ้านคนนี้ที่ Area นี้"` | `"การสแกนนี้ไม่ใช่ของแม่บ้านคนนี้ที่พื้นที่นี้"` |
| `"การสแกนที่ถูกบล็อกนี้ไม่ได้อยู่ในกะนี้"` (every) | `"การสแกนนี้ไม่ได้อยู่ในกะนี้"` |
| `"หัวหน้าตรวจแทนเป็นตึก ไม่ต้องระบุ Area"` | `"หัวหน้าตรวจแทนเป็นตึก ไม่ต้องระบุพื้นที่"` |
| `"การสแกนที่ถูกบล็อกนี้ไม่ใช่ของหัวหน้าคนนี้ที่ตึกนี้"` | `"การสแกนนี้ไม่ใช่ของหัวหน้าคนนี้ที่ตึกนี้"` |
| `"ต้องระบุ Area วันที่ (yyyy-MM-dd) และกะ (Day หรือ Night)"` | `"ต้องระบุพื้นที่ วันที่ (yyyy-MM-dd) และกะ (Day หรือ Night)"` |

`ExportSheets.cs`:

| Old | New |
|---|---|
| `"แก้โดย Admin: "` | `"แก้โดยแอดมิน: "` |
| the attendance header `["วันที่กะ", "กะ", "ตึก", "Area", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "GPS"]` | `["วันที่กะ", "กะ", "ตึก", "พื้นที่", "รหัสพนักงาน", "ชื่อ", "เข้างาน", "ออกพัก", "กลับจากพัก", "เลิกงาน", "ที่มา", "ตำแหน่ง"]` |
| `s.IssueTags ?? "มี Issue"` | `s.IssueTags ?? "แจ้งปัญหา"` |
| the submissions header `["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "Issue", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "GPS"]` | `["วันที่กะ", "กะ", "ตึก", "พื้นที่", "จุด", "ป้าย", "รอบ", "เวลาส่ง", "ผล", "แจ้งปัญหา", "ทำแทน", "รหัสพนักงาน", "ชื่อ", "ตำแหน่ง"]` |
| the inspections header `["วันที่กะ", "กะ", "ตึก", "Area", "จุด", "ป้าย", "รอบ", "เวลาตรวจ", "ผล", "ข้อบกพร่อง", "ตรวจแทน", "รหัสพนักงาน", "ชื่อ", "GPS"]` | `["วันที่กะ", "กะ", "ตึก", "พื้นที่", "จุด", "ป้าย", "รอบ", "เวลาตรวจ", "ผล", "สิ่งที่ต้องแก้", "ตรวจแทน", "รหัสพนักงาน", "ชื่อ", "ตำแหน่ง"]` |

The other files:

| File | Old | New |
|---|---|---|
| `GpsInput.cs` | `"ต้องเปิด Location และอนุญาตให้เว็บอ่านตำแหน่งก่อนสแกน"` | `"ต้องเปิดตำแหน่งและอนุญาตให้เว็บอ่านตำแหน่งก่อนสแกน"` |
| `AdminPointEndpoints.cs` | `"ไม่พบ Area นี้"` | `"ไม่พบพื้นที่นี้"` |
| `AdminAccountEndpoints.cs` | `"บัญชี Admin ใช้ชื่อผู้ใช้และรหัสผ่าน ไม่ใช้รหัสพนักงานหรือเบอร์โทร"` | `"บัญชีแอดมินใช้ชื่อผู้ใช้และรหัสผ่าน ไม่ใช้รหัสพนักงานหรือเบอร์โทร"` |
| | `"เปิดใช้งานบัญชีก่อน แล้วค่อยเลือก Area"` | `"เปิดใช้งานบัญชีก่อน แล้วค่อยเลือกพื้นที่"` |
| | `"บัญชี Admin ไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน"` | `"บัญชีแอดมินไม่มีเบอร์โทร ใช้ปุ่มรีเซ็ตรหัสผ่าน"` |
| `AccountChecks.cs` | `"บัญชี Admin ไม่มี Area ตึก หรือกะ"` | `"บัญชีแอดมินไม่มีพื้นที่ ตึก หรือกะ"` |
| | `"แม่บ้านประจำ Area ไม่ได้ประจำตึก"` | `"แม่บ้านประจำพื้นที่ ไม่ได้ประจำตึก"` |
| | `"ต้องเลือก Area ที่ใช้งานอยู่"` | `"ต้องเลือกพื้นที่ที่ใช้งานอยู่"` |
| | `"หัวหน้าประจำตึก ไม่ได้ประจำ Area"` | `"หัวหน้าประจำตึก ไม่ได้ประจำพื้นที่"` |
| `AdminAttendanceEndpoints.cs` | `"แม่บ้านคนนี้ยังไม่มี Area"` | `"แม่บ้านคนนี้ยังไม่มีพื้นที่"` |
| `AdminAreaEndpoints.cs` | `"รหัส Area ต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03"` | `"รหัสพื้นที่ต้องเป็นตัวอักษรอังกฤษหรือตัวเลข 2–20 ตัว เช่น AR03"` |
| | `"รหัส Area นี้มีแล้ว"` | `"รหัสพื้นที่นี้มีแล้ว"` |
| | `` $"เพิ่ม Area {code} {form.Name}" `` | `` $"เพิ่มพื้นที่ {code} {form.Name}" `` |
| | `` $"แก้ Area {area.Code} {area.Name}" `` | `` $"แก้พื้นที่ {area.Code} {area.Name}" `` |
| | `` ใช้งาน Area {area.Code}" `` | `` ใช้งานพื้นที่ {area.Code}" `` |
| | `"ต้องใส่ชื่อ Area ไม่เกิน 150 ตัวอักษร"` | `"ต้องใส่ชื่อพื้นที่ ไม่เกิน 150 ตัวอักษร"` |
| | `"Area ที่ทำเฉพาะกะเช้าไม่มีแม่บ้านกะดึก"` | `"พื้นที่ที่ทำเฉพาะกะเช้าไม่มีแม่บ้านกะดึก"` |
| | `ย้ายออกจาก Area นั้นก่อน` | `ย้ายออกจากพื้นที่นั้นก่อน` |
| | `"ไม่พบ Area นี้"` | `"ไม่พบพื้นที่นี้"` |
| `InspectionEndpoints.cs` | `"เลือกไม่สะอาดต้องระบุข้อบกพร่อง"` | `"เลือกไม่สะอาดต้องระบุสิ่งที่ต้องแก้"` |
| | `"ข้อบกพร่องใส่ได้เฉพาะเมื่อเลือกไม่สะอาด"` | `"สิ่งที่ต้องแก้ใส่ได้เฉพาะเมื่อเลือกไม่สะอาด"` |
| | `` $"ข้อบกพร่องยาวได้ไม่เกิน `` | `` $"สิ่งที่ต้องแก้ยาวได้ไม่เกิน `` |
| `AdminSignEndpoints.cs` | `"ต้องระบุว่าพิกัดมาจากหน้างาน (Site) หรือแผนที่ (Map)"` | `"ต้องระบุว่าพิกัดวัดที่ป้ายจริง (Site) หรือปักจากแผนที่ (Map)"` |
| | `"ต้องส่งพิกัด (ละติจูด ลองจิจูด) และความแม่นยำเมื่อเก็บที่หน้างาน"` | `"ต้องส่งพิกัด (ละติจูด ลองจิจูด) และความแม่นยำเมื่อวัดที่ป้ายจริง"` |
| | `"ป้ายนี้ยืนยันพิกัดที่หน้างานแล้ว ถ้าจะแก้ให้เก็บใหม่ที่หน้างาน"` | `"ป้ายนี้วัดพิกัดที่ป้ายจริงแล้ว ถ้าจะแก้ให้ไปวัดใหม่ที่ป้าย"` |
| | `` $"เก็บพิกัดป้าย {sign.Code} ที่หน้างาน ±{sign.LocationAccuracyM} ม." `` | `` $"วัดพิกัดป้าย {sign.Code} ที่ป้ายจริง ±{sign.LocationAccuracyM} ม." `` |
| | `` $"ปักพิกัดป้าย {sign.Code} จากแผนที่ (ยังไม่ยืนยันหน้างาน)" `` | `` $"ปักพิกัดป้าย {sign.Code} จากแผนที่ (ยังไม่ได้วัดที่ป้ายจริง)" `` |
| | `"ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงออก QR ใหม่ได้"` | `"ป้ายนี้ปิดใช้งานอยู่ เปิดใช้งานก่อนจึงสร้าง QR ใหม่ได้"` |
| | `` $"ออก QR ใหม่ของป้าย {sign.Code} ป้ายเดิมสแกนไม่ได้แล้ว" `` | `` $"สร้าง QR ใหม่ของป้าย {sign.Code} ป้ายเดิมสแกนไม่ได้แล้ว" `` |
| `ScanRecordEndpoints.cs` | `"ลงเวลาเข้างานที่ป้ายของ Area คุณก่อน"` | `"ลงเวลาเข้างานที่ป้ายของพื้นที่คุณก่อน"` |
| `Application/Rounds/RoundWindowRules.cs` | `"Area นี้ทำเฉพาะกะเช้า ใส่ช่วงรอบกะดึกไม่ได้"` | `"พื้นที่นี้ทำเฉพาะกะเช้า ใส่ช่วงรอบกะดึกไม่ได้"` |

Not changed:
- `MyWorkEndpoints.cs`'s `"หน้างานของฉันใช้สำหรับแม่บ้านเท่านั้น"` (the page name)
- the seed data in `DbInitializer.cs` (data)
- the `ExcelWorkbook` sheet names (`การลงเวลา`, `การส่งงาน`, `การตรวจงาน`)

- [ ] **Step 4: Run the suite and the gate**

Run: `dotnet test backend/FacilityRealtime.slnx`
Expected: unit 302 passed; API 540 passed, 5 skipped.

Gate: `grep -rnE 'Area|Admin|GPS|Location|ข้อบกพร่อง|หน้างาน|ออก QR|ถูกบล็อก|Issue|นอกรอบ|สแกนนอก' backend/src --include='*.cs' | grep -v '/Migrations/' | grep -vE ':[0-9]+:\s*///?' | grep -E '"[^"]*[ก-๙][^"]*"'`
Expected: only:
- `DbInitializer.cs` (seed names)
- `MyWorkEndpoints.cs` ("หน้างานของฉัน")
- `AdminAttendanceEndpoints.cs` line 134, which has codes, not words
- lines where the word is a code name inside an interpolation, e.g. `{cleaner.Area!.Code}`, `{holder.Area!.Code}`

- [ ] **Step 5: Log the task in the handoff and commit**

```markdown
- 2026-10-22 · Task 11 done: approved words (facility-0085) in server messages, new audit summaries and the Excel file (headers พื้นที่, ตำแหน่ง, แจ้งปัญหา, สิ่งที่ต้องแก้; "ส่งเพิ่ม (ไม่นับรอบ)"; "แก้โดยแอดมิน"); a Blocked Scan's alert name in summaries follows its reason (`ThaiLabels.BlockedName`), which closes the parked "สแกนนอก Area" item; old audit rows keep their words. Unit 302; API 540 + 5 skipped. Next: Task 12 (docs, final checks, real run).
```

```bash
git add docs/handoff.md backend/src/FacilityRealtime.Api/Endpoints backend/src/FacilityRealtime.Application/Rounds/RoundWindowRules.cs \
  backend/tests
git status --short   # only the files of this task
git commit -m "feat(api): users' words in messages, audit summaries and Excel (facility-0085)"
```

---

### Task 12: Docs, final checks and the real run

**Files:**
- Modify:
  - `docs/adr/facility-0074-ux-pass-becomes-a-fixed-fix-list.md`
  - `docs/adr/facility-0077-off-shift-pages-show-own-shift-time.md`
  - `docs/adr/facility-0082-unusable-check-in-sign-told-on-open.md`
  - `docs/adr/facility-0083-round-window-time-split-hour-minute.md`
  - `docs/adr/facility-0084-advance-cover-picks-area-from-searchable-list.md`
  - `docs/adr/facility-0085-ui-words-from-the-users-not-the-glossary.md`
  - `docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md`
  - `docs/architecture.md`
  - `docs/handoff.md`

**Interfaces:** none. This task changes no code.

- [ ] **Step 1: ADR amendments**

Append each block at the end of its ADR, after a blank line.

`facility-0074`:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** ทำครบทุกกลุ่มในแผน `docs/superpowers/plans/2026-10-22-ux-fix-list.md` บน branch `feat/ux-fix-list`: กลุ่ม 1 (D3 ล่วงหน้า), กลุ่ม 2 (D6, D4), กลุ่ม 4 (หน้ามือถือแม่บ้าน) และกลุ่ม 5 (คำบนหน้าเว็บ) ข้อ 4.2 และส่วนป้ายของพื้นที่อื่นในข้อ 4.1 ไม่แก้ตาม facility-0080
```

`facility-0077`:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** เซิร์ฟเวอร์ส่ง `ownShift { shift, opensAt, closesAt }` มากับ `GET /api/my-work` (เวลาตามนาฬิกาไทย คำนวณจากค่า 60 นาทีใน config) ส่วนจุดนอกช่วงลงเวลาเซิร์ฟเวอร์ยังส่งเหมือนเดิม หน้างานของฉันเป็นคนซ่อนตัวเลขสรุปและการ์ดเอง หน้าป้ายลงเวลาและหน้าป้ายจุดจึงยังใช้ชื่อพื้นที่ของตัวเองได้
```

`facility-0082`:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** ทางที่ใช้คือ `GET /api/attendance/signs/{token}` (แม่บ้านเท่านั้น) ตอบ 204 ถ้าเป็นป้ายลงเวลาของพื้นที่ที่ยังใช้งานอยู่ ไม่ว่าเป็นของพื้นที่ไหน และตอบ 404 "ไม่พบป้ายลงเวลานี้" ถ้าเป็นป้ายจุด QR ที่ไม่มีในระบบ หรือพื้นที่ปิดใช้งาน กฎเดียวกับตอนกดลงเวลา ไม่บันทึกอะไร หน้าจอแสดงปุ่มลงเวลาเมื่อได้ 204 ล่าสุดเท่านั้น
```

`facility-0083`:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** รายละเอียดการพิมพ์: ช่องชั่วโมงรับ 0–23 จะพิมพ์ 1 หรือ 2 หลักก็ได้ ("7" คือ 07) ช่องนาทีว่างถือเป็น 00 ถ้าพิมพ์ต้องเป็น 2 หลัก 00–59 ช่องรับเฉพาะตัวเลข พิมพ์ชั่วโมงครบ 2 หลักแล้วเคอร์เซอร์ไปช่องนาทีเอง กด Enter ในช่องไหนก็เท่ากับกด "เพิ่ม" ถ้าเวลาไม่ถูก ขึ้นว่า "ชั่วโมงใส่ 0–23 นาทีใส่ 00–59"
```

`facility-0084`:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** ใช้ข้อมูลที่มีอยู่ ไม่แก้เซิร์ฟเวอร์: กะล่วงหน้าอ่านจาก `GET /api/admin/attendance?shiftDate&shift` ซึ่งให้ทุกพื้นที่ที่ทำกะนั้นพร้อมแม่บ้านประจำอยู่แล้ว ตัวเลือก "วันและกะ" มีตั้งแต่กะปัจจุบันถึงกะดึกของวันที่ 7 ข้างหน้า พร้อมชื่อวันแบบย่อ ช่องค้นหาค้นเฉพาะแถวที่อยู่บนหน้า (ชื่อแม่บ้านประจำ รหัสพื้นที่ ชื่อพื้นที่) ใช้ได้ทั้งกะปัจจุบันและกะล่วงหน้า กล่อง "คนทำแทน" และรายชื่อคนทำแทนตามกะที่เลือก
```

`facility-0085`:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** เปลี่ยนครบทั้งหน้าเว็บ ข้อความจากเซิร์ฟเวอร์ หัวคอลัมน์ Excel และป้ายที่พิมพ์ ประวัติการแก้ไขที่บันทึกไปแล้วยังเป็นคำเดิม เพราะ audit_log แก้ย้อนหลังไม่ได้ (facility-0057) รายการใหม่ใช้คำใหม่ ชื่อที่คนพิมพ์ไว้ เช่น ชื่อพื้นที่ "Area 1 ชั้น 1" ไม่เปลี่ยน ชื่อเตือนของการสแกนผิดที่ในประวัติตามเหตุจริง (สแกนผิดพื้นที่ / สแกนผิดตึก / สแกนนอกกะ) ไฟล์ Excel ที่เคยทำสูตรอ้างหัวคอลัมน์เดิม ("Area", "GPS", "Issue", "ข้อบกพร่อง") ต้องแก้หัวคอลัมน์ตาม
```

`facility-0062`: append a line that records the main JS gzip size measured in Step 3 below, in the style of the earlier amendments in that file. Example:

```markdown
> **แก้ไข 2026-10-22 (แผน 13):** วัดหลังแผน 13 ได้ <size> kB gzip จาก 200 kB
```

- [ ] **Step 2: Architecture and handoff**

In `docs/architecture.md`, in the line that lists the endpoints (`Ep["Endpoints/*<br/>/api/service-points (Admin) | /api/scan-records | /api/attendance | …`), change `/api/attendance` to `/api/attendance (+ /signs/{token})`.

In `docs/handoff.md`:

1. In the Status table, change the plan 13 row's status to `done on branch \`feat/ux-fix-list\` (not merged): \`docs/superpowers/plans/2026-10-22-ux-fix-list.md\``.
2. Replace the whole section `## Next: the UX fix list`, from its heading to just before `## Owner's rules (still in force)`, with:

```markdown
## Next: after plan 13

Plan 13 built the UX fix list (facility-0074 as amended; facility-0075 to 0085), branch `feat/ux-fix-list`. Report the plan's decisions 1–6 to the owner in the PR. Still to check by eye on real phones: the device's font (facility-0072), the plain-http message, and the check-in page's sign check over the tunnel.
```

3. In `## Pending work (ask the owner before starting)`, replace the plan 13 line with `- Plan 13 (UX fix list): the owner's OK for the push and PR of \`feat/ux-fix-list\`.`
4. In `## Parked; not needed before merging`, delete the bullet that starts with `- ThaiLabels.AlertName(BlockedScan) is still 'สแกนนอก Area'`. Task 11 fixed it.
5. In `## Notes for the frontend plan`, delete the bullet `- A Blocked Scan alert of a Supervisor still says 'สแกนนอก Area' in the history summary; show the alert's \`blockReason\` on the page instead.` and add:

```markdown
- Screen words follow facility-0085 (พื้นที่, แอดมิน, ตำแหน่ง, ไกลป้าย, เลยเวลา, ส่งเพิ่ม (ไม่นับรอบ), สิ่งที่ต้องแก้, สร้าง QR ใหม่ …); code, API and database names keep the glossary words. New text uses the screen words.
- `GET /api/my-work` sends `ownShift` (facility-0077); `GET /api/attendance/signs/{token}` checks a check-in sign on open (facility-0082). D3 reads a shift ahead with the board's `shiftDate` and `shift` (facility-0084).
```

- [ ] **Step 3: Final checks**

Run, from the repo root:

```bash
dotnet test backend/FacilityRealtime.slnx
cd frontend && npx vitest run && npm run lint && npx tsc -b && npm run build && cd ..
```

Expected:
- backend: unit 302 passed; API 540 passed, 5 skipped
- frontend: 398 passed; lint and tsc clean
- `npm run build`: the main `index-*.js` ≤ 200 kB gzip. Write its size into the facility-0062 amendment, then `rm -rf frontend/dist`.

Then check the rules:

```bash
cd frontend/src
grep -rn "style={" . --include='*.tsx'                                     # expect nothing
grep -rnE "localStorage|sessionStorage|indexedDB" . --include='*.ts*'      # expect nothing
grep -rnE "toLocale(String|TimeString|DateString)" . --include='*.ts*' | grep -v thaiTime   # expect nothing
grep -rnE "to=\{?[\"'\`]/(scan|check-in)/" . --include='*.tsx'            # expect nothing (facility-0059)
```

- [ ] **Step 4: Commit the docs**

Add the handoff entry:

```markdown
- 2026-10-22 · Task 12 done: ADR amendments (0062 measured size, 0074, 0077, 0082, 0083, 0084, 0085), architecture endpoint list, handoff Next and Notes; the parked "สแกนนอก Area" item removed. Unit 302; API 540 + 5 skipped; frontend 398; main JS <size> kB gzip. Next: the controller's real run, then the whole-branch review.
```

```bash
git add docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md docs/adr/facility-0074-ux-pass-becomes-a-fixed-fix-list.md \
  docs/adr/facility-0077-off-shift-pages-show-own-shift-time.md docs/adr/facility-0082-unusable-check-in-sign-told-on-open.md \
  docs/adr/facility-0083-round-window-time-split-hour-minute.md docs/adr/facility-0084-advance-cover-picks-area-from-searchable-list.md \
  docs/adr/facility-0085-ui-words-from-the-users-not-the-glossary.md docs/architecture.md docs/handoff.md
git commit -m "docs: plan 13 ADR amendments, architecture and handoff"
```

- [ ] **Step 5 (controller only, not a subagent): the real run**

1. **Start the API.** Follow `## Sharing a test build` in the handoff:
   - `cd frontend && npm run build:api`
   - create the throwaway database `facility_ux_test` (on this Mac MySQL has no root password; if it asks, ask the owner)
   - in the API's shell set `ConnectionStrings__DefaultConnection` to it, `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://localhost:5001`
   - run `dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api`
2. **Script checks.** Use a Python script (`python -X utf8`) as the seed users:
   - Cleaner `E1001`:
     - `GET /api/attendance/signs/token-checkin-ar01` is 204
     - `…/signs/token-restroom-m1` and `…/signs/nope` are 404 "ไม่พบป้ายลงเวลานี้"
     - `GET /api/my-work` has `ownShift` `{ "shift": "Day", "opensAt": "06:00:00", "closesAt": "20:00:00" }`
   - Cleaner `E1002` (Night) during the day: `attendance` has no `shift`, and `ownShift.opensAt` is `"18:00:00"`.
   - Admin:
     - `POST /api/admin/export` for today: the first sheet's header row has "พื้นที่" and "ตำแหน่ง"
     - `GET /api/admin/attendance?shiftDate=<tomorrow>&shift=Day` lists every Day Area with its regular Cleaner
     - a cover for tomorrow's Day shift is 201
   - The API log has no warnings.
3. **The owner's Chrome checklist** on `http://localhost:5001`:
   1. As `E1001`, open `/check-in/token-checkin-ar01`. The "วันนี้" row has no "ถัดไป". Press เข้างาน.
   2. Open `/check-in/nope`: "ป้ายนี้ใช้ไม่ได้แล้ว", no button.
   3. As `E1002` (night) during the day:
      - My Work shows "ตอนนี้ไม่อยู่ในกะของคุณ / กะดึก 19:00–07:00 / ลงเวลาเข้างานได้ตั้งแต่ 18:00", no cards
      - the check-in page shows "ลงเวลาได้ตั้งแต่ 18:00 ถึง 08:00"
   4. As `E1001`, on a point sign, pick พบปัญหา and press ส่งงาน. The warning shows, then goes when a tag is ticked. After sending, the status is one line.
   5. As Admin, on D3, pick "กะเช้า <tomorrow>":
      - every Area is listed with แม่บ้านประจำ
      - the search narrows the list
      - assign a cover; it shows under "คนทำแทน…"; cancel it
   6. On D6, "แก้เบอร์โทร" shows name, employee ID, role and place, and "ไม่ต้องรู้เบอร์เดิม".
   7. On D4, add a window by typing 10, 30, 12 into the boxes.
   8. Look over every page: no "Area", "Admin", "GPS", "ติดธง" in the text. Print a sign: it says "พื้นที่" and "สร้าง QR".
4. **Clean up.** Stop the API, drop `facility_ux_test`, `rm -rf backend/src/FacilityRealtime.Api/wwwroot`.
5. **Record and review.** Add a handoff entry with the results, commit it, then run the whole-branch review (sp-subagent-driven-development's final review).

