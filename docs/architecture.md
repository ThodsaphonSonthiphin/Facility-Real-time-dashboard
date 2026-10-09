# Architecture — how the Facility Real-time Dashboard is connected

This page shows how the parts of the POC connect: browser, web server, API server and database server.
It describes the code as it is on `master` today. Decisions behind it are in [docs/adr/](adr/).

| Node | What runs there | Port |
|---|---|---|
| Phone (cleaner) | Browser opens `/scan/{qrToken}` from the QR code | — |
| Dashboard screen (PC / tablet) | Browser opens `/dashboard` | — |
| Web server (dev) | Vite dev server: serves the React app, proxies `/api` and `/hubs` | `5173` |
| API server | ASP.NET Core 10 (Kestrel): Minimal API + SignalR hub | `5001` |
| Database server | MySQL 8 (Homebrew, native) — database `facility_dashboard` | `3306` |

All of it runs on one Mac. The phone reaches the Mac over the same WiFi with plain HTTP ([facility-0003](adr/facility-0003-phone-reaches-mac-over-same-wifi-http.md)).

---

## 1. Deployment diagram — where each part runs

```mermaid
flowchart LR
    subgraph LAN["Same WiFi network (HTTP only)"]
        Phone["«device» Phone<br/>Browser: Scan page"]
        Screen["«device» PC / Tablet<br/>Browser: Dashboard"]

        subgraph Mac["«device» Mac M1 (dev machine)"]
            subgraph Vite["«execution environment» Vite dev server :5173"]
                SPA["«artifact» React SPA<br/>(index.html + JS bundle)"]
                Proxy["«component» Dev proxy<br/>/api → :5001<br/>/hubs → :5001 (ws)"]
            end
            subgraph Kestrel["«execution environment» Kestrel :5001"]
                Api["«artifact» FacilityRealtime.Api"]
            end
            subgraph MySQL["«execution environment» MySQL 8 :3306"]
                DB[("«database»<br/>facility_dashboard")]
            end
        end
    end

    Phone -->|"HTTP GET page"| SPA
    Screen -->|"HTTP GET page"| SPA
    Phone -->|"HTTP/JSON /api/*"| Proxy
    Screen -->|"HTTP/JSON /api/*<br/>WebSocket /hubs/scan"| Proxy
    Proxy -->|"HTTP + WebSocket"| Api
    Api -->|"MySQL protocol (EF Core)"| DB
```

Why the proxy matters: the browser only ever talks to one origin (`:5173`).
So the refresh-token cookie works without CORS settings, and the URL inside the QR code is the Vite URL ([facility-0004](adr/facility-0004-app-architecture.md) item 5).

---

## 1b. Network diagram — servers, ports and firewalls

The deployment diagram shows *what runs where*. This network diagram shows *which traffic each boundary lets through*.
UML has no firewall element, so this is a plain infrastructure (network topology) diagram, not strict UML.

```mermaid
flowchart LR
    Internet(("Internet"))

    subgraph Home["Home / office network"]
        Router{{"WiFi router<br/>NAT + firewall<br/>no port forwarding"}}

        subgraph LAN["WiFi LAN 192.168.x.x"]
            Phone["Phone<br/>(cleaner)"]
            Screen["PC / Tablet<br/>(dashboard)"]

            subgraph Mac["Mac — web, app and database server in one machine"]
                FW{{"macOS Application Firewall<br/>allow incoming: node (Vite), dotnet"}}

                subgraph Exposed["Listens on 0.0.0.0 (reachable from LAN)"]
                    Web["Web server<br/>Vite :5173"]
                    App["App server<br/>Kestrel :5001"]
                end

                subgraph Local["Listens on 127.0.0.1 only"]
                    DBS[("Database server<br/>MySQL :3306")]
                end
            end
        end
    end

    Internet -. "blocked: no inbound route" .-x Router
    Router --- Phone & Screen & FW
    Phone -->|"HTTP :5173"| FW
    Screen -->|"HTTP + WebSocket :5173"| FW
    FW -->|"allowed"| Web
    Web -->|"proxy via localhost:5001"| App
    App -->|"localhost:3306"| DBS
    FW -. "not needed by browsers<br/>(but :5001 is open)" .-> App
    FW -. "blocked: not listening on LAN" .-x DBS
```

