using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A Pinned Column stands outside virtualisation and is painted at every offset
/// (ADR-0004). 100 columns of 100px in a 350px Viewport with the first two pinned: the
/// pinned pair covers the Viewport's left 200px, so only 150px is left for the
/// scrollable columns and two of them fit.
/// </summary>
public class PinnedColumnTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private const double ViewportWidthPx = 350;
    private const int RowsPerViewport = 5;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        int pinnedColumnCount = 2,
        GridColumn<TestRow>[]? columns = null,
        bool virtualiseColumns = true)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, columns ?? TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, ViewportWidthPx)
            .Add(g => g.PinnedColumnCount, pinnedColumnCount)
            .Add(g => g.VirtualiseColumns, virtualiseColumns));

    private static string[] PinnedColumnsOf(IRenderedComponent<ExGrid<TestRow>> cut, int row = 0)
        => [.. cut.FindAll(".ex-row")[row].QuerySelectorAll(".ex-cell.ex-pinned")
            .Select(c => TestRows.ColumnOf(c.TextContent))];

    private static string[] ScrollableColumnsOf(IRenderedComponent<ExGrid<TestRow>> cut, int row = 0)
        => [.. cut.FindAll(".ex-row")[row].QuerySelectorAll(".ex-cell:not(.ex-pinned)")
            .Select(c => TestRows.ColumnOf(c.TextContent))];

    [Fact] // ADR-0004: pinned columns are painted, and the Viewport they leave decides the rest
    public void The_pinned_columns_are_painted_beside_what_is_left_of_the_viewport()
    {
        var cut = RenderGrid();

        Assert.Equal(["C00", "C01"], PinnedColumnsOf(cut));
        Assert.Equal(["C02", "C03"], ScrollableColumnsOf(cut));
        Assert.Equal(RowsPerViewport * 2, cut.FindAll(".ex-cell.ex-pinned").Count);
    }

    [Fact] // ADR-0004: pinned columns are the landmark that survives panning — they never leave
    public async Task The_pinned_columns_stay_painted_at_the_far_right()
    {
        var cut = RenderGrid();

        // Two ordinary moves rather than one jump, so this is not a fling.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 300);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 600);

        Assert.Equal(["C00", "C01"], PinnedColumnsOf(cut));
        Assert.Equal(["C08", "C09"], ScrollableColumnsOf(cut));
    }

    [Fact] // ADR-0013: the sticky offset is the cumulative width, computed in C# and emitted inline
    public void A_pinned_cell_carries_its_own_offset_as_the_sticky_left()
    {
        var cut = RenderGrid();

        var pinned = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell.ex-pinned");
        Assert.Contains("left: 0px", pinned[0].GetAttribute("style"));
        Assert.Contains("left: 100px", pinned[1].GetAttribute("style"));
        Assert.Contains("width: 100px", pinned[1].GetAttribute("style"));
    }

    [Fact] // ADR-0004: a sticky cell still occupies the flow, so the spacer measures from where it ends
    public async Task The_spacer_measures_from_the_end_of_the_pinned_block()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 300);

        // The first painted scrollable column is C05, at 500. The pinned pair already
        // occupies the first 200 of the flow, so the spacer covers 200-500. Measuring
        // from the content's edge instead would push every column 200px right.
        Assert.Equal(["C05", "C06"], ScrollableColumnsOf(cut));
        Assert.All(cut.FindAll(".ex-gap"), g => Assert.Contains("width: 300px", g.GetAttribute("style")));
    }

    [Fact] // ADR-0004: a column completely under the pinned block is not painted — nobody could read it
    public async Task A_column_hidden_under_the_pinned_block_is_left_out()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 300);

        // At 300 the Viewport shows content 300-650, but 300-500 is under the pinned
        // pair: C03 and C04 are on screen and invisible, so they stay out of the DOM.
        Assert.DoesNotContain("C03", ScrollableColumnsOf(cut));
        Assert.DoesNotContain("C04", ScrollableColumnsOf(cut));
    }

    [Fact] // A pinned column is painted once, as a pinned cell — never a second time in the slice
    public void A_pinned_column_is_never_painted_twice()
    {
        var cut = RenderGrid();

        Assert.Empty(PinnedColumnsOf(cut).Intersect(ScrollableColumnsOf(cut)));
    }

    [Fact] // Rather than be quietly wrong: the header pins what the rows pin, or the labels slide off
    public void The_header_pins_the_same_columns_as_the_rows()
    {
        var cut = RenderGrid();

        var pinnedHeaders = cut.FindAll(".ex-header-cell.ex-pinned").Select(h => h.TextContent);
        Assert.Equal(["C00", "C01"], pinnedHeaders);
        Assert.Equal(
            [.. PinnedColumnsOf(cut), .. ScrollableColumnsOf(cut)],
            cut.FindAll(".ex-header-cell").Select(h => h.TextContent));
    }

    [Fact] // ADR-0004: pinning costs directly — pin enough and there is nothing left to virtualise
    public void Pinning_more_than_the_viewport_holds_leaves_only_pinned_cells()
    {
        var cut = RenderGrid(pinnedColumnCount: 4);

        Assert.Equal(["C00", "C01", "C02", "C03"], PinnedColumnsOf(cut));
        Assert.Empty(ScrollableColumnsOf(cut));
        Assert.Empty(cut.FindAll(".ex-gap"));
    }

    [Fact] // ADR-0004: a Pinned Column blinking out on every fling is what pinning exists to prevent
    public async Task The_pinned_columns_are_painted_through_a_fling()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 100 * RowHeightPx, left: 0);

        // Placeholders, and yet the landmark is still there: only the scrollable cells
        // are the cost being skipped.
        Assert.Equal(RowsPerViewport, cut.FindAll(".ex-placeholder").Count);
        Assert.Equal(["C00", "C01"], PinnedColumnsOf(cut));
        Assert.Empty(ScrollableColumnsOf(cut));

        Clock.Advance(SettleDelay);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-placeholder")));
        Assert.Equal(["C02", "C03"], ScrollableColumnsOf(cut));
    }

    [Fact] // ADR-0003/0004: a fling still moving sideways repaints nothing — the pinned cells are already right
    public async Task Panning_on_through_a_fling_does_not_repaint_the_rows()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 400);
        var duringFling = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        // Still flinging, so the columns on screen change again — but a Placeholder is
        // handed no slice, so its output cannot have changed and it does not render.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 900);

        Assert.Equal(duringFling, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount));
        Assert.Equal(["C00", "C01"], PinnedColumnsOf(cut));
    }

    [Fact] // ADR-0004: a row with no data behind it has nothing to pin either
    public void A_row_outside_the_window_paints_no_pinned_cells()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(3))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, ViewportWidthPx)
            .Add(g => g.PinnedColumnCount, 2));

        var placeholders = cut.FindAll(".ex-row.ex-placeholder");
        Assert.Equal(2, placeholders.Count);
        Assert.All(placeholders, p => Assert.Empty(p.QuerySelectorAll(".ex-cell")));
    }

    [Fact] // ADR-0004: off, the pinned block is unchanged — the switch is about cell count only
    public void Virtualisation_off_keeps_the_same_pinned_offsets()
    {
        var cut = RenderGrid(virtualiseColumns: false);

        var pinned = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell.ex-pinned");
        Assert.Equal(2, pinned.Length);
        Assert.Contains("left: 0px", pinned[0].GetAttribute("style"));
        Assert.Contains("left: 100px", pinned[1].GetAttribute("style"));
        Assert.Equal(100, cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell").Length);
    }

    [Fact] // Rather than be quietly wrong: pinning columns that are not there is refused, not clamped
    public void A_pinned_count_outside_the_columns_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderGrid(pinnedColumnCount: -1));

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => RenderGrid(pinnedColumnCount: 101));
        Assert.Contains("PinnedColumnCount", ex.Message);
    }

    [Fact] // Nothing pinned is the default, and it paints no pinned cells at all
    public void Nothing_is_pinned_by_default()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, ViewportWidthPx));

        Assert.Empty(cut.FindAll(".ex-pinned"));
    }
}
