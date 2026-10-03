using ExPivot.Engine;

namespace ExGrid.Docs.Examples.Pivot;

/// <summary>
/// A server's side of a Pivot Source, simulated in the page: the endpoints a real server would
/// expose over HTTP — each takes a question as <c>PivotJson</c> and answers with JSON, after a
/// network's delay. It answers from the bundled engine over its own Snapshot; a server answering
/// from SQL builds the same answers with <c>PivotAnswerBuilder</c>.
/// </summary>
public sealed class SalesServer : IDisposable
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(500);

    private readonly Sale[] _sales = Sales.Sample();
    private readonly SnapshotPivotSource _data;
    private readonly Random _random = new(7);
    private PeriodicTimer? _trading;

    public SalesServer()
    {
        _data = PivotSource.From(_sales, Sales.Fields);
        _data.Changed += change => VersionChanged?.Invoke(change.SourceVersion!);
    }

    /// <summary>GET /api/pivot/fields: the fields the server offers.</summary>
    public IReadOnlyList<PivotField> Fields => _data.Fields;

    /// <summary>POST /api/pivot/aggregate.</summary>
    public async Task<string> AggregateAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotJson.Write(await _data.AggregateAsync(PivotJson.ReadQuery(question), token));
    }

    /// <summary>POST /api/pivot/items: a field's Items, for Filter….</summary>
    public async Task<string> ItemsAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotJson.Write(await _data.ItemsAsync(PivotJson.ReadItemsQuery(question), token));
    }

    /// <summary>POST /api/pivot/details: the records behind a cell, for Show Details.</summary>
    public async Task<string> DetailsAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotJson.Write(await _data.DetailsAsync(PivotJson.ReadDetailsQuery(question), token));
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

    public void Dispose() => _trading?.Dispose();
}
