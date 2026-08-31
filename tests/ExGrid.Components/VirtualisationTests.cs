using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Only the Viewport is painted, wherever the Window happens to sit inside the result
/// (ADR-0001/0004). The geometry is 20px rows in a 100px Viewport, so a Viewport holds
/// six rows: five that fit plus the one straddling the bottom edge.
/// </summary>
public class VirtualisationTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private const int RowsPerViewport = 6;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] window, int windowStart = 0, int? total = null, double viewportHeight = ViewportHeightPx)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.WindowStart, windowStart)
            .Add(g => g.TotalCount, total)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, viewportHeight));

    [Fact] // ADR-0004: render cost is decided by the Viewport, not by the size of the data
    public void Only_the_viewport_slice_renders_rows_however_large_the_total()
    {
        var cut = RenderGrid(TestRows.Many(500), total: 100_000);

        Assert.Equal(RowsPerViewport, cut.FindComponents<ExGridRow<TestRow>>().Count);
        Assert.Equal(RowsPerViewport, cut.FindAll(".ex-row").Count);
    }

    [Fact] // ADR-0013: the scrollbar spans every row; the painted rows are offset into it
    public async Task The_spacer_spans_the_whole_result_and_the_viewport_is_offset_by_the_first_row()
    {
        var cut = RenderGrid(TestRows.Many(100_000), total: 100_000);

        Assert.Contains("height: 2000000px", cut.Find(".ex-spacer").GetAttribute("style"));
        Assert.Contains("translateY(0px)", cut.Find(".ex-viewport").GetAttribute("style"));

        // Six rows — one Viewport exactly, so this is ordinary scrolling and the rows
        // are painted for real (a longer jump is a fling; FlingTests covers that).
        await ScrollToAsync(cut.Find(".ex-scroller"), 6 * RowHeightPx);

        Assert.Contains("translateY(120px)", cut.Find(".ex-viewport").GetAttribute("style"));
        Assert.Equal("Row 000006", cut.FindAll(".ex-row")[0].QuerySelector(".ex-cell")!.TextContent);
    }

    [Fact] // ADR-0004 / CONTEXT.md "Placeholder": a row outside the Window is painted, but not with cells
    public void Rows_outside_the_window_are_placeholders_without_cells()
    {
        // The Window holds rows 0-3 of a much longer result, so the Viewport's last two
        // rows have no data behind them yet.
        var cut = RenderGrid(TestRows.Many(4), total: 1000);

        var rows = cut.FindAll(".ex-row");
        Assert.Equal(RowsPerViewport, rows.Count);
        Assert.Equal(4, cut.FindComponents<ExGridRow<TestRow>>().Count);
        Assert.Collection(rows,
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.Contains("ex-placeholder", r.ClassList),
            r => Assert.Contains("ex-placeholder", r.ClassList));
        Assert.All(cut.FindAll(".ex-placeholder"), p => Assert.Empty(p.QuerySelectorAll(".ex-cell")));
    }

    [Fact] // ADR-0001: a Window starting past the Viewport paints Placeholders above itself
    public void A_window_starting_below_the_viewport_paints_placeholders_above_it()
    {
        // Rows 3-6 are in hand while the Viewport shows 0-5: the top three are gaps.
        var cut = RenderGrid(TestRows.Many(4), windowStart: 3, total: 1000);

        var rows = cut.FindAll(".ex-row");
        Assert.Equal(3, rows.Take(3).Count(r => r.ClassList.Contains("ex-placeholder")));
        Assert.Equal(3, cut.FindComponents<ExGridRow<TestRow>>().Count);
    }

    [Fact] // ADR-0003: scrolling one row keeps the overlapping rows alive and unrepainted
    public async Task Scrolling_one_row_reuses_the_overlapping_rows()
    {
        var cut = RenderGrid(TestRows.Many(1000), total: 1000);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance).ToArray();

        await ScrollToAsync(cut.Find(".ex-scroller"), RowHeightPx);

        var after = cut.FindComponents<ExGridRow<TestRow>>();
        // The five rows that stayed on screen are the same component instances, and none
        // of them rendered a second time: only the row entering at the bottom mounted.
        Assert.Equal(before.Skip(1), after.Take(5).Select(r => r.Instance));
        Assert.All(after, r => Assert.Equal(1, r.RenderCount));
    }

    [Fact] // ADR-0013: ViewportHeight is a C# parameter, emitted inline like the row height
    public void The_viewport_height_is_emitted_on_the_scroller()
    {
        var cut = RenderGrid(TestRows.Many(10), total: 10, viewportHeight: 240);

        Assert.Contains("height: 240px", cut.Find(".ex-scroller").GetAttribute("style"));
    }

    [Fact] // ADR-0013: a non-positive ViewportHeight is refused, not painted
    public void A_non_positive_viewport_height_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RenderGrid(TestRows.Many(10), total: 10, viewportHeight: 0));
    }

    [Fact] // ADR-0001: a null TotalCount claims the Window is the whole result — a start makes that a lie
    public void A_null_total_with_a_nonzero_window_start_is_refused()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => RenderGrid(TestRows.Many(10), windowStart: 5));
        Assert.Contains("TotalCount", ex.Message);
    }

    [Fact] // Rather than be quietly wrong: a Window cannot hold rows the result does not have
    public void A_window_claiming_rows_past_the_total_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => RenderGrid(TestRows.Many(10), windowStart: 995, total: 1000));
    }

    [Fact] // ADR-0018: header and body are one horizontal unit — the root's scrollbar pans both
    public void The_header_and_the_scroller_are_as_wide_as_the_columns()
    {
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
            new("Amount", ColumnType.Number, r => r.Amount, width: new ColumnWidthSpec(ColumnWidth.Fixed(80))),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, 10)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx));

        // Left to itself the scroller would pan horizontally on its own and slide the
        // body out from under the header; sized to the columns, only the root scrolls.
        Assert.Contains("width: 200px", cut.Find(".ex-header").GetAttribute("style"));
        Assert.Contains("width: 200px", cut.Find(".ex-scroller").GetAttribute("style"));
    }

    [Fact] // ADR-0013/0017: a result too tall for a browser to scroll is refused, not half-shown
    public void A_result_too_tall_to_scroll_is_refused()
    {
        var tooMany = (int)(ViewportGeometry.MaxScrollHeightPx / RowHeightPx) + 1;

        var ex = Assert.Throws<InvalidOperationException>(
            () => RenderGrid(TestRows.Many(10), total: tooMany));
        Assert.Contains("page the result", ex.Message);
    }

    [Fact] // ADR-0018: a grid gives back the per-instance handle it took
    public async Task Disposing_releases_the_scroll_handle()
    {
        RenderGrid(TestRows.Many(10), total: 10);

        await DisposeComponentsAsync();

        Js.Dispose.VerifyInvoke("dispose");
    }
}
