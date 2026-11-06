using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using JanitorialManagementDashboard.Application.Rounds;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// This executable is testing infrastructure only. Production startup and seed code are unchanged.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var profiles = new Dictionary<string, (int Buildings, int Areas, int Points, int Days)>
{
    ["small"] = (2, 8, 2, 7),
    ["medium"] = (8, 80, 3, 14),
    ["large"] = (30, 600, 2, 14),
};
if (args.Length != 1 || !profiles.TryGetValue(args[0], out var volume))
    throw new ArgumentException("Choose small, medium or large");
var profile = args[0];
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Missing isolated connection");
var settings = new DbConnectionStringBuilder { ConnectionString = connection };
var database = settings["Database"].ToString()!;
if (!Regex.IsMatch(database, @"\Afacility_e2e_[0-9]{14}_[0-9a-f]{8}\z")
    || settings["Server"].ToString() != "localhost"
    || settings["Uid"].ToString() != "e2e_" + database.Split('_')[^1])
    throw new InvalidOperationException("Only a runner-owned localhost E2E database/user is allowed");
var token = Environment.GetEnvironmentVariable("E2E_SEED_TOKEN");
if (token is null || !Regex.IsMatch(token, @"\A[0-9a-f]{48}\z"))
    throw new InvalidOperationException("Missing ownership token");
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySQL(connection).Options);
await db.Database.OpenConnectionAsync();
await using var marker = db.Database.GetDbConnection().CreateCommand();
marker.CommandText = "SELECT token FROM __e2e_owner";
if ((await marker.ExecuteScalarAsync())?.ToString() != token)
    throw new InvalidOperationException("Ownership marker mismatch");
