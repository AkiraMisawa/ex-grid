using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Rows;

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
/// A row for the cells page: a book's limit usage, as a position screen would hold it.
/// The Row Kind is stored on the row here because this Consumer computes its own
/// subtotals — the grid is only told which role each row plays (ADR-0024).
/// </summary>
public sealed class DemoPosition
{
    public string Book = "";
    public decimal CloseOfBusiness;
    public decimal Intraday;
    public double LimitUsed;
    public RowKind Kind;
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

/// <summary>A row of the server page. Values are derived from the position, so the
/// "server" can answer any range without holding ten million values.</summary>
public sealed class DemoServerRow
{
    public int Index;
    public string Book = "";
    public decimal Amount;
    public DateTime AsOf;
}

public static class DemoData
{
    /// <summary>
    /// Stands in for a server: it answers a range and nothing else, and it has no idea
    /// what the grid is showing. The artificial delay is what makes the Placeholders and
    /// the loading state visible — without it every answer lands in the same frame as the
    /// question.
    /// </summary>
    public static async Task<GridPage<DemoServerRow>> FetchAsync(
        GridQuery query, int total, int delayMs, CancellationToken cancellation)
    {
        await Task.Delay(delayMs, cancellation);

        var start = Math.Min(query.Range.Start, total);
        var count = Math.Min(query.Range.Count, total - start);
        var rows = new DemoServerRow[count];
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            rows[i] = new DemoServerRow
            {
                Index = index,
                Book = $"{Books[index % Books.Length]}-{index:D6}",
                Amount = ((index * 7919L) % 1_999_999L) / 100m,
                AsOf = new DateTime(2026, 1, 1).AddMinutes(index),
            };
        }

        return new GridPage<DemoServerRow>(rows, start, total);
    }

    /// <summary>One shared, reference-stable columns array, for the reason
    /// <see cref="Columns"/> is one.</summary>
    public static readonly GridColumn<DemoServerRow>[] ServerColumns =
    [
        new("Index", ColumnType.Number, r => r.Index, width: new ColumnWidthSpec(ColumnWidth.Fixed(90))),
        new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(180))),
        new("Amount", ColumnType.Number, r => r.Amount),
        new("AsOf", ColumnType.Date, r => r.AsOf, header: "As of"),
    ];

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

    /// <summary>
    /// Group headers, their detail rows and a grand total, in the order they are painted
    /// — the Consumer computed all of it, including the aggregates (ADR-0001 / ADR-0024).
    /// </summary>
    public static readonly DemoPosition[] Positions = BuildPositions();

    private static DemoPosition[] BuildPositions()
    {
        var rows = new List<DemoPosition>();
        decimal totalCob = 0, totalIntraday = 0;
        foreach (var region in new[] { "EMEA", "Americas" })
        {
            var groupCob = 0m;
            var groupIntraday = 0m;
            var details = new List<DemoPosition>();
            for (var b = 0; b < Books.Length; b++)
            {
                var book = Books[b];
                var index = b + (region == "EMEA" ? 0 : 5);
                var cob = 1_000m * (index + 3) + (index * 137m);
                var intraday = cob + (index % 3 == 0 ? -420.5m : 318.25m);
                groupCob += cob;
                groupIntraday += intraday;
                details.Add(new DemoPosition
                {
                    Book = $"{region} · {book}",
                    CloseOfBusiness = cob,
                    Intraday = intraday,
                    LimitUsed = Math.Min(1, 0.25 + (index * 0.09)),
                    Kind = RowKind.Detail,
                });
            }

            rows.Add(new DemoPosition
            {
                Book = region,
                CloseOfBusiness = groupCob,
                Intraday = groupIntraday,
                LimitUsed = 0,
                Kind = RowKind.Group,
            });
            rows.AddRange(details);
            totalCob += groupCob;
            totalIntraday += groupIntraday;
        }

        rows.Add(new DemoPosition
        {
            Book = "All books",
            CloseOfBusiness = totalCob,
            Intraday = totalIntraday,
            LimitUsed = 0,
            Kind = RowKind.Total,
        });
        return [.. rows];
    }

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
