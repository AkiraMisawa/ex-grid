using System.Runtime.CompilerServices;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// What a report row is (ADR-0161): it says what it stands for — its role, its Value Field, its
/// Items and its labels — and holds no value and no report. A value cell is asked of a report, which
/// computes it when it is first read and keeps it; so a row can belong to every report it did not
/// change in, without holding any of them alive.
/// </summary>
public class ReportRowTests
{
    private static readonly PivotLayout RegionAndProduct = RowsBy("Region", "Product");

    [Fact] // ADR-0161: a value cell is asked of the report — the same value every time it is read
    public void A_value_is_asked_of_the_report()
    {
        var report = Report(RowsBy("Region"));
        var east = report.Rows[0];

        Assert.Equal("East", east.Labels[0].Text);
        Assert.Equal("180", report.ValueAt(east, 0)!.Text);
        Assert.Same(report.ValueAt(east, 0), report.ValueAt(east, 0));
        Assert.Equal("285", report.ValueAt(report.Rows[^1], 0)!.Text);
    }

    [Fact] // ADR-0161: an empty cell reads as null, and a row that carries no values reads null in every column
    public void An_empty_cell_and_a_row_without_values_read_null()
    {
        var report = Report(new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount")], SubtotalsAtTop = false });
        var east = report.Rows[0];
        var pears = report.Rows.First(row => row.Labels[0].Text == "Pears");

        Assert.False(east.CarriesValues);
        Assert.All(Enumerable.Range(0, report.ValueColumns.Count), column => Assert.Null(report.ValueAt(east, column)));
        Assert.Null(report.ValueAt(pears, 1));
        Assert.Equal("50", report.ValueAt(pears, 0)!.Text);
    }

    [Fact] // ADR-0161/0160: a report row holds no report — the report is collected while one of its rows is still held
    public void A_report_row_holds_no_report()
    {
        var (row, report) = RowOfAReportLetGo();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(report.TryGetTarget(out _), "the report is still alive");
        Assert.Equal("East", row.Labels[0].Text);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (PivotReportRow Row, WeakReference<PivotReport> Report) RowOfAReportLetGo()
    {
        var report = Report(RegionAndProduct);
        var row = report.Rows[0];
        // A value read: the report keeps it, the row does not.
        Assert.Equal("180", report.ValueAt(row, 0)!.Text);
        return (row, new WeakReference<PivotReport>(report));
    }

    [Fact] // ADR-0161 (principle 1): a row of a report laid out from another answer is read by what it stands for here, never as another cell by its place in another cube; a row that stands for nothing here reads nothing
    public void A_row_of_another_answer_is_read_by_what_it_stands_for()
    {
        var report = Report(RowsBy("Region"));
        var other = Report(RowsBy("Region"), [.. Sales.Select(sale => sale with { Amount = sale.Amount + 1 })]);
        var collapsed = Report(RowsBy("Region", "Product") with { Rows = [P("Region") with { Collapsed = true }, P("Product")] });
        var apples = Report(RowsBy("Region", "Product")).Rows.First(row => row.Role == PivotRowRole.Item);

        Assert.Equal("183", other.ValueAt(other.Rows[0], 0)!.Text);
        Assert.Equal("180", report.ValueAt(other.Rows[0], 0)!.Text);
        Assert.Null(collapsed.ValueAt(apples, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.ValueAt(report.Rows[0], 1));
    }

    [Fact] // ADR-0161: a report finds its own row for a key — the row standing for the same thing in another report of the layout — and none for a key it has not
    public void A_report_finds_its_row_for_a_key()
    {
        var report = Report(RegionAndProduct);
        var other = Report(RegionAndProduct, [.. Sales.Select(sale => sale with { Amount = sale.Amount + 1 })]);
        var collapsed = Report(RegionAndProduct with { Rows = [P("Region") with { Collapsed = true }, P("Product")] });

        for (var i = 0; i < report.Rows.Count; i++)
        {
            Assert.Same(report.Rows[i], report.RowFor(report.Rows[i].Key));
            Assert.Same(report.Rows[i], report.RowFor(other.Rows[i].Key));
        }
        var apples = report.Rows.First(row => row.Role == PivotRowRole.Item);
        Assert.Null(collapsed.RowFor(apples.Key));
    }

    [Fact] // ADR-0161/0063: Show Details' question is asked of a row by what it stands for — the row of another report of the layout asks what this report's row asks; a row this report has not is refused
    public void Show_details_asks_by_what_a_row_stands_for()
    {
        var report = Report(RegionAndProduct);
        var other = Report(RegionAndProduct, [.. Sales.Select(sale => sale with { Amount = sale.Amount + 1 })]);
        var collapsed = Report(RegionAndProduct with { Rows = [P("Region") with { Collapsed = true }, P("Product")] });
        var apples = report.Rows.First(row => row.Role == PivotRowRole.Item);

        Assert.Equal(report.DetailsQuery(apples, 0), report.DetailsQuery(other.Rows[report.Rows.ToList().IndexOf(apples)], 0));
        Assert.Throws<ArgumentException>(() => collapsed.DetailsQuery(apples, 0));
    }
}
