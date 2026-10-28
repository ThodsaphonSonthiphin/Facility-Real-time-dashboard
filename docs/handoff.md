# Handoff: status and next steps

Read this first on a new machine or in a new session. It is updated after **every** task (owner's rule).

This repo is public: never put a password, phone number, real name or any secret in this file.

Last updated: 2026-10-28, late (**performance measured, then build-time compression and SignalR in its own file: the owner passed it on a phone through the tunnel, merged; see the first section**); earlier: 2026-10-28, evening (**compressed frontend files and a quieter live badge, merged**); earlier: 2026-10-27, night (**the first Admin (facility-0092) is done on branch `feat/first-admin` (HEAD after this handoff commit; code `d74ecbe`..`cbb797b`), final whole-branch review clean after one fix round, the real run on MySQL passed, not pushed. **The owner passed the test on their machine (2026-10-27): first start, restart warning, D9 "ระบบ", `reset-admin` typed by hand with hidden typing. Test build taken down. Merged to master as PR #107 (`140c2db`) on the owner's OK, branch deleted. Next work: the deploy guide, written into `docs/handover.md`, then the pilot**); earlier: 2026-10-27, evening (**the check-in scroll fix merged to master as PR #106 (`04bbc14`) on the owner's OK. Next work: the first Admin on a real server (facility-0092): ADR committed and plan written (`docs/superpowers/plans/2026-10-27-first-admin-from-server-settings.md`) on branch `feat/first-admin`, not started; the owner chose by popup that the history shows the actor of these rows as "ระบบ" (nullable `audit_log.actor_id`)**); earlier: 2026-10-27, later (**explanations behind ℹ on the remaining pages: done on branch `feat/more-info-rest`, final whole-branch review clean, the owner passed it on 2026-10-27; merged to master as PR #105 (`9e0a261`), see `## Info-link branch`. Then the check-in page scroll the owner reported: fixed on branch `fix/check-in-scroll` (`100svh` instead of `100dvh`), the owner passed it on a phone; push, PR and merge on the owner's OK, see `## The check-in page scroll`. Next: the plan for the first Admin (facility-0092), not started; the owner asked to see it before any work. Issue #37 closed. The owner confirmed the #81 paper print and the plan-13 phone checks had passed long before**); earlier: 2026-10-27 (**the owner tested `feat/admin-buildings`: D9 history showing the building rows, a refused turn-on of an Area in an off building and a refused turn-on of a Supervisor of an off building all passed, and moving AR01 into an off building passes by design; three problems found and fixed by facility-0091 (building names on Admin pages and the Excel cell, an Area is turned off only once no active Cleaner holds it). the owner passed all 5 re-checks; merged to master as PR #104 (merge commit `c210603`) on the owner's explicit OK (2026-10-27), see `## Earlier: test the buildings branch`**); earlier: 2026-10-26, end of day (**Buildings managed by Admin (facility-0090): done on branch `feat/admin-buildings` (HEAD `e28a976`), final whole-branch review clean, committed, not pushed. Next session: the owner tests it. See `## Earlier: test the buildings branch` just below**; earlier: **#101 the Admin password rule (15–100 characters, not a common password) merged to master as PR #103 (`3a6323b`) after the owner's D6 check**; earlier: **#82 the contract step merged to master on the owner's OK (branch `feat/contract-preflight` deleted): preflight on, `shared.css` and every `app-*` class and `--old-*` variable gone; the owner's eye checks and D5 paper print passed, nothing owed on #82; the admin password rule (15 characters) is the next open item, see `## Pending work`**; earlier: **#81 D5 print signs moved to Tailwind, merged to master on the owner's explicit "handoff commit push PR merge"; a real paper print and the owner's 1280 px check are owed, see the #81 entry under `### Start here`**; earlier: **three PRs merged to master on the owner's explicit "handoff commit push PR merge": #96 D6–D9 in the new look (`edf2418`), #98 D7 leaves out superseded submissions and renames its column to "จำนวนครั้งที่ตรวจ" (`07319cd`), #97 My Work updates live for Cleaners (`d4e8fde`). The eye checks of #79 (D4, D10, M17), #80 (D6–D9) and #97 (two phones) were NOT finished and are still owed (since passed, owner, 2026-10-26) — see `### Waiting on the owner's eye check`**; earlier: #70–#79 merged; 2026-10-25 wrote `docs/code-map.md`, the file-level map for the owner)

## Performance: measured, then fixed (2026-10-28; branch `perf/measure-baseline`, merged on the owner's "commit, push, PR, merge")

The owner asked to measure first, then plan and fix. Skills installed **globally** on the owner's Mac (not in this repo), by the owner's choice: `npx skills add addyosmani/web-quality-skills -g -a claude-code -s '*'` (performance, core-web-vitals, web-quality-audit, accessibility, seo, best-practices). The owner chose by popup: the symptom is "slow through the tunnel, fast on localhost"; measure a production build with phone emulation.
- **How it was measured (repeatable):** production build (`npm run build:api`) on throwaway `facility_perf_test`, API in Development on port 5001 (`-c Release --no-launch-profile`); `npx lighthouse@13.6.0 <url> --only-categories=performance --form-factor=mobile` (simulated slow 4G, 4x CPU, cold cache), 3 runs per page, median. Logged-in pages: log in with `curl` to `/api/auth/login` and pass the `facility_refresh` cookie with `--extra-headers` (a fresh cookie per run).
- **Before → after (median of 3):** pages before login 226 → 135 KB, LCP 0.75 s both; D1 `/dashboard` 235 → 156 KB, LCP 2.54 → 2.34 s; My Work 229 → 151 KB, LCP 2.49 → 2.34 s. CLS unchanged: My Work 0.10 (its footer moves when the data arrives), D1 0.04 ("วิธีอ่านการ์ด"); not fixed, the owner did not pick it.
- **What changed:** `frontend/precompress.ts` (Vite plugin) writes `.br`/`.gz` at the highest levels beside each text file of the build; `PrecompressedFiles.cs` sends that copy (main JS br 212 → 99 KB, `favicon.svg` 9.5 → 1.2 KB); `scanHub.ts` loads `@microsoft/signalr` by dynamic import (main JS 535 → 383 KB raw). ADR facility-0067 amended, `docs/code-map.md` updated. 3 new backend tests, 2 new frontend tests (both fail on the old code).
- Tests: backend 343 unit + 636 API (5 MySQL-only skipped); frontend 459; tsc and eslint clean.
- Known and kept: pages ask their data again once SignalR goes live (`onMissed` on first start, facility-0064) by design.
- **Through the owner's quick tunnel (same Lighthouse settings, median of 3; the before build was master in a temporary worktree on the same port and tunnel):** LCP `/login` 2.13 → 1.66 s, check-in 2.05 → 1.64 s, D1 2.07 → 1.61 s, My Work 2.06 → 1.62 s; bytes about 237 → 153 KB. Cloudflare asks our server for gzip only, so through the tunnel the main JS goes as the `.gz` copy (230 → 114 KB), not `.br`. A bare request through the tunnel takes about 0.17–0.2 s from this Mac.
- **The owner tried a phone through the tunnel (2026-10-28): faster, works normally** (live updates included). Merged on the owner's OK by popup.
- **State left:** test build taken down (API stopped, `facility_perf_test` dropped, `wwwroot` deleted). The owner stops their own `cloudflared`. Lighthouse JSON files were in the session scratchpad only.
- Open, not picked by the owner: My Work's CLS 0.10 (footer) and D1's 0.04 ("วิธีอ่านการ์ด").

## Compressed frontend files and a quieter live badge (2026-10-28; branch `feat/response-compression`, merged on the owner's "handoff push pr merge")

The owner wiped the dev database and walked the system by hand from `npm run build:api` and `dotnet run`, with `cloudflared` in front. Two findings:
- **Slow over the quick tunnel, fast on localhost:** the API sent the 535 KB main JS uncompressed (about 160 KB gzipped). Now `UseResponseCompression` (gzip and brotli, also over https) runs for the frontend's files only; `/api` and `/hubs` are left out on purpose (answers hold tokens: BREACH). 4 new tests in `FrontendHostingTests`. What stays slow is the quick tunnel itself (an experimental service, extra latency per request); the owner has not yet confirmed the speed-up on a phone or through the tunnel.
- **"SignalR Live" meant nothing to an Admin (owner's choice by popup):** `LiveBadge` now shows nothing while the hub is live and the orange `กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)` only while it is not; the `role="status"` region stays (empty) for screen readers. ADR facility-0064 amended, `docs/code-map.md` updated.
- Tests: backend 343 unit + 631 API (5 MySQL-only skipped); frontend 457; tsc and eslint clean.
- **State left:** API stopped, `wwwroot` deleted. The dev database `facility_dashboard` was NOT reset: it holds the owner's walkthrough rows (6 accounts, 3 history rows). To get the seed back, drop and recreate it and start the API once in Development. The owner's own `cloudflared` may still be running; they stop it.
- **Not done:** the walkthrough stopped before step 4 (create the buildings, Areas, points, accounts and print signs from zero). Pick it up from `## Sharing a test build`; the owner wants to be walked step by step, one step at a time, starting from the build and the run, not from a running server.

## Server-side search for the Admin history (D9, done 2026-10-28; merged to master as PR #110 on the owner's "push pr merge", branch deleted)

Branch `feat/history-server-search` (from `docs/handoff-cleanup`, PR #109). The owner chose by popup (2026-10-28): the server searches **summary, actor name and reason** (the Thai type name is left to the ประเภท filter), and the words are sent with **Enter or แสดง**, kept in the address as `?q=`.
- `GET /api/admin/audit-log?q=` (trimmed, at most 100 characters, else 400): LIKE with `!` as the escape (MySQL reads a backslash in a string literal as its own escape: the first try failed on real MySQL), so `%` and `_` are plain text; the actor "ระบบ" matches rows the server wrote. Paging by `beforeId` works inside a search.
- D9: the browser-only search (`searchEntries`) is gone; the count line says `แสดง N รายการที่ตรงกับ "…"`; an empty search result says `ไม่พบรายการที่ตรงกับที่ค้นหาในช่วงนี้`.
- On MySQL (`utf8mb4_0900_ai_ci`) the search ignores letter case and Thai tone marks (`ไม่` also finds `ไม้`); SQLite in the tests ignores letter case only.
- Tests: backend 343 unit + 632 API with the MySQL tests on throwaway `facility_search_test` (dropped), 0 skipped; frontend 457; tsc and eslint clean.
- The owner tried the build on 2026-10-28 and gave the OK to merge (#109 first, merged as `afde3a9`, then #110). The D9 search box itself was not walked by the agent in a browser; if the owner finds a problem, fix it as a small branch.
- **Test build taken down (owner, 2026-10-28):** API stopped, `wwwroot` deleted, `facility_dashboard` dropped and recreated, then the API started once in Development so the seed ran (seed Admin logs in, 200; 5 accounts, 0 history rows) and stopped again. The owner stops their own `cloudflared` (none was running when checked).
- **D5 print passed (owner, 2026-10-28):** the PDF saved from Chrome through the tunnel (1 A4 page, 5 signs) printed on a colleague's printer at work. The first try did not reach the printer; the file was fine (checked), the cause was on the printer side.

## Safe start, `reset-admin` confirmation, start script and deploy guide (facility-0093, done 2026-10-28; merged to master as PR #108, `447e782`, branch deleted)

**Status (2026-10-28):** branch `feat/safe-start` (from master `140c2db`), Tasks 1–4 of the plan done (`6c323e7`..`1fad766`), each task reviewed, final whole-branch review clean after one fix wave. Backend 343 unit + 624 API pass (5 MySQL-only skipped); `bash scripts/facility.test.sh` 43 checks pass on macOS bash 5 and 3.2. **Task 5, the real run, passed (2026-10-28)** on `facility_deploy_test` (the owner made the database, its own user and the env file with `facility.sh init`): `start` created the first Admin, `/login/admin` and the login answered 200; without the setting both the startup and `reset-admin` stopped with `ConnectionStrings__DefaultConnection is not set`; `reset-admin` answered `no` showed `localhost:3306 / facility_deploy_test` and exited 1 with no history row, answered `yes` reset the password (row `ADMIN_RECOVER_RESET` by "ระบบ", new password logs in). The plan's step 6 (start Development on `facility_dashboard`) was skipped by the owner's rule; `DatabaseSettingsTests` pins that default. API stopped, `~/.facility/deploy-test.env` deleted. **Owner still to drop `facility_deploy_test` and user `facility_deploy_app`** (needs the root password). Push, PR and merge on the owner's explicit "push pr then merge" (2026-10-28).
- **Next work:** the pilot's setup on a real server with this guide (`docs/handover.md` §2), once IT names the server and domain; the server type is still open (Linux written, Windows not).
- What changed against the plan (rulings, all in the PR): script messages are Thai; `init` and the loader refuse `'` `"` `\` anywhere and spaces at the ends of a value (systemd reads them differently); `init` writes a temp file then moves it; `reset-admin`'s whole command-line path is `ResetAdminCommand.RunFromCommandLineAsync` (usage, username format, `yes`, then migrate), so the order is tested; `facility.sh start` `cd`s to the dll's folder (content root); backups are time-stamped and never overwrite.
- **On the first Linux server:** run `bash scripts/facility.test.sh` there; the GNU `stat` path has only been read, never run (`docs/handover.md` §2.3 says so too).
- Deferred minors (accepted by the final review): `Describe` does not know the connector aliases `Address`/`Addr`; a failed `gzip` leaves an uncompressed `.sql`; `start` does not re-check a hand-edited relative `FACILITY_APP_DLL`.

The owner chose (2026-10-28, by popup) to fix what went wrong in the trial run before the deploy guide, and to reply in English from now on.
- Owner's choices: the server type is **not decided** (the guide is written for Linux, Windows stays open); secrets live in **one env file outside the repo** (`chmod 600`, the same format as a systemd `EnvironmentFile`); `reset-admin` **prints the server and database and waits for `yes`**.
- ADR `docs/adr/facility-0093-safe-start-and-deploy-guide.md` and plan `docs/superpowers/plans/2026-10-28-safe-start-and-deploy-guide.md` written, not committed, no branch yet. The plan's "Decisions made while planning" (Caddy, a daily `mysqldump`, `yes` in any case, `FACILITY_APP_DLL`, no `;'"` in the MySQL password) are the owner's to overrule.
- From the trial: everything cleaned up and `facility_dashboard`'s seed Admin restored (2026-10-28), see `## Trial run on the owner's Mac`.

## Trial run on the owner's Mac (owner, 2026-10-27, stopped)

The owner asked to try the system for one Area on their own Mac before the deploy guide. The owner chose by popup: **a temporary trycloudflare link** (no domain yet; the QR signs hold the full address, so signs must be reprinted whenever the link changes) and **made-up data only** (no real Cleaner names or phones on the Mac).
- Prepared: throwaway database `facility_pilot_trial_test`, `npm run build:api` done on master `140c2db` (`wwwroot` filled).
- Run in Production, not Development (no seed, so no public Admin password): `FirstAdmin__*` creates the Admin; `Jwt__SigningKey` random; `Jwt__RefreshCookieSecure=true` because phones reach it over https through the tunnel.
- Then on the web pages: building, one Area, its points, made-up Cleaner and Supervisor accounts, print the signs from D5, test with phones.
- A domain plus a named Cloudflare tunnel is the step to permanent signs; it belongs in the deploy guide.
- Afterwards: stop the API and `cloudflared`, drop `facility_pilot_trial_test`, delete `wwwroot`.
- **Stopped on the owner's word (2026-10-27).** API and tunnel stopped; `facility_pilot_trial_test` (Admin `admin`, nothing else set up yet) and `wwwroot` kept until the owner says otherwise. **Cleaned up (owner, 2026-10-28): `facility_pilot_trial_test` and `facility_deploy_test` (with its user) dropped by the owner, `wwwroot` and the stray empty root `package-lock.json` deleted. Nothing from the trial is left on the Mac except the developer database `facility_dashboard`, whose seed Admin password was changed by the 16:09 `reset-admin`. **Restored (2026-10-28, on the owner's OK by popup): `facility_dashboard` dropped and recreated, the API started once in Development so the seed ran again; the seed Admin logs in again (200). The old dev rows (5 accounts, 7 history rows, test data) are gone.**
- **What went wrong, fix before the next try:** the owner ran the steps across several Terminal windows; `export`ed values live in one window only. A window without `ConnectionStrings__DefaultConnection` silently used the fallback `facility_dashboard`: one `reset-admin admin` (16:09) changed the seed Admin's password in the developer database `facility_dashboard` (test data only; restore it, e.g. re-run the seed on a fresh dev DB), and a start there failed on the missing `Jwt__SigningKey`. Planned with the deploy guide: in Production, refuse to start or run `reset-admin` without a connection string; `reset-admin` prints the server and database before acting; one start script so the owner never types `export`s; the guide warns about the Thai keyboard when typing a hidden password.

## First-Admin branch (owner passed 2026-10-27; merged as PR #107, `140c2db`)

**The owner ran steps 1–6 below on their machine and passed them all (2026-10-27).** The API is stopped, `facility_firstadmin_test` dropped and `wwwroot` deleted.

Branch `feat/first-admin`, not pushed. What it does: a server with an empty database gets its first Admin from `FirstAdmin__Username`, `FirstAdmin__Password` and `FirstAdmin__DisplayName`, only while no Admin exists; `reset-admin <username>` recovers Admin access from the server's terminal; rows the server writes show "ระบบ" as the actor on D9 (migration `SystemAuditActor`); the Thai guide for whoever takes the system on is `docs/handover.md`.

- Tests: backend unit 343, API 603 passed + 5 MySQL-only skipped after the last fix (one test added); before it, the full run with the MySQL tests on a throwaway database passed 607 with 0 skipped; frontend 457; tsc and eslint clean.
- Controller's real run (Production, throwaway `facility_firstadmin_test`, port 5001), all passed: first start creates the Admin, logs no password, login 200, D9 API shows `ระบบ`; a restart creates nobody and warns the password is still set; `short-pass` stops the startup naming `FirstAdmin__Password` without the value; nothing set starts and warns; `reset-admin` with no `Jwt__SigningKey` resets `first.admin`, creates `backup.admin` with a Thai display name, exits 2 with no username, and never prints the password.
- `npm run build:api` was run, so `wwwroot` holds the build (git-ignored). `facility_firstadmin_test` still exists (holds `first.admin` and `backup.admin` with passwords that were only in shell variables, now gone): recreate it for the owner's test.
- `dotnet ef migrations add` needs `backend/src/FacilityRealtime.Api/wwwroot` to exist (an empty folder is enough), or the design-time build fails.

**The owner's test, in their own terminal from the repo root** (the owner types their own password; it never goes in a file):
1. `mysql -u root -e "DROP DATABASE IF EXISTS facility_firstadmin_test; CREATE DATABASE facility_firstadmin_test CHARACTER SET utf8mb4;"`
2. Set the server's values:
   ```bash
   export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_firstadmin_test;Uid=root;Pwd=;"
   export ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://localhost:5001
   export Jwt__SigningKey="$(openssl rand -base64 48)"
   export FirstAdmin__Username=first.admin FirstAdmin__DisplayName='แอดมินทดสอบ'
   read -rs FirstAdmin__Password && export FirstAdmin__Password
   ```
   (`read -rs`: type a password of 15+ characters, then Enter; nothing shows.)
3. `dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api`: the log says `Created the first Admin 'first.admin'`. Open `http://localhost:5001/login/admin`, log in as `first.admin`; D9 (ประวัติการแก้ไขของแอดมิน) shows a row "สร้างแอดมินคนแรกจากค่าในเซิร์ฟเวอร์" whose actor is "ระบบ".
4. Stop it (Ctrl+C) and start it again: no new account, and the log warns `FirstAdmin__Password is still set`.
5. Stop it, `unset FirstAdmin__Password Jwt__SigningKey`, then type by hand: `dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api -- reset-admin first.admin`. The typing of the new password must not show. Start the API again (set `Jwt__SigningKey` again): the new password works, and D9 shows "กู้คืนแอดมินด้วยคำสั่ง reset-admin" by "ระบบ".
6. Optional: `reset-admin backup.admin` asks for a display name first and creates a second Admin.

After the owner's test: stop the API, drop `facility_firstadmin_test`, delete `wwwroot`, then ask the owner about push, PR and merge.

**Deferred minors** (final review: none blocks merge): the reset summary could exceed 300 characters only with a username plus display name over about 215 characters; two instances starting at once on an empty database could race (single server); no test that a failed audit save rolls back the created Admin, or that an Admin-exists start without the password logs nothing; `ServerAdminAccounts` writes its own snapshot shape instead of D6's `AccountSnapshot`; piped input to the display-name prompt prints no newline; the NU1903 advisory on `Microsoft.AspNetCore.SignalR.Common` 10.0.12 (older than this branch); `reset-admin` does not print which database it changed and falls back to localhost when the connection string is missing (do it with the deploy guide); a Thai display name typed on a Windows console may need `Console.InputEncoding` (the server's OS is not decided).

## The check-in page scroll (owner, 2026-10-27; fixed, the owner passed it on a phone; merged as PR #106, `04bbc14`)

The owner reports that on the Cleaner's check-in sign page (`/check-in/<token>`, M1/M10), on a phone, the "เข้างาน" and "ดูงานของฉัน" buttons seem to move with the scroll in an odd way when the page is scrolled up and down. The owner could not describe it more exactly.

**Cause (confirmed by the owner's phone check):** the page's `<main>` had `min-h-[calc(100dvh-6.25rem)]` since #75 (`8ee2167`, 2026-10-22), and its button group has `mt-auto`, so the buttons sit at the bottom of a box as tall as the visible screen. A phone hides its browser toolbar as the page scrolls; `dvh` grows with it, the box grows, and the buttons (with ℹ and "ดูงานของฉัน") slide down while the heading scrolls normally.
- This Mac has no iOS simulator or Android emulator, so it was measured in headless Chrome at phone widths: when the viewport grows by the toolbar's height (iPhone 81 px, Android 56 px, iPhone SE 82 px), "เข้างาน" moves down by exactly that and the heading "ลงเวลา" does not move. The page's content (452 px) is shorter than the box at all three sizes, so the box height always applies. Desktop Chrome has no toolbar, so it cannot show the fix working.
- **Fix on branch `fix/check-in-scroll` (`e12702b`), pushed, PR and merge on the owner's explicit OK (2026-10-27):** `100svh` instead of `100dvh` (the screen with the toolbar shown, which does not change while scrolling). `design-system/facility/pages/phone.md` now says heights in the page's flow use `svh`, never `dvh`. Frontend 456 tests pass; no automated test can see this (jsdom has no layout).
- **Owner's check on a phone: passed (2026-10-27).** Test build taken down (API stopped, `facility_scroll_test` dropped, `wwwroot` deleted). How it was run: test build on `facility_scroll_test`, port 5001; the owner runs `cloudflared tunnel --url http://localhost:5001`. Log in as the seed Cleaner E1001 (its phone is in `DbInitializer.cs`), open `<tunnel>/check-in/token-checkin-ar01`, scroll up and down: the buttons must now move with the page like the heading. Control: "ดูงานของฉัน" (My Work) never used `dvh`. If the check-in page still misbehaves after the fix, the cause is something else and the investigation starts again.

## Info-link branch (owner passed 2026-10-27; merged to master as PR #105, merge commit `9e0a261`, branch deleted)

Branch `feat/more-info-rest` (plan `docs/superpowers/plans/2026-10-27-explanations-behind-more-info.md`). Frontend 456 tests, lint and tsc clean; startup JS 162.10 kB gzip. Final whole-branch review: no Critical or Important finding. **The owner checked it on 2026-10-27 and said it passes**, which also accepts check 3 (the ℹ at the left) and check 4 (M17's https line behind ℹ). Test build taken down (API stopped, `facility_moreinfo_test` dropped, `wwwroot` deleted). The owner gave the explicit OK to push, open the PR and merge.

**Bring the test build up:** `git checkout feat/more-info-rest`, `cd frontend && npm install && npm run build:api`; throwaway DB `facility_moreinfo_test`; the API on it as in `## Sharing a test build` step 3 (port 5001). For a phone, the owner runs `cloudflared tunnel --url http://localhost:5001` in their own terminal.

**What the owner checks:**
1. Desktop 1280 px: D1 "วิธีอ่านการ์ด"; D2, D3, D4, ตึก "เกี่ยวกับหน้านี้"; D10 and the building Drawer "เกี่ยวกับการบันทึก"; D5 "เกี่ยวกับ QR". Each is closed, opens on click, and shows the old text. Lines that stay: D2 tile lines, D3 cover form, D4 sign help, D5 "ติดเสร็จให้สแกนทดสอบ 1 ครั้ง…".
2. Phone: check-in sign "เกี่ยวกับการลงเวลา", a point's scan page "เกี่ยวกับการส่งงาน", M17 "เกี่ยวกับหน้านี้". The ℹ is easy to tap.
3. Decide: the old lines on check-in and scan were centred; the ℹ now sits at the left. On an iPhone, check whether a small ▸ shows beside the ℹ (one class fixes it).
4. Decide: M17's "ต้องเปิดผ่าน HTTPS" is now behind ℹ. On plain http the page says so only after "ใช้ตำแหน่งตรงนี้" is pressed, not on open. Keep it behind ℹ, or bring it back into view?

Known, not from this branch: `src/shared/api/scanHub.test.ts` failed once in the pre-commit run under load (expected 2 to be 1) and passed alone and on retry; a flaky timing test.

**Afterwards:** stop the API, drop `facility_moreinfo_test`, delete `wwwroot`; ask the owner by popup about push, PR and merge; update this file.

## Earlier: test the buildings branch (done, merged as PR #104)

The owner tested `feat/admin-buildings` on 2026-10-27 and this plan (facility-0091) fixed what they found. The owner then passed all 5 re-checks below on the running test build, and gave the explicit OK to push, open the PR and merge. Nothing is owed on this branch. The test API (port 5001) is stopped, its `wwwroot` deleted, and the throwaway database `facility_buildings_test` dropped (owner's OK). The branch is merged as PR #104 (`c210603`).

**Owner test, 2026-10-27:**
- Passed: D9 history shows the building rows under its "ตึก · พื้นที่ · จุด · รอบ · ป้าย" filter; turning on an Area that is in an off building is refused; turning on a Supervisor of an off building is refused.
- Moving AR01 into an off building passes by design: the Area form does not offer an off building, and the server refuses too.
- Found and fixed by facility-0091 (plan `docs/superpowers/plans/2026-10-27-building-names-and-area-close.md`):
  - D4 kept showing the building code after a rename. Now every Admin page and picker (D1, D2, D3, D4, D6, D7, D8) shows `code · current name`; phones and printed signs keep the code.
  - D8 showed codes only and the owner did not see the off building. The D7/D8 picker now asks fresh on every open; D8 lists the off building as `C · name (ปิดใช้งาน)`; the Excel "ตึก" cell is `code · current name`.
  - Closing building C left its Cleaner on an Area that was off. An Area is now turned off only once no active Cleaner holds it (`AreaRules.DeactivateProblem`, answered 409 with the Cleaners' names); the Area form disables its button and names them.
- Commits since `e28a976`: `9cce108`, `eb58e01`, `d8dd5e3`, `04729ae`, `c5e0c14`, `8ad44d7`, `7934192`, `635ed61`, then the two docs commits `45231ee` and `b9a1fa8` (ADR facility-0091), then the final fix wave (`3d4a2ff`: the Area form refuses a new Cleaner on an Area that is off, plus docs).

**State (2026-10-27):**
- Branch `feat/admin-buildings`. Nothing is pushed and there is no PR.
- Tests: backend unit 340; API 581 passed, 5 skipped (MySQL only); frontend 446; tsc and eslint clean. `npm run build:api` was run, so `backend/src/FacilityRealtime.Api/wwwroot` holds the new build (git-ignored).
- The test API has been restarted on the new build (port 5001, database `facility_buildings_test`, the owner's test data kept). The owner runs `cloudflared tunnel --url http://localhost:5001` in their own terminal.
- Data written before facility-0091 may still hold a Cleaner on an Area that is off (test databases only; no migration).

**Bring the test build up** (if the machine was rebooted; details in `### Start here` → "Eye check on a real phone"):
1. `git checkout feat/admin-buildings`, then `cd frontend && npm install && npm run build:api`.
2. Create a throwaway DB `facility_buildings_test`. On this Mac, MySQL root has no password.
3. Start the API on it with `--no-launch-profile` on port 5001 (see `## Sharing a test build`). The Development seed creates building A with AR01 and AR02 on, Supervisor S2001, and admin / admin1234.
4. **The owner runs `cloudflared tunnel --url http://localhost:5001` in their own terminal.** The agent cannot.

**What the owner re-checks (desktop, 1280 px):**
1. Rename building A on "ตึก", then open "จัดการพื้นที่และป้าย": the list line and "พื้นที่ที่เลือก · ตึก A · <new name>" show the new name.
2. D8 "ส่งออก Excel": the picker shows `A · <name>` and the off building as `C · <name> (ปิดใช้งาน)`; export one sheet and see `A · <name>` in the "ตึก" column.
3. D7, D6, D2, D1: the building pickers and texts show `A · <name>`.
4. Open the Area form of AR01 (it has Cleaners): "ปิดใช้งานพื้นที่นี้" is disabled and names the Cleaners. Take them out in the form, save, open แก้ไขพื้นที่ again, and the button works.
5. On an Area that is off, open its form: the Cleaner pickers offer "— ยังไม่มี —" and only the Cleaner who already holds the shift, and a note says to turn the Area on first.

**Afterwards:**
- Stop the API, drop `facility_buildings_test`, and delete `wwwroot`.
- Ask the owner by popup whether to push, open the PR and merge. Do none of these without the owner's explicit OK.
- Update this file.

## Project

A factory cleaning-inspection system: 6 buildings, 70–72 Areas and about 170 housekeepers.

- Backend: .NET 10 Minimal API (Clean Architecture), EF Core 10, SignalR, xUnit.
- Runtime uses Oracle `MySql.EntityFrameworkCore` (MySQL 8.0); tests use SQLite in memory.
- The ADRs in `docs/adr/facility-NNNN-*.md` are the main spec. Where they conflict with a wireframe, the ADR wins.
- Plans live in `docs/superpowers/plans/`.

## Status

| Plan | Scope | Status |
|---|---|---|
| 1 | Data model | merged (PR #50) |
| 2 | Attendance + scan rules | merged (PR #51) |
| 3 | Admin setup: Areas, points, round windows, sign location, radius, new QR | merged (PR #52) |
| 4 | Attendance page (D2), alert list (D3), Admin history (D9) | merged (PR #53, `7cd8ba4`) |
| 5 | Accounts page (D6) + Excel export (D8) | done, merged (PR #54, `c530c84`) (branch `feat/accounts-export`): `docs/superpowers/plans/2026-10-14-accounts-and-excel-export-backend.md` |
| 6 | Supervisor inspection, covers, My Building, alerts, export sheet, summary | done, merged (PR #55, `d9b4374`) (branch `feat/supervisor-inspection`): `docs/superpowers/plans/2026-10-14-supervisor-inspection-backend.md` |
| 7 | Frontend foundation: tree, store, API layer, session, router and guard, login pages, served by the API | done, merged (PR #56, `43f10a3`) (branch `feat/frontend-foundation`): `docs/superpowers/plans/2026-10-15-frontend-foundation.md` |
| 8 | Cleaner phone: My Work, check-in sign page, point sign page with send failures and blocked scans | done, merged (PR #57, `96895d8`) (branch `feat/cleaner-phone`): `docs/superpowers/plans/2026-10-15-cleaner-phone.md` |
| 9 | Supervisor phone: My Building with live updates, inspection at the point's sign, nothing to inspect, blocked and send failures | done, merged (PR #58, `84890df`) (branch `feat/supervisor-phone`): `docs/superpowers/plans/2026-10-15-supervisor-phone.md` |
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | done, merged (PR #59, `a6dc170`) (branch `feat/admin-setup`): `docs/superpowers/plans/2026-10-19-admin-setup.md` |
| 11 | Admin live board: lazy Admin routes, point status with hub and 30 s poll (D1), attendance and corrections (D2), alerts and covers (D3) | done, merged (PR #60, `f897548`) (branch `feat/admin-live-board`): `docs/superpowers/plans/2026-10-19-admin-live-board.md` |
| 12 | Admin reports: inspection summary (D7), Excel export (D8), Admin history (D9) | done, merged (PR #62, `d9ac984`) (branch `feat/admin-reports`): `docs/superpowers/plans/2026-10-21-admin-reports.md` |
| 13 | UX fix list: facility-0074 as amended (facility-0075 to 0085) | done, merged (PR #66) (branch `feat/ux-fix-list`): `docs/superpowers/plans/2026-10-22-ux-fix-list.md` |

Plan 4 test results at merge:
- Unit tests: 215 passed.
- API tests: 332 passed, 3 skipped (the MySQL tests need `FACILITY_MYSQL_TEST`).
- On real MySQL, all passed: migrations, the MySQL tests, and an 18-case API script.

## Task log (plan 5)

- 2026-10-14 · Owner approved: merge PR #53, scrub the MySQL password from the plan-2 file, delete the leftover pilot branches, write plan 5.
  - PR #53 merged (`7cd8ba4`); its branch was deleted locally and on GitHub.
  - The two pilot branches (`feat/pilot-data-model`, `feat/pilot-scan-rules`) no longer existed on GitHub; nothing to delete.
  - The password is replaced with `<local root password>` in `docs/superpowers/plans/2026-10-09-pilot-scan-rules-backend.md` (lines 33, 1030–1032).
  - The plan-5 file was written; this handoff was rewritten in English (the owner now prefers English).
  - Branch `feat/accounts-export` created from master `7cd8ba4`. The owner chose subagent-driven execution.
  - Next: plan 5 Task 1.
- 2026-10-14 · Task 1 done: AccountRules, PhoneNumber.IsComplete, audit categories Account and Export. Unit and API tests pass. Next: Task 2 (account list and create).
- 2026-10-14 · Task 2 done: GET/POST /api/admin/accounts (list; add Cleaner, Supervisor or Admin; slot and login-name checks; ACCOUNT_CREATE). All tests pass. Next: Task 3 (edit, deactivate, activate).
- 2026-10-14 · Task 3 done: PUT /api/admin/accounts/{id}, /deactivate, /activate (deactivation revokes sessions and frees the Cleaner's Area; no self-deactivation; slot check on activation). All tests pass. Next: Task 4 (phone and password).
- 2026-10-14 · Task 4 done: PUT /api/admin/accounts/{id}/phone and /password (sessions revoked, nothing secret logged); test that the history never shows a phone, password, hash or QR Token. All tests pass. Next: Task 5 (export rules and query).
- 2026-10-14 · Task 5 done: ExportRules.MissedRounds and ExportQuery (attendance, submissions, points with windows; date range and building filter). All tests pass. Next: Task 6 (the .xlsx endpoint).
- 2026-10-14 · Task 6 done: POST /api/admin/export returns .xlsx (การลงเวลา, การส่งงาน with ไม่ได้ทำ rows; text-only cells; EXPORT_EXCEL logged). New package ClosedXML 0.104.2. All tests pass. Next: Task 7 (MySQL check and docs).
- 2026-10-14 · Task 7 done: a MySQL test of the export queries, the case-insensitive employee ID rule and the deactivation transaction (user change plus session revocation); README and architecture docs. The MySQL test compiles and is skipped without `FACILITY_MYSQL_TEST`; the controller runs it on real MySQL. All other tests pass (421 passed, 4 skipped in API tests; 260 unit tests). Next: the MySQL run, the whole-branch review, then a PR for the owner.
- 2026-10-14 · Final review fixes: (F1) the export's "ไม่ได้ทำ" rows now wait until the attendance window closes (ClosesMinutesAfterShift after shift end, inclusive), because a Late Submission is accepted until then; (F2) the workbook is built before the EXPORT_EXCEL row is saved; (F3) editing a Cleaner whose Area was deactivated keeps the Area, moving one to a deactivated Area still gets 400; (F4) test pins that PUT without `areaId` clears the Area, documented in README and the frontend notes; (F5) README audit categories and this file's roadmap line corrected. All tests pass (MySQL ones skipped). Next: owner runs the MySQL tests on the Mac, then a PR.
- 2026-10-14 · MySQL check on the Mac (Homebrew MySQL 26.7, not 8.0): the owner ran the whole suite with `FACILITY_MYSQL_TEST` set; 691 passed, 0 skipped (263 unit, 428 API including the 4 MySQL tests). The throwaway database was dropped and `facility_dashboard` is untouched. Next: push the branch and open the PR, with the owner's OK.
- 2026-10-14 · PR #54 opened (owner approved the push). Not merged. Next: the owner reviews PR #54 and rules on the parked items listed in it; after the merge, ask which plan comes next (see Pending work).

## Task log (plan 6)

- 2026-10-14 · Task 1 done: migration SupervisorInspection (inspection GPS and cover; Supervisor covers by building with a one-place CHECK); attendance board and My Work read Area covers only. All tests pass (MySQL skipped). Next: Task 2 (Supervisor rules).

- 2026-10-14 · Task 2 done: BlockReason OtherBuilding and OutsideShift; SupervisorScope.WithoutCover and InspectionRules.Find (latest submission of the current round, nothing while rework is owed). Unit tests pass. Next: Task 3 (the Supervisor's scan).

- 2026-10-14 · Task 3 done: POST /api/inspections/scan (the current round's submission, or why there is nothing to inspect; another building or outside the shift is a Blocked Scan unless covered); Blocked Scan answers carry reason and building. All tests pass. Next: Task 4 (record the inspection).

- 2026-10-14 · Task 4 done: POST /api/inspections (สะอาด / ไม่สะอาด with defect, GPS and cover stamped, 409 when the scanned submission is stale or a rework is owed); cards in ต้องแก้ไข show the defect; Admins get InspectionRecorded live. All tests pass. Next: Task 5 (Supervisor covers).

- 2026-10-14 · Task 5 done: POST /api/admin/cover-assignments takes a Supervisor with buildingId (any shift but their own building-shift; from a Blocked Scan it closes that alert); cancel handles both kinds; CoverAssignmentDto.CleanerName renamed PersonName. All tests pass. Next: Task 6 (My Building and live updates).

- 2026-10-14 · Task 6 done: GET /api/my-building (own building then covered ones, รอตรวจ first, CanInspect outside the shift false); ScanRecorded and InspectionRecorded reach the building's Supervisors by account id (SubjectUserIdProvider). All tests pass. Next: Task 7 (alerts).

- 2026-10-14 · Task 7 done: alert list adds INSPECTION_GPS (closed with คุยแล้ว), shows each Blocked Scan's reason, and offers a building cover for a Supervisor's Blocked Scan. All tests pass. Next: Task 8 (inspection sheet of the export).

- 2026-10-14 · Task 8 done: RoundOutcomes and InspectionHistory (ไม่ได้ตรวจ / ไม่ได้แก้ for closed shifts); export set Inspections writes sheet "การตรวจงาน" (inspections, outcome rows, ตรวจแทน, GPS verdict only). All tests pass. Next: Task 9 (Inspection Summary).

- 2026-10-14 · Task 9 done: GET /api/admin/inspection-summary (per active Supervisor: beat points inspected of total, submissions and how many were inspected, ไม่ได้ตรวจ rounds, own inspections, reworks and GPS flags; 7 shift dates by default, at most 31). All tests pass. Next: Task 10 (MySQL check and docs).

- 2026-10-15 · Task 10 (part) done: MySqlSupervisorTests (compiles, skipped without FACILITY_MYSQL_TEST); README and architecture docs for inspections, My Building, Supervisor covers, alerts, export sheet and summary; frontend notes. Next: the controller's real-MySQL run (migration on a throwaway database, the whole suite with FACILITY_MYSQL_TEST, the smoke script), then the whole-branch review.

- 2026-10-15 · Final-review fix wave done: Inspection Summary leaves out post-shift work (decision 17); a failed live push is logged, not a 500; My Building IsCover follows the cover; docs updated. Unit 299, API 519 + 5 skipped. Next: the controller's MySQL run.

- 2026-10-15 · MySQL check on the Mac (Homebrew MySQL 26.7): the migration SupervisorInspection applied to an empty throwaway database (the one-place CHECK on the FK columns is accepted); the whole suite with `FACILITY_MYSQL_TEST` set passed, 823 passed, 0 skipped (299 unit, 524 API including the 5 MySQL tests); a smoke script against the real API on a throwaway database passed (check-in, submission with live push, My Building, scan, inspection with GPS, building cover create and cancel, alerts, summary, export) with no warnings in the API log. Both throwaway databases were dropped; `facility_dashboard` is untouched. Next: push the branch and open the PR, with the owner's OK.

- 2026-10-15 · Owner approved push and merge: PR #55 opened and merged (`d9b4374`). Plan 6 is done. Next: the owner chooses the next work (see Pending work).

## Decisions made while building plan 6 (report them to the owner in the PR)

- 17. Work a Cleaner submits after the clock's shift end (Late or a rework fix, inside the attendance window) cannot be inspected: Supervisors inspect only in their clock shift and a cover for an ended shift is refused. The Inspection Summary leaves it out of the beat's submitted / inspected / ไม่ได้ตรวจ numbers; the export still lists the round. Confirmed by the owner on 2026-10-15.
- 18. A blank defect sent with สะอาด counts as left out (201), like blank Notes on a submission.
- 19. The live feed goes only to active Supervisors, covering ones included; a live-update failure is logged and never fails the saved submission or inspection.
- 20. Migration SupervisorInspection is forward-only once a Supervisor (building) cover exists: Down() cannot make cover_assignments.area_id NOT NULL again.

## Task log (plan 7)

- 2026-10-15 · Task 1 done: old frontend deleted (it targeted the phase-1 API); toolchain is eslint 9 + boundaries, Vitest on jsdom with a fake fetch, husky pre-commit (frontend lint, tsc, tests); wireframe CSS tokens and IBM Plex Sans Thai bundled. Frontend tests 3 passed; boundary rules shown to catch page→page, shared→pages, unknown files and raw HTML. Next: Task 2 (store, API layer and session).
- 2026-10-15 · Task 2 done: authSlice (memory only), store, RTK Query baseApi with Bearer header and one shared refresh on 401 (never for /auth/ calls), page-load startSession, endSession (logout + cache reset), getErrorMessage. Frontend tests 23 passed; each guard shown to fail when its rule is removed. Next: Task 3 (router, guard and role homes).
- 2026-10-15 · Task 3 done: route table (createBrowserRouter), ProtectedRoute by role, role homes /my-work, /my-building, /dashboard (placeholders), not-found, no-permission, crash screen, session gate, bar with logout. Frontend tests 34 passed. Next: Task 4 (login pages M0 and D0).
- 2026-10-15 · Task 4 done: login pages /login (employee ID + phone, M0) and /login/admin (username + password, D0); wrong secret, throttle and server messages; return to the page opened before login; build commit shown. Frontend tests 44 passed; JS bundle 127.18 kB gzip. Next: Task 5 (the API serves the built frontend).
- 2026-10-15 · Task 5 done: the API serves the built frontend from wwwroot (npm run build:api), index.html for every page path but never for /api or /hubs (they keep 404/405), index.html no-cache and hashed assets immutable; CORS AllowAll removed; health check moved to /api/health. Unit 299, API 530 + 5 skipped. Next: Task 6 (docs, ADR amendments, final checks).
- 2026-10-15 · Task 6 done: ADR amendments (0004 frontend section #37, 0025 rebuild not move, 0026 routes, 0062 measured size, 0063 tokens, 0065 boundaries 7), plans 2B/2C marked superseded, README, frontend README, architecture diagram. Frontend 44 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-15 · Final-review fix wave: a session that ends for any reason (logout or a refused refresh) empties the API cache (store listener); logout forgets the session at once, then waits for any refresh in flight and ignores its result, then logs out on the server; the refresh gives up after 15 s. Frontend tests 47 passed. Next: the controller's real run (MySQL), then the PR.

- 2026-10-15 · Task 6 Step 9 done (real run): the API on a throwaway database `facility_frontend_test` served the built app; curl checks passed (health, `/login`, `/scan/<x>` gets the app, unknown `/api` is 404); a script logged in as the seed Cleaner, Supervisor and Admin, with refresh and logout behaving as expected and a wrong phone refused. The owner checked the pages on a phone and reports everything works. Build output and the throwaway database were removed. Plan 7 is complete on branch `feat/frontend-foundation` (not pushed, no PR). Frontend tests 47 passed; unit 299; API 531 + 5 skipped. Next: the owner's OK for the push and PR, then frontend plan 8 (Cleaner phone).

## Task log (plan 8)

- 2026-10-15 · Task 1 done: My Work (M9, M14): own Area then covered Areas, most urgent first, summary tiles, rework defect, issue tag, no link to the scan page; asks the server on open and on "อัปเดต". Shared Cleaner types, GET /api/my-work query, Thai time helpers, status chip and colour tokens. Frontend tests 61 passed. Next: Task 2 (check-in page).
- 2026-10-15 · Task 2 done: check-in sign page /check-in/:token (M1, M3, M10): the shift's four events, buttons for what the server allows next, a fresh GPS reading per press, flag with the phone accuracy, already recorded and too-soon answers; Location off (M2), plain http, blocked sign (M8), outside the window, out of order (reloads the buttons), no signal. Shared readLocation, fakeGps, LocationHelp, BlockedScanScreen, scan answers. Frontend tests 82 passed. Next: Task 3 (scan page).
- 2026-10-15 · Task 3 done: point sign page /scan/:token (M4, M5, M11, M13): the point with its round hint and rework defect, เรียบร้อย chosen (two taps), พบปัญหา with tags and note (a tag or a note required), result by placement (on time, late with minutes, rework, off-round) with distance or flag; not checked in, checked out and outside the shift refused before the form; unknown sign; Location help (M2). Supervisors get no-permission until plan 9. Frontend tests 98 passed. Next: Task 4 (send failures and blocked point).
- 2026-10-15 · Task 4 done: the scan page's failures: no signal, timeout or 5xx show "ยังไม่ได้บันทึก" (M12) with the choices kept and "ส่งใหม่" that reads GPS again; another Area's point shows the blocked screen (M8) with the cover advice; a server refusal stays on the form; one send at a time. Frontend tests 103 passed. Next: Task 5 (docs, final checks, the controller's real run).
- 2026-10-15 · Task 5 done: ADR amendments (0025 shared Cleaner pieces, 0026 sign URLs, 0036 GPS reading and plain http, 0041 blocked on press, 0052 refresh and resend, 0062 measured size 135.53 kB gzip), README device notes, frontend README, architecture diagrams (and the plan-7 note above section 3), handoff notes for plans 9 and 10; plan 7 marked merged in the Status table. Frontend 103 tests (the plan expected 95; Tasks 3 and 4 added more); unit 299; API 531 + 5 skipped; no inline styles, web storage, ad-hoc time formatting or links to the sign pages; nothing under `backend/` changed. Next: the controller's real run, then the whole-branch review.
- 2026-10-15 · Task 5 Step 9 done (real run): the API on throwaway database `facility_cleaner_test` served the built app (JS 135.53 kB gzip). Server checks: `/check-in/<token>` and `/scan/<token>` return the app, unknown `/api` is 404, health OK. A script as the seed Cleaner: pre-shift-in refused, shift-in recorded, a clean point submission 201 (late by minutes, 8 m from the sign, within radius), My Work showed the point as รอตรวจ, another Area's point 403 with the Blocked Scan in the Admin alerts, an issue-tag submission 201, a check-in token on the point lookup 404; no warnings in the API log. The owner ran the plan's 8 Chrome steps on localhost (Sensors override, offline then resend, far-flag after 5 minutes, Location blocked) and reports no problems; the phone check on plain http was not run. Build output and the throwaway database were removed. Frontend 103 tests. Next: the whole-branch review, then the owner's OK for the push and PR.
- 2026-10-15 · Whole-branch review: no Critical; one Important, fixed: no test pinned the 20 s send timeout (removing it left every test green). `fakeServer` now ends a request when its signal aborts, as a browser does, and each sign page has a test where the server never answers: still sending at 19 s, "ยังไม่ได้บันทึก" / "ยังไม่ได้บันทึกเวลา" at 20 s. Both fail with no timeout, 10 s or 30 s. Minor, left as is: the scan page does not reload My Work after a 409 (the check-in page does); the server still refuses. Frontend 105 tests; unit 299; API 531 + 5 skipped. Next: the owner's OK for the push and PR.
- 2026-10-15 · Owner approved push and merge: PR #57 opened and merged (`96895d8`); GitGuardian passed. Plan 8 is done. Still open: the plain-http message on a real phone. Next: the owner chooses the next work (plan 9, Supervisor phone, see Pending work).

## Task log (plan 9)

- 2026-10-16 · Task 1 done: the server leaves out null fields, so outside the shift the attendance has no `shift`; My Work, the check-in page and the scan page now test it by truthiness (plan-8 bug: My Work printed "undefined", the scan page said "not checked in"). The `outsideShift` fixture matches the server. Frontend tests 105 passed. Next: Task 2 (hub follower and token).
- 2026-10-16 · Task 2 done: `followScanHub` (shared/api/scanHub.ts) opens one SignalR connection per cache entry after its first answer, hands ScanRecorded and InspectionRecorded cards to the page, asks again after every start and every reconnect (a restart every 3 s after the connection closes), and stops when the entry goes; `hubSlice` holds connecting / live / reconnecting; `getValidAccessToken` refreshes first when 30 s or less is left. Tests fake @microsoft/signalr through `test.alias` (src/test/fakeSignalR.ts). Frontend tests 116 passed. Next: Task 3 (My Building).
- 2026-10-16 · Task 3 done: My Building (M15, /my-building): the own building then covered ones, รอตรวจ by submit time, ต้องแก้ไข, then the rest; summary tiles; "not your shift" note; no links to the sign. Live: ScanRecorded and InspectionRecorded patch the card and it moves; a reconnect, "อัปเดต" and every visit ask the server; the footer says whether the hub is live; logout closes it. baseApi has tag `MyBuilding`. Frontend tests 130 passed (a push that lands during a GET asks again after it). Next: Task 4 (inspection page).
- 2026-10-16 · Task 4 done: /scan/:token opens by role (shared ByRole): the Cleaner's form, the Supervisor's inspection, no-permission for an Admin. Inspection (M6, M7, M16): opening reads a fresh fix and scans; the latest submission with distance; no verdict chosen; ไม่สะอาด needs a defect (sent trimmed, only with ไม่สะอาด); saved result with what the Cleaner sees; nothing to inspect or rework owed with the server's reason; 409 opens the newest work and clears the verdict; one send at a time. Frontend tests 146 passed (two pin the fresh scan on every visit and the splash during the rescan after a 409). Next: Task 5 (inspection failures).
- 2026-10-16 · Task 5 done: the inspection page's failures: Location off or plain http, on opening ("ตรวจงาน") and on the press ("บันทึกผลตรวจ"), with the retry repeating the same step; another building or the wrong shift shows the blocked screen in the Supervisor's wording (building cover advice, back to My Building), on opening or on the press; no signal, 20 s with no answer or a 5xx: "เปิดงานตรวจไม่ได้" with ลองอีกครั้ง on opening, "ยังไม่ได้บันทึกผลตรวจ" with the verdict kept and ส่งใหม่ (fresh fix) on the press. Frontend tests 157 passed. Next: Task 6 (docs, final checks, the controller's real run).
- 2026-10-16 · Task 6 done: ADR amendments (0023 token for SignalR, 0025 tree, 0026 sign URL by role, 0029 inspection page, 0048 blocked on opening, 0057 My Building live, 0062 measured size 154.83 kB gzip, 0064 hub follower and hub slice), README device notes, frontend README, architecture diagram, handoff notes for plans 10 and 11. Two behaviours the docs now state: `followScanHub` asks the server after every successful start (the first included) and every reconnect, and My Building asks once more after a GET that was in flight when a push landed. Frontend 157 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-16 · Whole-branch review: no Critical or Important finding; StrictMode probes passed (one GPS reading and one scan per opening, one hub connection per page). One doc fix wave (hub client in architecture, BlockedScanScreen and hub-status notes here, the ไม่สะอาด resend risk in ADR 0029). Deferred to the start of plan 11: type the nullable PointCard fields as optional and drop the nulls from the `point()` fixture; make the hub status per connection before following more than one cache entry.
- 2026-10-16 · Task 6 Step 9 done (real run): the API on throwaway database `facility_supervisor_test` served the built app (JS 154.83 kB gzip). Server checks: `/my-building` and `/scan/<token>` return the app, unknown `/api` is 404, health OK, `/hubs/scan/negotiate` without a token is 401. A script with the real SignalR client as the seed Supervisor: the hub connected with the access token, a Cleaner's submission arrived as ScanRecorded, and the inspection scan offered that submission within the radius. The owner then ran the plan's 11 Chrome steps (Supervisor and Cleaner windows: live รอตรวจ without reload, ไม่สะอาด with defect seen on My Work, rework owed, nothing to inspect, 409 with newer work, offline then ส่งใหม่, hub drop and recovery, Admin no-permission) and reports everything works; no warnings in the API log. Build output and the throwaway database were removed. Next: the owner's OK for the push and PR.

## Task log (plan 10)

- 2026-10-19 · Task 1 done: Admin menu (สถานะจุด, จัดการ Area และป้าย, บัญชีผู้ใช้) shown by AppLayout wide and hidden in print; cache tag AdminSetup; shared/api/adminSetupApi.ts (Area, sign, building and Cleaner types and reads, setSignLocation); labels for shift pattern, sign location, rounds and coordinates; formatDate; shared form, table and confirm styles; test/adminFixtures.ts. Plan 9 marked merged in the Status table. Frontend tests 163 passed. Next: Task 2 (Areas page).
- 2026-10-19 · Task 2 done: Areas page D4 (/admin/areas/:areaId?): every Area with its meta line, the open Area (first one by default) with shifts, Cleaners and sign count, its check-in sign and points with rounds, location and radius, print links for active signs, links to edit, print and capture; deactivated Areas and points marked. Frontend tests 172 passed (fix round 1 added the switch and bad-id tests; the action cells stay table cells). Next: Task 3 (point editor).
- 2026-10-19 · Task 3 done: point editor on D4: name, Round Windows per shift (typed "HH:mm", checked before adding, Enter adds), the full list sent in shift order on save, day-only Areas have no night shift, the server's refusal shown with the draft kept, deactivate / activate a point; "+ เพิ่มจุดทำความสะอาดใน Area นี้" opens the same editor and a new point stays open for editing. Frontend tests 180 passed. Next: Task 4 (sign settings).
- 2026-10-19 · Task 4 done: sign settings on D4 for the check-in sign and every point: location (site capture link to M17, or coordinates typed from a map saved as Map and hidden once confirmed on site), radius 10–500 m, a new QR asked once in the page then "พิมพ์ป้ายใหม่ ›"; none for a deactivated point or Area. Frontend tests 188 passed. Next: Task 5 (Area form D10).
- 2026-10-19 · Task 5 done: Area form D10 (/admin/areas/new, /admin/areas/:areaId/edit): code (new only, fixed after), name, building, shift pattern, one Cleaner per shift from those without an Area, a warning that going day-only removes night rounds and the night Cleaner, the server's refusal shown, a save opens the Area on D4; the Area's signs and deactivate / activate on edit; one send at a time (the save is disabled while in flight). Frontend tests 194 passed. Next: Task 6 (print signs).
- 2026-10-19 · Task 6 done: print page D5 (/admin/print?area=<id>[&sign=<id>] or ?area=all): A4 sheets of 6 small or 2 A5 signs with dashed cut lines, each with type, name, Area and building, the QR drawn on the computer (uqr 0.1.3, SVG path, ECC M, quiet zone 4), rounds or attendance events, sign code, QR date and the full URL; deactivated Areas and points left out; the origin printed and a warning when it is not https; "พิมพ์" opens the browser's print with the bar, menu and controls hidden. JS bundle 167.62 kB gzip. Frontend tests 202 passed. Next: Task 7 (capture M17).
- 2026-10-19 · Task 7 done: capture page M17 (/admin/areas/:areaId/capture[?sign=<id>]), phone column: choose the sign you stand at, "ใช้ตำแหน่งตรงนี้" watches the GPS for 30 s (countdown, readings, latest accuracy), the median latitude, longitude and accuracy (rounded up) from at least 3 readings, the radius warning, "บันทึกพิกัด" only after 30 s, saved as Site; refused location, plain http and too few readings explained; choosing another sign or leaving stops the GPS; both buttons are disabled while a save is in flight. test/fakeGps.ts gained fakeGpsTrack. Frontend tests 220 passed. Next: Task 8 (accounts list and add).
- 2026-10-19 · Task 8 done: accounts page D6 (/admin/accounts): filters ทั้งหมด / แม่บ้าน / หัวหน้า / Admin / ปิดใช้งาน with counts, search by name or login name, the table with role, place and active; the add form sends only the chosen role's fields (Cleaner: employee ID, phone, optional Area, shift; Supervisor: building and shift; Admin: username and temporary password), a held shift or a night in a day-only Area cannot be picked, the phone and password are cleared once sent; the save is disabled while in flight. Frontend tests 233 passed. Next: Task 9 (account actions).
- 2026-10-19 · Task 9 done: account actions on D6: edit (name; a Cleaner's Area and shift, the current Area sent back to keep it, none for a deactivated Cleaner; a Supervisor's building and shift; an Admin's name only), แก้เบอร์โทร for Cleaners and Supervisors, รีเซ็ตรหัสผ่าน for Admins (own reset says this browser drops too), deactivate asks first and names the Area slot that goes, no self-deactivate button, activate in one press with the server's 409 shown; secrets are cleared once sent; one send at a time (the edit's save is disabled in flight, tested). Frontend tests 244 passed. Next: Task 10 (docs, final checks, real run).
- 2026-10-19 · Task 10 done: ADR amendments (0020 accounts page, 0021 new QR and print, 0025 tree, 0026 Admin routes, 0038 radius, 0045 capture and map fallback, 0060 where the token goes, 0062 measured size 173.53 kB gzip), README device notes, frontend README, architecture diagram, handoff notes for plan 11. Frontend 244 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-19 · Final review fix wave: Admin reads ask the server on every open (`freshOnOpen` moved to shared/api/baseApi.ts) and "พิมพ์" refetches before printing; "บันทึกจุด" refuses times typed but not added; `normalizePhone` matches the server (+66 becomes 0, non-digits dropped, phone inputs allow 16 characters); phone and password help say re-entering the same secret changes nothing. Frontend tests 247 passed.

- 2026-10-19 · Owner's real run (plan 10), preliminary pass on the API at `facility_admin_test` and the Vite dev server: Areas, points, signs, print, accounts and the QR checks passed. An old check-in QR gets 404 "ไม่พบป้ายลงเวลานี้" and the new one records ShiftIn (tested with `POST /api/attendance`). The capture page (M17) on a real phone passed over https: the dev server runs with a mkcert certificate for the Mac's LAN IP (`FACILITY_DEV_CERT_DIR` in `frontend/vite.config.ts`, certificate kept outside the repo), the CA was installed on the phone, and the 30 s GPS capture saved. QR codes printed over plain http do not carry GPS, so signs are printed from the https origin.

- 2026-10-19 · PR #59 merged to master (merge commit `a6dc170`), branch `feat/admin-setup` deleted. Plan 10 is done. The GitGuardian check on #59 stayed red on six test-fixture values (fake UUIDs and `temporary-123`, not secrets); the incidents are still open in the GitGuardian dashboard to mark as false positives. Next: plan 11, after the owner's OK.

## Task log (plan 11)

- 2026-10-19 · Task 1 done: Admin pages are lazy routes (facility-0062 item 3, owner 2026-10-19); the root route shows "กำลังโหลด..." while the first page's file loads; renderApp waits for router.state.initialized. Main JS 156.21 kB gzip (was 173.53 kB in one file; a rebuild at the branch start measured 173.78 kB). Frontend tests 250 passed. Next: Task 2 (hub status per connection, AttendanceRecorded, optional PointCard fields).
- 2026-10-19 · Task 2 done: hubSlice keeps one status per connection key (selectHubStatus, hubFollowEnded), followScanHub takes a key and an optional onPoint and onAttendance (AttendanceRecorded), My Building uses key 'my-building'; PointCard's nullable fields are optional and point() leaves them out (both deferred from plan 9). Frontend tests 252 passed. Next: Task 3 (Dashboard cards with live updates).
- 2026-10-19 · Task 3 done: Dashboard D1 (/dashboard): every point by Area (server order), status chip, round, what happened by status, issue tag, no links; getServicePoints follows the hub (key 'service-points'): a pushed card replaces its point, an unknown point or a push during a GET asks again; livePoll (every open, 30 s, focus) with setupListeners in main.tsx; LiveBadge "SignalR Live" / "Connecting..." (facility-0064); tags ServicePoints, AttendanceBoard, Alerts. Main JS 156.36 kB gzip. Frontend tests 264 passed. Next: Task 4 (Dashboard filters).
- 2026-10-19 · Task 4 done: Dashboard filters in dashboardSlice (facility-0024): building (by code), one status from the legend buttons with counts inside the building, มีปัญหาที่แจ้ง; kept across pages, cleared when the session ends; "ไม่มีจุดตามตัวกรองนี้". Frontend tests 272 passed. Next: Task 5 (attendance page D2).
- 2026-10-19 · Task 5 done: attendance page D2 (/admin/attendance, lazy, in the menu as การเข้างาน): the clock's shift by default, ?date&shift&building in the address; summary tiles; table in the server's order with the regular Cleaner (or ไม่มีแม่บ้านประจำ), ยังไม่ลงเวลา, covers, state, the four times with "แก้โดย Admin", GPS flags; typed date dd/mm/yyyy checked before asking; search on Area, Cleaner or cover; shared/api/attendanceBoardApi.ts follows the hub per board (AttendanceRecorded invalidates tag AttendanceBoard:<date>:<shift>) with livePoll; formatShiftDate and parseShiftDate. Main JS 126.01 kB gzip. Frontend tests 285 passed. Next: Task 6 (corrections).
- 2026-10-19 · Task 6 done: corrections on D2 (facility-0051): "เพิ่มเวลา" / "แก้เวลา" on rows with a regular Cleaner; the form (event with each current time, time "HH:mm", reason required up to 500) checks before sending, shows the server's refusal and keeps the draft, one send at a time; the save invalidates AttendanceBoard and Alerts; ?fill=<userId>&event=<event> opens the form for an alert's link and closing it clears them. Frontend tests 295 passed. Next: Task 7 (alert list D3).
- 2026-10-19 · Task 7 done: alert list D3 (/admin/alerts, lazy): yesterday and today, ยังไม่ได้คุย / คุยแล้ว / ทั้งหมด, type picker (ถูกบล็อก, ไกลจากป้าย for the three GPS kinds, ไม่ได้ลงเวลา), each alert keyed by kind and id with time, who ((หัวหน้า) marked), why a Blocked Scan was refused, sign, place, distance, radius, accuracy; "คุยแล้ว ปิดเตือน" with an optional note, refusals at the top of the page, one send at a time; a missing Shift-Out links to D2's form; the menu shows "รายการที่ขึ้นเตือน (n)", polled every 30 s (shared/api/alertListApi.ts); formatDayTime. Main JS 126.2 + 32.0 (preloaded chunk) = 158.1 kB gzip. Frontend tests 307 passed. Next: Task 8 (covers on D3).
- 2026-10-19 · Task 8 done: covers on D3: "Area ที่ยังไม่มีคน" from the clock's board with "มอบหมายคนทำแทน" (candidates best first, "ยืนยันมอบหมาย", the server's refusal in the form, nobody to cover said); a Blocked Scan with a cover gives it in one press (Cleaner: Area; Supervisor: building; the server closes the alert); "คนทำแทนกะนี้" with "ยกเลิกก่อนหมดกะ"; Supervisors' building covers are not listed (no endpoint). Covers invalidate AttendanceBoard and Alerts. Frontend tests 313 passed. Next: Task 9 (docs, final checks, the controller's real run).
- 2026-10-19 · Task 9 done: ADR amendments (0024 dashboardSlice as built, 0025 tree, 0026 routes and hydrate fallback, 0027 D2 as its own page with the shift in the URL, 0041 covers on D3, 0051 corrections on D2, 0061 livePoll, 0062 lazy Admin routes and all the JavaScript `index.html` loads 156.97 kB gzip (one file, no preloaded chunk in the final build), 0064 hub status per connection and the badge), architecture (section 1, the section 2 diagram, section 3 redrawn for pages/dashboard, section 4's client), README device notes, frontend README, handoff notes for plan 12. Frontend 313 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-20 · Final-review fix wave: ยกเลิก is disabled while a send is in flight in the D2 correction, D3 review and D3 cover forms, and a finished send closes only its own form; an open D2 correction or D3 cover form closes when the board under it changes slot (D2 shows "กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน"); the cover candidates follow the per-shift board tag; the hub registers no-op handlers for events a follower does not take; test async waits 3 s. Frontend tests 319 passed. Next: the controller's real run (MySQL), then the owner's OK for the push and PR.
- 2026-10-20 · Controller's real run (plan 11), scripted part: the API on throwaway database `facility_live_test` (Homebrew MySQL, no root password on this Mac) served the built app (main JS 156.99 kB gzip). A Node script with the real SignalR client as the seed Admin passed 27/27: D1 list; D2 board by the clock's shift; a far Shift-In (200 m, radius 50 m) pushed as `AttendanceRecorded` and flagged; a submission with an issue tag pushed as `ScanRecorded`; a Blocked Scan refused 403 and listed on D3 with its cover suggestion; a cover from the Blocked Scan closes the alert, D2 shows "Covered", cancel clears it; a correction recorded as `AdminAdd`, pushed, and audited as `ATTENDANCE_ADD` with its reason; the far Shift-In alert closed with a note (open 2 → 0); a Cleaner gets 403 on the Admin endpoints; the hub negotiate without a token is 401; no `warn` or `fail` in the API log. Page paths return the app, unknown `/api` is 404.
- 2026-10-20 · Bug found by the real run and fixed (a53be69): `followScanHub` returned the page handler's value to SignalR, and D2's `onAttendance` returns the dispatched action, so the client logged an Error ("Result given for 'attendancerecorded' method but server is not expecting a result") on every D2 push. The handlers now have block bodies, and the fake hub's `push` throws when a handler returns a result, so this cannot come back (4 existing tests went red before the fix). Frontend 320 tests. Missing Shift-Out needs a shift to end, so it is covered by the tests only.
- 2026-10-20 · Still to do from the plan's 10 browser checks: the owner walks the Chrome steps (check-in reaching D2, submission reaching D1, filters across pages, hub drop and recovery, D2 correction, Blocked Scan and cover, GPS flag, phone size). The owner is running them on a second machine with a Cloudflare quick tunnel (see "Sharing a test build" below). The owner's results are in the entry below (checks 1–3 so far).
- 2026-10-20 · Mac setup for the walkthrough, done; no check walked yet. On the Mac: frontend 320 tests, backend 830 passed and 5 skipped (MySQL-only). `facility_live_test` exists and is seeded, `npm run build:api` has been run, and the signing key is in user-secrets. The API served on 5001 and a quick tunnel reached `/api/health` and the app, so a phone can use it. Both were stopped afterwards; every tunnel link is new. To resume: steps 3 and 4 of "Sharing a test build" (the database and the build are still there). Note: `dotnet run` without `--no-launch-profile` listened on 5225 even with `ASPNETCORE_URLS` set. The owner wants 7-digit employee ids (like 1000001) but the seed hard-codes `E1001`..`S2001`; held off. Changing it means `DbInitializer.cs`, about 35 test files, the README and docs, as its own commit with made-up numbers. For a test database only, renumbering by `ROW_NUMBER() OVER (ORDER BY id)` for non-Admin users inside a transaction works (the logins change, the passwords stay).
- 2026-10-20 · Owner's walkthrough (plan 11), checks 1–3 of 10 passed on `localhost:5001` (API on `facility_live_test`): (1) Admin login lands on `/dashboard` with the cards and the live badge; the JS the app loads is the main file plus the Dashboard chunk and two small shared chunks (`LiveBadge`, `adminSetupApi`), no other Admin page file; the other scripts in the Network list came from a browser extension, so use a Incognito window for the Network checks; (2) a Cleaner's check-in reaches D2 without a reload; (3) a submission reaches D1 as รอตรวจ and the Supervisor's inspection turns it to ตรวจผ่าน. Checks 4–10 still to walk: filters across pages, hub drop and recovery, D2 correction, Blocked Scan and cover, GPS flag, phone size, API log. While testing, the owner saw a Cleaner do Shift-Out at 15:57 on a day shift: this is by design (ADR 0050 no early-leave rule, ADR 0069 window from 60 minutes before to 60 minutes after the shift). The owner may take it back to the Supervisor; changing it would be its own plan, not part of PR #60.
- 2026-10-21 · Owner's walkthrough (plan 11), checks 4–10 reported passed. Check 5 (hub drop and recovery) was first walked with DevTools Offline, which does not close an open WebSocket: the badge stayed "SignalR Live" and only the 30 s poll failed (the "เชื่อมต่อเซิร์ฟเวอร์ไม่ได้" message came and went), which is the designed fallback, not a bug. Walked again by stopping the API (Ctrl+C) and starting it again: the badge went to "Connecting..." and came back to "SignalR Live" without a reload; passed. API log (check 10) has no `warn` or `fail`. One finding, not yet detailed: Admin pages other than `/dashboard` overflow (the owner has not said which pages or which screen width); to be handled as its own item, not in PR #60 unless it happens on a normal desktop screen.
- 2026-10-21 · Fix for that finding (owner opened the pages on a phone): the wide tables of D2 (`/admin/attendance`) and D6 (`/admin/accounts`) sit in `.app-table-scroll` (shared.css), which scrolls sideways with a 720 px minimum table width; on D3 (`/admin/alerts`) the status chip is on its own line in every card (it wrapped only when the name was long). `/admin/areas` tables were not checked. Frontend 320 tests, lint and tsc clean. Checked by tests only, not by eye, until the owner looks. Next: the owner's OK to push and merge PR #60.
- 2026-10-21 · PR #60 merged to master (`f897548`), branch deleted. Plan 11 is done. Cleanup: no API or tunnel running, throwaway database `facility_live_test` dropped, `wwwroot` deleted. Next: plan 12 (Admin reports), after the owner's OK.

## Task log (plan 12)

- 2026-10-21 · Task 1 done: branch `feat/admin-reports` from `ca59320`; `thaiToday` and `shared/utils/dateRange.ts` (shortcuts วันนี้ / 7 วัน / เดือนนี้, `checkRange` with the server's limits, `readRange`, `readBuildingId`, `toRangeParams`, `exportLink`). Frontend tests 329 passed. Next: Task 2 (inspection summary D7).
- 2026-10-21 · Task 2 done: inspection summary D7 (`/admin/inspection-summary`, lazy, in the menu as สรุปการตรวจ): each active Supervisor with building and shift, points inspected of the beat ("ไม่ได้ตรวจ n จุด"), ตรวจทั้งหมด (facility-0048 rule 5), submitted, inspected, the share as a progress bar ("–" when nothing was submitted), ไม่ได้ตรวจ, ให้แก้ไข, GPS flags; no thresholds; range typed dd/mm/yyyy with the shortcuts, checked before asking (31 dates), building picker, both in the address. Shared `RangeFields` and `BuildingSelect`. Frontend tests 341 passed; main JS 157.13 kB gzip. Next: Task 3 (Excel export D8).
- 2026-10-21 · Task 3 done: Excel export D8 (`/admin/export`, lazy, in the menu as ส่งออก Excel): the three sets (การลงเวลา and การส่งงาน ticked by default, sent in the file's order), range typed dd/mm/yyyy with the shortcuts (1–31 dates, checked before sending), building; the file name and sheets before the download; "ดาวน์โหลด .xlsx" posts through the refresh-on-401 base query, saves the server's file under its Content-Disposition name (`exportApi`'s `queryFn` hands the Blob to `saveFile`, so it never enters the store), and is disabled while in flight. "ส่งออก Excel" links on D1, on D2 (shift date and building) and on D7 (range and building); D8 reads `?from&to&building` and re-seeds its range and building (and clears the notice) when the address changes under the open page, keeping the chosen sets. `fakeServer` answers files; `test/fakeDownload.ts` records saved files. Frontend tests 356 passed; main JS 157.25 kB gzip. Next: Task 4 (Admin history D9).
- 2026-10-21 · Task 4 done: Admin history D9 (`/admin/history`, lazy, last in the menu as ประวัติ Admin): newest first with time, actor, the action's Thai name, the summary and the reason ("–" when none); range typed dd/mm/yyyy (92 dates at most, checked before asking), ผู้ทำ from `/audit-log/actors` and ประเภท with the wireframe's seven labels, all in the address; the typed range re-seeds from the address (and the notice clears) when it changes under the open page, as on D7 and D8; older pages with "โหลดรายการเก่ากว่านี้" (RTK Query infinite query by `beforeId`; an empty last page adds nothing and the button goes). Frontend tests 369 passed; main JS 157.35 kB gzip. Next: Task 5 (before → after, search).
- 2026-10-21 · Task 5 done: D9's "ก่อน → หลัง": one Thai line per changed field from the stored snapshots (events, sources, shifts, roles, Area shift patterns, alert types, sets, ใช่/ไม่ใช่; times via `formatDayTime`, dates via `formatShiftDate`, round windows "เช้า 07:00-09:00"; ids stay numbers; unknown fields show their key; `RemovedNightWindows` 0 counts as none); phone and password changes say so without the value. Search over the loaded rows (summary, actor, reason, type) with "แสดง n จาก m รายการที่โหลดแล้ว". Frontend tests 375 passed. Next: Task 6 (docs, final checks, the controller's real run).
- 2026-10-21 · Task 6 done: ADR amendments (0025 tree, 0026 routes, 0048 summary page, 0053 export page, 0057 history page, 0062 measured size 157.35 kB gzip), architecture, README, frontend README, handoff notes. Frontend 375 tests; unit 299; API 531 + 5 skipped. Next: the controller's real run, then the whole-branch review.
- 2026-10-21 · Final-review fix wave: D9 formats a value by its field, not its shape (time only for OccurredAt and QrIssuedAt, date only for ShiftDate, From and To, anything unparsable or typed shows as stored), so a name like `2026-13-45T25:61Z` no longer crashes the page; `renderApp` flushes the router's transition so the lazy page is committed when it returns (ExportPage's `open()` helper removed); "แสดง" on the range already in the address refetches in D7 and D9 instead of doing nothing; D2's heading wraps; README comma. Frontend tests 380 passed (three full runs). Next: the controller's real run (MySQL), then the owner's OK for the push and PR.
- 2026-10-21 · Controller's real run (plan 12), scripted part: the API on throwaway database `facility_reports_test` (Homebrew MySQL, no root password) served the built app; `dist/index.html` loads one JS file and preloads no other chunk, so the report pages stay out of what a phone downloads. A Python script as the seed Admin passed 22/22: the summary's default range is the 7 dates ending today and lists the seed Supervisor; 32 dates gets 400 "เลือกได้ไม่เกิน 31 วัน"; the export of today with all three sets is a zip named by `Content-Disposition` `facility_<today>_<today>.xlsx`; no set gets 400; the audit log's newest entry is that `EXPORT_EXCEL` (category Export) with From, To, Sets and Rows; `limit=1` gives `nextBeforeId` and `beforeId` the next older entry; `category=Export` filters; the actors list has the seed Admin; a Cleaner gets 403 on all four endpoints; the three page paths serve the app. An export without a token answers 401 with an empty body, which the page's refresh path relies on. No `warn` or `fail` in the API log. Next: the owner's browser checks (plan 12 Task 6 Step 9 item 5), then the owner's OK for the push and PR.
- 2026-10-21 · Owner's browser walkthrough of the plan 12 pages found two issues, both fixed. (1) The shortcut buttons วันนี้ / 7 วัน / เดือนนี้ did not show which one was chosen: `matchesPreset` (shared/utils/dateRange.ts) compares the trimmed fields with each shortcut's range today, and RangeFields sets `aria-pressed` on every matching button (more than one can match), styled like D2's pressed shift button; this covers D7, D8 and D9. (2) D9's note "ค้นหาเฉพาะรายการที่โหลดแล้ว" sat under the search box and read as "downloaded": the note and its wrapper are gone, the search is a plain field, and the count line under the table now says `พบ N จาก M รายการที่แสดงอยู่`, with ` · กด "โหลดรายการเก่ากว่านี้" เพื่อค้นรายการที่เก่ากว่า` while older pages remain. Frontend tests 381 passed. Next: the owner checks the two fixes in the browser, then the OK for the push and PR.
- 2026-10-21 · Owner: pressing "แสดง" gave no sign the press landed when the data did not change. Of the options offered (a press effect, a button that reads "กำลังโหลด..." then "✓ แสดงแล้ว", both), the owner chose the press effect only: every `.app-button` (buttons and button-styled links, the whole app) shrinks to 96 % and darkens while pressed, with no motion under `prefers-reduced-motion`. CSS only (shared.css); jsdom cannot show `:active`, so it is checked by eye. Frontend tests 381 passed. Next: the owner checks it in the browser, then the OK for the push and PR.
- 2026-10-21 · Owner: a real tap is too short to see `:active`. Replaced it with a tap animation: `shared/utils/tapFeedback.ts` (installed once in `main.tsx`, one capture listener on the document) adds `app-button--tapped` on every `pointerdown` on an enabled `.app-button` and removes it on `animationend`, so a 300 ms press (scale 0.94 and darker, back to normal) plays to its end however short the tap; a second tap restarts it; under `prefers-reduced-motion` only the darkening plays. Frontend tests 385 passed (4 new). Next: the owner checks it in the browser, then the OK for the push and PR.
- 2026-10-21 · PR #62 merged to master (`d9ac984`), branch deleted. Plan 12 is done, and it was the last of the six frontend plans: every wireframe page now exists. Cleanup: API stopped, throwaway database `facility_reports_test` dropped, `wwwroot` deleted. GitGuardian passed. Next: the owner chooses (see Pending work): the UX/UI pass over every page, the server-side search for the Admin history, or the Excel import of accounts.
- 2026-10-22 · Owner: only the device's font now, the rest of facility-0074 stays (D3 covers ahead, D6 phone, D4 round windows, the five Cleaner phone problems; facility-0074 amended). Font done (branch `feat/system-font`, PR #65): `@fontsource/ibm-plex-sans-thai` imports removed from `main.tsx` and the package uninstalled, `index.css` is `font-family: system-ui, sans-serif` (facility-0072); the build has no font files, main JS 157.53 kB gzip. Lint and tsc clean, frontend tests 385 passed. Next: the plan for the rest of facility-0074 (see `## Next`).
- 2026-10-22 · Requirements session (`sp-grill-with-doc`) over mockups of the rest of facility-0074: ADRs facility-0076 to 0085 settle how each item is fixed (0085 adds a fifth group: UI words, list approved by the owner); the owner dropped telling a wrong-Area sign on open (facility-0080: the cover flow already goes scan, blocked, Admin approves), and changed D6 (show the old phone: impossible, it is a hash; facility-0081) and D4 (typed in split hour and minute boxes, not a list; facility-0083). No code changed. Next: write the plan.
- 2026-10-22 · PR #65 merged to master (`7ced464`), branch deleted: the device's font (facility-0072) and ADRs facility-0070 to 0085. GitGuardian passed. Still to check by eye: the font on a real iPhone and a real Android phone. Next: write the plan for facility-0074 as amended (facility-0075 to 0085) with `sp-writing-plans`.
- 2026-10-22 · Plan 13 written and committed on the new branch `feat/ux-fix-list` (from `7ced464`; not pushed): `docs/superpowers/plans/2026-10-22-ux-fix-list.md`, 12 tasks. The server changes in 3 places: `ownShift` on `GET /api/my-work`, a new `GET /api/attendance/signs/{token}`, and the words in messages, summaries and Excel. Tasks 2–7 fix the Cleaner phone, D6, D4 and D3 (ahead via the existing board API). Tasks 8–11 sweep the words. Task 12 covers docs and the real run. Six planning decisions are at the top of the plan, for the PR. Next (new session): `git checkout feat/ux-fix-list`; the owner picks how to run the plan (subagent-driven with `sp-subagent-driven-development` recommended, or `sp-executing-plans`); then Task 1. No code has changed yet.

## Task log (plan 13)

- 2026-10-22 · Task 1 done: `GET /api/my-work` sends `ownShift { shift, opensAt, closesAt }` (AttendanceCalendar.WindowClock; facility-0077); new `GET /api/attendance/signs/{token}` (Cleaner only; 204 for an active check-in sign of an active Area, 404 "ไม่พบป้ายลงเวลานี้" otherwise; records nothing; facility-0082). Unit 302; API 540 + 5 skipped. Next: Task 2 (check-in page).
- 2026-10-22 · Task 2 done: the check-in page asks `GET /api/attendance/signs/{token}` on opening and shows buttons only after a fresh 204; a 404 shows "ป้ายนี้ใช้ไม่ได้แล้ว" with no button (facility-0082); off shift it shows "กะของคุณ: …" and "ลงเวลาได้ตั้งแต่ … ถึง …" from `ownShift` (facility-0077); the "วันนี้" row shows only recorded times (facility-0078). Types `OwnShift`, fixtures `dayShift` / `nightShift`. Frontend 389 tests. Next: Task 3 (My Work off shift).
- 2026-10-22 · Task 3 done: My Work off shift shows "ตอนนี้ไม่อยู่ในกะของคุณ", the own shift and "ลงเวลาเข้างานได้ตั้งแต่ …" from `ownShift`, with no counts and no cards (facility-0077); the server still sends the clock's points, the page hides them (plan decision 1). Frontend 389 tests. Next: Task 4 (scan page).
- 2026-10-22 · Task 4 done: the scan form's "เลือกปัญหาที่พบ…" warning shows after ส่งงาน only while nothing is chosen, and goes as soon as a tag or a note is given (facility-0076); the result page shows "สถานะของจุดนี้ [chip]" as one line under the heading, no card (facility-0079). Frontend 391 tests; main JS 157.82 kB gzip. Next: Task 5 (D6 phone form).
- 2026-10-22 · Task 5 done: D6 "แก้เบอร์โทร" shows the name, employee ID, role and place in large type, the field reads "เบอร์โทรที่คนนี้ใช้ตอนนี้", and the help starts "ไม่ต้องรู้เบอร์เดิม …" (facility-0081). Frontend 391 tests. Next: Task 6 (D4 hour and minute boxes).
- 2026-10-22 · Task 6 done: D4 round-window times are typed as an hour box and a minute box ("เริ่ม ชั่วโมง" … "ถึง นาที"); the hour 0–23 in one or two digits, the minute empty (00) or 00–59, digits only, two hour digits move on to the minute, Enter adds; `clockOf` replaces `isClock` (facility-0083). Frontend 391 tests. Next: Task 7 (D3 covers ahead).
- 2026-10-22 · Task 7 done: D3 "วันและกะ" picks the shift on the clock ("(กะนี้)") to the Night shift 7 days on, with the Thai weekday; a shift not started shows every Area of it with "แม่บ้านประจำ: …" (the board's `shiftDate` and `shift`, no server change); a search by Cleaner, Area code or name; the cover form, its candidates and the covers box follow the pick (facility-0075, 0084). `formatWeekday`, `coverSlots`, `slotText`, `matchesSearch`. Frontend 401 tests. Next: Task 8 (words on the phone pages).
- 2026-10-22 · Task 8 done: approved words (facility-0085) on the phone pages, the login pages and the shared pieces: พื้นที่, แอดมิน, ตำแหน่ง, ไกลป้าย, เลยเวลา, ส่งเพิ่ม (ไม่นับรอบ), สิ่งที่ต้องแก้; tests changed with them. Frontend 401 tests; main JS 157.92 kB gzip. Next: Task 9 (words on the Admin live and report pages).
- 2026-10-22 · Task 9 done: approved words (facility-0085) on D1, D2, D3, D7, D8, D9 and the menu (จัดการพื้นที่และป้าย, ประวัติการแก้ไขของแอดมิน; blocked scans named สแกนผิดพื้นที่ / ผิดตึก / นอกกะ; ไกลป้าย for flags; ตรวจตอนอยู่ไกลป้าย on D7); tests changed with them. Frontend 401 tests. Next: Task 10 (words on the Admin setup pages).
- 2026-10-22 · Task 10 done: approved words (facility-0085) on D4, D5 (printed signs: "พื้นที่ AR01 · ตึก A", "สร้าง QR"), D6, D10 and M17 (วัดที่ป้ายจริง / ปักจากแผนที่, สร้าง QR ใหม่, แอดมิน); tests changed with them. Frontend 401 tests. Next: Task 11 (server words).
- 2026-10-22 · Task 11 done: approved words (facility-0085) in server messages, new audit summaries and the Excel file (headers พื้นที่, ตำแหน่ง, แจ้งปัญหา, สิ่งที่ต้องแก้; "ส่งเพิ่ม (ไม่นับรอบ)"; "แก้โดยแอดมิน"); a Blocked Scan's alert name in summaries follows its reason (`ThaiLabels.BlockedName`), which closes the parked "สแกนนอก Area" item; old audit rows keep their words. Unit 302; API 542 + 5 skipped. Next: Task 12 (docs, final checks, real run).
- 2026-10-22 · Task 12 done: ADR amendments (0062 measured size, 0074, 0077, 0082, 0083, 0084, 0085), architecture endpoint list, handoff Next and Notes; the parked "สแกนนอก Area" item removed. Unit 302; API 542 + 5 skipped; frontend 401; main JS 157.93 kB gzip. Next: the controller's real run, then the whole-branch review.
- 2026-10-22 · Controller's real run (plan 13), scripted part: the API on throwaway database `facility_ux_test` (Homebrew MySQL, no root password) served the built app (main JS 157.94 kB gzip). A Python script as the seed users passed 13/13: Cleaner E1001 gets 204 with no body on `/api/attendance/signs/token-checkin-ar01` and 404 "ไม่พบป้ายลงเวลานี้" on `token-restroom-m1` and `nope`; E1001's `ownShift` is Day 06:00:00–20:00:00; Night Cleaner E1002 at 14:56 has no `attendance.shift` and `ownShift.opensAt` 18:00:00; the Admin export of today is an .xlsx whose headers read พื้นที่ and ตำแหน่ง (no Area, GPS or Issue); tomorrow's Day board lists AR01 and AR02 as Empty with their regular Cleaners; a cover for AR01 tomorrow is 201, shows on that board and is cancelled with 204. `/check-in/nope`, `/scan/…` and `/admin/alerts` serve the app. No warn or fail in the API log. Frontend 401 passed three more times (once while the backend suite ran), so Task 12's one-off failure did not come back. API stopped, `facility_ux_test` dropped, `wwwroot` deleted. Not run: the owner's Chrome checklist (plan 13 Task 12 Step 5 item 3). Next: the whole-branch review, then the owner's checklist and OK for the push and PR.
- 2026-10-22 · Final-review fix wave: check-in "กำลังโหลด..." also while the sign check runs; a dead assertion removed; ThaiLabels.AlertName takes the block reason with no default; the D4 minute box is selected on auto-advance; D3's covers box ahead names the picked shift when empty and shows the date of each assignment. Unit 302; API 542 + 5 skipped; frontend 402. Next: the owner's Chrome checklist, then the OK for the push and PR.
- 2026-10-22 · Owner tried the build (throwaway `facility_ux_test`) on the three open questions. (1) Excel "ตำแหน่ง" is column L, after "ที่มา", not next to "ชื่อ" (F) as reported; kept unless the owner says otherwise. (2) D3 ahead: a covered Area looked like an empty one; the owner chose: it says "ทำแทนแล้ว: <name>" and has no assign button (cancel in the covers box to change who); the open cover form also closes once that Area is covered (facility-0084 amended). (3) No in-page retry after a failed sign check: checked by blocking the request, kept as is (plan decision 2). Frontend 403 tests; lint and tsc clean. Next: the owner's Chrome checklist, then the OK for the push and PR.
- 2026-10-22 · Owner: push, PR and merge. Cleanup: API stopped, `facility_ux_test` dropped, `wwwroot` deleted. PR #66 opened (the plan's decisions 1–6 and the owner's three decisions in its description) and merged to master with the owner's OK. Next: the owner chooses from Pending work (Excel import of accounts, server-side search for the Admin history).

## Task log (docs)

- 2026-10-25 · Wrote `docs/code-map.md` for the owner, who said the vibe-coded repo had become unreadable (no idea which file or function does what, how login works, how the scan pulls GPS). Read-only exploration, no source file touched. It covers: where to start reading, the frontend and backend file-to-responsibility maps, the route table, state management, the login flow traced file by file, the scan → GPS → SignalR flow traced file by file, all 48 routes with a Why row each, the ER diagram, the tests that pin each behaviour, and ten things left unclear. `docs/architecture.md` stays the system-level page; code-map links to it and does not repeat it. Spot-checked against the code: 48 `Map*` calls in `backend/src`, 16 feature folders under `frontend/src/pages`, zero `createContext`, React 19.2.8, and both flag-not-block tests (`ScanRecordEndpointTests.cs:327`, `InspectionScanEndpointTests.cs:93`). Two small discrepancies found in `architecture.md` are recorded in code-map Part 12 rather than edited away. Next: the owner reads code-map and says what is still unclear.
- 2026-10-26 · Buildings managed by Admin (facility-0090) done on branch `feat/admin-buildings`, committed, not pushed (ADR amendments in facility-0045 and facility-0048, glossary entry **Building** in `CONTEXT.md`, code map rows). Commits: `3109c7e` (ADR, plan, handoff); Task 1 `1ce972d` + `b9229b1` (the endpoints; "code taken" only when it is); Task 2 `d1d3ff7` (an off building keeps its Areas and Supervisors off); Task 3 `bdd11a5` (the D7/D8 picker lists off buildings); Task 4 `e71d73e` + `72982b2` (the buildings page and its tests); and this docs commit. Tests: backend unit 338 passed; API 574 passed, 5 skipped (MySQL only); frontend 438 tests in 49 files. Main JS 130.58 kB gzip, CSS 6.28 kB gzip; the buildings chunk is lazy (2.58 kB gzip). The six plan rulings, one line each:
  - The full list is `GET /api/admin/buildings/all` (off buildings too, with counts).
  - The turn-off button is disabled while something is inside.
  - The D9 Setup option becomes "ตึก · พื้นที่ · จุด · รอบ · ป้าย".
  - The last column is headed จัดการ and holds the แก้ไข button.
  - An off building can still be renamed.
  - An Area turned on at the same moment its building is turned off is not guarded.
  - Made during execution: the refusal's two parts join with " และ" (no space after และ).
  - Made during execution: a failed create answers "code taken" only when a re-check finds the code taken; otherwise it is rethrown.
  - Final-review fixes (2026-10-26): `BuildingSelect` takes `includeOff` (D8 only); the Area form answers "ต้องเลือกตึกที่ใช้งานอยู่" for an off building and keeps "ไม่พบตึกนี้" for an unknown id; the building create answers `Location: /api/admin/buildings/all`; the buildings page footer says "หัวหน้าที่เปิดใช้งาน"; code-map line refs of the Admin setup table refreshed. Commits `130c206`, `aab4f0c` and `e28a976`; the scoped re-review is clean. API tests are now 576 passed, 5 skipped.
  - Made during execution:
    - the building tests also assert the audit rows' before and after values;
    - the glossary says "ไม่มีหัวหน้าที่เปิดใช้งานประจำตึก";
    - ADR 0090 has an amendment naming the real route `/all`.
  - Left as they are: the comments at `adminSetupApi.ts:123` and `AdminSetupDtos.cs:7` say "D7/D8 picker". Both pickers do read that list, but only D8 shows the off buildings.

## Sharing a test build (colleagues on other networks)

Test data only. The Development seed has an Admin whose password is public in this repo (facility-0049), so a shared link must never point at a database with real data, and never at a long-lived server.

1. Clone the branch. `cd frontend && npm install && npm run build:api` (the build goes to the API's `wwwroot`, which git ignores).
2. Once per machine, set the signing key outside the repo: `dotnet user-secrets set "Jwt:SigningKey" <48 random bytes, base64> --project backend/src/FacilityRealtime.Api`. On Windows PowerShell 5.1 use `$rng = [Security.Cryptography.RandomNumberGenerator]::Create()` and `$rng.GetBytes($bytes)`; the static `GetBytes` is not there.
3. Create a throwaway database named `facility_*_test`. In the shell that runs the API, set `ConnectionStrings__DefaultConnection` (password typed there, never in a file), `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://localhost:5001`, then `dotnet run --no-launch-profile --project backend/src/FacilityRealtime.Api`. Without `--no-launch-profile` the launch profile wins (port 5225) and the connection string can be missed, which would point the API at `facility_dashboard`. Simpler and safer since facility-0093: `scripts/facility.sh init` an env file outside the repo for the throwaway database, then `scripts/facility.sh start` it (Production, no seed: the Admin comes from `FirstAdmin__*`).
4. `cloudflared tunnel --url http://localhost:5001` prints an `https://….trycloudflare.com` link with a real certificate, so phones need no CA and the browser allows GPS. Stop `cloudflared` and the link is dead. A named tunnel with Cloudflare Access (needs a domain) is the next step if colleagues should keep using it.
5. Same Wi-Fi only, no tunnel: the Vite dev server over https with a mkcert certificate for the machine's LAN IP (`FACILITY_DEV_CERT_DIR`, certificate kept outside the repo), and the mkcert CA installed on each phone. The IP changes with the network, so the certificate must be reissued.
6. Afterwards: stop the API and `cloudflared`, drop the database, delete `wwwroot`.

## Pending work (ask the owner before starting)

- **Owner, 2026-10-27, order of work: the pilot is not in a hurry, and the owner tests each piece on their own machine before it moves on. (1) Move the always-on explanations of D1–D4, D10, M17 and the phone pages behind `MoreInfo` (the D6–D9 rule, see #80's "Explanations always on screen") — done on branch `feat/more-info-rest`, committed, not pushed; the owner passed it on 2026-10-27; (2) then the first Admin (facility-0092); then the deploy guide and the pilot.** Issue #37 (rewrite plan 2B for 170 accounts) closed on the owner's OK: the accounts page was built in plans 5 and 10, and accounts in bulk belong to the Excel import (facility-0056).

- **Owner, 2026-10-27: the pilot stays (1 Area first, facility-0043). Next work: the first Admin on a real server (facility-0092), before the Excel import.** Plan written 2026-10-27 (`docs/superpowers/plans/2026-10-27-first-admin-from-server-settings.md`, branch `feat/first-admin`); its "Decisions made while planning" list what the owner may overrule. **Done on the branch (2026-10-27), final review clean, the owner passed it on their machine, not pushed; see `## Next session: test the first-Admin branch`.** The deploy guide's settings (`Jwt:SigningKey`, `RefreshCookieSecure`, connection string) go into `docs/handover.md`, not a new file (the ADR wants them in the same guide). An empty production database had no way in: the seed runs in Development only and every account is added on D6, behind an Admin login. The owner hands the system over after the project and does not yet know who takes it on, so they asked for the usual industry way (Keycloak, GitLab, Grafana: researched 2026-10-27). The owner chose: the server's settings `FirstAdmin__Username`, `FirstAdmin__Password`, `FirstAdmin__DisplayName` create one Admin at startup only while no Admin exists at all; a `reset-admin <username>` command run on the server recovers access (new password asked twice, hidden; account turned on; tokens revoked); passwords pass the facility-0089 rule; a bad value stops the startup; a Thai handover guide for whoever takes the system on, advising 2 active Admins at all times. ADR committed with the plan. After it: a deploy guide (server, https, `Jwt:SigningKey`, `RefreshCookieSecure`, connection string), then the pilot's setup on the web pages. The Excel import is not needed for the pilot (a few people, typed on D6) and comes before the rollout to every building.
- **Buildings managed by Admin (owner, 2026-10-26): done on branch `feat/admin-buildings` (facility-0090), committed, not pushed, not merged; the owner's check is owed.** Before this work a building existed only as the Development seed (`DbInitializer`, "ตึก A") and was readable through `GET /api/admin/buildings`; nothing created one. The ADR decided:
  - a new Admin menu "ตึก" (`/admin/buildings`) with one table, where a building is added and edited in the Drawer;
  - the code is A–Z/0–9, 1–20 characters, set once;
  - the name can be changed and may repeat another building's name;
  - a building is never deleted, only turned off, and only when no active Area and no active Supervisor is left in it;
  - an Area or a Supervisor account in a building that is off cannot be turned on;
  - the D8 building picker also lists buildings that are off, marked "(ปิดใช้งาน)"; D7 lists active ones only (owner, 2026-10-26, after the final review: D7 counts active Supervisors and an off building has none);
  - every change goes to the audit log.
  
  The 6 real buildings are added through this page on deploy, not seeded in code.
- **Admin password of 15 characters and a common-password list (owner, 2026-10-26): merged to master as PR #103 (`3a6323b`, #101, facility-0089); branch deleted.** An Admin password is now 15–100 characters and must not be on the embedded list of common passwords (`CommonPasswords`). The rule is checked when an account is created or its password is reset, in `AccountRules.PasswordProblem`; an existing password shorter than 15 keeps working until it is reset. D6 says 15–100. The owner checked it on D6 on 2026-10-26 and said it passes. Test build taken down after the check (API stopped, `facility_admin_pw_test` dropped, `wwwroot` deleted). The owner's OK to push, PR, merge and delete the branch came on 2026-10-26. Known risk, accepted for the pilot: a person who types an Admin's username and a wrong password 5 times locks that Admin out for 15 minutes (`LoginThrottle`, in memory). The "หน้าแอดมิน" link stays on every login page (the Admin needs it on a phone for M17, and a desktop visitor reaches `/login` first).
- **Tailwind migration with the new look (owner, 2026-10-22): done.** Every ticket #70–#82 is merged and spec #68 was closed on 2026-10-26. The eye checks in `### Waiting on the owner's eye check` below (D4, D10, M17, D6–D9, live My Work on two phones) were done by the owner on 2026-10-26 and passed; nothing is owed.
- Excel import of accounts (ADR 0056; before rollout to every building).
  - **Owner, 2026-10-26:** the owner already has an Excel file of Cleaners, Supervisors, buildings and Areas.
  - Its codes may not match the system's rules. An Area code there looks like `BPI_B2_L1`, and `_` is not allowed today.
  - The owner deferred this. The building code stays A–Z/0–9 for now (facility-0090).
  - Before this work starts, look at the file's structure with the owner: sheets, column headers and code formats only. Never copy names or phone numbers into the repo or this file.
  - Then decide two things: whether buildings and Areas are imported too, not only accounts, and whether the Area and building code rules accept `_`. `-` stays forbidden because it separates sign codes.
- ~~Server-side search for the Admin history~~: built 2026-10-28 on `feat/history-server-search`, see the section at the top.

## Next: Tailwind migration

### Waiting on the owner's eye check (written 2026-10-24; **all passed, owner, 2026-10-26**)

The owner ended the session before the check was finished: "เรายังตรวจกันไมครบ ช่วยจดไว้หน่อย" — and will open a new session on 2026-10-25 to finish it. Nothing is blocking; these three are only **checks owed**, not known bugs.

**State at the end of 2026-10-24.** Branch `feat/admin-setup-new-look`, PR [#95](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/pull/95) **merged to master on 2026-10-26 (`9d1d1f0`) on the owner's explicit OK, with the eye checks below still owed** — fix anything they find in a new branch. Last commit `4f43345`; 419 frontend tests pass, lint and `tsc -b` clean; main JS 132.02 kB gzip, CSS 28.71 / 6.04 kB gzip. The working tree carries only the two deliberate leftovers (`M .gitignore`, untracked root `package-lock.json`) — never commit either. The API, `facility_ui_test` and `wwwroot` may still be running; a quick tunnel from the day before will be dead, so start a new one and send its URL. **Update 2026-10-26 (later): #96, #97 and #98 are merged too (see the log below); the same three eye checks, plus D6–D9 and the live My Work on two phones, are still owed. Fixes go in a new branch.**

**1. D4 — จัดการพื้นที่และป้าย** (the bulk of the round; `/admin/areas/1`)
- The header: name, shifts/Cleaner line on the left; `[แก้ไขพื้นที่] [พิมพ์ป้าย]` on the right.
- The view switcher is a tablist with a 2 px line under the words (not boxes): `จุดทำความสะอาด (2)` / `ป้ายลงเวลา`. Judge whether it reads as tabs at 1280 px and whether the spacing under the header is right.
- Tab 1: `+ เพิ่มจุด` at the table's top right, no second heading; Tab 2: the check-in sign's place, radius, QR, `เก็บพิกัดบนมือถือ ›` and `[พิมพ์ป้ายลงเวลา]`.
- The Drawer (480 px, right edge): a click on a point row's `แก้ไข`, `+ เพิ่มจุด`, and `แก้ไขพื้นที่`. Judge its width, and that a save closes it and a Toast appears.
- The window picker: `[07:00] – [09:00]` + `+ เพิ่มช่วงเวลา`, and the pill's ✕. **Two fixes after the last screenshot are still unconfirmed by the owner**: the ✕ shows only a cross (no browser grey circle) and is exactly as big as it looks, so a click beside the time no longer deletes the window.

**2. D10 — ฟอร์มพื้นที่** (`/admin/areas/2/edit` in the Drawer, `/admin/areas/new` as a full page)
- Same form both ways: code (create only), name, building, shift pattern, day/night Cleaner, the help line and the `losesNightRounds` warning. Confirmed by the owner already: create stays a full page, edit opens in the Drawer, and the two are never grouped in one row.

**3. M17 — เก็บพิกัดด้วยมือถือ** (phone, `/admin/areas/2/capture`, then per sign)
- Use **AR02**, whose signs have no place yet (AR01's are measured). The parts to judge: the 30-second reading, the "ใช้ตำแหน่งตรงนี้" answer, and the words when the reading is weak.

**Environment (it does not survive a reboot):**
- Frontend: `cd frontend && npm run build:api` (writes `backend/src/FacilityRealtime.Api/wwwroot`; the folder is git-ignored and is a throwaway).
- API: `cd backend/src/FacilityRealtime.Api && dotnet run --no-launch-profile` → `http://localhost:5001`, log `/tmp/facility-api.log`; throwaway database `facility_ui_test`, login `admin / admin1234`.
- Phone: `cloudflared tunnel --url http://localhost:5001` (log `/tmp/facility-tunnel.log`). A quick tunnel's URL changes on every start — read the current one with `grep -o "https://[a-z0-9-]*\.trycloudflare\.com" /tmp/facility-tunnel.log | tail -1` and send it to the owner; the one from 2026-10-24 (`worm-oasis-menu-moderate`) is already stale.
- Seed: AR01 (2 shifts, 2 points, signs measured), AR02 (day only, 1 point, signs unmeasured); E1001 / 0810000001 (Cleaner), S2001 / 0820000001 (Supervisor).
- Repo noise left alone on purpose: a modified `.gitignore` and an untracked root `package-lock.json`. Never commit either.

**After the check:** the owner's answers (fix or pass; fixes go in a new branch since #95 is merged), then `implement` #80. Cleanup: stop the API and cloudflared, drop `facility_ui_test`, delete `wwwroot`.

### Start here (new session, 2026-10-22)

**Where it stands.** Spec #68 is split into tickets #70–#82, which are sub-issues of #68 with GitHub blocked-by edges.
- **Done and merged:** #70 (Tailwind and tokens), #71 (shared components), #72 (login), #73 (My Work B), #74 (scan A), #75 (check-in), #76 (Supervisor pages), #77 (D1 A), #78 (D2 + D3), #79 (D4 + D10 + M17, PR #95), #80 (D6–D9, PR #96). Master is `d4e8fde` (the merge of PR #97) plus this note; 422 frontend tests pass.
- **#81 D5 print, merged to master on 2026-10-26 (owner's "handoff commit push PR merge"); branch `feat/print-signs-tailwind` deleted.** `print.css` is deleted; the page uses `Field`, `FieldGroup`, `Select`, `Button`, `Note` and `linkClass`; the sign's colours and point sizes are a paper-only `sign-*` token group in `@theme` (documented in MASTER.md, "The printed sign (D5)"); the mm measures are arbitrary values with a comment; `@page` moved to `index.css` (it now applies to every printed page). Check: printed to PDF with headless Chrome before and after, small and A5 (6 A4 pages), **0 differing pixels at 150 dpi**. 422 frontend tests, lint and tsc clean; startup JS 162.99 kB gzip (master 161.94). The real paper print and the 1280 px check: passed (the owner confirmed on 2026-10-27 that they had passed long before; nothing owed). On 2026-10-26 the test build was taken down (API stopped, `facility_print_test` dropped, `wwwroot` deleted).
- **#82 the contract step, merged to master on the owner's OK (branch `feat/contract-preflight` from master `14b5230`, deleted after the merge).** `index.css` is `@import 'tailwindcss'` (preflight on) plus `@theme`, two base rules (`body` colours and min-height, `#root` min-height), `@page` and the tap animation; it is the only CSS file. `shared/components/shared.css` is deleted; the last `app-splash` users take `Splash`; `tapFeedback` listens to `[data-tap]` only. MASTER.md, `frontend-standards.md` (the preflight rule) and facility-0062 are updated. **Check:** the computed style of every element on 23 page views (all roles, 390 px and 1280 px) was dumped in headless Chrome from the master build and this one and diffed. Two visible changes were fixed: My Work's point name had lost the browser's heading size (now `text-lg`, 19 px against 19.89 px before, the row 3 px shorter) and D9's change list had lost its bullets (`list-disc`). One is left as is: checkboxes and radios lost the browser's 3–5 px margins, so they line up with the left edge (D8's set boxes sit 7 px closer to their words; D10's and D5's radios 8 px). Buttons and inputs lost 1 px of browser padding with no size change. D5 printed from both builds (small and A5, 4 A4 pages): **0 differing pixels**. `ui-ux-pro-max` priorities 1–2 over the phone pages (contrast, targets, 8 px gaps, focus, headings): one finding, **listed, not fixed**: the phone login's inline link "หน้าแอดมิน" is 63×16 px, below the 48 px phone rule (WCAG 2.5.8 exempts a link inside a sentence); the owner approved that page in #72, so the owner decides. 422 frontend tests, lint and tsc clean; startup JS 162.96 kB gzip (130.51 main + 32.45 preloaded `baseApi`); CSS one file, 27.89 kB / 6.26 kB gzip (before: 31.26 / 6.54). Test build taken down (API stopped, `facility_contract_test` dropped, `wwwroot` deleted). **Owner's eye check, 2026-10-26:** the owner looked at every page and said it passes. The owner printed D5 on paper and it passed (2026-10-26). The login-link decision is made: the owner chose to keep the link inline and enlarge its tap area to 48 px (`inlineLinkClass` in `styles.ts`, on both login pages); the words and the layout did not move (checked in headless Chrome). The owner's OK to push, PR and merge came on 2026-10-26; the PR and the branch deletion closed #68's tickets. The test build was still running after the merge (the API was on `localhost:5001`, though an earlier note said it was down); on 2026-10-26 the API was stopped, `facility_contract_test` dropped and `wwwroot` deleted. `facility_dashboard` was not touched.

**Read first:**
- `design-system/facility/MASTER.md`: tokens, the shared component table, the pre-delivery check.
- `design-system/facility/pages/phone.md` or `admin.md`.
- `docs/agents/frontend-standards.md`.
- facility-0086, 0087 and 0088, including their notes.

**How each ticket has gone (keep doing it):**
1. Run `/mattpocock-skills:implement #N`. Branch from master as `feat/<slug>`.
2. Rebuild the page from the shared components (`Button`, `buttonClass` + `data-tap` for a Link, `Note`, `StatusChip`, `Field` / `FieldGroup` / `TextInput` / `Select` / `TextArea`, `ChoiceTile`, `ToggleChip`, `Table`, `Panel`, `Facts`, `Splash`, `Toast`, `styles.ts` helpers).
   - Set `text-base text-text` (phone) or `text-sm text-text` (Admin) on the page's root.
   - Colour links with `linkClass`.
   - Delete the page's CSS file.
3. Keep every text, accessible name and role. A red box that was not an alert takes `Note alert={false}`. Tests should pass unchanged; use TDD only where behaviour changes, at the page-test seam.
4. Run `tsc`, `eslint` and the suite, then `npm run build` (startup JS, main plus the preloaded `baseApi` chunk, was 164.05 kB gzip after #78; budget 200).
5. Review with one sub-agent covering both axes (Standards and Spec) as two separate sections, and fix what it finds.
6. Commit; the pre-commit hook runs lint and the tests. Push and open a PR with the sizes and checks.
7. Update this file.
8. The owner's eye check can be batched over several tickets. Stack the branches (`--base` the previous branch), then merge in order and retarget each PR to master before merging it. The owner says when to merge.

**Eye check on a real phone:**
1. `npm run build:api`.
2. Create a throwaway DB `facility_ui_test`. On this Mac, MySQL root has no password, so nothing has to be asked.
3. Start the API on it (see "Sharing a test build"). The Development seed has E1001 / 0810000001 (Cleaner), S2001 / 0820000001 (Supervisor), admin / admin1234, and signs `token-checkin-ar01` and `token-restroom-m1`.
4. **The owner runs `cloudflared tunnel --url http://localhost:5001` in their own terminal.** Inside the agent sandbox, cloudflared cannot get a quick tunnel, and running it outside the sandbox is refused.
5. Afterwards: stop the API, drop the DB, delete `wwwroot`.

**Known leftovers:**
- An empty, untracked `package-lock.json` at the repo root. Never commit it.
- `facility_uxpass_test`: dropped on 2026-10-23 at the owner's request.

Plan 13 (the UX fix list) is merged (PR #66). The eye checks on real phones (the device's font (facility-0072), the plain-http message, and the check-in page's sign check over the tunnel) passed; the owner confirmed on 2026-10-27 that they had passed long before.

**The owner's answers (2026-10-22), the input for the grill:**
1. Why: so the team that maintains the project after us can work on it more easily.
2. The look changes too. This is not a like-for-like port.
3. Timing: now, before the Excel import of accounts and before the pilot.

**What this overrides.** Each needs a new ADR in the grill:
- facility-0063 (plain CSS, tokens in `index.css`, one CSS file per page, `app-` prefix): replaced by Tailwind.
- facility-0071 (keep the current look until after the pilot): replaced by the new look.
- facility-0070's order (pilot first): the owner put this migration first.

**Facts measured on master `c04eab3`:**
- 19 CSS files, 2,656 lines in all: `index.css` has 79 lines with 36 tokens, and `shared/components/shared.css` has 437 lines (`app-*`).
- About 650 `className` uses in 77 non-test `.tsx` files, across about 25 pages.
- Two `@media print` blocks: `shared.css` and the D5 sign page `pages/print-signs/print.css`.
- Built CSS is 18.75 kB raw, 3.26 kB gzip. Main JS is 157.93 kB gzip.
- Tailwind 4.3.3 is the latest. `@tailwindcss/vite` 4.3.3 supports `vite ^8`, and the project is on Vite ^8.3. It is CSS built at build time, so the 200 kB JS budget (facility-0062) is not touched; watch the CSS size instead.
- Tests find elements by role, label and text, never by class, so they survive the move. They do not catch a look that drifts, so every page needs an eye check on a phone and on a computer, including the printed signs.

**Advice already given to the owner:**
- Move page by page; Tailwind and the old CSS can live side by side.
- Turn Tailwind's preflight off until the last page has moved.
- Turn the `app-*` pieces into React components, not `@apply`.
- Start with one small page (login or My Work). Then the phone pages, then the Admin pages, and the print page last.
- No `tailwind-merge` or `clsx` until a real class clash shows up.

**The workflow the owner chose:** `grill-with-docs` → `to-spec` → `implement` → `code-review` (Matt Pocock's skills), with the `ui-ux-pro-max` skill. All five SKILL.md files were read on 2026-10-22.

Setup first:
1. `ui-ux-pro-max` is installed at `~/.agents/skills/ui-ux-pro-max`, which Claude Code does not read, so it is not in the skill list. Symlink it into `~/.claude/skills/ui-ux-pro-max` (ask the owner first).
   - Its SKILL.md calls the search tool at `${CLAUDE_PLUGIN_ROOT}/.claude/skills/ui-ux-pro-max/scripts/search.py`. That path does not exist here. Use `python3 ~/.agents/skills/ui-ux-pro-max/scripts/search.py` instead.
2. The owner runs `/setup-matt-pocock-skills` before `to-spec`, `to-tickets` and `code-review`.
   - Tracker: GitHub Issues of `ThodsaphonSonthiphin/Facility-Real-time-dashboard`.
   - ADRs: `docs/adr/facility-NNNN-<slug>.md`, as AGENTS.md says. Tell it so; otherwise `grill-with-docs` would name ADRs its own way.
   - `grill-with-docs`, `to-spec`, `to-tickets` and `implement` have `disable-model-invocation`, so the owner types them.

Where `ui-ux-pro-max` fits:
0. **Before the grill.** Run `--design-system` twice, with `--stack html-tailwind`, and do not persist yet:
   - for the Cleaner and Supervisor phone pages: used at a sign, one hand, big targets;
   - for the Admin computer pages: dense data, so `--density` high.
   The output is options to talk through.
1. **`grill-with-docs`.** Use the skill's rules to challenge each choice: contrast 4.5:1, targets 44×44 px, the device's Thai font (facility-0072), Admin table density, and states for loading and errors.
   - ADRs to come out of it: a new styling ADR (replaces 0063), the new look (replaces 0071), and the design tokens.
   - Once the owner agrees, persist with `--design-system --persist -p "Facility" --output-dir <repo root>`. That writes `design-system/facility/MASTER.md`, plus page files (phone, Admin) for overrides. It is the source every later step reads.
   - Between the grill and the spec (recommended): `prototype`. Mockups of 2–3 typical pages (My Work, the scan page, D1) for the owner to pick from, as was done for plan 13.
2. **`to-spec`.** The skill is barely used here. The spec points at `MASTER.md` and the new ADRs, and takes the Pre-Delivery Checklist from `references/pro-rules.md` as each page's acceptance check. The test seam stays role, label and text.
   - Between the spec and `implement` (recommended): `to-tickets`, since 25 pages will not fit one session. Order: Tailwind and tokens, then the shared pieces (button, note, field), then the phone pages, then the Admin pages, then the print page.
3. **`implement`.** The skill is used most here, on every ticket:
   - Read `MASTER.md` and the page's file first.
   - Ask targeted questions while building (`--stack react`, `--domain ux`).
   - Run the Pre-Delivery Checklist before calling a page done.
4. **`code-review`.** It has only two axes: Standards (the repo's documented standards) and Spec. Neither covers the look or UX, so do one or both:
   - write the Tailwind rules and the key UX rules (accessibility, target size) into a repo standards document, so the Standards axis checks them;
   - run a separate `ui-ux-pro-max` UX pass, priorities 1–2 (Accessibility, Touch).
   An eye check on real phones is still needed.

- 2026-10-22 · Setup step 1 done: `~/.claude/skills/ui-ux-pro-max` is a symlink to `~/.agents/skills/ui-ux-pro-max`, and the search tool runs as `python3 ~/.claude/skills/ui-ux-pro-max/scripts/search.py`. Step 0 done, not persisted. Phone (density 4) gave light Swiss minimalism with primary `#2563EB`, accent `#EA580C` and background `#F8FAFC`. Admin (density 9) gave Swiss minimalism with a dark slate theme, status colours green, amber and red, and a dense 8–32 px spacing scale. Reject for the grill: both fonts the tool suggested (Kalam / Patrick Hand, and Fira), because facility-0072 keeps the device font; the landing-page "Hero + CTA" patterns; the GSAP scroll reveal (no GSAP, JS budget); and the hover-first checklist items on the phone. Open for the grill: a dark or light Admin theme, and whether the phone and the Admin share one palette. Tailwind 4.3 rules: tokens in `@theme` (`bg-primary`, never `bg-[var(--…)]`), and only complete class names (no `bg-${x}-500`). Next: the owner runs `/setup-matt-pocock-skills`, then `/grill-with-docs`.

- 2026-10-22 · `/setup-matt-pocock-skills` done (owner's answers: GitHub Issues, the default triage labels, glossary = `CONTEXT.md`). `AGENTS.md` has an `## Agent skills` section. `docs/agents/` has `issue-tracker.md`, `triage-labels.md` and `domain.md`; `domain.md` points at `CONTEXT.md` and names new ADRs `facility-NNNN-<slug>.md`. Labels `needs-triage`, `needs-info`, `ready-for-agent` and `ready-for-human` were created on GitHub (`wontfix` was there already). Not committed. Next: the owner runs `/grill-with-docs`.

- 2026-10-22 · Grill done (`/grill-with-docs`, 5 rounds, owner confirmed). Goal of the look: easy to read and tap at the sign. Page by page, preflight off until the last page; our own components, no UI library, no `@apply`; light only; primary `#2563EB`, no accent; statuses keep their meaning, รอตรวจ becomes `#0E6B66`, the three idle statuses stay one grey, chips always show words; device font, phone body ≥17 px, Admin ≥14 px, nothing <13 px; phone targets ≥48 px (primary 56 px full width), Admin ≥36 px; 8 px corners, thin borders; `lucide-react` icons beside text; D5 printed signs keep their layout; end state is one `index.css` (`@theme` + `@page`). Order: Tailwind + tokens → shared components → phone → Admin → D5 → preflight on, old CSS deleted. Written: ADR facility-0086 (Tailwind, replaces 0063), 0087 (new look, replaces 0071), 0088 (icons); notes added to 0063, 0070, 0071; `design-system/facility/MASTER.md` + `pages/phone.md`, `pages/admin.md` (persisted, then rewritten by hand to match the decisions); `docs/agents/frontend-standards.md` (the Standards axis for `code-review`), linked from `AGENTS.md`. Not committed. Next: `prototype`, mockups of My Work, the scan page and D1 for the owner to check on a real phone (watch whether รอตรวจ `#0E6B66` and ตรวจผ่าน `#2E6E4A` look too close); then `/to-spec`.

- 2026-10-22 · Prototype published as a private claude.ai Artifact (owner's choice: one static page, not the real app): https://claude.ai/artifact/R8ywjKCLmAFx2iBDa22GRp. Three layouts per page, switched by the bottom bar or `#mywork-a` … `#dash-c`. My Work: A cards by urgency, B the next job large, C grouped by status. Scan: A one form with large choices, B two large buttons (เรียบร้อย sends in one tap, which changes the flow), C steps with the send button fixed at the bottom. D1: A cards by Area, B a table sorted by urgency, C totals on top plus a "ต้องดูตอนนี้" list. Example data only; the tokens are MASTER.md's. Not in the repo yet; per the `prototype` skill, once the owner picks, put the file on a throwaway branch and record the verdict. Next: the owner's pick (or a mix) on a real phone, then `/to-spec`.

- 2026-10-22 · Owner picked: My Work **B**, scan page **A**, D1 **A**. รอตรวจ `#0E6B66` looked too close to ตรวจผ่าน `#2E6E4A` on the phone; after comparing violet, cyan and dark pink in the mockup, the owner chose cyan **`#0E7490`** (5.36:1). Recorded in facility-0087 (rule 3 and an amendment), `MASTER.md`, and the "chosen layouts" sections of `pages/phone.md` and `pages/admin.md`. The mockup is committed alone on throwaway branch `prototype/tailwind-look` (`a8dab9c`, `docs/design/tailwind-look-prototype.html`) and pushed with the owner's OK; it never merges to master. Open for the spec (the mockup did not settle them): what My Work B's large card shows when the most urgent point is ยังไม่ทำ rather than ต้องแก้ไข, or when nothing is left to do; where OffHours points sit in B's groups. Next: the owner runs `/to-spec`.

- 2026-10-22 · Spec published as issue #68 (`ready-for-agent`). Owner's answers at the spec: one test seam (the whole-app page tests with `renderApp` and `fakeServer`; no tests of the shared components alone, no class or colour tests); My Work B's large card holds the most urgent ต้องแก้ไข / เลยเวลา / ยังไม่ทำ point with words for its status, and with nothing left it is replaced by "ส่งครบทุกจุดที่ต้องทำตอนนี้แล้ว"; the last group is "ยังไม่ต้องทำ" (ยังไม่ถึงรอบ + นอกเวลา). Recorded in `pages/phone.md`. The ADRs, design-system and docs/agents files that #68 points at are still uncommitted on master's working copy: commit them (branch + PR, owner's OK) before anyone works from #68. Next: commit the docs, then `/to-tickets` on #68.

- 2026-10-22 · Docs committed on branch `docs/tailwind-grill-and-spec`, PR #69 opened, not merged (waits for the owner). The pre-commit hook ran lint and the frontend tests: 403 passed. Next: the owner merges #69, then runs `/to-tickets` on #68.

- 2026-10-22 · Owner: merge. PR #69 merged to master (`7492117`); the files #68 points at are now on master. Next: the owner runs `/to-tickets` on #68.

- 2026-10-22 · `/to-tickets` on #68: 13 tickets (#70–#82), sub-issues of #68, each `ready-for-agent`, with GitHub's native blocked-by edges. The split is expand–contract: #70 Tailwind + tokens + lucide-react (nothing visibly changes) → #71 shared components → page moves #72 login, #73 My Work B, #74 scan A + outcome screens, #75 check-in, #76 Supervisor pages, #77 D1 A, #78 D2 + D3, #79 D4 + D10 + M17, #80 D6–D9, #81 D5 print (each blocked only by #71, so they can run in parallel) → #82 contract (preflight on, old CSS deleted, final eye check and UX pass; blocked by #72–#81). Every ticket shares the acceptance checks: suite green, lint/tsc clean, JS ≤200 kB gzip, CSS size noted, MASTER.md check, the owner's eye check. Next: `implement` #70.

- 2026-10-22 · #70 done on branch `feat/tailwind-setup`: Tailwind 4.3.3 through `@tailwindcss/vite` (dev dependencies), preflight off (only `theme.css` and `utilities.css` are imported); `@theme` holds MASTER.md's tokens and clears Tailwind's own colours, text sizes, radii and fonts, so `bg-blue-600` and the like do not exist; the type scale is `text-xs` 13, `text-sm` 14, `text-md` 15, `text-base` 17, `text-lg` 19, `text-xl` 22, `text-2xl` 28 (written into MASTER.md). The old `:root` variables are renamed `--old-*` in all 19 CSS files (checked to be a pure rename) because they had the same names as the new tokens; they go in #82. `lucide-react` 1.53.0 (the newest release at least two weeks old). Measured: main JS 157.94 kB gzip (no icon used yet); a temporary probe showed `bg-primary` resolving to `#2563eb` and the first icon costing +1.38 kB, later ones about 0.15 kB (facility-0062, 0088 notes); main CSS 22.07 kB raw / 3.88 kB gzip (was 18.75 / 3.26). Tailwind also emits a few utilities from words in the source (`fixed`, `hidden`, `border`, `table`, …); no element uses those as class names. Lint, tsc clean; 403 tests pass. Code review (Standards + Spec) fixes: ADR 0088 keeps its estimate with a dated note; the `:root` comment says the base rules stay; MASTER.md names the icon sizes (`size-5` phone, `size-4` Admin); Tailwind moved to devDependencies. Not done: the owner's eye check of a phone page and an Admin page (nothing should look different). Next: the eye check, push and PR (with the CSS size), then #71.

- 2026-10-22 · Owner's eye check of #70: both login pages (`/login` on a phone, `/login/admin` on the computer) served from this branch and from master side by side; the owner saw no difference. Dev servers stopped, comparison worktree removed. Pushed and PR opened. Next: the owner merges the PR, then `implement` #71.

- 2026-10-22 · Owner: merge. PR #83 merged to master (`16f1cc5`); #70 closed. Next: `implement` #71 (shared components).

- 2026-10-22 · #71 on branch `feat/shared-components`. New components in `shared/components`:
  - `Button` with `buttonClass` (primary / secondary / danger / link; phone 48, main 56 full width, desk 36);
  - `Note` (an error is `role="alert"`);
  - `StatusChip` (md / lg);
  - `Field` with `TextInput`, `Select`, `TextArea`; the help line sits outside the label and is linked with `aria-describedby`;
  - `ChoiceTile`, `ToggleChip`;
  - `Table` (its own scroll box; style-free cells);
  - `Panel`, `Facts`, `Splash`;
  - class helpers in `styles.ts`.

  Already moved to the new look: `AppLayout` (shell and bar, `print:hidden`), `AdminNav` (36 px links, the current page in blue), `LiveBadge`, `StatusScreen`, `CrashScreen`, `StatusChip`. Still on `app-*`, for the page tickets: `BlockedScanScreen`, `LocationHelp` (#74); `RangeFields`, `BuildingSelect` (#80).

  Other changes:
  - `index.css` base rules and old tokens moved into `@layer base`, because an unlayered `a { color }` beat `text-on-primary` on a button-styled link. Unmoved pages are unlayered, so they still win as before.
  - The press feedback is now class `is-tapped`, with its animation in `index.css`, so it reaches every page. `tapFeedback` also matches `[data-tap]`: TDD, with one new test that a press on the new Button plays.
  - Owner's decision: the irreversible-action button (old orange `app-button--warn`) becomes `danger`: red error-tone text and border on white, no status colour (facility-0087 note).
  - The shell sets no text size; each moved page sets `text-base` (phone) or `text-sm` (Admin) on its root (frontend-standards, MASTER.md).
  - Code review fixes: the `no-underline` / `underline` clash, the arbitrary value comment, the shared focus ring and box classes, the `TextArea` size, a stray `main` input size removed, the doc comments.

  Lint and tsc clean; 404 tests pass. Main JS 158.84 kB gzip; CSS 32.64 kB / 5.80 kB gzip. Not done: the owner's eye check. The bar, chips, Admin menu and live badge show only on logged-in pages, which need the API and a throwaway database. Next: the eye check, then the PR.

- 2026-10-22 · Owner: check #71 by eye together with #72–#74. Pushed; PR #84 opened and **held** (do not merge before that eye check). The page tickets branch from `feat/shared-components`, so their PRs stack on #84 until it merges. Next: `implement` #72.

- 2026-10-22 · #72 on branch `feat/login-new-look` (stacked on `feat/shared-components`). Both login pages are rebuilt from `Field` + `TextInput`, `Note` and `Button`, and `auth.css` is deleted.
  - **Phone page:** `text-base` root, 16 px gutter, 56 px main action at the bottom; its bottom padding grows with the home-bar inset (`env(safe-area-inset-bottom)`).
  - **Admin page:** `text-sm` root, one card, 36 px controls.
  - **Text:** the build line and help line are 13 px now (they were 11 and 12).
  - **Links:** new `linkClass` in `styles.ts`, because the base layer still colours `a` with the old teal until #82. Moved pages also set `text-text` on their root, so they do not depend on `--old-color-text`.
  - **Unchanged:** every text, accessible name, autocomplete and link; the 10 login tests pass as they were.
  - **Code review:** no hard violations. Fixes: gutter, safe area, link hover and focus, root text colour, import order.
  - **Checks:** lint and tsc clean; 404 tests; main JS 159.38 kB gzip; CSS 31.40 / 5.78 kB.
  - **Visible changes for the eye check:** the phone button is 56 px (was 72); the Admin button is 36 px / 14 px (was 48 / 17); the error note is no longer bold.

  Next: PR (based on `feat/shared-components`), then `implement` #73.

- 2026-10-22 · #73 on branch `feat/my-work-b` (stacked on `feat/login-new-look`). My Work is variant B.
  - **Order of the page:** the cover note; the "ทำอันนี้ก่อน" card; the progress line "x จาก y จุด ส่งแล้ว", with a bar in the ตรวจผ่าน and รอตรวจ colours; then the rows grouped ต้องทำต่อ / ส่งแล้ว / ยังไม่ต้องทำ.
  - **The card:** the most urgent ต้องแก้ไข / เลยเวลา / ยังไม่ทำ point across the own and covered Areas. Its frame takes the status colour, and it shows the defect and the instruction for the status. With nothing left to do, an info note "ส่งครบทุกจุดที่ต้องทำตอนนี้แล้ว" takes its place.
  - **The rows:** empty groups are hidden, and a covered Area says "(ทำแทน)".
  - **Owner's call while building:** a ต้องแก้ไข row also shows its defect (pages/phone.md).
  - **Files:** new `workRules.ts`, `NextJob.tsx` and `WorkGroup.tsx`; `WorkCard.tsx` and `myWork.css` deleted; two icons (QrCode, RefreshCw).
  - **Tests (TDD, at the page seam):** 6 new; the 4 variant-A tests rewritten for B (order, cover, off shift, refetch); the overdue/issue test is folded into the group test. The page now has 16 tests; 410 in the frontend.
  - **Code review:** no hard violations. Fixes: the test gaps, `useId` for the card heading, exports narrowed, the two-colour bar.
  - **Left as is (reported):** the group headings are small eyebrow labels as in the mockup; `roundText` and `issueText` repeat the dashboard's and My Building's versions, so they could move to `shared/` when those pages move (#76, #77).
  - **Checks:** lint and tsc clean; main JS 161.71 kB gzip (first icons); CSS 5.84 kB.

  Next: PR, then `implement` #74.

- 2026-10-22 · #74 on branch `feat/scan-a` (stacked on `feat/my-work-b`).
  - **Scan page, variant A:** one form; เรียบร้อย / พบปัญหา are a real radio group drawn as two 96 px `ChoiceTile`s with icons; the tags are real checkboxes drawn as 48 px `ToggleChip`s; the note field stays inside the "ปัญหาที่พบ" fieldset; ส่งงาน is the 56 px main action with an icon.
  - **Outcome screens in the new look:** result (large chip), send failed (still a region named "ยังไม่ได้บันทึก", in `noteClass('error')`), off duty, splash, and the shared `BlockedScanScreen` and `LocationHelp`. The check-in and inspect pages use the last two, so they show there in the new look already.
  - **`Note` gains `alert`:** it is on by default for an error. Red boxes that were not alerts before (the Supervisor's defect, ไม่ได้บันทึก…) pass `alert={false}`, so roles are unchanged.
  - **Tests:** none changed; 410 pass. The scan, check-in and inspect suites are untouched.
  - **Files:** `scan.css` deleted.
  - **Code review:** no hard violations. Fixes: the note back inside its fieldset, the shared `noteClass`, MASTER.md (`alert`, `size-7` icons in a tile), matching kicker spacing.
  - **Checks:** lint and tsc clean; main JS 162.53 kB gzip; CSS 29.10 / 5.75 kB.

  Next: PR, then the owner's eye check of #71–#74 on a real phone (needs the API and a throwaway database).

- 2026-10-22 · Owner's eye check of #71–#74: **passed**. The run used the app from `feat/scan-a` (#71–#74 stacked), served by the API in Development on throwaway database `facility_ui_test`. The owner opened the link from a `cloudflared` quick tunnel, which the owner ran in their own terminal: inside this session's sandbox, cloudflared could not get a quick tunnel, and running outside the sandbox was refused. Cleanup is done: API stopped, `facility_ui_test` dropped, `wwwroot` deleted. `facility_uxpass_test` (not from this session) is still there, untouched. Next: merge #84 → #85 → #86 → #87 in order, with the owner's OK.

- 2026-10-22 · Owner: merge all four. PRs #84 → #85 → #86 → #87 merged to master in order (each retargeted to master first); #71–#74 closed; master `47b49e9` has the same tree as `feat/scan-a`; 410 tests pass. Next: `implement` #75 (check-in page), then #76–#81.

- 2026-10-22 · #75 on branch `feat/check-in-new-look` (from master `cf1d313`). The check-in sign page is in the new look; `checkIn.css` is deleted.
  - **Page:** `text-base text-text` root; kicker, 28 px heading, the shift line now 19 px in text colour (was 15 px muted), the own Area 15 px muted; the "วันนี้" row is a four-column box of 15 px steps (was 12 px), a recorded step solid, one still to come dashed; ไกลป้าย in the warn text colour.
  - **Buttons:** all ลงเวลา buttons 56 px full width; only the first (the next event) is blue, the others outlined. **Owner's call while building:** "56 px primary" in #75 means the size; phone.md's one-blue rule stands (recorded in `pages/phone.md`). The old first button was 72 px / 22 px text.
  - **Notes:** load error and failed press are `Note` errors (`role="alert"` as before); the answer to a press keeps `role="status"` through a new `Note` prop `status` (MASTER.md updated); the off-shift note and "ลงเวลาครบแล้ว" stay without a role.
  - **Height:** the page fills the screen less the shell (100 px: `pt-3` + 48 px bar + `gap-4` + `pb-6`), so the buttons sit low; the old value (80 px) made the page scroll.
  - **Tests:** unchanged, plus one assertion that the recorded time is `role="status"`; 410 pass.
  - **Code review (one sub-agent, Standards + Spec):** no wrong words, names or roles. Fixes: the min-height, the timeline's doc comment, the `status` test. Left as is (reported): no bottom safe-area inset in the phone shell (the scan pages have the same gap; a shell change, for #82 or a small ticket); off-rhythm gaps `gap-2.5` / `gap-1.5` match the scan pages; the kicker + heading pattern now repeats in four places and could become a shared `PageHeading`; `Note`'s `alert` + `status` are two booleans for one role.
  - **Checks:** lint and tsc clean; main JS 162.61 kB gzip; CSS 27.74 / 5.60 kB gzip.
  - **Not done:** the owner's eye check on a real phone (can be batched with #76).

  Pushed; PR #89 opened, not merged (the owner says when). Next: `implement` #76.

- 2026-10-22 · #76 on branch `feat/supervisor-new-look` (stacked on `feat/check-in-new-look`). My Building and the inspection page are in the new look; `myBuilding.css` and `inspect.css` are deleted.
  - **My Building:** summary tiles white with a 2 px frame, รอตรวจ cyan and รอแม่บ้านแก้ red (were tinted boxes); point cards framed in cyan (รอตรวจ) or red (ต้องแก้ไข) through `statusFrame` in `buildingRules.ts`; the "ไปที่จุดนี้แล้วสแกน QR เพื่อตรวจ" line is a grey box with a QR icon (was teal text, and blue means pressable); อัปเดต is a full-width button under the live line, as on My Work.
  - **Inspection:** สะอาด / ไม่สะอาด are 96 px `ChoiceTile`s with icons; สิ่งที่ต้องแก้ is a `Field` + `TextArea`; บันทึกผลตรวจ and ส่งใหม่ are the 56 px main action (were 72 px); the result shows a large chip; "อยู่ในรัศมี" is plain bold words (was a green chip: green is only ever a Point Status); the open-failed screen uses `helpScreenClass`, `Splash` for the wait.
  - **Owner's call while building:** the chosen ไม่สะอาด tile is blue like every chosen tile (was red); recorded in `pages/phone.md`.
  - **Shared:** `roundText` and `issueText` moved to `shared/utils/pointText.ts`; My Work, My Building and the dashboard's `PointTile` use them (the dashboard's look is still #77's).
  - **Tests:** unchanged; 410 pass. Words, names and roles unchanged (checked by the review).
  - **Code review (one sub-agent, Standards + Spec):** no hard violations. Fixes: the green radius chip, the status frame map shared by the cards and the summary, a comment. Left as is (reported): the building headings are 15 px eyebrow labels as on My Work; loading lines are 15 px on check-in and My Building but 17 px on My Work; #76 says "ตรวจผ่าน / ต้องแก้ไข choices" but the words stay สะอาด / ไม่สะอาด (behaviour unchanged).
  - **Checks:** lint and tsc clean; main JS 162.78 kB gzip; CSS 23.88 / 5.13 kB gzip.
  - **Not done:** the owner's eye check on a real phone (with #75).

  Pushed; PR #90 opened against `feat/check-in-new-look` (stacked on #89), not merged. Next: `implement` #77.

- 2026-10-23 · #77 on branch `feat/dashboard-d1-a` (stacked on `feat/supervisor-new-look`). D1 is variant A; `dashboard.css` is deleted.
  - **Layout:** Area sections ("AR01 …", "ตึก A · n จุด"); tiles in an auto-fill grid, 230 px minimum (four across at 1280 px).
  - **Filter row:** building `Select` (desk); one 36 px toggle per status with a dot (`statusBg`) and a bold count; "มีปัญหาที่แจ้ง n" with a `TriangleAlert` icon. A toggle that is on gets a blue frame (`aria-pressed:` ring). The borders use `input-border` for 3:1.
  - **Tiles:** ต้องแก้ไข / เลยเวลา have a 2 px frame in the status colour (`tileFrame` in `boardRules.ts`, the same idiom as `statusFrame`). The issue stays a small red box, as the rules line calls it ป้ายแดง.
  - **Header:** ส่งออก Excel is a desk button-link with a `Download` icon. The error and empty messages are `Note`s.
  - **Text:** everything is ≥14 px. The mockup's 13 px rules line and round line became 14 px (admin.md: 13 px is for table meta only).
  - **Idle dots:** the three idle statuses share one grey dot (the dashed and faded dots are gone, facility-0087 rule 3).
  - **Shared:** StatusChip's colour map moved to `styles.ts` as `statusBg`.
  - **Tests:** unchanged; 410 pass. Words, names and roles unchanged (checked by the review).
  - **Code review (one sub-agent, Standards + Spec):** one hard violation, the toggle borders (`border`, 1.4:1), now fixed. Also fixed: the frame map moved to `boardRules.ts`, the `styles.ts` header, the icon colour (currentColor, as in the mockup). Left as is (reported): `gap-2.5` / `gap-1.5` / `px-2.5` copy the mockup and the shared components (off the 4/8 rhythm); the issue box is a box, while the mockup has red text.
  - **Checks:** lint and tsc clean; main JS 162.83 kB gzip; CSS 24.72 / 5.24 kB gzip.
  - **Not done:** the owner's eye check at 1280 px on a computer (with #75 and #76).

  Pushed; PR #91 opened against `feat/supervisor-new-look` (stacked on #90), not merged. Next: `implement` #78.

- 2026-10-23 · Owner's eye check of #75–#77: **passed**. The app was served from `feat/dashboard-d1-a` (#75–#77 stacked) by the API in Development on throwaway database `facility_ui_test`. D1 was checked on the computer; the phone pages were checked through a `cloudflared` quick tunnel that the owner ran. Cleanup is done: API stopped, `facility_ui_test` dropped, `wwwroot` deleted. `facility_uxpass_test` is still there, untouched. Next: merge #89 → #90 → #91 in order, with the owner's OK; then `implement` #78.

- 2026-10-23 · Owner: merge all three. PRs #89 → #90 → #91 merged to master in order (#90 and #91 retargeted to master first); #75–#77 closed; master `c177dfd` has the same tree as `feat/dashboard-d1-a`; 410 tests pass. Next: `implement` #78 (D2 + D3), branched from master.

- 2026-10-23 · #78 on branch `feat/attendance-alerts-new-look` (from master `6081a94`). D2 and D3 are in the new look; `attendance.css` and `alerts.css` are deleted.
  - **D2:**
    - shift toggles; `Field` controls;
    - the four counts are white boxes, and ยังไม่มีคน is framed and tinted red;
    - the table is in the shared `Table` box; Area-state chips keep the status colours (Empty rework, Working passed, OnBreak break, Done idle, Covered pending), each with its words;
    - แก้โดยแอดมิน is a warn badge; the correction form uses `Field`s.
  - **D3:**
    - the three white boxes; review tabs as toggles;
    - **decision made while building:** alert-kind chips take note colours, not status colours (BlockedScan error, the Gps kinds warn, Missing plain grey), recorded in MASTER.md;
    - the cover form loses its blue frame (blue means pressable).
  - **Shared:**
    - `toggleClass` in `styles.ts` (D1's filters use it too; hover only when enabled);
    - `NoticeLine` + `Notice` in `Note.tsx`: one `<p>` with the role, because a D2 test checks the role sits on the text's own element;
    - `noteTones` exported.
  - **Owner's call (review finding):** `--color-input-border` `#AEB6B7` (2.06:1, below the 3:1 rule since #70) → `#868F91` (3.31:1 on surface, 3.03:1 on bg), in `index.css` and MASTER.md. Every moved page's fields, buttons and toggles get darker outlines. PR #91 wrongly said D1's toggles met 3:1; PR #93 corrects it.
  - **Build:** main JS 132.68 kB gzip, but Rolldown now splits a preloaded `baseApi` chunk (RTK Query, 31.37 kB gzip), so startup JS ≈ 164.05 kB (budget 200). Measure main + preloaded chunks from now on. The split appears when D2 or D3 use the shared components; it is not configured. CSS 25.38 / 5.36 kB gzip.
  - **Tests:** unchanged; 410 pass. Words, names and roles unchanged (checked by the review against master).
  - **Code review (one sub-agent, Standards + Spec):** besides the border, fixed: duplicate imports, the kind chips reuse `noteTones`, `boxClass` for the counts, the ยังไม่มีคน tint, `max-w-120` instead of `max-w-phone`, hover on disabled toggles. Left as is (reported): `alertStyles.ts`'s section box is close to `Panel` (Panel cannot take a heading id); Covered in cyan shares รอตรวจ's colour, as in the old look.
  - **Not done:** the owner's eye check of D2 and D3 at 1280 px, and of the darker outlines on the pages already merged.

  Pushed; PR #93 opened against master, not merged. Next: the eye check, then `implement` #79.

- 2026-10-23 · Owner's eye check of #78, first round: three changes asked, all done on the same branch (PR #93):
  1. **Forms as popups.** D2's correction form (and, on the owner's OK, D3's cover form) opened inline and pushed the table down. Both now open in a shared `Modal`: the browser's modal `<dialog>`; the heading (`tabIndex={-1}`) takes focus; Escape asks `onClose`; closing returns focus to the opener (a layout effect, so it never lands on `<body>`).
     - jsdom 30 has no `showModal`, so `src/test/setup.ts` adds a minimal `showModal` / `close`.
     - The tests find the forms as `dialog` now (was `region`), plus three new tests: Escape on D2 and on D3, and focus return on D2. 413 tests pass.
     - D3's Area button is no longer disabled while its form is open: the page behind is inert, and the focus has to return to it. Its words "กำลังมอบหมาย ›" stay.
  2. **แก้ไข column.** A `link` button now has no side padding (`buttonClass`), so แก้เวลา lines up with the column heading. This also applies to D3's ยกเลิก… buttons.
  3. **Date controls.** วันที่ + ดูวันที่นี้ + กะปัจจุบัน sit together as one group (`gap-1`); the groups are 16 px apart.
  - Code review (one sub-agent): no hard findings. Fixed: `text-sm` dropped from `Modal`, `useLayoutEffect`, a stale comment.
  - Left as is (reported): after a save, the notice line above the counts still shifts the table by one line; the D2 test "pressing กะดึก with a form open" now runs a path a user can only reach by Back or a push (the background is inert in a browser); Chrome's back-gesture close-watcher could close the dialog natively without telling React (low risk on desk pages).
  - **Checks:** lint and tsc clean; startup JS 132.73 + 31.37 kB gzip; CSS 25.77 / 5.47 kB gzip.
  - The second round of the eye check ran on `facility_ui_test`.

- 2026-10-23 · Owner's ask, on the reported table shift after a save: notices become a toast (same branch, PR #93).
  - **Owner's spec:**
    - floats at the top right over the page and pushes nothing down;
    - a done notice goes by itself (3–5 s, we use 5 s) and has a check icon;
    - "✕" closes it at once.
  - **Shared `Toast`** (`shared/components/Toast.tsx`, which now owns the `Notice` type) replaces `NoticeLine`:
    - the timer holds while the pointer or the keyboard is on it, with a 200 ms fade (none under reduced motion);
    - `warn` and `error` stay until "✕ ปิด". The board-flip notice on D2 ("กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด…") is now `warn`, because it reports a form closing by itself.
  - **Owner, second look:** the toast moved to the top centre; ✕ without the word ปิด.
  - **Owner may still want:** the done colour is the info blue (the owner allowed "green or blue"; a green would need new tokens).
  - **Review (one sub-agent):**
    - Hard finding: the ✕ was an icon-only button (frontend-standards forbids it). It became "✕ ปิด", then the owner asked for ✕ alone: it is the rule's one exception, written into frontend-standards and MASTER.md, named "ปิดข้อความ".
    - Also fixed: `shadow-md` → `shadow-sm` ("no heavy shadows"); the icon is 16 px (Admin size).
    - Left as is: after "ปิด" the keyboard falls to `<body>`. As before, the `role="status"` element is inserted already holding its words, which some screen readers can miss.
  - **Tests:** 3 new (D2: ✕ closes, goes after 5 s but not while hovered; D3: an error stays until ✕). 416 pass.
  - **Checks:** lint and tsc clean; startup JS 132.68 + 31.37 kB gzip; CSS 26.93 / 5.75 kB gzip.

- 2026-10-23 · Owner's second eye check of #78: passed. API stopped, `facility_ui_test` dropped, `wwwroot` removed; `facility_uxpass_test` also dropped at the owner's request. PR #93 merged into master on the owner's OK.

Next session: `implement` #79 (D4 + D10 + M17). New Admin pages use the shared `Toast` and `Modal`.

- 2026-10-23 · Matt Pocock's skills are usable in OpenCode now (they had been installed for Claude Code only; OpenCode does not read `~/.claude/plugins/cache/`, which is why `implement`, `to-spec`, `to-tickets`, `code-review` and `grill-with-docs` were missing). `~/.config/opencode/scripts/install-mattpocock-skills.sh` symlinks the plugin's 24 promoted skills (version 1.3.1) into `~/.config/opencode/skills/mattpocock/`, and writes one `/‹skill-id›` command per skill into `~/.config/opencode/commands/`. The five user-invoked skills keep `disable-model-invocation`, so they stay out of the model's skill menu but load by id through their command: `/implement #79` works. `handoff`, `wait-what` and `grill-me` keep this machine's own versions (the script leaves them alone). Re-run the script after a plugin update: the symlinks name a version path, so a new version would leave them dangling. No repo file changed.

- 2026-10-23 · #79 on branch `feat/admin-setup-new-look` (from master `7b1054b`). D4, D10 and M17 are in the new look; `areas.css` (248 lines), `areaForm.css` and `capture.css` are deleted. The per-page CSS left in `pages/` belongs to #80 and #81.
  - **D4:** the 300 px Area rail and the open Area on the Admin tokens; the sign table is the shared `Table`; the editor boxes are `boxClass` sections; the hour/minute boxes (facility-0083) use `inputClass('desk')`.
  - **D10:** the same one column at 720 px (`max-w-180`, commented: no width token names it), on `Field` / `TextInput` / `Select`; the two lower boxes are named sections.
  - **M17:** phone sizes in the phone shell (`text-base`, 48 px sign rows, 56 px full-width main action); it gained the "กำลังโหลด..." line it never had.
  - **Decisions:**
    - a point save (D4) and an Area (de)activation (D10) answer with the shared `Toast`, as on D2/D3 (owner, #78);
    - the per-sign notes (radius, location, QR) stay inline `Note`s: they answer one box, and the QR one carries the "พิมพ์ป้ายใหม่ ›" link;
    - the new-QR question stays a page `alertdialog` (a test asserts it and its accessible description), not the shared `Modal`;
    - deactivation is an outlined button, not `danger`: MASTER reserves `danger` for what cannot be undone, and the same panel says "เปิดกลับได้" (review finding; the old look used a warn button).
  - **Shared:** `FieldGroup` in `Field.tsx`; D10's shift pattern and D4's round windows were two private fieldset styles, now one piece (MASTER's table updated). `Panel` was briefly given a `headingId` and reverted: the review called the optional pair unsafe and the edit out of scope, so D10's two boxes are named sections built from `boxClass`.
  - **Code review (two sub-agents, Standards + Spec):** fixed `danger` on reversible actions; focus rings on the Area rows, the M17 sign rows and the shift radios; 8 px between the Area rows and between the window chips; `gap-1` label pairs; M17's group heading 15 → 19 px; the missing M17 loading line; the map-pick help line under its row; the window chip's × a 36 px target. Left as is (reported): M17's main action sits in the sign card above the sign list (the page's own words say "จากรายการข้างล่าง", so the order cannot change without changing the words); `SignPanel`'s local ok → info + status mapping (two lines); the D4 list heading at 17 px bold, like D3's box titles.
  - **Checks:** 416 tests pass, the 51 page tests unchanged (no test file touched); lint and tsc clean; main JS 132.07 kB gzip, startup ≈ 163.44 kB (main + preloaded `baseApi` 31.37); CSS 27.65 / 5.86 kB gzip.
  - **Not done:** the owner's eye check (M17 on a real phone, D4 and D10 at 1280 px).

  Pushed; PR #95 opened against master, not merged. Next: the eye check, then `implement` #80.

- 2026-10-24 · #79 D4 rework after the owner's test-1 feedback (same branch, PR #95; only D4 — D10/M17 feedback comes later).
  - **1.1 forms in a Drawer:** `Drawer` added beside `Modal` in `shared/components/Modal.tsx`: the same modal `<dialog>`, a full-height panel on the right edge (480 px `w-120`, commented), scrolling on its own; Escape, `onClose` and the opener's keyboard back work as `Modal`'s. D4's point editor, and the Area form (`AreaEditorDrawer`: D10's `AreaForm` + `AreaActiveSection`, the building/Cleaner lists asked for only while it is open), now open there — the Area table stays visible on the left. A save closes the Drawer and floats the shared `Toast`; a Toast under an open dialog would sit under its backdrop.
  - **1.2 master-detail:** the header is the name, the shifts/Cleaner line, `[แก้ไขพื้นที่]` (Drawer) and `[พิมพ์ป้าย]`; two tabs — `จุดทำความสะอาด (n)` with `+ เพิ่มจุด` at the table's top right, and `ป้ายลงเวลา` holding the check-in sign's place, radius and QR. The check-in sign is no longer a row of the point table.
  - **1.3 time picker:** one field per time `[07:00] – [09:00]` plus `+ เพิ่มช่วงเวลา`; digits only, the colon added after the second digit, Backspace takes a digit, Enter adds; saved windows are 30 px pills whose ✕ is 24 px and exactly as big as it looks (owner, later the same day: no grey circle from the browser's button face, and no invisible bigger target reaching over the time). facility-0083 amended with the owner's new rationale (2026-10-23); the browser `input type=time` stays rejected (AM/PM).
  - **Folders:** `pages/area-form/` merged into `pages/areas/` — D4 now uses D10's form, and the repo's boundaries rule forbids page→page imports (facility-0025 amended). The `/admin/areas/new` and `/admin/areas/:areaId/edit` paths are unchanged.
  - **Tests changed (reported, not silent):** `row(/AR01-IN/)` is gone (the check-in sign is Tab 2; the test asserts its region and its `พิมพ์ป้ายลงเวลา` link); `แก้ไขพื้นที่` is a button, not a link; `พิมพ์ป้ายของพื้นที่นี้ ›` → `พิมพ์ป้าย`; `+ เพิ่มจุดทำความสะอาดในพื้นที่นี้` → `+ เพิ่มจุด`; `แก้ไข AR01-IN` → the `ป้ายลงเวลา` tab; the four time boxes → the `เริ่ม` / `ถึง` fields; `เพิ่ม` → `+ เพิ่มช่วงเวลา`; a save or (de)activation now closes the editor, so the deactivation test opens the point again; the facility-0083 test now covers the colon, Backspace and Enter; two new tests (the Area form opens in a Drawer, `clockOf`/`typedTime`).
  - **Checks:** 418 tests pass, lint and `tsc -b` clean; main JS 132.04 kB gzip, CSS 28.28 / 6.00 kB gzip.
  - **Not done:** the second eye check (D4 at 1280 px).

- 2026-10-24 · #79 D4's header and view switcher, after the owner picked layout A from three mockups (`mockup-d4.html`, a throwaway in `wwwroot`, never committed). The complaint: three stacked rows of look-alike boxes — `[แก้ไขพื้นที่] [พิมพ์ป้าย] เก็บพิกัดบนมือถือ ›` then `[จุดทำความสะอาด] [ป้ายลงเวลา]` — so the eye could not tell an action from a view switch. The cause was not master–detail: it was that the tabs were made from the same button as the actions, and every action shared one left-aligned row with the name.
  - **Header:** name, shifts/Cleaner line on the left; `[แก้ไขพื้นที่]` and `[พิมพ์ป้าย]` on the right, away from the name. `เก็บพิกัดบนมือถือ ›` left the header — it is about signs — and now sits in Tab 2 beside `[พิมพ์ป้ายลงเวลา]`.
  - **Tabs:** a real tablist (`role="tab"`/`tabpanel`, `aria-selected`, roving `tabIndex`, arrows/Home/End) with a 2 px line under the words instead of a box — `tabClass` + `tabListClass` in `shared/components/styles.ts`, 36 px, documented in MASTER.md. `toggleClass` stays for D1/D2/D3, where the control really is an on/off filter. The count moved into the tab, so the point list lost the heading that said it twice.
  - **Tests changed (reported):** `getByRole('button', { name: 'จุดทำความสะอาด' })` → `getByRole('tab', { name: 'จุดทำความสะอาด (2)' })` with `aria-selected`; the `heading level 3 'จุดทำความสะอาด (2)'` is gone; the `เก็บพิกัดบนมือถือ ›` assertions moved into Tab 2 (the deactivated test now opens Tab 2 to prove the link is not there); a new test covers the arrow keys.
  - **Checks:** 419 tests pass, lint and `tsc -b` clean.
  - **Owner's decision, the same day (1A + keep apart):** `+ เพิ่มพื้นที่` stays a full page — creating is a rare setup task with six fields and a warning that the code cannot be changed later, so a 480 px Drawer would turn it into a scroll tunnel; `แก้ไขพื้นที่` stays in the open Area's header, never beside the add button. The rule ("an action sits where what it acts on sits, not by its verb") is now in MASTER.md. No code changed for either answer, so no commit besides this note.

- 2026-10-26 · #80 on branch `feat/admin-reports-new-look` (from master `d15f4c6`), started on the owner's "Implement the next plan" while the #79 eye checks are still owed. D6, D7, D8 and D9 are in the new look; `accounts.css`, `inspectionSummary.css`, `export.css` and `history.css` are deleted. `RangeFields` and `BuildingSelect` (shared by D7–D9) moved too. The rules in `shared/components/shared.css` that these pages were the last users of (`app-range*`, `app-confirm*`, `app-link-button`, `app-row--inactive`, `app-table*`, `app-field__help`, `app-button--warn`, `app-facts`) are left for #82, as the earlier tickets did. Only `pages/print-signs/print.css` (#81) is left in `pages/`.
  - **Behaviour:** unchanged; 419 tests pass with no test edited. The D6 and D8 answers stay boxes in the page (not a Toast like D2/D3), because the ticket says behaviour does not change; ask the owner whether they want Toasts.
  - **Look changes to show the owner:** D6 filters are `toggleClass` buttons instead of round pills; D6's "ยืนยัน ปิดใช้งาน" is `danger` (was the orange filled button), like D4's new QR; D6's blocked shift radios fade with their words. D7's share bar is grey (`accent-muted`), not blue, since blue means "can be pressed" and the page judges nothing; D7's counts and their headings are on the right. D7/D8/D9 range shortcuts are toggles (blue frame when on, 36 px, 8 px apart). D8's ticked set gets a blue border; its download button and back link are no longer full width. D6 and D8 go to two columns at 1024 px (`lg`) instead of 960 px. Headings 22 px, section titles 19 px; the small notes are 14 px.
  - **Review (one sub-agent, Standards + Spec):** no Critical or Important. Fixed: blocked shift radios now visibly weaker; D7's count headings aligned right; HistoryTable indentation. Noted: `Field` now links the help lines of D6's forms with `aria-describedby` (they were plain spans).
  - **Checks:** lint and `tsc -b` clean; main JS 132.73 kB gzip + `baseApi` 31.37 kB = 164.10 kB startup (budget 200); CSS 29.42 / 6.16 kB gzip.
  - **A slip, fixed:** a prettier run without the repo's options was pointed at all of `src/pages`. Every file outside this change was restored from git, and this change's files were formatted with single quotes and 120 columns (what AreaForm, AttendanceTable and Boxes already match). The repo has no prettier config; pass `--single-quote --print-width 120` if you run it, and only on the files you changed.
  - **Owner's eye check, 2026-10-26:** passed except three points, fixed in the next commit: (1) D6's phone form showed the person's values at 19 px bold — now the normal size; (2) D6's deactivate confirm is a popup (`Modal` with `role="alertdialog"`, new optional `role` and `describedBy` props; the add form stays under it), my advice and the owner's wish; the test still finds it as an `alertdialog`; (3) #79 D4: the Drawer has no border (`border-0` on the dialog; the 3 px frame was the browser's own <dialog> border, since preflight is still off). I first guessed wrong and removed the window groups' frames and a divider; that was reverted. Everything else passed. D8 and D9 were not mentioned and count as passed.
  - **Explanations always on screen (owner's question, 2026-10-26):** they were copied from the wireframes and ADRs, so they explain the system to the team, not the task to the user. Decision (owner picked "keep only what concerns the action"): a warning about what the Admin is about to do and a field's help line stay; notes about how a page counts or keeps things go behind a new `MoreInfo` (`shared/components/MoreInfo.tsx`, a native `<details>` with an ℹ and a link-coloured summary). Done for D6–D9 (D6 "เกี่ยวกับการปิดใช้งาน", D7 "วิธีนับตัวเลข", D8 "เกี่ยวกับไฟล์นี้", D9 "เกี่ยวกับหน้านี้"); D8's "(เรื่องคนเก็บไฟล์รอคุยกับ HR)" was an internal note and is deleted; "ห้ามส่งต่อนอกบริษัท" stays in view. No test changed. **The other pages (owner, 2026-10-27: same rule):** done on branch `feat/more-info-rest` (plan `docs/superpowers/plans/2026-10-27-explanations-behind-more-info.md`; the plan's table lists what moved and what stayed). D1–D5, D10, the buildings page, M17, check-in and scan; `MoreInfo` has `size="phone"` (48 px) for the phone pages. Two lines with no table row also stay in view: D2's correction-form note (`CorrectionForm.tsx:101`, what the save does) and D3's header line "เมื่อวานและวันนี้ · แอดมินเท่านั้น" (the list's scope).
  - **Not done:** the owner's second look at those three points and at the shut notes, and M17 on a phone.

- 2026-10-26 · **D7 no longer counts a submission that a later one replaced before anyone inspected it (branch `fix/summary-superseded-submissions`, stacked on `feat/admin-reports-new-look`).** The owner asked: a Cleaner sends again while the first is still waiting; does D7 show it, and is that fair to the Supervisor? Finding: the server places every submission made while the round is open in that round (OnTime), and the Supervisor is offered only the round's latest (`InspectionRules.Find`), so the earlier one can never be inspected; D7 still counted it in "งานที่แม่บ้านส่ง", so a Supervisor who inspected everything inspectable showed 50% (2 sent, 1 inspected). After the round ends, a repeat is placed Off-Round and was never counted. Owner chose: leave such submissions out.
  - **Rule (`InspectionSummary.Tally`):** a submission is left out of Submitted and SubmittedInspected when it was never inspected and a later one (by time, then id) exists in the same point, shift and round. An inspected one stays, so a rework's first submission still counts. Late-after-shift work is left out as before. NotInspected (per round), points inspected, inspections, reworks and GPS flags are unchanged. The Excel export still lists every submission.
  - **Page:** D7's "วิธีนับตัวเลข" gained one sentence saying so. The column "ตรวจทั้งหมด" is renamed "จำนวนครั้งที่ตรวจ" (owner, after asking why 4 inspections sat beside 3 submissions: it counts presses of บันทึกผลตรวจ, so one submission inspected twice counts twice, and a cover inspection counts too), and its note says so.
  - **Tests:** 4 new unit tests (the repeat is left out; two repeats with nothing inspected count once; a rework's inspected first submission stays; the same-instant tie goes by id). Unit 306 passed, API 542 passed + 5 skipped, frontend suite passed.
  - **Not done / open:** the Cleaner can still send repeatedly while a submission waits (no block). The owner was offered "stop the repeat at the source" and chose the counting fix only; revisit if repeats become a habit (the Excel export shows them).

- 2026-10-26 · **My Work updates live (branch `feat/cleaner-live-my-work`, from master `d15f4c6`, not in PR #96).** The owner saw that after the Supervisor inspects, the Cleaner's page needs "อัปเดต" to show ไม่สะอาด. This was facility-0052's decision (no push for Cleaners in the pilot), so it was asked first and the owner chose "live, the whole page".
  - **Server:** `PointNotifications` now also sends `ScanRecorded` / `InspectionRecorded` to the active Cleaners of the point's Area (both shifts) and to any active Cleaner covering that Area in the clock's shift (not cancelled). Same card, no `qrToken`. `AttendanceRecorded` stays Admin-only.
  - **Page:** `myWorkApi.getMyWork` follows the hub (`MY_WORK_HUB`) and **asks the server again** on a push and on every (re)connect, instead of patching the pushed card: the hub's card is the clock's shift's, and a Cleaner in the hour before or after their shift sees another one. The footer says whether the page is live, as My Building's does; "อัปเดต" stays for a dropped connection. The check-in and scan pages share the query, so they hold the connection too.
  - **Tests:** API 544 passed + 5 skipped (new `CleanerLiveTests`: both shifts' Cleaners of the Area get it, another Area's does not, a covering Cleaner does, a cancelled cover and a deactivated Cleaner do not; `TestData.AddCleanerAsync` takes an optional phone). Two old tests that pinned "Cleaners get nothing" now use AR02's Cleaner as the one who must get nothing. Frontend 422 passed: four tests that counted `/api/my-work` calls now wait for the hub's catch-up call first (a start always asks once more), and three live tests were added. Unit 302 passed.
  - **Docs:** facility-0052 and 0058 each have a 2026-10-26 amendment; `docs/code-map.md` corrected (its open question 3 is closed).
  - **Not done:** the owner's eye check (log in as a Cleaner on one phone and a Supervisor on another, mark ไม่สะอาด, watch the Cleaner's page change) and the PR.

- 2026-10-26 · **Merged, in this order, on the owner's "handoff commit push PR merge ให้เรียบร้อย":** #96 (`edf2418`), then #98 retargeted from `feat/admin-reports-new-look` to master (`07319cd`), then #97 (`d4e8fde`) after master was merged into its branch to resolve the one conflict, this file (both sides' entries kept). Every suite passed on the combined code before #97 merged: frontend 422, unit 306, API 544 + 5 skipped. GitGuardian passed on all three. **The eye checks the owner has not finished are now checks on master, not on a branch:** D4 / D10 (M17 on a phone), D6–D9 at 1280 px (the deactivate popup, the shut notes, the renamed column), and My Work live on two phones. Anything they find goes in a new branch.
  - **Open questions kept for the owner:** the other Admin and phone pages still carry always-visible explanations (D1–D4, D10, M17); I offered to list them page by page so the owner picks which to shut behind `MoreInfo`. The D6 and D8 answers are still boxes in the page, not Toasts.

- 2026-10-26 · **Branch cleanup (owner: "branch ไหนไม่ใช้ก็ลบทิ้ง").** All 18 merged remote branches and the 3 merged local ones are deleted; GitHub now has only `master`. No PR is open. The one branch kept is the local `prototype/tailwind-look` (the owner's chosen mockup, commit `a8dab9c`, never for master): `design-system/facility/pages/*.md` point at it, and it is on no remote now, so a deletion would lose it. Push it again or move the mockup into the repo if the owner wants it kept safe.

- 2026-10-26 · **The owner finished the eye checks** (D4 / D10 / M17, D6–D9, My Work live) and said to delete the prototype branch. Every check owed in this file is closed; #79, #80 and #97's "eye check owed" lines above are history. The local `prototype/tailwind-look` (`a8dab9c`) is deleted, so `design-system/facility/pages/*.md` still name a branch that no longer exists; the chosen layouts are written in those files. Cleanup done: API and tunnel stopped, `facility_ui_test` dropped (`facility_dashboard` untouched), `wwwroot` and `frontend/dist` removed. Next: #81 D5 print, then #82 the contract step.
- 2026-10-26 · #82 done (the contract step, see `### Start here`): the owner's eye checks of every page and the D5 paper print passed; the login link's tap area is enlarged (`inlineLinkClass`) by the owner's choice. Owner's OK: push, PR, merge and delete the branch. Owner's decision on Admin passwords: 15 characters + a common-password list, its own issue after #82 (see `## Pending work`).
- 2026-10-26 · #101 done on branch `feat/admin-password-15`, committed, not pushed (facility-0089, facility-0020 amended): an Admin password is 15–100 characters and not one of 10,898 common passwords, checked on create and on reset only; D6 says 15–100. Tests: backend unit 318 passed, API 546 passed and 5 skipped (MySQL); frontend 422 passed (47 files); lint and `tsc -b` clean. Startup JS 163.02 kB gzip (130.54 main + 32.48 baseApi), under 200 kB (facility-0062). Owner to check on D6: creating an Admin with a 14-character password is refused; so is a common one such as `1qaz2wsx3edc4rfv`; resetting an Admin password is refused the same way; a good 15-character password is accepted.

## Owner's rules (still in force)

- Never merge a PR or the younger brother's work without asking. PR #50–#53 were approved.
- MySQL: only throwaway databases `facility_*_test`; never touch `facility_dashboard`. The MySQL tests now refuse any other name.
- Ask the owner for the MySQL root password every time it is needed. Never write it into any file or into memory.
- Reply in Thai (owner, 2026-10-28 afternoon; English for a few hours that morning), short and direct. Ask the owner's questions as a popup (AskUserQuestion), not as text.
- Update this file after every task.

## System rules already decided (why the code is as it is)

- Attendance can be recorded from 60 minutes before the shift to 60 minutes after it (ADR 0069).
- Only Cleaners submit work, from Shift-In to Shift-Out; a break does not block it.
- A cover is only in that Cleaner's own regular shift.
- A round window cannot end exactly at 19:00 (Day) or 07:00 (Night).
- A deactivated Area leaves the Dashboard, the scan page and "my work".
- The attendance page never computes late or early (ADR 0050).

## Rulings made for the owner during plan 5

The 16 planning decisions are at the top of the plan-5 file (`docs/superpowers/plans/2026-10-14-accounts-and-excel-export-backend.md`), which has the detail. In short:

1. Routes are `/api/admin/accounts`, like every Admin page since plan 3.
2. Role, employee ID and username are fixed once created.
3. Deactivating a Cleaner frees their Area; activating brings them back with no Area; a Supervisor keeps building and shift; activating answers 409 if the slot is taken.
4. A deactivated Cleaner cannot be given an Area (400).
5. Changing a phone or resetting a password revokes all refresh tokens; the log row has no before or after; the same value again changes and logs nothing.
6. An Admin may reset their own password but cannot deactivate themselves.
7. On creation a Cleaner's Area is optional, a Supervisor needs building and shift, an Admin has neither.
8. Input limits: employee ID 1–20 characters, username 3–100, temporary password 15–100 (Admin only, see facility-0089), display name 1–150, phone exactly 10 Arabic digits starting with 0.
9. Fields that do not belong to the role must be omitted (400 otherwise).
10. The export is `POST /api/admin/export`, returns the `.xlsx`, covers 1–31 shift dates, one building or all, sets `Attendance` and `Submissions`.
11. No inspection sheet yet; the Supervisor plan adds it.
12. "ไม่ได้ทำ" rows come from today's setup, for shifts that have ended; a window counts only if it existed when its round started.
13. The submissions sheet names the Cleaner.
14. Issue tags are exported as stored.
15. Every cell is text, with fixed column widths.
16. Each export writes `EXPORT_EXCEL` with range, building, sets and row counts, and no row content.

## Rulings made for the owner during plan 4

The 13 planning decisions are at the top of the plan-4 file and in the PR #53 description. These were made while building it:

1. Subagents skip the steps that need MySQL; the controller runs them at the end, so the password never goes to a subagent.
2. The one-line `Utc(DateTime)` helper is repeated in several files, following `AdminSetupView`.
3. A test checks that `AttendanceRecorded` reaches Admins only (ADR 0058).
4. On an attendance correction or closing an alert, a `DbUpdateException` answers 409 only if the racing row really exists; otherwise it is rethrown. Database errors are never swallowed.
5. Each correction rule has its own test, and a test checks that a correction sends the live event.
6. No cover is offered to a Cleaner without an Area, or for a deactivated Area.
7. Tests cover closing every alert type, a note over 500 characters, and the cover-candidate rules.
8. If the Admin picks a cover who is not the person who scanned the wrong Area, that alert is not closed automatically; "คุยแล้ว" must be pressed.
9. A Blocked Scan sent with a cover assignment must be in the same shift as the cover, or the answer is 400.
10. One test set checks that a deactivated Admin with a live token gets 401 on all 7 new routes.
11. Saving the same minute the Cleaner scanned is not a change (nothing logged, no "แก้โดย Admin" badge).
12. The MySQL tests refuse a database whose name is not `facility_*_test`.

## Parked; not needed before merging

- The AttendanceRecorded live pushes (check-in/out, Admin corrections) are not wrapped like PointNotifications: a hub failure there still answers 500 after the attendance row is saved (pre-existing since plan 4).
- A Cleaner who forgot Shift-Out and is later deactivated or moved to another shift: that alert can no longer be filled or closed. Normal date ranges do not show it.
- A wildly wrong date (e.g. year 9999) gives 500 instead of 400.
- Two Admins closing an alert or assigning a cover at the same moment: assigning may give 500 (nothing is left half-saved).
- The attendance window (opens/closes) is computed in 3 places; it should become `AttendanceCalendar.WindowOf`.
- `AuditLogQuery` runs 2 queries; one join would do.
- History paging: `NextBeforeId` offers an empty next page when the rows exactly fill the limit.
- No test yet that every timestamp in every response ends in `Z`.
- Retrying POST /api/inspections writes a second Inspection Record (Passed) or gets 409 (Rework); the page must disable the button while posting.
- Supervisors' building covers (facility-0048) can be assigned from a Blocked Scan on D3, but no endpoint lists them, so the alert page cannot show or cancel them (plan 11 decision 10).
- From plan 3: a point can be added to a deactivated Area; two Admins editing one point at once get 500 instead of 409; the 404 tests of the sign routes are missing; removing a round window loads its Scan Records into memory.
- D8 shows no row counts before the download; the API would need a dry-run endpoint (plan 12 decision 5).

## Notes for the frontend plan

- Plan 7 built the base: add a page as `src/pages/<page>/` with `<page>Api.ts` (`baseApi.injectEndpoints`), a `.css` with the page prefix and an `index.ts`; add its route in `router.tsx` under the right `ProtectedRoute`; test with `renderApp` + `fakeServer`.
- QR Sign URLs (plan 8): a check-in sign prints `<origin>/check-in/<token>`, a point sign `<origin>/scan/<token>`. Plan 10's print page (`/admin/print`) prints them with the printing page's origin, so print from the server's https address.
- `/scan/:token` opens the Cleaner's ScanPage or the Supervisor's InspectPage by role (`shared/components/ByRole`, plan 9); an Admin gets no-permission. `/check-in/:token` is Cleaner-only.
- Admin reads use `freshOnOpen` (ask the server on every open) and the print page refetches before printing: plan 10's final review overruled plan decision 13, because M17 runs on a second device.
- Shared for later pages: `shared/geo/readLocation.ts`, `shared/api/scanAnswers.ts` (`blockedScanOf`, `isNotSent`), `LocationHelp`, `BlockedScanScreen` (`sign` is `check-in`, `point` or `inspect`; `inspect` is the Supervisor's wording, plan 9), `StatusChip`, `test/fakeGps.ts`.
- Live updates (plans 9 and 11): open the hub with `followScanHub({ key, … })` (`shared/api/scanHub.ts`) in the query's `onCacheEntryAdded`. `onPoint` patches the cache (ScanRecorded, InspectionRecorded), `onAttendance` takes AttendanceRecorded (Admins only), and `onMissed` refetches by tag. Each connection's status is `selectHubStatus(key)` (`store/slices/hubSlice.ts`), and `LiveBadge` shows it. Admin live pages use `livePoll` (every open, 30 s, focus). Tests drive the fake hub with `hub.latest()` from `src/test/fakeSignalR.ts`.
- The server leaves out null fields in REST and hub JSON. Test them by truthiness; plan 9 Task 1 fixed the three `shift === null` checks of plan 8.
- Supervisor scan: call `POST /api/inspections/scan` when the page opens. Send `submission.id` back as `scanRecordId`, and on 409 scan again.
- `CoverAssignmentDto.cleanerName` is now `personName`. A cover has either `areaId`/`areaCode` or `buildingId`/`buildingCode`. `CoverSuggestionDto` likewise has `areaId` or `buildingId`.
- Alerts carry `blockReason` (`OtherArea`, `OtherBuilding`, `OutsideShift`) and the new kind `InspectionGps`.
- Cards in ต้องแก้ไข carry `rework { defect, requestedAt }`.
- Inspection Summary `notInspected` counts finished rounds left ไม่ได้ตรวจ in closed shifts, not submitted minus inspected; work a Cleaner submits after the shift end (inside the 60-minute window) cannot be inspected by anyone and is left out of the beat's numbers (plan 6 decision 17).
- Cleaners still get no hub events. My Work reloads on open.
- Disable the inspect button while POST /api/inspections is in flight: a retried Passed writes a second Inspection Record, and a retried Rework gets 409.
- Key alerts by kind **and** id: the "far from sign" and "no Shift-Out" alerts of one attendance share an id.
- Leave out unused filters entirely; an empty value gets 400. The same holds for unused account fields in plan 5.
- `PUT /api/admin/accounts/{id}` replaces every place field: leaving `areaId` out clears a Cleaner's Area, so the client sends the current `areaId` to keep it.
- The "no Shift-Out" alert has no shift or check-in time yet for the D3 layout.
- The attendance page does not yet return the Admin's reason to show beside "แก้โดย Admin" (D2).
- Admin pages (plans 10–12): add a page as a `lazy` route under the Admin guard in `router.tsx`, its path to the lazy-route list in `router.test.tsx`, and its link to `shared/components/AdminNav.tsx` if it belongs in the menu. Admin setup data uses the `AdminSetup` tag; the live board uses `ServicePoints`, `AttendanceBoard` (one per shift) and `Alerts`; the report pages (plan 12) use no tag and ask on every open. `src/test/adminFixtures.ts`, `src/test/liveFixtures.ts` and `src/test/reportFixtures.ts` have made-up Admin answers.
- Date ranges (plan 12): `shared/utils/dateRange.ts` (`checkRange` with the server's limits, `readRange`, `toRangeParams`, `exportLink`) and `shared/components/RangeFields.tsx` (typed dd/mm/yyyy with the shortcuts). `thaiToday` is in `shared/utils/thaiTime.ts`.
- The print page's `@page` rule (A4, no margin) is in the one CSS bundle, so it applies to anything the browser prints.
- M17 on a real phone over https is still to be checked: a desktop browser with a fixed simulated location may report one reading only (the real run nudges it), while phones report about once a second.
- Bundle after plan 12: all the JavaScript a phone loads from `index.html` is 157.35 kB gzip of 200 kB; 157.93 kB after plan 13. Admin pages, the three report pages included, are their own files.
- Screen words follow facility-0085 (พื้นที่, แอดมิน, ตำแหน่ง, ไกลป้าย, เลยเวลา, ส่งเพิ่ม (ไม่นับรอบ), สิ่งที่ต้องแก้, สร้าง QR ใหม่ …); code, API and database names keep the glossary words. New text uses the screen words.
- `GET /api/my-work` sends `ownShift` (facility-0077); `GET /api/attendance/signs/{token}` checks a check-in sign on open (facility-0082). D3 reads a shift ahead with the board's `shiftDate` and `shift` (facility-0084).

## Before plan 5

- At deployment: the app's MySQL user must not have UPDATE/DELETE on `audit_log` (ADR 0057).

## Open items to tell the owner

- The MySQL root password that was in the plan-2 file is scrubbed in the working copy but **still in git history**, and the repo is public. Change the root password on this laptop.
- GitGuardian once flagged `LoginTests.cs`. It is a made-up test credential; whoever manages GitGuardian should mark it Resolved as a test credential.
- `docs/roadmap.html` was committed on this branch by the owner (`0e9ab37`); it is not part of any plan.
- Build warning NU1903: `Microsoft.AspNetCore.SignalR.Common` 10.0.12 has a known high-severity advisory. It comes in through `Microsoft.AspNetCore.SignalR.Client` in the ApiTests project (it existed before plan 5, and plan 5 did not cause it). Bump the package when a patched version exists.

## Working method that works

- Plan with the `sp-writing-plans` skill, then run it with `sp-subagent-driven-development`: one subagent per task, a review per task, and a whole-branch review at the end.
- Test against the real API: run the API in Development with `ConnectionStrings__DefaultConnection` pointing at a throwaway database, then drive it with a Python script (use `python -X utf8` when printing Thai).
- `dotnet test backend/FacilityRealtime.slnx` runs every test.
- New migration: `dotnet ef migrations add <Name> --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api` (run `dotnet tool restore` first).

### Quirks of the company Windows laptop

- `gh` is in `...\WinGet\Packages\GitHub.cli_Microsoft.Winget.Source_8wekyb3d8bbwe\bin`; add it to PATH yourself.
- Thai text in a shell heredoc breaks; write such files with the Write or Edit tool.
- Stop any running API before building, because Windows locks its DLLs.
- `TZ=Asia/Bangkok date` in Git Bash prints UTC, not Thai time.

### Setting up a new machine (e.g. the Mac)

Install the .NET 10 SDK, MySQL 8.0, the GitHub CLI and Claude Code. Then `git clone`, check out the branch, run `dotnet tool restore` and `dotnet test backend/FacilityRealtime.slnx`.
- 2026-10-14 · PR #54 merged by the owner (`c530c84`). Plan 5 is done. Next: the owner chooses the next plan (see Pending work).
