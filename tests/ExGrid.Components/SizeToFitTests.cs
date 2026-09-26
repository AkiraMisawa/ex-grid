using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Size to fit on a double-click, and what a fit counts (ADR-0016, decided 2026-09-25):
/// the grip's double-click fits over the whole Window, bounded by MaxWidth; a press
/// without movement reports nothing; whole-column ranges resize together; and a header
/// is charged its label, an em of slack, the menu band and the sort indicator.
/// 6 columns of 100px, 350px viewport, 20px rows: ten rows are painted out of fifty.
/// </summary>
public class SizeToFitTests : GridTestContext
{
    private static readonly GridMetrics Metrics = GridMetrics.Resolve(GridDensity.Compact);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<ColumnWidthChange> changes,
        TestRow[]? rows = null,
        GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows ?? TestRows.Many(50))
            .Add(g => g.TotalCount, (rows ?? TestRows.Many(50)).Length)
            .Add(g => g.Columns, columns ?? TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnColumnWidthChanged, changes.Add));

    private static TestRow[] RowsWithALongBookAt(int row, int length)
    {
        var rows = TestRows.Many(50);
        rows[row].Book = new string('W', length);
        return rows;
    }

    private static Task DoubleClickGrip(IRenderedComponent<ExGrid<TestRow>> cut, int column)
        => cut.FindAll(".ex-resize-grip")[column].DoubleClickAsync(new MouseEventArgs { Button = 0 });

    private static async Task DragGrip(IRenderedComponent<ExGrid<TestRow>> cut, int column, double fromX, double toX)
    {
        await cut.FindAll(".ex-resize-grip")[column].MouseDownAsync(new MouseEventArgs { Button = 0, ClientX = fromX });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = toX });
    }

    [Fact] // ADR-0016 / FN-12a: a double-click fits a value in the Window that is not painted
    public async Task A_double_click_fits_over_the_whole_window_not_the_painted_rows()
    {
        var changes = new List<ColumnWidthChange>();
        var rows = RowsWithALongBookAt(40, 30);
        var cut = RenderGrid(changes, rows);
        Assert.DoesNotContain(cut.FindAll(".ex-cell"), c => c.TextContent.StartsWith("WWW", StringComparison.Ordinal));

        await DoubleClickGrip(cut, 1);

        var expected = Metrics.CellMetrics.EstimatePx(rows[40].Book + "/01");
        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(1), expected)], changes);
    }

    [Fact] // ADR-0016 / FN-12a: the fit is bounded by MaxWidth, even for a column dragged past it
    public async Task A_double_click_fit_is_bounded_by_max_width()
    {
        var changes = new List<ColumnWidthChange>();
        var columns = TestRows.Wide(6);
        columns[1] = new GridColumn<TestRow>(columns[1].Name, columns[1].Type, columns[1].Value,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(600)));
        var cut = RenderGrid(changes, RowsWithALongBookAt(3, 60), columns);

        await DoubleClickGrip(cut, 1);

        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(1), ColumnWidthSpec.DefaultMaxWidthPx)], changes);
    }

    [Fact] // ADR-0016 / FN-12d: when the header is the widest thing, the fit is the header's full need
    public async Task A_double_click_fits_a_header_with_its_menu_band_sort_room_and_slack()
    {
        var changes = new List<ColumnWidthChange>();
        var columns = new GridColumn<TestRow>[]
        {
            new("評価額評価額評価額", ColumnType.Number, r => r.Amount % 10),
        };
        var cut = RenderGrid(changes, columns: columns);

        await DoubleClickGrip(cut, 0);

        Assert.Equal(
            [new ColumnWidthChange("評価額評価額評価額", Metrics.HeaderRequiredPx("評価額評価額評価額", menuButton: true, sortable: true))],
            changes);
    }

    [Fact] // ADR-0016 / FN-12d: an Auto column's header is charged the same as a fit charges it
    public void An_auto_columns_header_counts_its_menu_band_sort_room_and_slack()
    {
        var columns = new GridColumn<TestRow>[]
        {
            new("Amount", ColumnType.Number, r => r.Amount % 10),
        };
        var cut = RenderGrid([], columns: columns);

        var expected = Metrics.HeaderRequiredPx("Amount", menuButton: true, sortable: true);
        Assert.Contains(FormattableString.Invariant($"width: {expected}px"),
            cut.Find(".ex-header-cell").GetAttribute("style"));
    }

    [Fact] // ADR-0016 / FN-12b: a press and release on the grip without movement reports nothing
    public async Task A_click_on_the_grip_reports_no_width()
    {
        var changes = new List<ColumnWidthChange>();
        var cut = RenderGrid(changes);

        await DragGrip(cut, 1, 200, 200);
        await DragGrip(cut, 1, 200, 204); // the reorder gesture's threshold: 4px is still a press
        await DragGrip(cut, 1, 200, 196);

        Assert.Empty(changes);

        await DragGrip(cut, 1, 200, 205);
        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(1), 105)], changes);
    }

    [Fact] // ADR-0016 / FN-12b: a double-click's two presses leave nothing behind but the fit
    public async Task A_double_click_reports_only_the_fit()
    {
        var changes = new List<ColumnWidthChange>();
        var cut = RenderGrid(changes);

        await DragGrip(cut, 1, 200, 200);
        await DragGrip(cut, 1, 200, 200);
        await DoubleClickGrip(cut, 1);

        Assert.Single(changes);
    }

    private static async Task SelectWholeColumns(IRenderedComponent<ExGrid<TestRow>> cut, int from, int to)
    {
        await cut.FindAll(".ex-cell")[0].MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, OffsetX = (from * 100) + 50, OffsetY = 5 });
        await cut.FindAll(".ex-cell")[0].MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, ShiftKey = true, OffsetX = (to * 100) + 50, OffsetY = 5 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", true, false, false, false, false));
    }

    [Fact] // ADR-0016 / FN-12c: a drag on one of several whole columns gives them all its width
    public async Task A_drag_on_a_whole_column_selection_resizes_every_whole_column()
    {
        var changes = new List<ColumnWidthChange>();
        var cut = RenderGrid(changes);
        await SelectWholeColumns(cut, 1, 2);

        await DragGrip(cut, 2, 300, 340);

        Assert.Equal(
            [new ColumnWidthChange(TestRows.ColumnName(1), 140), new ColumnWidthChange(TestRows.ColumnName(2), 140)],
            changes);
    }

    [Fact] // ADR-0016 / FN-12c: a double-click on one of several whole columns fits each to its own content
    public async Task A_double_click_on_a_whole_column_selection_fits_each_column()
    {
        var changes = new List<ColumnWidthChange>();
        var rows = RowsWithALongBookAt(40, 20);
        var cut = RenderGrid(changes, rows);
        await SelectWholeColumns(cut, 1, 2);

        await DoubleClickGrip(cut, 1);

        Assert.Equal(
        [
            new ColumnWidthChange(TestRows.ColumnName(1), Metrics.CellMetrics.EstimatePx(rows[40].Book + "/01")),
            new ColumnWidthChange(TestRows.ColumnName(2), Metrics.CellMetrics.EstimatePx(rows[40].Book + "/02")),
        ], changes);
    }

    [Fact] // ADR-0016 / FN-12c: cells that do not span every row are not columns — only the grabbed one moves
    public async Task A_range_short_of_every_row_resizes_only_the_grabbed_column()
    {
        var changes = new List<ColumnWidthChange>();
        var cut = RenderGrid(changes);
        await cut.FindAll(".ex-cell")[0].MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 5 });
        await cut.FindAll(".ex-cell")[0].MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, ShiftKey = true, OffsetX = 250, OffsetY = 5 });

        await DragGrip(cut, 2, 300, 340);

        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(2), 140)], changes);
    }

    [Fact] // ADR-0016 / FN-12c: a whole-column selection elsewhere leaves a grip outside it alone
    public async Task A_grip_outside_the_whole_columns_resizes_only_itself()
    {
        var changes = new List<ColumnWidthChange>();
        var cut = RenderGrid(changes);
        await SelectWholeColumns(cut, 1, 2);

        await DragGrip(cut, 0, 100, 120);

        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(0), 120)], changes);
    }
}
