using System.Globalization;
using JanitorialManagementDashboard.Application.Attendance;
using JanitorialManagementDashboard.Application.Shifts;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

internal sealed class SchedulingScenario(DateTime now, AttendanceSettings settings)
{
    private const string Prefix = "E2ESCH";
    internal const string EmployeePrefix = "61";
    internal DateTime ObservedAt => now;
    private readonly ShiftSlot _slot = ShiftCalendar.SlotAt(now);
    private DateTime Start => ShiftCalendar.StartUtc(_slot);
    private DateTime End => ShiftCalendar.EndUtc(_slot);
    private (DateTime Opens, DateTime Closes) Window => AttendanceCalendar.Window(_slot, settings);
    private DateTime FixtureCreatedAt => Window.Opens.AddMinutes(-1);
    private DateOnly WeekStart => _slot.ShiftDate.AddDays(-((int)_slot.ShiftDate.DayOfWeek + 6) % 7);
    private DateOnly HistoryStarts => WeekStart.AddDays(-7);
    private DateTime LeaveStarts => new(Math.Max(Start.Ticks,
        Math.Min(now.AddMinutes(-2).Ticks, End.AddMinutes(-30).Ticks)) / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute,
        DateTimeKind.Utc);
    private DateTime LeaveEnds => now < End.AddMinutes(-10) ? End : Window.Closes;
    private DateTime Effective => new(Math.Max(Start.Ticks, now.AddSeconds(-30).Ticks), DateTimeKind.Utc);

    public static SchedulingScenario? ForProfile(string profile)
    {
        if (profile != "large" || Environment.GetEnvironmentVariable("E2E_SCENARIO_NOW") is not string instant)
        {
            return null;
        }

        // This is a single observed wall-clock instant shared by the two seed passes. The API still uses System time.
        var utc = DateTime.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        if (utc > DateTime.UtcNow)
        {
            throw new InvalidOperationException("The scheduling scenario clock must come from the current run");
        }

        var settings = new AttendanceSettings();
        if (int.TryParse(Environment.GetEnvironmentVariable("Attendance__OpensMinutesBeforeShift"), out var opens))
        {
            settings.OpensMinutesBeforeShift = opens;
        }
        if (int.TryParse(Environment.GetEnvironmentVariable("Attendance__ClosesMinutesAfterShift"), out var closes))
        {
            settings.ClosesMinutesAfterShift = closes;
        }
        return new SchedulingScenario(utc, settings);
    }

