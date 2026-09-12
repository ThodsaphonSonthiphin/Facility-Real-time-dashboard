# Phase 1: Walking Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify an End-to-End Walking Skeleton where a cleaner scans a Service Point QR URL on mobile over local WiFi, submits a scan record, saves to MySQL, and triggers an instant real-time SignalR card update on the Mac dashboard.

**Architecture:** .NET 10 Minimal APIs backend organized in 4 Clean Architecture projects (`Domain`, `Application`, `Infrastructure`, `Api`) with `MySql.EntityFrameworkCore 10.x` and SignalR Hub, alongside a React (Vite + TypeScript) frontend using Vanilla CSS and strict hook/component separation per `react-structure`.

**Tech Stack:** 
- Backend: .NET 10 LTS (C# 13), ASP.NET Core Minimal APIs, SignalR, `MySql.EntityFrameworkCore 10.x`, xUnit
- Database: MySQL 8+ running natively via Homebrew on macOS M1
- Frontend: Vite 6, React 19, TypeScript, Vanilla CSS, `@microsoft/signalr`, `lucide-react`
- Network: Local LAN WiFi (HTTP on Mac LAN IP: Port 5000 API / 5173 Vite)

**Spec:**
- [ADR 0001: Scan Record Structure](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0001-scan-record-structure.md)
- [ADR 0002: Scanner Authentication](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0002-scanner-authentication.md)
- [ADR 0003: Local Network WiFi Access](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0003-phone-reaches-mac-over-same-wifi-http.md)
- [ADR 0004: App Architecture](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0004-app-architecture.md)
- [ADR 0005: Point Status Rules](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0005-point-status-rules.md)
- [ADR 0006: MySQL Schema & QR Token](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0006-mysql-schema-and-qr-token.md)
- [ADR 0007: Scan Flow Native Camera](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0007-scan-flow-native-camera.md)
- [ADR 0008: Dashboard Cards Grid Layout](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0008-dashboard-cards-grid-layout.md)
- [ADR 0009: Delivery Plan Walking Skeleton](file:///Users/macbookair/Desktop/Facility-Real-time-dashboard/docs/adr/facility-0009-delivery-plan-walking-skeleton.md)

## Global Constraints

- Native Execution: Run .NET and MySQL natively on macOS arm64 to preserve RAM (8GB constraint, avoid heavy Docker VM).
- React Structure: Strictly adhere to `react-structure` — all React components MUST separate custom hooks (`hooks/use*.ts`) from presentation components (`components/*.tsx`).
- Design System: Vanilla CSS only (no Tailwind), dark theme (`#0f172a` slate background), vibrant status indicators (Green `#22c55e`, Orange `#f97316`, Red `#ef4444`, Gray `#94a3b8`), responsive for mobile and desktop.
- Security & Public Repo: No plaintext secrets or corporate passwords committed; use `appsettings.Development.json` for local configuration.
- Traceability: All git commits must link back to relevant issue (#13 or Phase 1 tasks).

---

### Task 1: Environment & Tooling Setup (Native Mac M1)

**Files:**
- Create: `backend/appsettings.Development.json`
- Create: `.env.example`

**Interfaces:**
- Consumes: macOS Homebrew
- Produces: Installed .NET 10 SDK, running MySQL database `facility_dashboard`

- [ ] **Step 1: Install .NET 10 SDK via Homebrew**

```bash
brew install --cask dotnet-sdk
```

- [ ] **Step 2: Install and start native MySQL via Homebrew**

```bash
brew install mysql
brew services start mysql
```

- [ ] **Step 3: Create database `facility_dashboard` and verify access**

```bash
mysql -u root -e "CREATE DATABASE IF NOT EXISTS facility_dashboard CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -u root -e "SHOW DATABASES LIKE 'facility_dashboard';"
```
Expected: `facility_dashboard` listed in output.

- [ ] **Step 4: Verify .NET and MySQL versions**

```bash
dotnet --version
mysql --version
```
Expected: .NET version `10.0.x` and MySQL version `8.x`/`9.x` printed successfully.

- [ ] **Step 5: Commit environment baseline documentation**

```bash
git add .env.example
git commit -m "chore(env): document local dev prerequisites and database setup (#13)"
```

---

### Task 2: Backend Solution & Domain Layer

**Files:**
- Create: `backend/FacilityRealtime.sln`
- Create: `backend/src/FacilityRealtime.Domain/Entities/ServicePoint.cs`
- Create: `backend/src/FacilityRealtime.Domain/Entities/User.cs`
- Create: `backend/src/FacilityRealtime.Domain/Entities/ScanRecord.cs`
- Create: `backend/src/FacilityRealtime.Domain/Enums/PointStatus.cs`
- Create: `backend/src/FacilityRealtime.Domain/Enums/ScanStatus.cs`
- Create: `backend/src/FacilityRealtime.Application/Common/StatusCalculator.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/StatusCalculatorTests.cs`

**Interfaces:**
- Consumes: None (pure Domain & Application logic)
- Produces: `StatusCalculator.CalculateStatus(lastScan, point, currentTime)` returning `PointStatus`

- [ ] **Step 1: Scaffold backend solution and project structure**

```bash
mkdir -p backend/src backend/tests
cd backend
dotnet new sln -n FacilityRealtime
dotnet new classlib -n FacilityRealtime.Domain -o src/FacilityRealtime.Domain
dotnet new classlib -n FacilityRealtime.Application -o src/FacilityRealtime.Application
dotnet new classlib -n FacilityRealtime.Infrastructure -o src/FacilityRealtime.Infrastructure
dotnet new web -n FacilityRealtime.Api -o src/FacilityRealtime.Api
dotnet new xunit -n FacilityRealtime.UnitTests -o tests/FacilityRealtime.UnitTests

dotnet sln add src/FacilityRealtime.Domain/FacilityRealtime.Domain.csproj
dotnet sln add src/FacilityRealtime.Application/FacilityRealtime.Application.csproj
dotnet sln add src/FacilityRealtime.Infrastructure/FacilityRealtime.Infrastructure.csproj
dotnet sln add src/FacilityRealtime.Api/FacilityRealtime.Api.csproj
dotnet sln add tests/FacilityRealtime.UnitTests/FacilityRealtime.UnitTests.csproj

dotnet add src/FacilityRealtime.Application reference src/FacilityRealtime.Domain
dotnet add src/FacilityRealtime.Infrastructure reference src/FacilityRealtime.Domain
dotnet add src/FacilityRealtime.Infrastructure reference src/FacilityRealtime.Application
dotnet add src/FacilityRealtime.Api reference src/FacilityRealtime.Application
dotnet add src/FacilityRealtime.Api reference src/FacilityRealtime.Infrastructure
dotnet add tests/FacilityRealtime.UnitTests reference src/FacilityRealtime.Application
cd ..
```

- [ ] **Step 2: Write failing unit test for Status Calculator**

Write in `backend/tests/FacilityRealtime.UnitTests/StatusCalculatorTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class StatusCalculatorTests
{
    [Fact]
    public void CalculateStatus_WhenLatestScanIsIssue_ReturnsIssue()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        var lastScan = new ScanRecord { ServicePointId = 1, Status = ScanStatus.Issue, ScannedAt = DateTime.UtcNow.AddMinutes(-10) };
        
        var status = StatusCalculator.CalculateStatus(lastScan, point, DateTime.UtcNow);
        
        Assert.Equal(PointStatus.Issue, status);
    }

    [Fact]
    public void CalculateStatus_WhenElapsedExceedsInterval_ReturnsOverdue()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        var lastScan = new ScanRecord { ServicePointId = 1, Status = ScanStatus.Normal, ScannedAt = DateTime.UtcNow.AddMinutes(-70) };
        
        var status = StatusCalculator.CalculateStatus(lastScan, point, DateTime.UtcNow);
        
        Assert.Equal(PointStatus.Overdue, status);
    }

    [Fact]
    public void CalculateStatus_WhenCleanedWithinInterval_ReturnsNormal()
    {
        var point = new ServicePoint { Id = 1, Name = "Point A", CleaningIntervalMinutes = 60, IsActive = true };
        var lastScan = new ScanRecord { ServicePointId = 1, Status = ScanStatus.Normal, ScannedAt = DateTime.UtcNow.AddMinutes(-30) };
        
        var status = StatusCalculator.CalculateStatus(lastScan, point, DateTime.UtcNow);
        
        Assert.Equal(PointStatus.Normal, status);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```bash
dotnet test backend/tests/FacilityRealtime.UnitTests
```
Expected: FAIL with compilation error (Types not yet created).

- [ ] **Step 4: Implement Domain Entities and Status Calculator**

Implement:
- `backend/src/FacilityRealtime.Domain/Enums/PointStatus.cs`: `Normal`, `Overdue`, `Issue`, `OffHours`
- `backend/src/FacilityRealtime.Domain/Enums/ScanStatus.cs`: `Normal`, `Issue`
- `backend/src/FacilityRealtime.Domain/Entities/ServicePoint.cs`: `Id`, `Name`, `Location`, `CleaningIntervalMinutes`, `QrToken`, `IsActive`, `CreatedAt`
- `backend/src/FacilityRealtime.Domain/Entities/User.cs`: `Id`, `Username`, `PasswordHash`, `FullName`, `Role`, `CreatedAt`
- `backend/src/FacilityRealtime.Domain/Entities/ScanRecord.cs`: `Id`, `ServicePointId`, `UserId`, `Status`, `IssueTags`, `Notes`, `ScannedAt`
- `backend/src/FacilityRealtime.Application/Common/StatusCalculator.cs`: Logic calculating Priority (Issue > OffHours > Overdue > Normal).

- [ ] **Step 5: Run tests and verify PASS**

```bash
dotnet test backend/tests/FacilityRealtime.UnitTests
```
Expected: All tests PASS.

- [ ] **Step 6: Commit Domain & Application layer**

```bash
git add backend/
git commit -m "feat(backend): implement domain entities and status calculation logic (#13)"
```

---

### Task 3: Infrastructure, Database Migrations & Seeding

**Files:**
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs`

**Interfaces:**
- Consumes: `MySql.EntityFrameworkCore 10.x`
- Produces: Database schema and seeded records (3 service points + 1 cleaner)

- [ ] **Step 1: Install MySQL EF Core packages in Infrastructure & Api**

```bash
dotnet add backend/src/FacilityRealtime.Infrastructure package MySql.EntityFrameworkCore
dotnet add backend/src/FacilityRealtime.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add backend/src/FacilityRealtime.Api package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef || true
```

- [ ] **Step 2: Configure `AppDbContext` with indexes and mappings**

Configure composite index in `AppDbContext`:
`modelBuilder.Entity<ScanRecord>().HasIndex(r => new { r.ServicePointId, r.ScannedAt });`
`modelBuilder.Entity<ServicePoint>().HasIndex(p => p.QrToken).IsUnique();`

- [ ] **Step 3: Implement `DbInitializer.cs` to seed test data**

Seed initial records if empty:
- 3 Points:
  1. `ห้องน้ำชาย ชั้น 1` (interval: 60 mins, qr_token: `token-restroom-m1`)
  2. `ห้องน้ำหญิง ชั้น 1` (interval: 60 mins, qr_token: `token-restroom-f1`)
  3. `จุดแยกขยะ โซน B` (interval: 120 mins, qr_token: `token-waste-zone-b`)
- 1 Cleaner:
  - username: `somchai`, password: `password123`, name: `สมชาย ใจดี`

- [ ] **Step 4: Create EF Core migration and apply to MySQL**

```bash
dotnet ef migrations add InitialCreate --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api
dotnet ef database update --project backend/src/FacilityRealtime.Infrastructure --startup-project backend/src/FacilityRealtime.Api
```

- [ ] **Step 5: Verify MySQL tables and records**

```bash
mysql -u root -e "USE facility_dashboard; SHOW TABLES; SELECT id, name, qr_token FROM service_points;"
```
Expected: 3 tables (`service_points`, `users`, `scan_records`) and 3 seeded points.

- [ ] **Step 6: Commit Infrastructure & Migrations**

```bash
git add backend/
git commit -m "feat(database): configure ef core mysql provider and initial migrations (#13)"
```

---

### Task 4: Backend Minimal APIs & SignalR Hub

**Files:**
- Create: `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `StatusCalculator`
- Produces: 
  - SignalR Hub at `/hubs/scan` broadcasting `ScanRecorded`
  - REST Endpoints:
    - `POST /api/auth/login`
    - `GET /api/service-points`
    - `GET /api/service-points/by-token/{token}`
    - `POST /api/scan-records`

- [ ] **Step 1: Implement `ScanHub.cs`**

```csharp
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

public class ScanHub : Hub
{
    // Clients listen on "ScanRecorded" and "StatusUpdated"
}
```

- [ ] **Step 2: Implement Minimal API routes in `Program.cs`**

Wire up CORS, JSON options, SignalR, and Minimal API routes:
- `POST /api/auth/login`: verifies user credentials, returns simple token + user details
- `GET /api/service-points`: returns all points with calculated current status, last scanned time, and last cleaner name
- `GET /api/service-points/by-token/{token}`: returns point details for scanner display
- `POST /api/scan-records`: creates `ScanRecord`, recalculates point status, broadcasts via `IHubContext<ScanHub>.Clients.All.SendAsync("ScanRecorded", updatedPointDto)`

- [ ] **Step 3: Test API endpoints using curl**

Start API in background and test:
```bash
curl -X GET http://localhost:5000/api/service-points
```
Expected: JSON array containing the 3 seeded service points with calculated status.

- [ ] **Step 4: Commit API and SignalR Hub**

```bash
git add backend/
git commit -m "feat(api): implement minimal apis and signalr hub for live scan broadcast (#13)"
```

---

### Task 5: Frontend Scaffolding & Shared Services (Vite + React + TS)

**Files:**
- Create: `frontend/package.json`
- Create: `frontend/vite.config.ts`
- Create: `frontend/src/styles/index.css`
- Create: `frontend/src/services/api.ts`
- Create: `frontend/src/services/signalr.ts`

**Interfaces:**
- Consumes: Backend API on port 5000
- Produces: Configured Vite React app with proxy, CSS design system, and SignalR hub connection helper

- [ ] **Step 1: Scaffold Vite React TypeScript app in `frontend/`**

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
npm install @microsoft/signalr lucide-react
```

- [ ] **Step 2: Configure `vite.config.ts` with proxy for API and WebSockets**

```typescript
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0', // Allow mobile access on local WiFi
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5000',
        ws: true
      }
    }
  }
});
```

- [ ] **Step 3: Implement Vanilla CSS design tokens and base styles**

Create `frontend/src/styles/index.css`:
- Modern dark theme palette (Slate-900 `#0f172a`, Slate-800 `#1e293b`, Slate-700 `#334155`)
- Status colors (Green `#22c55e`, Orange `#f97316`, Red `#ef4444`, Gray `#94a3b8`)
- Glassmorphism effects, badge pills, smooth animations.

- [ ] **Step 4: Implement API client and SignalR connection helper**

Create `frontend/src/services/api.ts` and `frontend/src/services/signalr.ts`.

- [ ] **Step 5: Verify build**

```bash
cd frontend && npm run build
```
Expected: Build succeeds with 0 errors.

- [ ] **Step 6: Commit frontend foundation**

```bash
git add frontend/
git commit -m "feat(frontend): scaffold vite react app with signalr and design tokens (#13)"
```

---

### Task 6: Cleaner Authentication & Persistent Session (Mobile UI)

**Files:**
- Create: `frontend/src/features/auth/hooks/useAuth.ts`
- Create: `frontend/src/features/auth/components/LoginPage.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: `POST /api/auth/login`
- Produces: Persistent cleaner session in `localStorage` (`currentUser`)

- [ ] **Step 1: Implement `useAuth.ts` hook adhering to `react-structure`**

Manage login state, credentials, `localStorage` persistence (`facility_scanner_user`), and logout function.

- [ ] **Step 2: Implement `LoginPage.tsx` component**

Mobile-optimized clean login card:
- Username & Password inputs
- Remember me checkbox
- Error feedback alert
- Submit button with loading state

- [ ] **Step 3: Verify login flow in browser**

Login with `somchai` / `password123`. Verify session is stored in `localStorage`.

- [ ] **Step 4: Commit Auth feature**

```bash
git add frontend/
git commit -m "feat(auth): implement mobile cleaner login and persistent session (#13)"
```

---

### Task 7: Mobile 2-Tap Scan Page (Mobile UI)

**Files:**
- Create: `frontend/src/features/scan/hooks/useScanRecord.ts`
- Create: `frontend/src/features/scan/components/ScanRecordPage.tsx`
- Create: `frontend/src/features/scan/components/IssueTagSelector.tsx`

**Interfaces:**
- Consumes: `GET /api/service-points/by-token/:token`, `POST /api/scan-records`
- Produces: 2-tap mobile scan submission flow matching ADR 0001 & ADR 0007

- [ ] **Step 1: Implement `useScanRecord.ts` hook**

Handles:
- Fetching service point info by QR token
- State for status (`Normal` vs `Issue`), selected issue tags (`no_toilet_paper`, `wet_floor`, `bad_odor`, etc.), notes
- Submit function that calls `POST /api/scan-records`

- [ ] **Step 2: Implement `ScanRecordPage.tsx` component**

Layout:
- Location header: Point name, zone, interval, last scanned timestamp
- Cleaner info banner: "ผู้สแกน: สมชาย ใจดี"
- Status toggle: Big green "ปกติ (เรียบร้อย)" button (Primary 1-tap submission)
- Issue reporting button: Expands quick tag pill buttons + optional note textarea + red "ส่งรายงานปัญหา" button
- Success confirmation overlay with auto-dismiss

- [ ] **Step 3: Manual test of scan submission**

Navigate to `http://localhost:5173/scan/token-restroom-m1` and submit a Normal scan.
Verify record is saved in MySQL:
```bash
mysql -u root -e "USE facility_dashboard; SELECT * FROM scan_records ORDER BY id DESC LIMIT 1;"
```

- [ ] **Step 4: Commit Scan feature**

```bash
git add frontend/
git commit -m "feat(scan): implement 2-tap mobile scan recording page (#13)"
```

---

### Task 8: Real-Time Dashboard (Cards Grid & KPI Summary Bar)

**Files:**
- Create: `frontend/src/features/dashboard/hooks/useDashboard.ts`
- Create: `frontend/src/features/dashboard/components/DashboardView.tsx`
- Create: `frontend/src/features/dashboard/components/KpiSummaryBar.tsx`
- Create: `frontend/src/features/dashboard/components/ServicePointCard.tsx`

**Interfaces:**
- Consumes: `GET /api/service-points`, SignalR event `ScanRecorded`
- Produces: Live facility monitor with KPI counters and priority-sorted status cards

- [ ] **Step 1: Implement `useDashboard.ts` hook**

Handles:
- Initial fetch of all service points
- SignalR connection lifecycle (connect, reconnect, subscribe to `ScanRecorded`)
- Instant in-memory card status update when a scan is received
- Periodic status recalculation (every 30s) to transition cards to `Overdue` when time elapses

- [ ] **Step 2: Implement `KpiSummaryBar.tsx` component**

Display counters:
- Total Points
- Normal (Green)
- Overdue (Orange)
- Issue (Red)
- Off Hours (Gray)

- [ ] **Step 3: Implement `ServicePointCard.tsx` component**

Display point card:
- Status color border and pulsing glow indicator
- Service point name, location/floor
- Time elapsed since last scan with relative humanized format ("10 นาทีที่แล้ว")
- Cleaner name badge
- Issue tags pills if in `Issue` status

- [ ] **Step 4: Implement `DashboardView.tsx` with sorting and filters**

Cards automatically sorted by priority: `Issue` first, then `Overdue`, then `Normal`, then `OffHours`.

- [ ] **Step 5: Verify live SignalR update**

Open Dashboard in browser. Make a scan request via curl or mobile page. Verify card changes status without page refresh!

- [ ] **Step 6: Commit Dashboard feature**

```bash
git add frontend/
git commit -m "feat(dashboard): implement real-time cards grid and kpi summary bar (#13)"
```

---

### Task 9: End-to-End Walking Skeleton Verification

**Files:**
- Create: `README.md` (Update setup & run instructions)

**Interfaces:**
- Consumes: All components from Tasks 1-8
- Produces: Fully verified End-to-End Walking Skeleton over local WiFi

- [ ] **Step 1: Verify backend and frontend run concurrently**

Run backend:
```bash
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5000"
```
Run frontend:
```bash
cd frontend && npm run dev -- --host 0.0.0.0
```

- [ ] **Step 2: Test Dashboard on Mac**

Open `http://localhost:5173/dashboard`. Verify all 3 seeded points are displayed.

- [ ] **Step 3: Test Scan from Phone over Local WiFi**

Find Mac LAN IP (e.g., `192.168.1.xxx` via `ipconfig getifaddr en0`).
Open on phone: `http://<LAN_IP>:5173/scan/token-restroom-m1`.
Tap "บันทึกเรียบร้อย (ปกติ)".
Observe: On Mac screen, the card for `ห้องน้ำชาย ชั้น 1` flashes and updates to Green immediately!

- [ ] **Step 4: Test Issue reporting from Phone**

On phone, select `มีปัญหา`, tap `กระดาษหมด` (`no_toilet_paper`), tap "ส่งรายงานปัญหา".
Observe: On Mac screen, the card transitions to Red with tag badge immediately!

- [ ] **Step 5: Final documentation, commit and tag**

```bash
git add README.md
git commit -m "docs: update setup instructions and verify walking skeleton (#13)"
git tag -a walking-skeleton-v1 -m "Phase 1 Walking Skeleton complete and verified"
git push origin master --tags
```

---

## Plan Complete

Two execution options:
1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** - Execute tasks in this session using `sp-executing-plans`, batch execution with checkpoints.
