namespace ExGrid.DemoHost.Server;

/// <summary>
/// Appends every log line of Warning and above to one file, so layer 3 can read what a
/// test left in the host's log (CON-6). On WebAssembly the host's log is the browser
/// console, which the fixture already hears; on a Server host it is this process's, and
/// the run's piped output cannot be read back per test.
/// </summary>
internal sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Append(string line)
    {
        lock (_gate)
            File.AppendAllText(path, line + Environment.NewLine);
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var line = $"{DateTimeOffset.UtcNow:O} {logLevel} {category}: {formatter(state, exception)}";
            provider.Append(exception is null ? line : $"{line}{Environment.NewLine}{exception}");
        }
    }
}
