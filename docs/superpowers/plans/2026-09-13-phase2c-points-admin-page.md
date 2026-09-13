# Phase 2C: Service Points Admin Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an Admin add, edit and deactivate service points, regenerate a point's QR Token behind one confirmation, and print QR Signs one at a time or all at once, from a paginated table.

**Architecture:** Admin-only routes under `/api/admin/service-points` return every point with its scan link, QR issue date and last scan; paging, search and sorting happen in the browser as the Dashboard decided in #14. Scan links everywhere come from one `PublicBaseUrl` config value. The React page reuses the admin menu, styles and toast from plan 2B, adds a shared pager, and draws QR images locally for the print sheet and the dashboard card.

**Tech Stack:** .NET 10 Minimal API, EF Core 10 (`MySql.EntityFrameworkCore` 10.0.9), xUnit 2.9.3 with the plan 2A harness; React 19 + TypeScript + Vite 8, Vanilla CSS, `lucide-react`, `qrcode` 1.5.4 (MIT), Vitest 5.0.0.

**Spec:**
- `docs/adr/facility-0021-points-admin-page.md` (the decision this plan implements)
- Prototype confirmed by the owner: `docs/decision-map/facility-poc/mockups/points-admin-prototype.html` (Version 2, with pagination)
- `docs/adr/facility-0003-phone-reaches-mac-over-same-wifi-http.md` (URL in QR from `PublicBaseUrl`)
- `docs/adr/facility-0006-mysql-schema-and-qr-token.md` (QR Token is a random UUID; regenerating voids the old sign and keeps history)
- `docs/adr/facility-0005-point-status-rules.md` (Cleaning Interval in whole minutes, greater than 0)
- `docs/adr/facility-0008-dashboard-cards-grid-layout.md` amendment (page bar 12/24/48)
- `docs/adr/facility-0009-delivery-plan-walking-skeleton.md` (Scope Defense item 2)
- `docs/adr/facility-0010-admin-gate-server-side.md`
- `CONTEXT.md` (Service Point, QR Token, QR Sign, Deactivated Service Point, Cleaning Interval)
- Decision map ticket: https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/16

**Depends on:** plan 2A (`AdminOnly`, test harness, `apiFetch`) and plan 2B (`admin.css`, `AdminLayout`, `adminTabs.ts`, `Toast`, `useToast`, `toApiError`, the `/admin` route) merged first.

## Global Constraints

- **Layout:** one table row per point with name and location, Cleaning Interval in minutes, last scan, short QR Token with its issue date, active state, and the actions **แก้ไข · พิมพ์ป้าย · ออก QR ใหม่ · ปิด/เปิดใช้งาน**; a search box and a **ซ่อนจุดที่ปิดใช้งาน** checkbox; the accounts page from plan 2B is a sibling tab in the same Admin menu (facility-0021).
- **Add and edit** happen in a right-hand drawer (full width on a phone): name required, location, Cleaning Interval as a whole number of minutes greater than 0 with shortcuts **30/45/60/90/120**. The QR Token is generated on create and **cannot be edited in the drawer** (facility-0021, facility-0005).
- **Pagination** uses the same bar as the Dashboard: **12/24/48 rows**, rows **sorted by name**, not urgency. Search, hiding deactivated points or changing the page size **returns to page 1**; adding or renaming a point **jumps to that row's page and flashes it**; deactivating and regenerating **never move a row** (facility-0021, facility-0008 amendment).
- **Deactivate instead of delete:** no delete button; a deactivated row stays in the table faded, **leaves the Dashboard**, keeps its scan history, and gets a toast with **undo**; **print and regenerate are disabled** for it (facility-0021).
- **Regenerate QR:** one dialog naming the point, warning that **the sign on the wall stops scanning immediately**, stating that scan history is unchanged, with an orange confirm button; after confirming, a toast **offers to print the new sign** (facility-0021, facility-0006).
- **Printing:** **พิมพ์ป้าย** on a row prints one sign; **พิมพ์ป้ายทั้งหมด** prints **every active point, not only the visible page**; paper is **A4 portrait with 6 signs per page**; each sign shows name, location, `สแกนด้วยกล้องมือถือ เพื่อบันทึกการทำความสะอาด`, interval, QR issue date, short token, the full URL in small type, and the QR image; printing uses the browser (facility-0021).
- **API** under `/api/admin/service-points`: list including deactivated points, create, update, activate/deactivate, regenerate-token; **admin-only, 401/403** (facility-0010). The Dashboard endpoints keep returning active points only.
- A sign for a **Deactivated Service Point** makes the scan page **say the point is deactivated**, not a bare "not found" (facility-0021).
- The URL printed on signs comes from **`PublicBaseUrl` in config**, replacing the IP hardcoded in `ServicePointCard.tsx` (facility-0003, facility-0021). Local machines set it with `dotnet user-secrets`, not in a committed file.
- The date the current QR Token was issued is stored (`QrTokenIssuedAt`) and shown in the table and on the sign (facility-0021).
- QR Tokens are **random UUID v4** strings (facility-0006).
- The page bar is **one shared component**. The Dashboard adopts it once it has more than 12 points (facility-0008 amendment); that switch is not part of this plan.
- QR images are drawn in the browser with the MIT `qrcode` package. An external QR image service is allowed only as the Scope Defense fallback (facility-0009 item 2).
- Field rules (plan-level choices, identical messages on client and server): name 1-150 characters after trimming; location at most 255 characters and may be empty; interval a whole number greater than 0.
- Paging, search and sorting run in the browser; the list endpoint returns every point, as #14 decided for the Dashboard.
- Follow the established code shape from plans 2A and 2B. Commit messages reference the decision ticket `(#16)`.

## File Structure

| Path | Status | Responsibility |
|---|---|---|
| `backend/src/FacilityRealtime.Application/Common/ScanUrlBuilder.cs` | Create | Scan link from `PublicBaseUrl` |
| `backend/tests/FacilityRealtime.UnitTests/ScanUrlBuilderTests.cs` | Create | Link format and config validation |
| `backend/src/FacilityRealtime.Api/Program.cs` | Modify | Register the builder, `scanUrl` in responses, 410 for deactivated points, map admin routes |
| `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs` | Modify | `ScanUrl` on `ServicePointStatusDto` |
| `backend/src/FacilityRealtime.Api/appsettings.json` | Modify | Default `PublicBaseUrl` |
| `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs` | Modify | Optional extra settings per test |
| `backend/tests/FacilityRealtime.ApiTests/ServicePoints/ScanUrlAndDeactivatedPointTests.cs` | Create | Scan links and 410 behaviour |
| `backend/src/FacilityRealtime.Application/ServicePoints/ServicePointRules.cs` | Create | Field rules and QR Token generation |
| `backend/tests/FacilityRealtime.UnitTests/ServicePointRulesTests.cs` | Create | Rule tests |
| `backend/src/FacilityRealtime.Domain/Entities/ServicePoint.cs` | Modify | `QrTokenIssuedAt` |
| `backend/src/FacilityRealtime.Infrastructure/Migrations/*_Phase2cQrTokenIssuedAt.cs` | Create (generated, then edited) | Column plus backfill from `CreatedAt` |
| `backend/src/FacilityRealtime.Api/DTOs/AdminServicePointDtos.cs` | Create | Row and request shapes |
| `backend/src/FacilityRealtime.Api/Endpoints/AdminServicePointEndpoints.cs` | Create | The six admin routes |
| `backend/tests/FacilityRealtime.ApiTests/Admin/AdminServicePointsTests.cs` | Create | Route behaviour and gate |
| `frontend/src/types/index.ts` | Modify | `scanUrl`, `AdminServicePoint`, `SaveServicePointInput` |
| `frontend/src/services/adminPointsApi.ts` | Create | Calls to `/api/admin/service-points` |
| `frontend/src/services/api.ts` | Modify | Deactivated and replaced sign messages |
| `frontend/src/features/admin/logic/pointValidation.ts` (+ test) | Create | Client copy of the field rules |
| `frontend/src/features/admin/logic/pointList.ts` (+ test) | Create | Name sort, search, upsert, A4 page split |
| `frontend/src/features/admin/logic/formatDate.ts` (+ test) | Create | UTC parsing and Thai dates in Bangkok time |
| `frontend/src/components/pager/pagination.ts` (+ test), `Pager.tsx`, `pager.css` | Create | Shared page bar |
| `frontend/src/components/qr/useQrDataUrl.ts`, `QrImage.tsx`, `qrcode.test.ts` | Create | Local QR images |
| `frontend/src/features/admin/adminTabs.ts` (+ test) | Modify | Add the `points` tab first |
| `frontend/src/features/admin/printSheet.css` | Create | A4 print layout |
| `frontend/src/features/admin/hooks/usePointsAdminPage.ts`, `components/PointsAdminPage.tsx` | Create | The table page |
| `frontend/src/features/admin/hooks/usePointForm.ts`, `components/PointFormDrawer.tsx` | Create | Add and edit |
| `frontend/src/features/admin/hooks/useRegenerateQr.ts`, `components/RegenerateQrDialog.tsx` | Create | Regenerate confirmation |
| `frontend/src/features/admin/components/PrintSheet.tsx` | Create | Print sheet |
| `frontend/src/features/dashboard/components/ServicePointCard.tsx` | Modify | `scanUrl` and local QR instead of the hardcoded IP and external service |
| `frontend/src/features/scan/components/ScanRecordPage.tsx` | Modify | Heading that fits deactivated and replaced signs |
| `frontend/src/App.tsx` | Modify | Render the points page for the `points` tab |

---

### Task 1: Scan link builder

**Files:**
- Create: `backend/src/FacilityRealtime.Application/Common/ScanUrlBuilder.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/ScanUrlBuilderTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `ScanUrlBuilder.ConfigKey = "PublicBaseUrl"`
  - `new ScanUrlBuilder(string? publicBaseUrl)` — throws `ArgumentException` whose message names `PublicBaseUrl` unless the value is an absolute http or https URL with no query or fragment
  - `string BaseUrl`, `string ForToken(string qrToken)` → `{BaseUrl}/scan/{escaped token}` (the route `App.tsx` already serves)

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/ScanUrlBuilderTests.cs`:

```csharp
using FacilityRealtime.Application.Common;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class ScanUrlBuilderTests
{
    [Fact]
    public void Builds_the_scan_route_on_the_configured_base_url()
    {
        var urls = new ScanUrlBuilder("http://192.168.1.20:5173");

        Assert.Equal("http://192.168.1.20:5173/scan/3f2b9c1e-8a4d-4c7e-9b1a-2d5e6f7a8b9c", urls.ForToken("3f2b9c1e-8a4d-4c7e-9b1a-2d5e6f7a8b9c"));
    }

    [Fact]
    public void Trailing_slash_on_the_base_url_does_not_double_up()
    {
        Assert.Equal("http://192.168.1.20:5173/scan/abc", new ScanUrlBuilder("http://192.168.1.20:5173/").ForToken("abc"));
    }

    [Fact]
    public void Keeps_a_base_path()
    {
        Assert.Equal("https://example.test/facility/scan/abc", new ScanUrlBuilder("https://example.test/facility").ForToken("abc"));
    }

    [Fact]
    public void Escapes_characters_that_would_break_the_path()
    {
        Assert.Equal("http://host:5173/scan/a%20b%2Fc", new ScanUrlBuilder("http://host:5173").ForToken("a b/c"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("192.168.1.20:5173")]
    [InlineData("/scan")]
    [InlineData("ftp://192.168.1.20")]
    [InlineData("http://192.168.1.20:5173/?lang=th")]
    public void Rejects_values_that_cannot_make_a_working_link(string? value)
    {
        var error = Assert.Throws<ArgumentException>(() => new ScanUrlBuilder(value));

        Assert.Contains("PublicBaseUrl", error.Message);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: FAIL at build with `The type or namespace name 'ScanUrlBuilder' could not be found`.

- [ ] **Step 3: Write the builder**

Create `backend/src/FacilityRealtime.Application/Common/ScanUrlBuilder.cs`:

```csharp
namespace FacilityRealtime.Application.Common;

/// <summary>
/// ADR facility-0003: the link inside a QR Sign is built from PublicBaseUrl in config, so moving the Mac to a new
/// IP means editing one value and reprinting, not changing code. The frontend route is /scan/{token} (App.tsx).
/// </summary>
public sealed class ScanUrlBuilder
{
    public const string ConfigKey = "PublicBaseUrl";

    public ScanUrlBuilder(string? publicBaseUrl)
    {
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                $"{ConfigKey} must be an absolute http or https URL with no query string, for example http://192.168.1.20:5173. Got '{publicBaseUrl}'.",
                nameof(publicBaseUrl));
        }

        BaseUrl = publicBaseUrl!.TrimEnd('/');
    }

    public string BaseUrl { get; }

    public string ForToken(string qrToken) => $"{BaseUrl}/scan/{Uri.EscapeDataString(qrToken)}";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: PASS, `Passed: 79`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Application/Common/ScanUrlBuilder.cs backend/tests/FacilityRealtime.UnitTests/ScanUrlBuilderTests.cs
git commit -m "feat(points): build scan links from PublicBaseUrl (#16)"
```

---

### Task 2: Scan links in dashboard and scan responses; 410 for deactivated points

**Files:**
- Create: `backend/tests/FacilityRealtime.ApiTests/ServicePoints/ScanUrlAndDeactivatedPointTests.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`
- Modify: `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Modify: `backend/src/FacilityRealtime.Api/appsettings.json`

**Interfaces:**
- Consumes: `ScanUrlBuilder` (Task 1); `FacilityApiFactory`, `AuthApi.LoggedInAsync` (plan 2A)
- Produces:
  - `ServicePointStatusDto` gains `string ScanUrl` right after `QrToken`, so `GET /api/service-points`, `GET /api/service-points/by-token/{token}` and the `ScanRecorded` SignalR message all carry `scanUrl`
  - `GET /api/service-points/by-token/{token}` and `POST /api/scan-records` answer `410 { message, reason: "deactivated" }` for a deactivated point; an unknown token stays `404`
  - `ScanUrlBuilder` registered as a singleton; startup fails when `PublicBaseUrl` cannot make a link
  - `FacilityApiFactory(string signingKey = TestSigningKey, IReadOnlyDictionary<string, string>? settings = null)`

