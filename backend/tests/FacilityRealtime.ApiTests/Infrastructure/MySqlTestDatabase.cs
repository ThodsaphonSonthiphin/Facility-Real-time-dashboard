using System.Data.Common;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>The MySQL tests drop and recreate their database, so they only ever touch a throwaway one named facility_*_test.</summary>
public static class MySqlTestDatabase
{
    public const string VariableName = "FACILITY_MYSQL_TEST";

    public static bool IsThrowawayName(string? name) =>
        !string.IsNullOrEmpty(name)
        && name.Length > "facility__test".Length - 1
        && name.StartsWith("facility_", StringComparison.Ordinal)
        && name.EndsWith("_test", StringComparison.Ordinal);

    public static string ConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(VariableName)
            ?? throw new InvalidOperationException($"{VariableName} is not set");
        var builder = new DbConnectionStringBuilder { ConnectionString = value };
        var name = builder.TryGetValue("Database", out var database) ? database as string
            : builder.TryGetValue("Initial Catalog", out var catalog) ? catalog as string
            : null;
        if (!IsThrowawayName(name))
        {
            throw new InvalidOperationException(
                $"{VariableName} must name a throwaway database (facility_*_test), not '{name}'; these tests drop it");
        }

        return value;
    }
}
