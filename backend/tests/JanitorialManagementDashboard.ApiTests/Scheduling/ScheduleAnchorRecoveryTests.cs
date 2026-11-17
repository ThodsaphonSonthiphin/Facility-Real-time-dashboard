using JanitorialManagementDashboard.ApiTests.Infrastructure;
using JanitorialManagementDashboard.Domain.Entities.Scheduling;
using JanitorialManagementDashboard.Domain.Enums;
using JanitorialManagementDashboard.Infrastructure.Persistence.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace JanitorialManagementDashboard.ApiTests.Scheduling;

/// <summary>
/// Spec §11: the anchor create/retry path may recover from exactly one save failure — losing the UNIQUE index to another
/// writer that created the anchor first. Every other failure (a deadlock, a lock wait timeout, a foreign key) has already
/// rolled the command's transaction back, so treating it as "another writer won" would let the command carry on writing
/// outside the transaction its business rows, audit rows and receipt commit in.
/// </summary>
public class ScheduleAnchorRecoveryTests
{
    private static readonly DateOnly Week = new(2026, 11, 16);

    [Fact]
    public async Task Only_a_lost_unique_race_on_an_anchor_is_recoverable()
    {
        using var factory = new JanitorialApiFactory();
        await factory.WithDbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.EmployeeId == "1000001");
            var now = new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc);
            db.ScheduleAnchors.Add(Anchor(person.Id, now));
            await db.SaveChangesAsync();

            // The race the retry exists for: another writer stored this person's slot anchor first.
            var losing = Anchor(person.Id, now);
            db.ScheduleAnchors.Add(losing);
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.True(ScheduleStore.IsDuplicateKeyViolation(duplicate),
                $"a UNIQUE violation on the anchor must be recoverable, got: {duplicate.GetBaseException().Message}");
            db.Entry(losing).State = EntityState.Detached;

            // A constraint the retry must not read as "someone else won": the person does not exist, which SQLite reports as
            // a foreign key failure (19/787), not a UNIQUE one (19/2067).
            var orphan = Anchor(-1, now);
            db.ScheduleAnchors.Add(orphan);
            var foreign = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.False(ScheduleStore.IsDuplicateKeyViolation(foreign),
                $"a foreign key failure must be rethrown, got: {foreign.GetBaseException().Message}");
            db.Entry(orphan).State = EntityState.Detached;

            // And a save failure that is not a constraint at all — what a deadlock or a lock wait timeout surfaces as — is
            // rethrown too, so the engine rolls back and reports uncertainty instead of continuing.
            Assert.False(ScheduleStore.IsDuplicateKeyViolation(
                new DbUpdateException("deadlock", new InvalidOperationException("MySQL 1213 (40001): Deadlock found"))));
        });
    }

    private static ScheduleAnchor Anchor(int personId, DateTime now) => new()
    {
        PersonId = personId,
        ShiftDate = Week,
        Shift = Shift.Day,
        Revision = 1,
        UpdatedAt = now,
    };
}
