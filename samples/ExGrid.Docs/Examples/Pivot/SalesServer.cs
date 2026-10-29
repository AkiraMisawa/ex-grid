using ExPivot.Engine;

namespace ExGrid.Docs.Examples.Pivot;

/// <summary>
/// A server, simulated in the page: each endpoint takes a question written by <c>PivotJson</c> and answers, after half a
/// second, with the Leaf Aggregates, a page of Items or the records behind a cell (ADR-0370 section 3). The pivot builds
/// the report, its totals and its order from them in the browser. In an application each would be an HTTP endpoint over
/// your own data, and its notices SignalR, Server-Sent Events, a WebSocket or a poll.
/// </summary>
public sealed class SalesServer : IAsyncDisposable
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(500);

    private readonly Sale[] _sales = Sales.Sample();
    private readonly SnapshotPivotSource _data;
    private readonly Random _random = new(7);
    private PeriodicTimer? _trading;

    /// <summary>A server over the sample's sales.</summary>
    public SalesServer()
    {
        _data = PivotSource.From(_sales, Sales.Fields);
        _data.Changed += change => VersionChanged?.Invoke(change.SourceVersion!);
    }

    /// <summary>GET /api/pivot/fields: the fields the server offers.</summary>
    public IReadOnlyList<PivotField> Fields => _data.Fields;

    /// <summary>POST /api/pivot/aggregate: the Leaf Aggregates of a question.</summary>
    public async Task<string> AggregateAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotJson.Write(await _data.AggregateAsync(PivotJson.ReadQuery(question), token));
    }

    /// <summary>POST /api/pivot/items: a page of a field's Items, for Filter.</summary>
    public async Task<string> ItemsAsync(string question, CancellationToken token)
    {
        await Task.Delay(Latency, token);
        return PivotJson.Write(await _data.ItemsAsync(PivotJson.ReadItemsQuery(question), token));
    }

    /// <summary>POST /api/pivot/details: the records behind a cell, for Show Details, under the Source Version its
    /// report was built from, while the server still holds that version.</summary>
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

    /// <summary>
    /// The page's source over the server: each fetcher writes the question as JSON, sends it, and reads the answer back.
    /// The transport, its authentication and its retries are yours. <paramref name="fields"/> are the server's, or yours
    /// over them; while <paramref name="reachable"/> answers false, a question fails as an unreachable server's does.
    /// </summary>
    public FetchingPivotSource Source(IReadOnlyList<PivotField>? fields = null, Func<bool>? reachable = null)
    {
        void Reach()
        {
            if (reachable is { } can && !can())
                throw new HttpRequestException("The server cannot be reached.");
        }
        return PivotSource.Fetch(fields ?? Fields, PivotSourceFeatures.All,
            async (query, token) =>
            {
                Reach();
                return PivotJson.ReadAnswer(await AggregateAsync(PivotJson.Write(query), token));
            },
            async (query, token) =>
            {
                Reach();
                return PivotJson.ReadItemPage(await ItemsAsync(PivotJson.Write(query), token));
            },
            async (query, token) =>
            {
                Reach();
                return PivotJson.ReadDetailPage(await DetailsAsync(PivotJson.Write(query), token));
            });
    }

    public ValueTask DisposeAsync()
    {
        _trading?.Dispose();
        return ValueTask.CompletedTask;
    }
}
