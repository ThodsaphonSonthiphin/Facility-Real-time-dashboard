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
    private readonly ManualClock _clock = new();
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests()
    {
        _throttle = new LoginThrottle(_clock, new LoginThrottleSettings { MaxFailures = 5, LockMinutes = 15 });
    }

    private int Attempt(int times, string key = Key)
    {
        var allowed = 0;
        for (var i = 0; i < times; i++)
        {
            if (_throttle.TryBeginAttempt(key))
            {
                allowed++;
            }
        }

        return allowed;
    }

    [Fact]
    public void Five_attempts_are_allowed_and_the_sixth_is_refused()
    {
        Assert.Equal(5, Attempt(5));
        Assert.False(_throttle.TryBeginAttempt(Key));
    }

    [Fact]
    public void The_lock_lasts_fifteen_minutes_from_the_refused_attempt()
    {
        Attempt(5);
        Assert.False(_throttle.TryBeginAttempt(Key));

        _clock.Now = _clock.Now.AddMinutes(14).AddSeconds(59);
        var stillLocked = _throttle.TryBeginAttempt(Key);
        _clock.Now = _clock.Now.AddSeconds(1);
        var unlocked = _throttle.TryBeginAttempt(Key);

        Assert.False(stillLocked);
        Assert.True(unlocked);
    }

    [Fact]
    public void Success_clears_the_count()
    {
        Attempt(4);
        _throttle.Reset(Key);

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

        Assert.False(_throttle.TryBeginAttempt("USER:1"));
        Assert.True(_throttle.TryBeginAttempt("user:2"));
    }

    [Fact]
    public async Task Concurrent_attempts_never_pass_the_limit()
    {
        var allowed = 0;

        await Task.Run(() => Parallel.For(0, 200, _ =>
        {
            if (_throttle.TryBeginAttempt(Key))
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
            _throttle.TryBeginAttempt($"k{i}");
        }

        Assert.Equal(LoginThrottle.SweepThreshold, _throttle.TrackedKeys);

        _clock.Now = _clock.Now.AddMinutes(15);
        _throttle.TryBeginAttempt("fresh");

        Assert.Equal(1, _throttle.TrackedKeys);
    }
}
