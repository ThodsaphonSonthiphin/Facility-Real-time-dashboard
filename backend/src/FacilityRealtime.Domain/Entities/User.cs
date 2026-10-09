using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>Every account role in one table (database.html, users).</summary>
public class User
{
    public int Id { get; set; }
    public UserRole Role { get; set; } = UserRole.Cleaner;

    /// <summary>Cleaner and Supervisor Accounts log in with it (facility-0054). Null for the Admin.</summary>
    public string? EmployeeId { get; set; }

    /// <summary>Admin Account only.</summary>
    public string? Username { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>PBKDF2 of the normalised phone number (Cleaner, Supervisor) or of the password (Admin). No phone number is stored.</summary>
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>The Cleaner's Area (facility-0040).</summary>
    public int? AreaId { get; set; }

    /// <summary>The Supervisor's building (facility-0048).</summary>
    public int? BuildingId { get; set; }

    /// <summary>The regular shift of a Cleaner or Supervisor.</summary>
    public Shift? Shift { get; set; }

    /// <summary>False for a Deactivated Account: cannot log in, refresh is refused (facility-0012, 0020).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    /// <summary>Computed by the database; unique, so an Area has one active Cleaner per shift.</summary>
    public int? CleanerSlot { get; private set; }

    /// <summary>Computed by the database; unique, so a building has one active Supervisor per shift.</summary>
    public int? SupervisorSlot { get; private set; }

    public Area? Area { get; set; }
    public Building? Building { get; set; }

    /// <summary>What the person types to log in, and what the API reports as "username".</summary>
    public string LoginName => Role == UserRole.Admin ? Username ?? string.Empty : EmployeeId ?? string.Empty;
}
