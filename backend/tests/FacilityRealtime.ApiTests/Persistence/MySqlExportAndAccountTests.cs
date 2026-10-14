using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>
/// The export reads DATE ranges, joins and nullable flags, and the accounts page compares UPPER(employee_id). SQLite hides
/// how the Oracle MySQL provider treats them, so this runs them on real MySQL. It also runs the deactivation save plus
/// the ExecuteUpdate that revokes sessions inside one explicit transaction, as the accounts endpoint does.
/// </summary>
[Collection("MySQL")]
public class MySqlExportAndAccountTests
{
    [MySqlFact]
    public async Task Export_and_account_queries_run_on_MySQL()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(MySqlTestDatabase.ConnectionString())
            .Options;

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.EnsureCreatedAsync();
        }

        try
        {
            var oct8 = new DateOnly(2026, 10, 8);
            var shiftIn = new DateTime(2026, 10, 7, 23, 58, 0, DateTimeKind.Utc);   // 06:58 Thai on 8 Oct
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                var cleaner = await write.Users.SingleAsync(u => u.EmployeeId == "E1001");
                var restroom = await write.Signs.SingleAsync(s => s.Code == "AR01-01");
                var window = await write.PointRoundWindows.FirstAsync(w => w.ServicePointId == restroom.ServicePointId && w.Shift == Shift.Day);
                write.ShiftAttendances.Add(new ShiftAttendance
                {
                    UserId = cleaner.Id, AreaId = cleaner.AreaId!.Value, ShiftDate = oct8, Shift = Shift.Day,
                    EventType = AttendanceEvent.ShiftIn, OccurredAt = shiftIn, Source = AttendanceSource.AdminAdd, CreatedAt = shiftIn,
                });
                write.ScanRecords.Add(new ScanRecord
                {
                    ServicePointId = restroom.ServicePointId!.Value, SignId = restroom.Id, UserId = cleaner.Id, ShiftDate = oct8, Shift = Shift.Day,
                    RoundWindowId = window.Id, RoundStart = window.StartTime, RoundEnd = window.EndTime, Placement = Placement.OnTime,
                    SubmittedAt = shiftIn.AddHours(1), DistanceM = 12, WithinRadius = true,
                });
                await write.SaveChangesAsync();

                // users.employee_id is unique ignoring case on MySQL, as AccountChecks assumes
                write.Users.Add(new User { Role = UserRole.Cleaner, EmployeeId = "e1001", DisplayName = "ซ้ำ", SecretHash = "x", Shift = Shift.Night, CreatedAt = shiftIn });
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());
            }

            // deactivation: the user change and the session revocation (an ExecuteUpdate) share one explicit transaction
            var deactivatedAt = shiftIn.AddHours(2);
            int deactivatedId;
            await using (var deactivate = new AppDbContext(options))
            {
                var user = await deactivate.Users.SingleAsync(u => u.EmployeeId == "E1002");
                deactivatedId = user.Id;
                if (!await deactivate.RefreshTokens.AnyAsync(t => t.UserId == user.Id))
                {
                    deactivate.RefreshTokens.Add(new RefreshToken
                    {
                        UserId = user.Id, SessionId = Guid.NewGuid(), TokenHash = new string('a', 64),
                        CreatedAt = shiftIn, ExpiresAt = shiftIn.AddDays(30),
                    });
                    await deactivate.SaveChangesAsync();
                }

                await using var transaction = await deactivate.Database.BeginTransactionAsync();
                user.IsActive = false;
                await deactivate.SaveChangesAsync();
                await deactivate.RevokeUserSessionsAsync(user.Id, deactivatedAt);
                await transaction.CommitAsync();
            }

            await using var read = new AppDbContext(options);
            var deactivated = await read.Users.AsNoTracking().SingleAsync(u => u.Id == deactivatedId);
            Assert.False(deactivated.IsActive);
            var tokens = await read.RefreshTokens.AsNoTracking().Where(t => t.UserId == deactivatedId).ToListAsync();
            Assert.NotEmpty(tokens);
            Assert.All(tokens, t => Assert.NotNull(t.RevokedAt));

            var buildingA = await read.Buildings.Where(b => b.Code == "A").Select(b => b.Id).SingleAsync();

            var attendance = await ExportQuery.LoadAttendanceAsync(read, oct8, oct8, buildingA);
            var submissions = await ExportQuery.LoadSubmissionsAsync(read, oct8, oct8.AddDays(1), null);
            var points = await ExportQuery.LoadPointsAsync(read, buildingA);
            var upperMatches = await read.Users.AnyAsync(u => u.EmployeeId != null && u.EmployeeId.ToUpper() == "E1001");

            var shiftInRow = Assert.Single(attendance);
            Assert.Equal(("E1001", "AR01", AttendanceSource.AdminAdd, (bool?)null), (shiftInRow.EmployeeId, shiftInRow.AreaCode, shiftInRow.Source, shiftInRow.WithinRadius));
            var submission = Assert.Single(submissions);
            Assert.Equal(("AR01-01", Placement.OnTime, false, (bool?)true), (submission.SignCode, submission.Placement, submission.IsCover, submission.WithinRadius));
            Assert.Contains(points, p => p.SignCode == "AR01-01" && p.Windows.Count > 0);
            Assert.True(upperMatches);
            Assert.Empty(await ExportQuery.LoadAttendanceAsync(read, oct8.AddDays(1), oct8.AddDays(2), null));
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