    public async Task SeedAsync(AppDbContext db, User admin, string hash)
    {
        var a = NewBuilding("A");
        var b = NewBuilding("B");
        var c = NewBuilding("C");
        var leadAB = NewEmployee(1, "หัวหน้าตาราง เอและบี", UserRole.Supervisor, a);
        var leadC = NewEmployee(2, "หัวหน้าตาราง ซี", UserRole.Supervisor, c);
        var home = NewArea(a, "FROM", "พื้นที่ก่อนย้าย");
        var destination = NewArea(b, "TO", "พื้นที่ปลายทาง");
        var leaveArea = NewArea(a, "LEAVE", "พื้นที่ออกครึ่งกะ");
        var weekly = NewArea(a, "WEEK", "พื้นที่วันหยุด");
        var competing = NewArea(b, "RACE", "พื้นที่แก้ตารางพร้อมกัน");

        var relief = NewEmployee(5, "สแปร์ทดสอบ ซี", UserRole.Cleaner);
        var leave = NewEmployee(6, "แม่บ้านทดสอบลา", UserRole.Cleaner, area: leaveArea);
        var transfer = NewEmployee(7, "แม่บ้านทดสอบย้าย", UserRole.Cleaner, area: home);
        var homeSign = NewSign(home);
        var leaveSign = NewSign(leaveArea);
        var destinationSign = NewSign(destination);
        var point = new ServicePoint { Area = home, Name = "จุดงานก่อนย้าย", SortOrder = 1, CreatedAt = FixtureCreatedAt };
        var pointSign = NewSign(home, point);

        db.Buildings.AddRange(a, b, c);
        db.Areas.AddRange(home, destination, leaveArea, weekly, competing);
        db.Users.AddRange(leadAB, leadC, NewEmployee(3, "แม่บ้านทดสอบวันหยุด", UserRole.Cleaner, area: weekly),
            NewEmployee(4, "แม่บ้านทดสอบตารางซ้ำ", UserRole.Cleaner, area: competing), relief, leave, transfer);
        db.Signs.AddRange(homeSign, leaveSign, destinationSign, pointSign);
        await db.SaveChangesAsync();

        // Before the generated legacy roster is inserted, run the same compatibility backfill used for known setup.
        await ScheduleBackfill.EnsureAsync(db, HistoryStarts, CancellationToken.None);
        db.SupervisorScopePeriods.Add(new SupervisorScopePeriod
        {
            SupervisorId = leadAB.Id, BuildingId = b.Id, Shift = _slot.Shift, EffectiveFrom = HistoryStarts,
            CreatedAt = FixtureCreatedAt, UpdatedAt = FixtureCreatedAt, CreatedById = admin.Id,
        });
        db.CleanerProfilePeriods.Add(new CleanerProfilePeriod
        {
            UserId = relief.Id, Kind = CleanerKind.Relief, HomeBuildingId = c.Id, Shift = _slot.Shift,
            EffectiveFrom = HistoryStarts, CreatedAt = FixtureCreatedAt, UpdatedAt = FixtureCreatedAt, CreatedById = admin.Id,
        });
        var leaveBaseline = await db.ScheduleBaselines.Include(baseline => baseline.Days)
            .SingleAsync(baseline => baseline.UserId == leave.Id);
        foreach (var day in leaveBaseline.Days)
        {
            day.BreakGroup = 1;
        }

        var leaveIn = now.AddMinutes(-8);
        // Leave needs an established ShiftIn beyond the normal five-minute repeat guard. When this run starts
        // at a receiving-window boundary, report the fixture problem instead of backdating a scan outside the window.
        if (leaveIn < Window.Opens || now >= Window.Closes)
        {
            throw new InvalidOperationException("The current receiving window does not allow the acceptance fixture timeline");
        }
        db.ShiftAttendances.AddRange(Scan(leave, leaveArea, leaveSign, AttendanceEvent.ShiftIn, leaveIn),
            Scan(transfer, home, homeSign, AttendanceEvent.ShiftIn, leaveIn),
            Scan(transfer, home, homeSign, AttendanceEvent.Break1Out, now.AddMinutes(-2)));
        db.ScanRecords.Add(new ScanRecord
        {
            UserId = transfer.Id, ServicePointId = point.Id, SignId = pointSign.Id,
            ShiftDate = _slot.ShiftDate, Shift = _slot.Shift, Placement = Placement.OffRound, SubmittedAt = now.AddMinutes(-3),
            Latitude = homeSign.Latitude, Longitude = homeSign.Longitude, AccuracyM = 5, DistanceM = 0, WithinRadius = true,
        });
        db.AreaTransfers.Add(new AreaTransfer
        {
            PersonId = transfer.Id, ShiftDate = _slot.ShiftDate, Shift = _slot.Shift, FromAreaId = home.Id, ToAreaId = destination.Id,
            EffectiveAt = Effective, Reason = "Synthetic authority to move after source work", CreatedById = leadAB.Id,
            CreatedAt = Effective.AddSeconds(-1), UpdatedAt = Effective.AddSeconds(-1),
        });
        await db.SaveChangesAsync();

        Building NewBuilding(string value) => new()
        {
            Code = Prefix + value,
            Name = value == "A" ? "อาคารทดสอบการจัดตารางงานหลายพื้นที่สำหรับหน้าจอขนาดเล็ก" : "อาคารตาราง " + value,
            CreatedAt = FixtureCreatedAt,
        };
        Area NewArea(Building building, string suffix, string name) => new()
        {
            Building = building, Code = Prefix + "-" + suffix, Name = name,
            ShiftPattern = ShiftPattern.DayAndNight, CreatedAt = FixtureCreatedAt,
        };
        User NewEmployee(int number, string name, UserRole role, Building? building = null, Area? area = null) => new()
        {
            Role = role, DisplayName = name, EmployeeId = EmployeeId(number), SecretHash = hash,
            Building = building, Area = area, Shift = _slot.Shift, CreatedAt = FixtureCreatedAt,
        };
        Sign NewSign(Area area, ServicePoint? servicePoint = null) => new()
        {
            Code = area.Code + (servicePoint is null ? "-IN" : "-P1"), Area = area, ServicePoint = servicePoint,
            QrToken = Guid.NewGuid().ToString("N"), QrIssuedAt = FixtureCreatedAt, LocatedAt = FixtureCreatedAt,
            Latitude = 13.7563m, Longitude = 100.5018m, LocationAccuracyM = 5, LocationSource = LocationSource.Site,
        };
        ShiftAttendance Scan(User person, Area area, Sign sign, AttendanceEvent action, DateTime instant) => new()
        {
            UserId = person.Id, AreaId = area.Id, SignId = sign.Id, ShiftDate = _slot.ShiftDate, Shift = _slot.Shift,
            EventType = action, Source = AttendanceSource.Scan, OccurredAt = instant, CreatedAt = instant,
            Latitude = sign.Latitude, Longitude = sign.Longitude, AccuracyM = 5, DistanceM = 0, WithinRadius = true,
        };
    }

