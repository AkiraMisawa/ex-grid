using ExGrid.Docs.Examples.Data;
using ExGrid.Finding;
using ExGrid.Data;

namespace ExGrid.Docs.Examples.Grid;

/// <summary>
/// Trades held by the page, as a source with its own answer to a Find step: here the reference step over the rows in
/// their current order, by the displayed text of the columns that hold text. A source that is not
/// <c>GridSource.From</c>'s gets no marks unless the grid is passed the rule it finds by, in the browser.
/// </summary>
public static class FindingTrades
{
    private static readonly Trade[] Trades = Trade.Sample(2_000);

    /// <summary>The columns of a trade.</summary>
    public static readonly GridColumn<Trade>[] Columns =
    [
        new("Id", ColumnType.Number, t => t.Id),
        new("Book", ColumnType.Text, t => t.Book),
        new("Trader", ColumnType.Text, t => t.Trader),
        new("Instrument", ColumnType.Text, t => t.Instrument),
        new("Notional", ColumnType.Number, t => t.Notional, format: v => ((decimal)v).ToString("N0")),
        new("TradeDate", ColumnType.Date, t => t.TradeDate, header: "Trade date", dateType: DateType.DateOnly),
    ];

    /// <summary>The rule the search finds by, for the cells on screen: <c>match</c> in the site's
    /// <c>js/find-marks.js</c>, GridFind.Matches over the same columns.</summary>
    public static readonly ScriptFunction Match = ScriptFunction.Of("./js/find-marks.js", "match");

    private static readonly IReadOnlyList<ColumnInfo<Trade>> Infos = [.. Columns.Select(c => c.Info)];

    // Searched by their displayed text; the others are skipped, and the script marks none of theirs.
    private static readonly Dictionary<string, Func<Trade, string>> Searched = Columns
        .Where(c => c.Info.Type == ColumnType.Text)
        .ToDictionary(c => c.Name, c => (Func<Trade, string>)c.Info.TextOf);

    /// <summary>A source of its own for each grid, disposed with the page.</summary>
    public static FetchingGridSource<Trade> Source() => GridSource.Fetch<Trade>(FetchAsync, find: FindAsync);

    // Where a server call would go, here answered at once from the rows held.
    private static ValueTask<GridPage<Trade>> FetchAsync(GridQuery query, CancellationToken cancellationToken)
    {
        var rows = GridQueryEngine.Apply(Trades, Infos, query.Filter, query.Sorts);
        var start = Math.Min(query.Range.Start, rows.Count);
        var end = Math.Min(start + query.Range.Count, rows.Count);
        return ValueTask.FromResult(new GridPage<Trade>([.. rows.Skip(start).Take(end - start)], start, rows.Count));
    }

    private static Task<GridFindResult> FindAsync(GridFindRequest request, GridFilter? filter, IReadOnlyList<SortSpec> sorts, CancellationToken cancellationToken)
        => Task.FromResult(GridFind.Step(GridQueryEngine.Apply(Trades, Infos, filter, sorts), request, name => Searched.GetValueOrDefault(name)));
}
