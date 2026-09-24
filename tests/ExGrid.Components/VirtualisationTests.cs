using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Only the Viewport is painted, wherever the Window happens to sit inside the result
/// (ADR-0001/0004). The geometry is 20px rows in a 100px Viewport, of which the header
/// takes the first 20: a Viewport therefore holds five rows — four that fit in the 80px
/// left over plus the one straddling the bottom edge.
/// </summary>
public class VirtualisationTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private const int RowsPerViewport = 5;

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

    [Fact] // ADR-0013: the scrollbar spans every row plus the header; the painted rows are offset into it
    public async Task The_spacer_spans_the_whole_result_and_the_viewport_is_offset_by_the_first_row()
    {
        var cut = RenderGrid(TestRows.Many(100_000), total: 100_000);

        // 100,000 rows of 20px, and one row height for the header standing at the top of
        // the content — which is also why the row offsets below carry no header term.
        Assert.Contains("height: 2000020px", cut.Find(".ex-spacer").GetAttribute("style"));
        Assert.Contains("translateY(0px)", cut.Find(".ex-viewport").GetAttribute("style"));

        // Five rows — one Viewport exactly, so this is ordinary scrolling and the rows
        // are painted for real (a longer jump is a fling; FlingTests covers that).
        await ScrollToAsync(cut.Find(".ex-scroller"), 5 * RowHeightPx);

        Assert.Contains("translateY(100px)", cut.Find(".ex-viewport").GetAttribute("style"));
        Assert.Equal("Row 000005", cut.FindAll(".ex-row")[0].QuerySelector(".ex-cell")!.TextContent);
    }

    [Fact] // ADR-0004 / CONTEXT.md "Placeholder": a row outside the Window is painted, but not with cells
    public void Rows_outside_the_window_are_placeholders_without_cells()
    {
        // The Window holds rows 0-3 of a much longer result, so the Viewport's last row
        // has no data behind it yet.
        var cut = RenderGrid(TestRows.Many(4), total: 1000);

        var rows = cut.FindAll(".ex-row");
        Assert.Equal(RowsPerViewport, rows.Count);
        Assert.Equal(4, cut.FindComponents<ExGridRow<TestRow>>().Count);
        Assert.Collection(rows,
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.DoesNotContain("ex-placeholder", r.ClassList),
            r => Assert.Contains("ex-placeholder", r.ClassList));
        Assert.All(cut.FindAll(".ex-placeholder"), p => Assert.Empty(p.QuerySelectorAll(".ex-cell")));
    }

    [Fact] // ADR-0001: a Window starting past the Viewport paints Placeholders above itself
    public void A_window_starting_below_the_viewport_paints_placeholders_above_it()
    {
        // Rows 3-6 are in hand while the Viewport shows 0-4: the top three are gaps.
        var cut = RenderGrid(TestRows.Many(4), windowStart: 3, total: 1000);

        var rows = cut.FindAll(".ex-row");
        Assert.Equal(3, rows.Take(3).Count(r => r.ClassList.Contains("ex-placeholder")));
        Assert.Equal(2, cut.FindComponents<ExGridRow<TestRow>>().Count);
    }

    [Fact] // ADR-0003: scrolling one row keeps the overlapping rows alive and unrepainted
    public async Task Scrolling_one_row_reuses_the_overlapping_rows()
    {
        var cut = RenderGrid(TestRows.Many(1000), total: 1000);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.Instance).ToArray();

        await ScrollToAsync(cut.Find(".ex-scroller"), RowHeightPx);

        var after = cut.FindComponents<ExGridRow<TestRow>>();
        // The four rows that stayed on screen are the same component instances, and none
        // of them rendered a second time: only the row entering at the bottom mounted.
        Assert.Equal(before.Skip(1), after.Take(4).Select(r => r.Instance));
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

    [Fact] // ADR-0004/0018: one scroll container, sized from C#, with the content as wide as the columns
    public void The_scroller_is_the_viewport_and_the_spacer_is_as_wide_as_the_columns()
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
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, 300d));

        // The header travels inside the one scroller and is held by `position: sticky`,
        // so there is no second container to slide the body out from under it.
        Assert.Contains("width: 300px", cut.Find(".ex-scroller").GetAttribute("style"));
        Assert.Contains("width: 200px", cut.Find(".ex-spacer").GetAttribute("style"));
        Assert.NotNull(cut.Find(".ex-scroller .ex-header"));
    }

    [Fact] // ADR-0013: ViewportWidth is a C# parameter for the same reason the height is
    public void A_non_positive_viewport_width_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, 10)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, 0d)));
    }

    [Fact] // ADR-0013: the header takes the first row height, so a Viewport that short holds no rows at all
    public void A_viewport_no_taller_than_a_row_is_refused()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => RenderGrid(TestRows.Many(10), total: 10, viewportHeight: RowHeightPx));
        Assert.Contains("header", ex.Message);
    }

    [Fact] // ADR-0013/0017: a result too tall for a browser to scroll is refused, not half-shown
    public void A_result_too_tall_to_scroll_is_refused()
    {
        var tooMany = (int)(ViewportGeometry.MaxScrollHeightPx / RowHeightPx) + 1;

        var ex = Assert.Throws<InvalidOperationException>(
            () => RenderGrid(TestRows.Many(10), total: tooMany));
        Assert.Contains("page the result", ex.Message);
    }

    [Fact] // ADR-0013: the header band spends a row of the browser's budget, and the guard counts it
    public void A_result_that_only_fits_without_the_header_is_refused()
    {
        // The rows alone clear the 2^25 px ceiling at both of these counts, so the pure
        // guard lets them through; the spacer is a header taller than the rows, and at
        // the second one that is what the browser clamps away — the last row unreachable,
        // in silence.
        var fits = (int)((ViewportGeometry.MaxScrollHeightPx - RowHeightPx) / RowHeightPx);

        RenderGrid(TestRows.Many(10), total: fits);

        var ex = Assert.Throws<InvalidOperationException>(
            () => RenderGrid(TestRows.Many(10), total: fits + 1));
        Assert.Contains("header", ex.Message);
    }

    [Fact] // ADR-0018: a grid gives back the per-instance handle it took
    public async Task Disposing_releases_the_scroll_handle()
    {
        RenderGrid(TestRows.Many(10), total: 10);

        await DisposeComponentsAsync();

        Js.Dispose.VerifyInvoke("dispose");
    }


    [Fact] // ADR-0013/0015: under a pager only one page ever scrolls — a paged result of
           // any total is the prescribed shape, and refusing it would refuse the
           // Consumer for having done the prescribed thing
    public void A_paged_result_of_any_size_is_not_refused()
    {
        var tooMany = (int)(ViewportGeometry.MaxScrollHeightPx / RowHeightPx) + 1;

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, tooMany)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.PageSize, 100));

        Assert.NotNull(cut.Find(".ex-pager"));
    }
}