- [ ] **Step 1: Let tests pass extra settings, then write the failing tests**

In `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`, replace:

```csharp
public sealed class FacilityApiFactory(string signingKey = FacilityApiFactory.TestSigningKey) : WebApplicationFactory<Program>
```

with:

```csharp
public sealed class FacilityApiFactory(
    string signingKey = FacilityApiFactory.TestSigningKey,
    IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
```

and replace:

```csharp
        builder.UseSetting("Jwt:SigningKey", signingKey);
```

with:

```csharp
        builder.UseSetting("Jwt:SigningKey", signingKey);
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
```

Create `backend/tests/FacilityRealtime.ApiTests/ServicePoints/ScanUrlAndDeactivatedPointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.ServicePoints;

public class ScanUrlAndDeactivatedPointTests
{
    private const string QrToken = "token-restroom-m1";

    [Fact]
    public async Task Dashboard_points_carry_the_scan_url_built_from_public_base_url()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var points = await client.GetFromJsonAsync<List<JsonElement>>("/api/service-points");

        var point = points!.Single(p => p.GetProperty("qrToken").GetString() == QrToken);
        Assert.Equal($"http://localhost:5173/scan/{QrToken}", point.GetProperty("scanUrl").GetString());
    }

    [Fact]
    public async Task Changing_public_base_url_changes_every_scan_url()
    {
        using var factory = new FacilityApiFactory(settings: new Dictionary<string, string> { ["PublicBaseUrl"] = "http://192.168.1.20:5173" });
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var point = await client.GetFromJsonAsync<JsonElement>($"/api/service-points/by-token/{QrToken}");

        Assert.Equal($"http://192.168.1.20:5173/scan/{QrToken}", point.GetProperty("scanUrl").GetString());
    }

    [Fact]
    public void App_refuses_to_start_when_public_base_url_cannot_make_a_link()
    {
        using var factory = new FacilityApiFactory(settings: new Dictionary<string, string> { ["PublicBaseUrl"] = "192.168.1.20:5173" });

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("PublicBaseUrl", error.ToString());
    }

    [Fact]
    public async Task Opening_the_sign_of_a_deactivated_point_is_410_with_a_reason()
    {
        using var factory = new FacilityApiFactory();
        await DeactivateSeededPointAsync(factory);
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await client.GetAsync($"/api/service-points/by-token/{QrToken}");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("deactivated", body.GetProperty("reason").GetString());
        Assert.Equal("จุดบริการ 'ห้องน้ำชาย ชั้น 1' ปิดใช้งานแล้ว", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Scanning_a_deactivated_point_is_410_and_records_nothing()
    {
        using var factory = new FacilityApiFactory();
        await DeactivateSeededPointAsync(factory);
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await client.PostAsJsonAsync("/api/scan-records", new { qrToken = QrToken, status = "Normal" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var scans = -1;
        await factory.WithDbAsync(async db => scans = await db.ScanRecords.CountAsync());
        Assert.Equal(0, scans);
    }

    [Fact]
    public async Task A_token_no_point_has_is_still_404()
    {
        using var factory = new FacilityApiFactory();
        var (client, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await client.GetAsync("/api/service-points/by-token/token-that-was-replaced");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task DeactivateSeededPointAsync(FacilityApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            (await db.ServicePoints.SingleAsync(p => p.QrToken == QrToken)).IsActive = false;
            await db.SaveChangesAsync();
        });
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo --filter "FullyQualifiedName~ScanUrlAndDeactivatedPointTests"`
Expected: FAIL. Responses have no `scanUrl` property (`KeyNotFoundException`), deactivated points answer `404` instead of `410`, and a bad `PublicBaseUrl` starts without error.

- [ ] **Step 3: Add `ScanUrl` to the DTO and register the builder**

In `backend/src/FacilityRealtime.Api/DTOs/ScanDtos.cs`, replace:

```csharp
    string QrToken,
    PointStatus CurrentStatus,
```

with:

```csharp
    string QrToken,
    string ScanUrl,
    PointStatus CurrentStatus,
```

In `backend/src/FacilityRealtime.Api/Program.cs`, replace:

```csharp
builder.Services.AddSingleton(workingHours);
```

with:

```csharp
builder.Services.AddSingleton(workingHours);

// 0b. Public base URL for the links inside QR Signs (ADR facility-0003, facility-0021). Fails at startup if it cannot make a working link.
builder.Services.AddSingleton(new ScanUrlBuilder(builder.Configuration[ScanUrlBuilder.ConfigKey]));
```

- [ ] **Step 4: Put the link in every point response and answer 410 for deactivated points**

In `Program.cs`, give the three handlers a `ScanUrlBuilder scanUrls` parameter:

```csharp
app.MapGet("/api/service-points", async (AppDbContext db, WorkingHours workingHours, ScanUrlBuilder scanUrls) =>
```

```csharp
app.MapGet("/api/service-points/by-token/{token}", async (string token, AppDbContext db, WorkingHours workingHours, ScanUrlBuilder scanUrls) =>
```

and in the `/api/scan-records` parameter list replace `    WorkingHours workingHours) =>` with:

```csharp
    WorkingHours workingHours,
    ScanUrlBuilder scanUrls) =>
```

In each of the three `new ServicePointStatusDto(` calls, add a line after `point.QrToken,` with the same indentation:

```csharp
            point.QrToken,
            scanUrls.ForToken(point.QrToken),
```

In the `/api/service-points/by-token/{token}` handler, replace:

```csharp
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == token && p.IsActive);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Service point with QR token '{token}' not found." });
    }
```

with:

```csharp
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == token);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Service point with QR token '{token}' not found." });
    }

    // ADR facility-0021: the sign of a Deactivated Service Point says so, instead of a bare "not found"
    if (!point.IsActive)
    {
        return Results.Json(new { message = $"จุดบริการ '{point.Name}' ปิดใช้งานแล้ว", reason = "deactivated" }, statusCode: StatusCodes.Status410Gone);
    }
```

In the `/api/scan-records` handler, replace:

```csharp
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == req.QrToken && p.IsActive);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Invalid QR token '{req.QrToken}'." });
    }
```

with:

```csharp
    var point = await db.ServicePoints.FirstOrDefaultAsync(p => p.QrToken == req.QrToken);
    if (point == null)
    {
        return Results.NotFound(new { message = $"Invalid QR token '{req.QrToken}'." });
    }

    if (!point.IsActive)
    {
        return Results.Json(new { message = $"จุดบริการ '{point.Name}' ปิดใช้งานแล้ว", reason = "deactivated" }, statusCode: StatusCodes.Status410Gone);
    }
```

Replace the whole of `backend/src/FacilityRealtime.Api/appsettings.json` with (the default suits a browser on the Mac itself; phones need the Mac's LAN address, set per machine in Task 8):

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
  },
  "PublicBaseUrl": "http://localhost:5173"
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 79, API tests 71.

- [ ] **Step 6: Commit**

```bash
git add backend/src/FacilityRealtime.Api backend/tests/FacilityRealtime.ApiTests
git commit -m "feat(points): send scan links with every point and answer 410 for deactivated signs (#16)"
```

---

### Task 3: Service point field rules

**Files:**
- Create: `backend/src/FacilityRealtime.Application/ServicePoints/ServicePointRules.cs`
- Create: `backend/tests/FacilityRealtime.UnitTests/ServicePointRulesTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `ServicePointRules.NameMaxLength = 150`, `LocationMaxLength = 255`
  - `string? NameError(string?)`, `string? LocationError(string?)`, `string? IntervalError(int)`
  - `Dictionary<string, string[]> Validate(string? name, string? location, int cleaningIntervalMinutes)` — keys `name`, `location`, `cleaningIntervalMinutes`
  - `string NewQrToken()` — UUID v4 string

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/FacilityRealtime.UnitTests/ServicePointRulesTests.cs`:

```csharp
using FacilityRealtime.Application.ServicePoints;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class ServicePointRulesTests
{
    [Fact]
    public void Complete_point_has_no_errors()
    {
        Assert.Empty(ServicePointRules.Validate("ห้องน้ำชาย ชั้น 2", "อาคาร A ชั้น 2", 60));
    }

    [Fact]
    public void Location_may_be_left_empty()
    {
        Assert.Empty(ServicePointRules.Validate("จุดทิ้งขยะหลังครัว", null, 90));
        Assert.Empty(ServicePointRules.Validate("จุดทิ้งขยะหลังครัว", "", 90));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Name_is_required(string? name)
    {
        Assert.Equal("กรอกชื่อจุด", ServicePointRules.NameError(name));
    }

    [Fact]
    public void Name_allows_150_characters_after_trimming_and_rejects_151()
    {
        Assert.Null(ServicePointRules.NameError($" {new string('ก', 150)} "));
        Assert.Equal("ชื่อจุดยาวได้ไม่เกิน 150 ตัวอักษร", ServicePointRules.NameError(new string('ก', 151)));
    }

    [Fact]
    public void Location_allows_255_characters_and_rejects_256()
    {
        Assert.Null(ServicePointRules.LocationError(new string('ก', 255)));
        Assert.Equal("ตำแหน่งยาวได้ไม่เกิน 255 ตัวอักษร", ServicePointRules.LocationError(new string('ก', 256)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Interval_must_be_greater_than_zero(int minutes)
    {
        Assert.Equal("รอบทำความสะอาดต้องเป็นจำนวนเต็มมากกว่า 0 นาที", ServicePointRules.IntervalError(minutes));
    }

    [Fact]
    public void One_minute_interval_is_allowed()
    {
        Assert.Null(ServicePointRules.IntervalError(1));
    }

    [Fact]
    public void Validate_names_every_bad_field()
    {
        var errors = ServicePointRules.Validate("", new string('ก', 256), 0);

        Assert.Equal(new[] { "cleaningIntervalMinutes", "location", "name" }, errors.Keys.OrderBy(k => k));
    }

    [Fact]
    public void New_qr_tokens_are_distinct_uuid_v4_values()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => ServicePointRules.NewQrToken()).ToList();

        Assert.All(tokens, t => Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", t));
        Assert.Equal(50, tokens.Distinct().Count());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: FAIL at build with `The type or namespace name 'ServicePoints' does not exist in the namespace 'FacilityRealtime.Application'`.

- [ ] **Step 3: Write the rules**

Create `backend/src/FacilityRealtime.Application/ServicePoints/ServicePointRules.cs`:

```csharp
namespace FacilityRealtime.Application.ServicePoints;

/// <summary>
/// Field rules for the service points admin page (ADR facility-0021, facility-0005). The frontend's
/// pointValidation.ts shows the same messages before a round trip; this class is the authority.
/// </summary>
public static class ServicePointRules
{
    public const int NameMaxLength = 150;
    public const int LocationMaxLength = 255;

    public static string? NameError(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return "กรอกชื่อจุด";
        }

        return value.Length > NameMaxLength ? $"ชื่อจุดยาวได้ไม่เกิน {NameMaxLength} ตัวอักษร" : null;
    }

    public static string? LocationError(string? location) =>
        (location?.Trim().Length ?? 0) > LocationMaxLength ? $"ตำแหน่งยาวได้ไม่เกิน {LocationMaxLength} ตัวอักษร" : null;

    /// <summary>ADR facility-0005: Cleaning Interval is a whole number of minutes greater than 0.</summary>
    public static string? IntervalError(int minutes) =>
        minutes > 0 ? null : "รอบทำความสะอาดต้องเป็นจำนวนเต็มมากกว่า 0 นาที";

    public static Dictionary<string, string[]> Validate(string? name, string? location, int cleaningIntervalMinutes) =>
        new (string Field, string? Error)[]
            {
                ("name", NameError(name)),
                ("location", LocationError(location)),
                ("cleaningIntervalMinutes", IntervalError(cleaningIntervalMinutes)),
            }
            .Where(c => c.Error is not null)
            .ToDictionary(c => c.Field, c => new[] { c.Error! });

    /// <summary>ADR facility-0006: a random UUID v4, so a sign's URL cannot be guessed from the point.</summary>
    public static string NewQrToken() => Guid.NewGuid().ToString();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test backend/tests/FacilityRealtime.UnitTests --nologo`
Expected: PASS, `Passed: 91`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/FacilityRealtime.Application/ServicePoints backend/tests/FacilityRealtime.UnitTests/ServicePointRulesTests.cs
git commit -m "feat(points): add service point field rules and UUID token generation (#16)"
```

---

### Task 4: QR issue date and the admin service points API

**Files:**
- Modify: `backend/src/FacilityRealtime.Domain/Entities/ServicePoint.cs`
- Create (generated, then edited): `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_Phase2cQrTokenIssuedAt.cs`, `.Designer.cs`; Modify (generated): `AppDbContextModelSnapshot.cs`
- Create: `backend/src/FacilityRealtime.Api/DTOs/AdminServicePointDtos.cs`
- Create: `backend/src/FacilityRealtime.Api/Endpoints/AdminServicePointEndpoints.cs`
- Create: `backend/tests/FacilityRealtime.ApiTests/Admin/AdminServicePointsTests.cs`
- Modify: `backend/src/FacilityRealtime.Api/Program.cs` (map the routes)

**Interfaces:**
- Consumes: `ServicePointRules` (Task 3); `ScanUrlBuilder` (Tasks 1-2); `AuthSetup.AdminOnly`, `TimeProvider`, test harness (plan 2A)
- Produces (all require the `admin` role: `401` anonymous, `403` cleaner):
  - `ServicePoint.QrTokenIssuedAt` (`DateTime`, UTC)
  - `GET /api/admin/service-points` → `200 AdminServicePointDto[]`, deactivated points included
  - `POST /api/admin/service-points` `{ name, location, cleaningIntervalMinutes }` → `201 AdminServicePointDto`; `400 { message, errors }`
  - `PUT /api/admin/service-points/{id}` same body → `200`; `400`; `404`
  - `POST /api/admin/service-points/{id}/regenerate-token` → `200 AdminServicePointDto` with a new token and issue date; `400` for a deactivated point; `404`
  - `POST /api/admin/service-points/{id}/deactivate` and `/activate` → `204`; `404`
  - `record AdminServicePointDto(int Id, string Name, string Location, int CleaningIntervalMinutes, bool IsActive, string QrToken, DateTime QrTokenIssuedAt, string ScanUrl, DateTime? LastScannedAt, string? LastCleanerName)`
  - `IEndpointRouteBuilder.MapAdminServicePointEndpoints()`

