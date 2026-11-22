using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JanitorialManagementDashboard.Application.Scheduling;

namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11: the server-issued identity a schedule command is submitted under. A token is immutable, signed with the
/// private <c>Scheduling:CommandSigningKey</c>, bound to one actor and one purpose, and it states when it was issued and
/// when it expires, so a client can never re-issue the same identity with a fresh timestamp: the only way to get a later
/// issuance is a new identity from the server.
/// <para>
/// The key lives in configuration, never in a stored record and never as a default in code. It must stay available for the
/// full 24 hours of any outstanding token: rotating it sooner retires those identities (they simply stop being recognised,
/// which is safe but makes their commands unavailable), it never resurrects a committed result.
/// </para>
/// </summary>
public sealed class ScheduleCommandTokenService
{
    /// <summary>Purpose-bound: a value signed for anything else is not a command token, even with the same key.</summary>
    private const string Purpose = "schedule-command";

    /// <summary>HMAC-SHA256 needs a key at least as long as its own digest; a shorter configured key is a setup error.</summary>
    private const int MinimumKeyBytes = SchedulingSettings.MinimumCommandSigningKeyBytes;

    /// <summary>How long an identity may start a new command after it was issued (spec §11).</summary>
    private static readonly TimeSpan StartWindow = TimeSpan.FromHours(24);

    private readonly byte[] _key;

    public ScheduleCommandTokenService(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < MinimumKeyBytes)
        {
            throw new ArgumentException(
                $"A scheduling command signing key of at least {MinimumKeyBytes} bytes is required.", nameof(signingKey));
        }

        _key = Encoding.UTF8.GetBytes(signingKey);
    }

    /// <summary>How long a freshly issued identity may start a command: the window a refused token has passed.</summary>
    public TimeSpan Lifetime => StartWindow;

    /// <summary>Issues a new identity for <paramref name="actorId"/> at <paramref name="now"/>; the id is the server's, not the caller's.</summary>
    public IssuedCommandToken Issue(int actorId, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        var issuedAt = now.ToUnixTimeSeconds();
        var expiresAt = issuedAt + (long)StartWindow.TotalSeconds;
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new SignedPayload(Purpose, actorId, id.ToString("D"), issuedAt, expiresAt));
        var encoded = Base64Url(payload);
        return new IssuedCommandToken(id, $"{encoded}.{Base64Url(Sign(encoded))}",
            DateTimeOffset.FromUnixTimeSeconds(expiresAt));
    }

    /// <summary>
    /// Spec §11: may this token start a command for this actor now? A value the server did not sign, one bound to another
    /// actor, one whose purpose differs and one whose 24-hour window has passed are all refused, and a refusal carries no
    /// identity out, so nothing can be looked up with it.
    /// </summary>
    public VerifiedCommandToken Verify(string token, int actorId, DateTimeOffset now)
    {
        var claims = ReadClaims(token);
        return claims is not null && claims.ActorId == actorId && now < claims.ExpiresAt
            ? new VerifiedCommandToken(true, claims.Id)
            : new VerifiedCommandToken(false, Guid.Empty);
    }

    /// <summary>
    /// The claims of a token this server signed for that purpose, ignoring the start window. The engine uses it only to
    /// tell "issued 25 hours ago" (a retained result may still answer) from "not ours at all" (nothing may be read).
    /// </summary>
    internal CommandTokenClaims? ReadClaims(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var separator = token.IndexOf('.');
        if (separator <= 0 || separator == token.Length - 1 || token.IndexOf('.', separator + 1) >= 0)
        {
            return null;
        }

        var encoded = token[..separator];
        var signature = FromBase64Url(token[(separator + 1)..]);
        if (signature is null || !CryptographicOperations.FixedTimeEquals(Sign(encoded), signature))
        {
            return null;
        }

        var payload = FromBase64Url(encoded);
        if (payload is null)
        {
            return null;
        }

        SignedPayload? signed;
        try
        {
            signed = JsonSerializer.Deserialize<SignedPayload>(payload);
        }
        catch (JsonException)
        {
            return null;
        }

        if (signed is null || !string.Equals(signed.Purpose, Purpose, StringComparison.Ordinal)
            || !Guid.TryParse(signed.Id, out var id))
        {
            return null;
        }

        return new CommandTokenClaims(id, signed.ActorId, DateTimeOffset.FromUnixTimeSeconds(signed.IssuedAt),
            DateTimeOffset.FromUnixTimeSeconds(signed.ExpiresAt));
    }

    private byte[] Sign(string encoded) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(encoded));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);
        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>What the server signs: purpose, actor, identity and the issue/expiry instants as UTC whole seconds.</summary>
    private sealed record SignedPayload(string Purpose, int ActorId, string Id, long IssuedAt, long ExpiresAt);
}

/// <summary>The verified claim set of one command identity.</summary>
internal sealed record CommandTokenClaims(Guid Id, int ActorId, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
