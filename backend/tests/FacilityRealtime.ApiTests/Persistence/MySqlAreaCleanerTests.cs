using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Auth;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

/// <summary>users.cleaner_slot is a stored computed column with a UNIQUE index, checked row by row on MySQL.</summary>
public class MySqlAreaCleanerTests
{
    [MySqlFact]
    public async Task Replacing_an_areas_cleaner_passes_the_unique_slot_on_MySQL()
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
            int area1Id;
            await using (var write = new AppDbContext(options))
            {
                await DbInitializer.SeedAsync(write, new Pbkdf2PasswordHasher(iterations: 1_000));
                area1Id = (await write.Areas.SingleAsync(a => a.Code == "AR01")).Id;
                var newcomer = new User
                {
                    Role = UserRole.Cleaner,
                    EmployeeId = "E1009",
                    DisplayName = "แม่บ้านทดสอบ E1009",
                    SecretHash = "not-a-hash",
                    Shift = Shift.Day,
                    CreatedAt = DateTime.UtcNow,
                };
                write.Users.Add(newcomer);
                await write.SaveChangesAsync();

                // Why AreaCleaners saves twice: two active Day Cleaners on AR01, even for one statement, are refused
                newcomer.AreaId = area1Id;
                await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());
            }

            await using (var write = new AppDbContext(options))
            {
                var newcomer = await write.Users.SingleAsync(u => u.EmployeeId == "E1009");
                var night = await write.Users.SingleAsync(u => u.EmployeeId == "E1002");
                await using var transaction = await write.Database.BeginTransactionAsync();
                await AreaCleaners.ApplyAsync(write, area1Id, newcomer, night);
                await transaction.CommitAsync();
            }

            await using var read = new AppDbContext(options);
            Assert.Null((await read.Users.SingleAsync(u => u.EmployeeId == "E1001")).AreaId);
            Assert.Equal(area1Id, (await read.Users.SingleAsync(u => u.EmployeeId == "E1009")).AreaId);
            Assert.Equal(area1Id, (await read.Users.SingleAsync(u => u.EmployeeId == "E1002")).AreaId);
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
