using ExGrid.Columns;

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

/// <summary>
/// A row for the wide page. It holds only its position and derives every value from it:
/// a hundred columns times a hundred thousand rows is ten million values, and storing
/// them would say more about the sample's memory use than about the grid.
/// </summary>
public sealed class DemoWideRow
{
    public int Index;
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

    /// <summary>
    /// A hundred columns, the first two of them pinnable identifiers and the rest
    /// metrics — the shape ADR-0004 measured as unpaintable at 60fps without horizontal
    /// virtualisation. Fixed widths so a frame-time comparison is not also measuring
    /// Auto columns settling. One shared, reference-stable array, for the same reason
    /// <see cref="Columns"/> is one.
    /// </summary>
    public static readonly GridColumn<DemoWideRow>[] WideColumns = BuildWideColumns(100);

    private static GridColumn<DemoWideRow>[] BuildWideColumns(int count)
    {
        var columns = new GridColumn<DemoWideRow>[count];
        columns[0] = new GridColumn<DemoWideRow>("Key", ColumnType.Text, r => $"K-{r.Index:D6}",
            width: new ColumnWidthSpec(ColumnWidth.Fixed(140)));
        columns[1] = new GridColumn<DemoWideRow>("Book", ColumnType.Text, r => Books[r.Index % Books.Length],
            width: new ColumnWidthSpec(ColumnWidth.Fixed(110)));
        for (var i = 2; i < count; i++)
        {
            var metric = i;
            columns[i] = new GridColumn<DemoWideRow>($"M{metric - 1:D2}", ColumnType.Number,
                r => Metric(r.Index, metric),
                width: new ColumnWidthSpec(ColumnWidth.Fixed(90)));
        }

        return columns;
    }

    /// <summary>Deterministic, and computed rather than stored — see
    /// <see cref="DemoWideRow"/>.</summary>
    private static decimal Metric(int rowIndex, int metric)
        => ((rowIndex * 7919L + metric * 104729L) % 1_999_999L) / 100m;

    public static DemoWideRow[] WideRows(int count)
    {
        var rows = new DemoWideRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = new DemoWideRow { Index = i };
        return rows;
    }

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
