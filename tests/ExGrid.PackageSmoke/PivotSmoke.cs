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
/// ExPivot.Engine's README examples, compiled against the packed package as a Consumer takes it
/// (ADR-0042). They are not run: that the package restores and its API compiles is what this
/// checks; what the engine computes is the engine's own suite's.
/// </summary>
internal static class PivotSmoke
{
    internal sealed record Deal(string Id, string Region, string Desk, string Tenor, DateOnly TradeDate, decimal Pnl, double Price);

    // "Declaring the fields: the standard way", "A Snapshot of your own", "Computing a report" and
    // "Live data", as the README writes them.
    public static async Task<string> Standard(IReadOnlyList<Deal> trades, ExGrid.Data.Snapshot snapshot, CancellationToken ct)
    {
        var fields = PivotFields.Of<Deal>()
            .Key("Id", t => t.Id)
            .Text("Region", t => t.Region)
            .Text("Desk", t => t.Desk, itemOrder: ["Rates", "Credit"])
            .Text("Tenor", t => t.Tenor, orderKey: Tenors.Months)
            .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
            .Month("Month", of: "TradeDate")
            .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")
            .Number("Price", t => t.Price);

        var source = PivotSource.From(trades, fields);

        var whole = PivotSource.From(snapshot);
        var named = PivotSource.From(snapshot,
        [
            new PivotField("Desk", PivotFieldType.Text),
            PivotField.DatePartOf("Quarter", column: "TradeDate", PivotDatePart.Quarter),
        ]);

        var layout = new PivotLayout
        {
            Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")],
            Columns = [new PivotFieldPlacement("Month")],
            Values = [new PivotValueField("Pnl", PivotAggregation.Sum)],
        };

        var query = PivotQuery.For(layout);
        var answer = await source.AggregateAsync(query, ct);
        var cube = PivotEngine.Cube(query, answer, source.Fields);
        var report = PivotEngine.Report(cube, layout,
            new PivotOptions { Culture = CultureInfo.GetCultureInfo("en-US") });

        source.Changed += change => _ = change.SourceVersion;
        source.Apply(fields.Batch(added: trades, changed: trades, removedKeys: ["T-1042"]));
        var updated = await source.AggregateAsync(query, ct);

        return report.Rows[0].Labels[0].Text + report.Rows[^1].ValueAt(0)!.Text
            + whole.Fields.Count + named.Fields.Count + updated.SourceVersion;
    }

    private static class Tenors
    {
        // Months to maturity: "1Y6M" is 18, as "18M" is, and the two stand side by side.
        public static IComparable? Months(string tenor)
        {
            if (tenor is "ON" or "TN")
                return tenor == "ON" ? -2m : -1m;
            decimal months = 0, number = 0;
            foreach (var c in tenor)
            {
                if (char.IsAsciiDigit(c))
                {
                    number = (number * 10) + (c - '0');
                    continue;
                }
                var unit = c switch { 'W' => 0.25m, 'M' => 1m, 'Y' => 12m, _ => 0m };
                if (unit == 0 || number == 0)
                    return null;
                months += number * unit;
                number = 0;
            }
            return number == 0 && months > 0 ? months : null;
        }
    }

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
