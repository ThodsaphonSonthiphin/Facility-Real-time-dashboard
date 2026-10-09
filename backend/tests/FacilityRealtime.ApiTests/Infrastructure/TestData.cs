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
}
