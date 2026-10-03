namespace ExGrid.Docs.Examples.Grid;

/// <summary>A position on a trading desk, as several ExGrid Examples show it.</summary>
public sealed record Position(
    int Id,
    string Desk,
    string Trader,
    string Instrument,
    string Currency,
    int Quantity,
    decimal Cost,
    decimal Price,
    double LimitUsed,
    DateOnly TradeDate,
    bool Hedged)
{
    /// <summary>The unrealised profit or loss: what the position is worth now against what it cost.</summary>
    public decimal Pnl => (Price - Cost) * Quantity;

    private static readonly string[] Desks = ["Rates", "Credit", "FX", "Equities"];
    private static readonly string[] Traders = ["Ishikawa", "Novak", "Osei", "Laurent", "Haddad", "Kowalski", "Brennan", "Sato"];
    private static readonly (string Name, string Currency, decimal Price)[] Instruments =
    [
        ("UST 10Y", "USD", 98.40m), ("Bund 5Y", "EUR", 101.15m), ("Gilt 30Y", "GBP", 87.65m),
        ("JGB 10Y", "JPY", 99.82m), ("iTraxx Main", "EUR", 61.30m), ("CDX IG", "USD", 54.75m),
        ("EUR/USD 3M", "USD", 108.42m), ("USD/JPY 1M", "JPY", 149.10m), ("SPX Dec", "USD", 57.25m),
        ("NKY Mar", "JPY", 38.90m), ("DAX Jun", "EUR", 182.40m), ("FTSE Sep", "GBP", 81.15m),
    ];

    /// <summary><paramref name="count"/> invented positions, the same on every call: the rows
    /// come from a fixed seed.</summary>
    public static Position[] Sample(int count = 300)
    {
        var random = new Random(20261003);
        var rows = new Position[count];
        for (var i = 0; i < count; i++)
        {
            var (name, currency, mark) = Instruments[random.Next(Instruments.Length)];
            var cost = Math.Round(mark * (decimal)(0.94 + random.NextDouble() * 0.12), 2);
            var price = Math.Round(mark * (decimal)(0.97 + random.NextDouble() * 0.06), 2);
            var quantity = (random.Next(2) == 0 ? -1 : 1) * random.Next(1, 400) * 250;
            rows[i] = new Position(
                Id: 5_001 + i,
                Desk: Desks[random.Next(Desks.Length)],
                Trader: Traders[random.Next(Traders.Length)],
                Instrument: name,
                Currency: currency,
                Quantity: quantity,
                Cost: cost,
                Price: price,
                LimitUsed: Math.Round(random.NextDouble() * 1.1, 3),
                TradeDate: new DateOnly(2026, 9, 1).AddDays(random.Next(30)),
                Hedged: random.Next(3) != 0);
        }
        return rows;
    }
}
