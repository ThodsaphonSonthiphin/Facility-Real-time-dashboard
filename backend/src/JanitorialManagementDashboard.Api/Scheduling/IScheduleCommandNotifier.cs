namespace JanitorialManagementDashboard.Api.Scheduling;

/// <summary>
/// Spec §11 and §12: the post-commit invalidation of a committed command. It is an interface with a no-op default so the
/// engine never depends on live data, and it is only ever called after the transaction committed: a failed push must never
/// roll back or change a committed fact. <see cref="ScheduleNotifications"/> is the real, scoped fan-out.
/// </summary>
public interface IScheduleCommandNotifier
{
    /// <summary>
    /// Told about a committed command and the identities it touched only. Reaching another team's recipients is the
    /// implementation's problem; refusing a payload or reporting a rejection is not a notification at all, because nothing
    /// changed.
    /// </summary>
    Task NotifyCommittedAsync(CommandResult result, ScheduleChangeScope scope, CancellationToken ct = default);
}

/// <summary>
/// A notifier that deliberately does nothing, for callers that have no live views to refresh (tests and future seams).
/// </summary>
public sealed class NoOpScheduleCommandNotifier : IScheduleCommandNotifier
{
    public Task NotifyCommittedAsync(CommandResult result, ScheduleChangeScope scope, CancellationToken ct = default) =>
        Task.CompletedTask;
}
