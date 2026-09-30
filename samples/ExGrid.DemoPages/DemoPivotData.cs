using System.Globalization;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// A booked trade as the /pivot page pivots it. Immutable: ExPivot holds a snapshot and a new list
/// is a refresh (ADR-0058). None of it is any real Consumer's: the books and desks are invented and
/// the numbers are a seeded sequence.
/// </summary>
public sealed record DemoPivotTrade(
    string Id,
    string Region,
    string Desk,
    string Book,
    string Product,
    string Currency,
    DateTime TradeDate,
    decimal Notional,
    decimal Pnl,
    int Quantity,
    bool Confirmed)
{
    /// <summary>The trade's month, as a text Item: Excel groups dates by month through a
    /// grouping this first version leaves out, so the page declares the month it wants.</summary>
    public string Month => TradeDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}

/// <summary>The /pivot page's records, fields and first layout.</summary>
public static class DemoPivotData
{
    private static readonly (string Region, string[] Books)[] Regions =
    [
        ("Americas", ["NY-RATES-01", "NY-CREDIT-02", "TOR-FX-01"]),
        ("EMEA", ["LDN-RATES-01", "LDN-FX-02", "FRA-CREDIT-01", "ZRH-EQ-01"]),
        ("APAC", ["TKY-RATES-01", "HKG-EQ-01", "SGP-FX-01"]),
    ];

    private static readonly string[] Desks = ["Rates", "Credit", "FX", "Equities"];

    private static readonly string[] Products = ["Swap", "Bond", "Option", "Future", "Spot"];

    private static readonly string[] Currencies = ["USD", "EUR", "JPY", "GBP", "CHF"];

    /// <summary>Two thousand trades from a fixed seed, so every run of the page — and every
    /// browser test — pivots the same numbers.</summary>
    public static DemoPivotTrade[] Trades(decimal revaluation = 1m)
    {
        var random = new Random(20260930);
        var trades = new DemoPivotTrade[2000];
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
        for (var i = 0; i < trades.Length; i++)
        {
            var (region, books) = Regions[random.Next(Regions.Length)];
            var book = books[random.Next(books.Length)];
            var desk = book.Contains("RATES", StringComparison.Ordinal) ? "Rates"
                : book.Contains("CREDIT", StringComparison.Ordinal) ? "Credit"
                : book.Contains("FX", StringComparison.Ordinal) ? "FX"
                : Desks[3];
            var notional = Math.Round((decimal)(random.Next(1, 500) * 10_000) * revaluation, 2);
            var pnl = Math.Round((decimal)(random.NextDouble() - 0.45) * notional / 100m, 2);
            trades[i] = new DemoPivotTrade(
                Id: "T" + (100000 + i).ToString(CultureInfo.InvariantCulture),
                Region: region,
                Desk: desk,
                Book: book,
                Product: Products[random.Next(Products.Length)],
                Currency: Currencies[random.Next(Currencies.Length)],
                TradeDate: start.AddDays(random.Next(0, 270)),
                Notional: notional,
                Pnl: pnl,
                Quantity: random.Next(1, 50),
                Confirmed: random.Next(10) > 0);
        }
        return trades;
    }

    /// <summary>The Pivot Fields, in the order the Field List shows them.</summary>
    public static readonly PivotField<DemoPivotTrade>[] Fields =
    [
        new("Region", PivotFieldType.Text, t => t.Region),
        new("Desk", PivotFieldType.Text, t => t.Desk),
        new("Book", PivotFieldType.Text, t => t.Book),
        new("Product", PivotFieldType.Text, t => t.Product),
        new("Currency", PivotFieldType.Text, t => t.Currency),
        new("Month", PivotFieldType.Text, t => t.Month),
        new("TradeDate", PivotFieldType.Date, t => t.TradeDate, caption: "Trade date", format: "yyyy-MM-dd"),
        new("Notional", PivotFieldType.Number, t => t.Notional, format: "#,##0.00"),
        new("Pnl", PivotFieldType.Number, t => t.Pnl, caption: "P&L", format: "#,##0.00"),
        new("Quantity", PivotFieldType.Number, t => t.Quantity),
        new("Confirmed", PivotFieldType.Boolean, t => t.Confirmed),
    ];

    /// <summary>The layout the page opens with: P&amp;L by region and desk, across products.</summary>
    public static PivotLayout FirstLayout() => new()
    {
        Filters = [new PivotFieldPlacement("Currency")],
        Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")],
        Columns = [new PivotFieldPlacement("Product")],
        Values = [new PivotValueField("Pnl", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    };
}