var seedNow = Environment.GetEnvironmentVariable("E2E_SEED_NOW") is string fixedNow
    ? DateTime.Parse(fixedNow, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
    : DateTime.UtcNow;
if (seedNow > DateTime.UtcNow) throw new ArgumentException("Seed clock cannot be in the future");
var today = DateOnly.FromDateTime(seedNow.AddHours(7));
var anchor = Environment.GetEnvironmentVariable("E2E_SEED_DATE") is string date ? DateOnly.ParseExact(date, "yyyy-MM-dd") : today;
if (anchor > today) throw new ArgumentException("Seed date cannot be in the future");
// Dedicated metadata lives only in the isolated database; no production migration or schema change.
await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS __e2e_dataset (identity VARCHAR(100) PRIMARY KEY)");
marker.CommandText = "SELECT identity FROM __e2e_dataset";
var identity = $"v1:{profile}:{anchor:yyyy-MM-dd}";
var existing = (await marker.ExecuteScalarAsync())?.ToString();
if (existing is not null && existing != identity)
    throw new InvalidOperationException("A different dataset already exists; start a fresh run");
var timer = Stopwatch.StartNew();
if (existing is null)
{
    if (await db.Areas.AnyAsync(a => a.Code.StartsWith("E2E")))
        throw new InvalidOperationException("Unmarked seed rows exist; refusing to duplicate or remove them");
    await using var transaction = await db.Database.BeginTransactionAsync();
    var admin = await db.Users.SingleAsync(u => u.Username == "admin");
    var created = Utc(anchor.AddDays(-volume.Days - 1), 7);
    // Reuse one synthetic phone hash across fixtures for speed. No credential is published in the manifest.
    var seedEmployee = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
    var hash = seedEmployee.SecretHash;
    var buildings = Enumerable.Range(1, volume.Buildings).Select(i => new Building
    { Code = $"E2EB{i:D3}", Name = $"อาคารทดสอบ {i:D3}", CreatedAt = created }).ToList();
    db.Buildings.AddRange(buildings);
    db.Buildings.Add(new Building { Code = "E2EOFF", Name = "อาคารทดสอบปิดใช้งาน", IsActive = false, CreatedAt = created });
    var supervisors = new Dictionary<(int Building, Shift Shift), User>();
    for (var i = 0; i < buildings.Count; i++)
        foreach (var shift in new[] { Shift.Day, Shift.Night })
        {
            var number = i * 2 + (shift == Shift.Day ? 0 : 1);
            var user = new User { Role = UserRole.Supervisor, EmployeeId = $"8{number:D6}",
                DisplayName = $"หัวหน้าทดสอบ {number:D4}", SecretHash = hash, Building = buildings[i], Shift = shift, CreatedAt = created };
            supervisors[(i, shift)] = user;
            db.Users.Add(user);
        }
    var areas = new List<Area>();
    var cleaners = new Dictionary<(int Area, Shift Shift), User>();
    var points = new List<(int Area, ServicePoint Point, Sign Sign, Dictionary<Shift, PointRoundWindow> Windows)>();
    var checkins = new List<Sign>();
    for (var a = 0; a < volume.Areas; a++)
    {
        var area = new Area { Code = $"E2EA{a + 1:D4}", Name = $"พื้นที่ทดสอบ {a + 1:D4}",
            Building = buildings[a % buildings.Count], ShiftPattern = a % 4 == 3 ? ShiftPattern.DayOnly : ShiftPattern.DayAndNight, CreatedAt = created };
        areas.Add(area);
        db.Areas.Add(area);
        var checkin = Located(new Sign { Area = area, Code = area.Code + "-IN", QrToken = $"e2e-checkin-{a + 1}", QrIssuedAt = created }, created);
        checkins.Add(checkin);
        db.Signs.Add(checkin);
        foreach (var shift in new[] { Shift.Day, Shift.Night }.Where(area.HasShift))
        {
            var number = a * 2 + (shift == Shift.Day ? 0 : 1);
            var user = new User { Role = UserRole.Cleaner, EmployeeId = $"7{number:D6}", DisplayName = $"แม่บ้านทดสอบ {number:D4}",
                SecretHash = hash, Area = area, Shift = shift, CreatedAt = created };
            cleaners[(a, shift)] = user;
            db.Users.Add(user);
        }
        for (var p = 1; p <= volume.Points; p++)
        {
            var point = new ServicePoint { Area = area, Name = $"จุดทดสอบ {a + 1:D4}-{p:D2}", SortOrder = (short)p, CreatedAt = created };
            var sign = Located(new Sign { Area = area, ServicePoint = point, Code = $"{area.Code}-{p:D2}", QrToken = $"e2e-point-{a + 1}-{p}", QrIssuedAt = created }, created);
            db.ServicePoints.Add(point);
            db.Signs.Add(sign);
            var windows = new Dictionary<Shift, PointRoundWindow>();
            foreach (var shift in new[] { Shift.Day, Shift.Night }.Where(area.HasShift))
            {
                var window = new PointRoundWindow { ServicePoint = point, Shift = shift,
                    StartTime = new TimeOnly(shift == Shift.Day ? 7 : 19, 0), EndTime = new TimeOnly(shift == Shift.Day ? 18 : 6, 59), CreatedAt = created };
                windows[shift] = window;
                db.PointRoundWindows.Add(window);
            }
            if (RoundWindowRules.Validate(windows.Values.Select(w => new RoundWindowInput(w.Shift, w.StartTime, w.EndTime)).ToList(), area.ShiftPattern) is string problem)
                throw new InvalidOperationException(problem);
            points.Add((a, point, sign, windows));
        }
    }
    for (var i = 0; i < 12; i++)
        db.Users.Add(new User { Role = UserRole.Cleaner, EmployeeId = $"9{i:D6}", DisplayName = $"บัญชีทดสอบพักใช้งาน {i:D2}",
            SecretHash = hash, Shift = Shift.Day, IsActive = i % 2 == 0, CreatedAt = created });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    // Older to newer insertion preserves the audit log's id-descending chronology.
    for (var day = volume.Days; day >= 1; day--)
    {
        var shiftDate = anchor.AddDays(-day);
        foreach (var ((a, shift), user) in cleaners)
        {
            if (shift == Shift.Night && Utc(shiftDate.AddDays(1), 7) > seedNow) continue;
            if ((a + day) % 11 == 0) continue; // planned absences: no scans or attendance for this slot
            var start = Utc(shiftDate, shift == Shift.Day ? 7 : 19);
            foreach (var (eventType, offset) in new[] {
                (AttendanceEvent.ShiftIn, 0), (AttendanceEvent.BreakOut, 240),
                (AttendanceEvent.BreakIn, 300), (AttendanceEvent.ShiftOut, 710) })
            {
                // Some completed historical shifts lack Shift-Out, producing genuine alert inputs.
                if (eventType == AttendanceEvent.ShiftOut && (a + day) % 13 == 0) continue;
                var flagged = (a + day) % 17 == 0;
                db.ShiftAttendances.Add(new ShiftAttendance { UserId = user.Id, AreaId = areas[a].Id, SignId = checkins[a].Id,
                    ShiftDate = shiftDate, Shift = shift, EventType = eventType, Source = AttendanceSource.Scan,
                    OccurredAt = start.AddMinutes(offset), CreatedAt = start.AddMinutes(offset), Latitude = flagged ? 13.7663m : 13.7563m,
                    Longitude = 100.5018m, AccuracyM = 5, DistanceM = flagged ? (short)1100 : (short)0, WithinRadius = !flagged });
            }
        }
        var scans = new List<(ScanRecord Scan, int Area, bool Rework)>();
        foreach (var (a, point, sign, windows) in points)
            foreach (var (shift, window) in windows)
            {
                if (shift == Shift.Night && Utc(shiftDate.AddDays(1), 7) > seedNow) continue;
                if ((a + day) % 11 == 0 || (point.Id + day) % 7 == 0) continue;
                var submitted = Utc(shiftDate, shift == Shift.Day ? 8 : 20).AddMinutes(point.SortOrder);
                var issue = (point.Id + day) % 9 == 0;
                var scan = new ScanRecord { ServicePointId = point.Id, SignId = sign.Id, UserId = cleaners[(a, shift)].Id,
                    ShiftDate = shiftDate, Shift = shift, RoundWindowId = window.Id, RoundStart = window.StartTime, RoundEnd = window.EndTime,
                    Placement = Placement.OnTime, Status = issue ? CleaningStatus.Issue : CleaningStatus.Normal,
                    IssueTags = issue ? "พื้นเปียก" : null, Note = issue ? "ข้อมูลทดสอบ: พื้นเปียก" : null,
                    SubmittedAt = submitted, Latitude = 13.7563m, Longitude = 100.5018m, AccuracyM = 5, DistanceM = 0, WithinRadius = true };
                db.ScanRecords.Add(scan);
                scans.Add((scan, a, (point.Id + day) % 10 == 1));
            }
        await db.SaveChangesAsync();
        foreach (var (scan, a, rework) in scans)
        {
            if ((scan.ServicePointId + day) % 5 == 0) continue; // submitted, never inspected
            var flagged = (scan.ServicePointId + day) % 19 == 0;
            db.InspectionRecords.Add(new InspectionRecord { ScanRecordId = scan.Id, ServicePointId = scan.ServicePointId,
                SupervisorId = supervisors[(a % buildings.Count, scan.Shift)].Id, Result = rework ? InspectionResult.Rework : InspectionResult.Passed,
                Defect = rework ? "ข้อมูลทดสอบ: กรุณาเช็ดพื้นอีกครั้ง" : null, InspectedAt = scan.SubmittedAt.AddMinutes(10),
                Latitude = flagged ? 13.7663m : 13.7563m, Longitude = 100.5018m, AccuracyM = 5,
                DistanceM = flagged ? (short)1100 : (short)0, WithinRadius = !flagged });
        }
        // Actual setup snapshots: one synthetic Admin creation event per point per date.
        // Repeated updates alternate sort order, with matching before/after; no fabricated credentials.
        foreach (var (a, point, _, _) in points)
            db.AuditLog.Add(new AuditEntry { ActorId = admin.Id, OccurredAt = Utc(shiftDate, 10).AddSeconds(point.Id),
                Action = "POINT_UPDATE", EntityType = "service_points", EntityId = point.Id,
                Summary = $"E2E-HISTORY {shiftDate:yyyyMMdd} {areas[a].Code} {point.Name}",
                BeforeJson = JsonSerializer.Serialize(new { SortOrder = day % 2 == 0 ? point.SortOrder : point.SortOrder + 1 }),
                AfterJson = JsonSerializer.Serialize(new { SortOrder = day % 2 == 0 ? point.SortOrder + 1 : point.SortOrder }) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
    // Historical covers, one target per shift; all synthetic and in the past.
    for (var a = 0; a < Math.Min(volume.Areas - 1, 20); a++)
    {
        var shiftDate = anchor.AddDays(-2);
        db.CoverAssignments.Add(new CoverAssignment { UserId = cleaners[(a, Shift.Day)].Id, AreaId = areas[a + 1].Id,
            ShiftDate = shiftDate, Shift = Shift.Day, AssignedById = admin.Id, AssignedAt = Utc(shiftDate.AddDays(-1), 12),
            CancelledById = a % 2 == 0 ? admin.Id : null, CancelledAt = a % 2 == 0 ? Utc(shiftDate, 6) : null });
    }
    await db.SaveChangesAsync();
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO __e2e_dataset (identity) VALUES ({identity})");
    await transaction.CommitAsync();
}

// Persist measured counts, not estimates, and fail the run on semantic relationship violations.
var invalidScans = await db.ScanRecords.CountAsync(s => s.Sign!.ServicePointId != s.ServicePointId
    || s.User!.AreaId != s.ServicePoint!.AreaId || s.User.Shift != s.Shift);
var invalidInspections = await db.InspectionRecords.CountAsync(i => i.ScanRecord!.ServicePointId != i.ServicePointId
    || i.Supervisor!.Shift != i.ScanRecord.Shift || i.Supervisor.BuildingId != i.ScanRecord.ServicePoint!.Area!.BuildingId
    || i.InspectedAt < i.ScanRecord.SubmittedAt);
if (invalidScans + invalidInspections != 0) throw new InvalidOperationException("Invalid dataset relationships");
var futureEvents = await db.ShiftAttendances.CountAsync(a => a.OccurredAt > seedNow)
    + await db.ScanRecords.CountAsync(s => s.SubmittedAt > seedNow)
    + await db.InspectionRecords.CountAsync(i => i.InspectedAt > seedNow);
var reworks = await db.InspectionRecords.CountAsync(i => i.Result == InspectionResult.Rework);
var passed = await db.InspectionRecords.CountAsync(i => i.Result == InspectionResult.Passed);
var uninspected = await db.ScanRecords.CountAsync(s => !db.InspectionRecords.Any(i => i.ScanRecordId == s.Id));
if (futureEvents != 0 || reworks == 0 || passed == 0 || uninspected == 0)
    throw new InvalidOperationException("Missing semantic categories or future events in dataset");
// Before 07:00 Bangkok, yesterday's Night shift is still live. Exclude that
// whole shift date so subsequent browser inspections cannot change this truth.
var currentShiftDate = seedNow.AddHours(7).Hour < 7 ? today.AddDays(-1) : today;
var summaryFrom = anchor.AddDays(-14);
var summaryTo = anchor.AddDays(-1) < currentShiftDate ? anchor.AddDays(-1) : currentShiftDate.AddDays(-1);
var expectedSummary = await db.InspectionRecords
    .Where(i => i.ScanRecord!.ShiftDate >= summaryFrom && i.ScanRecord.ShiftDate <= summaryTo)
    .GroupBy(i => i.Supervisor!.EmployeeId)
    .Select(g => new { employeeId = g.Key, inspections = g.Count(), reworks = g.Count(i => i.Result == InspectionResult.Rework), gpsFlagged = g.Count(i => i.WithinRadius == false) })
    .ToListAsync();
var report = new {
    identity, profile, anchorDate = anchor.ToString("yyyy-MM-dd"), reused = existing is not null,
    seedSeconds = Math.Round(timer.Elapsed.TotalSeconds, 2),
    counts = new { buildings = await db.Buildings.CountAsync(), areas = await db.Areas.CountAsync(),
        users = await db.Users.CountAsync(), points = await db.ServicePoints.CountAsync(), signs = await db.Signs.CountAsync(),
        windows = await db.PointRoundWindows.CountAsync(), attendance = await db.ShiftAttendances.CountAsync(),
        scans = await db.ScanRecords.CountAsync(), inspections = await db.InspectionRecords.CountAsync(),
        covers = await db.CoverAssignments.CountAsync(), audit = await db.AuditLog.CountAsync() },
    roleCounts = new { cleaner = await db.Users.CountAsync(u => u.Role == UserRole.Cleaner),
        supervisor = await db.Users.CountAsync(u => u.Role == UserRole.Supervisor), admin = await db.Users.CountAsync(u => u.Role == UserRole.Admin) },
    invalidScans, invalidInspections, futureEvents, reworks, passed, uninspected,
    seedNow = seedNow.ToString("O"), summaryFromDate = summaryFrom.ToString("yyyy-MM-dd"),
    summaryToDate = summaryTo.ToString("yyyy-MM-dd"), expectedSummary,
};
var output = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(output);
var runDir = Environment.GetEnvironmentVariable("E2E_RUN_DIR");
if (runDir is not null) await File.WriteAllTextAsync(Path.Combine(runDir, existing is null ? "dataset.json" : "dataset-repeat.json"), output);

static DateTime Utc(DateOnly date, int thaiHour) => DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(thaiHour, 0)).AddHours(-7), DateTimeKind.Utc);
static Sign Located(Sign sign, DateTime created)
{
    sign.Latitude = 13.7563m; sign.Longitude = 100.5018m; sign.LocationAccuracyM = 5;
    sign.LocationSource = LocationSource.Site; sign.LocatedAt = created;
    return sign;
}
