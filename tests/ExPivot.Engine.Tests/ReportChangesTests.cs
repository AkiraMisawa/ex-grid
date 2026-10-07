using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// What changed in a report's painted values since an earlier report of the layout (ADR-0067/0161):
/// what the Change Highlight marks. A value cell whose painted text differs from the cell that
/// stands for the same row and column in the earlier report; every cell of a row or a column the
/// earlier report has not; and the rows and columns that left. Rows are paired by key and columns by
/// name, so a row a sort by value moved is compared with itself; a change the format hides is none.
/// </summary>
public class ReportChangesTests
{
    private static readonly PivotLayout RegionAmount = RowsBy("Region");

    private static Sale[] EastApples(decimal amount) => [Sales[0] with { Amount = amount }, .. Sales[1..]];

    private static string[] Changed(PivotReport report, PivotReportChanges changes)
        => changes.Cells.Select(cell => $"{report.Rows[cell.Row].Labels[0].Text}@{report.ValueColumns[cell.Column].Header}").ToArray();

    [Fact] // ADR-0067/0161: the cells whose painted text changed — the leaf's and every total above it — and no others
    public void The_cells_whose_painted_text_changed()
    {
        var earlier = Report(RegionAmount);
        var later = Report(RegionAmount, EastApples(101));

        var changes = later.ChangesSince(earlier);

        Assert.Equal(["East@Sum of Amount", "Grand Total@Sum of Amount"], Changed(later, changes));
        Assert.Empty(changes.NewRows);
        Assert.Empty(changes.NewColumns);
        Assert.Empty(changes.LeftRows);
        Assert.Empty(changes.LeftColumns);
    }

    [Fact] // ADR-0067: a change the number format hides changes no painted text
    public void A_change_the_format_hides_is_none()
    {
        var layout = RegionAmount with { Values = [Sum("Amount") with { NumberFormat = "0" }] };
        var earlier = Report(layout);

        Assert.Empty(Report(layout, EastApples(100.4m)).ChangesSince(earlier).Cells);
        Assert.Equal(2, Report(layout, EastApples(101m)).ChangesSince(earlier).Cells.Count);
    }

    [Fact] // ADR-0067: a row that appears is new, a row that leaves has left, and the totals they move are changed cells
    public void Rows_that_appear_and_leave()
    {
        var earlier = Report(RegionAmount);
        Sale[] sales = [.. Sales.Where(s => s.Region != "North"), Sales[0] with { Region = "South" }];
        var later = Report(RegionAmount, sales);

        var changes = later.ChangesSince(earlier);

        Assert.Equal(["South"], changes.NewRows.Select(i => later.Rows[i].Labels[0].Text));
        Assert.Equal(["North"], changes.LeftRows.Select(key => earlier.RowFor(key)!.Labels[0].Text));
        Assert.Equal(["Grand Total@Sum of Amount"], Changed(later, changes));
    }

    [Fact] // ADR-0067: a column that appears is new, every cell of it; one that leaves has left
    public void Columns_that_appear_and_leave()
    {
        var layout = RegionAmount with { Columns = [P("Product")] };
        var earlier = Report(layout);
        Sale[] sales = [.. Sales.Where(s => s.Product != "Plums"), Sales[0] with { Product = "Quinces" }];
        var later = Report(layout, sales);

        var changes = later.ChangesSince(earlier);

        Assert.Equal(["Quinces"], changes.NewColumns.Select(c => later.ValueColumns[c].Header));
        Assert.Equal([earlier.ValueColumns.Single(c => c.Header == "Plums").Name], changes.LeftColumns);
        Assert.DoesNotContain(changes.Cells, cell => changes.NewColumns.Contains(cell.Column));
    }

    [Fact] // ADR-0067/0011: rows a sort by value reorders are paired by what they stand for, not where they stand
    public void Rows_reordered_by_value_are_paired_by_key()
    {
        var layout = new PivotLayout { Rows = [P("Region") with { Sort = new PivotSort(PivotSortDirection.Descending, 0) }], Values = [Sum("Amount")] };
        var earlier = Report(layout);
        var later = Report(layout, [.. Sales[..3], Sales[3] with { Amount = 200m }, .. Sales[4..]]);

        var changes = later.ChangesSince(earlier);

        Assert.Equal("West", later.Rows[0].Labels[0].Text);
        Assert.Equal(["West@Sum of Amount", "Grand Total@Sum of Amount"], Changed(later, changes));
        Assert.Empty(changes.NewRows);
    }

    [Fact] // ADR-0067: a report compared with itself, or with one of the same data, has nothing changed
    public void The_same_data_changes_nothing()
    {
        var earlier = Report(new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount"), Sum("Quantity")] });

        var changes = earlier.ChangesSince(earlier);

        Assert.True(changes.IsEmpty);
        Assert.True(Report(earlier.Layout).ChangesSince(earlier).IsEmpty);
    }

    [Fact] // ADR-0161 (PV-40): the comparison is made in slices for a large report, with the same result as at once
    public async Task The_comparison_in_slices_is_the_comparison()
    {
        var many = Enumerable.Range(0, 3_000).Select(i => new Sale("R" + i.ToString("0000", System.Globalization.CultureInfo.InvariantCulture), "P", new DateTime(2026, 1, 1), i, 1, true)).ToArray();
        var earlier = Report(RegionAmount, many);
        var later = Report(RegionAmount, [.. many[..10], many[10] with { Amount = -1 }, .. many[11..]]);
        var yields = 0;
        var slicing = new PivotSlicing { Budget = TimeSpan.Zero, UnitsPerCheck = 64, Yield = _ => { yields++; return ValueTask.CompletedTask; } };

        var sliced = await later.ChangesSinceAsync(earlier, slicing, TestContext.Current.CancellationToken);

        Assert.True(yields > 0);
        Assert.Equal(Changed(later, later.ChangesSince(earlier)), Changed(later, sliced));
        Assert.Equal(["R0010@Sum of Amount", "Grand Total@Sum of Amount"], Changed(later, sliced));
    }
}
