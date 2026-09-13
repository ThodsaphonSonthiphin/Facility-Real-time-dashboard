# Phase 2A: Login, JWT and Refresh Tokens Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the phase 1 fake login (a random token nobody checks, the whole session in `localStorage`) with server-checked JWT access tokens, rotating refresh tokens in an HttpOnly cookie, and login-required dashboard, scan and SignalR endpoints.

**Architecture:** The API issues a 5-minute JWT on login and keeps the long-lived refresh token only as a SHA-256 hash in a new `refresh_tokens` table; the browser holds the refresh token in an HttpOnly cookie scoped to `/api/auth` and the access token in memory. Pure rules (password hashing, refresh rotation decisions) live in `Application`/`Infrastructure` and are unit-tested; endpoint behaviour is tested through `WebApplicationFactory` on in-memory SQLite, because this Mac has no MySQL.

**Tech Stack:** .NET 10 Minimal API, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9 at runtime, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 in tests), xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12; React 19 + TypeScript + Vite 8, `@microsoft/signalr`, Vitest 5.0.0.

**Spec:**
- `docs/adr/facility-0002-scanner-authentication.md` (accounts created by Admin, passwords hashed securely)
- `docs/adr/facility-0010-admin-gate-server-side.md`
- `docs/adr/facility-0011-jwt-api-authentication.md`
- `docs/adr/facility-0012-short-access-token-with-refresh.md`
- `docs/adr/facility-0013-refresh-token-httponly-cookie.md`
- `docs/adr/facility-0014-refresh-tokens-stored-server-side.md`
- `docs/adr/facility-0015-refresh-token-reuse-detection.md`
- `docs/adr/facility-0016-refresh-token-sliding-30-days.md`
- `docs/adr/facility-0017-scanner-identity-from-jwt.md`
- `docs/adr/facility-0018-dashboard-and-hub-require-login.md`
- `docs/adr/facility-0006-mysql-schema-and-qr-token.md` (amendment: 4th table `refresh_tokens`, `users.is_active`)
- `CONTEXT.md` (Persistent Session, Cleaner Account, Admin Account, Deactivated Account)

**Follow-on plans:** `2026-09-13-phase2b-accounts-page.md` and `2026-09-13-phase2c-points-admin-page.md` both depend on this plan being merged first.

## Global Constraints

- Access token lifetime: **5 minutes**; JWT `ClockSkew` **30 seconds** (facility-0012).
- On every refresh the server re-reads the account's **role and active state** from the database (facility-0012).
- Refresh token: random, **at least 256 bits**, server stores **only its hash**, **rotated on every refresh** (facility-0014).
- Logout revokes the refresh tokens **of that login** and deletes the cookie; deactivating an account or changing its password revokes **every** refresh token of that user (facility-0014).
- A rotated refresh token presented again **within 30 seconds** is treated as a concurrent refresh from another tab; **after 30 seconds** it revokes every token of that login (facility-0015).
- Refresh token lifetime: **sliding 30 days** from last use, **no absolute cap** (facility-0016).
- Refresh token lives in an **HttpOnly cookie sent only to `/api/auth`**; the access token lives **in page memory, never `localStorage`**; the cookie is **not `Secure`** in this POC because phones use HTTP (facility-0013, facility-0003).
- API calls send `Authorization: Bearer`; SignalR's `access_token` query value is accepted **only for paths under `/hubs`** (facility-0011).
- The JWT signing key comes from config or `dotnet user-secrets`, **never from committed files** — the repo is public (facility-0011, facility-0004).
- `POST /api/scan-records` reads the scanner from the JWT; **`UserId` is removed from the request body**; cleaner and admin accounts may both scan (facility-0017).
- `GET /api/service-points`, `GET /api/service-points/by-token/{token}` and `/hubs/scan` require **any logged-in account** (facility-0018).
- Admin-only APIs answer **401 when not logged in and 403 when logged in but not admin** (facility-0010). This plan registers the `AdminOnly` policy; plans 2B and 2C use it.
- Roles stay lowercase strings `cleaner` and `admin`, as stored on master.
- Password hashing (plan-level choice implementing facility-0002's "hashed securely"): PBKDF2-HMAC-SHA256, 600,000 iterations, 16-byte salt, stored as `pbkdf2-sha256$<iterations>$<salt b64>$<hash b64>`. Master's unsalted SHA-256 is replaced, not kept as a fallback.
- Follow the established code shape: Minimal API handlers call `AppDbContext` directly. Phase 1 did not adopt the Mediator library named in facility-0004, and this plan does not introduce it. Frontend keeps Vanilla CSS, the manual router in `App.tsx`, and hooks in separate files from components.
- No Docker (facility-0004). Automated tests never need MySQL; MySQL is only for the manual end-to-end task.
- Commit messages reference the decision tickets: `(#15)` for login/token work, `(#18)` for scanner identity and login-required dashboard.

## File Structure

| Path | Status | Responsibility |
|---|---|---|
| `backend/tests/FacilityRealtime.ApiTests/FacilityRealtime.ApiTests.csproj` | Create | API test project |
| `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs` | Create | Boots the API in `Testing` environment on in-memory SQLite with a controllable clock |
| `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestTimeProvider.cs` | Create | `TimeProvider` tests can move forward |
| `backend/tests/FacilityRealtime.ApiTests/Infrastructure/AuthApi.cs` | Create | Login/refresh request helpers and cookie parsing |
| `backend/tests/FacilityRealtime.ApiTests/HarnessTests.cs` | Create | Proves the harness boots and seeds |
| `backend/tests/FacilityRealtime.ApiTests/Auth/LoginTests.cs` | Create | Login endpoint behaviour |
| `backend/tests/FacilityRealtime.ApiTests/Auth/RefreshTests.cs` | Create | Rotation, grace, reuse, sliding expiry, logout |
| `backend/tests/FacilityRealtime.ApiTests/Auth/ProtectedEndpointTests.cs` | Create | 401s, scanner identity, hub negotiate |
| `backend/tests/FacilityRealtime.ApiTests/Persistence/RefreshTokenModelTests.cs` | Create | Schema rules for the new table and column |
| `backend/tests/FacilityRealtime.UnitTests/Pbkdf2PasswordHasherTests.cs` | Create | Hash format and verification |
| `backend/tests/FacilityRealtime.UnitTests/RefreshTokenRulesTests.cs` | Create | Rotation decisions and token generation |
| `backend/src/FacilityRealtime.Application/Auth/IPasswordHasher.cs` | Create | Hashing contract |
| `backend/src/FacilityRealtime.Application/Auth/RefreshTokenRules.cs` | Create | Pure rotate / grace / reuse / reject decision, token generation and hashing |
| `backend/src/FacilityRealtime.Application/Auth/JwtSettings.cs` | Create | `Jwt` config section |
| `backend/src/FacilityRealtime.Application/Auth/IAccessTokenIssuer.cs` | Create | Access token contract and `AccessToken` record |
| `backend/src/FacilityRealtime.Infrastructure/Auth/Pbkdf2PasswordHasher.cs` | Create | PBKDF2 implementation |
| `backend/src/FacilityRealtime.Infrastructure/Persistence/RefreshTokenRevocation.cs` | Create | `RevokeSessionAsync` / `RevokeUserSessionsAsync` |
| `backend/src/FacilityRealtime.Domain/Entities/RefreshToken.cs` | Create | Refresh token row |
| `backend/src/FacilityRealtime.Domain/Entities/User.cs` | Modify | Add `IsActive` |
| `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs` | Modify | `RefreshTokens` set and mapping |
| `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs` | Modify | Seed with `IPasswordHasher`; stop calling `EnsureCreated` |
| `backend/src/FacilityRealtime.Infrastructure/Migrations/*_Phase2AuthRefreshTokens.cs` | Create (generated) | `users.IsActive`, `refresh_tokens` |
| `backend/src/FacilityRealtime.Api/Auth/AuthClaims.cs` | Create | Claim type names |
| `backend/src/FacilityRealtime.Api/Auth/JwtKeys.cs` | Create | Signing key from settings |
| `backend/src/FacilityRealtime.Api/Auth/JwtAccessTokenIssuer.cs` | Create | Issues the JWT |
| `backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs` | Create | Options validation, JwtBearer, `AdminOnly` policy |
| `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs` | Create | `/api/auth/login`, `/refresh`, `/logout` |
| `backend/src/FacilityRealtime.Api/DTOs/AuthDtos.cs` | Create | `LoginRequest`, `AuthResponse`, `AuthUserDto` |
| `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs` | Modify | Remove old login DTOs and `UserId` |
| `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs` | Modify | `[Authorize]` |
| `backend/src/FacilityRealtime.Api/Program.cs` | Modify | Testing switch, migrations, auth wiring, `RequireAuthorization` |
| `backend/src/FacilityRealtime.Api/appsettings.json` | Modify | Non-secret `Jwt` values |
| `frontend/src/services/authSession.ts` | Create | In-memory access token, single-flight refresh, `apiFetch` |
| `frontend/src/services/authSession.test.ts` | Create | Vitest tests for the session module |
| `frontend/src/types/index.ts` | Modify | `AuthUser`, `AuthResponse`; drop `UserSession` and `userId` |
| `frontend/src/services/api.ts` | Modify | Every call through `apiFetch` |
| `frontend/src/services/signalr.ts` | Modify | `accessTokenFactory` |
| `frontend/src/features/auth/context/AuthContext.tsx` | Modify | Restore session by refresh on load; no `localStorage` |
| `frontend/src/features/auth/components/LoginPage.tsx` | Modify | Use `isSubmitting` |
| `frontend/src/features/scan/hooks/useScanRecord.ts` | Modify | Drop `userId` |
| `frontend/src/features/scan/components/ScanRecordPage.tsx` | Modify | `AuthUser` prop type |
| `frontend/src/App.tsx` | Modify | Dashboard behind login; wait for session restore |
| `frontend/package.json`, `frontend/vite.config.ts` | Modify | Vitest |
| `README.md` | Modify | DB reset, signing key secret, test commands |

---

### Task 1: API test harness and migrations at startup

Phase 1 creates the schema with `EnsureCreatedAsync`, which never applies migrations, so the columns this plan adds would never reach an existing database. This task switches startup to `MigrateAsync`, skips the MySQL registration in a `Testing` environment, and adds a test project that boots the real `Program` on in-memory SQLite.

**Files:**
- Create: `backend/tests/FacilityRealtime.ApiTests/FacilityRealtime.ApiTests.csproj`
- Create: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestTimeProvider.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/HarnessTests.cs`
- Modify: `backend/FacilityRealtime.slnx`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs:28-33` (DbContext), `:66-71` (seed), end of file
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs:15`

**Interfaces:**
- Consumes: `Program` (top-level statements), `AppDbContext`, `DbInitializer.SeedAsync(AppDbContext)`
- Produces:
  - `public partial class Program;` so tests can reference the entry point
  - `FacilityApiFactory : WebApplicationFactory<Program>` with `TestTimeProvider Clock`, `Task WithDbAsync(Func<AppDbContext, Task> action)`, and constructor `FacilityApiFactory(string signingKey = FacilityApiFactory.TestSigningKey)`
  - `TestTimeProvider : TimeProvider` with `void Advance(TimeSpan by)`
  - `TimeProvider` registered in DI (`TimeProvider.System` in the app); every later task reads "now" from it

- [ ] **Step 1: Create the test project and add it to the solution**

Create `backend/tests/FacilityRealtime.ApiTests/FacilityRealtime.ApiTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\FacilityRealtime.Api\FacilityRealtime.Api.csproj" />
  </ItemGroup>

</Project>
```

Run:

```bash
dotnet sln backend/FacilityRealtime.slnx add backend/tests/FacilityRealtime.ApiTests/FacilityRealtime.ApiTests.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 2: Write the clock, the factory and the failing harness test**

Create `backend/tests/FacilityRealtime.ApiTests/Infrastructure/TestTimeProvider.cs`:

```csharp
namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>A clock tests can move forward. Starts at the real "now" so JWTs issued without advancing still validate.</summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
```

Create `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`:

```csharp
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>
/// Boots the real Program in the "Testing" environment: Program skips its MySQL registration and
/// startup migration there, and this factory supplies in-memory SQLite plus a movable clock.
/// Create one per test when the test mutates data or the clock.
/// </summary>
public sealed class FacilityApiFactory(string signingKey = FacilityApiFactory.TestSigningKey) : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-signing-key-that-is-at-least-32-bytes-long-0123456789";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting reaches Program before builder.Build(), unlike ConfigureAppConfiguration
        builder.UseSetting("Jwt:SigningKey", signingKey);

        builder.ConfigureServices(services =>
        {
            _connection.Open();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
        return host;
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
```

Create `backend/tests/FacilityRealtime.ApiTests/HarnessTests.cs`:

```csharp
using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests;

public class HarnessTests
{
    [Fact]
    public async Task Api_boots_on_sqlite_and_answers_the_healthcheck()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Seed_runs_against_the_test_database()
    {
        using var factory = new FacilityApiFactory();
        var pointCount = 0;
        var userCount = 0;

        await factory.WithDbAsync(async db =>
        {
            pointCount = await db.ServicePoints.CountAsync();
            userCount = await db.Users.CountAsync();
        });

        Assert.Equal(3, pointCount);
        Assert.Equal(2, userCount);
    }
}
```

- [ ] **Step 3: Run the harness tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo`
Expected: FAIL. The build fails with `'Program' is inaccessible due to its protection level`, or, once that compiles, startup tries to reach MySQL on `localhost:3306` and throws.

- [ ] **Step 4: Make Program testable and switch startup to migrations**

In `backend/src/FacilityRealtime.Api/Program.cs`, replace lines 28-33:

```csharp
// 1. Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(connectionString));
```

with:

```csharp
// 1. Clock and Database Context. The "Testing" environment (API tests) registers its own SQLite context.
builder.Services.AddSingleton(TimeProvider.System);

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySQL(connectionString));
}
```

Replace the seed block (lines 66-71):

```csharp
// 5. Seed initial data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.SeedAsync(db);
}
```

with:

```csharp
// 5. Apply migrations and seed on startup (tests create their own schema)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(db);
}
```

Append after `app.Run();` at the end of the file:

```csharp

