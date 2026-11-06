using System.Data;
using System.Data.Common;
using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence;
using JanitorialManagementDashboard.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace JanitorialManagementDashboard.ApiTests.Retention;

public class RetentionGpsRaceTests
{
    [Fact]
    public async Task GPS_update_rechecks_age_if_the_timestamp_changes_after_batch_selection()
    {
        var race = new CorrectTimestamp();
        using var factory = new JanitorialApiFactory { ConfigureTestServices = services => services.AddDbContext<AppDbContext>(o => o.AddInterceptors(race)) };
        factory.Clock.SetUtcNow(new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
        await factory.WithDbAsync(async db =>
        {
            var sign = await db.Signs.FirstAsync(s => s.ServicePointId != null);
            var user = await db.Users.FirstAsync(u => u.Role == UserRole.Cleaner);
            var row = new ScanRecord { UserId = user.Id, ServicePointId = sign.ServicePointId!.Value, SignId = sign.Id,
                ShiftDate = new(2026, 8, 1), SubmittedAt = factory.Clock.GetUtcNow().UtcDateTime.AddDays(-100), Latitude = 1, AccuracyM = 10 };
            db.ScanRecords.Add(row); await db.SaveChangesAsync();
            race.Id = row.Id; race.Now = factory.Clock.GetUtcNow().UtcDateTime;
            var result = await new RetentionRunner(db, factory.Clock, new()).RunAsync(default);
            Assert.True(race.Corrected); Assert.Equal(0, result.GpsCleared);
            db.ChangeTracker.Clear();
            Assert.NotNull((await db.ScanRecords.SingleAsync()).Latitude);
        });
    }

    private sealed class CorrectTimestamp : DbCommandInterceptor
    {
        public long Id; public DateTime Now; public bool Corrected;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Id != 0 && !Corrected && command.CommandText.StartsWith("UPDATE \"scan_records\"", StringComparison.Ordinal))
            {
                Corrected = true;
                await using var correction = command.Connection!.CreateCommand();
                correction.Transaction = command.Transaction;
                correction.CommandText = "UPDATE scan_records SET submitted_at = @now WHERE id = @id";
                var now = correction.CreateParameter(); now.ParameterName = "@now"; now.DbType = DbType.DateTime; now.Value = Now;
                var id = correction.CreateParameter(); id.ParameterName = "@id"; id.Value = Id;
                correction.Parameters.Add(now); correction.Parameters.Add(id);
                await correction.ExecuteNonQueryAsync(cancellationToken);
            }
            return result;
        }
    }
}
