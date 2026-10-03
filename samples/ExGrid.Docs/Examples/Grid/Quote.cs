namespace ExGrid.Docs.Examples.Grid;

/// <summary>A live quote for an invented instrument.</summary>
public sealed record Quote(string Symbol, string Name, decimal Bid, decimal Ask, decimal Open, long Volume, TimeOnly Time)
{
    /// <summary>The middle of the bid and the ask.</summary>
    public decimal Last => Math.Round((Bid + Ask) / 2, 2);

    /// <summary>The move since the open, as a fraction.</summary>
    public decimal Change => Last / Open - 1;

    private static readonly (string Symbol, string Name, decimal Price)[] Instruments =
    [
        ("ALDR", "Alder Energy", 48.20m), ("BRKW", "Brookwell Holdings", 112.75m), ("CNTR", "Centra Logistics", 23.10m),
        ("DLMR", "Dalmore Foods", 67.45m), ("EVRN", "Everline Telecom", 15.82m), ("FNMR", "Fenmoor Mining", 91.30m),
        ("GLRD", "Glenrod Pharma", 204.60m), ("HRTN", "Harton Steel", 36.95m), ("IVRS", "Iverson Bank", 58.10m),
        ("JSPR", "Jasper Software", 342.15m), ("KNLY", "Kenley Retail", 27.40m), ("LMBK", "Lambeck Insurance", 81.05m),
    ];

    /// <summary>The opening quotes, the same on every call.</summary>
    public static Quote[] Opening()
    {
        var random = new Random(20261003);
        return [.. Instruments.Select(i => new Quote(i.Symbol, i.Name, i.Price - 0.02m, i.Price + 0.02m, i.Price,
            random.Next(50_000, 400_000), new TimeOnly(9, 30)))];
    }

    /// <summary>The next quote: the price moves a little and some volume trades.</summary>
    public Quote Tick(Random random, TimeOnly time)
    {
        var move = Math.Round(Last * (decimal)((random.NextDouble() - 0.5) * 0.006), 2);
        var spread = Math.Round(0.01m + (decimal)random.NextDouble() * 0.04m, 2);
        var mid = Last + move;
        return this with { Bid = mid - spread, Ask = mid + spread, Volume = Volume + random.Next(100, 5_000), Time = time };
    }
}
