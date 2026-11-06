using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace JanitorialManagementDashboard.ApiTests.Infrastructure;

/// <summary>
/// Every line the API logs while a test runs, caught as a Serilog sink (facility-0103). The app's level rules
/// (Serilog:MinimumLevel in appsettings.json) still apply, because Serilog filters before a sink sees a line, and
/// each line is also rendered with the Console sink's own template, so a test reads what the server's journal would get.
/// </summary>
public sealed class LogCapture : ILogEventSink
{
    public const string ConsoleTemplateKey = "Serilog:WriteTo:Console:Args:outputTemplate";

    private static readonly MessageTemplateTextFormatter MessageOnly = new("{Message:lj}");

    private readonly ConcurrentQueue<LogLine> _lines = new();
    private MessageTemplateTextFormatter? _consoleLine;

    public IReadOnlyList<LogLine> Lines => _lines.ToArray();

    /// <summary>Takes the Console sink's template from the app's configuration, so the rendered text cannot drift from it.</summary>
    public LogCapture RenderingWith(IConfiguration configuration)
    {
        _consoleLine = new MessageTemplateTextFormatter(
            configuration[ConsoleTemplateKey] ?? throw new InvalidOperationException($"{ConsoleTemplateKey} is not set."));
        return this;
    }

    public void Emit(LogEvent logEvent) =>
        _lines.Enqueue(new LogLine(
            logEvent.Properties.TryGetValue("SourceContext", out var context) && context is ScalarValue { Value: string category } ? category : "",
            logEvent.Level,
            Render(MessageOnly, logEvent) + (logEvent.Exception is null ? "" : $" {logEvent.Exception}"),
            Render(_consoleLine ?? throw new InvalidOperationException("Call RenderingWith before the sink is used."), logEvent)));

    private static string Render(MessageTemplateTextFormatter formatter, LogEvent logEvent)
    {
        using var writer = new StringWriter();
        formatter.Format(logEvent, writer);
        return writer.ToString();
    }
}

/// <summary>A logged line: its category, level and message, and the whole text the Console sink prints for it.</summary>
public sealed record LogLine(string Category, LogEventLevel Level, string Message, string ConsoleText);
