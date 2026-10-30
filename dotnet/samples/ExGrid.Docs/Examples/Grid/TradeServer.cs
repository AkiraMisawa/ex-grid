using ExGrid.Chrome;
using ExGrid.Docs.Examples.Data;

namespace ExGrid.Docs.Examples.Grid;

/// <summary>
/// A trade server, simulated in the browser: 100,000 trades, answered a page at a time after a
/// network delay. A real one would run the query in its database; this one runs ExGrid's
/// reference implementation, which is what a server's answers are expected to match.
/// </summary>
public sealed class TradeServer(IReadOnlyList<ColumnInfo<Trade>> schema)
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(250);

    private readonly Trade[] _trades = Trade.Sample(100_000);
    private (GridFilter? Filter, IReadOnlyList<SortSpec> Sorts, IReadOnlyList<Trade> Rows)? _last;

    /// <summary>How many pages the server has answered.</summary>
    public int Answered { get; private set; }

    /// <summary>One page of the trades the query selects, in its order, and how many there are in all.</summary>
    public async ValueTask<GridPage<Trade>> PageAsync(GridQuery query, CancellationToken cancellation)
    {
        await Task.Delay(Latency, cancellation);
        var result = Select(query.Filter, query.Sorts);
        var start = Math.Min(query.Range.Start, result.Count);
        var count = Math.Min(query.Range.Count, result.Count - start);
        Answered++;
        return new GridPage<Trade>(result.Skip(start).Take(count).ToArray(), start, result.Count);
    }

    /// <summary>The distinct values of one column under a filter, for the filter panel's value list.</summary>
    public async Task<DistinctValues> DistinctAsync(string column, GridFilter? filter, CancellationToken cancellation)
    {
        await Task.Delay(Latency, cancellation);
        var info = schema.First(c => c.Name == column);
        var values = GridQueryEngine.Apply(_trades, schema, filter, []).Select(info.Value).Distinct().Take(1_001).ToList();
        return values.Count > 1_000 ? DistinctValues.TooMany : DistinctValues.Of(values);
    }

    // The last result is kept, so scrolling through one query does not sort the trades again.
    // The source asks every page of one query with the same Filter instance.
    private IReadOnlyList<Trade> Select(GridFilter? filter, IReadOnlyList<SortSpec> sorts)
    {
        if (_last is { } last && ReferenceEquals(last.Filter, filter) && last.Sorts.SequenceEqual(sorts))
            return last.Rows;
        var rows = GridQueryEngine.Apply(_trades, schema, filter, sorts);
        _last = (filter, sorts.ToArray(), rows);
        return rows;
    }
}