- [ ] **Step 1: Write the failing API tests**

Create `backend/tests/FacilityRealtime.ApiTests/Admin/AdminServicePointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FacilityRealtime.ApiTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Admin;

public record AdminServicePointModel(
    int Id, string Name, string Location, int CleaningIntervalMinutes, bool IsActive,
    string QrToken, DateTime QrTokenIssuedAt, string ScanUrl, DateTime? LastScannedAt, string? LastCleanerName);

public class AdminServicePointsTests
{
    private const string SeededToken = "token-restroom-m1";

    public static TheoryData<string, string> EveryRoute => new()
    {
        { "GET", "/api/admin/service-points" },
        { "POST", "/api/admin/service-points" },
        { "PUT", "/api/admin/service-points/1" },
        { "POST", "/api/admin/service-points/1/regenerate-token" },
        { "POST", "/api/admin/service-points/1/deactivate" },
        { "POST", "/api/admin/service-points/1/activate" },
    };

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task Anonymous_requests_are_401(string method, string path)
    {
        using var factory = new FacilityApiFactory();

        var response = await SendAsync(factory.CreateApiClient(), method, path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EveryRoute))]
    public async Task Cleaner_accounts_are_403(string method, string path)
    {
        using var factory = new FacilityApiFactory();
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);

        var response = await SendAsync(cleaner, method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_includes_deactivated_points_with_scan_url_issue_date_and_last_scan()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await cleaner.PostAsJsonAsync("/api/scan-records", new { qrToken = SeededToken, status = "Normal" });
        await factory.WithDbAsync(async db =>
        {
            (await db.ServicePoints.SingleAsync(p => p.QrToken == "token-waste-zone-b")).IsActive = false;
            await db.SaveChangesAsync();
        });

        var points = (await admin.GetFromJsonAsync<List<AdminServicePointModel>>("/api/admin/service-points"))!;

        Assert.Equal(3, points.Count);
        Assert.False(points.Single(p => p.QrToken == "token-waste-zone-b").IsActive);
        var scanned = points.Single(p => p.QrToken == SeededToken);
        Assert.Equal($"http://localhost:5173/scan/{SeededToken}", scanned.ScanUrl);
        Assert.Equal("สมชาย ใจดี", scanned.LastCleanerName);
        Assert.NotNull(scanned.LastScannedAt);
        Assert.True(scanned.QrTokenIssuedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task Created_point_gets_a_uuid_token_and_shows_on_the_dashboard()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/service-points", new { name = " ห้องน้ำชาย ชั้น 2 ", location = "อาคาร A ชั้น 2", cleaningIntervalMinutes = 45 });
        var dashboard = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/service-points"))!;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<AdminServicePointModel>())!;
        Assert.Equal("ห้องน้ำชาย ชั้น 2", created.Name);
        Assert.True(Guid.TryParse(created.QrToken, out _));
        Assert.Equal($"http://localhost:5173/scan/{created.QrToken}", created.ScanUrl);
        Assert.Null(created.LastScannedAt);
        Assert.Contains(dashboard, p => p.GetProperty("name").GetString() == "ห้องน้ำชาย ชั้น 2");
    }

    [Fact]
    public async Task Create_rejects_bad_fields_and_names_each_one()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/admin/service-points", new { name = "", location = "", cleaningIntervalMinutes = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "cleaningIntervalMinutes", "name" }, body.GetProperty("errors").EnumerateObject().Select(p => p.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Edit_changes_name_location_and_interval_but_keeps_the_sign_working()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await admin.PutAsJsonAsync("/api/admin/service-points/1", new { name = "ห้องน้ำชาย ชั้น 1 (ปรับปรุงแล้ว)", location = "อาคาร A ชั้น 1", cleaningIntervalMinutes = 30 });
        var sign = await admin.GetAsync($"/api/service-points/by-token/{SeededToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AdminServicePointModel>())!;
        Assert.Equal(30, body.CleaningIntervalMinutes);
        Assert.Equal(SeededToken, body.QrToken);
        Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
    }

    [Fact]
    public async Task Regenerating_replaces_the_token_so_the_old_sign_stops_working_and_history_stays()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await cleaner.PostAsJsonAsync("/api/scan-records", new { qrToken = SeededToken, status = "Normal" });
        var before = (await admin.GetFromJsonAsync<List<AdminServicePointModel>>("/api/admin/service-points"))!.Single(p => p.QrToken == SeededToken);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));

        var response = await admin.PostAsync($"/api/admin/service-points/{before.Id}/regenerate-token", null);
        var after = (await response.Content.ReadFromJsonAsync<AdminServicePointModel>())!;
        var oldSign = await cleaner.GetAsync($"/api/service-points/by-token/{SeededToken}");
        var newSign = await cleaner.GetAsync($"/api/service-points/by-token/{after.QrToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(SeededToken, after.QrToken);
        Assert.True(after.QrTokenIssuedAt > before.QrTokenIssuedAt);
        Assert.Equal(HttpStatusCode.NotFound, oldSign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newSign.StatusCode);
        Assert.Equal("สมชาย ใจดี", after.LastCleanerName);
    }

    [Fact]
    public async Task Regenerating_a_deactivated_point_is_refused()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        await admin.PostAsync("/api/admin/service-points/1/deactivate", null);

        var response = await admin.PostAsync("/api/admin/service-points/1/regenerate-token", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivating_hides_the_point_from_the_dashboard_and_keeps_its_history()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        var (cleaner, _, _) = await AuthApi.LoggedInAsync(factory);
        await cleaner.PostAsJsonAsync("/api/scan-records", new { qrToken = SeededToken, status = "Normal" });

        var response = await admin.PostAsync("/api/admin/service-points/1/deactivate", null);
        var dashboard = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/service-points"))!;
        var sign = await cleaner.GetAsync($"/api/service-points/by-token/{SeededToken}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(dashboard, p => p.GetProperty("id").GetInt32() == 1);
        Assert.Equal(HttpStatusCode.Gone, sign.StatusCode);
        var scans = 0;
        await factory.WithDbAsync(async db => scans = await db.ScanRecords.CountAsync(r => r.ServicePointId == 1));
        Assert.Equal(1, scans);
    }

    [Fact]
    public async Task Reactivating_brings_the_point_back_to_the_dashboard()
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);
        await admin.PostAsync("/api/admin/service-points/1/deactivate", null);

        var response = await admin.PostAsync("/api/admin/service-points/1/activate", null);
        var dashboard = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/service-points"))!;

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(dashboard, p => p.GetProperty("id").GetInt32() == 1);
    }

    [Theory]
    [InlineData("PUT", "/api/admin/service-points/999", "{\"name\":\"จุดใหม่\",\"location\":\"\",\"cleaningIntervalMinutes\":60}")]
    [InlineData("POST", "/api/admin/service-points/999/regenerate-token", null)]
    [InlineData("POST", "/api/admin/service-points/999/activate", null)]
    public async Task Unknown_point_is_404(string method, string path, string? json)
    {
        using var factory = new FacilityApiFactory();
        var (admin, _, _) = await AdminAsync(factory);

        var response = await SendAsync(admin, method, path, json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<(HttpClient Client, AuthResponseModel Auth, string RefreshToken)> AdminAsync(FacilityApiFactory factory) =>
        AuthApi.LoggedInAsync(factory, "admin", "admin1234");

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string? json = "{}")
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method != "GET" && json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --nologo --filter "FullyQualifiedName~AdminServicePointsTests"`
Expected: FAIL. With no routes mapped, every request gets `404 NotFound`.

- [ ] **Step 3: Store the QR issue date**

In `backend/src/FacilityRealtime.Domain/Entities/ServicePoint.cs`, replace:

```csharp
    public string QrToken { get; set; } = string.Empty;
```

with:

```csharp
    public string QrToken { get; set; } = string.Empty;

    /// <summary>When the current QR Token was issued: shown in the admin table and printed on the QR Sign (ADR facility-0021).</summary>
    public DateTime QrTokenIssuedAt { get; set; } = DateTime.UtcNow;
```

Generate the migration (no MySQL server needed):

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add Phase2cQrTokenIssuedAt \
  --project backend/src/FacilityRealtime.Infrastructure \
  --startup-project backend/src/FacilityRealtime.Api \
  --output-dir Migrations
```

Expected: `Done.` The generated `Up` holds one `AddColumn<DateTime>` named `QrTokenIssuedAt` on `service_points` with `defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)`.

Existing points would otherwise show year 1 as their issue date. Open `backend/src/FacilityRealtime.Infrastructure/Migrations/<timestamp>_Phase2cQrTokenIssuedAt.cs` and add this line at the end of `Up`, after the `AddColumn` call:

```csharp
            // Existing signs were printed from the token they were created with
            migrationBuilder.Sql("UPDATE service_points SET QrTokenIssuedAt = CreatedAt;");
```

- [ ] **Step 4: Write the DTOs and the endpoints, and map them**

Create `backend/src/FacilityRealtime.Api/DTOs/AdminServicePointDtos.cs`:

```csharp
using System;

namespace FacilityRealtime.Api.DTOs;

/// <summary>One row on the service points admin page, deactivated points included (ADR facility-0021).</summary>
public record AdminServicePointDto(
    int Id,
    string Name,
    string Location,
    int CleaningIntervalMinutes,
    bool IsActive,
    string QrToken,
    DateTime QrTokenIssuedAt,
    string ScanUrl,
    DateTime? LastScannedAt,
    string? LastCleanerName
);

/// <summary>Create and edit share one shape; the QR Token is never set by hand.</summary>
public record SaveServicePointRequest(string Name, string? Location, int CleaningIntervalMinutes);
```

Create `backend/src/FacilityRealtime.Api/Endpoints/AdminServicePointEndpoints.cs`:

```csharp
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.DTOs;
using FacilityRealtime.Application.Common;
using FacilityRealtime.Application.ServicePoints;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.Api.Endpoints;

/// <summary>
/// ADR facility-0021: add and edit points, deactivate instead of delete, and regenerate a QR Token.
/// Every route is admin-only (facility-0010). Paging happens in the browser, as the Dashboard decided in #14.
/// </summary>
public static class AdminServicePointEndpoints
{
    public static IEndpointRouteBuilder MapAdminServicePointEndpoints(this IEndpointRouteBuilder app)
    {
        var points = app.MapGroup("/api/admin/service-points").RequireAuthorization(AuthSetup.AdminOnly);
        points.MapGet("", ListAsync);
        points.MapPost("", CreateAsync);
        points.MapPut("/{id:int}", UpdateAsync);
        points.MapPost("/{id:int}/regenerate-token", RegenerateTokenAsync);
        points.MapPost("/{id:int}/deactivate", (int id, AppDbContext db) => SetActiveAsync(id, false, db));
        points.MapPost("/{id:int}/activate", (int id, AppDbContext db) => SetActiveAsync(id, true, db));
        return app;
    }