// Lets WebApplicationFactory<Program> in the API tests reach the entry point
public partial class Program;
```

In `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs`, delete line 15 and the blank line after it:

```csharp
        await context.Database.EnsureCreatedAsync();
```

The schema now comes from `MigrateAsync` at startup, or from `EnsureCreated` in the test factory.

- [ ] **Step 5: Run the harness tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo`
Expected: PASS, `Passed: 2`.

Run the whole solution to confirm nothing else broke: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS, unit tests 15 plus API tests 2.

- [ ] **Step 6: Commit**

```bash
git add backend/FacilityRealtime.slnx backend/tests/FacilityRealtime.ApiTests backend/src/FacilityRealtime.Api/Program.cs backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs
git commit -m "test(api): boot Program on in-memory SQLite and apply migrations at startup (#15)"
```

**Local database note for whoever runs the app on MySQL:** a database created by phase 1's `EnsureCreated` has no migrations history, so `MigrateAsync` fails with `Table 'service_points' already exists`. Reset it once (Task 10 puts this in the README):

```bash
mysql -u root -e "DROP DATABASE IF EXISTS facility_dashboard; CREATE DATABASE facility_dashboard CHARACTER SET utf8mb4;"
```

### Task 2: PBKDF2 password hashing

Master stores `SHA256(password)` with no salt, so two accounts with the same password share a hash and a leaked table falls to a lookup table. facility-0002 requires secure hashing, and plan 2B lets an Admin type new passwords, so this lands before any password-writing screen exists.

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Auth/IPasswordHasher.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Auth/Pbkdf2PasswordHasher.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/Pbkdf2PasswordHasherTests.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Auth/LoginTests.cs`
- Modify: `backend/tests/FacilityRealtime.UnitTests/FacilityRealtime.UnitTests.csproj` (reference Infrastructure)
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs` (seed signature, remove `HashPassword`)
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (register hasher, seed call, login handler)
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs` (fast hasher, seed call)

**Interfaces:**
- Consumes: `FacilityApiFactory` (Task 1)
- Produces:
  - `FacilityRealtime.Application.Auth.IPasswordHasher` with `string Hash(string password)` and `bool Verify(string password, string storedHash)`
  - `FacilityRealtime.Infrastructure.Auth.Pbkdf2PasswordHasher` with `const int DefaultIterations = 600_000`, constructors `()` and `(int iterations)`
  - `DbInitializer.SeedAsync(AppDbContext context, IPasswordHasher hasher)`; `DbInitializer.HashPassword` no longer exists

- [ ] **Step 1: Write the failing unit tests**

Add to `backend/tests/FacilityRealtime.UnitTests/FacilityRealtime.UnitTests.csproj`, inside the existing `ProjectReference` `ItemGroup`:

```xml
    <ProjectReference Include="..\..\src\FacilityRealtime.Infrastructure\FacilityRealtime.Infrastructure.csproj" />
