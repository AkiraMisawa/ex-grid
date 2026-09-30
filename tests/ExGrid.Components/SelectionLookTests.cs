using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Excel's look for the Focus and a single range (ADR-0008, 2026-09-29; UX-19): which
/// rectangles are painted, the hole the Focus makes in the tint, and which range carries an
/// outline. What the outline looks like is the stylesheet's; that it is drawn around the right
/// rectangle, and that the hole lies where the Focus is, is geometry and is pinned here.
///
/// The grid is <see cref="SelectionGridContext"/>'s, as <see cref="SelectionTests"/>' is.
/// </summary>
public class SelectionLookTests : SelectionGridContext
{
    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    /// <summary>The hole a range is painted with, in the range's own box.</summary>
    private static string Hole(double left, double top, double right, double bottom)
        => FormattableString.Invariant(
            $"; --ex-range-hole: polygon(evenodd, 0 0, 100% 0, 100% 100%, 0 100%, 0 0, {left}px {top}px, {right}px {top}px, {right}px {bottom}px, {left}px {bottom}px, {left}px {top}px)");

    private static (string Class, string Style)[] Ranges(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} > .ex-range").Select(r => (r.GetAttribute("class")!, r.GetAttribute("style")!))];

    [Fact] // ADR-0008 (2026-09-29) / UX-19: a one-cell selection is the Focus outline alone, with no selection tint
    public async Task A_one_cell_selection_is_the_focus_outline_alone()
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, 1, 1);

        Assert.Empty(cut.FindAll(".ex-range"));
        Assert.Equal(Rect(100, 20, 100, 20), cut.Find(".ex-focus").GetAttribute("style"));
    }

    [Fact] // ADR-0008 (2026-09-29) / UX-19: a single range is one rectangle, outlined, with a hole where the Focus is
    public async Task A_single_range_is_one_outlined_rectangle_with_a_hole_at_the_focus()
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, 1, 1);
        await PressCellAsync(cut, 3, 3, shift: true);

        // One element for the whole range, whatever its size (ADR-0008's economy): the Focus
        // is its top-left cell, so the hole is the box's first 100 x 20.
        Assert.Equal([("ex-range ex-range-single", Rect(100, 20, 300, 60) + Hole(0, 0, 100, 20))], Ranges(cut));
        // The Focus keeps its element: forced colors discard the tint that marks it, and it is
        // outlined there (the stylesheet's half, UX-19 in layer 3).
        Assert.Equal(Rect(100, 20, 100, 20), cut.Find(".ex-focus").GetAttribute("style"));
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0052: the hole goes where the Focus goes as Enter and Tab cycle it
    public async Task The_hole_follows_the_focus_round_the_range()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 2, 2, shift: true);

        await KeyAsync(cut, "Enter");                        // (1, 0)
        Assert.Equal(Rect(0, 0, 300, 60) + Hole(0, 20, 100, 40), Assert.Single(Ranges(cut)).Style);

        await KeyAsync(cut, "Tab");                          // (1, 1): inside, touching no edge
        Assert.Equal(Rect(0, 0, 300, 60) + Hole(100, 20, 200, 40), Assert.Single(Ranges(cut)).Style);
    }

    [Fact] // ADR-0008 (2026-09-29) / UX-19: several ranges are each tinted with no outline, and their tint never covers the Focus cell
    public async Task Several_ranges_are_tinted_without_an_outline_and_their_tint_never_covers_the_focus()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 1, 1, shift: true);

        // Ctrl+click adds a range of one cell, which is the Focus: the selection's tint covers
        // none of it, so it is not painted.
        await PressCellAsync(cut, 3, 2, ctrl: true);
        Assert.Equal([("ex-range", Rect(0, 0, 200, 40))], Ranges(cut));
        Assert.Equal(Rect(200, 60, 100, 20), cut.Find(".ex-focus").GetAttribute("style"));

        // Extended, it is tinted round a hole where the Focus is, and still carries no outline.
        await PressCellAsync(cut, 4, 3, shift: true);
        Assert.Equal(
            [("ex-range", Rect(0, 0, 200, 40)), ("ex-range", Rect(200, 60, 200, 40) + Hole(0, 0, 100, 20))],
            Ranges(cut));
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0004: a single range across the pinned boundary is whole in each layer, clipped to its side
    public async Task A_single_range_across_the_pinned_boundary_is_whole_in_each_layer_and_clipped_to_its_side()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);

        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 1, 2, shift: true);

        // Each layer paints the whole range and clips it at the boundary, so each draws the
        // outline's outer sides only and no seam stands where the two meet. The hole is in
        // the pinned part, which is where the Focus is.
        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(0 100px 0 0)" + Hole(0, 0, 100, 20))],
            Ranges(cut, ".ex-selection-pinned"));
        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(0 0 0 200px)")],
            Ranges(cut));
    }

    [Fact] // ADR-0008 (2026-09-29): the hole falls in whichever part of a split range holds the Focus
    public async Task The_hole_falls_in_the_scrollable_part_when_the_focus_is_there()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);

        await PressCellAsync(cut, 0, 2);
        await PressCellAsync(cut, 1, 0, shift: true);

        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(0 100px 0 0)")],
            Ranges(cut, ".ex-selection-pinned"));
        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(0 0 0 200px)" + Hole(200, 0, 300, 20))],
            Ranges(cut));
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0004: scrolled sideways, the scrollable part keeps its box and slides under the pinned block
    public async Task A_split_range_keeps_its_boxes_when_the_content_scrolls_sideways()
    {
        var cut = RenderGrid(pinnedColumnCount: 1);
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 0, 2, shift: true);
        var before = (Pinned: Ranges(cut, ".ex-selection-pinned"), Scrollable: Ranges(cut));

        // One column across, not a fling: the scrollable layer moves with the content, the
        // pinned one stays against the Viewport's edge, so neither's numbers change.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 100);

        Assert.Equal(before.Pinned, Ranges(cut, ".ex-selection-pinned"));
        Assert.Equal(before.Scrollable, Ranges(cut));
        Assert.Equal("; clip-path: inset(0 0 0 100px)", before.Scrollable.Single().Style[Rect(0, 0, 300, 20).Length..]);
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0053: a Focus scrolled out of the painted rows makes no hole in what is painted
    public async Task A_focus_outside_the_painted_rows_makes_no_hole()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 0, 0);
        await KeyAsync(cut, "a", ctrl: true);
        Assert.Contains("--ex-range-hole", Assert.Single(Ranges(cut)).Style);

        // Four rows at a time, so neither step is a fling: rows 8 to 12 are painted, and the
        // rectangle is clipped to one row beyond each end.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 4 * RowHeightPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 8 * RowHeightPx);

        Assert.Equal(("ex-range ex-range-single", Rect(0, -20, 10_000, 140)), Assert.Single(Ranges(cut)));
    }
}
