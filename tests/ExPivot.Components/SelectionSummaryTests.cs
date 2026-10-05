using Bunit;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// The Selection Summary over a report (ADR-0130, SM-13): ExPivot answers from the cells it lays
/// out, a grand total summed with the rest when it is selected, as Excel sums it.
/// </summary>
public class SelectionSummaryTests : PivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static string Summary(IRenderedComponent<PivotComponent> cut) => cut.Find(".ex-summary").TextContent;

    [Fact] // ADR-0130 / SM-13: the cells as laid out, the grand total among them
    public async Task ADR0130_the_report_cells_are_summed_with_the_grand_total()
    {
        var cut = RenderPivot(RegionAmount);
        // East 180, North 10, West 90, (blank) 5, Grand Total 285.
        Assert.Equal(5, Grid(cut).Instance.Window.Count);
        var version = Grid(cut).Instance.RowSequenceVersion;

        await cut.InvokeAsync(() => Grid(cut).Instance.PlaceSelectionAsync(new SelectionRange(0, 1, 5, 1), new CellPosition(0, 1), version));

        cut.WaitForAssertion(() => Assert.Equal("Average: 114Count: 5Sum: 570", Summary(cut)));
    }

    [Fact] // ADR-0130 / SM-13: subtotals are summed with the leaves when selected, as Excel sums them
    public async Task ADR0130_subtotals_are_summed_with_the_rest()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });
        var rows = Grid(cut).Instance.Window.Count;
        var version = Grid(cut).Instance.RowSequenceVersion;

        await cut.InvokeAsync(() => Grid(cut).Instance.PlaceSelectionAsync(new SelectionRange(0, 1, rows, 1), new CellPosition(0, 1), version));

        // The leaves (285), each Region's subtotal (285 together) and the grand total (285).
        cut.WaitForAssertion(() => Assert.Contains("Sum: 855", Summary(cut)));
        Assert.Contains($"Count: {rows}", Summary(cut));
    }

    [Fact] // ADR-0130 / SM-13: a label is counted and never a number
    public async Task ADR0130_labels_are_counted()
    {
        var cut = RenderPivot(RegionAmount);
        var version = Grid(cut).Instance.RowSequenceVersion;

        await cut.InvokeAsync(() => Grid(cut).Instance.PlaceSelectionAsync(new SelectionRange(0, 0, 2, 2), new CellPosition(0, 0), version));

        cut.WaitForAssertion(() => Assert.Equal("Average: 95Count: 4Sum: 190", Summary(cut)));
    }
}
