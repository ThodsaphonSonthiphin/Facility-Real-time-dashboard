using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FacilityRealtime.Infrastructure.Persistence;

public static partial class ModelConventions
{
    /// <summary>database.html names every column in snake_case; the computed-column and CHECK SQL below rely on it.</summary>
    public static void UseSnakeCaseColumns(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    public static string ToSnakeCase(string name) => UpperAfterLowerOrDigit().Replace(name, "_$1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex UpperAfterLowerOrDigit();
}

/// <summary>Stores an enum as its UPPER_SNAKE name ('DAY', 'ON_TIME', 'DAY_AND_NIGHT'), the values database.html lists.</summary>
public sealed class UpperSnakeEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ModelConventions.ToSnakeCase(value.ToString()).ToUpperInvariant(),
    stored => Enum.Parse<TEnum>(stored.Replace("_", string.Empty), true))
    where TEnum : struct, Enum
{
}
