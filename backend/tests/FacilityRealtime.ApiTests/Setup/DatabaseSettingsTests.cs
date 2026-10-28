using FacilityRealtime.Api;
using Microsoft.Extensions.Configuration;

namespace FacilityRealtime.ApiTests.Setup;

/// <summary>facility-0093 decision 1: only Development has a default database; elsewhere a missing one stops the server.</summary>
public class DatabaseSettingsTests
{
    private static IConfiguration Config(params string[] jsonFiles)
    {
        var builder = new ConfigurationBuilder();
        foreach (var file in jsonFiles)
        {
            builder.AddJsonFile(Path.Combine(AppContext.BaseDirectory, file), optional: false);
        }

        return builder.Build();
    }

    private static IConfiguration ConfigWithConnection(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
            .Build();

    [Fact]
    public void Returns_the_connection_string_that_is_set()
    {
        Assert.Equal("Server=db;Database=facility_pilot", DatabaseSettings.ConnectionString(ConfigWithConnection("Server=db;Database=facility_pilot")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_connection_string_stops_with_the_setting_name(string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.ConnectionString(ConfigWithConnection(value)));

        Assert.Contains("ConnectionStrings__DefaultConnection", error.Message);
    }

    [Fact]
    public void Production_settings_alone_have_no_database()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseSettings.ConnectionString(Config("appsettings.json")));
    }

    [Fact]
    public void Development_settings_keep_the_developer_database()
    {
        var value = DatabaseSettings.ConnectionString(Config("appsettings.json", "appsettings.Development.json"));

        Assert.Contains("Database=facility_dashboard", value);
    }

    // Made up at run time so no password-like text sits in the repo (secret scanners flag it).
    private static readonly string MadeUpLogin = Guid.NewGuid().ToString("N");

    [Theory]
    [InlineData("Server=db.example;Port=3307;Database=facility_pilot;Uid=app;Pwd=", "db.example:3307 / facility_pilot")]
    [InlineData("Host=10.0.0.5;Initial Catalog=facility_pilot;User Id=app;Password=", "10.0.0.5:3306 / facility_pilot")]
    [InlineData("Uid=app;Pwd=", "(ไม่ได้ระบุ):3306 / (ไม่ได้ระบุ)")]
    public void Describes_server_port_and_database_without_the_login(string connectionWithoutPassword, string expected)
    {
        var described = DatabaseSettings.Describe(connectionWithoutPassword + MadeUpLogin);

        Assert.Equal(expected, described);
        Assert.DoesNotContain(MadeUpLogin, described);
        Assert.DoesNotContain("app", described.Replace("(ไม่ได้ระบุ)", ""));
    }
}
