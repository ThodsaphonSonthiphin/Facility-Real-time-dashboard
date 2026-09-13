namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>A clock tests can move forward. Starts at the real "now" so JWTs issued without advancing still validate.</summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
