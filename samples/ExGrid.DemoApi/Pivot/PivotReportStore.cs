using ExPivot.Engine;

namespace ExGrid.DemoApi;

// The demo Consumer owns calculation lifetime. Expiry never rebinds an old operation:
// only a Window request may create replacement state, and it receives a complete reset.
internal sealed class PivotReportStore(TradePivotSource provider, TradeStore trades) : BackgroundService
{
    internal const int Capacity = 8;
    internal static readonly TimeSpan IdleTime = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private string? _observed;
    private sealed class Entry(LocalPivotReportSource source)
    {
        public LocalPivotReportSource Source { get; } = source;
        public DateTimeOffset Used { get; set; } = DateTimeOffset.UtcNow;
    }

    internal async ValueTask<T> AnswerAsync<T>(string id, bool create,
        Func<LocalPivotReportSource?, ValueTask<T>> answer, CancellationToken token)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new FormatException("A report identity must be a UUID in N format.");
        await _gate.WaitAsync(token);
        try
        {
            await PruneAsync();
            if (create && !_entries.ContainsKey(id))
            {
                if (_entries.Count >= Capacity)
                {
                    var oldest = _entries.MinBy(pair => pair.Value.Used);
                    _entries.Remove(oldest.Key);
                    await oldest.Value.Source.DisposeAsync();
                }
                _entries.Add(id, new(PivotReportSource.From(provider)));
            }
            if (!_entries.TryGetValue(id, out var entry)) return await answer(null);
            entry.Used = DateTimeOffset.UtcNow;
            // The SQL provider knows only that a version changed: full refresh is explicit.
            if (_observed != trades.Version)
            {
                _observed = trades.Version;
                await provider.RefreshAsync(token);
            }
            return await answer(entry.Source);
        }
        finally { _gate.Release(); }
    }

    internal async Task RemoveAsync(string id, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try { if (_entries.Remove(id, out var entry)) await entry.Source.DisposeAsync(); }
        finally { _gate.Release(); }
    }

    private async ValueTask PruneAsync()
    {
        var cutoff = DateTimeOffset.UtcNow - IdleTime;
        foreach (var id in _entries.Where(pair => pair.Value.Used < cutoff).Select(pair => pair.Key).ToArray())
        {
            var entry = _entries[id];
            _entries.Remove(id);
            await entry.Source.DisposeAsync();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _gate.WaitAsync(stoppingToken);
                try { await PruneAsync(); }
                finally { _gate.Release(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                foreach (var entry in _entries.Values) await entry.Source.DisposeAsync();
                _entries.Clear();
            }
            finally { _gate.Release(); }
        }
    }

    internal static PivotReportRefusal NotHeld()
        => new(PivotReportRefusalKind.ReportVersionNotHeld, "The report expired; request its current Window again.");
}
