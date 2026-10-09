using System.Text.Json.Serialization;
using FacilityRealtime.Api.Auth;
using FacilityRealtime.Api.Endpoints;
using FacilityRealtime.Api.Hubs;
using FacilityRealtime.Application.Attendance;
using FacilityRealtime.Application.Auth;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=localhost;Port=3306;Database=facility_dashboard;Uid=root;Pwd=;CharSet=utf8mb4;";

    builder.Services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));
}

// 2. SignalR with string enum serialization
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 3. CORS (Allow local network devices & dev servers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. JSON Serialization with String Enum Converter
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// 5. OpenAPI document (served only in Development, below)
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI for trying the API by hand: Development only, never on a server reachable from the internet (facility-0049)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Facility Real-time Dashboard API"));
}

// 6. Migrate on startup. Seed only on a developer machine: the seed's known Admin password must never reach a server on the internet (facility-0049).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
    {
        await DbInitializer.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
    }
}

// 7. SignalR Hub Mapping
app.MapHub<ScanHub>("/hubs/scan");

// 8. Endpoints
app.MapGet("/", () => Results.Ok(new { status = "healthy", service = "Facility Real-time Dashboard API" }));
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAttendanceEndpoints();
app.MapServicePointEndpoints();
app.MapScanRecordEndpoints();
app.MapCoverAssignmentEndpoints();
app.MapMyWorkEndpoints();

app.Run();

// Lets WebApplicationFactory<Program> in the API tests reach the entry point
public partial class Program;
