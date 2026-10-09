using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>The Oracle MySQL provider returns DATE/TIME as DateTime/TimeSpan; SQLite hides that, so this reads DateOnly/TimeOnly back from real MySQL.</summary>
public class MySqlReadTests
{
    [MySqlFact]
    public async Task DateOnly_and_TimeOnly_columns_are_read_back_from_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("FACILITY_MYSQL_TEST")!)
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            int userId, pointId, signId;
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var sign = await write.Signs.SingleAsync(s => s.QrToken == "token-restroom-m1");
                signId = sign.Id;
                pointId = sign.ServicePointId!.Value;
                userId = (await write.Users.FirstAsync()).Id;
                write.ScanRecords.Add(new ScanRecord
                {
                    ServicePointId = pointId,
                    SignId = signId,
                    UserId = userId,
                    ShiftDate = new DateOnly(2026, 10, 8),
                    Shift = Shift.Day,
                    RoundStart = new TimeOnly(7, 0),
                    RoundEnd = new TimeOnly(9, 0),
                    Placement = Placement.OnTime,
                    SubmittedAt = DateTime.UtcNow,
                });
                await write.SaveChangesAsync();
            }

            await using var read = new AppDbContext(options);
            var windows = await read.PointRoundWindows.AsNoTracking().ToListAsync();
            Assert.NotEmpty(windows);
            Assert.All(windows, w => Assert.NotEqual(w.StartTime, w.EndTime));

            var scan = await read.ScanRecords.AsNoTracking().SingleAsync();
            Assert.Equal(new DateOnly(2026, 10, 8), scan.ShiftDate);
            Assert.Equal(new TimeOnly(7, 0), scan.RoundStart);
            Assert.Equal(new TimeOnly(9, 0), scan.RoundEnd);
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