| Boundary | Rule | Why |
|---|---|---|
| Router (Internet → LAN) | Nothing comes in. No port forwarding, no tunnel | The phone is always on the same WiFi ([facility-0003](adr/facility-0003-phone-reaches-mac-over-same-wifi-http.md)) |
| macOS firewall (LAN → Mac) | Allow incoming connections for Vite (`node`) and the API (`dotnet`) | facility-0003 item 2. **On this Mac the firewall is currently off**, so every listening port is reachable from the WiFi |
| Vite `:5173` | The only port browsers use | Single origin, so cookies work without CORS |
| Kestrel `:5001` | Bound to `0.0.0.0` (`--urls "http://0.0.0.0:5001"` in the README). Reachable from the LAN, but only the Vite proxy needs it | Binding it to `localhost` would be enough while Vite proxies every request |
| MySQL `:3306` | Only the API connects, over `localhost` | Homebrew's MySQL config uses `bind-address = 127.0.0.1` by default, so other devices cannot reach it |

---

## 2. Component diagram — what talks to what

```mermaid
flowchart TB
    subgraph FE["Frontend — frontend/src (React 19 + TypeScript)"]
        App["App.tsx<br/>path routing + AuthProvider"]
        Dash["features/dashboard<br/>DashboardView, useDashboard"]
        Scan["features/scan<br/>ScanRecordPage, useScanRecord"]
        Login["features/auth<br/>LoginPage, AuthContext"]
        ApiSvc["services/api.ts<br/>REST calls"]
        AuthSess["services/authSession.ts<br/>access token in memory,<br/>single-flight refresh"]
        SR["services/signalr.ts<br/>HubConnection"]
    end

    subgraph BE["Backend — backend/src (.NET 10, Clean Architecture)"]
        subgraph ApiP["FacilityRealtime.Api"]
            Ep["Endpoints/ServicePointEndpoints, ScanRecordEndpoints, MeEndpoints<br/>/api/service-points (Admin) | /api/scan-records | /api/me"]
            AuthEp["Endpoints/AuthEndpoints.cs<br/>/api/auth/login | refresh | logout"]
            Hub["Hubs/ScanHub.cs<br/>/hubs/scan  [Authorize]"]
            Jwt["Auth/AuthSetup.cs<br/>JWT bearer validation"]
        end
        subgraph AppP["FacilityRealtime.Application"]
            Status["Shifts/ShiftCalendar<br/>Rounds/RoundPlacer, PointStatusCalculator"]
            Rules["RefreshTokenRules<br/>IAccessTokenIssuer, IPasswordHasher"]
        end
        subgraph InfP["FacilityRealtime.Infrastructure"]
            Ctx["AppDbContext (EF Core)<br/>Migrations, DbInitializer"]
            Hash["Pbkdf2PasswordHasher"]
        end
        subgraph DomP["FacilityRealtime.Domain"]
            Ent["Entities: User, ServicePoint,<br/>ScanRecord, RefreshToken"]
        end
    end

    DB[("MySQL<br/>facility_dashboard")]

    App --> Dash & Scan & Login
    Dash --> ApiSvc
    Dash --> SR
    Scan --> ApiSvc
    Login --> AuthSess
    ApiSvc --> AuthSess
    SR --> AuthSess

    AuthSess -->|"POST /api/auth/*<br/>(cookie facility_refresh)"| AuthEp
    ApiSvc -->|"GET/POST /api/*<br/>Authorization: Bearer"| Ep
    SR -->|"WebSocket /hubs/scan?access_token=…"| Hub

    Ep --> Status
    Ep --> Ctx
    Ep -->|"IHubContext.Clients.All"| Hub
    AuthEp --> Rules
    AuthEp --> Ctx
    Jwt -.->|validates| Ep & Hub

    ApiP --> AppP
    ApiP --> InfP
    InfP --> AppP
    AppP --> DomP
    InfP --> DomP
    Ctx -->|"MySql.EntityFrameworkCore"| DB
```

Project references follow Clean Architecture: `Api → Application, Infrastructure`; `Infrastructure → Application, Domain`; `Application → Domain`; `Domain` depends on nothing.

