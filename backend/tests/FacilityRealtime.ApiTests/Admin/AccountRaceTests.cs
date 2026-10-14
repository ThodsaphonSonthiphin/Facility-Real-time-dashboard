using System.Data.Common;
using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityRealtime.ApiTests.Admin;

/// <summary>
/// Plan-4 ruling: a DbUpdateException becomes a 409 only when re-reading the database proves another save took the
/// login name or the slot; any other database failure is rethrown. These tests make a save lose the race on purpose.
/// </summary>
public class AccountRaceTests
{
    [Fact]
    public async Task A_cleaner_who_loses_the_slot_race_gets_409_naming_the_winner()
    {
        var race = new RacingInsertInterceptor();
        using var factory = new FacilityApiFactory { ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(race)) };
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        var ar03 = await TestData.AddAreaAsync(factory, "AR03");
        // Committed by "another save" after the pre-checks passed and before this save's transaction starts
        race.Arm($"""
            INSERT INTO users (role, employee_id, display_name, secret_hash, area_id, shift, is_active, created_at)
            VALUES ('CLEANER', 'E9001', 'แม่บ้านคู่แข่ง ทดสอบ', 'not-a-real-hash', {ar03}, 'DAY', 1, '2026-10-14 00:00:00')
            """);

        var response = await AccountApi.CreateCleanerAsync(admin, "E2101", "0000000031", ar03, "Day");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("กะเช้าของ AR03 มีแม่บ้านประจำแล้ว (แม่บ้านคู่แข่ง ทดสอบ)", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Users.AnyAsync(u => u.EmployeeId == "E2101"));
            Assert.True(await db.Users.AnyAsync(u => u.EmployeeId == "E9001"));
        });
    }

    [Fact]
    public async Task A_cleaner_who_loses_the_employee_id_race_gets_409()
    {
        var race = new RacingInsertInterceptor();
        using var factory = new FacilityApiFactory { ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(race)) };
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        race.Arm("""
            INSERT INTO users (role, employee_id, display_name, secret_hash, shift, is_active, created_at)
            VALUES ('CLEANER', 'E2102', 'แม่บ้านคู่แข่ง ทดสอบ', 'not-a-real-hash', 'NIGHT', 1, '2026-10-14 00:00:00')
            """);

        var response = await AccountApi.CreateCleanerAsync(admin, "E2102", "0000000032", areaId: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("รหัสพนักงานนี้มีแล้ว", await AdminSetupApi.MessageAsync(response));
        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    [Fact]
    public async Task A_database_failure_that_is_no_race_is_not_turned_into_409()
    {
        var failing = new FailingUserSaveInterceptor();
        using var factory = new FacilityApiFactory { ConfigureTestServices = services => services.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(failing)) };
        var (admin, _, _) = await AuthApi.LoggedInAdminAsync(factory);
        failing.Armed = true;   // the seeder also adds Users, so only fail after the login

        // The test host has no exception handler, so the rethrown DbUpdateException reaches the client call itself
        await Assert.ThrowsAsync<DbUpdateException>(() => AccountApi.CreateCleanerAsync(admin, "E2103", "0000000033", areaId: null));

        Assert.Empty(await AdminSetupApi.AuditAsync(factory));
    }

    /// <summary>Runs one raw INSERT on the connection just before the next explicit transaction starts, so it is committed on its own.</summary>
    private sealed class RacingInsertInterceptor : DbTransactionInterceptor
    {
        private string? _sql;

        public void Arm(string sql) => _sql = sql;

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _sql, null) is { } sql)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }

    /// <summary>Fails the save of a new User with a DbUpdateException that no login name or slot explains.</summary>
    private sealed class FailingUserSaveInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<FacilityRealtime.Domain.Entities.User>().Any(e => e.State == EntityState.Added))
            {
                throw new DbUpdateException("simulated database failure");
            }

            return ValueTask.FromResult(result);
        }
    }
}
