using System.Globalization;
using ExGrid;
using ExGrid.Columns;

namespace Costs;

// /grid-live-local's trades, columns and amendment, copied from samples/ExGrid.DemoPages
// (DemoPivotData.cs and GridLiveLocalPage.razor at 41c8d8c8) rather than referenced, so this harness
// does not pull in the demo pages' MudBlazor and SignalR dependencies. The record is a record, as
// DemoPivotTrade is: the grid compares rows with EqualityComparer<TRow>.Default in places, and a
// record's Equals is not a reference comparison.

public sealed record Trade(
    string Id, string Region, string Desk, string Book, string Product, string Currency,
    DateTime TradeDate, decimal Notional, decimal Pnl, int Quantity, bool Confirmed);

public static class Trades
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
    public static Trade[] Generate(int count)
    {
        var random = new Random(20260930);
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
        var trades = new Trade[count];
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
            trades[i] = new Trade(
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

    // GridLiveLocalPage: the trades the busy half of every batch falls on, the rows the grid opens on.
    private const int BusyTrades = 500;

    /// <summary>GridLiveLocalPage.Amend: trades each picked once, half of them — up to half the busy
    /// ones — among the busy ones; the P&amp;L moved by up to 0.1% of the notional, and one in four
    /// the notional amended by a million as well. <paramref name="trades"/> keeps each trade's newest
    /// version.</summary>
    public static Trade[] Amend(Trade[] trades, int count, Random random)
    {
        var picked = new HashSet<int>();
        var busy = Math.Min(Math.Min(count / 2, BusyTrades / 2), trades.Length);
        while (picked.Count < busy)
            picked.Add(random.Next(Math.Min(BusyTrades, trades.Length)));
        while (picked.Count < count)
            picked.Add(random.Next(trades.Length));
        var amended = new Trade[picked.Count];
        var i = 0;
        foreach (var at in picked)
        {
            var trade = trades[at];
            var move = Math.Round(trade.Notional * (decimal)(random.NextDouble() - 0.5) / 500m, 2);
            var notional = random.Next(4) != 0 ? trade.Notional
                : trade.Notional > 1_000_000m && random.Next(2) == 0 ? trade.Notional - 1_000_000m
                : trade.Notional + 1_000_000m;
            amended[i++] = trades[at] = trade with { Pnl = trade.Pnl + move, Notional = notional };
        }
        return amended;
    }

    /// <summary>GridLiveLocalPage.Columns.</summary>
    public static readonly GridColumn<Trade>[] Columns =
    [
        new("Id", ColumnType.Text, t => t.Id, header: "Trade ID", width: Fixed(110)),
        new("Region", ColumnType.Text, t => t.Region, width: Fixed(100)),
        new("Desk", ColumnType.Text, t => t.Desk, width: Fixed(90)),
        new("Book", ColumnType.Text, t => t.Book, width: Fixed(140)),
        new("Product", ColumnType.Text, t => t.Product, width: Fixed(90)),
        new("Currency", ColumnType.Text, t => t.Currency, width: Fixed(90)),
        new("TradeDate", ColumnType.Date, t => t.TradeDate, header: "Trade date", width: Fixed(120), align: CellAlign.Left,
            format: value => ((DateTime)value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        new("Notional", ColumnType.Number, t => t.Notional, width: Fixed(170), format: Money),
        new("Pnl", ColumnType.Number, t => t.Pnl, header: "P&L", width: Fixed(150), format: Money),
        new("Quantity", ColumnType.Number, t => t.Quantity, width: Fixed(90)),
        new("Confirmed", ColumnType.Boolean, t => t.Confirmed, width: Fixed(100)),
    ];

    private static ColumnWidthSpec Fixed(double width) => new(ColumnWidth.Fixed(width));

    private static string Money(object value) => ((decimal)value).ToString("#,##0.00", CultureInfo.InvariantCulture);
}
