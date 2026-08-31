using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Columns outside the Viewport are not in the DOM (ADR-0004). 100 columns of 100px in a
/// 350px Viewport, so four are painted: three that fit plus the one straddling the right
/// edge — the same rule the vertical axis follows, and the arithmetic behind it is
/// pinned in ColumnGeometryTests.
/// </summary>
public class HorizontalVirtualisationTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private const double ViewportWidthPx = 350;
    private const int RowsPerViewport = 5;
    private const int ColumnsPerViewport = 4;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridColumn<TestRow>[]? columns = null,
        bool virtualiseColumns = true,
        int pinnedColumnCount = 0)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, columns ?? TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, ViewportWidthPx)
            .Add(g => g.PinnedColumnCount, pinnedColumnCount)
            .Add(g => g.VirtualiseColumns, virtualiseColumns));

    [Fact] // ADR-0003: an unmoved layout keeps its instance, so the rows keep skipping — whatever the widths are
    public void A_fractional_column_width_does_not_churn_the_rows()
    {
        // The widths are only resolved once, so nothing here should read as movement.
        // Recovering a width from the offsets would say otherwise for half the columns,
        // rebuild the styles, and hand every row a new reference to compare against.
        // 120.3, not 120.5: a width that is exactly representable survives the subtraction
        // too, and would prove nothing.
        var cut = RenderGrid(columns: TestRows.Wide(100, widthPx: 120.3));
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        cut.Render(ps => ps.Add(g => g.IsLoading, true));

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount));
    }

    private static string[] PaintedColumns(IRenderedComponent<ExGrid<TestRow>> cut, int row = 0)
        => [.. cut.FindAll(".ex-row")[row].QuerySelectorAll(".ex-cell").Select(c => TestRows.ColumnOf(c.TextContent))];

    private static string[] HeaderColumns(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-header-cell").Select(h => h.TextContent)];

    private static double GapWidth(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var gaps = cut.FindAll(".ex-gap");
        if (gaps.Count == 0)
            return 0;
        var match = System.Text.RegularExpressions.Regex.Match(gaps[0].GetAttribute("style")!, @"width: ([0-9.]+)px");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact] // ADR-0004: the cells in the DOM are decided by the Viewport, not by how many columns exist
    public void Only_the_columns_the_viewport_reaches_are_painted()
    {
        var cut = RenderGrid();

        Assert.Equal(RowsPerViewport, cut.FindAll(".ex-row").Count);
        Assert.All(cut.FindAll(".ex-row"),
            r => Assert.Equal(ColumnsPerViewport, r.QuerySelectorAll(".ex-cell").Length));
        Assert.Equal(RowsPerViewport * ColumnsPerViewport, cut.FindAll(".ex-cell").Count);
    }

    [Fact] // ADR-0004: the header is virtualised too — 100 resident header cells would be the same cost
    public void The_header_paints_only_the_columns_the_rows_paint()
    {
        var cut = RenderGrid();

        Assert.Equal(ColumnsPerViewport, cut.FindAll(".ex-header-cell").Count);
        Assert.Equal(PaintedColumns(cut), HeaderColumns(cut));
    }

    [Fact] // Rather than be quietly wrong: a column read under its neighbour's label is the failure to avoid
    public async Task The_header_and_the_rows_keep_the_same_column_set_while_panning()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 250);

        Assert.Equal(["C02", "C03", "C04", "C05"], HeaderColumns(cut));
        Assert.Equal(HeaderColumns(cut), PaintedColumns(cut));
    }

    [Fact] // ADR-0004: what is left out to the left is one spacer, not one element per column
    public async Task The_spacer_stands_in_for_every_column_left_out_to_the_left()
    {
        var cut = RenderGrid();

        Assert.Empty(cut.FindAll(".ex-gap"));

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 250);

        // Columns 0 and 1 are off screen, so the first painted column starts at 200 —
        // and every row carries the same spacer as the header, or the two would drift
        // apart and each column would sit under its neighbour's label.
        Assert.Equal(200, GapWidth(cut));
        var gaps = cut.FindAll(".ex-gap");
        Assert.Equal(RowsPerViewport + 1, gaps.Count);
        Assert.All(gaps, g => Assert.Contains("width: 200px", g.GetAttribute("style")));
    }

    [Fact] // ADR-0004: off, every column is painted and there is nothing to stand in for
    public void Virtualisation_off_paints_every_column_and_no_spacer()
    {
        var cut = RenderGrid(virtualiseColumns: false);

        Assert.All(cut.FindAll(".ex-row"), r => Assert.Equal(100, r.QuerySelectorAll(".ex-cell").Length));
        Assert.Equal(100, cut.FindAll(".ex-header-cell").Count);
        Assert.Empty(cut.FindAll(".ex-gap"));
    }

    [Fact] // ADR-0004: the switch changes how many cells exist, never what one says
    public async Task Turning_virtualisation_off_does_not_change_what_the_visible_columns_say()
    {
        var columns = TestRows.Wide(100);
        var cut = RenderGrid(columns);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 250);
        var virtualised = PaintedColumns(cut);
        var text = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")[0].TextContent;

        cut.Render(ps => ps.Add(g => g.VirtualiseColumns, false));

        var all = PaintedColumns(cut);
        Assert.Equal(virtualised, all.Skip(2).Take(virtualised.Length));
        Assert.Equal(text, cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")[2].TextContent);
    }

    [Fact] // ADR-0016: an Auto width must not depend on where the Viewport sits, or it feeds itself
    public async Task Panning_sideways_does_not_move_an_auto_width()
    {
        // Auto columns, so a width that were measured only from the painted columns
        // would grow as one scrolled into view — changing the total width, changing the
        // scrollbar, and making the same scrollLeft point somewhere else. Every column
        // is observed instead, which takes the offset out of the input.
        var columns = new GridColumn<TestRow>[20];
        for (var i = 0; i < columns.Length; i++)
        {
            var index = i;
            columns[i] = new GridColumn<TestRow>(TestRows.ColumnName(index), ColumnType.Text,
                r => index % 3 == 0 ? $"{r.Book} a much longer value {index}" : $"{index}");
        }
        var cut = RenderGrid(columns);
        var totalWidth = cut.Find(".ex-spacer").GetAttribute("style");

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 300);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 600);

        Assert.Equal(totalWidth, cut.Find(".ex-spacer").GetAttribute("style"));
    }

    [Fact] // ADR-0016: a column entering from the right was already measured, so it does not jump
    public async Task A_column_arriving_from_the_right_is_already_at_its_observed_width()
    {
        GridColumn<TestRow>[] columns =
        [
            new(TestRows.ColumnName(0), ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(300))),
            new(TestRows.ColumnName(1), ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(300))),
            // Off screen at rest, and Auto: unobserved it would stand at MinWidth and
            // grow the instant it was painted.
            new(TestRows.ColumnName(2), ColumnType.Text, _ => "a value far wider than the minimum"),
        ];
        var cut = RenderGrid(columns);
        var spacer = cut.Find(".ex-spacer").GetAttribute("style");

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 300);

        Assert.Contains("C02", cut.Markup);
        Assert.Equal(spacer, cut.Find(".ex-spacer").GetAttribute("style"));
    }

    [Fact] // ADR-0021: both axes come back in one read, so the rows and the columns are one moment
    public async Task A_diagonal_scroll_reads_both_axes_in_a_single_call()
    {
        var cut = RenderGrid();
        var before = Js.OffsetReads;

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 2 * RowHeightPx, left: 300);

        // One read, and both axes moved by it: two reads would mean the rows were
        // painted from one instant's offset and the columns from another's.
        Assert.Equal(before + 1, Js.OffsetReads);
        Assert.Equal("Row 000002/03", cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")[0].TextContent);
    }

    [Fact] // ADR-0004: a fling on either axis is a fling — a sideways one moves every cell just the same
    public async Task Crossing_a_viewport_sideways_is_a_fling_even_when_the_rows_barely_move()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 400);

        Assert.Equal(RowsPerViewport, cut.FindAll(".ex-placeholder").Count);
        Assert.Empty(cut.FindAll(".ex-cell"));

        Clock.Advance(SettleDelay);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-placeholder")));
        Assert.Equal(["C04", "C05", "C06", "C07"], HeaderColumns(cut));
    }

    [Fact] // ADR-0004: a diagonal move inside one Viewport on both axes is ordinary scrolling
    public async Task A_diagonal_move_within_one_viewport_paints_for_real()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 2 * RowHeightPx, left: ViewportWidthPx);

        Assert.Empty(cut.FindAll(".ex-placeholder"));
        Assert.Equal(RowsPerViewport * ColumnsPerViewport, cut.FindAll(".ex-cell").Count);
    }

    [Fact] // Negative offsets are a browser artefact (rubber-band scrolling), not a position
    public async Task A_negative_offset_on_either_axis_reads_as_the_top_left()
    {
        var cut = RenderGrid();
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 3 * RowHeightPx, left: 250);

        await ScrollToAsync(cut.Find(".ex-scroller"), top: -40, left: -80);

        Assert.Equal(["C00", "C01", "C02", "C03"], HeaderColumns(cut));
        Assert.Equal("Row 000000/00", cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")[0].TextContent);
    }
}
