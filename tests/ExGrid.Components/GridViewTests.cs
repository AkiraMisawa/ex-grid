using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A view handed ahead of the Row Sequence Version it is written in (ADR-0331, ADR-0011): a Consumer
/// that shows one set of rows after another in one grid — ExSheet's Sheets — hands each set's
/// Selection and scroll offsets back with the version it shows them under, and the grid takes them in
/// the render that takes the version, in place of the empty Selection a new version leaves.
/// </summary>
public class GridViewTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid() =>
        Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.RowSequenceVersion, 0));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0331, ADR-0011: a view handed ahead is taken with its version: its Selection in place of the empty one, and its scroll offsets written once the render lands
    public async Task A_view_handed_ahead_is_taken_with_its_version()
    {
        var cut = RenderGrid();
        await ScrollToAsync(cut.Find(".ex-scroller"), 400);
        await ClickCellAsync(cut, 150, 30);
        var kept = cut.Instance.ReadView();
        Assert.False(kept.Selection.IsEmpty);
        Assert.Equal(400, kept.ScrollTopPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), 0);

        await cut.InvokeAsync(() => cut.Instance.ShowViewWith(kept with { RowSequenceVersion = 1 }));
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        await WhenIdleAsync();

        var held = cut.Instance.ReadSelection();
        Assert.Equal(1, held.RowSequenceVersion);
        Assert.Equal(kept.Selection.Ranges, held.Selection.Ranges);
        Assert.Equal((400d, 0d), Js.ScrolledTo[^1]);
    }

    [Fact] // ADR-0011: a new version with no view handed ahead leaves the Selection empty, as it always has
    public async Task A_new_version_without_a_view_drops_the_selection()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 30);

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0331: a view written in a version the grid never takes is never shown
    public async Task A_view_of_another_version_is_not_taken()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 30);
        var kept = cut.Instance.ReadView();

        await cut.InvokeAsync(() => cut.Instance.ShowViewWith(kept with { RowSequenceVersion = 3 }));
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 2));

        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0331, principle 1: a view whose Selection lies outside the rows the grid now holds is not taken
    public async Task A_view_outside_the_rows_is_not_taken()
    {
        var cut = RenderGrid();
        await ScrollToAsync(cut.Find(".ex-scroller"), 3000);
        await ClickCellAsync(cut, 150, 30);
        var kept = cut.Instance.ReadView();

        await cut.InvokeAsync(() => cut.Instance.ShowViewWith(kept with { RowSequenceVersion = 1 }));
        cut.Render(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, 10)
            .Add(g => g.RowSequenceVersion, 1));

        Assert.True(cut.Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0331: a view holds a Selection
    public void A_view_without_a_selection_is_refused()
    {
        var cut = RenderGrid();

        Assert.Throws<ArgumentException>(() => cut.Instance.ShowViewWith(default));
    }
}
