using System.Data.Common;

namespace FacilityRealtime.Api;

/// <summary>
/// facility-0093 decision 1: the one place that reads the database setting, for serving and for reset-admin. Only
/// appsettings.Development.json has a default, so a server that forgot the setting stops instead of changing
/// facility_dashboard.
/// </summary>
public static class DatabaseSettings
{
    public const string VariableName = "ConnectionStrings__DefaultConnection";

    private const string NotGiven = "(ไม่ได้ระบุ)";

    public static string ConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("DefaultConnection") is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"{VariableName} is not set. Set it on the server, or run with scripts/facility.sh and its env file (docs/handover.md). Only Development has a default database.");

    /// <summary>"server:port / database" for a person to check; never the user name or password.</summary>
    public static string Describe(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var server = First(builder, "Server", "Host", "Data Source") ?? NotGiven;
        var port = First(builder, "Port") ?? "3306";
        var database = First(builder, "Database", "Initial Catalog") ?? NotGiven;
        return $"{server}:{port} / {database}";
    }

    private static string? First(DbConnectionStringBuilder builder, params string[] keys) =>
        keys.Select(key => builder.TryGetValue(key, out var value) ? value as string : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
