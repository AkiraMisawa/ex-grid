namespace PackageSmoke;

public sealed class Trade
{
    public required string Book { get; init; }
    public decimal Notional { get; init; }
    public DateTime TradeDate { get; init; }

    public static IReadOnlyList<Trade> Sample() =>
        Enumerable.Range(0, 200)
            .Select(i => new Trade
            {
                Book = i % 2 == 0 ? "Alpha" : "Beta",
                Notional = 1_000_000m + (i * 12_345.67m),
                TradeDate = new DateTime(2026, 1, 1).AddDays(i),
            })
            .ToArray();
}