```

Create `backend/tests/FacilityRealtime.UnitTests/Pbkdf2PasswordHasherTests.cs`:

```csharp
using FacilityRealtime.Infrastructure.Auth;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class Pbkdf2PasswordHasherTests
{
    // Low iteration count keeps the suite fast; production uses DefaultIterations
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Verify_accepts_the_password_that_was_hashed()
    {
        var stored = _hasher.Hash("password123");

        Assert.True(_hasher.Verify("password123", stored));
    }

    [Fact]
    public void Verify_rejects_a_different_password()
    {
        var stored = _hasher.Hash("password123");

        Assert.False(_hasher.Verify("password124", stored));
    }

    [Fact]
    public void Same_password_hashes_differently_because_of_the_salt()
    {
        Assert.NotEqual(_hasher.Hash("password123"), _hasher.Hash("password123"));
    }

    [Fact]
    public void Stored_format_names_algorithm_and_iterations()
    {
        var parts = _hasher.Hash("password123").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("1000", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void Verify_reads_iterations_from_the_stored_value()
    {
        var storedWithMoreIterations = new Pbkdf2PasswordHasher(iterations: 2_000).Hash("password123");

        Assert.True(_hasher.Verify("password123", storedWithMoreIterations));
    }

    [Theory]
    [InlineData("ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f")] // master's unsalted SHA-256 of "password123"
    [InlineData("")]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$1000$not-base64$AAAA")]
    [InlineData("bcrypt$10$salt$hash")]
    public void Verify_rejects_values_that_are_not_pbkdf2_hashes(string stored)
    {
        Assert.False(_hasher.Verify("password123", stored));
    }

    [Fact]
    public void Default_iterations_follow_owasp_guidance_for_pbkdf2_sha256()
    {
        Assert.Equal(600_000, Pbkdf2PasswordHasher.DefaultIterations);
    }
}
```

Create `backend/tests/FacilityRealtime.ApiTests/Auth/LoginTests.cs` (Task 5 replaces this file with the full login suite):

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests.Auth;

public class LoginTests
{
    [Fact]
    public async Task Seeded_cleaner_logs_in_against_a_pbkdf2_hash()
    {
        using var factory = new FacilityApiFactory();
        string storedHash = "";
        await factory.WithDbAsync(async db => storedHash = (await db.Users.FindAsync(1))!.PasswordHash);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "somchai", password = "password123" });

        Assert.StartsWith("pbkdf2-sha256$", storedHash);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "somchai", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: FAIL at build with `The type or namespace name 'Auth' does not exist in the namespace 'FacilityRealtime.Infrastructure'`.

- [ ] **Step 3: Write the contract and the implementation**

Create `backend/src/FacilityRealtime.Application/Auth/IPasswordHasher.cs`:

```csharp
namespace FacilityRealtime.Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>False for a wrong password and for any stored value this hasher did not produce.</summary>
    bool Verify(string password, string storedHash);
}
```

Create `backend/src/FacilityRealtime.Infrastructure/Auth/Pbkdf2PasswordHasher.cs`:

```csharp
using System.Security.Cryptography;
using FacilityRealtime.Application.Auth;

namespace FacilityRealtime.Infrastructure.Auth;

/// <summary>
/// PBKDF2-HMAC-SHA256 with a random 16-byte salt (ADR facility-0002: passwords hashed securely).
/// Stored as "pbkdf2-sha256$iterations$salt$hash" so the iteration count can rise later without breaking old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int DefaultIterations = 600_000;

    private const string Prefix = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly int _iterations;

    public Pbkdf2PasswordHasher() : this(DefaultIterations)
    {
    }

    public Pbkdf2PasswordHasher(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join('$', Prefix, _iterations.ToString(), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string storedHash)
    {
        if (password is null || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
```

- [ ] **Step 4: Seed and log in through the hasher**

In `backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs`:

1. Replace `using System.Security.Cryptography;` and `using System.Text;` with `using FacilityRealtime.Application.Auth;`.
2. Change the signature to `public static async Task SeedAsync(AppDbContext context, IPasswordHasher hasher)`.
3. Replace `PasswordHash = HashPassword("password123"),` with `PasswordHash = hasher.Hash("password123"),`.
4. Replace `PasswordHash = HashPassword("admin1234"),` with `PasswordHash = hasher.Hash("admin1234"),`.
5. Delete the whole `HashPassword` method at the bottom of the class.

The seed accounts `somchai / password123` and `admin / admin1234` are sample data already published in the README and the dashboard's QR modal, not secrets.

In `backend/src/FacilityRealtime.Api/Program.cs`:

1. Add `using FacilityRealtime.Application.Auth;` and `using FacilityRealtime.Infrastructure.Auth;` to the usings.
2. Directly after `builder.Services.AddSingleton(TimeProvider.System);` add:

```csharp
builder.Services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
```

3. In the startup block, replace `await DbInitializer.SeedAsync(db);` with:

```csharp
    await DbInitializer.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
```

4. Replace the login handler:

```csharp
app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db) =>
{
    var hash = DbInitializer.HashPassword(req.Password);
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.PasswordHash == hash);
    if (user == null)
    {
        return Results.Unauthorized();
    }
```

with:

```csharp
app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, IPasswordHasher hasher) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
    if (user == null || !hasher.Verify(req.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }
```

In `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`:

1. Add `using FacilityRealtime.Application.Auth;` and `using FacilityRealtime.Infrastructure.Auth;`.
2. Inside `ConfigureServices`, after the `TimeProvider` line, add:

```csharp
            // 600,000 iterations per login would make the API suite slow; the format is identical
            services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(iterations: 1_000));
```

3. In `CreateHost`, replace `DbInitializer.SeedAsync(db).GetAwaiter().GetResult();` with:

```csharp
        DbInitializer.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>()).GetAwaiter().GetResult();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 26, API tests 4.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Auth backend/src/FacilityRealtime.Infrastructure/Auth backend/src/FacilityRealtime.Infrastructure/Persistence/DbInitializer.cs backend/src/FacilityRealtime.Api/Program.cs backend/tests
git commit -m "feat(auth): hash passwords with salted PBKDF2 instead of plain SHA-256 (#15)"
```

---

### Task 3: `users.IsActive` and the `refresh_tokens` table

**Files:**
- Create: `backend/src/FacilityRealtime.Domain/Entities/RefreshToken.cs`
- Create: `backend/src/FacilityRealtime.Infrastructure/Persistence/RefreshTokenRevocation.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Persistence/RefreshTokenModelTests.cs`
- Create (generated): `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_Phase2AuthRefreshTokens.cs` and `.Designer.cs`
- Modify: `backend/src/FacilityRealtime.Domain/Entities/User.cs`
- Modify: `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`
- Modify (generated): `backend/src/FacilityRealtime.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `FacilityApiFactory.WithDbAsync` (Task 1)
- Produces:
  - `User.IsActive` (`bool`, defaults to `true`)
  - `RefreshToken` with `long Id`, `int UserId`, `Guid SessionId`, `string TokenHash`, `DateTime CreatedAt`, `DateTime ExpiresAt`, `DateTime? RotatedAt`, `DateTime? RevokedAt`, `User? User`
  - `AppDbContext.RefreshTokens`
  - `RefreshTokenRevocation.RevokeSessionAsync(this AppDbContext db, Guid sessionId, DateTime nowUtc)` and `RevokeUserSessionsAsync(this AppDbContext db, int userId, DateTime nowUtc)`, both `Task<int>` returning rows revoked

- [ ] **Step 1: Write the failing persistence tests**

Create `backend/tests/FacilityRealtime.ApiTests/Persistence/RefreshTokenModelTests.cs`:

```csharp
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

public class RefreshTokenModelTests
{
    private static RefreshToken Token(int userId, Guid sessionId, string hash) => new()
    {
        UserId = userId,
        SessionId = sessionId,
        TokenHash = hash,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(30),
    };

    [Fact]
    public async Task Seeded_users_are_active()
    {
        using var factory = new FacilityApiFactory();
        var activeCount = 0;

        await factory.WithDbAsync(async db => activeCount = await db.Users.CountAsync(u => u.IsActive));

        Assert.Equal(2, activeCount);
    }

    [Fact]
    public async Task A_deactivated_user_stays_deactivated_after_saving()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Username == "somchai");
            user.IsActive = false;
            await db.SaveChangesAsync();
        });

        var isActive = true;
        await factory.WithDbAsync(async db => isActive = (await db.Users.AsNoTracking().SingleAsync(u => u.Username == "somchai")).IsActive);
        Assert.False(isActive);
    }

    [Fact]
    public async Task Token_hash_must_be_unique()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.Add(Token(1, Guid.NewGuid(), new string('a', 64)));
            db.RefreshTokens.Add(Token(1, Guid.NewGuid(), new string('a', 64)));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Revoking_a_session_leaves_other_sessions_alone()
    {
        using var factory = new FacilityApiFactory();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.AddRange(Token(1, sessionA, new string('a', 64)), Token(1, sessionA, new string('b', 64)), Token(1, sessionB, new string('c', 64)));
            await db.SaveChangesAsync();

            var revoked = await db.RevokeSessionAsync(sessionA, now);

            Assert.Equal(2, revoked);
            Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.RevokedAt == null));
        });
    }

    [Fact]
    public async Task Revoking_a_user_revokes_every_session_of_that_user_only()
    {
        using var factory = new FacilityApiFactory();
        var now = DateTime.UtcNow;

        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.AddRange(Token(1, Guid.NewGuid(), new string('a', 64)), Token(1, Guid.NewGuid(), new string('b', 64)), Token(2, Guid.NewGuid(), new string('c', 64)));
            await db.SaveChangesAsync();

            var revoked = await db.RevokeUserSessionsAsync(1, now);

            Assert.Equal(2, revoked);
            Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == 2 && t.RevokedAt == null));
        });
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo`
Expected: FAIL at build with `'User' does not contain a definition for 'IsActive'` and `'AppDbContext' does not contain a definition for 'RefreshTokens'`.

- [ ] **Step 3: Add the entity, the column, the mapping and the revocation helpers**

In `backend/src/FacilityRealtime.Domain/Entities/User.cs`, add after the `Role` property:

```csharp
    /// <summary>False for a Deactivated Account: cannot log in, refresh is refused (ADR facility-0012, facility-0020).</summary>
    public bool IsActive { get; set; } = true;
```

Create `backend/src/FacilityRealtime.Domain/Entities/RefreshToken.cs`:

```csharp
using System;

namespace FacilityRealtime.Domain.Entities;

/// <summary>
/// One row per refresh token ever issued (ADR facility-0014). Only the SHA-256 hash of the token is stored.
/// Every token rotated out of one login shares that login's SessionId (facility-0015).
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>CreatedAt + 30 days; each rotation issues a new row, which is what makes the lifetime sliding (facility-0016).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When this token was first exchanged for a successor. Null while unused.</summary>
    public DateTime? RotatedAt { get; set; }

    /// <summary>Set by logout, reuse detection, deactivation or password reset.</summary>
    public DateTime? RevokedAt { get; set; }

    public User? User { get; set; }
}
```

In `backend/src/FacilityRealtime.Infrastructure/Persistence/AppDbContext.cs`, add after the `ScanRecords` property:

```csharp
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
```

and add at the end of `OnModelCreating`, after the `ScanRecord` block:

```csharp

        // RefreshToken (ADR facility-0014, amendment to facility-0006)
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
```

Do **not** add `HasDefaultValue(true)` to `IsActive`: EF would then treat `false` as "not set" on insert and the database default would silently turn a new deactivated account active. Existing rows get `true` through the migration edit in Step 5 instead.

Create `backend/src/FacilityRealtime.Infrastructure/Persistence/RefreshTokenRevocation.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Infrastructure.Persistence;

public static class RefreshTokenRevocation
{
    /// <summary>Revokes every still-valid token of one login: logout and reuse detection (ADR facility-0014, facility-0015).</summary>
    public static Task<int> RevokeSessionAsync(this AppDbContext db, Guid sessionId, DateTime nowUtc) =>
        db.RefreshTokens
            .Where(t => t.SessionId == sessionId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTime?)nowUtc));

    /// <summary>Revokes every still-valid token of a user: deactivation and password reset (ADR facility-0014).</summary>
    public static Task<int> RevokeUserSessionsAsync(this AppDbContext db, int userId, DateTime nowUtc) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTime?)nowUtc));
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo`
Expected: PASS, `Passed: 9`.

- [ ] **Step 5: Generate the MySQL migration and fix the default for existing rows**

The migration is generated against MySQL types but needs no running MySQL server.

```bash
dotnet tool install --global dotnet-ef --version 10.0.12
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add Phase2AuthRefreshTokens \
  --project backend/src/FacilityRealtime.Infrastructure \
  --startup-project backend/src/FacilityRealtime.Api \
  --output-dir Migrations
```

Expected: `Done. To undo this action, use 'ef migrations remove'`. If `dotnet tool install` reports the tool is already installed, continue.

Open `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_Phase2AuthRefreshTokens.cs`. In `Up`, find the `AddColumn<bool>` for `IsActive` on table `users` and change `defaultValue: false` to `defaultValue: true`, so accounts that already exist stay able to log in:

```csharp
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
```

Check the generated file contains the new table and nothing unexpected:

```bash
grep -n "CreateTable\|AddColumn\|DropColumn\|AlterColumn\|name: \"refresh_tokens\"\|IX_refresh_tokens" backend/src/FacilityRealtime.Infrastructure/Migrations/*_Phase2AuthRefreshTokens.cs
```

Expected, as measured on a copy of master `45e7be2` with Tasks 1-3 applied: in `Up`, one `AddColumn` named `IsActive` on `users` and one `CreateTable` named `refresh_tokens` with indexes `IX_refresh_tokens_SessionId`, `IX_refresh_tokens_TokenHash` and `IX_refresh_tokens_UserId`; in `Down`, the matching `DropTable` and `DropColumn`. An `AlterColumn` anywhere, or a `DropColumn` inside `Up`, would mean phase 1's `InitialCreate` had drifted from the model; keep such lines, because the model is the source of truth, and name them in the commit message.

Rebuild and re-run everything:

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 26, API tests 9.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Domain backend/src/FacilityRealtime.Infrastructure backend/tests/FacilityRealtime.ApiTests/Persistence
git commit -m "feat(auth): add users.IsActive and refresh_tokens table with revocation helpers (#15)"
```

---

### Task 4: Refresh token rules

The rotation decision is the heart of facility-0014, 0015 and 0016, so it is a pure function tested without a database or HTTP.

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Auth/RefreshTokenRules.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/RefreshTokenRulesTests.cs`

**Interfaces:**
- Consumes: `RefreshToken` (Task 3)
- Produces:
  - `enum RefreshDecision { Rotate, RotateWithinGrace, Reject, ReuseDetected }`
  - `RefreshTokenRules.Lifetime` (`TimeSpan`, 30 days) and `RefreshTokenRules.ReuseGrace` (`TimeSpan`, 30 seconds)
  - `RefreshDecision RefreshTokenRules.Decide(RefreshToken token, DateTime nowUtc)`
  - `string RefreshTokenRules.NewToken()` — 32 random bytes, base64url, 43 characters
  - `string RefreshTokenRules.Hash(string token)` — lowercase hex SHA-256, 64 characters

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/RefreshTokenRulesTests.cs`:

```csharp
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class RefreshTokenRulesTests
{
    private static readonly DateTime Issued = new(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);

    private static RefreshToken Token(DateTime? rotatedAt = null, DateTime? revokedAt = null) => new()
    {
        CreatedAt = Issued,
        ExpiresAt = Issued + RefreshTokenRules.Lifetime,
        RotatedAt = rotatedAt,
        RevokedAt = revokedAt,
    };

    [Fact]
    public void Lifetime_is_30_days_and_reuse_grace_is_30_seconds()
    {
        Assert.Equal(TimeSpan.FromDays(30), RefreshTokenRules.Lifetime);
        Assert.Equal(TimeSpan.FromSeconds(30), RefreshTokenRules.ReuseGrace);
    }

    [Fact]
    public void Unused_unexpired_token_rotates()
    {
        Assert.Equal(RefreshDecision.Rotate, RefreshTokenRules.Decide(Token(), Issued.AddDays(29)));
    }

    [Fact]
    public void Token_is_rejected_from_the_moment_it_expires()
    {
        var token = Token();

        Assert.Equal(RefreshDecision.Rotate, RefreshTokenRules.Decide(token, token.ExpiresAt.AddSeconds(-1)));
        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(token, token.ExpiresAt));
    }

    [Fact]
    public void Revoked_token_is_rejected()
    {
        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(Token(revokedAt: Issued.AddMinutes(1)), Issued.AddMinutes(2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(30)]
    public void Rotated_token_replayed_within_30_seconds_counts_as_another_tab(int secondsLater)
    {
        var rotatedAt = Issued.AddMinutes(10);

        Assert.Equal(RefreshDecision.RotateWithinGrace, RefreshTokenRules.Decide(Token(rotatedAt), rotatedAt.AddSeconds(secondsLater)));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(3600)]
    public void Rotated_token_replayed_after_30_seconds_is_reuse(int secondsLater)
    {
        var rotatedAt = Issued.AddMinutes(10);

        Assert.Equal(RefreshDecision.ReuseDetected, RefreshTokenRules.Decide(Token(rotatedAt), rotatedAt.AddSeconds(secondsLater)));
    }

    [Fact]
    public void Revocation_wins_over_the_grace_window()
    {
        var rotatedAt = Issued.AddMinutes(10);

        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(Token(rotatedAt, revokedAt: rotatedAt), rotatedAt.AddSeconds(5)));
    }

    [Fact]
    public void Expired_rotated_token_is_rejected_without_reuse_detection()
    {
        var token = Token(rotatedAt: Issued.AddMinutes(10));

        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(token, token.ExpiresAt.AddDays(1)));
    }

    [Fact]
    public void New_tokens_are_43_url_safe_characters_and_never_repeat()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenRules.NewToken()).ToList();

        Assert.All(tokens, t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
        Assert.Equal(100, tokens.Distinct().Count());
    }

    [Fact]
    public void Hash_is_deterministic_64_char_lowercase_hex_and_not_the_token()
    {
        var token = RefreshTokenRules.NewToken();
        var hash = RefreshTokenRules.Hash(token);

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, RefreshTokenRules.Hash(token));
        Assert.NotEqual(token, hash);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: FAIL at build with `The name 'RefreshTokenRules' does not exist in the current context`.

- [ ] **Step 3: Write the rules**

Create `backend/src/FacilityRealtime.Application/Auth/RefreshTokenRules.cs`:

```csharp
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Application.Auth;

public enum RefreshDecision
{
    /// <summary>First use: mark it rotated and issue a successor.</summary>
    Rotate,

    /// <summary>Already rotated at most 30 seconds ago: another tab refreshed at the same time, issue a successor too.</summary>
    RotateWithinGrace,

    /// <summary>Expired or revoked: refuse, change nothing.</summary>
    Reject,

    /// <summary>Already rotated more than 30 seconds ago: two parties hold it, revoke the whole login.</summary>
    ReuseDetected,
}

public static class RefreshTokenRules
{
    /// <summary>ADR facility-0016: 30 days from last use. Every rotation issues a fresh token with a fresh 30 days.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>ADR facility-0015: a rotated token replayed within this window is a concurrent refresh, not theft.</summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    public static RefreshDecision Decide(RefreshToken token, DateTime nowUtc)
    {
        if (token.RevokedAt is not null || nowUtc >= token.ExpiresAt)
        {
            return RefreshDecision.Reject;
        }

        if (token.RotatedAt is null)
        {
            return RefreshDecision.Rotate;
        }

        return nowUtc - token.RotatedAt.Value <= ReuseGrace
            ? RefreshDecision.RotateWithinGrace
            : RefreshDecision.ReuseDetected;
    }

    /// <summary>256 random bits (ADR facility-0014), base64url so it needs no escaping in a cookie.</summary>
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>What the database stores. A plain SHA-256 is enough for a 256-bit random value; no salt or stretching needed.</summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: PASS, `Passed: 39`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Auth/RefreshTokenRules.cs backend/tests/FacilityRealtime.UnitTests/RefreshTokenRulesTests.cs
git commit -m "feat(auth): add refresh token rotation, grace and reuse rules (#15)"
```

---

