namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>Runs only when FACILITY_MYSQL_TEST holds a connection string to a throwaway MySQL database.</summary>
public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FACILITY_MYSQL_TEST")))
        {
            Skip = "Set FACILITY_MYSQL_TEST to a MySQL connection string to run";
        }
    }
}
