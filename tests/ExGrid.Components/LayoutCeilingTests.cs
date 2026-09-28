using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The grid told a Layout Ceiling under its true height (ADR-0053): the spacer is capped,
/// the rows are read through <c>c(s) = s × k</c>, and every consumer of vertical geometry
/// agrees. A million rows of 20px in a 100px Viewport whose header takes 20: the true
/// height is 20,000,000 px and the ceiling told is half that.
/// </summary>
public class LayoutCeilingTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private const double ReadablePx = ViewportHeightPx - RowHeightPx;
    private const int Total = 1_000_000;
    private const double CeilingPx = 10_000_000;

    // What the geometry is for this grid once told: the rows' room is the ceiling less the band.
    private static readonly ViewportGeometry Told = new(RowHeightPx, ReadablePx, Total, CeilingPx - RowHeightPx);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(TestRow[] window, int windowStart)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.WindowStart, windowStart)
            .Add(g => g.TotalCount, Total)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx));

    private static double TranslateY(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var match = Regex.Match(cut.Find(".ex-viewport").GetAttribute("style")!, @"translateY\((?<px>[-\d.E+]+)px\)");
        Assert.True(match.Success);
        return double.Parse(match.Groups["px"].Value, CultureInfo.InvariantCulture);
    }

    private static double StyleHeight(string style)
        => double.Parse(Regex.Match(style, @"height: (?<px>[-\d.E+]+)px").Groups["px"].Value, CultureInfo.InvariantCulture);

    [Fact] // ADR-0053: until told, and when told the scale-1 ceiling, the spacer is the true height
    public async Task The_scale_one_ceiling_changes_nothing()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);
        Assert.Contains("height: 20000020px", cut.Find(".ex-spacer").GetAttribute("style"));

        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(ViewportGeometry.MaxScrollHeightPx));

        Assert.Contains("height: 20000020px", cut.Find(".ex-spacer").GetAttribute("style"));
        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0053 / VZ-15: a ceiling under the true height caps the spacer, less a margin
    public async Task A_ceiling_under_the_true_height_caps_the_spacer()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);

        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));

        var spacer = StyleHeight(cut.Find(".ex-spacer").GetAttribute("style")!);
        Assert.Equal(RowHeightPx + Told.ScrollHeightPx, spacer);
        Assert.True(spacer <= CeilingPx - ViewportGeometry.LayoutCeilingMarginPx);
    }

    [Fact] // ADR-0053 / BIG-1: the furthest the browser scrolls paints the last row, flush with the bottom
    public async Task The_largest_offset_paints_the_last_row_at_the_bottom()
    {
        // The Window holds the last ten rows; its tenth is row 999,999.
        var cut = RenderGrid(TestRows.Many(10), Total - 10);
        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));

        await ScrollToAsync(cut.Find(".ex-scroller"), Told.MaxScrollTopPx);
        Clock.Advance(TimeSpan.FromSeconds(1)); // the fling settles and the rows are painted

        var rows = cut.FindAll(".ex-row");
        Assert.Equal("Row 000009", rows[^1].QuerySelector(".ex-cell")!.TextContent);
        // Placed from the offset the browser holds: the last row's bottom is the readable bottom.
        var first = Told.SliceAt(Told.MaxScrollTopPx)!.Value.Start;
        Assert.Equal(Told.PaintedTopPxOf(first, Told.MaxScrollTopPx), TranslateY(cut));
        var lastBottom = TranslateY(cut) - Told.MaxScrollTopPx + ((Total - first) * RowHeightPx);
        Assert.Equal(ReadablePx, lastBottom, 6);
    }

    [Fact] // ADR-0053: a compressed grid re-places its rows on a scroll that does not change the slice
    public async Task A_scroll_inside_one_row_re_places_the_rows()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);
        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));
        const double s = 5;

        await ScrollToAsync(cut.Find(".ex-scroller"), s);

        Assert.Equal(0, Told.SliceAt(s)!.Value.Start);
        Assert.Equal(Told.PaintedTopPxOf(0, s), TranslateY(cut));
        Assert.NotEqual(0, TranslateY(cut));
    }

    [Fact] // ADR-0053 / ADR-0012: Ctrl+End asks for the furthest offset the capped spacer allows
    public async Task Ctrl_End_reveals_through_the_mapping()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);
        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("End", true, false, false, false, false));

        Assert.Equal(Told.MaxScrollTopPx, Js.ScrolledTo[^1].Top);
    }

    [Fact] // ADR-0053: an overlay rectangle is clipped to the painted rows — Ctrl+A is not a million rows tall
    public async Task A_whole_result_selection_is_clipped_to_the_painted_rows()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", true, false, false, false, false));

        var range = cut.Find(".ex-selection .ex-range").GetAttribute("style")!;
        // Five painted rows, and one beyond the bottom; nothing above row 0.
        Assert.Contains("top: 0px", range);
        Assert.Equal(6 * RowHeightPx, StyleHeight(range));

        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * RowHeightPx);
        Clock.Advance(TimeSpan.FromSeconds(1));

        range = cut.Find(".ex-selection .ex-range").GetAttribute("style")!;
        // One row above the painted slice and one below it, so no edge is drawn on screen.
        Assert.Contains($"top: {-RowHeightPx}px", range);
        Assert.Equal(7 * RowHeightPx, StyleHeight(range));
    }

    [Fact] // ADR-0053 / ADR-0028: a ceiling that moves keeps the first visible row where it was
    public async Task A_moved_ceiling_keeps_the_first_visible_row()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);
        await ScrollToAsync(cut.Find(".ex-scroller"), 400_000 * RowHeightPx);

        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));

        var anchored = Told.ScrollTopAt(400_000 * RowHeightPx);
        Assert.Equal(anchored, Js.ScrolledTo[^1].Top);
        // The same row is first, read through the new mapping (to the float's last bit,
        // it can land on the row boundary from either side).
        var first = int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-first-row")!, CultureInfo.InvariantCulture);
        Assert.Equal(Told.SliceAt(anchored)!.Value.Start, first);
        Assert.InRange(first, 399_999, 400_000);
    }

    [Fact] // ADR-0053 / ADR-0028: the anchor is written only where the browser still stands where the grid last knew it
    public async Task A_moved_ceiling_writes_its_anchor_only_over_the_offset_the_grid_last_knew()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);
        await ScrollToAsync(cut.Find(".ex-scroller"), 400_000 * RowHeightPx);

        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));

        var (top, from) = Assert.Single(Js.Anchored);
        Assert.Equal(Told.ScrollTopAt(400_000 * RowHeightPx), top);
        Assert.Equal(400_000 * RowHeightPx, from);
    }

    [Fact] // ADR-0053 / BIG-5: a scroll the grid has not heard yet is not overwritten when the ceiling is told
    public async Task A_scroll_made_before_the_ceiling_was_heard_stands()
    {
        // The grid knows the browser at the top. The user has already scrolled to the end, and
        // the scroll event is still on its way when the ceiling arrives (a Server circuit): the
        // anchor keeps row 0, and the browser, no longer at 0, refuses to write it.
        var cut = RenderGrid(TestRows.Many(10), Total - 10);
        Js.RefuseAnchors();
        Js.SetScrollOffset(Told.MaxScrollTopPx, 0);

        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(CeilingPx));
        Clock.Advance(TimeSpan.FromSeconds(1)); // the fling settles and the rows are painted

        // Asked with the offset it assumed; refused; and then the grid reads where the browser is
        // and paints that, the end, rather than the rows it was about to put back.
        Assert.Equal(0, Assert.Single(Js.Anchored).From);
        var first = int.Parse(cut.Find(".ex-viewport").GetAttribute("data-ex-first-row")!, CultureInfo.InvariantCulture);
        Assert.Equal(Told.SliceAt(Told.MaxScrollTopPx)!.Value.Start, first);
        Assert.Equal("Row 000009", cut.FindAll(".ex-row")[^1].QuerySelector(".ex-cell")!.TextContent);
    }

    [Fact] // ADR-0053: a ceiling that is not a number, or not positive, is not believed
    public async Task A_nonsense_ceiling_is_ignored()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);

        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(double.NaN));
        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(0));
        await cut.InvokeAsync(() => cut.Instance.OnLayoutCeilingAsync(-5));

        Assert.Contains("height: 20000020px", cut.Find(".ex-spacer").GetAttribute("style"));
    }

    [Fact] // ADR-0053 / ADR-0021's sixth entry: the probe is rendered once, inline, declared 2^25 px tall
    public void The_ceiling_probe_is_declared_two_to_the_twenty_five_pixels_tall()
    {
        var cut = RenderGrid(TestRows.Many(10), 0);

        var probe = Assert.Single(cut.FindAll(".ex-ceiling-probe > div"));
        Assert.Contains("height: 33554432px", probe.GetAttribute("style"));
        Assert.Equal("true", cut.Find(".ex-ceiling-probe").GetAttribute("aria-hidden"));
    }
}