### Task 5: JWT access tokens and the login endpoint

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Auth/JwtSettings.cs`
- Create: `backend/src/FacilityRealtime.Application/Auth/IAccessTokenIssuer.cs`
- Create: `backend/src/FacilityRealtime.Api/Auth/AuthClaims.cs`
- Create: `backend/src/FacilityRealtime.Api/Auth/JwtKeys.cs`
- Create: `backend/src/FacilityRealtime.Api/Auth/JwtAccessTokenIssuer.cs`
- Create: `backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/AuthDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/AuthApi.cs`
- Replace: `backend/tests/FacilityRealtime.ApiTests/Auth/LoginTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/FacilityRealtime.Api.csproj` (package, `UserSecretsId`)
- Modify: `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs:7-9`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Modify: `backend/src/FacilityRealtime.Api/appsettings.json`

**Interfaces:**
- Consumes: `IPasswordHasher` (Task 2), `User.IsActive`, `RefreshToken`, `AppDbContext.RefreshTokens` (Task 3), `RefreshTokenRules` (Task 4), `TimeProvider` (Task 1)
- Produces:
  - `JwtSettings` (section `Jwt`): `Issuer`, `Audience`, `SigningKey`, `AccessTokenMinutes` (default 5), `RefreshCookieSecure` (default `false`)
  - `record AccessToken(string Token, DateTime ExpiresAtUtc)`; `IAccessTokenIssuer.Issue(User user)`
  - `AuthClaims.UserId = "sub"`, `AuthClaims.Username = "preferred_username"`, `AuthClaims.Name = "name"`, `AuthClaims.Role = "role"`
  - `AuthSetup.AdminOnly = "AdminOnly"` policy name; `IServiceCollection.AddFacilityAuth(IConfiguration)`
  - `record LoginRequest(string Username, string Password)`, `record AuthUserDto(int Id, string Username, string FullName, string Role)`, `record AuthResponse(string AccessToken, DateTime ExpiresAt, AuthUserDto User)`
  - `POST /api/auth/login` → `200 AuthResponse` plus cookie `facility_refresh` (HttpOnly, SameSite=Strict, Path=/api/auth), or `401`
  - `AuthEndpoints.RefreshCookieName = "facility_refresh"`; `IEndpointRouteBuilder.MapAuthEndpoints()`
  - Test helpers in `AuthApi`: `CreateApiClient(this FacilityApiFactory)`, `LoginAsync`, `ReadAuthAsync`, `RefreshSetCookieHeader`, `RefreshCookieValue`, `RefreshAsync`, `LogoutAsync`, `LoggedInAsync`, records `AuthUserModel`, `AuthResponseModel`

- [ ] **Step 1: Add the test helpers and write the failing login tests**

Create `backend/tests/FacilityRealtime.ApiTests/Infrastructure/AuthApi.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FacilityRealtime.ApiTests.Infrastructure;

public record AuthUserModel(int Id, string Username, string FullName, string Role);

public record AuthResponseModel(string AccessToken, DateTime ExpiresAt, AuthUserModel User);

public static class AuthApi
{
    public const string CookieName = "facility_refresh";

    /// <summary>Cookies are handled by hand so a test can replay an old refresh token on purpose.</summary>
    public static HttpClient CreateApiClient(this FacilityApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username = "somchai", string password = "password123") =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password });

    public static async Task<AuthResponseModel> ReadAuthAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AuthResponseModel>())!;

    /// <summary>The refresh cookie's Set-Cookie header, or null when the response did not touch the cookie.</summary>
    public static string? RefreshSetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.LastOrDefault(v => v.StartsWith(CookieName + "="))
            : null;

    /// <summary>The refresh token; "" when the response deleted the cookie; null when it did not touch it.</summary>
    public static string? RefreshCookieValue(HttpResponseMessage response) =>
        RefreshSetCookieHeader(response)?.Split(';')[0][(CookieName.Length + 1)..];

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? refreshToken) =>
        PostWithCookieAsync(client, "/api/auth/refresh", refreshToken);

    public static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? refreshToken) =>
        PostWithCookieAsync(client, "/api/auth/logout", refreshToken);

    /// <summary>Logs in and returns a client that already sends the access token.</summary>
    public static async Task<(HttpClient Client, AuthResponseModel Auth, string RefreshToken)> LoggedInAsync(
        FacilityApiFactory factory, string username = "somchai", string password = "password123")
    {
        var client = factory.CreateApiClient();
        var response = await LoginAsync(client, username, password);
        response.EnsureSuccessStatusCode();
        var auth = await ReadAuthAsync(response);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth, RefreshCookieValue(response)!);
    }

    private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string path, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={refreshToken}");
        }

        return client.SendAsync(request);
    }
}
```

Replace the whole of `backend/tests/FacilityRealtime.ApiTests/Auth/LoginTests.cs` with:

```csharp
using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Application.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FacilityRealtime.ApiTests.Auth;

public class LoginTests
{
    [Fact]
    public async Task Valid_login_returns_a_five_minute_access_token_and_the_user()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await AuthApi.ReadAuthAsync(response);
        Assert.Equal("somchai", body.User.Username);
        Assert.Equal("สมชาย ใจดี", body.User.FullName);
        Assert.Equal("cleaner", body.User.Role);
        var expected = factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        Assert.InRange(body.ExpiresAt.ToUniversalTime(), expected.AddSeconds(-1), expected.AddSeconds(1));
    }

    [Fact]
    public async Task Access_token_carries_user_id_username_name_and_role()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.LoginAsync(factory.CreateApiClient()));

        var jwt = new JsonWebToken(body.AccessToken);
        Assert.Equal(body.User.Id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("somchai", jwt.GetClaim("preferred_username").Value);
        Assert.Equal("สมชาย ใจดี", jwt.GetClaim("name").Value);
        Assert.Equal("cleaner", jwt.GetClaim("role").Value);
        Assert.Equal(TimeSpan.FromMinutes(5), jwt.ValidTo - jwt.IssuedAt);
    }

    [Fact]
    public async Task Admin_login_reports_the_admin_role()
    {
        using var factory = new FacilityApiFactory();

        var body = await AuthApi.ReadAuthAsync(await AuthApi.LoginAsync(factory.CreateApiClient(), "admin", "admin1234"));

        Assert.Equal("admin", body.User.Role);
    }

    [Fact]
    public async Task Login_sets_an_http_only_strict_refresh_cookie_scoped_to_auth_routes()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        var header = AuthApi.RefreshSetCookieHeader(response)!.ToLowerInvariant();
        Assert.Contains("httponly", header);
        Assert.Contains("samesite=strict", header);
        Assert.Contains("path=/api/auth", header);
        Assert.Contains("expires=", header);
        Assert.DoesNotContain("secure", header); // ADR facility-0013: phones reach the POC over plain HTTP
    }

    [Fact]
    public async Task Only_the_hash_of_the_refresh_token_is_stored()
    {
        using var factory = new FacilityApiFactory();
        var cookie = AuthApi.RefreshCookieValue(await AuthApi.LoginAsync(factory.CreateApiClient()))!;
        var stored = new List<string>();

        await factory.WithDbAsync(async db => stored = await db.RefreshTokens.Select(t => t.TokenHash).ToListAsync());

        Assert.Equal(43, cookie.Length);
        Assert.Equal(new[] { RefreshTokenRules.Hash(cookie) }, stored);
    }

    [Theory]
    [InlineData("somchai", "wrong-password")]
    [InlineData("nobody", "password123")]
    public async Task Bad_credentials_are_401_and_set_no_cookie(string username, string password)
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LoginAsync(factory.CreateApiClient(), username, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(AuthApi.RefreshSetCookieHeader(response));
    }

    [Fact]
    public async Task Deactivated_account_cannot_log_in()
    {
        using var factory = new FacilityApiFactory();
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Username == "somchai")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await AuthApi.LoginAsync(factory.CreateApiClient());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void App_refuses_to_start_without_a_signing_key()
    {
        using var factory = new FacilityApiFactory(signingKey: "");

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("Jwt:SigningKey", error.ToString());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo`
Expected: FAIL. `Microsoft.IdentityModel.JsonWebTokens` does not resolve until the JwtBearer package is added; after that, the login tests fail because the old endpoint returns no `accessToken` and sets no cookie.

- [ ] **Step 3: Add the package and a user-secrets store for local runs**

```bash
dotnet add backend/src/FacilityRealtime.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.12
dotnet user-secrets init --project backend/src/FacilityRealtime.Api
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/FacilityRealtime.Api
```

Expected: the csproj gains the package reference and a `<UserSecretsId>`. The key itself is written to `~/.microsoft/usersecrets/<id>/secrets.json`, outside the repo. Each developer machine runs the last command once.

- [ ] **Step 4: Write settings, claims, key, issuer and auth setup**

Create `backend/src/FacilityRealtime.Application/Auth/JwtSettings.cs`:

```csharp
namespace FacilityRealtime.Application.Auth;

/// <summary>The "Jwt" config section. SigningKey is never committed: set it with dotnet user-secrets (ADR facility-0011).</summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "facility-realtime";
    public string Audience { get; set; } = "facility-realtime-web";
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>ADR facility-0012.</summary>
    public int AccessTokenMinutes { get; set; } = 5;

    /// <summary>ADR facility-0013: false in the POC because phones use plain HTTP; true on any HTTPS deployment.</summary>
    public bool RefreshCookieSecure { get; set; }
}
```

Create `backend/src/FacilityRealtime.Application/Auth/IAccessTokenIssuer.cs`:

```csharp
using FacilityRealtime.Domain.Entities;

namespace FacilityRealtime.Application.Auth;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
```

Create `backend/src/FacilityRealtime.Api/Auth/AuthClaims.cs`:

```csharp
namespace FacilityRealtime.Api.Auth;

/// <summary>Claim names inside the access token. Inbound claim mapping is off, so these are also what handlers read.</summary>
public static class AuthClaims
{
    public const string UserId = "sub";
    public const string Username = "preferred_username";
    public const string Name = "name";
    public const string Role = "role";
}
```

Create `backend/src/FacilityRealtime.Api/Auth/JwtKeys.cs`:

```csharp
using System.Text;
using FacilityRealtime.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace FacilityRealtime.Api.Auth;

public static class JwtKeys
{
    public static SymmetricSecurityKey SigningKey(JwtSettings settings) =>
        new(Encoding.UTF8.GetBytes(settings.SigningKey));
}
```

Create `backend/src/FacilityRealtime.Api/Auth/JwtAccessTokenIssuer.cs`:

```csharp
using System.Security.Claims;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FacilityRealtime.Api.Auth;

public sealed class JwtAccessTokenIssuer(IOptions<JwtSettings> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(AuthClaims.UserId, user.Id.ToString()),
                new Claim(AuthClaims.Username, user.Username),
                new Claim(AuthClaims.Name, user.FullName),
                new Claim(AuthClaims.Role, user.Role),
            }),
            SigningCredentials = new SigningCredentials(JwtKeys.SigningKey(settings), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}
```

Create `backend/src/FacilityRealtime.Api/Auth/AuthSetup.cs`:

```csharp
using System.Text;
using FacilityRealtime.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FacilityRealtime.Api.Auth;

public static class AuthSetup
{
    /// <summary>ADR facility-0010: 401 when not logged in, 403 when logged in without the admin role.</summary>
    public const string AdminOnly = "AdminOnly";

    public static IServiceCollection AddFacilityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                s => Encoding.UTF8.GetByteCount(s.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes. Set it once per machine with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"$(openssl rand -base64 48)\" --project backend/src/FacilityRealtime.Api")
            .Validate(s => s.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured lazily from validated settings, so building the host (and `dotnet ef`) never needs the key
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearer, jwt) =>
            {
                var settings = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = JwtKeys.SigningKey(settings),
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // ADR facility-0012: the .NET default of 5 minutes would double the token's life
                    NameClaimType = AuthClaims.Name,
                    RoleClaimType = AuthClaims.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // ADR facility-0011: browsers cannot set headers on a WebSocket, so SignalR sends the token
                        // in the query string. Accept that only for hub paths.
                        var queryToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(queryToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = queryToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole("admin"));

        return services;
    }
}
```

- [ ] **Step 5: Write the DTOs and the login endpoint**

Create `backend/src/FacilityRealtime.Api/DTOs/AuthDtos.cs`:

```csharp
using System;

namespace FacilityRealtime.Api.DTOs;

public record LoginRequest(string Username, string Password);

public record AuthUserDto(int Id, string Username, string FullName, string Role);

/// <summary>Returned by login and refresh. The refresh token itself only ever travels in the HttpOnly cookie.</summary>
public record AuthResponse(string AccessToken, DateTime ExpiresAt, AuthUserDto User);
```

In `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs`, delete lines 7-9:

```csharp
public record LoginRequest(string Username, string Password);

