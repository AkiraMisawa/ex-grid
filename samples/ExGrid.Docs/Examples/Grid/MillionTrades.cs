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
