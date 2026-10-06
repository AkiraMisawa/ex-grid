using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// A report row's key (ADR-0140; PV-42, PV-43): what the row stands for — its role, its Value Field
/// and its Items — as a value equal across the reports of one layout and distinct within one report,
/// made with the row so that reading it costs nothing on the grid's check of every redraw.
/// </summary>
public class RowKeyTests
{
    private static readonly PivotLayout RegionAndProduct = RowsBy("Region", "Product");

    [Fact] // ADR-0140 / PV-42: no two rows of one report stand for the same thing
    public void A_reports_row_keys_are_distinct()
    {
        var report = Report(RegionAndProduct);

        Assert.Equal(report.Rows.Count, report.Rows.Select(row => row.Key).Distinct().Count());
    }

    [Fact] // ADR-0140 / PV-42: the row standing for the same thing in another report of the layout has an equal key and hash
    public void A_rows_key_equals_the_key_of_the_same_row_in_another_report()
    {
        var first = Report(RegionAndProduct);
        var second = Report(RegionAndProduct, [.. Sales.Select(sale => sale with { Amount = sale.Amount + 1 })]);

        Assert.Equal(first.Rows.Count, second.Rows.Count);
        for (var i = 0; i < first.Rows.Count; i++)
        {
            Assert.NotSame(first.Rows[i].Key, second.Rows[i].Key);
            Assert.Equal(first.Rows[i].Key, second.Rows[i].Key);
            Assert.Equal(first.Rows[i].Key.GetHashCode(), second.Rows[i].Key.GetHashCode());
        }
    }

    [Fact] // ADR-0140 / PV-42: a key names the Items, outermost first, as a refusal shows it
    public void A_key_prints_its_role_value_field_and_items()
    {
        var report = Report(RegionAndProduct);
        var apples = report.Rows.First(row => row.Role == PivotRowRole.Item && row.Labels.Any(l => l.Text == "Apples"));

        Assert.Equal($"Item -1 [{PivotItemKey.Text("East")} / {PivotItemKey.Text("Apples")}]", apples.Key.ToString());
    }

    [Fact] // ADR-0140 / PV-43: reading and hashing every row's key over a report allocates nothing
    public void Reading_every_rows_key_allocates_nothing()
    {
        var report = Report(RegionAndProduct);
        var rows = report.Rows;
        static long Pass(IReadOnlyList<PivotReportRow> rows)
        {
            var hashes = 0L;
            for (var i = 0; i < rows.Count; i++)
                hashes += rows[i].Key.GetHashCode();
            return hashes;
        }
        Pass(rows);

        // The least of several runs: JIT work can land on the measuring thread now and then.
        var least = long.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            Pass(rows);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, least);
    }
}
