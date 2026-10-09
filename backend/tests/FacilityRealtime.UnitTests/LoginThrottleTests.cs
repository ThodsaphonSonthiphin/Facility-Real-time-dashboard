using FacilityRealtime.Application.Auth;

namespace FacilityRealtime.UnitTests;

public class LoginThrottleTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 1, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string Key = "employee:E1001";
    private readonly ManualClock _clock = new();
    private readonly LoginThrottle _throttle;

    public LoginThrottleTests()
    {
        _throttle = new LoginThrottle(_clock, new LoginThrottleSettings { MaxFailures = 5, LockMinutes = 15 });
    }

    private void Fail(int times, string key = Key)
    {
        for (var i = 0; i < times; i++)
        {
            _throttle.RecordFailure(key);
        }
    }

    [Fact]
    public void Four_failures_do_not_lock()
    {
        Fail(4);

        Assert.False(_throttle.IsLocked(Key));
    }

    [Fact]
    public void Fifth_failure_locks_for_fifteen_minutes()
    {
        Fail(5);
        var lockedAtFirst = _throttle.IsLocked(Key);
        _clock.Now = _clock.Now.AddMinutes(14).AddSeconds(59);
        var lockedJustBefore = _throttle.IsLocked(Key);
        _clock.Now = _clock.Now.AddSeconds(1);

        Assert.True(lockedAtFirst);
        Assert.True(lockedJustBefore);
        Assert.False(_throttle.IsLocked(Key));
    }

    [Fact]
    public void Success_resets_the_count()
    {
        Fail(4);
        _throttle.Reset(Key);
        Fail(4);

        Assert.False(_throttle.IsLocked(Key));
    }

    [Fact]
    public void Failures_spread_over_more_than_the_window_start_over()
    {
        Fail(4);
        _clock.Now = _clock.Now.AddMinutes(15);
        Fail(1);

        Assert.False(_throttle.IsLocked(Key));
    }

    [Fact]
    public void After_a_lock_ends_counting_starts_over()
    {
        Fail(5);
        _clock.Now = _clock.Now.AddMinutes(15);
        Fail(1);

        Assert.False(_throttle.IsLocked(Key));
    }

    [Fact]
    public void Login_names_are_counted_separately_ignoring_case()
    {
        Fail(5);

        Assert.True(_throttle.IsLocked("employee:e1001"));
        Assert.False(_throttle.IsLocked("employee:E1002"));
    }
}
