using System;

namespace FacilityRealtime.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "cleaner"; // cleaner, admin

    /// <summary>False for a Deactivated Account: cannot log in, refresh is refused (ADR facility-0012, facility-0020).</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