public record LoginResponse(int Id, string Username, string FullName, string Role, string Token);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs`:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FacilityRealtime.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshCookieName = "facility_refresh";

    /// <summary>ADR facility-0013: the browser sends the refresh cookie to these routes and nowhere else.</summary>
    private const string RefreshCookiePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        AppDbContext db,
        IPasswordHasher hasher,
        IAccessTokenIssuer issuer,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var refresh = AddRefreshToken(db, user.Id, sessionId: Guid.NewGuid(), now);
        await db.SaveChangesAsync();

        WriteRefreshCookie(http, refresh.Token, refresh.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(user, issuer.Issue(user)));
    }

    private static (string Token, DateTime ExpiresAt) AddRefreshToken(AppDbContext db, int userId, Guid sessionId, DateTime nowUtc)
    {
        var token = RefreshTokenRules.NewToken();
        var expiresAt = nowUtc + RefreshTokenRules.Lifetime;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = RefreshTokenRules.Hash(token),
            CreatedAt = nowUtc,
            ExpiresAt = expiresAt,
        });

        return (token, expiresAt);
    }

    private static CookieOptions RefreshCookieOptions(JwtSettings settings) => new()
    {
        HttpOnly = true,
        Secure = settings.RefreshCookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
    };

    private static void WriteRefreshCookie(HttpContext http, string token, DateTime expiresAtUtc, JwtSettings settings)
    {
        var options = RefreshCookieOptions(settings);
        options.Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
        http.Response.Cookies.Append(RefreshCookieName, token, options);
    }

    private static AuthResponse ToResponse(User user, AccessToken access) =>
        new(access.Token, access.ExpiresAtUtc, new AuthUserDto(user.Id, user.Username, user.FullName, user.Role));
}
```

- [ ] **Step 6: Wire authentication into Program and config**

In `backend/src/FacilityRealtime.Api/Program.cs`:

1. Add usings `using FacilityRealtime.Api.Auth;` and `using FacilityRealtime.Api.Endpoints;`.
2. After `builder.Services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());` add:

```csharp
builder.Services.AddFacilityAuth(builder.Configuration);
```

3. Replace `app.UseCors("AllowAll");` with:

```csharp
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
```

4. Replace the whole login block, from `// Auth: Login` through its closing `});`:

```csharp
// Auth: Login
app.MapPost("/api/auth/login", async (LoginRequest req, AppDbContext db, IPasswordHasher hasher) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
    if (user == null || !hasher.Verify(req.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var token = Guid.NewGuid().ToString("N");
    return Results.Ok(new LoginResponse(user.Id, user.Username, user.FullName, user.Role, token));
});
```

with:

```csharp
// Auth: login, refresh, logout (ADR facility-0011..0016)
app.MapAuthEndpoints();
```

Replace the whole of `backend/src/FacilityRealtime.Api/appsettings.json` with (no signing key here; it comes from user-secrets):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;"
  },
  "WorkingHours": {
    "Start": "08:00",
    "End": "17:00"
  },
  "Jwt": {
    "Issuer": "facility-realtime",
    "Audience": "facility-realtime-web",
    "AccessTokenMinutes": 5,
    "RefreshCookieSecure": false
  }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 39, API tests 16.

Confirm no signing key is committed:

```bash
git grep -n "SigningKey\"" -- backend/src ':!*.cs' || echo "no key in committed config"
```

Expected: `no key in committed config`.

- [ ] **Step 8: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Auth backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests
git commit -m "feat(auth): issue 5-minute JWTs and an HttpOnly refresh cookie on login (#15)"
```

---

### Task 6: Refresh and logout endpoints

**Files:**
- Replace: `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Auth/RefreshTests.cs`

**Interfaces:**
- Consumes: `RefreshTokenRules.Decide` and `RefreshDecision` (Task 4); `RevokeSessionAsync`, `RevokeUserSessionsAsync` (Task 3); `AuthApi` helpers and `FacilityApiFactory.Clock` (Tasks 1, 5)
- Produces:
  - `POST /api/auth/refresh` — reads cookie `facility_refresh`; `200 AuthResponse` plus a rotated cookie, or `401` plus a deleted cookie
  - `POST /api/auth/logout` — revokes the presented token's login; always `204` plus a deleted cookie

- [ ] **Step 1: Write the failing refresh and logout tests**

Create `backend/tests/FacilityRealtime.ApiTests/Auth/RefreshTests.cs`:

```csharp
using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FacilityRealtime.ApiTests.Auth;

public class RefreshTests
{
    [Fact]
    public async Task Refresh_rotates_the_cookie_and_returns_a_new_access_token()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);

        var response = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = AuthApi.RefreshCookieValue(response);
        Assert.False(string.IsNullOrEmpty(second));
        Assert.NotEqual(first, second);
        Assert.Equal("somchai", (await AuthApi.ReadAuthAsync(response)).User.Username);
    }

    [Fact]
    public async Task Rotated_token_replayed_within_30_seconds_still_refreshes()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.RefreshAsync(client, first);

        factory.Clock.Advance(TimeSpan.FromSeconds(20)); // the Dashboard tab and the scan tab refreshed together
        var replay = await AuthApi.RefreshAsync(client, first);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }

    [Fact]
    public async Task Rotated_token_replayed_after_30_seconds_revokes_the_whole_login()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        var second = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(client, first))!;

        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        var replay = await AuthApi.RefreshAsync(client, first);
        var legitimate = await AuthApi.RefreshAsync(client, second);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, legitimate.StatusCode);
    }

    [Fact]
    public async Task Reuse_detection_leaves_the_same_users_other_logins_alone()
    {
        using var factory = new FacilityApiFactory();
        var (phone, _, phoneToken) = await AuthApi.LoggedInAsync(factory);
        var (_, _, officeToken) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.RefreshAsync(phone, phoneToken);

        factory.Clock.Advance(TimeSpan.FromSeconds(31));
        await AuthApi.RefreshAsync(phone, phoneToken);
        var office = await AuthApi.RefreshAsync(phone, officeToken);

        Assert.Equal(HttpStatusCode.OK, office.StatusCode);
    }

    [Fact]
    public async Task Token_unused_for_30_days_expires()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, token) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.Advance(TimeSpan.FromDays(30));
        var response = await AuthApi.RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Each_refresh_gives_the_session_another_30_days()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, token) = await AuthApi.LoggedInAsync(factory);

        factory.Clock.Advance(TimeSpan.FromDays(29));
        var next = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(client, token))!;
        factory.Clock.Advance(TimeSpan.FromDays(29));
        var response = await AuthApi.RefreshAsync(client, next);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_account_cannot_refresh_and_all_its_sessions_are_revoked()
    {
        using var factory = new FacilityApiFactory();
        var (client, auth, token) = await AuthApi.LoggedInAsync(factory);
        await AuthApi.LoggedInAsync(factory); // a second login on another device
        await SetActiveAsync(factory, auth.User.Id, false);

        var whileInactive = await AuthApi.RefreshAsync(client, token);
        await SetActiveAsync(factory, auth.User.Id, true);
        var afterReactivation = await AuthApi.RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, whileInactive.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReactivation.StatusCode);
        var liveTokens = -1;
        await factory.WithDbAsync(async db => liveTokens = await db.RefreshTokens.CountAsync(t => t.UserId == auth.User.Id && t.RevokedAt == null));
        Assert.Equal(0, liveTokens);
    }

    [Fact]
    public async Task Role_change_takes_effect_on_the_next_refresh()
    {
        using var factory = new FacilityApiFactory();
        var (client, auth, token) = await AuthApi.LoggedInAsync(factory);
        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == auth.User.Id)).Role = "admin";
            await db.SaveChangesAsync();
        });

        var refreshed = await AuthApi.ReadAuthAsync(await AuthApi.RefreshAsync(client, token));

        Assert.Equal("admin", refreshed.User.Role);
        Assert.Equal("admin", new JsonWebToken(refreshed.AccessToken).GetClaim("role").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token-this-server-issued")]
    public async Task Missing_or_unknown_cookie_is_401_and_clears_the_cookie(string? cookie)
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.RefreshAsync(factory.CreateApiClient(), cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("", AuthApi.RefreshCookieValue(response));
    }

    [Fact]
    public async Task Logout_revokes_that_login_and_deletes_the_cookie()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, first) = await AuthApi.LoggedInAsync(factory);
        var second = AuthApi.RefreshCookieValue(await AuthApi.RefreshAsync(client, first))!;

        var logout = await AuthApi.LogoutAsync(client, second);
        var afterLogout = await AuthApi.RefreshAsync(client, second);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal("", AuthApi.RefreshCookieValue(logout));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_cookie_still_succeeds()
    {
        using var factory = new FacilityApiFactory();

        var response = await AuthApi.LogoutAsync(factory.CreateApiClient(), null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task SetActiveAsync(FacilityApiFactory factory, int userId, bool isActive) =>
        factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == userId)).IsActive = isActive;
            await db.SaveChangesAsync();
        });
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo --filter "FullyQualifiedName~RefreshTests"`
Expected: FAIL. Every test gets `404 NotFound` because `/api/auth/refresh` and `/api/auth/logout` are not mapped.

- [ ] **Step 3: Replace the auth endpoints file with login, refresh and logout**

Replace the whole of `backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs` with:

```csharp
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FacilityRealtime.Api.Endpoints;

public static class AuthEndpoints
{
    public const string RefreshCookieName = "facility_refresh";

    /// <summary>ADR facility-0013: the browser sends the refresh cookie to these routes and nowhere else.</summary>
    private const string RefreshCookiePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/refresh", RefreshAsync);
        auth.MapPost("/logout", LogoutAsync);
        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        AppDbContext db,
        IPasswordHasher hasher,
        IAccessTokenIssuer issuer,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var refresh = AddRefreshToken(db, user.Id, sessionId: Guid.NewGuid(), now);
        await db.SaveChangesAsync();

        WriteRefreshCookie(http, refresh.Token, refresh.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(user, issuer.Issue(user)));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext http,
        AppDbContext db,
        IAccessTokenIssuer issuer,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var presented = http.Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(presented))
        {
            return Unauthorized(http, jwt.Value);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var hash = RefreshTokenRules.Hash(presented);
        var stored = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored?.User is null)
        {
            return Unauthorized(http, jwt.Value);
        }

        switch (RefreshTokenRules.Decide(stored, now))
        {
            case RefreshDecision.Reject:
                return Unauthorized(http, jwt.Value);

            case RefreshDecision.ReuseDetected:
                // ADR facility-0015: two parties hold this token and the server cannot tell which is the owner
                await db.RevokeSessionAsync(stored.SessionId, now);
                return Unauthorized(http, jwt.Value);
        }

        // ADR facility-0012: the account is re-read on every refresh, so deactivation and role changes land within 5 minutes
        if (!stored.User.IsActive)
        {
            await db.RevokeUserSessionsAsync(stored.UserId, now);
            return Unauthorized(http, jwt.Value);
        }

        // Rotate (facility-0014). Within the grace window the first rotation time is kept, so the window never extends itself.
        stored.RotatedAt ??= now;
        var next = AddRefreshToken(db, stored.UserId, stored.SessionId, now);
        await db.SaveChangesAsync();

        WriteRefreshCookie(http, next.Token, next.ExpiresAt, jwt.Value);
        return Results.Ok(ToResponse(stored.User, issuer.Issue(stored.User)));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext http,
        AppDbContext db,
        TimeProvider clock,
        IOptions<JwtSettings> jwt)
    {
        var presented = http.Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(presented))
        {
            var hash = RefreshTokenRules.Hash(presented);
            var sessionId = await db.RefreshTokens
                .Where(t => t.TokenHash == hash)
                .Select(t => (Guid?)t.SessionId)
                .FirstOrDefaultAsync();

            if (sessionId is not null)
            {
                await db.RevokeSessionAsync(sessionId.Value, clock.GetUtcNow().UtcDateTime);
            }
        }

        http.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(jwt.Value));
        return Results.NoContent();
    }

    private static IResult Unauthorized(HttpContext http, JwtSettings settings)
    {
        http.Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(settings));
        return Results.Unauthorized();
    }

    private static (string Token, DateTime ExpiresAt) AddRefreshToken(AppDbContext db, int userId, Guid sessionId, DateTime nowUtc)
    {
        var token = RefreshTokenRules.NewToken();
        var expiresAt = nowUtc + RefreshTokenRules.Lifetime;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = RefreshTokenRules.Hash(token),
            CreatedAt = nowUtc,
            ExpiresAt = expiresAt,
        });

        return (token, expiresAt);
    }

    private static CookieOptions RefreshCookieOptions(JwtSettings settings) => new()
    {
        HttpOnly = true,
        Secure = settings.RefreshCookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
    };

    private static void WriteRefreshCookie(HttpContext http, string token, DateTime expiresAtUtc, JwtSettings settings)
    {
        var options = RefreshCookieOptions(settings);
        options.Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
        http.Response.Cookies.Append(RefreshCookieName, token, options);
    }

    private static AuthResponse ToResponse(User user, AccessToken access) =>
        new(access.Token, access.ExpiresAtUtc, new AuthUserDto(user.Id, user.Username, user.FullName, user.Role));
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 39, API tests 28.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api/Endpoints/AuthEndpoints.cs backend/tests/FacilityRealtime.ApiTests/Auth/RefreshTests.cs
git commit -m "feat(auth): rotate refresh tokens with 30s grace, reuse detection and logout (#15)"
```

---

### Task 7: Login-required dashboard, scan and hub; scanner from the token

**Files:**
- Create: `backend/tests/FacilityRealtime.ApiTests/Auth/ProtectedEndpointTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (three handlers)
- Modify: `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs` (`CreateScanRecordRequest`)
- Modify: `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs`

