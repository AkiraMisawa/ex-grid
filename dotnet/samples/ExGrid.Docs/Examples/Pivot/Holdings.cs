using ExPivot.Engine;

namespace ExGrid.Docs.Examples.Pivot;

/// <summary>One invented holding at a custodian: a market value in the tens of billions, to the cent.</summary>
public sealed record Holding(long Id, string Custodian, string AssetClass, decimal MarketValue);

/// <summary>The holdings, and the same market value declared twice: exactly, and as a double.</summary>
public static class Holdings
{
    public static readonly PivotFields<Holding> Fields = PivotFields.Of<Holding>()
        .Key("Id", h => h.Id)
        .Text("Custodian", h => h.Custodian)
        .Text("AssetClass", h => h.AssetClass, caption: "Asset class")
        .Number("MarketValue", h => h.MarketValue, caption: "Market value")                     // decimal: exact
        .Number("MarketValueDouble", h => (double)h.MarketValue, caption: "Market value (double)"); // double: binary

    private static readonly string[] Custodians = ["Harbor Trust", "Northgate Custody", "Pacific Clearing"];
    private static readonly string[] AssetClasses = ["Government bonds", "Corporate bonds", "Equities", "Cash"];

    /// <summary>5,000 holdings from a fixed seed.</summary>
    public static Holding[] Sample()
    {
        var random = new Random(20261003);
        return
        [
            .. Enumerable.Range(0, 5_000).Select(i => new Holding(
                Id: i + 1,
                Custodian: Custodians[random.Next(Custodians.Length)],
                AssetClass: AssetClasses[random.Next(AssetClasses.Length)],
                MarketValue: random.Next(10_000, 90_000) * 1_000_000m + random.Next(0, 100_000_000) / 100m)),
        ];
    }
}
