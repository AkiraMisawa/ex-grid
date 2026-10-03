namespace ExGrid.Docs.Examples.Data;

/// <summary>A trade, as the ExGrid Examples show it.</summary>
public sealed record Trade(
    int Id,
    string Book,
    string Trader,
    string Instrument,
    string Currency,
    decimal Notional,
    decimal Price,
    DateOnly TradeDate,
    bool Confirmed)
{
    private static readonly string[] Books = ["Rates", "Credit", "FX", "Equities"];
    private static readonly string[] Traders = ["Ishikawa", "Novak", "Osei", "Laurent", "Haddad", "Kowalski"];
    private static readonly string[] Instruments = ["UST 10Y", "Bund 5Y", "EUR/USD", "USD/JPY", "iTraxx Main", "CDX IG", "SPX Dec", "NKY Mar"];
    private static readonly string[] Currencies = ["USD", "EUR", "JPY", "GBP"];

    /// <summary><paramref name="count"/> invented trades, the same on every call: the rows come
    /// from a fixed seed.</summary>
    public static Trade[] Sample(int count = 200)
    {
        var random = new Random(20261003);
        var trades = new Trade[count];
        for (var i = 0; i < count; i++)
        {
            trades[i] = new Trade(
                Id: 10_001 + i,
                Book: Books[random.Next(Books.Length)],
                Trader: Traders[random.Next(Traders.Length)],
                Instrument: Instruments[random.Next(Instruments.Length)],
                Currency: Currencies[random.Next(Currencies.Length)],
                Notional: Math.Round((decimal)(random.NextDouble() * 49 + 1), 1) * 1_000_000m,
                Price: Math.Round((decimal)(random.NextDouble() * 40 + 80), 3),
                TradeDate: new DateOnly(2026, 9, 1).AddDays(random.Next(30)),
                Confirmed: random.Next(4) != 0);
        }
        return trades;
    }
}
