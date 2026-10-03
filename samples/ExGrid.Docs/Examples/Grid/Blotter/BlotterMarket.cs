namespace ExGrid.Docs.Examples.Grid.Blotter;

/// <summary>One instrument the desk trades, and its quote.</summary>
public sealed record BlotterInstrument(
    string Symbol, string Desk, string Currency, decimal Multiplier, long Lot, int Decimals, double Volatility,
    decimal PreviousClose, decimal Bid, decimal Ask);

/// <summary>
/// An invented market: the desk's instruments, the trades booked on them, and quotes that move.
/// Everything comes from fixed seeds, so every visit shows the same trades and the same first moves.
/// </summary>
public sealed class BlotterMarket
{
    /// <summary>The first trade's id.</summary>
    public const int FirstId = 4_100_001;

    private static readonly (string Symbol, string Desk, string Currency, decimal Price, decimal Multiplier, long Lot, int Decimals, double Volatility)[] Universe =
    [
        ("UST 2Y", "Rates", "USD", 99.84m, 0.01m, 1_000_000, 3, 0.0004), ("UST 5Y", "Rates", "USD", 98.62m, 0.01m, 1_000_000, 3, 0.0006),
        ("UST 10Y", "Rates", "USD", 97.15m, 0.01m, 1_000_000, 3, 0.0008), ("UST 30Y", "Rates", "USD", 92.40m, 0.01m, 1_000_000, 3, 0.0012),
        ("Bund 10Y", "Rates", "EUR", 101.32m, 0.01m, 1_000_000, 3, 0.0007), ("OAT 10Y", "Rates", "EUR", 96.75m, 0.01m, 1_000_000, 3, 0.0008),
        ("BTP 10Y", "Rates", "EUR", 94.18m, 0.01m, 1_000_000, 3, 0.0010), ("Gilt 10Y", "Rates", "GBP", 95.06m, 0.01m, 1_000_000, 3, 0.0009),
        ("JGB 10Y", "Rates", "JPY", 99.71m, 0.01m, 1_000_000, 3, 0.0003),
        ("CDX IG 47", "Credit", "USD", 101.24m, 0.01m, 1_000_000, 3, 0.0005), ("CDX HY 47", "Credit", "USD", 106.85m, 0.01m, 1_000_000, 3, 0.0011),
        ("iTraxx Main 44", "Credit", "EUR", 100.92m, 0.01m, 1_000_000, 3, 0.0005), ("iTraxx XO 44", "Credit", "EUR", 108.10m, 0.01m, 1_000_000, 3, 0.0012),
        ("EUR/USD", "FX", "USD", 1.0842m, 1m, 1_000_000, 5, 0.0003), ("USD/JPY", "FX", "JPY", 149.315m, 1m, 1_000_000, 3, 0.0004),
        ("GBP/USD", "FX", "USD", 1.2671m, 1m, 1_000_000, 5, 0.0004), ("AUD/USD", "FX", "USD", 0.6588m, 1m, 1_000_000, 5, 0.0005),
        ("USD/CHF", "FX", "CHF", 0.8823m, 1m, 1_000_000, 5, 0.0004), ("USD/CAD", "FX", "CAD", 1.3594m, 1m, 1_000_000, 5, 0.0003),
        ("EUR/GBP", "FX", "GBP", 0.8556m, 1m, 1_000_000, 5, 0.0003), ("USD/MXN", "FX", "MXN", 17.2410m, 1m, 1_000_000, 4, 0.0007),
        ("ES Dec26", "Equities", "USD", 5_812.25m, 50m, 10, 2, 0.0009), ("NQ Dec26", "Equities", "USD", 20_604.50m, 20m, 10, 2, 0.0012),
        ("FDAX Dec26", "Equities", "EUR", 19_244.00m, 25m, 5, 1, 0.0010), ("FESX Dec26", "Equities", "EUR", 4_986.00m, 10m, 20, 0, 0.0010),
        ("NKD Dec26", "Equities", "JPY", 38_910.00m, 5m, 10, 0, 0.0011), ("Z Dec26", "Equities", "GBP", 8_312.50m, 10m, 20, 1, 0.0008),
        ("CL Jan27", "Commodities", "USD", 71.84m, 1_000m, 10, 2, 0.0016), ("GC Dec26", "Commodities", "USD", 2_651.40m, 100m, 5, 1, 0.0008),
        ("NG Jan27", "Commodities", "USD", 3.412m, 10_000m, 10, 3, 0.0024), ("HG Mar27", "Commodities", "USD", 4.2135m, 25_000m, 5, 4, 0.0013),
        ("SI Mar27", "Commodities", "USD", 31.245m, 5_000m, 5, 3, 0.0015),
    ];

