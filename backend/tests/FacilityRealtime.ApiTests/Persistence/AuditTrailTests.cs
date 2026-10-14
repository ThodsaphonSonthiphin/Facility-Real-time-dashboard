using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

public class AuditTrailTests
{
    /// <summary>facility-0057: no phone or password in the log. A User entity carries a phone hash, so only snapshots go in.</summary>
    [Fact]
    public async Task Entities_are_refused_as_audit_snapshots()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var cleaner = await db.Users.SingleAsync(u => u.EmployeeId == "E1001");

            Assert.Throws<ArgumentException>(() =>
                AuditTrail.Add(db, cleaner.Id, DateTime.UtcNow, "TEST", "users", cleaner.Id, "test", before: cleaner, after: null));
            Assert.Throws<ArgumentException>(() =>
                AuditTrail.Add(db, cleaner.Id, DateTime.UtcNow, "TEST", "users", cleaner.Id, "test", before: null, after: cleaner));
            Assert.Empty(db.AuditLog.Local);
        });
    }
}
