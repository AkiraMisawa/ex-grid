using System.Globalization;

namespace ExGrid.DemoApi;

/// <summary>
/// One trade as the database stores it (ADR-0069): money in integer cents, so the database's
/// own <c>SUM</c> is exact, and a Blank currency as null.
/// </summary>
internal readonly record struct GeneratedTrade(
    string TradeId,
    string Region,
    string Desk,
    string Book,
    string Product,
    string? Currency,
    DateOnly TradeDate,
    long NotionalCents,
    long PnlCents,
    int Quantity,
    bool Confirmed);

/// <summary>
/// The demo's trades, generated from a fixed seed (ADR-0069). The vocabulary is
/// <c>DemoPivotData</c>'s — its regions, desks, products and currencies, and its books among
/// more of the same pattern — and the P&amp;L is drawn the way it draws it, spread wide enough
/// for a million trades.
/// <para>
/// Trade <c>n</c> is a function of <c>n</c> alone. The first twenty thousand of a million are
/// therefore the twenty thousand, and a trade the live updates add is the one generation would
/// have made next.
/// </para>
/// <para>
/// Changing anything here changes the data, so <see cref="TradeDatabase.FormatVersion"/> moves
/// with it, and a database written by the old generator is never reused.
/// <c>TradeGeneratorTests</c> pins what the current version generates.
/// </para>
/// </summary>
internal static class TradeGenerator
{
    /// <summary>The seed every trade's numbers come from.</summary>
    public const ulong Seed = 20260930;

    /// <summary>Trade <c>n</c>'s Record Key is <c>T</c> followed by this plus <c>n</c>: eight digits
    /// for every count the server takes, so the keys' text order is their numeric order and a
    /// page in <c>TradeId</c> order is a page in the order the trades were made.</summary>
    public const long FirstKeyNumber = 10_000_000;

    /// <summary>The first trade date. The dates run over 270 days from it, as <c>DemoPivotData</c>'s
    /// do, so the Month field has nine Items.</summary>
    public static readonly DateOnly FirstTradeDate = new(2026, 1, 2);

    /// <summary>How many days the trade dates run over.</summary>
    public const int TradeDays = 270;

    /// <summary>A book whose name holds a comma — a CSV's separator — for the tests that write or
    /// read the data as text. It is trade 3's, and every two-thousandth after.</summary>
    public const string CommaBook = "NY-CREDIT, run-off";

    /// <summary>A book whose name holds double quotes — a CSV's quote, and JSON's. It is trade 5's,
    /// and every two-thousandth after.</summary>
    public const string QuoteBook = "LDN-RATES \"legacy\"";

    private static readonly (string Name, string[] Cities)[] Regions =
    [
        ("Americas", ["NY", "TOR", "CHI", "SAO"]),
        ("EMEA", ["LDN", "FRA", "ZRH", "PAR"]),
        ("APAC", ["TKY", "HKG", "SGP", "SYD"]),
    ];

    private static readonly (string Code, string Name)[] Desks =
    [
        ("RATES", "Rates"),
        ("CREDIT", "Credit"),
        ("FX", "FX"),
        ("EQ", "Equities"),
    ];

    private const int BooksPerDesk = 4;

    private static readonly string[] Products = ["Swap", "Bond", "Option", "Future", "Spot"];

    private static readonly string[] Currencies = ["USD", "EUR", "JPY", "GBP", "CHF"];

    // Every city has every desk's books, numbered -01 to -04: 192 books, which include all ten
    // of DemoPivotData's. Made once, so a million trades share 192 strings.
    private static readonly string[][][][] Books = Regions
        .Select(region => region.Cities
            .Select(city => Desks
                .Select(desk => Enumerable.Range(1, BooksPerDesk)
                    .Select(k => $"{city}-{desk.Code}-{k:00}")
                    .ToArray())
                .ToArray())
            .ToArray())
        .ToArray();

    /// <summary>Trade <c>n</c>'s Record Key.</summary>
    public static string TradeId(long n) => "T" + (FirstKeyNumber + n).ToString(CultureInfo.InvariantCulture);

    /// <summary>Which trade a Record Key names: the inverse of <see cref="TradeId"/>.</summary>
    public static long Number(string tradeId) =>
        long.Parse(tradeId.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture) - FirstKeyNumber;

    /// <summary>Whether trade <c>n</c> has no currency: a Blank, stored as null. Trade 7's, and
    /// every thousandth after.</summary>
    public static bool HasBlankCurrency(long n) => n % 1000 == 7;

    /// <summary>Trade <c>n</c>.</summary>
    public static GeneratedTrade Generate(long n)
    {
        var random = new SplitMix64(Seed, (ulong)n);
        // Every draw is made whatever the special cases below replace, so one trade's special
        // value never shifts another field's numbers.
        var region = random.Next(Regions.Length);
        var city = random.Next(Regions[region].Cities.Length);
        var desk = random.Next(Desks.Length);
        var bookNumber = random.Next(BooksPerDesk);
        var product = Products[random.Next(Products.Length)];
        string? currency = Currencies[random.Next(Currencies.Length)];
        var tradeDate = FirstTradeDate.AddDays(random.Next(TradeDays));
        // DemoPivotData's figures: a notional of 10,000 to 4,990,000 in steps of 10,000, and a
        // P&L of (x − 0.45) × notional / 100 for x drawn in [0, 1) — here x is basis / 10,000,
        // which keeps the P&L a whole number of cents with no rounding: (basis − 4,500) × units.
        var units = 1 + random.Next(499);
        var basis = random.Next(10_000);
        var quantity = 1 + random.Next(49);
        var confirmed = random.Next(10) > 0;

        var regionName = Regions[region].Name;
        var book = Books[region][city][desk][bookNumber];
        var deskName = Desks[desk].Name;
        switch (n % 2000)
        {
            case 3:
                (regionName, deskName, book) = ("Americas", "Credit", CommaBook);
                break;
            case 5:
                (regionName, deskName, book) = ("EMEA", "Rates", QuoteBook);
                break;
        }
        if (HasBlankCurrency(n))
            currency = null;

        return new GeneratedTrade(
            TradeId: TradeId(n),
            Region: regionName,
            Desk: deskName,
            Book: book,
            Product: product,
            Currency: currency,
            TradeDate: tradeDate,
            NotionalCents: units * 1_000_000L,
            PnlCents: (basis - 4_500L) * units,
            Quantity: quantity,
            Confirmed: confirmed);
    }
}
