using FacilityRealtime.Infrastructure.Auth;
using Xunit;

namespace FacilityRealtime.UnitTests;

public class Pbkdf2PasswordHasherTests
{
    // Low iteration count keeps the suite fast; production uses DefaultIterations
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Verify_accepts_the_password_that_was_hashed()
    {
        var stored = _hasher.Hash("password123");

        Assert.True(_hasher.Verify("password123", stored));
    }

    [Fact]
    public void Verify_rejects_a_different_password()
    {
        var stored = _hasher.Hash("password123");

        Assert.False(_hasher.Verify("password124", stored));
    }

    [Fact]
    public void Same_password_hashes_differently_because_of_the_salt()
    {
        Assert.NotEqual(_hasher.Hash("password123"), _hasher.Hash("password123"));
    }

    [Fact]
    public void Stored_format_names_algorithm_and_iterations()
    {
        var parts = _hasher.Hash("password123").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("1000", parts[1]);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
    }

    [Fact]
    public void Verify_reads_iterations_from_the_stored_value()
    {
        var storedWithMoreIterations = new Pbkdf2PasswordHasher(iterations: 2_000).Hash("password123");

        Assert.True(_hasher.Verify("password123", storedWithMoreIterations));
    }

    [Theory]
    [InlineData("ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f")] // master's unsalted SHA-256 of "password123"
    [InlineData("")]
    [InlineData("pbkdf2-sha256$abc$AAAA$AAAA")]
    [InlineData("pbkdf2-sha256$1000$not-base64$AAAA")]
    [InlineData("bcrypt$10$salt$hash")]
    public void Verify_rejects_values_that_are_not_pbkdf2_hashes(string stored)
    {
        Assert.False(_hasher.Verify("password123", stored));
    }

    [Fact]
    public void Default_iterations_follow_owasp_guidance_for_pbkdf2_sha256()
    {
        Assert.Equal(600_000, Pbkdf2PasswordHasher.DefaultIterations);
    }

    [Fact]
    public void Verify_rejects_a_stored_hash_with_an_empty_hash_segment()
    {
        var storedWithEmptyHash = "pbkdf2-sha256$1000$" + Convert.ToBase64String(new byte[16]) + "$";

        Assert.False(_hasher.Verify("password123", storedWithEmptyHash));
    }
}