> Note: [facility-0004](adr/facility-0004-app-architecture.md) plans Mediator handlers in `Application`. The code does not use Mediator yet. The endpoint handlers live in `Endpoints/` (AuthEndpoints, MeEndpoints, ServicePointEndpoints, ScanRecordEndpoints), use `AppDbContext` directly, and `Program.cs` only composes them.

---

## 3. Sequence diagram — a scan reaches the dashboard in real time

This is the main flow: a Cleaner submits a Scan Record and the Admin Dashboard updates.

```mermaid
sequenceDiagram
    autonumber
    actor Cleaner
    participant Phone as Phone browser<br/>(ScanRecordPage)
    participant Vite as Vite :5173<br/>(proxy)
    participant Api as API :5001<br/>(Endpoints/*)
    participant DB as MySQL
    participant Hub as ScanHub<br/>/hubs/scan
    participant Dash as Dashboard browser<br/>(DashboardView)

    Note over Dash,Hub: Earlier: an Admin opened the Dashboard; its WebSocket to /hubs/scan<br/>(?access_token=<JWT>) joined the "admins" group (facility-0058)

    Cleaner->>Phone: Scan QR with phone camera → opens /scan/{qrToken}
    Phone->>Vite: GET /api/service-points/by-token/{qrToken}<br/>Authorization: Bearer <JWT>
    Vite->>Api: forward
    Api->>DB: SELECT sign → point, this shift's Round Windows, Scan and Inspection Records
    DB-->>Api: rows
    Api-->>Phone: 200 PointStatusDto (no QR Token)

    Cleaner->>Phone: Choose Normal / Issue (+ tags, notes), tap submit
    Phone->>Vite: POST /api/scan-records { qrToken, status, issueTags, notes }
    Vite->>Api: forward
    Note right of Api: The scanner is the user in the JWT,<br/>never a field in the body (facility-0017)
    Api->>Api: RoundPlacer.Place(...) → OnTime / Late / Rework / OffRound (facility-0047)
    Api->>DB: INSERT scan_records (shift, round, placement)
    DB-->>Api: OK
    Api->>Api: PointStatusCalculator.Calculate(...)
    Api->>Hub: Clients.Group("admins").SendAsync("ScanRecorded", dto)
    Hub-->>Dash: "ScanRecorded" (WebSocket push)
    Dash->>Dash: Update the card and KPI bar, no page reload
    Api-->>Phone: 201 Created { scanRecordId, placement, lateMinutes, newPointStatus, submittedAt }
```

---

## 4. Sequence diagram — login, token refresh and page reload

