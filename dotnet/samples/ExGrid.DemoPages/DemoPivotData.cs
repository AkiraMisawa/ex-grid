using System.Globalization;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// A booked trade as the /pivot page pivots it. Immutable: ExPivot holds a snapshot and a new list
/// is a refresh (ADR-0059). None of it is any real Consumer's: the books and desks are invented and
/// the numbers are a seeded sequence. Its month is not a property: the Pivot Fields declare it as a
/// part of the trade date (ADR-0060).
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
    bool Confirmed);

/// <summary>The /pivot page's records, fields and first layout. /pivot-csv writes the same trades
/// as a CSV and reads them back.</summary>
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

    /// <summary>The trades the page opens with.</summary>
    public const int Count = 2000;

    /// <summary><paramref name="count"/> trades from a fixed seed, so every run of the page — and
    /// every browser test — pivots the same numbers; the first 2,000 are the same whatever the count.</summary>
    public static DemoPivotTrade[] Trades(decimal revaluation = 1m, int count = Count) => [.. Generate(count, revaluation)];

    /// <summary>The same trades as <see cref="Trades"/>, made one at a time as they are enumerated.</summary>
    public static IEnumerable<DemoPivotTrade> Generate(int count, decimal revaluation = 1m)
    {
        var random = new Random(20260930);
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
        for (var i = 0; i < count; i++)
        {
            var (region, books) = Regions[random.Next(Regions.Length)];
            var book = books[random.Next(books.Length)];
            var desk = book.Contains("RATES", StringComparison.Ordinal) ? "Rates"
                : book.Contains("CREDIT", StringComparison.Ordinal) ? "Credit"
                : book.Contains("FX", StringComparison.Ordinal) ? "FX"
                : Desks[3];
            var notional = Math.Round((decimal)(random.Next(1, 500) * 10_000) * revaluation, 2);
            var pnl = Math.Round((decimal)(random.NextDouble() - 0.45) * notional / 100m, 2);
            yield return new DemoPivotTrade(
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
    }

    #region The code: fields
    // Each field once, with a typed accessor and its settings: the declaration makes both the
    // Snapshot column the trades are read into, boxing no value, and the Pivot Field over it.
    public static readonly PivotFields<DemoPivotTrade> Fields = PivotFields.Of<DemoPivotTrade>()
        .Key("Id", t => t.Id)                                     // the Record Key, not a field
        .Text("Region", t => t.Region)
        .Text("Desk", t => t.Desk)
        .Text("Book", t => t.Book)
        .Text("Product", t => t.Product)
        .Text("Currency", t => t.Currency)
        .Date("TradeDate", t => t.TradeDate, caption: "Trade date", format: "yyyy-MM-dd")
        .Month("Month", of: "TradeDate")                          // Jan … Dec, by the calendar
        .Number("Notional", t => t.Notional, format: "#,##0.00")  // decimal: summed exactly
        .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")
        .Number("Quantity", t => t.Quantity)                      // int: an Integer column
        .Boolean("Confirmed", t => t.Confirmed);                  // TRUE and FALSE

    // P&L by region and desk, across products, filtered by currency.
    public static PivotLayout FirstLayout() => new()
    {
        Filters = [new PivotFieldPlacement("Currency")],
        Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")],
        Columns = [new PivotFieldPlacement("Product")],
        Values = [new PivotValueField("Pnl", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    };
    #endregion
}
