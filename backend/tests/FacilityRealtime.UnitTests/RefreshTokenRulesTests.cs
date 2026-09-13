using FacilityRealtime.Application.Auth;
using FacilityRealtime.Domain.Entities;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class RefreshTokenRulesTests
{
    private static readonly DateTime Issued = new(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);

    private static RefreshToken Token(DateTime? rotatedAt = null, DateTime? revokedAt = null) => new()
    {
        CreatedAt = Issued,
        ExpiresAt = Issued + RefreshTokenRules.Lifetime,
        RotatedAt = rotatedAt,
        RevokedAt = revokedAt,
    };

    [Fact]
    public void Lifetime_is_30_days_and_reuse_grace_is_30_seconds()
    {
        Assert.Equal(TimeSpan.FromDays(30), RefreshTokenRules.Lifetime);
        Assert.Equal(TimeSpan.FromSeconds(30), RefreshTokenRules.ReuseGrace);
    }

    [Fact]
    public void Unused_unexpired_token_rotates()
    {
        Assert.Equal(RefreshDecision.Rotate, RefreshTokenRules.Decide(Token(), Issued.AddDays(29)));
    }

    [Fact]
    public void Token_is_rejected_from_the_moment_it_expires()
    {
        var token = Token();

        Assert.Equal(RefreshDecision.Rotate, RefreshTokenRules.Decide(token, token.ExpiresAt.AddSeconds(-1)));
        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(token, token.ExpiresAt));
    }

    [Fact]
    public void Revoked_token_is_rejected()
    {
        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(Token(revokedAt: Issued.AddMinutes(1)), Issued.AddMinutes(2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(30)]
    public void Rotated_token_replayed_within_30_seconds_counts_as_another_tab(int secondsLater)
    {
        var rotatedAt = Issued.AddMinutes(10);

        Assert.Equal(RefreshDecision.RotateWithinGrace, RefreshTokenRules.Decide(Token(rotatedAt), rotatedAt.AddSeconds(secondsLater)));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(3600)]
    public void Rotated_token_replayed_after_30_seconds_is_reuse(int secondsLater)
    {
        var rotatedAt = Issued.AddMinutes(10);

        Assert.Equal(RefreshDecision.ReuseDetected, RefreshTokenRules.Decide(Token(rotatedAt), rotatedAt.AddSeconds(secondsLater)));
    }

    [Fact]
    public void Revocation_wins_over_the_grace_window()
    {
        var rotatedAt = Issued.AddMinutes(10);

        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(Token(rotatedAt, revokedAt: rotatedAt), rotatedAt.AddSeconds(5)));
    }

    [Fact]
    public void Expired_rotated_token_is_rejected_without_reuse_detection()
    {
        var token = Token(rotatedAt: Issued.AddMinutes(10));

        Assert.Equal(RefreshDecision.Reject, RefreshTokenRules.Decide(token, token.ExpiresAt.AddDays(1)));
    }

    [Fact]
    public void New_tokens_are_43_url_safe_characters_and_never_repeat()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenRules.NewToken()).ToList();

        Assert.All(tokens, t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
        Assert.Equal(100, tokens.Distinct().Count());
    }

    [Fact]
    public void Hash_is_deterministic_64_char_lowercase_hex_and_not_the_token()
    {
        var token = RefreshTokenRules.NewToken();
        var hash = RefreshTokenRules.Hash(token);

        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, RefreshTokenRules.Hash(token));
        Assert.NotEqual(token, hash);
    }
}
