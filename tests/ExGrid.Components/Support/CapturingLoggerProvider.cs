using Microsoft.Extensions.Logging;

namespace ExGrid.Components.Tests.Support;

/// <summary>Keeps every warning the grid writes, so a test can count what the browser's
/// console would show (ADR-0028's named parent, CON-3).</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _warnings = [];

    internal IReadOnlyList<string> Warnings
    {
        get { lock (_warnings) return [.. _warnings]; }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning)
                return;
            lock (owner._warnings)
                owner._warnings.Add(formatter(state, exception));
        }
    }
}
