using System.Text;
using System.Text.Json.Serialization;
using FacilityRealtime.Api;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.Commands;
using FacilityRealtime.Api.Endpoints;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Accounts;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// facility-0092: `reset-admin <username>` recovers Admin access from the server's console, then exits without serving.
// It needs only the connection string, so it runs before the auth setup that asks for Jwt:SigningKey.
// facility-0093: it names the database and waits for "yes" before the migration touches anything.
if (args.Length > 0 && args[0] == ResetAdminCommand.Name)
{
    Console.OutputEncoding = Encoding.UTF8;
    Environment.ExitCode = await ResetAdminCommand.RunFromCommandLineAsync(
        args[1..],
        DatabaseSettings.ConnectionString(builder.Configuration),
        async connection =>
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySQL(connection).Options);
            await db.Database.MigrateAsync();
            return db;
        },
        new Pbkdf2PasswordHasher(),
        TimeProvider.System,
        new SystemAdminConsole());
    return;
}

// 1. Clock, hashing, auth and database. The "Testing" environment (API tests) registers its own SQLite context.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
builder.Services.AddSingleton(
    builder.Configuration.GetSection(LoginThrottleSettings.SectionName).Get<LoginThrottleSettings>() ?? new LoginThrottleSettings());
builder.Services.AddSingleton<LoginThrottle>();
var attendance = builder.Configuration.GetSection(AttendanceSettings.SectionName).Get<AttendanceSettings>() ?? new AttendanceSettings();
if (attendance.OpensMinutesBeforeShift is < 0 or > 360 || attendance.ClosesMinutesAfterShift is < 0 or > 360 || attendance.RepeatIgnoreMinutes < 0)
{
    throw new InvalidOperationException("Attendance: window minutes must be 0-360 and RepeatIgnoreMinutes 0 or more.");
}

builder.Services.AddSingleton(attendance);
builder.Services.AddFacilityAuth(builder.Configuration);

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = DatabaseSettings.ConnectionString(builder.Configuration);
    builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));
}

// 2. SignalR with string enum serialization
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();

// 3. JSON Serialization with String Enum Converter
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// 4. OpenAPI document (served only in Development, below)
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// facility-0067: the built frontend (frontend/dist, copied into wwwroot by `npm run build:api`) is served from this origin.
// Hashed files under assets/ never change; everything else, index.html included, is revalidated on every load.
var appFiles = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    },
};
app.UseDefaultFiles();
app.UseStaticFiles(appFiles);
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI for trying the API by hand: Development only, never on a server reachable from the internet (facility-0049)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Facility Real-time Dashboard API"));
}

// 6. Migrate on startup. Seed only on a developer machine: the seed's known Admin password must never reach a server on the internet (facility-0049). Then the first Admin from the server's settings (facility-0092).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
    {
        await DbInitializer.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
    }

    // facility-0092: the first Admin comes from the server's settings (FirstAdmin__*), only while no Admin exists at all.
    // After the seed, so a developer machine keeps the seed's Admin and nothing happens there.
    var firstAdmin = app.Configuration.GetSection(FirstAdminSettings.SectionName).Get<FirstAdminSettings>() ?? new FirstAdminSettings();
    await FirstAdmin.EnsureAsync(
        db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), scope.ServiceProvider.GetRequiredService<TimeProvider>(), firstAdmin, app.Logger);
}

// 7. SignalR Hub Mapping
app.MapHub<ScanHub>("/hubs/scan");

// 8. Endpoints
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "Facility Real-time Dashboard API" }));
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAttendanceEndpoints();
app.MapServicePointEndpoints();
app.MapScanRecordEndpoints();
app.MapCoverAssignmentEndpoints();
app.MapMyWorkEndpoints();
app.MapInspectionEndpoints();
app.MapMyBuildingEndpoints();
app.MapAdminAreaEndpoints();
app.MapAdminBuildingEndpoints();
app.MapAdminPointEndpoints();
app.MapAdminSignEndpoints();
app.MapAdminAttendanceEndpoints();
app.MapAdminAlertEndpoints();
app.MapAdminAuditLogEndpoints();
app.MapAdminAccountEndpoints();
app.MapAdminExportEndpoints();
app.MapAdminInspectionSummaryEndpoints();

// 9. Every other page path (/login, /scan/<token>, ...) gets the app, so a printed QR Sign opens it (facility-0026, 0067).
// Paths under /api and /hubs never do: they keep their own 404 and 405. A path with a file extension is a missing file, 404.
app.MapFallbackToFile("{*path:regex(^(?!(api|hubs)(/|$)).*$):nonfile}", "index.html", appFiles);

app.Run();

// Lets WebApplicationFactory<Program> in the API tests reach the entry point
public partial class Program;
