using System.Collections.Concurrent;

namespace FacilityRealtime.Application.Auth;

/// <summary>The "LoginThrottle" config section.</summary>
public sealed class LoginThrottleSettings
{
    public const string SectionName = "LoginThrottle";

    public int MaxFailures { get; set; } = 5;
    public int LockMinutes { get; set; } = 15;
}

/// <summary>
/// facility-0054: MaxFailures failed logins within LockMinutes lock that login name for LockMinutes.
/// In memory, because there is one server (database.html); a restart forgets every count.
/// </summary>
public sealed class LoginThrottle(TimeProvider clock, LoginThrottleSettings settings)
{
    private sealed record Entry(int Failures, DateTime FirstFailureUtc, DateTime? LockedUntilUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private TimeSpan Window => TimeSpan.FromMinutes(settings.LockMinutes);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public bool IsLocked(string loginKey) =>
        _entries.TryGetValue(loginKey, out var entry) && entry.LockedUntilUtc > Now;

    public void RecordFailure(string loginKey) =>
        _entries.AddOrUpdate(loginKey, _ => Count(null), (_, entry) => Count(entry));

    public void Reset(string loginKey) => _entries.TryRemove(loginKey, out _);

    private Entry Count(Entry? entry)
    {
        var now = Now;
        var startOver = entry is null
            || entry.LockedUntilUtc <= now
            || (entry.LockedUntilUtc is null && now - entry.FirstFailureUtc >= Window);

        var failures = startOver ? 1 : entry!.Failures + 1;
        var firstFailure = startOver ? now : entry!.FirstFailureUtc;
        return new Entry(failures, firstFailure, failures >= settings.MaxFailures ? now + Window : null);
    }
}
