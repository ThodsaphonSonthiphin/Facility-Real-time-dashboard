namespace JanitorialManagementDashboard.Application.Scheduling;

/// <summary>
/// Spec §11: one server-issued command identity. The value is what a client holds, and it is signed rather than authored:
/// it cannot be minted, cannot be re-issued with a newer timestamp, and its claims name the purpose, the actor and the
/// moment it was issued. <see cref="Id"/> is what the client submits as the envelope's <c>CommandId</c>, and
/// <see cref="ExpiresAt"/> is 24 hours after issuance — long enough to finish an uncertain write, short enough that a
/// receipt can be purged without an old identity restarting anything.
/// </summary>
public sealed record IssuedCommandToken(Guid Id, string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Spec §11: the answer to "may this token start a command for this actor now?". <see cref="Accepted"/> is false for a
/// value the server did not sign, a token bound to a different actor, and one whose 24-hour start window has passed; a
/// refused token never carries an identity out, so nothing can be looked up with it.
/// </summary>
public sealed record VerifiedCommandToken(bool Accepted, Guid Id);
