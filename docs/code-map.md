# Code Map — which file does what

Includes the 2026-11-03 retention implementation. Current checkout/publication status is in [handoff](handoff.md); the tables below describe code, not a confirmed production deployment.

> **Read this when you open the repo and do not know where anything is.**
> `docs/architecture.md` explains how the parts *connect* (deployment, network, MySQL schema).
> This page explains what each *file* is for, and walks the two flows everyone asks about first: **login** and **scan → GPS → dashboard**.
> Decisions behind the code are in [docs/adr/](adr/). Words are in [CONTEXT.md](../CONTEXT.md).

```mermaid
graph TD
    QR["QR sign on the wall<br/>/scan/token or /check-in/token"] --> Pages
    subgraph FE["frontend/src — React 19 + TypeScript"]
        Pages["pages/ — 17 feature folders"]
        Shared["shared/ — api, auth, geo, components, utils"]
        Router["main.tsx, App.tsx, router.tsx"]
        Store["store/ — auth, hub, dashboard"]
    end
    subgraph BE["backend/src — ASP.NET Core 10"]
        Endpoints["Endpoints/ — 53 HTTP routes"]
        Rules["Application/ — pure rules, no I/O"]
        Persist["Infrastructure/ — EF Core, PBKDF2, Excel"]
        Hub["Hubs/ScanHub — /hubs/scan"]
    end
    DB[("MySQL — 15 tables")]
    Shared -->|"HTTP JSON /api/*"| Endpoints
    Shared -.->|"WebSocket"| Hub
    Hub -.->|"push: ScanRecorded"| Shared
    Endpoints --> Rules
    Endpoints --> Persist
    Persist --> DB
    Router --> Pages
    Store --> Shared
```

The picture above is the whole system: a phone opens a URL from a printed QR sign, React renders the page, the page talks to the API over `/api/*`, the API applies the pure rules, EF Core writes MySQL, and any change is pushed back to open dashboards over SignalR.

---

## Table of contents

