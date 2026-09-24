using Microsoft.Extensions.Logging;

namespace CodeCiir.Api.Tests.Support;

/// <summary>Collects every log entry written through it, so tests can assert on what the application logged.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

    public void Dispose()
    {
        // Nothing to release.
    }

    internal sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
