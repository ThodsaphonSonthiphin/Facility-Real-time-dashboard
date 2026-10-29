using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FacilityRealtime.ApiTests.Infrastructure;

/// <summary>
/// Every line the API logs while a test runs. The app's level rules (appsettings.json) still apply, because the
/// logger factory filters before a provider sees a line, so a test reads what the server's journal would get.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<LogLine> _lines = new();

    public IReadOnlyList<LogLine> Lines => _lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<LogLine> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue(new LogLine(category, logLevel, exception is null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}"));
    }
}

public sealed record LogLine(string Category, LogLevel Level, string Message);
