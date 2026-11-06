using System.Threading.Channels;
using JanitorialManagementDashboard.Api.Retention;
using JanitorialManagementDashboard.Application.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace JanitorialManagementDashboard.ApiTests.Retention;

public class RetentionWorkerTests
{
    [Fact]
    public async Task Waits_for_startup_runs_immediately_then_next_Bangkok_night_with_fresh_scopes_and_cancels_delay()
    {
        var clock = new ManualClock();
        var lifetime = new Lifetime();
        var recorder = new Recorder();
        using var services = new ServiceCollection().AddSingleton(recorder).AddScoped<IRetentionRunner, ScopedProbe>().BuildServiceProvider();
        using var worker = new RetentionWorker(services.GetRequiredService<IServiceScopeFactory>(), clock, new(), lifetime, NullLogger<RetentionWorker>.Instance);
        await worker.StartAsync(default);
        Assert.Equal(0, recorder.Attempts);
        lifetime.Start();
        var first = await recorder.Runs.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var delay = await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromHours(19), delay);
        Assert.Equal(1, recorder.Disposed);
        clock.Advance(delay);
        var second = await recorder.Runs.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(first, second);
        Assert.Equal(TimeSpan.FromDays(1), await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, recorder.Attempts);
        Assert.Equal(2, recorder.Disposed);
    }

    [Fact]
    public async Task Failure_retries_after_fifteen_minutes_then_returns_to_nightly_schedule()
    {
        var clock = new ManualClock(); var lifetime = new Lifetime(); var recorder = new Recorder { FailOnce = true };
        using var services = new ServiceCollection().AddSingleton(recorder).AddScoped<IRetentionRunner, ScopedProbe>().BuildServiceProvider();
        using var worker = new RetentionWorker(services.GetRequiredService<IServiceScopeFactory>(), clock, new(), lifetime, NullLogger<RetentionWorker>.Instance);
        lifetime.Start(); await worker.StartAsync(default);
        await recorder.Runs.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromMinutes(15), await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, recorder.Disposed);
        clock.Advance(TimeSpan.FromMinutes(15));
        await recorder.Runs.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromHours(18.75), await clock.Delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, recorder.Attempts);
    }

    [Fact]
    public async Task Disabled_or_cancelled_before_startup_never_resolves_a_runner()
    {
        foreach (var enabled in new[] { false, true })
        {
            var recorder = new Recorder();
            using var services = new ServiceCollection().AddSingleton(recorder).AddScoped<IRetentionRunner, ScopedProbe>().BuildServiceProvider();
            using var worker = new RetentionWorker(services.GetRequiredService<IServiceScopeFactory>(), new ManualClock(), new() { Enabled = enabled }, new Lifetime(), NullLogger<RetentionWorker>.Instance);
            await worker.StartAsync(default);
            await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, recorder.Attempts);
        }
    }

    private sealed class Recorder
    {
        public readonly Channel<Guid> Runs = Channel.CreateUnbounded<Guid>();
        public int Attempts;
        public int Disposed;
        public bool FailOnce;
    }
    private sealed class ScopedProbe(Recorder recorder) : IRetentionRunner, IDisposable
    {
        private readonly Guid _id = Guid.NewGuid();
        public Task<RetentionResult> RunAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref recorder.Attempts);
            recorder.Runs.Writer.TryWrite(_id);
            if (recorder.FailOnce) { recorder.FailOnce = false; throw new InvalidOperationException("Synthetic"); }
            return Task.FromResult(new RetentionResult());
        }
        public void Dispose() => Interlocked.Increment(ref recorder.Disposed);
    }
    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
        public void Start() => _started.Cancel();
    }
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        private readonly List<ManualTimer> _timers = [];
        public Channel<TimeSpan> Delays { get; } = Channel.CreateUnbounded<TimeSpan>();
        public override DateTimeOffset GetUtcNow() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            Delays.Writer.TryWrite(dueTime);
            return timer;
        }
        public void Advance(TimeSpan time)
        {
            _now += time;
            foreach (var timer in _timers.ToArray()) if (!timer.Disposed && timer.Due <= _now) { timer.Disposed = true; timer.Fire(); }
        }
        private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
        {
            public DateTimeOffset Due { get; } = due;
            public bool Disposed;
            public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() => Disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
