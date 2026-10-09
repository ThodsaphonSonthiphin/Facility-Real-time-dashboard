namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>A clock tests can move forward. Starts at the real "now" so JWTs issued without advancing still validate.</summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);

    /// <summary>
    /// Jumps to an exact moment. Log in before calling it: JwtBearer validates tokens against the real
    /// clock, while the API's business logic reads this one.
    /// </summary>
    public void SetUtcNow(DateTimeOffset value) => _now = value.ToUniversalTime();
}