**Interfaces:**
- Consumes: `AuthClaims.UserId` (Task 5), JwtBearer with `/hubs` query token (Task 5), `AuthApi.LoggedInAsync` (Task 5)
- Produces:
  - `GET /api/service-points`, `GET /api/service-points/by-token/{token}`, `POST /api/scan-records` answer `401` without a valid access token
  - `record CreateScanRecordRequest(string QrToken, ScanStatus Status, List<string>? IssueTags, string? Notes)` — no `UserId`
  - `/hubs/scan` requires a valid access token, taken from the `access_token` query value

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.ApiTests/Auth/ProtectedEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Auth;

public class ProtectedEndpointTests
{
    private const string QrToken = "token-restroom-m1";

    [Theory]
    [InlineData("/api/service-points")]
    [InlineData("/api/service-points/by-token/" + QrToken)]
    public async Task Dashboard_and_point_lookup_require_login(string path)
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Any_logged_in_account_can_read_the_dashboard_and_a_point()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var list = await client.GetAsync("/api/service-points");
        var point = await client.GetAsync("/api/service-points/by-token/" + QrToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, point.StatusCode);
    }

    [Fact]
    public async Task Scanning_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Scan_is_recorded_as_the_logged_in_user_even_if_the_body_names_someone_else()
    {
        using var factory = new FacilityApiFactory();
        var (client, auth, _) = await AuthApi.LoggedInAsync(factory);
        var adminId = 0;
        await factory.WithDbAsync(async db => adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id);

        // Master's contract accepted userId in the body; an old client or curl may still send it
        var response = await client.PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, userId = adminId, status = "Normal" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recordedUserId = 0;
        await factory.WithDbAsync(async db => recordedUserId = (await db.ScanRecords.OrderByDescending(r => r.Id).FirstAsync()).UserId);
        Assert.Equal(auth.User.Id, recordedUserId);
    }

    [Fact]
    public async Task Admin_accounts_can_scan_too()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = await AuthApi.LoggedInAsync(factory, "admin", "admin1234");

        var response = await client.PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Hub_negotiation_requires_login()
    {
        using var factory = new FacilityApiFactory();

        var response = await factory.CreateApiClient().PostAsync("/hubs/scan/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hub_accepts_the_access_token_from_the_query_string()
    {
        using var factory = new FacilityApiFactory();
        var (_, auth, _) = await AuthApi.LoggedInAsync(factory);

        var response = await factory.CreateApiClient().PostAsync($"/hubs/scan/negotiate?negotiateVersion=1&access_token={auth.AccessToken}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Query_string_token_is_ignored_outside_hubs()
    {
        using var factory = new FacilityApiFactory();
        var (_, auth, _) = await AuthApi.LoggedInAsync(factory);

        var response = await factory.CreateApiClient().GetAsync($"/api/service-points?access_token={auth.AccessToken}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo --filter "FullyQualifiedName~ProtectedEndpointTests"`
Expected: FAIL. The anonymous requests get `200` instead of `401`, and the body-without-`userId` scans get `400 User ID '0' not found`.

- [ ] **Step 3: Require login and read the scanner from the token**

In `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs`, replace:

```csharp
public record CreateScanRecordRequest(
    string QrToken,
    int UserId,
    ScanStatus Status,
```

with:

```csharp
/// <summary>ADR facility-0017: no UserId. The scanner is the account in the access token.</summary>
public record CreateScanRecordRequest(
    string QrToken,
    ScanStatus Status,
```

In `backend/src/FacilityRealtime.Api/Program.cs`:

1. Add `using System.Security.Claims;` to the usings.
2. End of the `/api/service-points` handler — replace:

```csharp
    return Results.Ok(result);
});
```

with:

```csharp
    return Results.Ok(result);
}).RequireAuthorization(); // ADR facility-0018: any logged-in account
```

3. End of the `/api/service-points/by-token/{token}` handler — replace:

```csharp
    return Results.Ok(dto);
});
```

with:

```csharp
    return Results.Ok(dto);
}).RequireAuthorization(); // ADR facility-0018: the scan page is only reachable logged in
```

4. Start of the `/api/scan-records` handler — replace (note the trailing spaces after `req,` and `db,` on master):

```csharp
app.MapPost("/api/scan-records", async (
    CreateScanRecordRequest req, 
    AppDbContext db, 
    IHubContext<ScanHub> hubContext,
    WorkingHours workingHours) =>
{
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == req.QrToken && p.IsActive);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Invalid QR token '{req.QrToken}'." });
    }

    var user = await db.Users.FindAsync(req.UserId);
    if (user == null)
    {
        return Results.BadRequest(new { message = $"User ID '{req.UserId}' not found." });
    }
```

with:

```csharp
app.MapPost("/api/scan-records", async (
    CreateScanRecordRequest req,
    ClaimsPrincipal principal,
    AppDbContext db,
    IHubContext<ScanHub> hubContext,
    WorkingHours workingHours) =>
{
    // ADR facility-0017: the scanner is whoever the access token belongs to, never a field in the body
    if (!int.TryParse(principal.FindFirstValue(AuthClaims.UserId), out var userId))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FindAsync(userId);
    if (user == null)
    {
        return Results.Unauthorized();
    }

    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == req.QrToken && p.IsActive);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Invalid QR token '{req.QrToken}'." });
    }
```

5. End of the `/api/scan-records` handler — replace:

```csharp
        scanRecord.ScannedAt
    ));
});
```

with:

```csharp
        scanRecord.ScannedAt
    ));
}).RequireAuthorization(); // ADR facility-0017: cleaner and admin accounts may both scan
```

Replace the whole of `backend/src/FacilityRealtime.Api/Hubs/ScanHub.cs` with:

```csharp
using System.Threading.Tasks;
using FacilityRealtime.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FacilityRealtime.Api.Hubs;

/// <summary>ADR facility-0018: only logged-in accounts receive the live feed, so Clients.All needs no groups.</summary>
[Authorize]
public class ScanHub : Hub
{
    // Clients connect to /hubs/scan and listen for "ScanRecorded" events
    public async Task NotifyPointUpdated(ServicePointStatusDto point)
    {
        await Clients.All.SendAsync("ScanRecorded", point);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 39, API tests 37.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests/Auth/ProtectedEndpointTests.cs
git commit -m "feat(auth): require login for dashboard, scan and hub; take scanner from the JWT (#18)"
```

---

### Task 8: Frontend session module (in-memory access token, single-flight refresh)

The browser side of facility-0013 and 0015: keep the access token in a module variable, restore it from the refresh cookie on page load, refresh once when several requests hit 401 together, and retry each request once. This task adds the module and its tests; Task 9 switches the app over to it.

**Files:**
- Create: `frontend/src/services/authSession.ts`
- Create: `frontend/src/services/authSession.test.ts`
- Modify: `frontend/src/types/index.ts` (add `AuthUser`, `AuthResponse`)
- Modify: `frontend/package.json` (Vitest, `test` script)
- Modify: `frontend/vite.config.ts` (Vitest config)

**Interfaces:**
- Consumes: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout` and the `AuthResponse` JSON shape (Tasks 5, 6)
- Produces (all exported from `frontend/src/services/authSession.ts`):
  - `login(username: string, password: string): Promise<AuthUser>` — throws `Error('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง')` on failure
  - `refreshSession(): Promise<AuthResponse | null>` — single-flight; `null` means logged out
  - `logout(): Promise<void>` — always clears the in-memory session
  - `getValidAccessToken(now?: number): Promise<string | null>` — refreshes first when 30 seconds or less remain
  - `apiFetch(input: string, init?: RequestInit): Promise<Response>` — attaches `Authorization: Bearer`, refreshes and retries once on 401
  - `getCurrentUser(): AuthUser | null`, `onSessionChange(listener: (user: AuthUser | null) => void): () => void`, `resetSessionForTests(): void`
  - `AuthUser { id: number; username: string; fullName: string; role: string }` and `AuthResponse { accessToken: string; expiresAt: string; user: AuthUser }` in `frontend/src/types/index.ts`

- [ ] **Step 1: Install Vitest and configure it**

Dependencies are not installed in a fresh clone; install them first, then add Vitest:

```bash
npm --prefix frontend install
npm --prefix frontend install --save-dev vitest@5.0.0
```

Expected: `frontend/package.json` gains `"vitest": "^5.0.0"` under `devDependencies`.

In `frontend/package.json`, add a `test` script so `scripts` reads:

```json
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "lint": "oxlint",
    "preview": "vite preview",
    "test": "vitest run"
  },
```

Replace the whole of `frontend/vite.config.ts` with:

```ts
/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0', // Allow access from mobile devices on local WiFi
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5001',
        ws: true
      }
    }
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts']
  }
});
```

- [ ] **Step 2: Write the failing tests**

Create `frontend/src/services/authSession.test.ts`:

```ts
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as authSession from './authSession';

const user = { id: 1, username: 'somchai', fullName: 'สมชาย ใจดี', role: 'cleaner' };

function authBody(accessToken: string, expiresInMs = 5 * 60_000) {
  return { accessToken, expiresAt: new Date(Date.now() + expiresInMs).toISOString(), user };
}

function jsonResponse(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function authHeader(init: RequestInit | undefined): string | null {
  return new Headers(init?.headers).get('Authorization');
}

const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>();
const callsTo = (url: string) => fetchMock.mock.calls.filter(([calledUrl]) => calledUrl === url).length;

beforeEach(() => {
  authSession.resetSessionForTests();
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('authSession', () => {
  it('keeps the access token in memory and attaches it to API calls', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/login' ? jsonResponse(200, authBody('A')) : jsonResponse(200, []),
    );

    await authSession.login('somchai', 'password123');
    await authSession.apiFetch('/api/service-points');

    const [, init] = fetchMock.mock.calls[1];
    expect(authHeader(init)).toBe('Bearer A');
    expect(authSession.getCurrentUser()).toEqual(user);
  });

  it('rejects a failed login with a Thai message and stores nothing', async () => {
    fetchMock.mockResolvedValue(jsonResponse(401));

    await expect(authSession.login('somchai', 'wrong')).rejects.toThrow('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
    expect(authSession.getCurrentUser()).toBeNull();
  });

  it('restores a session from the refresh cookie on page load', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, authBody('R')));

    const restored = await authSession.refreshSession();

    expect(restored?.accessToken).toBe('R');
    expect(authSession.getCurrentUser()?.username).toBe('somchai');
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  });

  it('shares one refresh between concurrent 401s, then retries each call with the new token', async () => {
    fetchMock.mockImplementation(async (url, init) => {
      if (url === '/api/auth/login') return jsonResponse(200, authBody('A'));
      if (url === '/api/auth/refresh') {
        await new Promise((resolve) => setTimeout(resolve, 5));
        return jsonResponse(200, authBody('B'));
      }
      return authHeader(init) === 'Bearer B' ? jsonResponse(200, []) : jsonResponse(401);
    });
    await authSession.login('somchai', 'password123');

    const [first, second] = await Promise.all([authSession.apiFetch('/api/a'), authSession.apiFetch('/api/b')]);

    expect(first.status).toBe(200);
    expect(second.status).toBe(200);
    expect(callsTo('/api/auth/refresh')).toBe(1);
  });

  it('clears the session, tells listeners and returns the 401 when refresh fails', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/login' ? jsonResponse(200, authBody('A')) : jsonResponse(401),
    );
    await authSession.login('somchai', 'password123');
    const listener = vi.fn();
    authSession.onSessionChange(listener);

    const response = await authSession.apiFetch('/api/service-points');

    expect(response.status).toBe(401);
    expect(authSession.getCurrentUser()).toBeNull();
    expect(listener).toHaveBeenLastCalledWith(null);
  });

  it('refreshes before sending when the token has 30 seconds or less left', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/login' ? jsonResponse(200, authBody('A', 20_000)) : jsonResponse(200, authBody('B')),
    );
    await authSession.login('somchai', 'password123');

    expect(await authSession.getValidAccessToken()).toBe('B');
    expect(await authSession.getValidAccessToken()).toBe('B');
    expect(callsTo('/api/auth/refresh')).toBe(1);
  });

  it('sends no Authorization header when nobody is logged in', async () => {
    fetchMock.mockResolvedValue(jsonResponse(401));

    const response = await authSession.apiFetch('/api/service-points');

    const apiCall = fetchMock.mock.calls.find(([url]) => url === '/api/service-points');
    expect(response.status).toBe(401);
    expect(authHeader(apiCall?.[1])).toBeNull();
  });

  it('forgets the session on logout even when the request fails', async () => {
    fetchMock.mockImplementation(async (url) => {
      if (url === '/api/auth/login') return jsonResponse(200, authBody('A'));
      throw new TypeError('Failed to fetch');
    });
    await authSession.login('somchai', 'password123');

    await authSession.logout();

    expect(authSession.getCurrentUser()).toBeNull();
  });
});
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `npm --prefix frontend test`
Expected: FAIL with `Failed to resolve import "./authSession"`.

