namespace ExGrid.DemoHost;

/// <summary>
/// Deliberately mutable: the home page's "rewrite in place" button exists to show that
/// an in-place rewrite does not reach the screen — identity, not mutation, is the
/// change signal (ADR-0003).
/// </summary>
public sealed class DemoTrade
{
    public string Book = "";
    public string Trader = "";
    public decimal Notional;
    public DateTime TradeDate;
    public bool Confirmed;
}

public static class DemoData
{
    /// <summary>
    /// One shared, reference-stable columns array. A Consumer that rebuilds this per
    /// render would defeat row memoisation — the same trap as method-group delegates
    /// (ADR-0003).
    /// </summary>
    public static readonly GridColumn<DemoTrade>[] Columns =
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Trader", ColumnType.Text, r => r.Trader),
        new("Notional", ColumnType.Number, r => r.Notional),
        new("TradeDate", ColumnType.Date, r => r.TradeDate, header: "Trade date"),
        new("Confirmed", ColumnType.Boolean, r => r.Confirmed),
    ];

    private static readonly string[] Books = ["Rates", "Credit", "FX", "Equity", "Commodity"];
    private static readonly string[] Traders = ["Ito", "Marsh", "Okafor", "Petrov", "Silva"];

    /// <summary>Deterministic rows so a reload paints the same data.</summary>
    public static DemoTrade[] Window(int count, int seed)
    {
        var random = new Random(seed);
        var rows = new DemoTrade[count];
        for (var i = 0; i < count; i++)
        {
            rows[i] = new DemoTrade
            {
                Book = $"{Books[random.Next(Books.Length)]}-{i + 1:D3}",
                Trader = Traders[random.Next(Traders.Length)],
                Notional = random.Next(-500, 2_000) * 1_000m + random.Next(0, 100),
                TradeDate = new DateTime(2026, 1, 1).AddDays(random.Next(0, 240)),
                Confirmed = random.Next(2) == 0,
            };
        }

        return rows;
    }
}
