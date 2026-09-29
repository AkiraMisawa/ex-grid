using System.Globalization;
using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A reveal paints where it is going (ADR-0012, added after the fifth Windows run): the
/// render that writes a reveal's scroll offset also paints the slice at that offset — the
/// rows the Consumer has, Placeholders for the rest, the Range Request raised as always —
/// and the browser's scroll event, when it arrives, confirms that slice and paints no row.
/// Before, the slice followed the scroll event, and on a circuit a far jump showed a
/// Viewport with no rows for a round trip.
///
/// 20px rows in a 120px Viewport whose header takes the first 20: five rows painted.
/// </summary>
public class RevealPaintsTargetTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ReadablePx = 100;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] window, int total, List<RowRange>? asked = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, window)
              .Add(g => g.TotalCount, total)
              .Add(g => g.Columns, TestRows.Columns())
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 600);
            if (asked is not null)
                ps.Add(g => g.OnRangeNeeded, asked.Add);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

    private static Task ClickFirstCellAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 10, OffsetY = 10 });

    /// <summary>The rows the Viewport paints, by their aria-rowindex (1-based), Placeholders
    /// included.</summary>
    private static int[] PaintedRows(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-viewport [role=row]")
            .Select(row => int.Parse(row.GetAttribute("aria-rowindex")!, CultureInfo.InvariantCulture) - 1)];

    /// <summary>The rows the geometry puts on screen at a scroll offset: the slice a render
    /// at that offset paints, the partly visible last row included.</summary>
    private static int[] SliceAt(double top, int total)
    {
        var slice = new ViewportGeometry(RowHeightPx, ReadablePx, total).SliceAt(top)!.Value;
        return [.. Enumerable.Range(slice.Start, slice.Count)];
    }

    private static string Status(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-status").SingleOrDefault()?.TextContent ?? "";

    private static Dictionary<int, int> RowRenders(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(row => row.Instance.RowIndex, row => row.RenderCount);

    [Fact] // ADR-0012 (2026-09-29): Ctrl+Down past the Window paints the target slice in the render that writes the offset
    public async Task A_far_reveal_paints_the_rows_it_scrolls_to_before_the_scroll_event()
    {
        var cut = RenderGrid(TestRows.Many(200), 200);
        await ClickFirstCellAsync(cut);

        await PressAsync(cut, "ArrowDown", ctrl: true);

        // The last row, bottom-aligned: 200 rows of 20px less the 100px the header leaves.
        var top = (200 * RowHeightPx) - ReadablePx;
        Assert.Equal((top, 0d), Js.ScrolledTo[^1]);
        // No scroll event has been simulated: the slice is the one at the written offset.
        var slice = SliceAt(top, 200);
        Assert.Contains(199, slice);
        Assert.Equal(slice, PaintedRows(cut));
        Assert.Empty(cut.FindAll(".ex-placeholder"));
        Assert.Equal(slice[0].ToString(CultureInfo.InvariantCulture), cut.Find(".ex-viewport").GetAttribute("data-ex-first-row"));
        // The Focus is painted, so the status line has nothing to say about it.
        Assert.DoesNotContain("outside the visible range", Status(cut), StringComparison.Ordinal);
        Assert.EndsWith("r199c0", cut.Find(".ex-grid").GetAttribute("aria-activedescendant"), StringComparison.Ordinal);
    }

    [Fact] // ADR-0012 (2026-09-29) / ADR-0004 / ADR-0001: rows the Consumer does not have are Placeholders in that same render, and the Range Request is raised for them
    public async Task A_reveal_past_the_window_paints_placeholders_and_asks_for_the_rows()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(200), 100_000, asked);
        await ClickFirstCellAsync(cut);

        await PressAsync(cut, "End", ctrl: true);

        var top = (100_000 * RowHeightPx) - ReadablePx;
        Assert.Equal(top, Js.ScrolledTo[^1].Top);
        var slice = SliceAt(top, 100_000);
        Assert.Equal(slice, PaintedRows(cut));
        Assert.Equal(slice.Length, cut.FindAll(".ex-viewport .ex-placeholder").Count);
        // Asked for after that render, for the rows it painted as Placeholders (ADR-0001).
        var range = Assert.Single(asked);
        Assert.Equal(slice[0], range.Start);
        Assert.Equal(99_999, range.Start + range.Count - 1);
        Assert.DoesNotContain("outside the visible range", Status(cut), StringComparison.Ordinal);
    }

    [Fact] // ADR-0012 (2026-09-29) / ADR-0027 RR-1: the scroll event that confirms a reveal re-renders no row
    public async Task The_scroll_event_after_a_reveal_renders_no_row()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(200), 200, asked);
        await ClickFirstCellAsync(cut);
        await PressAsync(cut, "ArrowDown", ctrl: true);
        var (top, left) = Js.ScrolledTo[^1];
        var rows = RowRenders(cut);
        var writes = Js.ScrolledTo.Count;

        await ScrollToAsync(cut.Find(".ex-scroller"), top, left);

        Assert.Equal(rows, RowRenders(cut));
        Assert.Equal(SliceAt(top, 200), PaintedRows(cut));
        Assert.Equal(writes, Js.ScrolledTo.Count);
        Assert.Empty(asked);
    }

    [Fact] // ADR-0012 (2026-09-29): a reveal back into the Window paints the rows it has at once — no fling, no Placeholders
    public async Task A_far_reveal_is_not_a_fling()
    {
        var cut = RenderGrid(TestRows.Many(200), 200);
        await ClickFirstCellAsync(cut);
        await PressAsync(cut, "ArrowDown", ctrl: true);
        await ScrollToAsync(cut.Find(".ex-scroller"), Js.ScrolledTo[^1].Top);

        await PressAsync(cut, "ArrowUp", ctrl: true);

        Assert.Equal((0d, 0d), Js.ScrolledTo[^1]);
        Assert.Equal(SliceAt(0, 200), PaintedRows(cut));
        Assert.Empty(cut.FindAll(".ex-placeholder"));
        // And the echo, a whole Viewport's jump as the browser reports it, still paints no row.
        var rows = RowRenders(cut);
        await ScrollToAsync(cut.Find(".ex-scroller"), 0);
        Assert.Equal(rows, RowRenders(cut));
        Assert.Empty(cut.FindAll(".ex-placeholder"));
    }

    [Fact] // ADR-0012 / ADR-0053: under a Layout Ceiling the slice is read through the mapping at the target offset
    public async Task A_reveal_under_a_layout_ceiling_paints_through_the_mapping()
    {
        const int total = 1_000_000;
        const double ceilingPx = 10_000_000;
        var cut = RenderGrid(TestRows.Many(200), total);
        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(ceilingPx));
        await ClickFirstCellAsync(cut);

        await PressAsync(cut, "End", ctrl: true);

        var told = new ViewportGeometry(RowHeightPx, ReadablePx, total, ceilingPx - RowHeightPx);
        Assert.True(told.IsCompressed);
        var top = Js.ScrolledTo[^1].Top;
        Assert.Equal(told.MaxScrollTopPx, top);
        var slice = told.SliceAt(top)!.Value;
        Assert.Equal(total - 1, slice.Start + slice.Count - 1);
        Assert.Equal(Enumerable.Range(slice.Start, slice.Count), PaintedRows(cut));

        var rows = RowRenders(cut);
        await ScrollToAsync(cut.Find(".ex-scroller"), top);
        Assert.Equal(rows, RowRenders(cut));
    }

    [Fact] // ADR-0012: a move that needs no scroll writes nothing and repaints no row
    public async Task A_reveal_already_in_view_writes_nothing()
    {
        var cut = RenderGrid(TestRows.Many(200), 200);
        await ClickFirstCellAsync(cut);
        var rows = RowRenders(cut);

        await PressAsync(cut, "ArrowDown");

        Assert.Empty(Js.ScrolledTo);
        Assert.Equal(SliceAt(0, 200), PaintedRows(cut));
        Assert.Equal(rows, RowRenders(cut));
    }
}