- [ ] **Step 4: Add the types and write the module**

Append to `frontend/src/types/index.ts` (leave `UserSession` in place; Task 9 removes it together with its last users):

```ts

/** The account in a login or refresh response (ADR facility-0011). */
export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: string;
}

/** Body of POST /api/auth/login and /api/auth/refresh. The refresh token is never here: it is an HttpOnly cookie. */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}
```

Create `frontend/src/services/authSession.ts`:

```ts
import type { AuthResponse, AuthUser } from '../types';

type SessionListener = (user: AuthUser | null) => void;

/** Refresh when less than this is left, so a request never leaves with a token about to expire. */
const REFRESH_MARGIN_MS = 30_000;

// ADR facility-0013: the access token lives only in page memory. The refresh token is an HttpOnly cookie JS never sees.
let session: AuthResponse | null = null;
let inflightRefresh: Promise<AuthResponse | null> | null = null;
const listeners = new Set<SessionListener>();

function setSession(next: AuthResponse | null): void {
  session = next;
  const user = next?.user ?? null;
  listeners.forEach((listener) => listener(user));
}

export function onSessionChange(listener: SessionListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getCurrentUser(): AuthUser | null {
  return session?.user ?? null;
}

export async function login(username: string, password: string): Promise<AuthUser> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'same-origin',
    body: JSON.stringify({ username, password }),
  });
  if (!res.ok) {
    throw new Error('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  }
  const body = (await res.json()) as AuthResponse;
  setSession(body);
  return body.user;
}

/** ADR facility-0015: one refresh at a time. Concurrent callers share the same request. */
export function refreshSession(): Promise<AuthResponse | null> {
  if (!inflightRefresh) {
    inflightRefresh = runRefresh().finally(() => {
      inflightRefresh = null;
    });
  }
  return inflightRefresh;
}

async function runRefresh(): Promise<AuthResponse | null> {
  let res: Response;
  try {
    res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  } catch {
    return session; // network error: keep the current session rather than logging the user out
  }
  const next = res.ok ? ((await res.json()) as AuthResponse) : null;
  setSession(next);
  return next;
}

export async function logout(): Promise<void> {
  try {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' });
  } catch {
    // offline: this tab forgets the session now; the server revokes it on the next successful logout or after 30 days
  } finally {
    setSession(null);
  }
}

export async function getValidAccessToken(now: number = Date.now()): Promise<string | null> {
  if (session && Date.parse(session.expiresAt) - now > REFRESH_MARGIN_MS) {
    return session.accessToken;
  }
  const refreshed = await refreshSession();
  return refreshed?.accessToken ?? null;
}

/** fetch with the access token attached. On 401 it refreshes once and retries once. */
export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const send = (token: string | null): Promise<Response> => {
    const headers = new Headers(init.headers);
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    return fetch(input, { ...init, headers, credentials: 'same-origin' });
  };

  const first = await send(await getValidAccessToken());
  if (first.status !== 401) {
    return first;
  }
  const refreshed = await refreshSession();
  return refreshed ? send(refreshed.accessToken) : first;
}

export function resetSessionForTests(): void {
  session = null;
  inflightRefresh = null;
  listeners.clear();
}
```

- [ ] **Step 5: Run the tests, the type check and the linter**

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  8 passed (8)`.

Run: `npm --prefix frontend run build`
Expected: `tsc -b` reports no errors and `vite build` prints `built in`. The test file is inside `src`, so this also type-checks it.

Run: `npm --prefix frontend run lint`
Expected: no line reports an error. The `react(set-state-in-effect)` and `react(only-export-components)` warnings are already present on master and are not errors.

- [ ] **Step 6: Commit**

```bash
git add frontend/package.json frontend/package-lock.json frontend/vite.config.ts frontend/src/types/index.ts frontend/src/services/authSession.ts frontend/src/services/authSession.test.ts
git commit -m "feat(web-auth): keep the access token in memory with single-flight refresh (#15)"
```

---

### Task 9: Switch the app to the session module

The phase 1 app stores the whole login response in `localStorage` and sends `userId` in the scan body. This task removes both, routes every API call through `apiFetch`, gives SignalR a token, and puts the dashboard behind the login page. There is no React component test setup in this repo, so the compiler drives the red step and Task 10 walks the flow by hand.

**Files:**
- Replace: `frontend/src/types/index.ts`
- Replace: `frontend/src/services/api.ts`
- Replace: `frontend/src/services/signalr.ts`
- Replace: `frontend/src/features/auth/context/AuthContext.tsx`
- Modify: `frontend/src/features/scan/hooks/useScanRecord.ts:5, 61-62, 72-74, 88`
- Modify: `frontend/src/features/scan/components/ScanRecordPage.tsx:15, 19, 45`
- Modify: `frontend/src/features/auth/components/LoginPage.tsx:10, 78, 81-82, 86`
- Modify: `frontend/src/App.tsx:10, 83-106`, end of `styles`

**Interfaces:**
- Consumes: `login`, `logout`, `refreshSession`, `getCurrentUser`, `onSessionChange`, `getValidAccessToken`, `apiFetch` (Task 8); the login-required API and `CreateScanRecordRequest` without `UserId` (Task 7)
- Produces:
  - `AuthContextType` with `currentUser: AuthUser | null`, `isLoading` (page-load refresh pending), `isSubmitting` (login request pending), `error`, `login(username, password): Promise<boolean>`, `logout(): Promise<void>`, `isAuthenticated`, `isAdmin`
  - `useScanRecord(qrToken: string)` — one parameter
  - `CreateScanRequest { qrToken; status; issueTags?; notes? }` — no `userId`
  - `UserSession` and `loginApi` no longer exist

- [ ] **Step 1: Replace the shared types**

Replace the whole of `frontend/src/types/index.ts` with:

```ts
export type PointStatus = 'Normal' | 'Overdue' | 'Issue' | 'OffHours';
export type ScanStatus = 'Normal' | 'Issue';

export interface ServicePointStatus {
  id: number;
  name: string;
  location: string;
  cleaningIntervalMinutes: number;
  qrToken: string;
  currentStatus: PointStatus;
  lastScannedAt: string | null;
  lastCleanerName: string | null;
  lastScanStatus: ScanStatus | null;
  lastIssueTags: string[] | null;
  lastNotes: string | null;
  minutesSinceLastScan: number;
}

/** The account in a login or refresh response (ADR facility-0011). */
export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: string;
}

/** Body of POST /api/auth/login and /api/auth/refresh. The refresh token is never here: it is an HttpOnly cookie. */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** ADR facility-0017: no userId. The server takes the scanner from the access token. */
export interface CreateScanRequest {
  qrToken: string;
  status: ScanStatus;
  issueTags?: string[];
  notes?: string;
}

export interface CreateScanResponse {
  scanRecordId: number;
  servicePointId: number;
  newPointStatus: PointStatus;
  scannedAt: string;
}
```

- [ ] **Step 2: Build to list every place still on the old contract**

Run: `npm --prefix frontend run build`
Expected: FAIL. `tsc -b` reports `Module '"../../../types"' has no exported member 'UserSession'` in `AuthContext.tsx`, `ScanRecordPage.tsx` and `api.ts`, and `Object literal may only specify known properties, and 'userId' does not exist in type 'CreateScanRequest'` in `useScanRecord.ts`.

- [ ] **Step 3: Route API calls, SignalR and the auth context through the session module**

Replace the whole of `frontend/src/services/api.ts` with:

```ts
import type { ServicePointStatus, CreateScanRequest, CreateScanResponse } from '../types';
import { apiFetch } from './authSession';

const BASE_URL = '/api';

export async function getServicePointsApi(): Promise<ServicePointStatus[]> {
  const res = await apiFetch(`${BASE_URL}/service-points`);
  if (!res.ok) {
    throw new Error('ไม่สามารถโหลดข้อมูลจุดบริการได้');
  }
  return res.json();
}

export async function getServicePointByTokenApi(token: string): Promise<ServicePointStatus> {
  const res = await apiFetch(`${BASE_URL}/service-points/by-token/${encodeURIComponent(token)}`);
  if (!res.ok) {
    throw new Error('ไม่พบข้อมูลจุดบริการสำหรับ QR Token นี้');
  }
  return res.json();
}

export async function createScanRecordApi(req: CreateScanRequest): Promise<CreateScanResponse> {
  const res = await apiFetch(`${BASE_URL}/scan-records`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.message || 'เกิดข้อผิดพลาดในการบันทึกผลการสแกน');
  }

  return res.json();
}
```

Replace the whole of `frontend/src/services/signalr.ts` with:

```ts
import * as signalR from '@microsoft/signalr';
import type { ServicePointStatus } from '../types';
import { getValidAccessToken } from './authSession';

export function createScanHubConnection(onScanRecorded: (point: ServicePointStatus) => void): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    // ADR facility-0018: the hub requires login. SignalR calls the factory on every connect and reconnect,
    // so each attempt carries a token that is still valid (facility-0011 sends it as access_token).
    .withUrl('/hubs/scan', { accessTokenFactory: async () => (await getValidAccessToken()) ?? '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.on('ScanRecorded', (point: ServicePointStatus) => {
    onScanRecorded(point);
  });

  return connection;
}
```

Replace the whole of `frontend/src/features/auth/context/AuthContext.tsx` with:

```tsx
import React, { createContext, useState, useEffect, useCallback } from 'react';
import type { AuthUser } from '../../../types';
import * as authSession from '../../../services/authSession';

/** Phase 1 kept the whole session here. ADR facility-0013 rejected localStorage, so a leftover copy is removed on load. */
const LEGACY_STORAGE_KEY = 'facility_scanner_user';

export interface AuthContextType {
  currentUser: AuthUser | null;
  /** True until the page-load refresh settles, so a logged-in user never sees the login page flash. */
  isLoading: boolean;
  /** True while a login request is running. */
  isSubmitting: boolean;
  error: string | null;
  login: (username: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  isAuthenticated: boolean;
  isAdmin: boolean;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(() => authSession.getCurrentUser());
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    localStorage.removeItem(LEGACY_STORAGE_KEY);
    const unsubscribe = authSession.onSessionChange(setCurrentUser);
    // ADR facility-0013: the access token is never persisted, so a reload asks the refresh cookie for a new one
    authSession.refreshSession().finally(() => setIsLoading(false));
    return unsubscribe;
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<boolean> => {
    setIsSubmitting(true);
    setError(null);
    try {
      await authSession.login(username, password);
      return true;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'เข้าสู่ระบบไม่สำเร็จ');
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }, []);

  const logout = useCallback(() => authSession.logout(), []);

