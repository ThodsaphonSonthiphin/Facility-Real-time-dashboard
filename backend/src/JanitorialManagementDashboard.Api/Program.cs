using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using JanitorialManagementDashboard.Api;
using JanitorialManagementDashboard.Api.Auth;
using JanitorialManagementDashboard.Api.Commands;
using JanitorialManagementDashboard.Api.Endpoints;
using JanitorialManagementDashboard.Api.Hubs;
using JanitorialManagementDashboard.Api.Retention;
using JanitorialManagementDashboard.Api.Scheduling;
using JanitorialManagementDashboard.Application.Retention;
using JanitorialManagementDashboard.Application.Scheduling;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using JanitorialManagementDashboard.Application.Accounts;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Auth;
using JanitorialManagementDashboard.Infrastructure.Auth;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Overview;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// facility-0103: until the host has built its own logger, a bootstrap logger with the same settings catches a server
// that cannot start (facility-0095), including the `reset-admin` command. It is never replaced, only closed at exit.
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();
try
{
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

    // facility-0103: the Server Log goes through Serilog behind ILogger<T>. Sinks and levels come from the "Serilog" settings;
    // ReadFrom.Services adds any ILogEventSink registered in the container: only the API tests' capture. The app registers
    // none; a real sink goes in the settings, and one that sends the log off the server needs a new ADR. The host owns this
    // logger, so the static one stays the bootstrap logger and test hosts never share one.
    builder.Services.AddSerilog(
        (services, configuration) => configuration.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(services),
        preserveStaticLogger: true);

    // 1. Clock, hashing, auth and database. The "Testing" environment (API tests) registers its own SQLite context.
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
    builder.Services.AddSingleton(
        builder.Configuration.GetSection(LoginThrottleSettings.SectionName).Get<LoginThrottleSettings>() ?? new LoginThrottleSettings());
    builder.Services.AddSingleton<LoginThrottle>();
    var attendance = builder.Configuration.GetSection(AttendanceSettings.SectionName).Get<AttendanceSettings>() ?? new AttendanceSettings();
    if (attendance.OpensMinutesBeforeShift is < 0 or > AttendanceSettings.MaxReceivingOffsetMinutes
        || attendance.ClosesMinutesAfterShift is < 0 or > AttendanceSettings.MaxReceivingOffsetMinutes
        || attendance.RepeatIgnoreMinutes < 0)
    {
        throw new InvalidOperationException("Attendance: window minutes must be 0-360 and RepeatIgnoreMinutes 0 or more.");
    }

    var retention = builder.Configuration.GetSection(RetentionSettings.SectionName).Get<RetentionSettings>() ?? new RetentionSettings();
    retention.Validate();
    builder.Services.AddSingleton(retention);
    builder.Services.AddScoped<IRetentionRunner, RetentionRunner>();
    // Spec §3: the dated Supervisor scope and its writes are the authority for management, work and inspection
    builder.Services.AddScoped<ScheduleStore>();
    builder.Services.AddScoped<ScheduleScopeReader>();
    builder.Services.AddScoped<SupervisorMembership>();

    // Spec §11: the command engine and its seams. The settings are built without demanding a signing key, and the token
    // service resolves the key only when a command is actually issued or verified, so a server that leaves scheduling off
    // still starts, logs in and serves the legacy endpoints with no key configured at all (Task 6 adds the routes and the
    // real command kinds; Task 12 supplies the scoped live invalidation).
    var scheduling = builder.Configuration.GetSection(SchedulingSettings.SectionName);
    var schedulingSettings = new SchedulingSettings(scheduling[SchedulingSettings.CommandSigningKeyName])
    {
        // A stored date format, read with the invariant culture so a machine's calendar never reinterprets it.
        HistoryStartsAt = DateOnly.TryParse(
            scheduling["HistoryStartsAt"], CultureInfo.InvariantCulture, out var historyStartsAt)
            ? historyStartsAt
            : null,
    };
    builder.Services.AddSingleton(schedulingSettings);
    builder.Services.AddScoped(provider =>
        new ScheduleCommandTokenService(provider.GetRequiredService<SchedulingSettings>().CommandSigningKey));
    builder.Services.AddScoped<ScheduleNotifications>();
    builder.Services.AddScoped<IScheduleCommandNotifier>(provider => provider.GetRequiredService<ScheduleNotifications>());
    builder.Services.AddScoped<ScheduleCommandService>();

    // Spec §11 and §5: one handler per command kind, resolved by kind through the seam the engine already calls. Each kind
    // owns which building/shift/person a command may touch; the engine owns the transaction, the locks and the receipt.
    builder.Services.AddScoped<IScheduleCommandHandler, SetWeeklyDaysOffHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, SetRegularBaselineHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, SetReliefBaselineHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, SetDatedAreaHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, SetDatedBreakHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, RecordLeaveHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, CancelLeaveHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, CorrectPlanHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, AssignReliefHandler>();
    // Spec §9 (Task 10): the midshift Area transfer — one instruction kind and one cancellation kind, from the same base,
    // plus the arrival confirmation that is the Cleaner's own actual fact rather than a management command.
    builder.Services.AddScoped<IScheduleCommandHandler, InstructTransferHandler>();
    builder.Services.AddScoped<IScheduleCommandHandler, CancelTransferHandler>();
    builder.Services.AddScoped<TransferCommandService>();

    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddHostedService<RetentionWorker>();

    builder.Services.AddScoped<OverviewTargetRecorder>();
    builder.Services.AddScoped<OverviewMutation>();
    builder.Services.AddScoped<OverviewNotifications>();
    builder.Services.AddScoped<OverviewReadQuery>();
    builder.Services.AddSingleton(attendance);
    builder.Services.AddFacilityAuth(builder.Configuration);

    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var connectionString = DatabaseSettings.ConnectionString(builder.Configuration);
        builder.Services.AddDbContextPool<AppDbContext>(
            options => options.UseMySQL(connectionString),
            poolSize: 128);
    }

    // 2. SignalR with string enum serialization; facility-0107: a ping every 5 s, for the Admin pages' 10 s wait
    builder.Services.AddSignalR(options => options.KeepAliveInterval = TimeSpan.FromSeconds(5))
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

    // 5. Compression of the frontend's files only (see the pipeline below). Also over https: these files hold no secret.
    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

    // facility-0106: no "Server: Kestrel" header; it only tells a visitor what the server runs
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    // facility-0106: HSTS for a year, this host only (the domain is not known yet, so no subdomains and no preload)
    builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

    var app = builder.Build();

    // facility-0104 decision 6: behind Caddy every request comes from loopback, so the client's address is taken from
    // X-Forwarded-For, one hop, and only when the request comes from loopback (the default trusted proxies: 127.0.0.0/8
    // and ::1). Kestrel listens on localhost only (facility-0093), so nobody outside can fake the header. facility-0106
    // adds X-Forwarded-Proto on the same terms, so the app knows an answer goes out over https.
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1,
    });
    app.UseSecurityHeaders(swaggerWithoutCsp: app.Environment.IsDevelopment());
    if (!app.Environment.IsDevelopment())
    {
        // Only on https answers, and never for localhost: a developer's browser would otherwise refuse http://localhost
        app.UseHsts();
    }

    // facility-0067: the built frontend (frontend/dist, copied into wwwroot by `npm run build:api`) is served from this origin.
    // Hashed files under assets/ never change; everything else, index.html included, is revalidated on every load.
    // API answers are never stored (facility-0106, SecurityHeaders).
    var appFiles = new StaticFileOptions
    {
        ContentTypeProvider = PrecompressedFiles.ContentTypes(),
        OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
                ? "public, max-age=31536000, immutable"
                : "no-cache";
            PrecompressedFiles.LabelCopy(context);
        },
    };
    // The frontend's files are compressed (the main JS is about a third of its size), which matters for phones on a slow
    // link. API answers and the hub are left alone: an answer holding a token must not be compressed over https (BREACH).
    // A file with a copy compressed at build time is sent as that copy; the on-the-fly compression leaves it alone.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/hubs"),
        frontend =>
        {
            frontend.UseResponseCompression();
            frontend.UsePrecompressedFiles(app.Environment);
        });
    app.UseDefaultFiles();
    app.UseStaticFiles(appFiles);
    app.UseAuthentication();
    app.UseAuthorization();
    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (OverviewCaptureException ex)
        {
            app.Logger.LogError(ex, "Setup history capture failed. Transaction rollback {RollbackStatus}",
                ex.RollbackConfirmed ? "confirmed" : "unconfirmed");
            await ApiResults.Message(StatusCodes.Status503ServiceUnavailable, "บันทึกไม่สำเร็จ กรุณาลองใหม่อีกครั้ง").ExecuteAsync(context);
        }
    });

    // Swagger UI for trying the API by hand: Development only, never on a server reachable from the internet (facility-0049)
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Janitorial Management Dashboard API"));
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
        await OverviewHistoryBootstrap.EnsureAsync(db, attendance, scope.ServiceProvider.GetRequiredService<TimeProvider>(),
            app.Lifetime.ApplicationStopping);
        var historyStart = schedulingSettings.HistoryStartsAt
            ?? ScheduleAuthority.BusinessDate(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        await ScheduleBackfill.EnsureAsync(db, historyStart, app.Lifetime.ApplicationStopping);
        if (!schedulingSettings.HasCommandSigningKey)
        {
            app.Logger.LogError(
                "Scheduling:{KeyName} is not configured (minimum {MinBytes} UTF-8 bytes). Schedule reads will work, but all schedule command saves will fail until it is configured.",
                SchedulingSettings.CommandSigningKeyName,
                SchedulingSettings.MinimumCommandSigningKeyBytes);
        }
        else if (!schedulingSettings.HasValidCommandSigningKey)
        {
            app.Logger.LogError(
                "Scheduling:{KeyName} is shorter than the required {MinBytes} UTF-8 bytes. Schedule reads will work, but all schedule command saves will fail until it is replaced with a key of at least {MinBytes} bytes.",
                SchedulingSettings.CommandSigningKeyName,
                SchedulingSettings.MinimumCommandSigningKeyBytes,
                SchedulingSettings.MinimumCommandSigningKeyBytes);
        }
    }

    // 7. SignalR Hub Mapping
    app.MapHub<ScanHub>("/hubs/scan");

    // 8. Endpoints
    app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "Janitorial Management Dashboard API" }));
    app.MapAuthEndpoints();
    app.MapMeEndpoints();
    app.MapAttendanceEndpoints();
    app.MapAreaTransferEndpoints();
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
    app.MapTeamAttendanceEndpoints();
    app.MapAdminAlertEndpoints();
    app.MapAdminAuditLogEndpoints();
    app.MapAdminAccountEndpoints();
    app.MapAdminExportEndpoints();
    app.MapAdminInspectionSummaryEndpoints();
    app.MapAdminOverviewEndpoints();
    app.MapScheduleEndpoints();

    // 9. Every other page path (/login, /scan/<token>, ...) gets the app, so a printed QR Sign opens it.
    // See docs/adr/facility-0026-frontend-router-shape.md and facility-0067.
    // Paths under /api and /hubs never do: they keep their own 404 and 405. A path with a file extension is a missing file, 404.
    app.MapFallbackToFile("{*path:regex(^(?!(api|hubs)(/|$)).*$):nonfile}", "index.html", appFiles);

    app.Run();
}
// `dotnet ef` stops Program on purpose once the host is built (design-time migrations); that is not a failure
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.ForContext<Program>().Fatal(exception, "Stopped on an error it could not recover from");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Lets WebApplicationFactory<Program> in the API tests reach the entry point
public partial class Program;
