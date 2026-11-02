namespace FacilityRealtime.Application.Auth;

/// <summary>The "LoginThrottle" config section.</summary>
public sealed class LoginThrottleSettings
{
    public const string SectionName = "LoginThrottle";

    public int MaxFailures { get; set; } = 5;
    public int LockMinutes { get; set; } = 15;

    /// <summary>facility-0104 decision 2: failures from one place across every login name.</summary>
    public int MaxFailuresPerPlace { get; set; } = 20;
}

/// <summary>What the gate decided for one login attempt.</summary>
public enum LoginGate
{
    Allowed,

    /// <summary>A Login Lock of this login name from this place (facility-0104 decision 1).</summary>
    NameLocked,

    /// <summary>A Login Lock of the whole place, for every login name (facility-0104 decision 2).</summary>
    PlaceLocked,

    /// <summary>This attempt found the place at its cap and locked it; the Server Log writes this one apart.</summary>
    PlaceLockedNow,
}

/// <summary>
/// facility-0054, 0104: at most MaxFailures login attempts per pair "login key + place" within LockMinutes; the
/// next one is refused and locks that pair for LockMinutes. The same name from another place is not locked, so nobody
/// can lock someone else out. One place may also have at most MaxFailuresPerPlace failures across every name, so it
/// cannot guess through many accounts. A pair's attempts are counted at the gate, before the secret is checked, so
/// concurrent requests cannot slip past its limit; a successful login clears its own pair's count. A place counts only
/// failures (Failed), so concurrent guesses may pass its cap by the few still being checked; each pair's limit still holds.
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
    private readonly object _sync = new();

    private TimeSpan Window => TimeSpan.FromMinutes(settings.LockMinutes);

    /// <summary>How many login keys are held in memory (tests only).</summary>
    internal int TrackedKeys
    {
        get { lock (_sync) { return _entries.Count; } }
    }

    /// <summary>Counts one attempt. Anything but Allowed means locked: answer 429 without checking the secret.</summary>
    public LoginGate TryBeginAttempt(string loginKey, string place)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        lock (_sync)
        {
            if (_entries.Count >= SweepThreshold)
            {
                Sweep(now);
            }

            // A refused attempt counts toward neither: it never reaches the secret
            var placeEntry = EntryAt(PlaceKey(place), now);
            if (placeEntry.LockedUntilUtc > now)
            {
                return LoginGate.PlaceLocked;
            }

            var pair = EntryAt(PairKey(loginKey, place), now);
            if (pair.LockedUntilUtc > now)
            {
                return LoginGate.NameLocked;
            }

            if (placeEntry.Attempts >= settings.MaxFailuresPerPlace)
            {
                placeEntry.LockedUntilUtc = now + Window;
                return LoginGate.PlaceLockedNow;
            }

            if (pair.Attempts >= settings.MaxFailures)
            {
                pair.LockedUntilUtc = now + Window;
                return LoginGate.NameLocked;
            }

            pair.Attempts++;
            return LoginGate.Allowed;
        }
    }

    /// <summary>
    /// A successful login clears its own pair only. The place's failures stay: an attacker must not wipe them by
    /// logging in to their own account (facility-0104 decision 3).
    /// </summary>
    public void Succeeded(string loginKey, string place)
    {
        lock (_sync)
        {
            _entries.Remove(PairKey(loginKey, place));
        }
    }

    /// <summary>
    /// An attempt whose secret was wrong, or whose name matches no account. Only these count toward the place's cap
    /// (facility-0104 decision 3): logins whose secret is still being checked are not failures, so people behind one
    /// place who log in together at shift start never lock it.
    /// </summary>
    public void Failed(string place)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        lock (_sync)
        {
            EntryAt(PlaceKey(place), now).Attempts++;
        }
    }

    private static string PairKey(string loginKey, string place) => $"login:{loginKey}|{place}";

    private static string PlaceKey(string place) => $"place:{place}";

    private Entry EntryAt(string key, DateTime now)
    {
        if (!_entries.TryGetValue(key, out var entry) || IsExpired(entry, now))
        {
            entry = new Entry { FirstAttemptUtc = now };
            _entries[key] = entry;
        }

        return entry;
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
