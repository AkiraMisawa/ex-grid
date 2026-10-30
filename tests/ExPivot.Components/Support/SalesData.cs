using ExPivot.Engine;

namespace ExPivot.Components.Tests.Support;

/// <summary>A sale, the Source Record every test pivots.</summary>
public sealed record Sale(string? Region, string Product, decimal Amount, int Quantity, bool Online);

/// <summary>The sales every test pivots, their fields, and the bundled source over them.</summary>
public static class SalesData
{
    //  Region  Product  Amount  Quantity  Online
    //  East    Apples   100     10        TRUE
    //  East    Pears    50      5         FALSE
    //  East    Apples   30      3         TRUE
    //  West    Apples   70      7         FALSE
    //  West    Plums    20      2         TRUE
    //  North   Pears    10      1         FALSE
    //  (blank) Plums    5       1         TRUE
    public static readonly Sale[] Records =
    [
        new("East", "Apples", 100m, 10, true),
        new("East", "Pears", 50m, 5, false),
        new("East", "Apples", 30m, 3, true),
        new("West", "Apples", 70m, 7, false),
        new("West", "Plums", 20m, 2, true),
        new("North", "Pears", 10m, 1, false),
        new(null, "Plums", 5m, 1, true),
    ];

    public static readonly PivotField<Sale>[] Fields =
    [
        new("Region", PivotFieldType.Text, s => s.Region),
        new("Product", PivotFieldType.Text, s => s.Product),
        new("Amount", PivotFieldType.Number, s => s.Amount),
        new("Quantity", PivotFieldType.Number, s => s.Quantity),
        new("Online", PivotFieldType.Boolean, s => s.Online),
    ];

    /// <summary>The bundled source over the sales — the reference, answering in the process.</summary>
    public static PivotSource Bundled(IReadOnlyList<Sale>? records = null) => PivotSource.From(records ?? Records, Fields);
}
