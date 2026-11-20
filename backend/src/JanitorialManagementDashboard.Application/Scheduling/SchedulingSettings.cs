namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §11: the "Scheduling" configuration section. Scheduling is always enabled as a core feature; the first date the
/// stored schedule is history rather than a compatibility projection is configuration rather than a constant, and the
/// private key the command token is signed with never has a default in product code.
/// <para>
/// Everything here is read lazily: constructing the settings cannot fail, and the key is only demanded by the getter a
/// caller actually needs it from. A server without a command signing key therefore still starts and serves read endpoints,
/// failing only when a scheduling write command is issued or verified.
/// </para>
/// </summary>
public sealed class SchedulingSettings
{
    public const string SectionName = "Scheduling";

    /// <summary>The setting the private command-signing key is read from; the value itself is never logged or stored.</summary>
    public const string CommandSigningKeyName = "CommandSigningKey";

    private readonly string? _commandSigningKey;

    /// <summary>Settings for a server that has to supply a key before any command can be started.</summary>
    public SchedulingSettings(string? commandSigningKey = null) =>
        _commandSigningKey = string.IsNullOrWhiteSpace(commandSigningKey) ? null : commandSigningKey.Trim();

    /// <summary>
    /// The first date the stored schedule is history. Before it a projection reports "unavailable" rather than inventing
    /// a past (spec §10, A27), so this is an owner-supplied date, not a default in code.
    /// </summary>
    public DateOnly? HistoryStartsAt { get; set; }

    /// <summary>True when a signing key is configured, which is what the command token needs.</summary>
    public bool HasCommandSigningKey => _commandSigningKey is not null;

    /// <summary>
    /// The private key itself. It is never a writable property, so it cannot be bound from an untrusted source or printed
    /// with the rest of the settings, and the failure names the missing setting without being able to echo a value.
    /// </summary>
    public string CommandSigningKey =>
        _commandSigningKey
        ?? throw new InvalidOperationException(
            $"{SectionName}:{CommandSigningKeyName} is not configured; a scheduling command cannot be issued or verified without it.");
}
