using FacilityRealtime.Application.Rounds;
using FacilityRealtime.Application.Shifts;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>Writes Scan and Inspection Records straight to the database, for tests that are about reading them.</summary>
public static class TestData
{
    /// <summary>A Scan Record by cleaner E1001 in the round that is current at <paramref name="at"/> (placement OnTime).</summary>
    public static async Task<long> AddScanAsync(
        FacilityApiFactory factory,
        string qrToken,
        DateTimeOffset at,
        CleaningStatus status = CleaningStatus.Normal,
        string? issueTags = null,
        string? note = null)
    {
        long id = 0;
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.SingleAsync(s => s.QrToken == qrToken);
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");
            var slot = ShiftCalendar.SlotAt(at.UtcDateTime);
            var windows = await db.PointRoundWindows
                .Where(w => w.ServicePointId == sign.ServicePointId && w.Shift == slot.Shift)
                .ToListAsync();
            var round = windows
                .Select(w => RoundWindow.For(w.Id, w.StartTime, w.EndTime, slot))
                .Where(w => w.StartUtc <= at.UtcDateTime)
                .MaxBy(w => w.StartUtc);

            var record = new ScanRecord
            {
                ServicePointId = sign.ServicePointId!.Value,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = slot.ShiftDate,
                Shift = slot.Shift,
                RoundWindowId = round?.Id,
                RoundStart = round?.Start,
                RoundEnd = round?.End,
                Placement = round is null ? Placement.OffRound : Placement.OnTime,
                Status = status,
                IssueTags = issueTags,
                Note = note,
                SubmittedAt = at.UtcDateTime,
            };
            db.ScanRecords.Add(record);
            await db.SaveChangesAsync();
            id = record.Id;
        });
        return id;
    }

    /// <summary>An Inspection Record by Supervisor S2001.</summary>
    public static Task AddInspectionAsync(FacilityApiFactory factory, long scanRecordId, InspectionResult result, DateTimeOffset at) =>
        factory.WithDbAsync(async db =>
        {
            var scan = await db.ScanRecords.SingleAsync(s => s.Id == scanRecordId);
            var supervisor = await db.Users.SingleAsync(u => u.EmployeeId == "S2001");
            db.InspectionRecords.Add(new InspectionRecord
            {
                ScanRecordId = scan.Id,
                ServicePointId = scan.ServicePointId,
                SupervisorId = supervisor.Id,
                Result = result,
                Defect = result == InspectionResult.Rework ? "พื้นยังเปียก" : null,
                InspectedAt = at.UtcDateTime,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>An active Cleaner who never logs in, on <paramref name="shift"/>, in no Area unless one is given.</summary>
    public static async Task<int> AddCleanerAsync(FacilityApiFactory factory, string employeeId, Shift shift, string? areaCode = null)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var area = areaCode is null ? null : await db.Areas.SingleAsync(a => a.Code == areaCode);
            var cleaner = new User
            {
                Role = UserRole.Cleaner,
                EmployeeId = employeeId,
                DisplayName = $"แม่บ้านทดสอบ {employeeId}",
                SecretHash = "not-a-hash",
                AreaId = area?.Id,
                Shift = shift,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(cleaner);
            await db.SaveChangesAsync();
            id = cleaner.Id;
        });
        return id;
    }

    /// <summary>A Cover Assignment written straight to the database, assigned by the seeded Admin.</summary>
    public static async Task<int> AddCoverAsync(
        FacilityApiFactory factory, string employeeId, string areaCode, DateOnly shiftDate, Shift shift, bool cancelled = false)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var area = await db.Areas.SingleAsync(a => a.Code == areaCode);
            var admin = await db.Users.SingleAsync(u => u.Username == "admin");
            var cover = new CoverAssignment
            {
                UserId = cleaner.Id,
                AreaId = area.Id,
                ShiftDate = shiftDate,
                Shift = shift,
                AssignedById = admin.Id,
                AssignedAt = DateTime.UtcNow,
                CancelledById = cancelled ? admin.Id : null,
                CancelledAt = cancelled ? DateTime.UtcNow : null,
            };
            db.CoverAssignments.Add(cover);
            await db.SaveChangesAsync();
            id = cover.Id;
        });
        return id;
    }

    /// <summary>A new building with no Areas, e.g. "B".</summary>
    public static async Task<int> AddBuildingAsync(FacilityApiFactory factory, string code)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var building = new Building { Code = code, Name = $"ตึก {code}", CreatedAt = DateTime.UtcNow };
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
            id = building.Id;
        });
        return id;
    }

    /// <summary>A new Area with its check-in sign and no Cleaner, e.g. "AR03" in building A.</summary>
    public static async Task<int> AddAreaAsync(
        FacilityApiFactory factory, string code, string buildingCode = "A", ShiftPattern pattern = ShiftPattern.DayAndNight)
    {
        var id = 0;
        await factory.WithDbAsync(async db =>
        {
            var building = await db.Buildings.SingleAsync(b => b.Code == buildingCode);
            var area = new Area { BuildingId = building.Id, Code = code, Name = $"Area {code}", ShiftPattern = pattern, CreatedAt = DateTime.UtcNow };
            db.Signs.Add(new Sign { Area = area, Code = $"{code}-IN", QrToken = $"token-checkin-{code.ToLowerInvariant()}", QrIssuedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            id = area.Id;
        });
        return id;
    }

    /// <summary>
    /// One attendance event of a Cleaner in their own Area, written straight to the database. A scan gets the Area's
    /// check-in sign and a GPS verdict; an Admin addition has neither (facility-0051).
    /// </summary>
    public static Task AddAttendanceAsync(
        FacilityApiFactory factory,
        string employeeId,
        DateOnly shiftDate,
        Shift shift,
        AttendanceEvent eventType,
        DateTimeOffset at,
        AttendanceSource source = AttendanceSource.Scan,
        short distanceM = 12,
        bool withinRadius = true) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.EmployeeId == employeeId);
            var sign = await db.Signs.SingleAsync(s => s.AreaId == user.AreaId && s.ServicePointId == null);
            var scanned = source != AttendanceSource.AdminAdd;
            db.ShiftAttendances.Add(new ShiftAttendance
            {
                UserId = user.Id,
                AreaId = user.AreaId!.Value,
                ShiftDate = shiftDate,
                Shift = shift,
                EventType = eventType,
                OccurredAt = at.UtcDateTime,
                Source = source,
                SignId = scanned ? sign.Id : null,
                Latitude = scanned ? 13.756300m : null,
                Longitude = scanned ? 100.501800m : null,
                AccuracyM = scanned ? (short)10 : null,
                DistanceM = scanned ? distanceM : null,
                WithinRadius = scanned ? withinRadius : null,
                CreatedAt = at.UtcDateTime,
            });
            await db.SaveChangesAsync();
        });

    /// <summary>
    /// The seed stamps its points and windows with the real clock, which is later than the October 2026 test dates. Move
    /// them back to 1 October so rounds on test dates existed when they started (export "ไม่ได้ทำ").
    /// </summary>
    public static Task BackdateSetupAsync(FacilityApiFactory factory) =>
        factory.WithDbAsync(async db =>
        {
            var longAgo = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.PointRoundWindows.ExecuteUpdateAsync(s => s.SetProperty(w => w.CreatedAt, longAgo));
            await db.ServicePoints.ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, longAgo));
        });
}
