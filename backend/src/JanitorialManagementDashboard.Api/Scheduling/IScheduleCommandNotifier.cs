namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11 and §12: the post-commit invalidation of a committed command. It is an interface with a no-op default so the
/// engine never depends on live data, and it is only ever called after the transaction committed: a failed push must never
/// roll back or change a committed fact. Task 12 supplies the real, scoped fan-out.
/// </summary>
public interface IScheduleCommandNotifier
{
    /// <summary>
    /// Told about a committed command only. Reaching another team's recipients is Task 12's problem; refusing a payload or
    /// reporting a rejection is not a notification at all, because nothing changed.
    /// </summary>
    Task NotifyCommittedAsync(CommandResult result, CancellationToken ct = default);
}

/// <summary>
/// The default registration: with no live notifications yet, a committed command is simply finished once it is committed.
/// </summary>
public sealed class NoOpScheduleCommandNotifier : IScheduleCommandNotifier
{
    public Task NotifyCommittedAsync(CommandResult result, CancellationToken ct = default) => Task.CompletedTask;
}
