using FacilityRealtime.Application.Retention;

namespace FacilityRealtime.Api.Retention;

/// <summary>Single-server catch-up and nightly policy. Each attempt owns a fresh scope and context.</summary>
public sealed class RetentionWorker(IServiceScopeFactory scopes, TimeProvider clock, RetentionSettings settings,
    IHostApplicationLifetime lifetime, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled) return;
        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delay;
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IRetentionRunner>().RunAsync(stoppingToken);
                    logger.LogInformation("Retention completed: GPS cleared {GpsCleared}, summaries created {SummariesCreated}, records deleted {RecordsDeleted}",
                        result.GpsCleared, result.SummariesCreated, result.RecordsDeleted);
                    var now = clock.GetUtcNow().UtcDateTime;
                    delay = RetentionSchedule.NextRunUtc(now, settings.RunAtHour) - now;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    // Exception messages/SQL may contain retained data. Log category and aggregate context only.
                    logger.LogWarning("Retention failed ({FailureType}); retry in {RetryMinutes} minutes", error.GetType().Name, settings.RetryMinutes);
                    delay = TimeSpan.FromMinutes(settings.RetryMinutes);
                }
                await Task.Delay(delay, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
