using ExGrid.Docs.Examples.Data;

namespace ExGrid.Docs.Examples.Grid;

/// <summary>
/// A million invented trades that are never all in memory: each one is made from its position
/// when it is asked for, the same every time.
/// </summary>
public static class MillionTrades
{
    /// <summary>How many trades there are.</summary>
    public const int Count = 1_000_000;

    private static readonly string[] Books = ["Rates", "Credit", "FX", "Equities"];
    private static readonly string[] Traders = ["Ishikawa", "Novak", "Osei", "Laurent", "Haddad", "Kowalski"];
    private static readonly string[] Instruments = ["UST 10Y", "Bund 5Y", "EUR/USD", "USD/JPY", "iTraxx Main", "CDX IG", "SPX Dec", "NKY Mar"];
    private static readonly string[] Currencies = ["USD", "EUR", "JPY", "GBP"];

    /// <summary>The columns of a trade. This server neither sorts nor filters, so no column is queryable: a header click
    /// sorts nothing, and a column's menu offers its commands only.</summary>
    public static readonly GridColumn<Trade>[] Columns =
    [
        new("Id", ColumnType.Number, t => t.Id) { IsQueryable = false },
        new("Book", ColumnType.Text, t => t.Book) { IsQueryable = false },
        new("Trader", ColumnType.Text, t => t.Trader) { IsQueryable = false },
        new("Instrument", ColumnType.Text, t => t.Instrument) { IsQueryable = false },
        new("Notional", ColumnType.Number, t => t.Notional, format: v => ((decimal)v).ToString("N0")) { IsQueryable = false },
        new("Price", ColumnType.Number, t => t.Price) { IsQueryable = false },
        new("TradeDate", ColumnType.Date, t => t.TradeDate, header: "Trade date", dateType: DateType.DateOnly) { IsQueryable = false },
    ];

    /// <summary>
    /// A million trades fetched a page at a time. A trade's id names it across pages, so a row fetched again keeps its
    /// row and only the text that changed is written; no id repeats, so the source vouches for its Windows. It reads 50
    /// rows either side of what the grid asks for, so ordinary scrolling does not ask again at once. Each grid takes a
    /// source of its own, disposed with the page.
    /// </summary>
    public static FetchingGridSource<Trade> Source() => GridSource.Fetch<Trade>(FetchAsync, readAheadRows: 50, rowKey: t => t.Id);

    // Where a server call would go: it answers after 150 ms, or not at all once the grid has asked for other rows.
    private static async ValueTask<GridPage<Trade>> FetchAsync(GridQuery query, CancellationToken cancellationToken)
    {
        if (query.Sorts.Count > 0 || query.Filter is not null)
            throw new NotSupportedException("This server neither sorts nor filters.");
        await Task.Delay(150, cancellationToken);
        var start = Math.Min(query.Range.Start, Count);
        var end = Math.Min(start + query.Range.Count, Count);
        return new GridPage<Trade>(Range(start, end - start), start, Count);
    }

    /// <summary>The line under the grid: which rows <paramref name="source"/> holds now.</summary>
    public static string Holding(FetchingGridSource<Trade> source) => source.Window.Count == 0
        ? "Holding no rows yet."
        : $"Holding rows {source.WindowStart:N0} to {source.WindowStart + source.Window.Count - 1:N0} of {Count:N0}.";

    /// <summary>The trades from <paramref name="start"/>, <paramref name="count"/> of them.</summary>
    public static Trade[] Range(int start, int count)
    {
        var trades = new Trade[count];
        for (var i = 0; i < count; i++)
            trades[i] = At(start + i);
        return trades;
    }

    private static Trade At(int index)
    {
        var random = new Random(index);
        return new Trade(
            Id: 1_000_001 + index,
            Book: Books[random.Next(Books.Length)],
            Trader: Traders[random.Next(Traders.Length)],
            Instrument: Instruments[random.Next(Instruments.Length)],
            Currency: Currencies[random.Next(Currencies.Length)],
            Notional: Math.Round((decimal)(random.NextDouble() * 49 + 1), 1) * 1_000_000m,
            Price: Math.Round((decimal)(random.NextDouble() * 40 + 80), 3),
            TradeDate: new DateOnly(2026, 1, 2).AddDays(index / 4_000),
            Confirmed: random.Next(4) != 0);
    }
}