    private static async Task<IResult> ListAsync(AppDbContext db, ScanUrlBuilder scanUrls)
    {
        var points = await db.ServicePoints.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var rows = new List<AdminServicePointDto>(points.Count);
        foreach (var point in points)
        {
            rows.Add(await ToDtoAsync(point, db, scanUrls));
        }

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateAsync(SaveServicePointRequest request, AppDbContext db, ScanUrlBuilder scanUrls, TimeProvider clock)
    {
        var errors = ServicePointRules.Validate(request.Name, request.Location, request.CleaningIntervalMinutes);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var point = new ServicePoint
        {
            Name = request.Name.Trim(),
            Location = request.Location?.Trim() ?? string.Empty,
            CleaningIntervalMinutes = request.CleaningIntervalMinutes,
            QrToken = ServicePointRules.NewQrToken(),
            QrTokenIssuedAt = now,
            IsActive = true,
            CreatedAt = now,
        };
        db.ServicePoints.Add(point);
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/service-points/{point.Id}", await ToDtoAsync(point, db, scanUrls));
    }

    private static async Task<IResult> UpdateAsync(int id, SaveServicePointRequest request, AppDbContext db, ScanUrlBuilder scanUrls)
    {
        var errors = ServicePointRules.Validate(request.Name, request.Location, request.CleaningIntervalMinutes);
        if (errors.Count > 0)
        {
            return ValidationFailed(errors);
        }

        var point = await db.ServicePoints.FindAsync(id);
        if (point is null)
        {
            return PointNotFound(id);
        }

        point.Name = request.Name.Trim();
        point.Location = request.Location?.Trim() ?? string.Empty;
        point.CleaningIntervalMinutes = request.CleaningIntervalMinutes;
        await db.SaveChangesAsync();

        return Results.Ok(await ToDtoAsync(point, db, scanUrls));
    }

    private static async Task<IResult> RegenerateTokenAsync(int id, AppDbContext db, ScanUrlBuilder scanUrls, TimeProvider clock)
    {
        var point = await db.ServicePoints.FindAsync(id);
        if (point is null)
        {
            return PointNotFound(id);
        }

        // ADR facility-0021: a deactivated point has no sign worth printing, so the button is disabled and the API agrees
        if (!point.IsActive)
        {
            return Results.BadRequest(new { message = "เปิดใช้งานจุดก่อน จึงออก QR ใหม่ได้" });
        }

        // The old sign stops scanning the moment this saves; scan history keeps pointing at the same point (facility-0006)
        point.QrToken = ServicePointRules.NewQrToken();
        point.QrTokenIssuedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();

        return Results.Ok(await ToDtoAsync(point, db, scanUrls));
    }

    private static async Task<IResult> SetActiveAsync(int id, bool isActive, AppDbContext db)
    {
        var point = await db.ServicePoints.FindAsync(id);
        if (point is null)
        {
            return PointNotFound(id);
        }

        point.IsActive = isActive;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<AdminServicePointDto> ToDtoAsync(ServicePoint point, AppDbContext db, ScanUrlBuilder scanUrls)
    {
        var lastScan = await db.ScanRecords
            .AsNoTracking()
            .Where(r => r.ServicePointId == point.Id)
            .OrderByDescending(r => r.ScannedAt)
            .Select(r => new { r.ScannedAt, CleanerName = r.User!.FullName })
            .FirstOrDefaultAsync();

        return new AdminServicePointDto(
            point.Id,
            point.Name,
            point.Location,
            point.CleaningIntervalMinutes,
            point.IsActive,
            point.QrToken,
            point.QrTokenIssuedAt,
            scanUrls.ForToken(point.QrToken),
            lastScan?.ScannedAt,
            lastScan?.CleanerName);
    }

    private static IResult PointNotFound(int id) =>
        Results.NotFound(new { message = $"ไม่พบจุดบริการหมายเลข {id}" });

    private static IResult ValidationFailed(Dictionary<string, string[]> errors) =>
        Results.BadRequest(new { message = string.Join(" · ", errors.Values.SelectMany(v => v)), errors });
}
```

In `backend/src/FacilityRealtime.Api/Program.cs`, replace:

```csharp
app.MapAdminUserEndpoints();
```

with:

```csharp
app.MapAdminUserEndpoints();

// Admin: service points page (ADR facility-0021)
app.MapAdminServicePointEndpoints();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test backend/FacilityRealtime.slnx --nologo`
Expected: PASS. Unit tests 91, API tests 94.

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests/FacilityRealtime.ApiTests/Admin/AdminServicePointsTests.cs
git commit -m "feat(points): add admin routes to add, edit, deactivate and regenerate QR for service points (#16)"
```

---

### Task 5: Frontend data layer, shared pager and local QR image

**Files:**
- Modify: `frontend/package.json` (`qrcode`, `@types/qrcode`)
- Modify: `frontend/src/types/index.ts`
- Create: `frontend/src/services/adminPointsApi.ts`
- Create: `frontend/src/features/admin/logic/pointValidation.ts`, `pointValidation.test.ts`
- Create: `frontend/src/components/pager/pagination.ts`, `pagination.test.ts`, `Pager.tsx`, `pager.css`
- Create: `frontend/src/components/qr/useQrDataUrl.ts`, `QrImage.tsx`, `qrcode.test.ts`

**Interfaces:**
- Consumes: `apiFetch` (plan 2A), `toApiError` (plan 2B), the JSON shapes from Tasks 2 and 4
- Produces:
  - `ServicePointStatus.scanUrl: string`; `AdminServicePoint`; `SaveServicePointInput { name; location; cleaningIntervalMinutes }`
  - `listPointsApi()`, `createPointApi(input)`, `updatePointApi(id, input)`, `regeneratePointTokenApi(id)`, `setPointActiveApi(id, isActive)`
  - `validatePointForm(values: PointFormValues): PointFormErrors`, `toSaveInput(values)`, `INTERVAL_PRESETS`, `NAME_MAX_LENGTH`, `LOCATION_MAX_LENGTH`, `PointFormValues { name; location; interval: string }`
  - `PAGE_SIZES`, `paginate(items, page, pageSize): PageSlice<T>`, `pageList(current, totalPages): Array<number | 'gap'>`, `pageOfIndex(index, pageSize)`
  - `Pager({ page, totalPages, pageSize, start, shown, total, itemLabel, onPageChange, onPageSizeChange })`
  - `QrImage({ value, label, size?, className? })`, `useQrDataUrl(text, size)`

- [ ] **Step 1: Install the QR library**

```bash
npm --prefix frontend install --save qrcode@1.5.4
npm --prefix frontend install --save-dev @types/qrcode@1.5.6
```

Expected: `frontend/package.json` gains `"qrcode": "^1.5.4"` and `"@types/qrcode": "^1.5.6"`.

- [ ] **Step 2: Write the failing tests**

Create `frontend/src/features/admin/logic/pointValidation.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { toSaveInput, validatePointForm } from './pointValidation';

const valid = { name: 'ห้องน้ำชาย ชั้น 2', location: 'อาคาร A ชั้น 2', interval: '60' };

describe('validatePointForm', () => {
  it('accepts a complete point and an empty location', () => {
    expect(validatePointForm(valid)).toEqual({});
    expect(validatePointForm({ ...valid, location: '' })).toEqual({});
  });

  it('requires a name that is not just spaces', () => {
    expect(validatePointForm({ ...valid, name: '   ' }).name).toBe('กรอกชื่อจุด');
  });

  it('limits the name to 150 and the location to 255 characters', () => {
    expect(validatePointForm({ ...valid, name: 'ก'.repeat(150) }).name).toBeUndefined();
    expect(validatePointForm({ ...valid, name: 'ก'.repeat(151) }).name).toBe('ชื่อจุดยาวได้ไม่เกิน 150 ตัวอักษร');
    expect(validatePointForm({ ...valid, location: 'ก'.repeat(256) }).location).toBe('ตำแหน่งยาวได้ไม่เกิน 255 ตัวอักษร');
  });

  it.each(['0', '-5', '1.5', 'abc', '', ' '])('rejects the interval "%s"', (interval) => {
    expect(validatePointForm({ ...valid, interval }).interval).toBe('รอบทำความสะอาดต้องเป็นจำนวนเต็มมากกว่า 0 นาที');
  });

  it.each(['1', '45', ' 120 '])('accepts the interval "%s"', (interval) => {
    expect(validatePointForm({ ...valid, interval }).interval).toBeUndefined();
  });
});

describe('toSaveInput', () => {
  it('trims text and turns the interval into a number', () => {
    expect(toSaveInput({ name: ' จุดทิ้งขยะ ', location: ' โรงอาหารกลาง ', interval: ' 90 ' })).toEqual({
      name: 'จุดทิ้งขยะ',
      location: 'โรงอาหารกลาง',
      cleaningIntervalMinutes: 90
    });
  });
});
```

Create `frontend/src/components/pager/pagination.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { pageList, pageOfIndex, paginate } from './pagination';

const points = Array.from({ length: 26 }, (_, i) => i + 1);

describe('paginate', () => {
  it('cuts 26 points into three pages of 12', () => {
    const first = paginate(points, 1, 12);
    const last = paginate(points, 3, 12);

    expect(first).toMatchObject({ page: 1, totalPages: 3, start: 0, total: 26 });
    expect(first.items).toHaveLength(12);
    expect(last.items).toEqual([25, 26]);
    expect(last.start).toBe(24);
  });

  it('clamps a page past the end to the last page and below 1 to the first', () => {
    expect(paginate(points, 9, 12).page).toBe(3);
    expect(paginate(points, 0, 12).page).toBe(1);
  });

  it('treats an empty list as one empty page', () => {
    expect(paginate([], 4, 12)).toEqual({ items: [], page: 1, totalPages: 1, start: 0, total: 0 });
  });

  it('shows everything on one page at 48 per page', () => {
    expect(paginate(points, 1, 48)).toMatchObject({ totalPages: 1, total: 26 });
  });
});

describe('pageList', () => {
  it('lists every page when there are few', () => {
    expect(pageList(1, 3)).toEqual([1, 2, 3]);
    expect(pageList(1, 1)).toEqual([1]);
  });

  it('keeps first, last and the neighbours of the current page, marking gaps', () => {
    expect(pageList(5, 10)).toEqual([1, 'gap', 4, 5, 6, 'gap', 10]);
    expect(pageList(2, 10)).toEqual([1, 2, 3, 'gap', 10]);
  });
});

describe('pageOfIndex', () => {
  it('finds the page an index falls on', () => {
    expect(pageOfIndex(0, 12)).toBe(1);
    expect(pageOfIndex(11, 12)).toBe(1);
    expect(pageOfIndex(12, 12)).toBe(2);
  });
});
```

Create `frontend/src/components/qr/qrcode.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { toDataURL } from 'qrcode';

describe('qrcode library', () => {
  it('turns a scan URL into a PNG data URL without any network call', async () => {
    const dataUrl = await toDataURL('http://192.168.1.20:5173/scan/3f2b9c1e-8a4d-4c7e-9b1a-2d5e6f7a8b9c', { width: 132, margin: 1 });

    expect(dataUrl.startsWith('data:image/png;base64,')).toBe(true);
  });
});
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `npm --prefix frontend test`
Expected: FAIL with `Failed to resolve import "./pointValidation"` and `Failed to resolve import "./pagination"`. `qrcode.test.ts` already passes, which proves the library and the named import work without a network.

- [ ] **Step 4: Add the types and the API client**

In `frontend/src/types/index.ts`, inside `ServicePointStatus`, replace:

```ts
  qrToken: string;
  currentStatus: PointStatus;
```

with:

```ts
  qrToken: string;
  /** Link inside this point's QR Sign, built from PublicBaseUrl on the server (ADR facility-0003). */
  scanUrl: string;
  currentStatus: PointStatus;
```

and append at the end of the file:

```ts

/** A row on the service points admin page: GET /api/admin/service-points (ADR facility-0021). */
export interface AdminServicePoint {
  id: number;
  name: string;
  location: string;
  cleaningIntervalMinutes: number;
  isActive: boolean;
  qrToken: string;
  qrTokenIssuedAt: string;
  scanUrl: string;
  lastScannedAt: string | null;
  lastCleanerName: string | null;
}

export interface SaveServicePointInput {
  name: string;
  location: string;
  cleaningIntervalMinutes: number;
}
```

Create `frontend/src/services/adminPointsApi.ts`:

```ts
import type { AdminServicePoint, SaveServicePointInput } from '../types';
import { apiFetch } from './authSession';
import { toApiError } from './apiError';

// ADR facility-0010 and facility-0021: every route here is admin-only on the server
const BASE_URL = '/api/admin/service-points';
const JSON_HEADERS = { 'Content-Type': 'application/json' };

export async function listPointsApi(): Promise<AdminServicePoint[]> {
  const res = await apiFetch(BASE_URL);
  if (!res.ok) {
    throw await toApiError(res, 'โหลดรายการจุดบริการไม่สำเร็จ');
  }
  return res.json();
}

export async function createPointApi(input: SaveServicePointInput): Promise<AdminServicePoint> {
  const res = await apiFetch(BASE_URL, { method: 'POST', headers: JSON_HEADERS, body: JSON.stringify(input) });
  if (!res.ok) {
    throw await toApiError(res, 'เพิ่มจุดบริการไม่สำเร็จ');
  }
  return res.json();
}

export async function updatePointApi(id: number, input: SaveServicePointInput): Promise<AdminServicePoint> {
  const res = await apiFetch(`${BASE_URL}/${id}`, { method: 'PUT', headers: JSON_HEADERS, body: JSON.stringify(input) });
  if (!res.ok) {
    throw await toApiError(res, 'บันทึกจุดบริการไม่สำเร็จ');
  }
  return res.json();
}

export async function regeneratePointTokenApi(id: number): Promise<AdminServicePoint> {
  const res = await apiFetch(`${BASE_URL}/${id}/regenerate-token`, { method: 'POST' });
  if (!res.ok) {
    throw await toApiError(res, 'ออก QR ใหม่ไม่สำเร็จ');
  }
  return res.json();
}

export async function setPointActiveApi(id: number, isActive: boolean): Promise<void> {
  const res = await apiFetch(`${BASE_URL}/${id}/${isActive ? 'activate' : 'deactivate'}`, { method: 'POST' });
  if (!res.ok) {
    throw await toApiError(res, isActive ? 'เปิดใช้งานจุดไม่สำเร็จ' : 'ปิดใช้งานจุดไม่สำเร็จ');
  }
}
```

- [ ] **Step 5: Write the validation, the pagination, the pager and the QR image**

Create `frontend/src/features/admin/logic/pointValidation.ts`:

```ts
import type { SaveServicePointInput } from '../../../types';

// The same rules ServicePointRules.cs enforces on the server; the server stays the authority.
export const NAME_MAX_LENGTH = 150;
export const LOCATION_MAX_LENGTH = 255;
/** Shortcut buttons under the interval field, in minutes (ADR facility-0021 prototype). */
export const INTERVAL_PRESETS: readonly number[] = [30, 45, 60, 90, 120];

/** What the drawer holds. The interval stays a string until it passes validation. */
export interface PointFormValues {
  name: string;
  location: string;
  interval: string;
}

export type PointFormErrors = Partial<Record<keyof PointFormValues, string>>;

export function validatePointForm(values: PointFormValues): PointFormErrors {
  const errors: PointFormErrors = {};
  const name = values.name.trim();
  if (!name) {
    errors.name = 'กรอกชื่อจุด';
  } else if (name.length > NAME_MAX_LENGTH) {
    errors.name = `ชื่อจุดยาวได้ไม่เกิน ${NAME_MAX_LENGTH} ตัวอักษร`;
  }
  if (values.location.trim().length > LOCATION_MAX_LENGTH) {
    errors.location = `ตำแหน่งยาวได้ไม่เกิน ${LOCATION_MAX_LENGTH} ตัวอักษร`;
  }
  const interval = values.interval.trim();
  if (!/^\d+$/.test(interval) || Number(interval) <= 0) {
    errors.interval = 'รอบทำความสะอาดต้องเป็นจำนวนเต็มมากกว่า 0 นาที';
  }
  return errors;
}

/** Only call after validatePointForm returned no errors. */
export function toSaveInput(values: PointFormValues): SaveServicePointInput {
  return {
    name: values.name.trim(),
    location: values.location.trim(),
    cleaningIntervalMinutes: Number(values.interval.trim())
  };
}
```

Create `frontend/src/components/pager/pagination.ts`:

```ts
/** Page sizes shared by the Dashboard and the admin tables (ADR facility-0008 amendment, facility-0021). */
export const PAGE_SIZES: readonly number[] = [12, 24, 48];

export interface PageSlice<T> {
  items: T[];
  /** The page actually shown, clamped into 1..totalPages. */
  page: number;
  totalPages: number;
  /** Index of the first item on this page. */
  start: number;
  total: number;
}

/** Slices one page. An out-of-range page is clamped, so filtering never strands the reader on an empty page. */
export function paginate<T>(items: readonly T[], page: number, pageSize: number): PageSlice<T> {
  const total = items.length;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const current = Math.min(Math.max(1, Math.floor(page)), totalPages);
  const start = (current - 1) * pageSize;
  return { items: items.slice(start, start + pageSize), page: current, totalPages, start, total };
}

/** Page buttons: first, last, current and its neighbours, with 'gap' where pages are skipped. */
export function pageList(current: number, totalPages: number): Array<number | 'gap'> {
  const pages = [...new Set([1, totalPages, current - 1, current, current + 1])]
    .filter((n) => n >= 1 && n <= totalPages)
    .sort((a, b) => a - b);
  const result: Array<number | 'gap'> = [];
  pages.forEach((n, i) => {
    if (i > 0 && n - pages[i - 1] > 1) {
      result.push('gap');
    }
    result.push(n);
  });
  return result;
}

/** The page an item at this index lands on. */
export function pageOfIndex(index: number, pageSize: number): number {
  return Math.floor(index / pageSize) + 1;
}
```

Create `frontend/src/components/pager/pager.css`:

```css
/* Page bar shared by the Dashboard and the admin tables (ADR facility-0008 amendment, facility-0021) */
.pager {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px 20px;
  margin-top: 14px;
  padding: 10px 16px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  background: var(--bg-card);
}

.pager-info { color: var(--text-secondary); font-size: 13px; font-variant-numeric: tabular-nums; }
.pager-info strong { color: var(--text-primary); font-weight: 600; }
.pager-pages { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }

.pager-btn {
  min-width: 36px;
  height: 36px;
  padding: 0 12px;
  border: 1px solid var(--border-subtle);
  border-radius: 9px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  font: inherit;
  font-size: 13px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  cursor: pointer;
}

.pager-btn:hover:not(:disabled) { border-color: var(--color-brand); }
.pager-btn:disabled { opacity: 0.4; cursor: default; }
.pager-btn:focus-visible { outline: 2px solid var(--color-brand); outline-offset: 2px; }
.pager-btn.is-active { border-color: var(--color-brand); background: var(--color-brand); color: #082f49; }
.pager-gap { padding: 0 4px; color: var(--text-muted); }

.pager-size { display: flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: 13px; }

.pager-size select {
  padding: 6px 10px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-sm);
  background: var(--bg-secondary);
  color: var(--text-primary);
  font: inherit;
  font-size: 13px;
}

@media (max-width: 640px) {
  .pager { justify-content: center; }
  .pager-info { width: 100%; text-align: center; }
}
```

Create `frontend/src/components/pager/Pager.tsx`:

```tsx
import React from 'react';
import { PAGE_SIZES, pageList } from './pagination';
import './pager.css';

interface PagerProps {
  page: number;
  totalPages: number;
  pageSize: number;
  start: number;
  shown: number;
  total: number;
  /** Counting word for the items, for example "จุด". */
  itemLabel: string;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

export const Pager: React.FC<PagerProps> = ({
  page,
  totalPages,
  pageSize,
  start,
  shown,
  total,
  itemLabel,
  onPageChange,
  onPageSizeChange
}) => (
  <div className="pager">
    <div className="pager-info">
      {total === 0 ? (
        `ไม่พบ${itemLabel}ที่ตรงกับการค้นหา`
      ) : (
        <>
          แสดง <strong>{start + 1}–{start + shown}</strong> จาก <strong>{total}</strong> {itemLabel}
        </>
      )}
    </div>

    <div className="pager-pages">
      <button type="button" className="pager-btn" onClick={() => onPageChange(page - 1)} disabled={page <= 1}>
        ‹ ก่อนหน้า
      </button>
      {pageList(page, totalPages).map((item, index) =>
        item === 'gap' ? (
          <span key={`gap-${index}`} className="pager-gap">…</span>
        ) : (
          <button
            key={item}
            type="button"
            className={`pager-btn${item === page ? ' is-active' : ''}`}
            aria-current={item === page ? 'page' : undefined}
            aria-label={`หน้า ${item}`}
            onClick={() => onPageChange(item)}
          >
            {item}
          </button>
        )
      )}
      <button type="button" className="pager-btn" onClick={() => onPageChange(page + 1)} disabled={page >= totalPages}>
        ถัดไป ›
      </button>
    </div>

    <label className="pager-size" htmlFor="pager-size">
      ต่อหน้า
      <select id="pager-size" value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))}>
        {PAGE_SIZES.map((size) => (
          <option key={size} value={size}>
            {size}
          </option>
        ))}
      </select>
    </label>
  </div>
);
```

Create `frontend/src/components/qr/useQrDataUrl.ts`:

```ts
import { useEffect, useState } from 'react';
import { toDataURL } from 'qrcode';

