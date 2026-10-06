using ExPivot.Engine;

namespace ExGrid.Docs.Examples.Pivot;

/// <summary>
/// A server report, simulated in the page: each endpoint takes a <c>PivotReportJson</c> question
/// and answers after a network's delay. The shared engine retains computation over the server's
/// Snapshot; only requested Windows and versioned operation results leave it.
/// </summary>
public sealed class SalesServer : IAsyncDisposable
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(500);

    private readonly Sale[] _sales = Sales.Sample();
    private readonly SnapshotPivotSource _data;
    private readonly LocalPivotReportSource _report;
    private readonly Random _random = new(7);
    private PeriodicTimer? _trading;

    public SalesServer()
    {
        _data = PivotSource.From(_sales, Sales.Fields);
        _report = PivotReportSource.From(_data);
        _data.Changed += change => VersionChanged?.Invoke(change.SourceVersion!);
    }

    /// <summary>GET /api/pivot/fields: the fields the server offers.</summary>
    public IReadOnlyList<PivotField> Fields => _data.Fields;

    /// <summary>A Window request. Only requested display rows leave this calculation.</summary>
    public async Task<string> WindowAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotReportJson.Write(await _report.WindowAsync(PivotReportJson.Read<PivotReportRequest>(question), token));
    }

    /// <summary>Versioned, labeled Items for Filter.</summary>
    public async Task<string> ItemsAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotReportJson.Write(await _report.ItemsAsync(PivotReportJson.Read<PivotReportItemsQuery>(question), token));
    }

    /// <summary>Versioned offscreen Copy.</summary>
    public async Task<string> CopyAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotReportJson.Write(await _report.CopyAsync(PivotReportJson.Read<PivotReportCopyQuery>(question), token));
    }

    /// <summary>Versioned Selection Summary.</summary>
    public async Task<string> SummaryAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotReportJson.Write(await _report.SummaryAsync(PivotReportJson.Read<PivotReportSummaryQuery>(question), token));
    }

    /// <summary>The source records behind the selected report version's cell.</summary>
    public async Task<string> DetailsAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotReportJson.Write(await _report.DetailsAsync(PivotReportJson.Read<PivotReportDetailsQuery>(question), token));
    }

    /// <summary>What a SignalR hub would push: the Source Version the data has moved on to.</summary>
    public event Action<string>? VersionChanged;

    /// <summary>Starts amending a few sales every <paramref name="every"/>, as orders are
    /// changed elsewhere; each amendment is one Change Batch.</summary>
    public void StartTrading(TimeSpan every)
    {
        _trading ??= new PeriodicTimer(every);
        _ = TradeAsync(_trading);
    }

    private async Task TradeAsync(PeriodicTimer timer)
    {
        while (await timer.WaitForNextTickAsync())
            _data.Apply(Sales.Fields.Batch(changed: Sales.Amend(_sales, _random, count: 8)));
    }

    public async ValueTask DisposeAsync()
    {
        _trading?.Dispose();
        await _report.DisposeAsync();
    }
}