  return (
    <AuthContext.Provider
      value={{
        currentUser,
        isLoading,
        isSubmitting,
        error,
        login,
        logout,
        isAuthenticated: !!currentUser,
        isAdmin: currentUser?.role === 'admin'
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};
```

- [ ] **Step 4: Update the scan hook, the scan page, the login page and the router**

In `frontend/src/features/scan/hooks/useScanRecord.ts`, make these four replacements:

```ts
export function useScanRecord(qrToken: string, userId: number | undefined) {
```

becomes

```ts
export function useScanRecord(qrToken: string) {
```

```ts
    if (!userId || !qrToken) {
      setSubmitError('ไม่พบข้อมูลผู้ใช้หรือรหัส QR');
```

becomes

```ts
    if (!qrToken) {
      setSubmitError('ไม่พบรหัส QR');
```

```ts
        qrToken,
        userId,
        status: targetStatus,
```

becomes

```ts
        qrToken,
        status: targetStatus,
```

```ts
  }, [qrToken, userId, status, selectedTags, notes]);
```

becomes

```ts
  }, [qrToken, status, selectedTags, notes]);
```

In `frontend/src/features/scan/components/ScanRecordPage.tsx`:

- line 15: `import type { UserSession } from '../../../types';` becomes `import type { AuthUser } from '../../../types';`
- line 19: `currentUser: UserSession;` becomes `currentUser: AuthUser;`
- line 45: `} = useScanRecord(qrToken, currentUser.id);` becomes `} = useScanRecord(qrToken);`

In `frontend/src/features/auth/components/LoginPage.tsx`, replace all five occurrences of `isLoading` with `isSubmitting` (the destructuring on line 10 and the four uses on the submit button). `isLoading` now means "restoring the session on page load", which must not disable the login button.

In `frontend/src/App.tsx`, line 10:

```tsx
  const { currentUser, logout, isAuthenticated } = useAuth();
```

becomes

```tsx
  const { currentUser, logout, isAuthenticated, isLoading } = useAuth();
```

Replace the routing block (lines 83-106):

```tsx
      {/* Main Page Routing */}
      <div style={{ flex: 1 }}>
        {isScanRoute ? (
          !isAuthenticated ? (
            <LoginPage onSuccess={() => navigateTo(`/scan/${tokenFromUrl}`)} />
          ) : (
            <ScanRecordPage
              qrToken={tokenFromUrl}
              currentUser={currentUser!}
              onLogout={logout}
              onOpenDashboard={() => navigateTo('/dashboard')}
            />
          )
        ) : currentPath === '/login' && !isAuthenticated ? (
          <LoginPage onSuccess={() => navigateTo('/dashboard')} />
        ) : (
          <DashboardView
            onOpenMobileScanner={(token) => {
              setSelectedScanToken(token);
              navigateTo(`/scan/${token}`);
            }}
          />
        )}
      </div>
```

with:

```tsx
      {/* Main Page Routing */}
      <div style={{ flex: 1 }}>
        {isLoading ? (
          // ADR facility-0013: wait for the page-load refresh before deciding to show the login page
          <div style={styles.sessionCheck}>กำลังตรวจสอบการเข้าสู่ระบบ...</div>
        ) : !isAuthenticated ? (
          // ADR facility-0017 and facility-0018: scanning and the dashboard both need a login; return to the same page after it
          <LoginPage
            onSuccess={() => navigateTo(isScanRoute ? `/scan/${tokenFromUrl}` : '/dashboard')}
          />
        ) : isScanRoute ? (
          <ScanRecordPage
            qrToken={tokenFromUrl}
            currentUser={currentUser!}
            onLogout={logout}
            onOpenDashboard={() => navigateTo('/dashboard')}
          />
        ) : (
          <DashboardView
            onOpenMobileScanner={(token) => {
              setSelectedScanToken(token);
              navigateTo(`/scan/${token}`);
            }}
          />
        )}
      </div>
```

At the end of the `styles` object, replace:

```tsx
    fontSize: '11px'
  }
};
```

with:

```tsx
    fontSize: '11px'
  },
  sessionCheck: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: '60vh',
    color: '#94a3b8',
    fontSize: '14px'
  }
};
```

- [ ] **Step 5: Build, test, lint and check nothing still uses the old session**

Run: `npm --prefix frontend run build`
Expected: PASS, `built in`.

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  8 passed (8)`.

Run: `npm --prefix frontend run lint`
Expected: no line reports an error. The `react(set-state-in-effect)` and `react(only-export-components)` warnings are already present on master and are not errors.

Run:

```bash
grep -rn -E "UserSession|loginApi|userId," --include='*.ts' --include='*.tsx' frontend/src
grep -rn "localStorage" --include='*.ts' --include='*.tsx' frontend/src
```

Expected: the first command prints nothing. The second prints only the `LEGACY_STORAGE_KEY` comment and the `localStorage.removeItem(LEGACY_STORAGE_KEY)` line in `AuthContext.tsx`.

- [ ] **Step 6: Commit**

```bash
git add frontend/src
git commit -m "feat(web-auth): restore sessions by refresh, send bearer tokens and require login for the dashboard (#15, #18)"
```

---

### Task 10: README and the manual end-to-end check on MySQL

Automated tests run on SQLite and cannot prove the MySQL migration, the real cookie in a browser, or a phone on the WiFi. This task writes the setup down and walks the whole flow once on a machine that has MySQL.

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: everything from Tasks 1-9
- Produces: README setup steps other developers follow; a ticked checklist in this plan

- [ ] **Step 1: Replace the README**

Replace the whole of `README.md` with:

````markdown
# Facility Real-time Dashboard (POC)

ระบบติดตามสถานะการทำความสะอาดและสุขอนามัยแบบเรียลไทม์ (Facility Real-time Dashboard) พัฒนาขึ้นเพื่อทดสอบแนวคิด (Proof of Concept) ก่อนเริ่มฝึกงานจริง

## 🏗️ สถาปัตยกรรมระบบ (Architecture)

- **Backend**: .NET 10 LTS (C# 13) Minimal APIs + SignalR Hub (`FacilityRealtime.Api`)
- **Database**: MySQL 8+ (EF Core `MySql.EntityFrameworkCore 10.x`) — API apply migration เองตอนเริ่ม
- **Frontend**: Vite + React 19 + TypeScript + Vanilla CSS (`frontend/`)
- **Real-time Pipeline**: เมื่อแม่บ้านสแกนและบันทึกข้อมูล -> Backend บันทึกลง MySQL -> ส่งผ่าน SignalR Hub (`/hubs/scan`) -> หน้า Dashboard อัปเดตการ์ดและ KPI ทันทีโดยไม่ต้องรีเฟรช
- **Authentication**: login ได้ access token (JWT อายุ 5 นาที) ที่หน้าเว็บเก็บไว้ในหน่วยความจำ และ refresh token ใน HttpOnly cookie ที่หมุนใบใหม่ทุกครั้ง (ADR facility-0011 ถึง facility-0016) หน้า Dashboard, หน้าสแกน และ SignalR hub ต้อง login ก่อน (facility-0017, facility-0018)

---

## 🚀 วิธีการรันระบบ (How to Run)

### 1. ฐานข้อมูล (MySQL)
ตรวจสอบให้แน่ใจว่า MySQL กำลังทำงาน:
```bash
brew services start mysql
```

**ทำครั้งเดียว ถ้าเครื่องนี้เคยรันเวอร์ชันเฟส 1:** เฟส 1 สร้างตารางด้วย `EnsureCreated` ซึ่งไม่มีประวัติ migration ตอนนี้ API สั่ง migrate เองตอนเริ่ม จึงต้องล้างฐานข้อมูลเดิมหนึ่งครั้ง (ข้อมูลสแกนทดสอบหาย แล้ว API seed ข้อมูลตัวอย่างให้ใหม่):
```bash
mysql -u root -e "DROP DATABASE IF EXISTS facility_dashboard; CREATE DATABASE facility_dashboard CHARACTER SET utf8mb4;"
```

### 2. ตั้ง JWT signing key (ครั้งเดียวต่อเครื่อง)
repo นี้เป็น public จึงไม่มี key อยู่ในไฟล์ config และ API จะไม่ยอมเริ่มถ้ายังไม่ได้ตั้ง:
```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/FacilityRealtime.Api
```

### 3. รัน Backend API (.NET 10)
รัน Backend บน Port `5001` (หลีกเลี่ยง Port 5000 ของ AirPlay บน macOS):
```bash
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

### 4. รัน Frontend (Vite React)
```bash
cd frontend
npm install
npm run dev -- --host 0.0.0.0 --port 5173
```

### 5. รันชุดทดสอบ
ชุดทดสอบ API ใช้ SQLite ในหน่วยความจำ ไม่ต้องเปิด MySQL:
```bash
dotnet test backend/FacilityRealtime.slnx
npm --prefix frontend test
```

---

## 📱 การเข้าใช้งานระบบบนอุปกรณ์จริง

- **หน้าจอ Dashboard (สำหรับเปิดบนคอมพิวเตอร์/แท็บเล็ต)**:
  - `http://localhost:5173/dashboard` — ต้อง login ด้วยบัญชีใดก็ได้ แล้วเปิดค้างไว้ได้
- **หน้าสแกนทำความสะอาดสำหรับแม่บ้าน (สำหรับเปิดบนมือถือผ่าน WiFi วงเดียวกัน)**:
  - `http://<MAC_LAN_IP>:5173/scan/token-restroom-m1` (เช่น `http://10.249.194.205:5173/scan/token-restroom-m1`)
  - login ครั้งแรกบนมือถือแล้วระบบจำไว้ จนกด "ออก" หรือไม่ได้ใช้เลย 30 วัน

### บัญชีผู้ใช้ทดสอบ (Seed Data)
- **แม่บ้าน**: username: `somchai` / password: `password123`
- **ผู้ดูแลระบบ**: username: `admin` / password: `admin1234`

### หมายเหตุด้านความปลอดภัยของ POC
- cookie ของ refresh token ไม่ได้ตั้ง `Secure` เพราะมือถือเข้าผ่าน HTTP ในวง WiFi (ADR facility-0013) ถ้าติดตั้งบน HTTPS ให้ตั้งค่า `Jwt:RefreshCookieSecure` เป็น `true`
- บัญชีทดสอบข้างบนเป็นข้อมูลตัวอย่างเท่านั้น
````

- [ ] **Step 2: Run every automated check from a clean state**

```bash
dotnet test backend/FacilityRealtime.slnx --nologo
npm --prefix frontend test
npm --prefix frontend run build
npm --prefix frontend run lint
```

Expected: backend unit tests 39 and API tests 37 pass; Vitest `8 passed`; `tsc -b` and `vite build` succeed; oxlint reports warnings only, no errors.

- [ ] **Step 3: Walk the flow on a machine with MySQL**

This Mac has no MySQL, so this step runs wherever MySQL is installed. Follow README steps 1-4 first, including the one-time database reset. Tick each row only after seeing the expected result; if one fails, stop and debug with `debug-mantra` before changing code.

| # | Do | Expect |
|---|---|---|
| 1 | Start the API | The log shows `Applying migration '..._InitialCreate'` and `Applying migration '..._Phase2AuthRefreshTokens'`, then `Now listening on: http://0.0.0.0:5001` |
| 2 | `curl -i http://localhost:5001/api/service-points` | `HTTP/1.1 401 Unauthorized` |
| 3 | Open `http://localhost:5173/dashboard` | The login page, not the dashboard |
| 4 | Log in as `admin` / `admin1234` | The dashboard with 3 cards and the live connection badge connected |
| 5 | DevTools → Application → Cookies | `facility_refresh` with HttpOnly ticked, Path `/api/auth`, SameSite `Strict` |
| 6 | DevTools → Application → Local Storage | No `facility_scanner_user` key |
| 7 | Reload the dashboard | It stays logged in; Network shows `POST /api/auth/refresh` → `200` |
| 8 | On a phone on the same WiFi, open `http://<MAC_LAN_IP>:5173/scan/token-restroom-m1` | The login page |
| 9 | Log in as `somchai` / `password123`, tap confirm | The success screen; within a second the dashboard card shows `สมชาย ใจดี` as last cleaner |
| 10 | Leave the dashboard open for 6 minutes | Network shows a `POST /api/auth/refresh` → `200`, and the 30-second `GET /api/service-points` calls keep returning `200` |
| 11 | `mysql -u root facility_dashboard -e "SELECT LENGTH(TokenHash), RotatedAt IS NULL, RevokedAt IS NULL FROM refresh_tokens ORDER BY Id DESC LIMIT 3;"` | `64` in the first column; the newest row unrotated and unrevoked |
| 12 | `mysql -u root facility_dashboard -e "UPDATE users SET IsActive = 0 WHERE Username = 'somchai';"`, wait 5 minutes, reload the phone's scan page | The login page; logging in as `somchai` shows `ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง` |
| 13 | `mysql -u root facility_dashboard -e "UPDATE users SET IsActive = 1 WHERE Username = 'somchai';"`, log in on the phone again | Scan page works again |
| 14 | Click `ออก` in the dashboard navbar, then reload | The login page both times; Network shows `POST /api/auth/logout` → `204` |

- [ ] **Step 4: Commit**

```bash
git add README.md docs/superpowers/plans/2026-09-13-phase2a-auth-jwt-refresh.md
git commit -m "docs(readme): document signing key, database reset and login-required flow (#15)"
```

---

## Plan Complete

Plan 2A is done when all ten tasks are ticked. Plans 2B (accounts page, facility-0020) and 2C (points admin page, facility-0021) build on the `AdminOnly` policy, `RevokeUserSessionsAsync`, `apiFetch` and the API test harness from this plan.