interface QrState {
  text: string;
  dataUrl: string | null;
  failed: boolean;
}

/** Draws the QR in the browser (ADR facility-0021), so printing a sign needs no outside service. */
export function useQrDataUrl(text: string, size: number): { dataUrl: string | null; failed: boolean } {
  const [state, setState] = useState<QrState>({ text: '', dataUrl: null, failed: false });

  useEffect(() => {
    let cancelled = false;
    toDataURL(text, { width: size, margin: 1, errorCorrectionLevel: 'M', color: { dark: '#0f172a', light: '#ffffff' } })
      .then((dataUrl) => {
        if (!cancelled) setState({ text, dataUrl, failed: false });
      })
      .catch(() => {
        if (!cancelled) setState({ text, dataUrl: null, failed: true });
      });
    return () => {
      cancelled = true;
    };
  }, [text, size]);

  // A result for an older text is never shown for a newer one
  return state.text === text ? { dataUrl: state.dataUrl, failed: state.failed } : { dataUrl: null, failed: false };
}
```

Create `frontend/src/components/qr/QrImage.tsx`:

```tsx
import React from 'react';
import { useQrDataUrl } from './useQrDataUrl';

interface QrImageProps {
  value: string;
  label: string;
  size?: number;
  className?: string;
}

export const QrImage: React.FC<QrImageProps> = ({ value, label, size = 200, className }) => {
  const { dataUrl, failed } = useQrDataUrl(value, size);

  if (failed) {
    return (
      <div className={className} style={{ width: size, height: size, display: 'grid', placeItems: 'center' }} role="img" aria-label={label}>
        สร้าง QR ไม่สำเร็จ
      </div>
    );
  }

  if (!dataUrl) {
    return <div className={className} style={{ width: size, height: size }} aria-busy="true" />;
  }

  return <img src={dataUrl} width={size} height={size} alt={label} className={className} />;
};
```

- [ ] **Step 6: Run the tests, the type check and the linter**

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  54 passed (54)`.

Run: `npm --prefix frontend run build` and `npm --prefix frontend run lint`
Expected: `built in`; no line of lint output reports an error.

- [ ] **Step 7: Commit**

```bash
git add frontend/package.json frontend/package-lock.json frontend/src/types/index.ts frontend/src/services/adminPointsApi.ts frontend/src/features/admin/logic/pointValidation.ts frontend/src/features/admin/logic/pointValidation.test.ts frontend/src/components
git commit -m "feat(points-web): add points API client, field rules, shared pager and local QR image (#16)"
```

---

### Task 6: Service points page, drawer, regenerate dialog and print sheet

The repo has no React component test setup: the list, date and tab logic are unit-tested, the compiler drives the page's red step, and Task 8 checks the screens against the confirmed prototype.

**Files:**
- Create: `frontend/src/features/admin/logic/pointList.ts`, `pointList.test.ts`
- Create: `frontend/src/features/admin/logic/formatDate.ts`, `formatDate.test.ts`
- Replace: `frontend/src/features/admin/adminTabs.ts`, `adminTabs.test.ts`
- Create: `frontend/src/features/admin/printSheet.css`
- Create: `frontend/src/features/admin/hooks/usePointsAdminPage.ts`, `usePointForm.ts`, `useRegenerateQr.ts`
- Create: `frontend/src/features/admin/components/PointsAdminPage.tsx`, `PointFormDrawer.tsx`, `RegenerateQrDialog.tsx`, `PrintSheet.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: Task 5 modules; `AdminLayout`, `Toast`, `useToast`, `admin.css` (plan 2B)
- Produces:
  - `LABELS_PER_PAGE = 6`, `visiblePoints(points, search, hideInactive)`, `upsertPoint(points, point)`, `chunk(items, size)`
  - `parseApiDate(value)`, `formatThaiDate(value)`, `formatThaiDateTime(value)`, `shortToken(token)`
  - `AdminTabKey = 'points' | 'accounts'` with `points` first, so `/admin` opens the service points page
  - `PointsAdminPage()` rendered at `/admin/points`

- [ ] **Step 1: Write the failing logic tests**

Create `frontend/src/features/admin/logic/pointList.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import type { AdminServicePoint } from '../../../types';
import { chunk, upsertPoint, visiblePoints } from './pointList';

function point(id: number, name: string, location = 'อาคาร A', isActive = true): AdminServicePoint {
  return {
    id,
    name,
    location,
    cleaningIntervalMinutes: 60,
    isActive,
    qrToken: `token-${id}`,
    qrTokenIssuedAt: '2026-09-13T03:00:00Z',
    scanUrl: `http://192.168.1.20:5173/scan/token-${id}`,
    lastScannedAt: null,
    lastCleanerName: null
  };
}

const points = [
  point(1, 'ห้องน้ำหญิง ชั้น 1'),
  point(2, 'โรงอาหาร โซนล้างจาน', 'โรงอาหารกลาง'),
  point(3, 'ห้องน้ำชาย ชั้น 1', 'อาคาร A', false),
  point(4, 'ห้องประชุมใหญ่', 'อาคาร B ชั้น 2')
];

describe('visiblePoints', () => {
  it('sorts by Thai name, where a leading vowel such as โ does not count', () => {
    expect(visiblePoints(points, '', false).map((p) => p.id)).toEqual([2, 3, 1, 4]);
  });

  it('keeps the same order when a point is deactivated, so rows never jump pages', () => {
    const toggled = upsertPoint(points, { ...points[0], isActive: false });

    expect(visiblePoints(toggled, '', false).map((p) => p.id)).toEqual([2, 3, 1, 4]);
  });

  it('hides deactivated points when asked', () => {
    expect(visiblePoints(points, '', true).map((p) => p.id)).toEqual([2, 1, 4]);
  });

  it('searches name and location', () => {
    expect(visiblePoints(points, 'ห้องน้ำ', false).map((p) => p.id)).toEqual([3, 1]);
    expect(visiblePoints(points, 'อาคาร b', false).map((p) => p.id)).toEqual([4]);
  });
});

describe('upsertPoint', () => {
  it('replaces a point with the same id in place and appends a new one', () => {
    const renamed = upsertPoint(points, { ...points[1], name: 'โรงอาหาร โซน A' });
    const added = upsertPoint(points, point(5, 'ห้องละหมาด'));

    expect(renamed[1].name).toBe('โรงอาหาร โซน A');
    expect(renamed).toHaveLength(4);
    expect(added.map((p) => p.id)).toEqual([1, 2, 3, 4, 5]);
  });
});

describe('chunk', () => {
  it('splits 13 signs into A4 pages of 6', () => {
    expect(chunk(Array.from({ length: 13 }, (_, i) => i), 6).map((page) => page.length)).toEqual([6, 6, 1]);
    expect(chunk([], 6)).toEqual([]);
  });
});
```

Create `frontend/src/features/admin/logic/formatDate.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { formatThaiDate, formatThaiDateTime, parseApiDate, shortToken } from './formatDate';

describe('parseApiDate', () => {
  it('reads a value without a zone as UTC', () => {
    expect(parseApiDate('2026-09-13T17:30:00').getTime()).toBe(Date.UTC(2026, 8, 13, 17, 30));
    expect(parseApiDate('2026-09-13T17:30:00.1234567').getTime()).toBe(Date.UTC(2026, 8, 13, 17, 30, 0, 123));
  });

  it('respects an explicit zone', () => {
    expect(parseApiDate('2026-09-13T17:30:00Z').getTime()).toBe(Date.UTC(2026, 8, 13, 17, 30));
    expect(parseApiDate('2026-09-14T00:30:00+07:00').getTime()).toBe(Date.UTC(2026, 8, 13, 17, 30));
  });
});

describe('Thai formatting in Bangkok time', () => {
  it('moves 17:30 UTC to the next Bangkok day and uses the Buddhist year', () => {
    const date = formatThaiDate('2026-09-13T17:30:00');

    expect(date).toContain('14');
    expect(date).toContain('ก.ย.');
    expect(date).toContain('2569');
  });

  it('shows the Bangkok clock time of a scan', () => {
    expect(formatThaiDateTime('2026-09-13T17:30:00Z')).toContain('00:30');
  });
});

describe('shortToken', () => {
  it('keeps the first 8 characters', () => {
    expect(shortToken('3f2b9c1e-8a4d-4c7e-9b1a-2d5e6f7a8b9c')).toBe('3f2b9c1e');
  });
});
```

Replace the whole of `frontend/src/features/admin/adminTabs.test.ts` with:

```ts
import { describe, expect, it } from 'vitest';
import { ADMIN_TABS, adminTabForPath } from './adminTabs';

