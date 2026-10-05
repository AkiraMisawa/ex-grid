using System.Globalization;
using ExPivot.Engine;

namespace PivotRedraw;

// /pivot-live's generator, copied from samples/ExGrid.DemoPages (DemoPivotData.cs and
// PivotLivePage.razor at 60d522ed) rather than referenced, so that this spike does not pull in the
// demo pages' MudBlazor and SignalR dependencies. The seeds, the records, the fields and the
// amendment are the same.

/// <summary>DemoPivotTrade.</summary>
public sealed record LiveTrade(
    string Id, string Region, string Desk, string Book, string Product, string Currency,
    DateTime TradeDate, decimal Notional, decimal Pnl, int Quantity, bool Confirmed);

public static class LiveTrades
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

    /// <summary>DemoPivotData.Generate.</summary>
    public static LiveTrade[] Generate(int count)
    {
        var random = new Random(20260930);
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
        var trades = new LiveTrade[count];
        for (var i = 0; i < count; i++)
        {
            var (region, books) = Regions[random.Next(Regions.Length)];
            var book = books[random.Next(books.Length)];
            var desk = book.Contains("RATES", StringComparison.Ordinal) ? "Rates"
                : book.Contains("CREDIT", StringComparison.Ordinal) ? "Credit"
                : book.Contains("FX", StringComparison.Ordinal) ? "FX"
                : Desks[3];
            var notional = Math.Round((decimal)(random.Next(1, 500) * 10_000), 2);
            var pnl = Math.Round((decimal)(random.NextDouble() - 0.45) * notional / 100m, 2);
            trades[i] = new LiveTrade(
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

    /// <summary>DemoPivotData.Fields.</summary>
    public static readonly PivotFields<LiveTrade> Fields = PivotFields.Of<LiveTrade>()
        .Key("Id", t => t.Id)
        .Text("Region", t => t.Region)
        .Text("Desk", t => t.Desk)
        .Text("Book", t => t.Book)
        .Text("Product", t => t.Product)
        .Text("Currency", t => t.Currency)
        .Date("TradeDate", t => t.TradeDate, caption: "Trade date", format: "yyyy-MM-dd")
        .Month("Month", of: "TradeDate")
        .Number("Notional", t => t.Notional, format: "#,##0.00")
        .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")
        .Number("Quantity", t => t.Quantity)
        .Boolean("Confirmed", t => t.Confirmed);

    /// <summary>PivotLivePage.Amend: <paramref name="count"/> trades, each picked once, their P&amp;L
    /// moved by up to 0.1% of their notional.</summary>
    public static LiveTrade[] Amend(LiveTrade[] trades, int count, Random random)
    {
        var picked = new HashSet<int>();
        while (picked.Count < count)
            picked.Add(random.Next(trades.Length));
        var amended = new List<LiveTrade>(count);
        foreach (var at in picked)
        {
            var trade = trades[at];
            var move = Math.Round(trade.Notional * (decimal)(random.NextDouble() - 0.5) / 500m, 2);
            amended.Add(trades[at] = trade with { Pnl = trade.Pnl + move });
        }
        return [.. amended];
    }
}
