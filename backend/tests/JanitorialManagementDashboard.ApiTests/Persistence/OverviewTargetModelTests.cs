using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Persistence;

public class OverviewTargetModelTests
{
    [Fact]
    public async Task Origin_is_not_a_foreign_key_and_survives_round_removal()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var scanType = db.Model.FindEntityType(typeof(ScanRecord))!;
            Assert.DoesNotContain(scanType.GetForeignKeys(), fk =>
                fk.Properties.Any(p => p.Name == nameof(ScanRecord.RoundOriginId)));
            var sign = await db.Signs.SingleAsync(s => s.QrToken == "token-restroom-m1");
            var window = await db.PointRoundWindows.FirstAsync(w => w.ServicePointId == sign.ServicePointId);
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var scan = new ScanRecord
            {
                ServicePointId = sign.ServicePointId!.Value,
                SignId = sign.Id,
                UserId = cleaner.Id,
                ShiftDate = new(2026, 11, 16),
                Shift = window.Shift,
                RoundWindowId = window.Id,
                RoundOriginId = window.Id,
                RoundStart = window.StartTime,
                RoundEnd = window.EndTime,
                Placement = Placement.OnTime,
                SubmittedAt = new(2026, 11, 16, 2, 0, 0, DateTimeKind.Utc),
            };
            db.ScanRecords.Add(scan);
            await db.SaveChangesAsync();
            var origin = window.Id;
            db.PointRoundWindows.Remove(window);
            await db.SaveChangesAsync();
            await db.Entry(scan).ReloadAsync();
            Assert.Null(scan.RoundWindowId);
            Assert.Equal(origin, scan.RoundOriginId);
        });
    }
}