describe('adminTabForPath', () => {
  it('finds the tab for its own path and anything below it', () => {
    expect(adminTabForPath('/admin/points')?.key).toBe('points');
    expect(adminTabForPath('/admin/accounts')?.key).toBe('accounts');
    expect(adminTabForPath('/admin/accounts/42')?.key).toBe('accounts');
  });

  it('does not match a path that only starts with the same letters', () => {
    expect(adminTabForPath('/admin/accountsx')).toBeUndefined();
  });

  it('returns undefined for the bare admin path so the router can fall back to the first tab', () => {
    expect(adminTabForPath('/admin')).toBeUndefined();
    expect(ADMIN_TABS[0].path).toBe('/admin/points');
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm --prefix frontend test`
Expected: FAIL with `Failed to resolve import "./pointList"` and `Failed to resolve import "./formatDate"`, and `adminTabs.test.ts` fails because `adminTabForPath('/admin/points')` is `undefined`.

- [ ] **Step 3: Write the logic and the new tab list**

Create `frontend/src/features/admin/logic/pointList.ts`:

```ts
import type { AdminServicePoint } from '../../../types';

/** ADR facility-0021: A4 portrait, 6 QR Signs per page. */
export const LABELS_PER_PAGE = 6;

/** Rows sort by name only, so deactivating or regenerating never moves a row to another page (ADR facility-0021). */
export function visiblePoints(
  points: readonly AdminServicePoint[],
  search: string,
  hideInactive: boolean
): AdminServicePoint[] {
  const query = search.trim().toLowerCase();
  return points
    .filter((p) => !hideInactive || p.isActive)
    .filter((p) => !query || p.name.toLowerCase().includes(query) || p.location.toLowerCase().includes(query))
    .sort((a, b) => a.name.localeCompare(b.name, 'th'));
}

export function upsertPoint(points: readonly AdminServicePoint[], point: AdminServicePoint): AdminServicePoint[] {
  const index = points.findIndex((p) => p.id === point.id);
  if (index < 0) {
    return [...points, point];
  }
  const next = [...points];
  next[index] = point;
  return next;
}

export function chunk<T>(items: readonly T[], size: number): T[][] {
  const pages: T[][] = [];
  for (let i = 0; i < items.length; i += size) {
    pages.push(items.slice(i, i + size));
  }
  return pages;
}
```

Create `frontend/src/features/admin/logic/formatDate.ts`:

```ts
const HAS_ZONE = /(?:[zZ]|[+-]\d{2}:?\d{2})$/;

/** The API stores UTC. Values read back from MySQL arrive without a zone marker, so treat those as UTC too. */
export function parseApiDate(value: string): Date {
  return new Date(HAS_ZONE.test(value) ? value : `${value}Z`);
}

/** Date for a QR Sign or the admin table, in Thai and in Bangkok time, for example "14 ก.ย. 2569". */
export function formatThaiDate(value: string): string {
  return parseApiDate(value).toLocaleDateString('th-TH', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'Asia/Bangkok'
  });
}

/** Date and time of a scan, in Thai and in Bangkok time. */
export function formatThaiDateTime(value: string): string {
  return parseApiDate(value).toLocaleString('th-TH', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Bangkok'
  });
}

/** First 8 characters of a QR Token: enough to tell signs apart, too short to rebuild the link. */
export function shortToken(token: string): string {
  return token.slice(0, 8);
}
```

Replace the whole of `frontend/src/features/admin/adminTabs.ts` with:

```ts
export type AdminTabKey = 'points' | 'accounts';

export interface AdminTab {
  key: AdminTabKey;
  path: string;
  label: string;
}

/** The Admin menu: service points (ADR facility-0021) and accounts (facility-0020) share it. */
export const ADMIN_TABS: readonly AdminTab[] = [
  { key: 'points', path: '/admin/points', label: 'จุดบริการ' },
  { key: 'accounts', path: '/admin/accounts', label: 'บัญชีผู้ใช้' }
];

/** The tab a path belongs to, or undefined when the path is not one of the admin pages. */
export function adminTabForPath(path: string): AdminTab | undefined {
  return ADMIN_TABS.find((tab) => path === tab.path || path.startsWith(`${tab.path}/`));
}
```

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  65 passed (65)`.

- [ ] **Step 4: Route the points tab to its page**

In `frontend/src/App.tsx`, replace:

```tsx
import { AccountsPage } from './features/admin/components/AccountsPage';
```

with:

```tsx
import { AccountsPage } from './features/admin/components/AccountsPage';
import { PointsAdminPage } from './features/admin/components/PointsAdminPage';
```

and replace:

```tsx
              <AccountsPage currentUserId={currentUser!.id} />
```

with:

```tsx
              {adminTab.key === 'points' ? <PointsAdminPage /> : <AccountsPage currentUserId={currentUser!.id} />}
```

Run: `npm --prefix frontend run build`
Expected: FAIL with `Cannot find module './features/admin/components/PointsAdminPage'`.

- [ ] **Step 5: Write the print styles, the hooks and the components**

Create `frontend/src/features/admin/printSheet.css`:

```css
/* QR Sign print sheet (ADR facility-0021): A4 portrait, 6 signs per page, always dark ink on white paper */
.print-sheet {
  position: fixed;
  inset: 0;
  z-index: 70;
  overflow-y: auto;
  padding: 24px 16px;
  background: #cbd5e1;
  color: #0f172a;
}

.print-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  max-width: 210mm;
  margin: 0 auto 14px;
}

.print-toolbar-actions { display: flex; gap: 8px; }
.print-sheet .admin-btn-ghost { color: #334155; }
.print-empty { max-width: 210mm; margin: 0 auto; }

.print-page {
  box-sizing: border-box;
  width: 100%;
  max-width: 210mm;
  margin: 0 auto 16px;
  padding: 12mm;
  background: #ffffff;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.25);
}

.print-page-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 6mm;
  padding-bottom: 2mm;
  border-bottom: 2px solid #0f172a;
  color: #475569;
  font-size: 11px;
}

.print-page-head b { color: #0f172a; font-size: 14px; }
.print-labels { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 5mm; }

.print-label {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: 4mm;
  min-height: 78mm;
  padding: 5mm;
  border: 1.5px solid #0f172a;
  border-radius: 3mm;
  break-inside: avoid;
}

.print-label-name { font-size: 20px; font-weight: 800; line-height: 1.2; }
.print-label-location { margin-top: 1mm; color: #475569; font-size: 13px; }
.print-label-cta { margin-top: 4mm; font-size: 13px; font-weight: 600; }
.print-label-meta { margin-top: 3mm; color: #64748b; font-size: 10px; font-variant-numeric: tabular-nums; }
.print-label-url { margin-top: 1mm; color: #94a3b8; font-family: ui-monospace, Menlo, monospace; font-size: 8px; word-break: break-all; }

@media (max-width: 640px) {
  .print-labels { grid-template-columns: 1fr; }
}

@page {
  size: A4 portrait;
  margin: 0;
}

@media print {
  body * { visibility: hidden; }
  .print-sheet, .print-sheet * { visibility: visible; }
  .print-sheet { position: absolute; top: 0; left: 0; width: 100%; padding: 0; overflow: visible; background: #ffffff; }
  .print-screen-only { display: none; }
  .print-page { width: 210mm; max-width: none; height: 297mm; margin: 0; box-shadow: none; break-after: page; }
  .print-page:last-child { break-after: auto; }
  .print-labels { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
```

Create `frontend/src/features/admin/hooks/usePointsAdminPage.ts`:

```ts
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { AdminServicePoint } from '../../../types';
import { listPointsApi, setPointActiveApi } from '../../../services/adminPointsApi';
import { pageOfIndex, paginate } from '../../../components/pager/pagination';
import { upsertPoint, visiblePoints } from '../logic/pointList';
import { useToast } from './useToast';

export type PointDrawerState = { mode: 'create' } | { mode: 'edit'; point: AdminServicePoint } | null;

export function usePointsAdminPage() {
  const [points, setPoints] = useState<AdminServicePoint[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearchValue] = useState<string>('');
  const [hideInactive, setHideInactiveValue] = useState<boolean>(false);
  const [page, setPage] = useState<number>(1);
  const [pageSize, setPageSizeValue] = useState<number>(12);
  const [drawer, setDrawer] = useState<PointDrawerState>(null);
  const [regenTarget, setRegenTarget] = useState<AdminServicePoint | null>(null);
  const [printList, setPrintList] = useState<AdminServicePoint[] | null>(null);
  const [flashId, setFlashId] = useState<number | null>(null);
  const flashTimer = useRef<number | undefined>(undefined);
  const { toast, show: showToast, dismiss: dismissToast } = useToast();

  const load = useCallback(async () => {
    try {
      setPoints(await listPointsApi());
      setLoadError(null);
    } catch (err: unknown) {
      setLoadError(err instanceof Error ? err.message : 'โหลดรายการจุดบริการไม่สำเร็จ');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    return () => window.clearTimeout(flashTimer.current);
  }, [load]);

  const visible = useMemo(() => visiblePoints(points, search, hideInactive), [points, search, hideInactive]);
  const slice = useMemo(() => paginate(visible, page, pageSize), [visible, page, pageSize]);
  const activeCount = useMemo(() => points.filter((p) => p.isActive).length, [points]);

  const flash = useCallback((id: number) => {
    window.clearTimeout(flashTimer.current);
    setFlashId(id);
    flashTimer.current = window.setTimeout(() => setFlashId(null), 1500);
  }, []);

  // ADR facility-0021: searching, hiding deactivated points or changing the page size goes back to page 1
  const setSearch = useCallback((value: string) => {
    setSearchValue(value);
    setPage(1);
  }, []);

  const setHideInactive = useCallback((value: boolean) => {
    setHideInactiveValue(value);
    setPage(1);
  }, []);

  const setPageSize = useCallback((value: number) => {
    setPageSizeValue(value);
    setPage(1);
  }, []);

  const openPrint = useCallback((list: AdminServicePoint[]) => setPrintList(list), []);

  const handleSaved = (saved: AdminServicePoint, wasCreate: boolean) => {
    const next = upsertPoint(points, saved);
    setPoints(next);
    setDrawer(null);
    // ADR facility-0021: after adding or renaming, follow the row to the page it sorted onto
    const index = visiblePoints(next, search, hideInactive).findIndex((p) => p.id === saved.id);
    if (index >= 0) {
      setPage(pageOfIndex(index, pageSize));
    }
    flash(saved.id);
    if (wasCreate) {
      showToast(`เพิ่ม ${saved.name} แล้ว — พิมพ์ป้ายไปติดได้เลย`, { label: 'พิมพ์ป้าย', run: () => openPrint([saved]) });
    } else {
      showToast(`บันทึก ${saved.name} แล้ว`);
    }
  };

  const toggleActive = async (point: AdminServicePoint) => {
    const next = !point.isActive;
    try {
      await setPointActiveApi(point.id, next);
      const updated = { ...point, isActive: next };
      setPoints((prev) => upsertPoint(prev, updated));
      flash(point.id);
      showToast(
        next
          ? `เปิดใช้งาน ${point.name} แล้ว — กลับขึ้น Dashboard`
          : `ปิดใช้งาน ${point.name} แล้ว — หายจาก Dashboard ประวัติสแกนยังอยู่`,
        { label: 'เลิกทำ', run: () => void toggleActive(updated) }
      );
    } catch (err: unknown) {
      showToast(err instanceof Error ? err.message : 'เปลี่ยนสถานะจุดไม่สำเร็จ');
    }
  };

  const handleRegenerated = (updated: AdminServicePoint) => {
    setPoints((prev) => upsertPoint(prev, updated));
    setRegenTarget(null);
    flash(updated.id);
    showToast(`ออก QR ใหม่ให้ ${updated.name} แล้ว — ป้ายเก่าใช้ไม่ได้แล้ว`, {
      label: 'พิมพ์ป้ายใหม่',
      run: () => openPrint([updated])
    });
  };

  return {
    rows: slice.items,
    page: slice.page,
    totalPages: slice.totalPages,
    start: slice.start,
    total: slice.total,
    pageSize,
    goToPage: setPage,
    setPageSize,
    totalPointCount: points.length,
    activeCount,
    inactiveCount: points.length - activeCount,
    isLoading,
    loadError,
    search,
    setSearch,
    hideInactive,
    setHideInactive,
    drawer,
    openCreate: () => setDrawer({ mode: 'create' }),
    openEdit: (point: AdminServicePoint) => setDrawer({ mode: 'edit', point }),
    closeDrawer: () => setDrawer(null),
    handleSaved,
    regenTarget,
    openRegen: (point: AdminServicePoint) => setRegenTarget(point),
    closeRegen: () => setRegenTarget(null),
    handleRegenerated,
    toggleActive,
    printList,
    openPrint,
    // ADR facility-0021: "print all" means every active point, not only the page on screen
    openPrintAllActive: () => openPrint(visiblePoints(points, '', true)),
    closePrint: () => setPrintList(null),
    flashId,
    toast,
    dismissToast
  };
}
```

Create `frontend/src/features/admin/hooks/usePointForm.ts`:

```ts
import { useState } from 'react';
import type { FormEvent } from 'react';
import type { AdminServicePoint } from '../../../types';
import { createPointApi, updatePointApi } from '../../../services/adminPointsApi';
import { toSaveInput, validatePointForm } from '../logic/pointValidation';
import type { PointFormErrors } from '../logic/pointValidation';

/** Create when initialPoint is null, otherwise edit it. The QR Token is never typed in (ADR facility-0021). */
export function usePointForm(initialPoint: AdminServicePoint | null, onSaved: (point: AdminServicePoint, wasCreate: boolean) => void) {
  const [name, setName] = useState<string>(initialPoint?.name ?? '');
  const [location, setLocation] = useState<string>(initialPoint?.location ?? '');
  const [interval, setInterval] = useState<string>(String(initialPoint?.cleaningIntervalMinutes ?? 60));
  const [errors, setErrors] = useState<PointFormErrors>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState<boolean>(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const values = { name, location, interval };
    const nextErrors = validatePointForm(values);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    setIsSaving(true);
    setSubmitError(null);
    try {
      const input = toSaveInput(values);
      const saved = initialPoint ? await updatePointApi(initialPoint.id, input) : await createPointApi(input);
      onSaved(saved, initialPoint === null);
    } catch (err: unknown) {
      setSubmitError(err instanceof Error ? err.message : 'บันทึกจุดบริการไม่สำเร็จ');
    } finally {
      setIsSaving(false);
    }
  };

  return {
    isEdit: initialPoint !== null,
    name,
    setName,
    location,
    setLocation,
    interval,
    setInterval,
    applyPreset: (minutes: number) => setInterval(String(minutes)),
    errors,
    submitError,
    isSaving,
    submit
  };
}
```

Create `frontend/src/features/admin/hooks/useRegenerateQr.ts`:

```ts
import { useState } from 'react';
import type { AdminServicePoint } from '../../../types';
import { regeneratePointTokenApi } from '../../../services/adminPointsApi';

export function useRegenerateQr(point: AdminServicePoint, onDone: (updated: AdminServicePoint) => void) {
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const confirm = async () => {
    setIsSaving(true);
    setError(null);
    try {
      onDone(await regeneratePointTokenApi(point.id));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'ออก QR ใหม่ไม่สำเร็จ');
    } finally {
      setIsSaving(false);
    }
  };

  return { isSaving, error, confirm };
}
```

Create `frontend/src/features/admin/components/PointsAdminPage.tsx`:

```tsx
import React from 'react';
import { Pencil, Plus, Power, Printer, QrCode } from 'lucide-react';
import { Pager } from '../../../components/pager/Pager';
import { usePointsAdminPage } from '../hooks/usePointsAdminPage';
import { formatThaiDate, formatThaiDateTime, shortToken } from '../logic/formatDate';
import { PointFormDrawer } from './PointFormDrawer';
import { PrintSheet } from './PrintSheet';
import { RegenerateQrDialog } from './RegenerateQrDialog';
import { Toast } from './Toast';

export const PointsAdminPage: React.FC = () => {
  const page = usePointsAdminPage();

  return (
    <>
      <div className="admin-title">
        <h1>จุดบริการ</h1>
        <span className="admin-count">
          {page.activeCount} จุดเปิดใช้งาน · {page.inactiveCount} ปิดใช้งาน
        </span>
      </div>

      <div className="admin-toolbar">
        <input
          id="point-search"
          className="admin-search"
          type="search"
          value={page.search}
          onChange={(e) => page.setSearch(e.target.value)}
          placeholder="ค้นหาชื่อจุดหรือตำแหน่ง"
          aria-label="ค้นหาจุดบริการ"
        />
        <label className="admin-check" htmlFor="point-hide-inactive">
          <input
            id="point-hide-inactive"
            type="checkbox"
            checked={page.hideInactive}
            onChange={(e) => page.setHideInactive(e.target.checked)}
          />
          ซ่อนจุดที่ปิดใช้งาน
        </label>
        <div className="admin-spacer" />
        <button type="button" className="admin-btn" onClick={page.openPrintAllActive}>
          <Printer size={16} /> พิมพ์ป้ายทั้งหมด
        </button>
        <button type="button" className="admin-btn admin-btn-primary" onClick={page.openCreate}>
          <Plus size={16} /> เพิ่มจุดบริการ
        </button>
      </div>

      {page.loadError && <div className="admin-error">{page.loadError}</div>}

      <div className="admin-table-wrap">
        <table className="admin-table" style={{ minWidth: '880px' }}>
          <thead>
            <tr>
              <th>จุดบริการ</th>
              <th>รอบ</th>
              <th>สแกนล่าสุด</th>
              <th>QR Token</th>
              <th>สถานะ</th>
              <th aria-label="การทำงาน" />
            </tr>
          </thead>
          <tbody>
            {page.isLoading ? (
              <tr>
                <td colSpan={6} className="admin-empty">กำลังโหลดรายการจุดบริการ...</td>
              </tr>
            ) : page.rows.length === 0 ? (
              <tr>
                <td colSpan={6} className="admin-empty">
                  {page.totalPointCount === 0 ? 'ยังไม่มีจุดบริการ กด “เพิ่มจุดบริการ” เพื่อเริ่ม' : 'ไม่พบจุดที่ตรงกับการค้นหา'}
                </td>
              </tr>
            ) : (
              page.rows.map((point) => {
                const rowClass = [point.isActive ? '' : 'is-inactive', page.flashId === point.id ? 'is-flash' : '']
                  .filter(Boolean)
                  .join(' ');
                return (
                  <tr key={point.id} className={rowClass}>
                    <td>
                      <div className="admin-strong">{point.name}</div>
                      <div className="admin-muted">{point.location}</div>
                    </td>
                    <td style={{ fontVariantNumeric: 'tabular-nums' }}>{point.cleaningIntervalMinutes} นาที</td>
                    <td className="admin-muted">
                      {point.lastScannedAt
                        ? `${formatThaiDateTime(point.lastScannedAt)} · ${point.lastCleanerName ?? ''}`
                        : 'ยังไม่เคยสแกน'}
                    </td>
                    <td>
                      <div className="admin-mono">{shortToken(point.qrToken)}…</div>
                      <div className="admin-muted">ออกเมื่อ {formatThaiDate(point.qrTokenIssuedAt)}</div>
                    </td>
                    <td>
                      {point.isActive ? (
                        <span className="admin-badge admin-badge-on">● เปิดใช้งาน</span>
                      ) : (
                        <span className="admin-badge admin-badge-off">○ ปิดใช้งาน</span>
                      )}
                    </td>
                    <td className="admin-actions">
                      <button type="button" className="admin-btn admin-btn-ghost admin-btn-sm" onClick={() => page.openEdit(point)}>
                        <Pencil size={14} /> แก้ไข
                      </button>
                      <button
                        type="button"
                        className="admin-btn admin-btn-ghost admin-btn-sm"
                        onClick={() => page.openPrint([point])}
                        disabled={!point.isActive}
                        title={point.isActive ? undefined : 'เปิดใช้งานก่อนจึงพิมพ์ป้ายได้'}
                      >
                        <Printer size={14} /> พิมพ์ป้าย
                      </button>
                      <button
                        type="button"
                        className="admin-btn admin-btn-ghost admin-btn-sm"
                        onClick={() => page.openRegen(point)}
                        disabled={!point.isActive}
                        title={point.isActive ? undefined : 'เปิดใช้งานก่อนจึงออก QR ใหม่ได้'}
                      >
                        <QrCode size={14} /> ออก QR ใหม่
                      </button>
                      <button type="button" className="admin-btn admin-btn-ghost admin-btn-sm" onClick={() => void page.toggleActive(point)}>
                        <Power size={14} /> {point.isActive ? 'ปิดใช้งาน' : 'เปิดใช้งาน'}
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {!page.isLoading && (
        <Pager
          page={page.page}
          totalPages={page.totalPages}
          pageSize={page.pageSize}
          start={page.start}
          shown={page.rows.length}
          total={page.total}
          itemLabel="จุด"
          onPageChange={page.goToPage}
          onPageSizeChange={page.setPageSize}
        />
      )}

      {page.drawer && (
        <PointFormDrawer
          key={page.drawer.mode === 'edit' ? page.drawer.point.id : 'create'}
          initialPoint={page.drawer.mode === 'edit' ? page.drawer.point : null}
          onClose={page.closeDrawer}
          onSaved={page.handleSaved}
        />
      )}

      {page.regenTarget && (
        <RegenerateQrDialog point={page.regenTarget} onClose={page.closeRegen} onDone={page.handleRegenerated} />
      )}

      {page.printList && <PrintSheet points={page.printList} onClose={page.closePrint} />}

      <Toast toast={page.toast} onDismiss={page.dismissToast} />
    </>
  );
};
```

Create `frontend/src/features/admin/components/PointFormDrawer.tsx`:

```tsx
import React from 'react';
import type { AdminServicePoint } from '../../../types';
import { usePointForm } from '../hooks/usePointForm';
import { INTERVAL_PRESETS } from '../logic/pointValidation';
import { formatThaiDate } from '../logic/formatDate';

interface PointFormDrawerProps {
  initialPoint: AdminServicePoint | null;
  onClose: () => void;
  onSaved: (point: AdminServicePoint, wasCreate: boolean) => void;
}

export const PointFormDrawer: React.FC<PointFormDrawerProps> = ({ initialPoint, onClose, onSaved }) => {
  const form = usePointForm(initialPoint, onSaved);

  return (
    <>
      <div className="admin-scrim" onClick={onClose} aria-hidden="true" />
      <aside className="admin-drawer" aria-label={form.isEdit ? 'แก้ไขจุดบริการ' : 'เพิ่มจุดบริการ'}>
        <form className="admin-drawer-form" onSubmit={form.submit} noValidate>
          <div>
            <h2>{initialPoint ? `แก้ไข ${initialPoint.name}` : 'เพิ่มจุดบริการ'}</h2>
            <p className="admin-drawer-sub">
              {form.isEdit
                ? 'ชื่อและรอบใหม่ขึ้นบน Dashboard ภายใน 30 วินาที'
                : 'QR Token จะถูกสุ่มให้อัตโนมัติเมื่อบันทึก'}
            </p>
          </div>

          <div className={`admin-field${form.errors.name ? ' is-invalid' : ''}`}>
            <label htmlFor="point-name">ชื่อจุด</label>
            <input
              id="point-name"
              value={form.name}
              onChange={(e) => form.setName(e.target.value)}
              autoComplete="off"
              placeholder="เช่น ห้องน้ำชาย ชั้น 2"
            />
            {form.errors.name ? (
              <span className="admin-field-error">{form.errors.name}</span>
            ) : (
              <span className="admin-field-help">ชื่อนี้ขึ้นบนการ์ด Dashboard และบนป้าย QR</span>
            )}
          </div>

          <div className={`admin-field${form.errors.location ? ' is-invalid' : ''}`}>
            <label htmlFor="point-location">ตำแหน่ง</label>
            <input
              id="point-location"
              value={form.location}
              onChange={(e) => form.setLocation(e.target.value)}
              autoComplete="off"
              placeholder="เช่น อาคาร A ชั้น 2"
            />
            {form.errors.location && <span className="admin-field-error">{form.errors.location}</span>}
          </div>

          <div className={`admin-field${form.errors.interval ? ' is-invalid' : ''}`}>
            <label htmlFor="point-interval">รอบทำความสะอาด (นาที)</label>
            <input
              id="point-interval"
              type="text"
              inputMode="numeric"
              value={form.interval}
              onChange={(e) => form.setInterval(e.target.value)}
            />
            <div className="admin-toolbar" style={{ marginBottom: 0 }}>
              {INTERVAL_PRESETS.map((minutes) => (
                <button
                  key={minutes}
                  type="button"
                  className="admin-btn admin-btn-sm"
                  onClick={() => form.applyPreset(minutes)}
                  aria-label={`ตั้งรอบ ${minutes} นาที`}
                >
                  {minutes}
                </button>
              ))}
            </div>
            {form.errors.interval ? (
              <span className="admin-field-error">{form.errors.interval}</span>
            ) : (
              <span className="admin-field-help">เลยรอบทันทีที่เกินเวลาครบรอบ ไม่มีเวลาผ่อนผัน รอบแรกของวันนับจากเวลาเปิด</span>
            )}
          </div>

          {initialPoint && (
            <p className="admin-field-help">
              QR Token <span className="admin-mono">{initialPoint.qrToken}</span> ออกเมื่อ {formatThaiDate(initialPoint.qrTokenIssuedAt)} —
              ถ้าป้ายหายหรือชำรุด ให้กด “ออก QR ใหม่” ที่ตาราง
            </p>
          )}

          {form.submitError && <div className="admin-error">{form.submitError}</div>}

          <div className="admin-drawer-footer">
            <button type="button" className="admin-btn admin-btn-ghost" onClick={onClose}>
              ยกเลิก
            </button>
            <button type="submit" className="admin-btn admin-btn-primary" disabled={form.isSaving}>
              {form.isSaving ? 'กำลังบันทึก...' : 'บันทึก'}
            </button>
          </div>
        </form>
      </aside>
    </>
  );
};
```

Create `frontend/src/features/admin/components/RegenerateQrDialog.tsx`:

```tsx
import React from 'react';
import type { AdminServicePoint } from '../../../types';
import { useRegenerateQr } from '../hooks/useRegenerateQr';
import { shortToken } from '../logic/formatDate';

interface RegenerateQrDialogProps {
  point: AdminServicePoint;
  onClose: () => void;
  onDone: (updated: AdminServicePoint) => void;
}

export const RegenerateQrDialog: React.FC<RegenerateQrDialogProps> = ({ point, onClose, onDone }) => {
  const regen = useRegenerateQr(point, onDone);

  return (
    <div className="admin-dialog-backdrop">
      <div className="admin-dialog" role="dialog" aria-modal="true" aria-labelledby="regen-title">
        <h2 id="regen-title">ออก QR ใหม่ให้ “{point.name}” ?</h2>

        <div className="admin-warning">
          <span aria-hidden="true">⚠️</span>
          <div>
            <b>ป้ายที่ติดอยู่ที่จุดนี้จะสแกนไม่ได้ทันที</b> ต้องพิมพ์ป้ายใหม่ไปติดแทนก่อนแม่บ้านรอบถัดไป
          </div>
        </div>

        <ul className="admin-muted" style={{ paddingLeft: '18px', display: 'grid', gap: '4px' }}>
          <li>ประวัติสแกนและสถานะบน Dashboard ไม่เปลี่ยน</li>
          <li>
            QR Token ปัจจุบัน <span className="admin-mono">{shortToken(point.qrToken)}…</span> จะถูกแทนด้วยค่าสุ่มใหม่
          </li>
        </ul>

        {regen.error && <div className="admin-error" style={{ marginTop: '14px' }}>{regen.error}</div>}

        <div className="admin-dialog-footer">
          <button type="button" className="admin-btn admin-btn-ghost" onClick={onClose}>
            ยกเลิก
          </button>
          <button type="button" className="admin-btn admin-btn-warning" onClick={() => void regen.confirm()} disabled={regen.isSaving}>
            {regen.isSaving ? 'กำลังออก QR ใหม่...' : 'ออก QR ใหม่'}
          </button>
        </div>
      </div>
    </div>
  );
};
```

Create `frontend/src/features/admin/components/PrintSheet.tsx`:

```tsx
import React from 'react';
import { Printer } from 'lucide-react';
import type { AdminServicePoint } from '../../../types';
import { QrImage } from '../../../components/qr/QrImage';
import { LABELS_PER_PAGE, chunk } from '../logic/pointList';
import { formatThaiDate, shortToken } from '../logic/formatDate';
import '../printSheet.css';

interface PrintSheetProps {
  points: AdminServicePoint[];
  onClose: () => void;
}

export const PrintSheet: React.FC<PrintSheetProps> = ({ points, onClose }) => {
  const pages = chunk(points, LABELS_PER_PAGE);
  const printedOn = formatThaiDate(new Date().toISOString());

  return (
    <section className="print-sheet" aria-label="หน้าพิมพ์ป้าย QR">
      <div className="print-screen-only print-toolbar">
        <strong>{points.length === 1 ? `ป้ายของ ${points[0].name}` : `ป้ายทุกจุดที่เปิดใช้งาน (${points.length} จุด)`}</strong>
        <div className="print-toolbar-actions">
          <button type="button" className="admin-btn admin-btn-ghost" onClick={onClose}>
            ปิด
          </button>
          <button type="button" className="admin-btn admin-btn-primary" onClick={() => window.print()} disabled={points.length === 0}>
            <Printer size={16} /> พิมพ์
          </button>
        </div>
      </div>

      {points.length === 0 && <p className="print-empty">ไม่มีจุดที่เปิดใช้งานให้พิมพ์</p>}

      {pages.map((labels, pageIndex) => (
        <div key={pageIndex} className="print-page">
          <div className="print-page-head">
            <b>ป้าย QR จุดบริการ</b>
            <span>
              พิมพ์ {printedOn} · หน้า {pageIndex + 1}/{pages.length} · A4 แนวตั้ง {LABELS_PER_PAGE} ป้ายต่อหน้า
            </span>
          </div>
          <div className="print-labels">
            {labels.map((point) => (
              <article key={point.id} className="print-label">
                <div>
                  <div className="print-label-name">{point.name}</div>
                  {point.location && <div className="print-label-location">{point.location}</div>}
                  <div className="print-label-cta">สแกนด้วยกล้องมือถือ เพื่อบันทึกการทำความสะอาด</div>
                  <div className="print-label-meta">
                    รอบทุก {point.cleaningIntervalMinutes} นาที · QR ออกเมื่อ {formatThaiDate(point.qrTokenIssuedAt)} ·{' '}
                    {shortToken(point.qrToken)}
                  </div>
                  <div className="print-label-url">{point.scanUrl}</div>
                </div>
                <QrImage value={point.scanUrl} label={`QR ของ ${point.name}`} size={132} />
              </article>
            ))}
          </div>
        </div>
      ))}
    </section>
  );
};
```

- [ ] **Step 6: Build, test and lint**

Run: `npm --prefix frontend run build`
Expected: PASS, `built in`.

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  65 passed (65)`.

Run: `npm --prefix frontend run lint`
Expected: no line reports an error. The `react(set-state-in-effect)` warning on `usePointsAdminPage.ts` matches the existing hooks.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/App.tsx frontend/src/features/admin
git commit -m "feat(points-web): add the service points page with paging, regenerate QR and A4 print sheet (#16)"
```

---

### Task 7: Dashboard card and scan page use the new links and messages

**Files:**
- Create: `frontend/src/services/api.test.ts`
- Modify: `frontend/src/services/api.ts`
- Modify: `frontend/src/features/scan/components/ScanRecordPage.tsx`
- Modify: `frontend/src/features/dashboard/components/ServicePointCard.tsx`

**Interfaces:**
- Consumes: `toApiError` (plan 2B), `QrImage` (Task 5), `scanUrl` and the 410 response (Task 2)
- Produces:
  - `getServicePointByTokenApi` throws the server's Thai message on `410` and `ไม่พบจุดบริการของป้ายนี้ ป้ายอาจถูกออก QR ใหม่แล้ว` on any other failure
  - The dashboard card's QR modal shows `point.scanUrl` and a locally drawn QR; no hardcoded IP and no `api.qrserver.com` request remain

- [ ] **Step 1: Write the failing tests and confirm the hardcoded link is still there**

Create `frontend/src/services/api.test.ts`:

```ts
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getServicePointByTokenApi } from './api';
import { resetSessionForTests } from './authSession';

const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>();

function refreshOk(): Response {
  const user = { id: 1, username: 'somchai', fullName: 'สมชาย ใจดี', role: 'cleaner' };
  return new Response(JSON.stringify({ accessToken: 'A', expiresAt: new Date(Date.now() + 300_000).toISOString(), user }), { status: 200 });
}

beforeEach(() => {
  resetSessionForTests();
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('getServicePointByTokenApi', () => {
  it('shows the server message when the sign belongs to a deactivated point', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/refresh'
        ? refreshOk()
        : new Response(JSON.stringify({ message: "จุดบริการ 'ห้องน้ำชาย ชั้น 1' ปิดใช้งานแล้ว", reason: 'deactivated' }), { status: 410 })
    );

    await expect(getServicePointByTokenApi('token-restroom-m1')).rejects.toThrow("จุดบริการ 'ห้องน้ำชาย ชั้น 1' ปิดใช้งานแล้ว");
  });

  it('explains in Thai that an unknown sign may have been replaced', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/refresh'
        ? refreshOk()
        : new Response(JSON.stringify({ message: "Service point with QR token 'old' not found." }), { status: 404 })
    );

    await expect(getServicePointByTokenApi('old')).rejects.toThrow('ไม่พบจุดบริการของป้ายนี้ ป้ายอาจถูกออก QR ใหม่แล้ว');
  });
});
```

Run: `npm --prefix frontend test`
Expected: FAIL. The deactivated case throws `ไม่พบข้อมูลจุดบริการสำหรับ QR Token นี้` instead of the server's message.

Run: `grep -rn "qrserver\|10.249.194.205" frontend/src`
Expected: two lines in `ServicePointCard.tsx`.

- [ ] **Step 2: Surface the deactivated message in the API client**

In `frontend/src/services/api.ts`, replace:

```ts
import { apiFetch } from './authSession';
```

with:

```ts
import { apiFetch } from './authSession';
import { toApiError } from './apiError';
```

and replace:

```ts
  const res = await apiFetch(`${BASE_URL}/service-points/by-token/${encodeURIComponent(token)}`);
  if (!res.ok) {
    throw new Error('ไม่พบข้อมูลจุดบริการสำหรับ QR Token นี้');
  }
```

with:

```ts
  const res = await apiFetch(`${BASE_URL}/service-points/by-token/${encodeURIComponent(token)}`);
  if (res.status === 410) {
    // ADR facility-0021: a Deactivated Service Point says so, with its name
    throw await toApiError(res, 'จุดบริการนี้ปิดใช้งานแล้ว');
  }
  if (!res.ok) {
    throw new Error('ไม่พบจุดบริการของป้ายนี้ ป้ายอาจถูกออก QR ใหม่แล้ว');
  }
```

In `frontend/src/features/scan/components/ScanRecordPage.tsx`, the error screen's heading must fit both a deactivated point and a replaced sign. Replace `>ไม่พบจุดบริการ</h2>` with `>สแกนป้ายนี้ไม่ได้</h2>`.

- [ ] **Step 3: Use the server's link and a local QR on the dashboard card**

In `frontend/src/features/dashboard/components/ServicePointCard.tsx`, replace:

```tsx
import type { ServicePointStatus, PointStatus } from '../../../types';
```

with:

```tsx
import type { ServicePointStatus, PointStatus } from '../../../types';
import { QrImage } from '../../../components/qr/QrImage';
```

replace:

```tsx
  const scanUrl = `http://10.249.194.205:5173/scan/${point.qrToken}`;
```

with:

```tsx
  // ADR facility-0003 and facility-0021: the server builds the link from PublicBaseUrl, never a hardcoded IP
  const scanUrl = point.scanUrl;
```

and replace:

```tsx
              <img
                src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&margin=10&data=${encodeURIComponent(scanUrl)}`}
                alt={`QR Code for ${point.name}`}
                style={styles.qrImage}
              />
```

with:

```tsx
              <QrImage value={scanUrl} label={`QR Code for ${point.name}`} size={200} />
```

- [ ] **Step 4: Run everything**

Run: `npm --prefix frontend test`
Expected: PASS, `Tests  67 passed (67)`.

Run: `grep -rn "qrserver\|10.249.194.205" frontend/src || echo "clean"`
Expected: `clean`.

Run: `npm --prefix frontend run build` and `npm --prefix frontend run lint`
Expected: `built in`; no line of lint output reports an error.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/services/api.ts frontend/src/services/api.test.ts frontend/src/features/scan/components/ScanRecordPage.tsx frontend/src/features/dashboard/components/ServicePointCard.tsx
git commit -m "fix(points-web): show deactivated and replaced sign messages and drop the hardcoded scan IP (#16)"
```

---

### Task 8: README and the manual check with a printer and a phone

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: Tasks 1-7
- Produces: a ticked checklist in this plan

- [ ] **Step 1: Document `PublicBaseUrl` and the page**

In `README.md`, replace:

````markdown
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/FacilityRealtime.Api
```
````

with:

````markdown
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project backend/src/FacilityRealtime.Api
```

ตั้ง URL ที่มือถือใช้เปิดหน้าสแกน URL นี้ถูกพิมพ์ลงป้าย QR ถ้า IP ของ Mac เปลี่ยน ให้ตั้งใหม่แล้วพิมพ์ป้ายใหม่ (ADR facility-0003):
```bash
dotnet user-secrets set "PublicBaseUrl" "http://<MAC_LAN_IP>:5173" --project backend/src/FacilityRealtime.Api
```
````

and replace:

```markdown
- ผู้ดูแลระบบเพิ่มบัญชี แก้สิทธิ์ รีเซ็ตรหัสผ่าน และปิดใช้งานบัญชีได้ที่เมนู **จัดการระบบ** (`/admin/accounts`) ไม่มีการลบบัญชี
```

with:

```markdown
- ผู้ดูแลระบบเพิ่ม แก้ไข ปิดใช้งานจุด ออก QR ใหม่ และพิมพ์ป้ายได้ที่เมนู **จัดการระบบ → จุดบริการ** (`/admin/points`)
- ผู้ดูแลระบบเพิ่มบัญชี แก้สิทธิ์ รีเซ็ตรหัสผ่าน และปิดใช้งานบัญชีได้ที่เมนู **จัดการระบบ → บัญชีผู้ใช้** (`/admin/accounts`) ไม่มีการลบบัญชี
```

- [ ] **Step 2: Run every automated check**

```bash
dotnet test backend/FacilityRealtime.slnx --nologo
npm --prefix frontend test
npm --prefix frontend run build
npm --prefix frontend run lint
```

Expected: unit tests 91 and API tests 94 pass; Vitest `67 passed`; build succeeds; lint reports no errors.

- [ ] **Step 3: Walk the flow on a machine with MySQL, a printer or PDF printer, and a phone**

Set `PublicBaseUrl` to the Mac's LAN address first (README step 2). Tick a row only after seeing its result; if one fails, stop and debug with `debug-mantra`.

| # | Do | Expect |
|---|---|---|
| 1 | `dotnet user-secrets set "PublicBaseUrl" "192.168.1.20:5173" --project backend/src/FacilityRealtime.Api`, start the API | The API refuses to start and the error names `PublicBaseUrl`. Set it back to `http://<MAC_LAN_IP>:5173` |
| 2 | Log in as `admin`, click `จัดการระบบ` | The `จุดบริการ` tab opens first; 3 seed points; the page bar reads `แสดง 1–3 จาก 3 จุด` |
| 3 | Add 10 test points: `mysql -u root facility_dashboard -e "INSERT INTO service_points (Name, Location, CleaningIntervalMinutes, QrToken, QrTokenIssuedAt, IsActive, CreatedAt) SELECT CONCAT('จุดทดสอบ ', n), 'อาคารทดสอบ', 60, UUID(), UTC_TIMESTAMP(), 1, UTC_TIMESTAMP() FROM (SELECT 1 n UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9 UNION SELECT 10) t;"`, reload | 13 points on 2 pages; page 2 holds 1 row; choosing 24 per page shows 1 page |
| 4 | Go to page 2, type `ห้องน้ำ` in search | Back on page 1 with the two restroom points |
| 5 | `เพิ่มจุดบริการ` with interval `abc`, then with name `ห้องน้ำ ชั้น 9` and preset `45` | First save shows `รอบทำความสะอาดต้องเป็นจำนวนเต็มมากกว่า 0 นาที`; second save jumps to the new row's page, flashes it, toast offers `พิมพ์ป้าย` |
| 6 | Click `พิมพ์ป้าย` in that toast | One sign with the name, `อาคาร…`, `รอบทุก 45 นาที`, today's Buddhist-era date, the LAN URL and a QR; the browser print preview is A4 portrait |
| 7 | `พิมพ์ป้ายทั้งหมด`, print to PDF | Pages of 6 signs; page count = active points ÷ 6 rounded up; no dark background |
| 8 | Scan one printed sign with the phone camera | The scan page opens on the LAN URL; saving works; the dashboard card updates |
| 9 | `ออก QR ใหม่` on that point, confirm | Orange dialog warns the sign stops working; row stays in place; toast offers `พิมพ์ป้ายใหม่` |
| 10 | Scan the old printed sign again | `สแกนป้ายนี้ไม่ได้` with `ไม่พบจุดบริการของป้ายนี้ ป้ายอาจถูกออก QR ใหม่แล้ว` |
| 11 | `ปิดใช้งาน` a point that has a printed sign | Row fades in place; `พิมพ์ป้าย` and `ออก QR ใหม่` are disabled; within 30 seconds the card leaves the dashboard |
| 12 | Scan that point's sign | `สแกนป้ายนี้ไม่ได้` with `จุดบริการ '<name>' ปิดใช้งานแล้ว` |
| 13 | Press `เลิกทำ` in the toast (or `เปิดใช้งาน`) | The point returns to the dashboard |
| 14 | On the dashboard, open a card's `QR สำหรับมือถือ` with DevTools Network open | The QR and link use the LAN URL; no request to `api.qrserver.com` |
| 15 | `curl -i http://localhost:5001/api/admin/service-points -H "Authorization: Bearer <somchai access token from DevTools>"` | `HTTP/1.1 403 Forbidden` |

Remove the test points afterwards by deactivating them on the page; there is no delete.

- [ ] **Step 4: Commit**

```bash
git add README.md docs/superpowers/plans/2026-09-13-phase2c-points-admin-page.md
git commit -m "docs(readme): document PublicBaseUrl and the service points page (#16)"
```

---

## Plan Complete

Plan 2C is done when all eight tasks are ticked. Two follow-ups stay outside this plan on purpose: the Dashboard switches to the shared `Pager` once it has more than 12 points (facility-0008 amendment), and the lessons from this phase go to decision-map ticket #20.
