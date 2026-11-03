using System.Data.Common;
using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Enums;
using FacilityRealtime.Infrastructure.Persistence;
using FacilityRealtime.Infrastructure.Persistence.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FacilityRealtime.ApiTests.Retention;

public class RetentionOrphanRaceTests
{
    [Fact]
    public async Task Both_orphan_deletes_recheck_typed_source_existence_after_selecting_the_batch()
    {
        var race = new CreateSourceBeforeDelete();
        using var factory = new FacilityApiFactory { ConfigureTestServices = services => services.AddDbContext<AppDbContext>(o => o.AddInterceptors(race)) };
        var ids = await AnnualRetentionTests.PrepareAsync(factory);
        race.User = ids.User; race.Sign = ids.Sign; race.Supervisor = ids.Supervisor; race.Now = factory.Clock.GetUtcNow().UtcDateTime;
        await factory.WithDbAsync(async db =>
        {
            race.Area = (await db.Signs.SingleAsync(s => s.Id == ids.Sign)).AreaId;
            db.AlertReviews.Add(new() { AlertType = AlertType.BlockedScan, RecordId = 999, ReviewedById = ids.Supervisor });
            db.AuditLog.Add(new() { EntityType = "cover_assignments", EntityId = 999, Action = "TEST", Summary = "live before delete" });
            await db.SaveChangesAsync();
            await new RetentionRunner(db, factory.Clock, new() { BatchSize = 1 }).RunAsync(default);
            Assert.True(race.ReviewRaced); Assert.True(race.AuditRaced);
            Assert.Single(await db.AlertReviews.ToListAsync()); Assert.Single(await db.AuditLog.ToListAsync());
            Assert.True(await db.BlockedScans.AnyAsync(s => s.Id == 999)); Assert.True(await db.CoverAssignments.AnyAsync(s => s.Id == 999));
        });
    }

    private sealed class CreateSourceBeforeDelete : DbCommandInterceptor
    {
        public int User, Sign, Area, Supervisor;
        public DateTime Now;
        public bool ReviewRaced, AuditRaced;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var review = !ReviewRaced && command.CommandText.StartsWith("DELETE FROM \"alert_reviews\"", StringComparison.Ordinal);
            var audit = !AuditRaced && command.CommandText.StartsWith("DELETE FROM \"audit_log\"", StringComparison.Ordinal);
            // Source-associated deletion earlier in the run also issues these statements. Race only the orphan NOT EXISTS sweep.
            if ((!review && !audit) || !command.CommandText.Contains("NOT EXISTS", StringComparison.Ordinal)) return result;
            await using var source = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(command.Connection!).Options);
            if (review)
            {
                ReviewRaced = true;
                source.BlockedScans.Add(new() { Id = 999, UserId = User, SignId = Sign, ScannedAt = Now });
            }
            else
            {
                AuditRaced = true;
                source.CoverAssignments.Add(new() { Id = 999, UserId = User, AreaId = Area, AssignedById = Supervisor,
                    ShiftDate = DateOnly.FromDateTime(Now), AssignedAt = Now });
            }
            await source.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}
