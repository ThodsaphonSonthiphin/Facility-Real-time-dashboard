using FacilityRealtime.Application.Auth;

namespace FacilityRealtime.UnitTests;

public class LoginThrottleTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 1, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string Key = "user:1";
    private const string Place = "203.0.113.7";
    private readonly ManualClock _clock = new();
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests()
    {
        _throttle = new LoginThrottle(_clock, new LoginThrottleSettings { MaxFailures = 5, MaxFailuresPerPlace = 20, LockMinutes = 15 });
    }

    private int Attempt(int times, string key = Key, string place = Place)
    {
        var allowed = 0;
        for (var i = 0; i < times; i++)
        {
            if (_throttle.TryBeginAttempt(key, place) == LoginGate.Allowed)
            {
                allowed++;
                _throttle.Failed(place); // the secret was wrong
            }
        }

        return allowed;
    }

    [Fact]
    public void Five_attempts_are_allowed_and_the_sixth_is_refused()
    {
        Assert.Equal(5, Attempt(5));
        Assert.NotEqual(LoginGate.Allowed, _throttle.TryBeginAttempt(Key, Place));
    }

    [Fact]
    public void The_lock_lasts_fifteen_minutes_from_the_refused_attempt()
    {
        Attempt(5);
        Assert.NotEqual(LoginGate.Allowed, _throttle.TryBeginAttempt(Key, Place));

        _clock.Now = _clock.Now.AddMinutes(14).AddSeconds(59);
        var stillLocked = _throttle.TryBeginAttempt(Key, Place);
        _clock.Now = _clock.Now.AddSeconds(1);
        var unlocked = _throttle.TryBeginAttempt(Key, Place);

        Assert.Equal(LoginGate.NameLocked, stillLocked);
        Assert.Equal(LoginGate.Allowed, unlocked);
    }

    [Fact]
    public void Success_clears_the_count()
    {
        Attempt(4);
        _throttle.Succeeded(Key, Place);

        Assert.Equal(5, Attempt(5));
    }

    [Fact]
    public void Attempts_spread_over_more_than_the_window_start_over()
    {
        Attempt(4);
        _clock.Now = _clock.Now.AddMinutes(15);

        Assert.Equal(5, Attempt(5));
    }

    [Fact]
    public void Keys_are_counted_separately_ignoring_case()
    {
        Attempt(5);

        Assert.Equal(LoginGate.NameLocked, _throttle.TryBeginAttempt("USER:1", Place));
        Assert.Equal(LoginGate.Allowed, _throttle.TryBeginAttempt("user:2", Place));
    }

    [Fact]
    public async Task Concurrent_attempts_never_pass_the_limit()
    {
        var allowed = 0;

        await Task.Run(() => Parallel.For(0, 200, _ =>
        {
            if (_throttle.TryBeginAttempt(Key, Place) == LoginGate.Allowed)
            {
                Interlocked.Increment(ref allowed);
            }
        }));

        Assert.Equal(5, allowed);
    }

    [Fact]
    public void Expired_keys_are_swept_once_many_are_tracked()
    {
        for (var i = 0; i < LoginThrottle.SweepThreshold; i++)
        {
            _throttle.TryBeginAttempt($"k{i}", $"a{i}"); // one place each, so no place reaches its cap
        }

        Assert.Equal(2 * LoginThrottle.SweepThreshold, _throttle.TrackedKeys); // each pair and its place

        _clock.Now = _clock.Now.AddMinutes(15);
        _throttle.TryBeginAttempt("fresh", "fresh");

        Assert.Equal(2, _throttle.TrackedKeys);
    }

    [Fact]
    public void The_same_login_name_from_another_place_is_not_locked()
    {
        Attempt(5);
        Assert.Equal(LoginGate.NameLocked, _throttle.TryBeginAttempt(Key, Place));

        Assert.Equal(LoginGate.Allowed, _throttle.TryBeginAttempt(Key, "198.51.100.20"));
    }

    [Fact]
    public void Twenty_attempts_from_one_place_across_names_lock_that_place_for_fifteen_minutes()
    {
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(5, Attempt(5, $"employee:E{i}"));
        }

        var capReached = _throttle.TryBeginAttempt("employee:E9", Place);
        var refusedAfter = _throttle.TryBeginAttempt("employee:E10", Place);
        var otherPlace = _throttle.TryBeginAttempt("employee:E9", "198.51.100.20");
        _clock.Now = _clock.Now.AddMinutes(15);
        var afterTheLock = _throttle.TryBeginAttempt("employee:E9", Place);

        Assert.Equal(LoginGate.PlaceLockedNow, capReached);
        Assert.Equal(LoginGate.PlaceLocked, refusedAfter);
        Assert.Equal(LoginGate.Allowed, otherPlace);
        Assert.Equal(LoginGate.Allowed, afterTheLock);
    }

    /// <summary>
    /// facility-0104 decision 3: only failures count toward the place's cap (people behind one CGNAT address log in
    /// fine), and a success never wipes it (an attacker would mix in logins to their own account).
    /// </summary>
    [Fact]
    public void A_success_neither_counts_toward_nor_clears_the_place_cap()
    {
        for (var i = 0; i < 4; i++)
        {
            Attempt(4, $"employee:E{i}");
        }

        Attempt(3, "employee:E4"); // 19 failures from this place
        Assert.Equal(LoginGate.Allowed, _throttle.TryBeginAttempt("employee:OWN", Place));
        _throttle.Succeeded("employee:OWN", Place);

        var twentiethFailure = _throttle.TryBeginAttempt("employee:E5", Place);
        _throttle.Failed(Place);
        var overTheCap = _throttle.TryBeginAttempt("employee:E6", Place);

        Assert.Equal(LoginGate.Allowed, twentiethFailure);
        Assert.Equal(LoginGate.PlaceLockedNow, overTheCap);
    }

    /// <summary>
    /// facility-0104 decision 3: at shift start many people behind one place log in at once. Logins whose secret is
    /// still being checked are not failures, so they never push the address over its cap.
    /// </summary>
    [Fact]
    public void Logins_still_being_checked_do_not_count_toward_the_place_cap()
    {
        for (var i = 0; i < 4; i++)
        {
            Attempt(4, $"employee:E{i}");
        }

        Attempt(3, "employee:E4"); // 19 failures from this place

        var inFlight = Enumerable.Range(0, 10).Select(i => _throttle.TryBeginAttempt($"employee:OWN{i}", Place)).ToList();

        Assert.All(inFlight, gate => Assert.Equal(LoginGate.Allowed, gate));
    }
}
