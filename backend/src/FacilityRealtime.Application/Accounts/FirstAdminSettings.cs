namespace FacilityRealtime.Application.Accounts;

/// <summary>
/// facility-0092 decision 1: the server's environment variables FirstAdmin__Username, FirstAdmin__Password and
/// FirstAdmin__DisplayName. Never in appsettings.json or git; the password is removed once the Admin exists.
/// </summary>
public sealed class FirstAdminSettings
{
    public const string SectionName = "FirstAdmin";

    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
}