The access token (JWT, 5 minutes) lives only in browser memory. The refresh token lives in an HttpOnly cookie
that the browser sends only to `/api/auth` ([facility-0011](adr/facility-0011-jwt-api-authentication.md) – [facility-0016](adr/facility-0016-refresh-token-sliding-30-days.md)).

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Browser<br/>(authSession.ts)
    participant Auth as API /api/auth
    participant DB as MySQL
    participant Api as API /api/* or /hubs/scan

    User->>FE: Enter username + password
    FE->>Auth: POST /api/auth/login
    Auth->>DB: Find user, verify PBKDF2 hash
    Auth->>DB: INSERT refresh_tokens (SHA-256 hash only)
    Auth-->>FE: 200 { accessToken, expiresAt, user }<br/>Set-Cookie: facility_refresh (HttpOnly, Path=/api/auth)

    FE->>Api: Request with Authorization: Bearer <accessToken>
    Api-->>FE: 200

    Note over FE: About 5 minutes later the token expires (or the page reloads)

    FE->>Api: Request with expired token
    Api-->>FE: 401
    FE->>Auth: POST /api/auth/refresh (cookie sent automatically)<br/>only one refresh at a time (single-flight)
    Auth->>DB: Check hash, mark old token rotated, INSERT new token
    alt Token is valid
        Auth-->>FE: 200 new accessToken + new cookie
        FE->>Api: Retry the request once
    else Token was already used (reuse) or expired
        Auth->>DB: Revoke every token in this session
        Auth-->>FE: 401
        FE->>User: Show the login page
    end
```

---

## 5. Class diagram — data stored in MySQL

```mermaid
classDiagram
    direction LR
    class Building {
        <<buildings>>
        +int Id
        +string Code «unique»
        +string Name
        +bool IsActive
    }
    class Area {
        <<areas>>
        +int Id
        +int BuildingId
        +string Code «unique»
        +string Name
        +ShiftPattern ShiftPattern
        +bool IsActive
    }
    class ServicePoint {
        <<service_points>>
        +int Id
        +int AreaId
        +string Name
        +short SortOrder
        +bool IsActive
    }
    class Sign {
        <<signs>>
        +int Id
        +int AreaId
        +int? ServicePointId «unique»
        +string Code «unique»
        +string QrToken «unique»
        +int? CheckinAreaId «computed, unique»
    }
    class PointRoundWindow {
        <<point_round_windows>>
        +int Id
        +int ServicePointId
        +Shift Shift
        +TimeOnly StartTime
        +TimeOnly EndTime
    }
    class User {
        <<users>>
        +int Id
        +UserRole Role
        +string? EmployeeId «unique»
        +string? Username «unique»
        +string DisplayName
        +string SecretHash
        +int? AreaId
        +int? BuildingId
        +Shift? Shift
        +bool IsActive
        +int? CleanerSlot «computed, unique»
        +int? SupervisorSlot «computed, unique»
    }
    class RefreshToken {
        <<refresh_tokens>>
        +long Id
        +int UserId
        +Guid SessionId
        +string TokenHash «unique»
    }
    class ScanRecord {
        <<scan_records>>
        +long Id
        +int ServicePointId
        +int SignId
        +int UserId
        +DateOnly ShiftDate
        +Shift Shift
        +int? RoundWindowId
        +Placement Placement
        +int? LateMinutes
        +CleaningStatus Status
        +DateTime SubmittedAt
    }
    class InspectionRecord {
        <<inspections>>
        +long Id
        +long ScanRecordId
        +int ServicePointId
        +int SupervisorId
        +InspectionResult Result
        +string? Defect
        +DateTime InspectedAt
    }

    Building "1" <-- "0..*" Area
    Area "1" <-- "0..*" ServicePoint
    Area "1" <-- "1..*" Sign
    ServicePoint "1" <-- "0..1" Sign
    ServicePoint "1" <-- "0..*" PointRoundWindow
    Area "0..1" <-- "0..*" User : Cleaner
    Building "0..1" <-- "0..*" User : Supervisor
    User "1" <-- "0..*" RefreshToken : cascade delete
    ServicePoint "1" <-- "0..*" ScanRecord
    PointRoundWindow "0..1" <-- "0..*" ScanRecord : set null on delete
    ScanRecord "1" <-- "0..*" InspectionRecord
    Sign "1" <-- "0..*" ScanRecord
    User "1" <-- "0..*" ScanRecord : cleaner
    ServicePoint "1" <-- "0..*" InspectionRecord
    User "1" <-- "0..*" InspectionRecord : supervisor
```

The full target schema, including the tables later plans add, is `docs/design/database.html`. Point Status is not stored: the API computes it on every read from the point's current Round Window, that shift's Scan Records and their Inspection Records (`Application/Rounds/PointStatusCalculator`, [facility-0047](adr/facility-0047-cleaning-rounds-are-time-windows.md)). A Scan Record's round is decided once, when it is saved (`Application/Rounds/RoundPlacer`).

---

## Connection summary

| From | To | Protocol | Auth | Configured in |
|---|---|---|---|---|
| Browser | Vite `:5173` | HTTP | — | `frontend/vite.config.ts` |
| Vite proxy | API `:5001` `/api/*` | HTTP/JSON | `Authorization: Bearer <JWT>` | `frontend/vite.config.ts` |
| Vite proxy | API `:5001` `/hubs/scan` | WebSocket (SignalR) | `?access_token=<JWT>` (hub paths only) | `Auth/AuthSetup.cs`, `services/signalr.ts` |
| Browser | API `/api/auth/*` | HTTP/JSON | HttpOnly cookie `facility_refresh` | `Endpoints/AuthEndpoints.cs` |
| API | MySQL `:3306` | MySQL protocol via EF Core | `ConnectionStrings:DefaultConnection` | `appsettings.json` / user-secrets |
| API | itself on startup | — | — | `Program.cs` runs `MigrateAsync()` and seeds data |
