using FacilityRealtime.ApiTests.Infrastructure;
using FacilityRealtime.Domain.Entities;
using FacilityRealtime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FacilityRealtime.ApiTests.Persistence;

public class RefreshTokenModelTests
{
    private static RefreshToken Token(int userId, Guid sessionId, string hash) => new()
    {
        UserId = userId,
        SessionId = sessionId,
        TokenHash = hash,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(30),
    };

    [Fact]
    public async Task Seeded_users_are_active()
    {
        using var factory = new FacilityApiFactory();
        var activeCount = 0;

        await factory.WithDbAsync(async db => activeCount = await db.Users.CountAsync(u => u.IsActive));

        Assert.Equal(2, activeCount);
    }

    [Fact]
    public async Task A_deactivated_user_stays_deactivated_after_saving()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Username == "somchai");
            user.IsActive = false;
            await db.SaveChangesAsync();
        });

        var isActive = true;
        await factory.WithDbAsync(async db => isActive = (await db.Users.AsNoTracking().SingleAsync(u => u.Username == "somchai")).IsActive);
        Assert.False(isActive);
    }

    [Fact]
    public async Task Token_hash_must_be_unique()
    {
        using var factory = new FacilityApiFactory();

        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.Add(Token(1, Guid.NewGuid(), new string('a', 64)));
            db.RefreshTokens.Add(Token(1, Guid.NewGuid(), new string('a', 64)));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Revoking_a_session_leaves_other_sessions_alone()
    {
        using var factory = new FacilityApiFactory();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.AddRange(Token(1, sessionA, new string('a', 64)), Token(1, sessionA, new string('b', 64)), Token(1, sessionB, new string('c', 64)));
            await db.SaveChangesAsync();

            var revoked = await db.RevokeSessionAsync(sessionA, now);

            Assert.Equal(2, revoked);
            Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.RevokedAt == null));
        });
    }

    [Fact]
    public async Task Revoking_a_user_revokes_every_session_of_that_user_only()
    {
        using var factory = new FacilityApiFactory();
        var now = DateTime.UtcNow;

        await factory.WithDbAsync(async db =>
        {
            db.RefreshTokens.AddRange(Token(1, Guid.NewGuid(), new string('a', 64)), Token(1, Guid.NewGuid(), new string('b', 64)), Token(2, Guid.NewGuid(), new string('c', 64)));
            await db.SaveChangesAsync();

            var revoked = await db.RevokeUserSessionsAsync(1, now);

            Assert.Equal(2, revoked);
            Assert.Equal(1, await db.RefreshTokens.CountAsync(t => t.UserId == 2 && t.RevokedAt == null));
        });
    }
}