- [Part 1 — What this app does](#part-1--what-this-app-does)
- [Part 2 — Where to start reading](#part-2--where-to-start-reading)
- [Part 3 — Frontend file map](#part-3--frontend-file-map)
- [Part 4 — Route table](#part-4--route-table)
- [Part 5 — State management](#part-5--state-management)
- [Part 6 — Backend file map](#part-6--backend-file-map)
- [Part 7 — Login flow](#part-7--login-flow)
- [Part 8 — Scan, GPS and real-time flow](#part-8--scan-gps-and-real-time-flow)
- [Part 9 — API reference](#part-9--api-reference)
- [Part 10 — Data model](#part-10--data-model)
- [Part 11 — Tests that pin behaviour](#part-11--tests-that-pin-behaviour)
- [Part 12 — Limits and remaining work](#part-12--limits-and-remaining-work)

---

## Part 1 — What this app does

A factory cleaning-inspection system: cleaners scan a printed QR sign at each service point to record that they cleaned it, supervisors scan the same sign to judge the work, and admins watch everything live on a dashboard. GPS is captured on every scan to flag distance or accuracy outside the sign radius; real indoor accuracy still needs field validation. Three roles, three different home pages, one URL per sign.

**Tech stack**

| Layer | What |
|---|---|
| Frontend | React 19.2.8, TypeScript, Vite 8, react-router 7, Redux Toolkit + RTK Query, Tailwind 4, Vitest |
| Real-time | `@microsoft/signalr` — one hub at `/hubs/scan`, server → client only |
| Backend | ASP.NET Core 10 Minimal API (no controllers), four projects: Api references Application + Infrastructure; Infrastructure references Application + Domain; Application references Domain |
| Persistence | EF Core 10 + `MySql.EntityFrameworkCore`, snake_case tables, 6 migrations |
| Auth | JWT access token 5 minutes (HS256) + rotating refresh token in an `HttpOnly` cookie scoped to `/api/auth` |
| Passwords | PBKDF2-HMAC-SHA256, 600 000 iterations |
| Tests | xUnit API/unit tests, Vitest frontend tests, Playwright real-API browser workflows; latest recorded results in handoff |

---

## Part 2 — Where to start reading

If you have just opened the repo, read in this order. Each step is one file.

| # | Question in your head | File to open | What you will see |
|---|---|---|---|
| 1 | "What routes exist?" | `frontend/src/router.tsx` | The whole route table as data. Route guards, role homes and lazy Admin routes. |
| 2 | "What runs at boot?" | `frontend/src/main.tsx` | Store → session refresh → tap feedback → render. Then `App.tsx` (three providers, that is all). |
| 3 | "How does the page get data?" | `frontend/src/shared/api/baseApi.ts` | The single `createApi`. Every request in the app goes through this one file: Bearer header, 401 → refresh → retry, timeouts, cache tags. |
| 4 | "How does login work?" | [Part 7](#part-7--login-flow) | Full trace with file names. |
| 5 | "How does a scan work?" | [Part 8](#part-8--scan-gps-and-real-time-flow) | Full trace with file names. |
| 6 | "What does the server do with a request?" | `backend/src/JanitorialManagementDashboard.Api/Program.cs` | Composition root, numbered 1–9 in comments. Then any file in `Endpoints/`. |
| 7 | "Where are the business rules?" | `backend/src/JanitorialManagementDashboard.Application/` | Pure static classes, no database, no HTTP. The easiest code in the repo to read and test. |

**Three habits that make this repo readable**

1. **One folder per feature.** `frontend/src/pages/scan/` owns the cleaner's scan page and nothing else. A page may not import another page — `eslint.config.js` enforces it.
2. **One API file per feature.** Each page folder has an `*Api.ts` that calls `baseApi.injectEndpoints`. Grep for a route string there and you will find the caller.
3. **Rules live apart from I/O.** Shared business rules sit in `backend/src/JanitorialManagementDashboard.Application/` mostly as static classes. LoginThrottle is a stateful in-memory gate; endpoints handle request validation, authorization and persistence.

---

## Part 3 — Frontend file map

Root: `frontend/src/`; UI and related hooks/rules are grouped by feature.

### Root files

| File | One line |
|---|---|
| `main.tsx` | Boot: create store → `setupListeners` → `startSession()` (refresh before React) → tap feedback → render. |
| `App.tsx` | Three nested providers only: `<Provider store>` → `<SessionGate>` → `<RouterProvider>`. No context, no toast provider, no theme provider. |
| `router.tsx` | The entire route table as plain data. No loaders, no actions — RTK Query owns data loading. |
| `index.css` | Tailwind 4 setup and the `@theme` design tokens that mirror `design-system/janitorial/MASTER.md`. |

### shared — the layer every page may import

| Folder | Files worth knowing | Purpose |
|---|---|---|
| `shared/api/` | `baseApi.ts` | **The single `createApi`.** `baseUrl = ${origin}/api`, Bearer header from `state.auth.accessToken`, `baseQueryWithReauth` (401 → one shared refresh → retry once, skipped for `/auth/*`), `SEND_TIMEOUT_MS = 20_000`, tag types, and the two standard hook options `freshOnOpen` and `livePoll` (30 s + refetch on focus). |
| | `types.ts` | Server DTOs shared by pages. Note: the server omits null fields, so test with truthiness. |
| | `buildingNames.ts` | `buildingOptionText` / `buildingText` / `useBuildingNames`: the Admin pages' `code · current name` for a building (facility-0091). Phones keep the bare code. |
| | `adminSetupApi.ts`, `attendanceBoardApi.ts`, `alertListApi.ts`, `myWorkApi.ts` | Endpoints shared by more than one page. |
| | `scanHub.ts` | `followScanHub()` — one SignalR connection per RTK Query cache entry, retry ladder, `onMissed` refetch on every reconnect. Loads `@microsoft/signalr` by dynamic import, so it is not in the main JS. |
| | `scanAnswers.ts` | Non-hook helpers: `blockedScanOf` (403 + areaCode = a blocked scan), `isNotSent` (why the send failed). |
| `shared/auth/` | `session.ts` | Single-flight `refreshSession`, `startSession`, `getValidAccessToken`, `abandonSession`. Raw `fetch` to `/api/auth/refresh` with a 15 s timeout. |
| | `endSession.ts` | Logout: forget locally → await in-flight refresh → `POST /api/auth/logout`. |
| | `roles.ts` | `homeOf(role)`: cleaner → `/my-work`, supervisor → `/my-building`, admin → `/dashboard`. |
| `shared/geo/` | `readLocation.ts` | Fresh scan reads and permission-retry detection; setup capture separately uses watchPosition. See [Part 8](#part-8--scan-gps-and-real-time-flow). |
| `shared/components/` | `Button`, `Field`, `Note`, `StatusChip`, `Drawer`, `Modal`, `ConfirmationDialog`, `LocationHelp` | Shared Tailwind UI, feedback and route/layout components. |
| `shared/cover/` | `coverApi.ts`, `CandidateChoices.tsx`, `CoverCancellationDialog.tsx`, `useCoverCancellation.ts`, `coverCandidates.ts`, `coverSlots.ts`, `coverText.ts` | Cover Assignment endpoints, UI and helpers shared by Alerts and Attendance. The Alerts form and alert review mutation live in `pages/alerts/`. |
| `shared/utils/` | `thaiTime.ts`, `getErrorMessage.ts` (31), `labels.ts` (26), `dateRange.ts` (11), `pointText.ts`, `tapFeedback.ts` | Pure helpers. `thaiTime.ts` is the Asia/Bangkok clock — every time you see a strange time, look here first. |
| `shared/version/` | `buildInfo.ts` | The build commit shown on the login pages. |

### pages — 17 feature folders

| Folder | Purpose | Key files |
|---|---|---|
| `auth/` | Both login screens | `LoginPage.tsx`, `AdminLoginPage.tsx`, `useLogin.ts` (the hook both pages share), `authApi.ts` |
| `my-work/` | Cleaner's own task list | `MyWorkPage.tsx`, `workRules.ts` (status buckets) |
| `check-in/` | Shift in/out at the area's check-in sign | `CheckInPage.tsx`, `checkInApi.ts`, `AttendanceTimeline.tsx` |
| `scan/` | **Cleaner submits cleaning at a sign** | `ScanPage.tsx` (state machine), `SubmitForm.tsx`, `SubmitResult.tsx`, `sendFailed`/`OffDuty`, `scanApi.ts`, `scanRules.ts` |
| `inspect/` | **Supervisor judges that submission — same URL, different role** | `InspectPage.tsx`, `InspectForm.tsx`, `NothingToInspect.tsx`, `DistanceLine.tsx`, `inspectApi.ts`, `inspectRules.ts` |
| `my-building/` | Supervisor's queue of what to inspect next | `MyBuildingPage.tsx`, `buildingRules.ts`, `myBuildingApi.ts` (hub follow) |
| `dashboard/` | Admin live point board (D1) | `DashboardPage.tsx`, `PointTile.tsx`, `boardRules.ts`, `dashboardApi.ts` (hub follow) |
| `attendance/` | Admin attendance board + corrections (D2) | `AttendancePage.tsx`, `CorrectionForm.tsx`, `attendanceRules.ts` |
| `alerts/` | Empty areas, covers, alert list (D3) | `AlertsPage.tsx`, `CoversThisShift.tsx`, `alertRules.ts` |
| `areas/` | Area / point / sign setup (D4, D10) | `AreasPage.tsx`, `PointEditor.tsx`, `SignPanel.tsx`, `signRules.ts` (radius 10–500 m) |
| `buildings/` | Building setup: add, rename, on/off (facility-0090) | `BuildingsPage.tsx`, `BuildingDrawer.tsx`, `buildingRules.ts` (same counts as the server's `BuildingRules`) |
| `accounts/` | Account CRUD, no delete (D6) | `AccountsPage.tsx`, `CreateAccountForm.tsx`, `SecretForms.tsx` |
| `print-signs/` | QR sign printing (D5) | `PrintSignsPage.tsx`, `qr.ts` + `printRules.ts` — `signUrl()` builds `/scan/<token>` |
| `capture/` | Admin records a sign's GPS standing at the site (M17) | `CapturePage.tsx`, `captureLocation.ts` (30 s, median of ≥3 readings) |
| `inspection-summary/` | Per-supervisor tallies (D7) | `InspectionSummaryPage.tsx`, `summaryRules.ts` |
| `export/` | Excel export (D8) | `ExportPage.tsx`, `exportRules.ts` (1–31 days), `saveFile.ts` |
| `history/` | Audit log with previous/next navigation over API cursors (D9) | `HistoryPage.tsx`, `historyRules.ts` (max 92 days) |

### store

Three slices plus RTK Query — see [Part 5](#part-5--state-management).

### test

`renderApp.tsx` (renders App with a memory router), `fakeServer.ts` (fake `fetch`), `fakeSignalR.ts`, `fakeGps.ts`, `fakeDownload.ts`, plus fixtures per role. `fakeGps.ts` is how tests script the phone's location without a phone.

### e2e (facility-0094)

Playwright runs through `npm run e2e` → `scripts/e2e.sh` → `scripts/e2e.py`. Each run publishes to its own ignored directory, creates a unique `facility_e2e_<timestamp>_<suffix>` database and restricted user, seeds synthetic data through `backend/tools/JanitorialManagementDashboard.E2ESeed`, and cleans up only resources it can prove it owns. It does not reset a fixed `facility_e2e_test` database or the shared `wwwroot` (facility-0102 amends 0094).

`frontend/e2e/phone/` and `desktop/` cover role workflows, setup/reporting and network/GPS conditions. `frontend/e2e/support/seed.ts` centralizes fixture credentials with `DbInitializer.cs`; `pageWatch.ts` watches browser errors, CSP and layout. Chromium is headed with 350 ms slow motion by default; choose WebKit/Firefox with `E2E_BROWSER`. [E2E guide](testing/e2e.md) covers profiles, retained reports, engine skips and explicit ownership-checked cleanup.

---

## Part 4 — Route table

From `frontend/src/router.tsx`. Guards come from `shared/components/ProtectedRoute.tsx`, role homes from `shared/auth/roles.ts`.

| Path | Page file | Roles allowed |
|---|---|---|
| `/` | `shared/components/HomeRedirect.tsx` → `homeOf(role)` | any |
| `/login` | `pages/auth/LoginPage.tsx` | public |
| `/login/admin` | `pages/auth/AdminLoginPage.tsx` | public |
| `/my-work` | `pages/my-work/MyWorkPage.tsx` | cleaner |
| `/check-in/:token` | `pages/check-in/CheckInPage.tsx` | cleaner |
| `/scan/:token` | `shared/components/ByRole.tsx` → cleaner: `pages/scan/ScanPage.tsx`, supervisor: `pages/inspect/InspectPage.tsx` | cleaner, supervisor |
| `/my-building` | `pages/my-building/MyBuildingPage.tsx` | supervisor |
| `/dashboard` | `pages/dashboard/DashboardPage.tsx` (lazy) | admin |
| `/admin/attendance` | `pages/attendance/AttendancePage.tsx` (lazy) | admin |
| `/admin/alerts` | `pages/alerts/AlertsPage.tsx` (lazy) | admin |
| `/admin/inspection-summary` | `pages/inspection-summary/InspectionSummaryPage.tsx` (lazy) | admin |
| `/admin/export` | `pages/export/ExportPage.tsx` (lazy) | admin |
| `/admin/areas/:areaId?` | `pages/areas/AreasPage.tsx` (lazy) | admin |
| `/admin/areas/new` | `pages/areas/AreaFormPage.tsx` (lazy) — outranks `:areaId?` | admin |
| `/admin/areas/:areaId/edit` | `pages/areas/AreaFormPage.tsx` (lazy) | admin |
| `/admin/buildings` | `pages/buildings/BuildingsPage.tsx` (lazy) | admin |
| `/admin/print` | `pages/print-signs/PrintSignsPage.tsx` (lazy) | admin |
| `/admin/accounts` | `pages/accounts/AccountsPage.tsx` (lazy) | admin |
| `/admin/history` | `pages/history/HistoryPage.tsx` (lazy) | admin |
| `/admin/areas/:areaId/capture` | `pages/capture/CapturePage.tsx` (lazy) | admin |
| `*` | inline `StatusScreen` 404 | anyone |

Admin routes are lazy-loaded so phones never download them ([facility-0062](adr/facility-0062-phone-bundle-budget-200kb-gzip.md)).

**The guard's own comment is the important part:** `ProtectedRoute.tsx` says it *"hides pages, it does not protect data: the server's policies do that."* Never assume a hidden button means a forbidden action — check the endpoint.

---

## Part 5 — State management

```mermaid
graph TD
    subgraph Redux["Redux store — frontend/src/store/index.ts"]
        Auth["auth slice<br/>currentUser, accessToken, expiresAt<br/>MEMORY ONLY — never localStorage"]
        Hub["hub slice<br/>connection status per follow key"]
        Dash["dashboard slice<br/>D1 filters"]
        Api["api slice<br/>the whole RTK Query cache"]
    end
    Local["Everything else<br/>useState drafts inside the page"]
    Auth -->|"Bearer header"| Base["shared/api/baseApi.ts"]
    Base -->|"401 → refresh → retry"| Session["shared/auth/session.ts"]
    Hub -->|"LiveBadge: orange only when down"| Badge["shared/components/LiveBadge.tsx"]
    Reset["listener middleware<br/>on sessionExpired or signedOut"] -->|"resetApiState()"| Api
```

| Slice | File | Holds |
|---|---|---|
| `auth` | `store/slices/authSlice.ts` | `currentUser`, `accessToken`, `expiresAt`, `isBootstrapping`, `signedOutByUser`. **Memory only — nothing is ever written to localStorage** (facility-0013). Refreshing the tab forgets the token on purpose; the cookie brings it back. |
| `hub` | `store/slices/hubSlice.ts` | `connections: Record<key, 'connecting'｜'live'｜'reconnecting'>` — one key per SignalR follow. |
| `dashboard` | `store/slices/dashboardSlice.ts` | D1 building, status, issue-only, search and problem-first sorting; session end resets them. |
| `api` | `baseApi.reducerPath` | The RTK Query cache — all server state. |

**Rules of thumb**

- **Server data → RTK Query.** There is no axios and no plain `fetch` for data. The only two raw `fetch` calls in `src` are `shared/auth/session.ts` and `shared/auth/endSession.ts`.
- **Form drafts → local `useState`.** Heaviest users: `pages/areas/SignPanel.tsx` (8), `pages/export/ExportPage.tsx` (6), `pages/areas/PointEditor.tsx` (6).
- **No React Context anywhere** — grep for `createContext` returns 0 hits.
- **Refetch policy** is one of three constants: `freshOnOpen` (phone and setup pages ask again every open) or `livePoll` (admin live pages, 30 s + on focus), both from `baseApi.ts`, or `namesFreshness` from `shared/api/buildingNames.ts` (30 s, building names only).
- **SignalR push** is wired in four cache entries via `onCacheEntryAdded → followScanHub`: `pages/dashboard/dashboardApi.ts`, `pages/my-building/myBuildingApi.ts`, `shared/api/attendanceBoardApi.ts` and `shared/api/myWorkApi.ts`.

---

## Part 6 — Backend file map

Solution `backend/JanitorialManagementDashboard.slnx` — four projects, no controllers.

### `JanitorialManagementDashboard.Api`

| Path | Purpose |
|---|---|
| `Program.cs` | Composition root, numbered 1–9 in comments: services → middleware → migrate → hub → endpoint groups → SPA fallback. |
| `Endpoints/` | All HTTP routes. `*Endpoints.cs` registers routes; the rest are helpers (`ThaiLabels`, `GpsInput`, `ApiResults`, `BlockedScans`, `PointNotifications`, `ExportSheets`, `AdminQuery`, `AccountChecks`, read-model builders). |
| `Auth/` | `AuthSetup.cs` (JWT bearer, `AdminOnly` policy, SignalR query-string token), `JwtAccessTokenIssuer.cs`, `JwtKeys.cs`, `AuthClaims.cs`, `CurrentUser.cs`. |
| `Hubs/ScanHub.cs` | Server → client only, **zero hub methods**. Every push addresses active user ids read from the database at send time; no Admin group is joined (0105). |
| `DTOs/` | One request/response record set per feature. |
| `SecurityHeaders.cs` | CSP and other browser security headers; `/api` answers use `no-store`. HSTS and forwarded-header trust are configured in `Program.cs` (0106). |
| `Auth/SecurityLog.cs` | Redacted security events, including normalized login IP, through `ILogger` → Serilog Console (0103, 0104). |
| `Commands/ResetAdminCommand.cs` | Console account recovery; `FirstAdmin` / `ServerAdminAccounts` create the first Admin from settings (0092). |
| `Retention/RetentionWorker.cs` | Startup catch-up and nightly Bangkok schedule, fresh DI scope per attempt, cancellation and error retry. Testing does not auto-start it. |
| `PrecompressedFiles.cs` | Sends a frontend file's build-time `.br`/`.gz` copy when the browser accepts it (facility-0067 amendment). |
| `wwwroot/` | The built frontend, copied in by `npm run build:api`; `frontend/precompress.ts` writes a `.br` and a `.gz` beside each text file. |

### `JanitorialManagementDashboard.Application` — read this first, it is the rules

Business rules without database or HTTP dependencies, mostly static; LoginThrottle holds in-memory counters:

| Area | Files |
|---|---|
| `Areas/` | `AreaRules` (an Area is turned off only once no active Cleaner holds it; facility-0091) |
| `Auth/` | `LoginThrottle`, `LoginPlace`, `PhoneNumber`, `RefreshTokenRules`, `JwtSettings`, `IPasswordHasher` |
| `Geo/` | **`Geofence.cs`** (haversine distance + within-radius verdict), `GpsStamping.cs` |
| `Rounds/` | **`RoundPlacer.cs`** (OnTime / Late / Rework / OffRound), `PointStatusCalculator`, `RoundWindow`, `RoundRules` |
| `Attendance/` | `AttendanceRules`, `AttendanceCalendar`, `AreaAttendance`, `AttendanceSettings` |
| `Inspections/` | `InspectionRules` (what may be judged), `SupervisorScope`, `InspectionSummary` |
| `Common/` | `ThaiTime` — Asia/Bangkok ↔ UTC. Every wall-clock conversion goes through here. |
| Others | `Accounts/AccountRules`, `Accounts/CommonPasswords` (the embedded `common-passwords.txt` list of common passwords, read once; facility-0089), `Alerts/AlertKind`, `Signs/SignCodes`, `Export/ExportRules`, `Shifts/ShiftCalendar` |

### `JanitorialManagementDashboard.Infrastructure`

| Path | Purpose |
|---|---|
| `Persistence/AppDbContext.cs` | All EF mapping: 15 `DbSet`s, snake_case columns, unique indexes, computed columns, check constraints. |
| `Persistence/Retention/` | Scoped RetentionRunner: GPS SQL batches, source/dependent cleanup and atomic PointMonthlySummary archives. |
| `Persistence/*Query.cs` | Reusable read models: `PointBoardQuery`, `RoundFactsQuery`, `AlertQuery`, `AuditLogQuery`, `ExportQuery`, `InspectionHistoryQuery`. |
| `Auth/Pbkdf2PasswordHasher.cs` | PBKDF2-HMAC-SHA256, 600 000 iterations, `pbkdf2-sha256$…` format. Also used for the phone number-as-secret. |
| `Export/ExcelWorkbook.cs` | ClosedXML `.xlsx` writer. |
| `Migrations/` | 6 migrations + model snapshot, applied at startup with `MigrateAsync()`. |
| `Persistence/DbInitializer.cs` | Dev-only seed (`admin` / `admin1234`). Never runs outside Development. |

### `JanitorialManagementDashboard.Domain`

`Entities/` models the 15 application tables and the `IGpsStamped` interface; `Enums/` holds shared domain values. Entities include small domain helpers such as Area.HasShift.

**Design shape to internalise:** endpoints inject `AppDbContext` directly. There is no repository layer and no service layer for business logic. If you go looking for `IOrderService`, it does not exist here — the rule is a static class in `Application`, the endpoint calls it.

---

## Part 7 — Login flow

There are two login screens because the three roles authenticate differently: cleaners and supervisors have no password, only employee ID + phone (facility-0054); admins have username + password.

### The files, in the order the request travels

| Step | File | What happens |
|---|---|---|
| 1 | `pages/auth/LoginPage.tsx` | Two fields. Client-side validation is **only** HTML5 `required` plus `.trim()` on the employee id — no format check. Submit calls `submit({ employeeId, phone })`. |
| 2 | `pages/auth/useLogin.ts` | The hook both login pages share: `submit` → `login(body).unwrap()` → `dispatch(setCredentials(response))`. Maps errors: 401 → the page's own Thai message, 429 → throttle message, otherwise `getErrorMessage()`. |
| 3 | `pages/auth/authApi.ts` | `POST /auth/login` — the RTK Query mutation. |
| 4 | `shared/api/baseApi.ts` | `prepareHeaders` attaches `Authorization: Bearer <accessToken>` if one exists. |
| 5 | `backend/.../Endpoints/AuthEndpoints.cs` | `LoginAsync` — see the numbered steps below. |
| 6 | `store/slices/authSlice.ts` | `setCredentials` copies `user`, `accessToken`, `expiresAt` into **memory**. |
| 7 | `shared/components/ProtectedRoute.tsx` + `shared/auth/roles.ts` | Next navigation: `state.from` if there was one, else `homeOf(role)`. |

### What LoginAsync actually does

1. Pick employee ID + phone or Admin username + password, trim the login name and normalize the connection IP with `LoginPlace` (IPv4 address, IPv6 /64).
2. Overlong login names → bare `401` and an unknown-account Server Log line; no throttle entry is kept.
3. Role-separated lookup: employee ID matches a non-Admin; username matches an Admin. Known-account spellings share `user:{id}`, unknown names use a mode-prefixed key.
4. `LoginThrottle.TryBeginAttempt(key, place)` checks the name/IP pair (5 attempts per 15 minutes) and IP-wide failures (20 per 15 minutes). A locked gate returns `429` without checking the secret (0104). Pair attempts are reserved before verification to bound concurrency; success clears that pair. Only failures increment the IP-wide counter.
5. Verify the real hash or a dummy PBKDF2 hash for an unknown name.
6. Unknown, inactive or wrong-secret → `401`, no cookie, a redacted security log and an IP failure increment. The public response does not distinguish these cases.
7. Success clears its own pair only, saves a SHA-256 refresh-token hash, writes the HttpOnly cookie and returns `{ accessToken, expiresAt, user }`. All `/api` responses carry `Cache-Control: no-store` (0106).

### The two tokens

| | Access token | Refresh token |
|---|---|---|
| What | JWT, HS256 | 256 random bits, base64url — not a JWT |
| Lifetime | **5 minutes** (`JwtSettings.AccessTokenMinutes`) | **30 days**, rotated on every use |
| Where it lives | `store/slices/authSlice.ts` — Redux memory, gone on refresh | `facility_refresh` cookie: `HttpOnly`, `SameSite=Strict`, **`Path=/api/auth`** |
| What is in the DB | nothing (verified per request) | only `RefreshTokenRules.Hash(token)` |
| Claims | `sub`, `preferred_username`, `name`, `role` | — |

### Session lifecycle

**Attachment** — `shared/api/baseApi.ts`. Every RTK Query request carries the Bearer header.

**Refresh fires in exactly three places** (there is no timer):

| Trigger | Code |
|---|---|
| Page load | `main.tsx` `void startSession(store.dispatch)` — deliberately outside React so StrictMode cannot fire two. The splash `SessionGate.tsx` waits for it so a live session never flashes the login page. |
| Any 401 on a non-auth request | `baseApi.ts` — refresh, then **retry once**. `isAuthCall` excludes `/auth/*` so a wrong password never triggers a refresh. |
| SignalR connect / reconnect | `session.ts getValidAccessToken` — refreshes if less than 30 s remain (`REFRESH_MARGIN_MS`), otherwise returns the stored token. |

**Single flight** — `session.ts`. All callers share one refresh promise, because *"the server treats a second use of the same refresh token as theft and ends the session."*

**Outcomes** — `session.ts` returns `'refreshed' | 'rejected' | 'offline'`:

- `rejected` (401/403) → `dispatch(sessionExpired())` → listener middleware resets the whole API cache → `ProtectedRoute` sees `currentUser === null` → redirect to `/login` **with** `state.from`, so the person lands back where they were.
- `offline` (network down, 5xx, non-JSON 200, 15 s timeout) → **session kept**. A phone losing signal must not log the person out.

**Server side of refresh** — `AuthEndpoints.cs` with `RefreshTokenRules.Decide`: rotate normally; reuse after the 30 s grace revokes the **whole session** (two holders means theft); refresh also rejects inactive accounts. Deactivation takes effect on the next protected API request and subsequent push through database account/recipient checks (0105); phone/password changes revoke refresh tokens and can take up to the access token's 5-minute lifetime to reach every device.

**Logout** — `shared/auth/endSession.ts`: forget locally → `abandonSession()` (bumps `sessionEpoch` so an in-flight refresh cannot log you back in) → `POST /api/auth/logout`.

```mermaid
sequenceDiagram
    actor U as User
    participant P as LoginPage.tsx
    participant H as useLogin.ts
    participant S as authSlice (memory)
    participant B as AuthEndpoints.cs
    participant D as MySQL

    U->>P: type employee id and phone, press เข้าสู่ระบบ
    P->>H: submit({ employeeId: trim(employeeId), phone })
    H->>B: POST /api/auth/login
    B->>B: throttle.TryBeginAttempt — 429 if locked
    B->>D: lookup EmployeeId AND Role != Admin
    B->>B: Pbkdf2PasswordHasher.Verify(secret, SecretHash or dummy hash)
    B->>D: insert RefreshToken row (SHA-256 hash only)
    B-->>H: 200 { accessToken, expiresAt, user } + Set-Cookie facility_refresh
    H->>S: dispatch(setCredentials)
    S-->>P: currentUser set, redirect to homeOf(role) or state.from
    P->>P: Navigate to /my-work or /my-building or /dashboard
```

### The failure table you will actually hit

| Failure | Handled where | User sees |
|---|---|---|
| Empty field | HTML5 `required` | native browser bubble, request never sent |
| Wrong employee id **or** wrong phone | `AuthEndpoints.cs` → 401 | `รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง` |
| Wrong admin credentials | same 401 path | `ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง` |
| Deactivated account | same 401 path, indistinguishable on purpose | same message as wrong credentials |
| 5 attempts for a name/IP pair or 20 IP failures in 15 min | `LoginThrottle` → 429 | `กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่` |
| Network / timeout during login | `getErrorMessage.ts` | `เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่` |
| Access token expired mid-session | `baseApi.ts` refresh + retry | nothing — silent retry |
| Refresh refused | `session.ts` → `sessionExpired()` | bounced to `/login` with `state.from` kept |
| Refresh offline / 5xx / timeout | `session.ts` → `'offline'` | nothing, session kept |
| Reused refresh token after 30 s grace | `AuthEndpoints.cs` revokes session | bounced to login |
| Deactivated while a refresh is in flight | `AuthEndpoints.cs` revokes **all** that user's sessions | bounced to login; every other device dies too |
| Role changed | re-read on refresh; guard `ProtectedRoute.tsx` | `ไม่มีสิทธิ์เปิดหน้านี้` with a link home |

**Server Log lines** (facility-0095–0099, 0103, 0104): `Api/Auth/SecurityLog.cs`, called from `AuthEndpoints.LoginAsync` (failed, unknown account, refused while locked) and `RefreshAsync` (reused token), plus the IP cap event. Login lines include normalized IP. Category `JanitorialManagementDashboard.Security`, level Warning; the phone sees the same 401/429 as before. SQL statements stay out because `appsettings.json` sets `Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command` to `Warning`; Console is the only sink.

---

## Part 8 — Scan, GPS and real-time flow

### What "scan" means here

**There is no camera code in this repo.** The app never decodes a QR itself. What happens is: the sign carries a printed URL, the phone's own camera or OS scanner opens it, the browser lands on `/scan/<qrToken>` and React takes over.

```ts
// frontend/src/pages/print-signs/printRules.ts
export function signUrl(kind, token) {
  return `${window.location.origin}/${kind === 'check-in' ? 'check-in' : 'scan'}/${token}`;
}
```

`uqr` is in `package.json` only for **printing** signs (`pages/print-signs/qr.ts`).

The token lives on the `signs` row (`Sign.QrToken`) and appears in no list response — *"whoever holds a sign's QR Token may see that one point"* (facility-0059).

### The three screens behind one URL

| Route | File | Purpose |
|---|---|---|
| `/scan/:token` as cleaner | `pages/scan/ScanPage.tsx` | submit that the point was cleaned |
| `/scan/:token` as supervisor | `pages/inspect/InspectPage.tsx` | judge that submission — same URL, `ByRole.tsx` picks the page |
| `/admin/areas/:areaId/capture` | `pages/capture/CapturePage.tsx` | **setup, not scanning**: the admin stands at the sign and records the coordinates the geofence will later compare against |

Related: `/check-in/:token` (`pages/check-in/`) is the shift in/out sign — same GPS and blocked-scan components, different endpoint.

### GPS — fresh reading and permission recovery

```ts
{ enableHighAccuracy: true, timeout: 15_000, maximumAge: 0 }
```

`maximumAge: 0` means never a cached fix — the comment says *"a cached fix may be from before the person walked to the sign."*

It **never rejects**. Every branch resolves to `{ kind: 'fix', fix }` or `{ kind: 'denied' | 'unavailable' | 'insecure' }`:

| Condition | Returned |
|---|---|
| `window.isSecureContext === false` (plain http) | `insecure` — the API is never touched |
| `!('geolocation' in navigator)` | `unavailable` |
| error callback with `PERMISSION_DENIED` | `denied` |
| anything else, including the 15 s timeout | `unavailable` |

**No automatic submission retry or cached scan fix.** Retry is user-driven through `LocationHelp`. After a refusal, `refusalNeedsReload()` probes whether the page still refuses despite permission being enabled; if so it reloads (WebKit), otherwise it repeats the action. Check-in, scan and inspection use this help. Setup capture uses `pages/capture/captureLocation.ts` with watchPosition for 30 seconds.

### The geofence: flag, never block

`backend/src/JanitorialManagementDashboard.Application/Geo/Geofence.cs`:

```csharp
var distance = (int)Math.Round(DistanceMeters(...));   // haversine, R = 6_371_000 m
return new GeofenceResult(distance, distance <= radiusM && phone.AccuracyM <= radiusM);
```

Three things follow from that one line, and they surprise people:

1. **Both distance and accuracy must fit the radius.** A precise fix taken 400 m away is still flagged.
2. **Outside the radius does not stop the save.** Tests assert this explicitly: `ScanRecordEndpointTests.Far_scan_is_saved_and_flagged` and `InspectionScanEndpointTests.Far_from_the_sign_is_flagged_not_blocked` both expect **201 with `withinRadius: false`**. The admin sees it later as an alert (`AlertQuery.cs`).
3. **A sign that was never captured gives no verdict at all** — `Evaluate` returns `null` distance.

Default radius is 50 m (`Sign.cs`), editable per sign in 10–500 m (`pages/areas/signRules.ts`).

**`BlockedScanScreen.tsx` is not the geofence.** It renders *scope* blocks — wrong area, wrong building, outside your shift — returned as 403 by `Endpoints/BlockedScans.cs`, which also writes a `blocked_scans` row.

**The one exception to "flag, never block": GPS is required.** `GpsInput.TryRead` returns 400 if coordinates are missing — *"facility-0036: without a location the scan is refused."*

### The submission, step by step

```mermaid
sequenceDiagram
    autonumber
    actor C as Cleaner (phone)
    participant Cam as Phone OS camera
    participant W as ScanPage.tsx
    participant G as navigator.geolocation
    participant A as ScanRecordEndpoints.cs
    participant D as MySQL
    actor Dash as Admin Dashboard
    actor S as Supervisor My Building

    Note over Cam,W: One-time setup — admin records the sign's coordinates
    Cam->>W: camera decodes QR, browser opens /scan/qrToken
    W->>A: GET /api/service-points/by-token/qrToken
    A-->>W: PointCard (one point only, never the map)
    C->>W: press ส่งงาน
    W->>G: getCurrentPosition {highAccuracy, timeout 15000, maximumAge 0}
    G-->>W: {latitude, longitude, accuracyM}
    W->>A: POST /api/scan-records {qrToken, status, issueTags?, notes?, lat, lng, accuracyM}
    A->>A: role is Cleaner, GPS present, sign active, right area or cover, on duty
    A->>D: RoundPlacer.Place → OnTime, Late, Rework or OffRound
    A->>D: ScanRecord + StampGps (haversine vs sign radius)
    A-->>W: 201 {placement, distanceM, withinRadius, newPointStatus}
    W-->>C: SubmitResult — amber ไกลป้าย note if outside the radius
    A--)Dash: SignalR ScanRecorded (PointCard)
    A--)S: SignalR ScanRecorded (PointCard)
    Dash->>Dash: updateCachedData(replacePoint) → tiles re-render
```

**Order of validation inside `CreateAsync`** (`ScanRecordEndpoints.cs`) — knowing this order tells you *which* error a user is looking at:

| # | Check | Failure |
|---|---|---|
| 1 | Authenticated and `Role == Cleaner` | 401 / 403 *"การส่งงานใช้สำหรับแม่บ้านเท่านั้น"* |
| 2 | Tags ≤ 200 chars, note ≤ 1000, status is a valid enum | 400 |
| 3 | **GPS present and coordinate values valid** | 400 *"ต้องเปิดตำแหน่งและอนุญาตให้เว็บอ่านตำแหน่งก่อนสแกน"* |
| 4 | Sign exists, point and area active | 404 *"ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว"* |
| 5 | Same area **or** a live cover for this shift | 403 + a `blocked_scans` row |
| 6 | On duty (in shift, not already signed out) | 409 *"ตอนนี้ไม่อยู่ในกะของคุณ"* etc. |
| 7 | Round placement — **not** a duplicate check | never fails |
| 8 | Write `ScanRecord`, stamp GPS, broadcast `ScanRecorded` | — |

**There is no "already scanned" rejection.** `RoundPlacer.cs` places submissions from the current time and round facts: `OnTime`, `Late`, `Rework` or `OffRound` and the result screen says so (*"ส่งเพิ่มแล้ว (ไม่นับรอบ)"*).

### How the dashboard updates without a refresh

```mermaid
graph LR
    A["Endpoint saves the record"] --> B["PointNotifications.SendAsync<br/>backend Endpoints/PointNotifications.cs"]
    B --> C["users: active Admin ids read at send time"]
    B --> D["users: supervisors of that building<br/>plus covers this shift"]
    C --> E["shared/api/scanHub.ts<br/>onCacheEntryAdded"]
    D --> E
    B --> J["users: active Area Cleaners and covers"]
    J --> K["myWorkApi.ts refetches MyWork"]
    E --> F["updateCachedData<br/>replacePoint or patchPoint"]
    F --> G["PointTile re-renders<br/>LiveBadge stays empty"]
    H["SignalR push fails"] --> I["logged and swallowed<br/>30 s livePoll + onMissed refetch recovers"]
```

- **Hub:** `Hubs/ScanHub.cs` at `/hubs/scan`. Zero methods — strictly server → client. Recipients are read from the database at send time, including Admins (0105); sockets do not join an Admin group. Since 2026-10-26 the **Cleaners of the point's Area (both shifts) and anyone covering it** get the point events too; their My Work page asks the server again when one arrives, on open and on "อัปเดต".
- **Three events:** `ScanRecorded`, `InspectionRecorded` (both → admins + the building's supervisors + the Area's Cleaners and covers), `AttendanceRecorded` (→ admins only).
- **Auth on the socket:** the access token goes in the query string, accepted **only** for `/hubs` paths (facility-0011) — browsers cannot set headers on a WebSocket.
- **Four subscribers:** `pages/dashboard/dashboardApi.ts`, `pages/my-building/myBuildingApi.ts`, `shared/api/attendanceBoardApi.ts` — one connection per cache entry.
- **The safety net:** `livePoll` = 30 s poll + refetch on focus. Pushes are best effort; a failure is logged and swallowed because the record is already committed.
- **Connection timing (0107):** browser offline sets reconnecting immediately, online refetches after 1 s; Dashboard/attendance wait 10 s for server silence, phones 30 s; server ping 5 s.
- **Badge:** `shared/components/LiveBadge.tsx` reads `hubSlice` — orange `กำลังเชื่อมต่อ… (อัปเดตทุก 30 วินาที)` only while `reconnecting` (browser offline, server timeout, reconnecting or a first start failed); nothing while live or during the first connect (owner, 2026-10-28).

### Scan failure table

| Failure | Handled where | User sees |
|---|---|---|
| GPS permission denied | `readLocation.ts` → `LocationHelp.tsx` | full-screen *"เปิดตำแหน่งก่อนส่งงาน"* with Android/iPhone steps + retry |
| GPS unavailable / 15 s timeout | `LocationHelp.tsx` | *"มือถือยังหาตำแหน่งไม่เจอ ลองเดินไปใกล้หน้าต่าง"* + retry |
| Page on plain http | `readLocation.ts` → `LocationHelp.tsx` | *"หน้านี้ต้องเปิดผ่าน https"* — **no retry button** |
| No signal / 5xx / 20 s timeout | `scanAnswers.ts isNotSent` → `SendFailed.tsx` | *"ยังไม่ได้บันทึก"*, choices kept in memory, ส่งใหม่ re-reads GPS fresh |
| Outside the geofence | server → `withinRadius: false` | **still saved**; amber *"ไกลป้าย: ห่างป้าย N ม."*, later an admin alert |
| Wrong area / building / shift, no cover | server 403 → `BlockedScanScreen.tsx` | big message + *"ไม่ได้บันทึกงาน"*, nothing stored except the blocked row |
| Off duty | server 409, pre-checked client-side by `scanRules.ts` | `OffDuty.tsx` shown before the form |
| Sign unknown / point deactivated | server 404 | `StatusScreen` *"ไม่พบป้ายนี้ หรือจุดนี้ปิดใช้งานแล้ว"* |
| Work changed while the supervisor looked away | `InspectionEndpoints.cs` → 409 | *"งานของจุดนี้เปลี่ยนไประหว่างที่คุณตรวจ"* — the verdict is discarded, page re-scans |
| Nothing to inspect | `InspectionRules.cs` | `NothingToInspect.tsx`, refusal shown as the heading, **no button** |
| Push lost | `scanHub.ts` reconnect + `onMissed` refetch | badge turns orange, data recovers |

---

## Part 9 — API reference

53 HTTP routes. `frontend/src/shared/api/baseApi.ts` adds the `/api` prefix, so the frontend `url` values below are relative to it.

**How to read the Why column:** it answers *"what business goal does this call serve, what breaks without it?"* — not "what does it fetch". If you are deciding whether an endpoint is essential while changing something, the Why is the row that matters.

### Auth

| Method | Route | Handler | Why |
|---|---|---|---|
| POST | `/api/auth/login` | `AuthEndpoints.cs` → `LoginAsync` | Three roles authenticate differently — staff use employee id + phone, admins use username + password. Without it, GPS-stamped scans would be anonymous. The throttle and dummy hash exist so an attacker cannot tell "no such account" from "wrong phone". |
| POST | `/api/auth/refresh` | `AuthEndpoints.cs` → `RefreshAsync` | Access tokens last 5 minutes (privacy on a shared phone), so the browser needs a way to stay logged in. Refresh refuses inactive accounts and revoked sessions. Protected API and push-recipient checks apply deactivation immediately (0105). |
| POST | `/api/auth/logout` | `AuthEndpoints.cs` → `LogoutAsync` | Kills the session server-side; otherwise a left-behind cookie keeps minting tokens for 30 days. |
| GET | `/api/me` | `MeEndpoints.cs` | Fresh self state. **No frontend caller found** — see [Part 12](#part-12--limits-and-remaining-work). |
| GET | `/api/health` | `Program.cs` | Liveness for curl and tunnel probes. No frontend caller. |

### Cleaner

| Method | Route | Handler | Called by | Why |
|---|---|---|---|---|
| GET | `/api/my-work` | `MyWorkEndpoints.cs` | `shared/api/myWorkApi.ts` | The cleaner's own beat plus covered areas. Without it the phone page has nothing to render. |
| GET | `/api/attendance/signs/{token}` | `AttendanceEndpoints.cs` | `pages/check-in/checkInApi.ts` | Fails fast on page open when a sign was deactivated, instead of the cleaner discovering it at the end of a shift. |
| POST | `/api/attendance` | `AttendanceEndpoints.cs` → `RecordAsync` | `pages/check-in/checkInApi.ts` | Records presence in an assigned Area and gates work submission. This is not payroll/timekeeping and does not calculate late/early attendance (0050); GPS gives a flag for Admin review. |
| GET | `/api/attendance/me` | `AttendanceEndpoints.cs` | **none found** | Current shift and next allowed event. |
| GET | `/api/service-points/by-token/{token}` | `ServicePointEndpoints.cs` | `pages/scan/scanApi.ts` | Resolves a scanned sign to exactly one point, so a photo of a QR cannot leak the facility map. |
| POST | `/api/scan-records` | `ScanRecordEndpoints.cs` → `CreateAsync` | `pages/scan/scanApi.ts` | **The core work product**: "this point was cleaned, at this time, by this person." Round placement, point status, inspections, exports and alerts are all derived from these rows. |

### Supervisor

| Method | Route | Handler | Called by | Why |
|---|---|---|---|---|
| POST | `/api/inspections/scan` | `InspectionEndpoints.cs` → `ScanAsync` | `pages/inspect/inspectApi.ts` | Stores nothing — it shows the supervisor the current submission (or a refusal reason). The two-step design exists so a supervisor can only judge what they were actually shown. |
| POST | `/api/inspections` | `InspectionEndpoints.cs` → `CreateAsync` | `pages/inspect/inspectApi.ts` | Records a voluntary inspection or rework request; an uninspected round remains visible as not inspected. `Rework` sends the cleaner back with the defect text. |
| GET | `/api/my-building` | `MyBuildingEndpoints.cs` | `pages/my-building/myBuildingApi.ts` | The supervisor's walking queue, pending-inspection first, then rework. |

### Admin setup

| Method | Route | Handler | Called by | Why |
|---|---|---|---|---|
| GET | `/api/admin/buildings` | `AdminAreaEndpoints.cs` | `shared/api/adminSetupApi.ts` | Picker options used across many pages. |
| GET | `/api/admin/buildings/all` | `AdminBuildingEndpoints.cs` | `adminSetupApi.ts` (`getAllBuildings`) | Every building, off ones too, with active Area and Supervisor counts: the buildings page and the D8 picker (D7 lists the active ones only). |
| POST | `/api/admin/buildings` | `AdminBuildingEndpoints.cs` → `CreateAsync` | `pages/buildings/buildingsApi.ts` | Code set once, never reused (facility-0090). |
| PUT | `/api/admin/buildings/{id}` | `AdminBuildingEndpoints.cs` → `RenameAsync` | `buildingsApi.ts` | Name only. |
| POST | `/api/admin/buildings/{id}/deactivate` | `AdminBuildingEndpoints.cs` → `SetActiveAsync` | `buildingsApi.ts` | Refused while an active Area or Supervisor is left in it. |
| POST | `/api/admin/buildings/{id}/activate` | `AdminBuildingEndpoints.cs` → `SetActiveAsync` | `buildingsApi.ts` | No condition; its Areas stay as they are. |
| GET | `/api/admin/cleaners` | `AdminAreaEndpoints.cs` | `adminSetupApi.ts` | Same. |
| GET | `/api/admin/areas` | `AdminAreaEndpoints.cs` | `adminSetupApi.ts` | Area summaries with counts. |
| GET | `/api/admin/areas/{id}` | `AdminAreaEndpoints.cs` | `adminSetupApi.ts` | Area detail: points, windows, signs. |
| POST | `/api/admin/areas` | `AdminAreaEndpoints.cs` → `CreateAsync` | `pages/areas/areaFormApi.ts` | Creates the area **and** its check-in sign in one transaction. |
| PUT | `/api/admin/areas/{id}` | `AdminAreaEndpoints.cs` → `UpdateAsync` | `areaFormApi.ts` | Renaming or moving an area drops night windows for a DayOnly pattern. |
| POST | `/api/admin/areas/{id}/deactivate` | `AdminAreaEndpoints.cs` → `SetActiveAsync` | `areaFormApi.ts` | Deactivate, never delete — history must keep the name. Refused while an active Cleaner holds the Area (facility-0091). |
| POST | `/api/admin/areas/{id}/activate` | `AdminAreaEndpoints.cs` | `areaFormApi.ts` | Re-open. |
| POST | `/api/admin/areas/{areaId}/points` | `AdminPointEndpoints.cs` → `CreateAsync` | `pages/areas/areasApi.ts` | A point is meaningless without its sign and round windows. |
| PUT | `/api/admin/points/{id}` | `AdminPointEndpoints.cs` → `UpdateAsync` | `areasApi.ts` | Unchanged windows keep their ids so a live round keeps its records. |
| POST | `/api/admin/points/{id}/deactivate` | `AdminPointEndpoints.cs` → `SetActiveAsync` | `areasApi.ts` | Leaves the dashboard, cannot be scanned, history kept. |
| POST | `/api/admin/points/{id}/activate` | `AdminPointEndpoints.cs` | `areasApi.ts` | Re-open. |
| PUT | `/api/admin/signs/{id}/location` | `AdminSignEndpoints.cs` → `SetLocationAsync` | `adminSetupApi.ts` | Sets the coordinates the geofence compares against. Site capture beats map pick. |
| PUT | `/api/admin/signs/{id}/radius` | `AdminSignEndpoints.cs` → `SetRadiusAsync` | `areasApi.ts` | A wrong radius makes every scan "off-site". |
| POST | `/api/admin/signs/{id}/regenerate-token` | `AdminSignEndpoints.cs` → `RegenerateTokenAsync` | `areasApi.ts` | Kills the printed QR at once — the only way to de-authorise a lost sign. |

### Admin accounts

| Method | Route | Handler | Why |
|---|---|---|---|
| GET | `/api/admin/accounts` | `AdminAccountEndpoints.cs` | All accounts including deactivated (~180 rows, filtered client-side). |
| POST | `/api/admin/accounts` | `AdminAccountEndpoints.cs` → `CreateAsync` | Provisioning. Staff secrets are the phone number, hashed as `SecretHash`. |
| PUT | `/api/admin/accounts/{id}` | `AdminAccountEndpoints.cs` → `UpdateAsync` | Slot-uniqueness: one cleaner per area-shift, one supervisor per building-shift. |
| POST | `/api/admin/accounts/{id}/deactivate` | `AdminAccountEndpoints.cs` → `SetActiveAsync` | **The only off-boarding mechanism** — revokes every refresh token and frees the area slot. No delete exists so history keeps names. |
| POST | `/api/admin/accounts/{id}/activate` | `AdminAccountEndpoints.cs` | Re-open only if the slot is free. |
| PUT | `/api/admin/accounts/{id}/phone` | `AdminAccountEndpoints.cs` → `ChangePhoneAsync` | Changing the secret forces every device to log in again. |
| PUT | `/api/admin/accounts/{id}/password` | `AdminAccountEndpoints.cs` → `ResetPasswordAsync` | Temporary admin password, told out of band, sessions revoked. |

### Admin operations

| Method | Route | Handler | Why |
|---|---|---|---|
| GET | `/api/service-points` | `ServicePointEndpoints.cs` | Dashboard point cards for active Admins, without QR tokens; role and active-account checks are enforced server-side (0058, 0060, 0105). |
| GET | `/api/admin/attendance` | `AdminAttendanceEndpoints.cs` → `GetBoardAsync` | Board D2: who is in each area this shift. |
| PUT | `/api/admin/attendance` | `AdminAttendanceEndpoints.cs` → `CorrectAsync` | Corrections require a reason and are audited — the Admin must be able to see what changed and why; this is Area coverage, not payroll (0050). |
| GET | `/api/admin/alerts` | `AdminAlertEndpoints.cs` → `ListAsync` | The exception queue: blocked scans, GPS flags, missing shift-out. |
| POST | `/api/admin/alerts/review` | `AdminAlertEndpoints.cs` → `ReviewAsync` | "คุยแล้ว ปิดเตือน" — one review per alert, audited, so "we talked about it" is a record not folklore. |
| POST | `/api/admin/cover-assignments` | `CoverAssignmentEndpoints.cs` → `AssignAsync` | The explicit authorization to work another Area (Cleaner, own regular shift) or another building/shift (Supervisor); without it every cross-area scan becomes a blocked scan. |
| DELETE | `/api/admin/cover-assignments/{id}` | `CoverAssignmentEndpoints.cs` → `CancelAsync` | Cancel closes duplicate twins too. |
| GET | `/api/admin/cover-assignments/candidates` | `CoverAssignmentEndpoints.cs` → `ListCandidatesAsync` | Ranked: those who already scanned this area first. |

### Admin reports

| Method | Route | Handler | Why |
|---|---|---|---|
| GET | `/api/admin/audit-log` | `AdminAuditLogEndpoints.cs` → `ListAsync` | Every admin mutation is attributable. Read-only, because rewriting it would defeat the point. Max 92 days, 500 rows. `q` searches summary, actor name and reason over every row in the range (LIKE, `!` escape), not only the rows loaded. |
| GET | `/api/admin/audit-log/actors` | `AdminAuditLogEndpoints.cs` | Actor filter options. |
| POST | `/api/admin/export` | `AdminExportEndpoints.cs` → `ExportAsync` | **POST not GET** precisely because downloading personnel data is itself an audited action. The file omits coordinates, phones, QR tokens and passwords. It accepts `buildingId` or multiple `buildingIds` (never both), including inactive buildings (0100). |
| GET | `/api/admin/inspection-summary` | `AdminInspectionSummaryEndpoints.cs` → `GetAsync` | How much of the beat was actually checked — supervisor coverage evidence, including expandable uninspected points (0100). |

### Non-HTTP routes

| Route | Where | Why |
|---|---|---|
| `/hubs/scan` | `Program.cs` | The SignalR hub. |
| any non-`/api`, non-`/hubs`, extension-less path | `Program.cs` `MapFallbackToFile` | Serves `index.html` so a printed QR link opens the SPA instead of a 404. |

### The events, all of them

| Event | Active recipients at send time | Frontend handling |
|---|---|---|
| `ScanRecorded`, `InspectionRecorded` (whole PointCard) | Admins, the building's Supervisors, the Area's Cleaners, current scoped covers | Dashboard/My Building patch the point; My Work refetches; attendance follower ignores point messages |
| `AttendanceRecorded` (Area/date/shift) | Admins | Attendance refetches the affected board; other followers ignore it |

`PointNotifications.SendAsync` and `AttendanceBoard.NotifyAsync` send after the record is saved and catch/log push failures. `ActiveAdmins.IdsAsync` reads Admin recipients; no group membership cache decides access.

---

## Part 10 — Data model

```mermaid
erDiagram
    SERVICE_POINT ||--o{ POINT_MONTHLY_SUMMARY : anonymous_archive
    BUILDING ||--o{ AREA : contains
    AREA ||--o{ SERVICE_POINT : contains
    SERVICE_POINT |o--o| SIGN : has
    AREA ||--o{ SIGN : has
    SERVICE_POINT ||--o{ POINT_ROUND_WINDOW : has
    AREA ||--o{ SHIFT_ATTENDANCE : records
    USER ||--o{ SHIFT_ATTENDANCE : makes
    USER ||--o{ SCAN_RECORD : submits
    SERVICE_POINT ||--o{ SCAN_RECORD : receives
    USER ||--o{ INSPECTION_RECORD : judges
    SCAN_RECORD ||--o{ INSPECTION_RECORD : judged_by
    USER ||--o{ REFRESH_TOKEN : owns
    USER ||--o{ COVER_ASSIGNMENT : receives
    AREA |o--o{ COVER_ASSIGNMENT : receives
    USER |o--o{ AUDIT_ENTRY : performs
    SIGN ||--o{ BLOCKED_SCAN : records
    USER ||--o{ BLOCKED_SCAN : makes
    BUILDING |o--o{ COVER_ASSIGNMENT : receives
```

15 tables, defined in `Infrastructure/Persistence/AppDbContext.cs`, snake_case. Accounts, buildings, Areas and points are deactivated through Admin actions. Automatic retention (0055) clears raw GPS, deletes expired records and stores anonymous point/month summaries transactionally; it is distinct from master-data deactivation. See [the retention guide](data-retention.md).

**Invariants the schema itself enforces** (you cannot code around them):

| Invariant | Where |
|---|---|
| One attendance event per user / shift / type — first press wins | unique index, `AppDbContext.cs` |
| One cleaner per area-shift, one supervisor per building-shift | stored computed unique columns, `AppDbContext.cs` |
| A cover is an area **or** a building, never both | check constraint, `AppDbContext.cs` |
| Every stored `DATETIME` is UTC; shifts and round windows are Thai wall clock | `Application/Common/ThaiTime.cs` |

**Enums worth memorising** (`Domain/Enums/`): `UserRole` (cleaner | supervisor | admin), `Shift`, `AttendanceEvent`, `CleaningStatus`, `InspectionResult` (Passed | Rework), `Placement` (OnTime | Late | Rework | OffRound), `BlockReason`, `AlertType`.

---

## Part 11 — Tests that pin behaviour

Backend paths below are relative to `backend/tests/`; frontend paths are relative to `frontend/src/`.

| Behaviour | Tests |
|---|---|
| Login forms, pair/IP throttle and forwarded address trust | `JanitorialManagementDashboard.ApiTests/Auth/LoginTests.cs`, `JanitorialManagementDashboard.UnitTests/LoginThrottleTests.cs`, `JanitorialManagementDashboard.UnitTests/LoginPlaceTests.cs` |
| Refresh rotation, reuse grace and revocation | `JanitorialManagementDashboard.ApiTests/Auth/RefreshTests.cs` |
| Deactivation blocks reads and live updates | `JanitorialManagementDashboard.ApiTests/Admin/DeactivatedAdminTests.cs`, `JanitorialManagementDashboard.ApiTests/Admin/DeactivatedAdminLiveTests.cs` |
| CSP, HSTS, no-store, Swagger exemption and ping interval | `JanitorialManagementDashboard.ApiTests/SecurityHeaderTests.cs`, `JanitorialManagementDashboard.ApiTests/HubKeepAliveTests.cs` |
| Scan placement, scope blocks and far-GPS flags | `JanitorialManagementDashboard.ApiTests/Points/ScanRecordEndpointTests.cs`, `JanitorialManagementDashboard.ApiTests/Inspections/InspectionScanEndpointTests.cs`, `JanitorialManagementDashboard.UnitTests/GeofenceTests.cs` |
| Scoped live updates and saved-record behavior when push fails | `JanitorialManagementDashboard.ApiTests/Inspections/SupervisorLiveTests.cs`, `CleanerLiveTests.cs`, `LiveUpdateFailureTests.cs` (same directory) |
| Redacted security log and real Serilog rendering | `JanitorialManagementDashboard.ApiTests/Auth/SecurityLogTests.cs`, `JanitorialManagementDashboard.ApiTests/ServerLogTests.cs` |
| Session and one refresh/retry | `shared/auth/session.test.ts`, `shared/api/baseApi.test.ts` |
| Browser offline/online, reconnect and timeout | `shared/api/scanHub.test.ts`, `shared/api/scanHub.noSignalR.test.ts` |
| Fresh GPS and permission recovery | `shared/geo/readLocation.test.ts`, `pages/check-in/CheckInPage.test.tsx` |
| Submission/inspection screens, live Dashboard and date/shift race | `pages/scan/ScanPage.test.tsx`, `pages/inspect/InspectPage.test.tsx`, `pages/dashboard/DashboardPage.test.tsx`, `pages/attendance/AttendancePage.test.tsx` |
| Route roles and lazy boundaries | `router.test.tsx` |

Retention tests are in `JanitorialManagementDashboard.ApiTests/Retention/` (real relational deletion/rollback, race cases, schedule/lifecycle and MySQL migration/provider) and `JanitorialManagementDashboard.UnitTests/RetentionRulesTests.cs` (settings/calendar/anonymous counts). See [the retention guide](data-retention.md).

The frontend harness is `src/test/`: fake fetch, SignalR, GPS and `renderApp`. Real-API workflows and runner safety checks are described in [testing/e2e.md](testing/e2e.md). Current recorded counts belong in [handoff](handoff.md), not in this file map.

---

## Part 12 — Limits and remaining work

- `GET /api/me`, `/api/attendance/me` and `/api/health` have no frontend data callers; health is used by operating/test probes. Endpoint existence does not imply a user-facing page.
- QR decoding is delegated to the phone's native camera (0007). The app renders QR codes for printing; it has no in-app camera scanner.
- LoginThrottle is in memory; restarting clears it. This is a single-server design.
- No offline submission queue, payroll calculations, automatic Supervisor score or external notification service is implemented; see the business overview for scope.
- Deployment/field checks, retention host verification and the shelved import are tracked in [handoff](handoff.md). Browser-engine E2E does not confirm real iPhone behavior, GPS accuracy or physical printing.

---

## Related documents

| Document | Answers |
|---|---|
| [`docs/architecture.md`](architecture.md) | How the parts connect: deployment, ports, network, component diagram, MySQL schema |
| [`docs/adr/`](adr/) | **Why** the code is shaped this way — the ADRs are the spec |
| [`../CONTEXT.md`](../CONTEXT.md) | What every word in the product means (glossary) |
| [`handoff.md`](handoff.md) | Current branch, what is done, what the owner still owes a check on |
| `design-system/janitorial/MASTER.md` | How it looks and why |
