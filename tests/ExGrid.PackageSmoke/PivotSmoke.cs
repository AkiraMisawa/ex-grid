using System.Globalization;
using ExPivot.Engine;

namespace PackageSmoke;

/// <summary>A sale, as the ExPivot READMEs pivot it.</summary>
public sealed record Sale(string Region, string Product, DateTime Date, decimal Amount)
{
    public static Sale[] Sample() =>
        Enumerable.Range(0, 120)
            .Select(i => new Sale(
                i % 3 == 0 ? "East" : i % 3 == 1 ? "West" : "North",
                i % 2 == 0 ? "Apples" : "Pears",
                new DateTime(2026, 1, 1).AddDays(i),
                100m + i))
            .ToArray();
}

/// <summary>
/// ExPivot.Engine's README example, compiled against the packed package as a Consumer takes it
/// (ADR-0042). It is not run: that the package restores and its API compiles is what this checks;
/// what the engine computes is the engine's own suite's.
/// </summary>
internal static class PivotSmoke
{
    private sealed record Line(string Region, string Product, decimal Amount);

    public static string Compute()
    {
        var sales = new[]
        {
            new Line("East", "Apples", 100m),
            new Line("East", "Pears", 50m),
            new Line("West", "Apples", 70m),
        };
        PivotField<Line>[] fields =
        [
            new("Region", PivotFieldType.Text, s => s.Region),
            new("Product", PivotFieldType.Text, s => s.Product),
            new("Amount", PivotFieldType.Number, s => s.Amount),
        ];
        var layout = new PivotLayout
        {
            Rows = [new PivotFieldPlacement("Region")],
            Columns = [new PivotFieldPlacement("Product")],
            Values = [new PivotValueField("Amount", PivotAggregation.Sum)],
        };

        var report = PivotEngine.Compute(sales, fields, layout,
            new PivotOptions { Culture = CultureInfo.GetCultureInfo("en-US") });

        return string.Join(",", report.ValueColumns.Select(c => c.Header))
            + report.Rows[0].Labels[0].Text
            + report.Rows[0].ValueAt(0)!.Text
            + report.Rows[^1].ValueAt(2)!.Text
            + PivotLayoutJson.Write(layout);
    }
}
