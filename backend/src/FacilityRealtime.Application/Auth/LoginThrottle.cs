namespace FacilityRealtime.Application.Auth;

/// <summary>The "LoginThrottle" config section.</summary>
public sealed class LoginThrottleSettings
{
    public const string SectionName = "LoginThrottle";

    public int MaxFailures { get; set; } = 5;
    public int LockMinutes { get; set; } = 15;
}

/// <summary>
/// facility-0054: at most MaxFailures login attempts per login key within LockMinutes; the next one is refused
/// and locks the key for LockMinutes. Attempts are counted at the gate, before the secret is checked, so
/// concurrent requests cannot slip past the limit; a successful login clears the count.
/// In memory, because there is one server (database.html); a restart forgets every count.
/// </summary>
public sealed class LoginThrottle(TimeProvider clock, LoginThrottleSettings settings)
{
    /// <summary>Above this many tracked keys, expired entries are swept before counting.</summary>
    internal const int SweepThreshold = 10_000;

    private sealed class Entry
    {
        public int Attempts;
        public DateTime FirstAttemptUtc;
        public DateTime? LockedUntilUtc;
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private TimeSpan Window => TimeSpan.FromMinutes(settings.LockMinutes);

    /// <summary>How many login keys are held in memory (tests only).</summary>
    internal int TrackedKeys
    {
        get { lock (_gate) { return _entries.Count; } }
    }

    /// <summary>Counts one attempt. False means locked: answer 429 without checking the secret.</summary>
    public bool TryBeginAttempt(string loginKey)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            if (_entries.Count >= SweepThreshold)
            {
                Sweep(now);
            }

            if (!_entries.TryGetValue(loginKey, out var entry) || IsExpired(entry, now))
            {
                entry = new Entry { FirstAttemptUtc = now };
                _entries[loginKey] = entry;
            }

            if (entry.LockedUntilUtc > now)
            {
                return false;
            }

            if (entry.Attempts >= settings.MaxFailures)
            {
                entry.LockedUntilUtc = now + Window;
                return false;
            }

            entry.Attempts++;
            return true;
        }
    }

    public void Reset(string loginKey)
    {
        lock (_gate)
        {
            _entries.Remove(loginKey);
        }
    }

    private bool IsExpired(Entry entry, DateTime now) =>
        entry.LockedUntilUtc is { } lockedUntil ? lockedUntil <= now : now - entry.FirstAttemptUtc >= Window;

    private void Sweep(DateTime now)
    {
        foreach (var key in _entries.Where(pair => IsExpired(pair.Value, now)).Select(pair => pair.Key).ToList())
        {
            _entries.Remove(key);
        }
    }
}