    private static readonly Dictionary<string, string[]> Books = new()
    {
        ["Rates"] = ["G10 Govies", "Swap Spreads", "Inflation"],
        ["Credit"] = ["IG Index", "HY Index"],
        ["FX"] = ["G10 Spot", "EM Spot", "FX Options Hedge"],
        ["Equities"] = ["Index Futures", "Delta One"],
        ["Commodities"] = ["Energy", "Metals"],
    };

    private static readonly string[] Traders = ["A. Ishikawa", "M. Novak", "K. Osei", "C. Laurent", "R. Haddad", "P. Kowalski", "J. Brennan", "Y. Sato", "L. Moreau", "D. Okafor"];
    private static readonly string[] Counterparties = ["Halden Capital", "Norrland Bank", "Cobalt Asset Mgmt", "Vireo Partners", "Austral Pension Fund", "Kestrel Securities", "Marlowe & Finch", "Tidewater Fund", "Sable Clearing", "Orrin Investments"];
    private static readonly string[] Statuses = ["Confirmed", "Confirmed", "Confirmed", "Confirmed", "Confirmed", "Confirmed", "Affirmed", "Affirmed", "Affirmed", "Pending", "Amended"];

    private readonly Random _random = new(77);
    private readonly Dictionary<string, BlotterInstrument> _instruments;

    /// <summary>Opens the market at yesterday's close.</summary>
    public BlotterMarket()
    {
        _instruments = Universe.ToDictionary(u => u.Symbol, u =>
        {
            var half = Spread(u.Price, u.Decimals);
            return new BlotterInstrument(u.Symbol, u.Desk, u.Currency, u.Multiplier, u.Lot, u.Decimals, u.Volatility,
                u.Price, u.Price - half, u.Price + half);
        });
    }

    /// <summary>The instruments, in the order the desk lists them.</summary>
    public IEnumerable<BlotterInstrument> Instruments => _instruments.Values;

    /// <summary>The trades booked today and over the last weeks, priced at the current quotes.</summary>
    public BlotterTrade[] Book(int count)
    {
        var random = new Random(20261003);
        var symbols = Universe.Select(u => u.Symbol).ToArray();
        var trades = new BlotterTrade[count];
        for (var i = 0; i < count; i++)
        {
            var instrument = _instruments[symbols[random.Next(symbols.Length)]];
            var drift = (decimal)((random.NextDouble() - 0.5) * instrument.Volatility * 40);
            var price = Math.Round(instrument.PreviousClose * (1 + drift), instrument.Decimals);
            var books = Books[instrument.Desk];
            trades[i] = new BlotterTrade(
                Id: FirstId + i,
                Instrument: instrument.Symbol,
                Desk: instrument.Desk,
                Book: books[random.Next(books.Length)],
                Trader: Traders[random.Next(Traders.Length)],
                Counterparty: Counterparties[random.Next(Counterparties.Length)],
                Side: random.Next(2) == 0 ? "Buy" : "Sell",
                Currency: instrument.Currency,
                Quantity: instrument.Lot * random.Next(1, 40),
                Multiplier: instrument.Multiplier,
                TradePrice: price,
                PreviousClose: instrument.PreviousClose,
                Bid: instrument.Bid,
                Ask: instrument.Ask,
                TradeDate: new DateOnly(2026, 10, 2).AddDays(-random.Next(0, 3) * random.Next(0, 10)),
                Status: Statuses[random.Next(Statuses.Length)]);
        }
        return trades;
    }

    /// <summary>A few instruments move: their new quotes.</summary>
    public IReadOnlyList<BlotterInstrument> Move(int instruments)
    {
        var symbols = _instruments.Keys.ToArray();
        var moved = new List<BlotterInstrument>(instruments);
        for (var n = 0; n < instruments; n++)
        {
            var current = _instruments[symbols[_random.Next(symbols.Length)]];
            var mid = (current.Bid + current.Ask) / 2;
            // A step of the random walk, pulled gently back towards yesterday's close.
            var step = (_random.NextDouble() - 0.5) * 2 * current.Volatility - (double)((mid - current.PreviousClose) / current.PreviousClose) * 0.02;
            var next = Math.Round(mid * (1 + (decimal)step), current.Decimals);
            var half = Spread(next, current.Decimals);
            var quoted = current with { Bid = next - half, Ask = next + half };
            _instruments[current.Symbol] = quoted;
            moved.Add(quoted);
        }
        return moved;
    }

    private static decimal Spread(decimal price, int decimals)
    {
        var tick = 1m;
        for (var i = 0; i < decimals; i++)
            tick /= 10;
        return tick * Math.Max(1, Math.Round(price * 0.00005m / tick));
    }
}