    public async Task<object> ReadManifestAsync(AppDbContext db)
    {
        var buildings = await db.Buildings.AsNoTracking().Where(b => b.Code.StartsWith(Prefix)).ToDictionaryAsync(b => b.Code);
        var people = await db.Users.AsNoTracking().Where(u => u.EmployeeId != null && u.EmployeeId.StartsWith(EmployeePrefix))
            .ToDictionaryAsync(u => u.EmployeeId!);
        var signs = await db.Signs.AsNoTracking().Include(sign => sign.Area)
            .Where(sign => sign.Code.StartsWith(Prefix)).ToListAsync();
        return new
        {
            shiftDate = _slot.ShiftDate, shift = _slot.Shift.ToString(), weekStart = WeekStart, planningWeek = WeekStart.AddDays(7),
            buildingA = Building("A"), buildingB = Building("B"), buildingC = Building("C"),
            supervisorAB = Employee(1), supervisorC = Employee(2), weekly = Employee(3), competing = Employee(4),
            relief = Employee(5), leave = Employee(6), transfer = Employee(7),
            leaveArea = Area("LEAVE"), homeArea = Area("FROM"), destinationArea = Area("TO"),
            leaveStartsAt = LeaveStarts, leaveEndsAt = LeaveEnds, transferEffectiveAt = Effective,
        };

        object Building(string suffix)
        {
            var building = buildings[Prefix + suffix];
            return new { id = building.Id, code = building.Code };
        }
        object Employee(int number)
        {
            var user = people[EmployeeId(number)];
            return new { id = user.Id, employeeId = user.EmployeeId, name = user.DisplayName };
        }
        object Area(string suffix)
        {
            var kind = Prefix + "-" + suffix;
            var checkIn = signs.Single(sign => sign.Code == kind + "-IN");
            return new
            {
                id = checkIn.AreaId, code = checkIn.Area!.Code, checkInToken = checkIn.QrToken,
                pointToken = signs.SingleOrDefault(sign => sign.Code == kind + "-P1")?.QrToken,
            };
        }
    }

    private static string EmployeeId(int number) => $"{EmployeePrefix}{number:D5}";
}
