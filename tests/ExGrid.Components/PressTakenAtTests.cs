using System.Globalization;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press on the rows lands where it was made (ED-31, ADR-0021's note of 2026-10-02). The script
/// tells the core what each press or release on the rows was taken against, just before Blazor
/// dispatches it. That is the offsets the browser gave it, the first painted row, the horizontal
/// scroll, and the row order and layout (columns, row height) the painting render carried. The
/// core resolves the cell against that, not against the slice, scroll and layout it holds when the
/// event arrives: a held press replayed after a key moved the view was re-measured against the new
/// rows. 200 rows of 20px in a 100px Viewport, 100 columns of 100px in 350px.
/// </summary>
public class PressTakenAtTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(int pinned = 0, int rowSequenceVersion = 0)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, pinned)
            .Add(g => g.RowSequenceVersion, rowSequenceVersion));

    private static readonly GridColumn<TestRow>[] Columns = TestRows.Wide(100);

    private static int Attribute(IRenderedComponent<ExGrid<TestRow>> cut, string name)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute(name)!, CultureInfo.InvariantCulture);

    /// <summary>What the script tells the core of a press it saw on the rows as they are painted now.</summary>
    private static Task TellAsync(IRenderedComponent<ExGrid<TestRow>> cut, string kind, double x, double y,
        int? firstRow = null, double scrollLeftPx = 0, int? rowSequence = null, int? layout = null)
    {
        var first = firstRow ?? Attribute(cut, "data-ex-first-row");
        var sequence = rowSequence ?? Attribute(cut, "data-ex-sequence");
        var painted = layout ?? Attribute(cut, "data-ex-layout");
        return cut.InvokeAsync(() => cut.Instance.PressTakenAt(kind, x, y, first, scrollLeftPx, sequence, painted));
    }

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task ReleaseAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });

    private static GridSelection Selection(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Instance.ReadSelection().Selection;

    [Fact] // ED-31, ADR-0021 (2026-10-02): the Viewport names the row order and layout it was painted under, and a new layout gets a new name
    public void The_viewport_names_what_it_was_painted_under()
    {
        var cut = RenderGrid(rowSequenceVersion: 4);
        var layout = Attribute(cut, "data-ex-layout");

        Assert.Equal(0, Attribute(cut, "data-ex-first-row"));
        Assert.Equal(4, Attribute(cut, "data-ex-sequence"));
        cut.Render(ps => ps.Add(g => g.RowHeight, 30d));
        Assert.NotEqual(layout, Attribute(cut, "data-ex-layout"));
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): a press told an earlier slice lands on the row painted there, not on the row under its offsets now
    public async Task A_press_taken_on_an_earlier_slice_lands_on_the_row_painted_there()
    {
        var cut = RenderGrid();
        // Painted from row 0, the press is taken on row 2 (y 50) of column 1 (x 150).
        var paintedFirst = Attribute(cut, "data-ex-first-row");
        // A key held before it moves the view to row 20 before the press is replayed; the replay is
        // measured again against those rows, and its offsets now say row 22.
        await ScrollToAsync(cut.Find(".ex-scroller"), 20 * 20);
        Assert.NotEqual(paintedFirst, Attribute(cut, "data-ex-first-row"));

        await TellAsync(cut, "mousedown", 150, 50, firstRow: paintedFirst);
        await PressAsync(cut, 150, 50);

        Assert.Equal(new CellPosition(2, 1), Selection(cut).Focus);
    }

    [Fact] // ED-31, ADR-0012: a press that lands on a row the view has since left brings it back into view
    public async Task A_press_on_a_row_the_view_has_left_reveals_it()
    {
        var cut = RenderGrid();
        var paintedFirst = Attribute(cut, "data-ex-first-row");
        await ScrollToAsync(cut.Find(".ex-scroller"), 20 * 20);
        var scrolls = Js.ScrolledTo.Count;

        await TellAsync(cut, "mousedown", 150, 50, firstRow: paintedFirst);
        await PressAsync(cut, 150, 50);

        Assert.Equal(new CellPosition(2, 1), Selection(cut).Focus);
        var (top, _) = Assert.Single(Js.ScrolledTo.Skip(scrolls));
        Assert.True(top <= 2 * 20, $"the view was brought back to row 2, not to {top}px");
    }

    [Fact] // ED-31: a press the view has not moved away from scrolls nothing
    public async Task A_press_where_the_view_has_not_moved_scrolls_nothing()
    {
        var cut = RenderGrid();
        var scrolls = Js.ScrolledTo.Count;

        await TellAsync(cut, "mousedown", 150, 90);
        await PressAsync(cut, 150, 90);

        Assert.Equal(new CellPosition(4, 1), Selection(cut).Focus);
        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): the offsets told are the press's, not the replay's
    public async Task The_offsets_told_outrank_the_replays()
    {
        var cut = RenderGrid();

        await TellAsync(cut, "mousedown", 150, 50);
        await PressAsync(cut, 450, 90);

        Assert.Equal(new CellPosition(2, 1), Selection(cut).Focus);
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): the pinned band is resolved against the horizontal scroll at the press
    public async Task The_pinned_band_is_resolved_against_the_scroll_at_the_press()
    {
        var cut = RenderGrid(pinned: 1);
        // Scrolled 500px right at the press: x 550 is 50px into the screen, under the pinned
        // column 0. The core has heard of no scroll, and alone would read x 550 as column 5.
        await TellAsync(cut, "mousedown", 550, 30, scrollLeftPx: 500);
        await PressAsync(cut, 550, 30);

        Assert.Equal(new CellPosition(1, 0), Selection(cut).Focus);
    }

    [Fact] // ED-31, ADR-0011, ADR-0021 (2026-10-02): a press taken under another row order selects nothing
    public async Task A_press_taken_under_another_row_order_selects_nothing()
    {
        var cut = RenderGrid(rowSequenceVersion: 3);
        await TellAsync(cut, "mousedown", 150, 30);
        await PressAsync(cut, 150, 30);
        var before = Selection(cut);

        await TellAsync(cut, "mousedown", 250, 70, rowSequence: 2);
        await PressAsync(cut, 250, 70);

        Assert.Equal(before, Selection(cut));
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): a press is resolved against the column widths and row height it was painted at
    public async Task A_press_is_resolved_against_the_layout_it_was_painted_under()
    {
        var cut = RenderGrid();
        var painted = Attribute(cut, "data-ex-layout");
        // The rows grow to 40px and the columns to 200px before the press is heard.
        cut.Render(ps => ps
            .Add(g => g.RowHeight, 40d)
            .Add(g => g.Columns, TestRows.Wide(100, widthPx: 200)));
        Assert.NotEqual(painted, Attribute(cut, "data-ex-layout"));

        // Taken at x 250, y 70: column 2 and row 3 as painted then.
        await TellAsync(cut, "mousedown", 250, 70, layout: painted);
        await PressAsync(cut, 250, 70);

        Assert.Equal(new CellPosition(3, 2), Selection(cut).Focus);
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): a press taken under columns since reordered selects nothing
    public async Task A_press_taken_under_columns_since_reordered_selects_nothing()
    {
        var cut = RenderGrid();
        await TellAsync(cut, "mousedown", 150, 30);
        await PressAsync(cut, 150, 30);
        var painted = Attribute(cut, "data-ex-layout");
        cut.Render(ps => ps.Add(g => g.Columns, [.. Columns.Reverse()]));
        var before = Selection(cut);

        await TellAsync(cut, "mousedown", 250, 70, layout: painted);
        await PressAsync(cut, 250, 70);

        Assert.Equal(before, Selection(cut));
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): a press taken under a layout the grid no longer keeps selects nothing
    public async Task A_press_taken_under_a_layout_no_longer_kept_selects_nothing()
    {
        var cut = RenderGrid();
        await TellAsync(cut, "mousedown", 150, 30);
        await PressAsync(cut, 150, 30);
        var before = Selection(cut);

        await TellAsync(cut, "mousedown", 250, 70, layout: -1);
        await PressAsync(cut, 250, 70);

        Assert.Equal(before, Selection(cut));
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): a press taken on a first row off the current rows selects nothing
    public async Task A_press_taken_on_rows_no_longer_there_selects_nothing()
    {
        var cut = RenderGrid();
        await TellAsync(cut, "mousedown", 150, 30);
        await PressAsync(cut, 150, 30);
        var before = Selection(cut);

        await TellAsync(cut, "mousedown", 250, 70, firstRow: 5000);
        await PressAsync(cut, 250, 70);

        Assert.Equal(before, Selection(cut));
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): what was told is for the next event of its kind only
    public async Task What_was_told_is_heard_once()
    {
        var cut = RenderGrid();

        await TellAsync(cut, "mousedown", 150, 50);
        await PressAsync(cut, 150, 50);
        await ReleaseAsync(cut, 150, 50);
        await PressAsync(cut, 350, 70);

        Assert.Equal(new CellPosition(3, 3), Selection(cut).Focus);
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): what was told of another kind of event is not this event's
    public async Task What_was_told_of_a_release_is_not_a_press()
    {
        var cut = RenderGrid();

        await TellAsync(cut, "mouseup", 150, 50, firstRow: 100);
        await PressAsync(cut, 350, 70);

        Assert.Equal(new CellPosition(3, 3), Selection(cut).Focus);
    }

    [Fact] // ED-31, ADR-0021 (2026-10-02): a press with nothing told is resolved as it always was (no script, as in these tests)
    public async Task A_press_with_nothing_told_is_resolved_against_the_slice_held()
    {
        var cut = RenderGrid();
        await ScrollToAsync(cut.Find(".ex-scroller"), 20 * 20);
        var first = Attribute(cut, "data-ex-first-row");

        await PressAsync(cut, 150, 50);

        Assert.Equal(new CellPosition(first + 2, 1), Selection(cut).Focus);
    }
}
