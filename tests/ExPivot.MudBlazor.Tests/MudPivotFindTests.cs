using Bunit;
using ExGrid.Selection;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// Find in ExPivot under <c>MudPivotChrome</c> (ADR-0332 §1 and §4): the report grid's find panel is
/// the grid Wrapper's, and what it finds and marks is the same as under ExPivot's own markup, because
/// swapping the Chrome changes no behaviour (ADR-0061).
/// </summary>
public class MudPivotFindTests : MudPivotTestContext
{
    //  Row 0 East, 1 Apples, 2 Pears, 3 North, 4 Pears, 5 West, 6 Apples, 7 Plums, 8 (blank),
    //  9 Plums, 10 Grand Total; column 1 the Sum of Amount. East collapsed: 0 East, 1 North, 2 Pears, …
    private static readonly PivotLayout EastCollapsed = new()
    {
        Rows = [P("Region") with { ToggledItems = [PivotItemKey.Text("East")] }, P("Product")],
        Values = [Sum("Amount")],
    };

    private static CellPosition FocusOf(IRenderedComponent<PivotComponent> cut)
        => Grid(cut).Instance.ReadSelection().Selection.Focus;

    private static int[] MarkedRows(IRenderedComponent<PivotComponent> cut)
    {
        var rows = Grid(cut).FindAll(".ex-viewport .ex-row");
        return Enumerable.Range(0, rows.Count).Where(r => rows[r].QuerySelector(".ex-match-mark") is not null).ToArray();
    }

    [Fact] // ADR-0332 §1/§4 under MudBlazor: Ctrl+F opens the Wrapper's panel; a step finds by the text shown, outside a collapsed group, and the painted matches are marked
    public async Task ADR0332_find_and_its_marks_under_the_mud_chrome()
    {
        var cut = RenderPivot(EastCollapsed);
        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnKeyAsync("f", true, false, false, false, false));

        Assert.Single(grid.FindAll("form.mud-ex-grid-find"));
        await cut.InvokeAsync(() => Grid(cut).Find(".mud-ex-grid-find-field input").InputAsync(new ChangeEventArgs { Value = "pears" }));
        Assert.Equal([2], MarkedRows(cut));

        await cut.InvokeAsync(() => Grid(cut).Find(".mud-ex-grid-find-next").ClickAsync(new MouseEventArgs()));
        Assert.Equal(new CellPosition(2, 0), FocusOf(cut));
        Assert.NotNull(Grid(cut).FindAll(".ex-viewport .ex-row")[2].QuerySelector(".ex-match-mark-current"));

        // East's Apples are inside the collapsed group: nothing is found, and nothing expands.
        await cut.InvokeAsync(() => Grid(cut).Find(".mud-ex-grid-find-field input").InputAsync(new ChangeEventArgs { Value = "130" }));
        await cut.InvokeAsync(() => Grid(cut).Find(".mud-ex-grid-find-next").ClickAsync(new MouseEventArgs()));
        Assert.Equal("No match", Grid(cut).Find(".mud-ex-grid-find-outcome").TextContent.Trim());
        Assert.True(cut.Instance.CurrentLayout.Rows[0].IsCollapsed(PivotItemKey.Text("East")));
    }
}
