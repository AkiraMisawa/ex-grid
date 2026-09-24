using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// PageUp / PageDown (ADR-0012): the Focus and the Viewport move by the same N — the
/// rows fully visible — so the Focus keeps its position on screen. This is the grid's
/// only scroll that is not a reveal, and at the two ends it degrades to exactly a
/// reveal: the scroll clamps, and what survives is that the Focus is still visible.
///
/// 200 rows of 20px in a 120px Viewport: the 20px header leaves 100px, so N = 5.
/// </summary>
public class MoveByViewportTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const int FullyVisibleRows = 5;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Action<GridSelection>? onSelectionChanged = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(3))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, shift, false, false, false));

    /// <summary>Feeds the browser's answer back, the way GoToStartTests does: the grid
    /// asks for a scroll, the browser raises the event, and the offsets come from that.</summary>
    private async Task AnswerLastScrollAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var (top, left) = Js.ScrolledTo[^1];
        await ScrollToAsync(cut.Find(".ex-scroller"), top, left);
    }

    [Fact] // ADR-0012 / KB-13: Focus and Viewport move by the same N, repeatedly
    public async Task Repeated_pagedown_keeps_the_focus_position_on_screen()
    {
        var focusRows = new List<int>();
        var cellCount = 0L;
        var cut = RenderGrid(s =>
        {
            if (!s.IsEmpty)
            {
                focusRows.Add(s.Focus.Row);
                cellCount = s.CellCount;
            }
        });

        await PressAsync(cut, "ArrowDown");                    // places the Focus at row 0
        var scrolls = Js.ScrolledTo.Count;

        await PressAsync(cut, "PageDown");
        Assert.Equal(scrolls + 1, Js.ScrolledTo.Count);
        // The Viewport was at 0, so the step lands at N rows exactly.
        Assert.Equal(FullyVisibleRows * RowHeightPx, Js.ScrolledTo[^1].Top);
        await AnswerLastScrollAsync(cut);

        await PressAsync(cut, "PageDown");
        Assert.Equal(2 * FullyVisibleRows * RowHeightPx, Js.ScrolledTo[^1].Top);
        await AnswerLastScrollAsync(cut);

        await PressAsync(cut, "PageDown");
        Assert.Equal(3 * FullyVisibleRows * RowHeightPx, Js.ScrolledTo[^1].Top);

        // The Focus moved by the same N each press: its offset within the Viewport is
        // identical after presses 2 and 3 (and after every one — it started at the top).
        Assert.Equal([0, 5, 10, 15], focusRows);
        Assert.Equal(1, cellCount);
    }

    [Fact] // ADR-0012 / KB-14: at the bottom the scroll clamps and the transition degrades to a reveal
    public async Task Pagedown_at_the_end_clamps_and_keeps_the_focus_visible()
    {
        var cut = RenderGrid();
        await PressAsync(cut, "ArrowDown");

        // 40 presses of 5 rows would pass row 199; the Focus clamps there.
        for (var i = 0; i < 41; i++)
        {
            await PressAsync(cut, "PageDown");
            if (Js.ScrolledTo.Count > 0)
                await AnswerLastScrollAsync(cut);
        }

        // The last row bottom-aligned in what the header leaves: 200×20 − (120−20).
        var maxScrollTop = (200 * RowHeightPx) - (120 - RowHeightPx);
        Assert.Equal(maxScrollTop, Js.ScrolledTo[^1].Top);
        // And the Focus is inside the visible box: its top offset is within the band.
        var lastRowTop = 199 * RowHeightPx;
        Assert.InRange(lastRowTop - maxScrollTop, 0, 120 - RowHeightPx - RowHeightPx);
    }

    [Fact] // ADR-0012 / KB-13: PageUp is the same step upward
    public async Task Pageup_moves_focus_and_viewport_back_by_the_same_rows()
    {
        var cut = RenderGrid();
        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "PageDown");
        await AnswerLastScrollAsync(cut);
        await PressAsync(cut, "PageDown");
        await AnswerLastScrollAsync(cut);

        await PressAsync(cut, "PageUp");

        Assert.Equal(FullyVisibleRows * RowHeightPx, Js.ScrolledTo[^1].Top);
    }

    [Fact] // ADR-0012: Shift+PageDown extends — the Anchor stays, the range grows by N rows
    public async Task Shift_pagedown_extends_the_selection_by_a_viewport()
    {
        GridSelection? last = null;
        var cut = RenderGrid(s => last = s);
        await PressAsync(cut, "ArrowDown");

        await PressAsync(cut, "PageDown", shift: true);

        Assert.NotNull(last);
        Assert.Equal([new SelectionRange(0, 0, FullyVisibleRows + 1, 1)], last!.Ranges);
        Assert.Equal(new CellPosition(0, 0), last.Anchor);
        Assert.Equal(new CellPosition(FullyVisibleRows, 0), last.Focus);
    }

    [Fact] // ADR-0012: on an empty selection the first press only places the Focus, without moving the Viewport
    public async Task On_an_empty_selection_pagedown_only_places_the_focus()
    {
        GridSelection? last = null;
        var cut = RenderGrid(s => last = s);

        await PressAsync(cut, "PageDown");

        Assert.NotNull(last);
        Assert.Equal(new CellPosition(0, 0), last!.Focus);
        Assert.Empty(Js.ScrolledTo);
    }
}
