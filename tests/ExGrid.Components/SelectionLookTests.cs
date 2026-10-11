using System.Globalization;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Excel's look for the Focus and a single range (ADR-0008, 2026-09-29; UX-19): which
/// rectangles are painted, the hole the Focus makes in the tint, and which range carries an
/// outline. What the outline looks like is the stylesheet's; that it is drawn around the right
/// rectangle, and that the hole lies where the Focus is, is geometry and is pinned here. A range
/// is a tint and a box in each layer it reaches (ADR-0206): the box keeps the class
/// <c>ex-range</c> and the outline, the tint carries the hole, and the pinned tints lie in the
/// pinned shade layer.
///
/// The grid is <see cref="SelectionGridContext"/>'s, as <see cref="SelectionTests"/>' is.
/// </summary>
public class SelectionLookTests : SelectionGridContext
{
    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    /// <summary>The sides of the Selection's outline that stay inside a range at the grid's top and
    /// left edges, where the header, the Row Headings or the pinned block would cover them
    /// (ADR-0008, 2026-10-01).</summary>
    private const string InsideTopAndLeft = "; --ex-outline-in-t: 1; --ex-outline-in-l: 1";

    /// <summary>The hole a range is painted with, in the range's own box.</summary>
    private static string Hole(double left, double top, double right, double bottom)
        => FormattableString.Invariant(
            $"; --ex-range-hole: polygon(evenodd, 0 0, 100% 0, 100% 100%, 0 100%, 0 0, {left}px {top}px, {right}px {top}px, {right}px {bottom}px, {left}px {bottom}px, {left}px {top}px)");

    /// <summary>Each range's box in a layer: its classes and its style.</summary>
    private static (string Class, string Style)[] Ranges(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} > .ex-range").Select(r => (r.GetAttribute("class")!, r.GetAttribute("style")!))];

    /// <summary>Each range's tint in a layer, as its style; the pinned tints are in the pinned
    /// shade layer (ADR-0206).</summary>
    private static string[] Tints(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} > .ex-range-tint").Select(t => t.GetAttribute("style")!)];

    private const string Shade = ".ex-shade-pinned";

    [Fact] // ADR-0008 (2026-09-29) / UX-19: a one-cell selection is the Focus outline alone, with no selection tint
    public async Task A_one_cell_selection_is_the_focus_outline_alone()
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, 1, 1);

        Assert.Empty(cut.FindAll(".ex-range"));
        Assert.Empty(cut.FindAll(".ex-range-tint"));
        Assert.Equal(Rect(100, 20, 100, 20), cut.Find(".ex-focus").GetAttribute("style"));
    }

    [Fact] // ADR-0008 (2026-09-29) / UX-19: a single range is one rectangle, outlined, with a hole where the Focus is
    public async Task A_single_range_is_one_outlined_rectangle_with_a_hole_at_the_focus()
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, 1, 1);
        await PressCellAsync(cut, 3, 3, shift: true);

        // A tint and a box for the whole range, whatever its size (ADR-0008's economy; ADR-0206):
        // the Focus is its top-left cell, so the hole is the tint's first 100 x 20, and the box,
        // which draws the outline, has none.
        Assert.Equal([("ex-range ex-range-single", Rect(100, 20, 300, 60))], Ranges(cut));
        Assert.Equal([Rect(100, 20, 300, 60) + Hole(0, 0, 100, 20)], Tints(cut));
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
        Assert.Equal(Rect(0, 0, 300, 60) + Hole(0, 20, 100, 40), Assert.Single(Tints(cut)));

        await KeyAsync(cut, "Tab");                          // (1, 1): inside, touching no edge
        Assert.Equal(Rect(0, 0, 300, 60) + Hole(100, 20, 200, 40), Assert.Single(Tints(cut)));
        // The box does not move with the Focus: it draws the outline round the whole range.
        Assert.Equal(Rect(0, 0, 300, 60) + InsideTopAndLeft, Assert.Single(Ranges(cut)).Style);
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
        Assert.Equal([("ex-range", Rect(0, 0, 200, 40) + InsideTopAndLeft)], Ranges(cut));
        Assert.Equal([Rect(0, 0, 200, 40)], Tints(cut));
        Assert.Equal(Rect(200, 60, 100, 20), cut.Find(".ex-focus").GetAttribute("style"));

        // Extended, it is tinted round a hole where the Focus is, and still carries no outline.
        await PressCellAsync(cut, 4, 3, shift: true);
        Assert.Equal(
            [("ex-range", Rect(0, 0, 200, 40) + InsideTopAndLeft), ("ex-range", Rect(200, 60, 200, 40))],
            Ranges(cut));
        Assert.Equal([Rect(0, 0, 200, 40), Rect(200, 60, 200, 40) + Hole(0, 0, 100, 20)], Tints(cut));
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0004: a single range across the pinned boundary is whole in each layer, clipped to its side
    public async Task A_single_range_across_the_pinned_boundary_is_whole_in_each_layer_and_clipped_to_its_side()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);

        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 1, 2, shift: true);

        // Each layer paints the whole box and clips it at the boundary, so each draws the
        // outline's outer sides only and no seam stands where the two meet; on its other sides
        // the clip lets a row's height past the edge, where the outline lies (2026-10-01).
        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(-20px 100px -20px -20px)" + InsideTopAndLeft)],
            Ranges(cut, ".ex-selection-pinned"));
        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(-20px -20px -20px 200px)" + InsideTopAndLeft)],
            Ranges(cut));
        // The tints are cut at the boundary instead, the pinned one in the pinned shade layer
        // (ADR-0206), and the hole is in the pinned part, which is where the Focus is.
        Assert.Equal([Rect(0, 0, 200, 40) + Hole(0, 0, 100, 20)], Tints(cut, Shade));
        Assert.Equal([Rect(200, 0, 100, 40)], Tints(cut));
        Assert.Empty(Tints(cut, ".ex-selection-pinned"));
    }

    [Fact] // ADR-0008 (2026-09-29): the hole falls in whichever part of a split range holds the Focus
    public async Task The_hole_falls_in_the_scrollable_part_when_the_focus_is_there()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);

        await PressCellAsync(cut, 0, 2);
        await PressCellAsync(cut, 1, 0, shift: true);

        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(-20px 100px -20px -20px)" + InsideTopAndLeft)],
            Ranges(cut, ".ex-selection-pinned"));
        Assert.Equal(
            [("ex-range ex-range-single", Rect(0, 0, 300, 40) + "; clip-path: inset(-20px -20px -20px 200px)" + InsideTopAndLeft)],
            Ranges(cut));
        // The hole is in the scrollable tint, in the tint's own box, which starts at the boundary;
        // the boxes have none (ADR-0206).
        Assert.Equal([Rect(0, 0, 200, 40)], Tints(cut, Shade));
        Assert.Equal([Rect(200, 0, 100, 40) + Hole(0, 0, 100, 20)], Tints(cut));
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0004: scrolled sideways, the scrollable part keeps its box and slides under the pinned block
    public async Task A_split_range_keeps_its_boxes_when_the_content_scrolls_sideways()
    {
        var cut = RenderGrid(pinnedColumnCount: 1);
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 0, 2, shift: true);
        var before = (Pinned: Ranges(cut, ".ex-selection-pinned"), Scrollable: Ranges(cut));
        var tints = (Pinned: Tints(cut, Shade), Scrollable: Tints(cut));

        // One column across, not a fling: the scrollable layer moves with the content, the
        // pinned ones stay against the Viewport's edge, so none of their numbers change — the
        // tints' neither (ADR-0206): a sideways scroll writes nothing down.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 100);

        Assert.Equal(before.Pinned, Ranges(cut, ".ex-selection-pinned"));
        Assert.Equal(before.Scrollable, Ranges(cut));
        Assert.Equal(tints.Pinned, Tints(cut, Shade));
        Assert.Equal(tints.Scrollable, Tints(cut));
        // The pinned tint is the Focus's cell alone, its hole the whole of it; the scrollable one
        // starts at the boundary.
        Assert.Equal([Rect(0, 0, 100, 20) + Hole(0, 0, 100, 20)], tints.Pinned);
        Assert.Equal([Rect(100, 0, 200, 20)], tints.Scrollable);
        Assert.Equal("; clip-path: inset(-20px -20px -20px 100px)" + InsideTopAndLeft, before.Scrollable.Single().Style[Rect(0, 0, 300, 20).Length..]);
    }

    [Fact] // ADR-0008 (2026-09-29) / ADR-0053: a Focus scrolled out of the painted rows makes no hole in what is painted
    public async Task A_focus_outside_the_painted_rows_makes_no_hole()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 0, 0);
        await KeyAsync(cut, "a", ctrl: true);
        Assert.Contains("--ex-range-hole", Assert.Single(Tints(cut)));

        // Four rows at a time, so neither step is a fling: rows 8 to 12 are painted, and the
        // rectangle is clipped to one row beyond each end.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 4 * RowHeightPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 8 * RowHeightPx);

        Assert.Equal(("ex-range ex-range-single", Rect(0, -20, 10_000, 140) + InsideTopAndLeft), Assert.Single(Ranges(cut)));
        Assert.Equal(Rect(0, -20, 10_000, 140), Assert.Single(Tints(cut)));
    }

    // ---- A tint and a box, and the pinned shade layer (ADR-0206; DC-59, DOM-2) ----

    /// <summary>A layer's children, in tree order, as their first class.</summary>
    private static string[] Order(IRenderedComponent<ExGrid<TestRow>> cut, string layer)
        => [.. cut.Find(layer).Children.Select(child => child.ClassList[0])];

    [Fact] // ADR-0206 / DC-59: in each layer every tint comes before every box, so the outlines lie over the tints by tree order alone
    public async Task In_each_layer_the_tints_come_before_the_boxes()
    {
        var cut = RenderGrid(pinnedColumnCount: 1);
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 1, 2, shift: true);
        await PressCellAsync(cut, 3, 0, ctrl: true);
        await PressCellAsync(cut, 4, 3, ctrl: true, shift: true);

        // Two ranges, both across the boundary: two tints in the shade layer, then two boxes, the
        // Focus and the rest in the pinned selection layer; two tints, then two boxes, in the
        // scrollable one.
        Assert.Equal(["ex-range-tint", "ex-range-tint"], Order(cut, Shade));
        Assert.Equal(["ex-range", "ex-range", "ex-focus"], Order(cut, ".ex-selection-pinned"));
        Assert.Equal(["ex-range-tint", "ex-range-tint", "ex-range", "ex-range"], Order(cut, ".ex-selection"));
        // The shade layer comes immediately before the pinned selection layer.
        Assert.Equal("ex-selection-pinned", cut.Find(Shade).NextElementSibling!.ClassList[0]);
    }

    [Fact] // ADR-0206 / DOM-2: the pinned shade layer is emitted only while it holds something — not for a one-cell selection, nor for a range that reaches no Pinned Column
    public async Task The_shade_layer_stands_only_while_it_holds_a_tint()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);

        // One pinned cell: the Focus outline alone, no tint, no layer.
        await PressCellAsync(cut, 1, 0);
        Assert.Empty(cut.FindAll(Shade));
        Assert.Single(cut.FindAll(".ex-selection-pinned > .ex-focus"));

        // A range wholly scrollable: its tint is the scrollable layer's, and no layer stands.
        await PressCellAsync(cut, 1, 2);
        await PressCellAsync(cut, 2, 3, shift: true);
        Assert.Empty(cut.FindAll(Shade));
        Assert.Single(Tints(cut));

        // A range wholly pinned: one tint in the layer, none in the scrollable one.
        await PressCellAsync(cut, 1, 0);
        await PressCellAsync(cut, 2, 1, shift: true);
        Assert.Equal([Rect(0, 20, 200, 40) + Hole(0, 0, 100, 20)], Tints(cut, Shade));
        Assert.Empty(Tints(cut));
        Assert.Single(Ranges(cut, ".ex-selection-pinned"));
        Assert.Empty(Ranges(cut));

        // And back to one cell: the layer goes.
        await PressCellAsync(cut, 1, 0);
        Assert.Empty(cut.FindAll(Shade));
    }

    [Fact] // ADR-0206 / A11Y-12: the pinned shade layer is paint, hidden from assistive technology like its siblings
    public async Task The_shade_layer_is_hidden_from_assistive_technology()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 2, 3, shift: true);

        Assert.Equal("true", cut.Find(Shade).GetAttribute("aria-hidden"));
        Assert.Null(cut.Find(Shade).GetAttribute("role"));
    }

    [Fact] // ADR-0206 / DC-59: a range's tint carries the hole and no other clip, and its box keeps inset(...) and no hole
    public async Task The_tint_carries_the_hole_and_no_clip_and_the_box_keeps_its_clip()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);
        await PressCellAsync(cut, 1, 1);
        await PressCellAsync(cut, 3, 4, shift: true);

        foreach (var tint in Tints(cut, Shade).Concat(Tints(cut)))
            Assert.DoesNotContain("clip-path", tint);
        Assert.All(Ranges(cut, ".ex-selection-pinned").Concat(Ranges(cut)), box =>
        {
            Assert.Contains("; clip-path: inset(", box.Style);
            Assert.DoesNotContain("--ex-range-hole", box.Style);
        });
        // The pinned tint is cut at the boundary, columns 1 to the boundary at 200px, with the Focus's
        // cell, column 1 of row 1, as the hole at its own top-left; the scrollable one starts there.
        Assert.Equal([Rect(100, 20, 100, 60) + Hole(0, 0, 100, 20)], Tints(cut, Shade));
        Assert.Equal([Rect(200, 20, 300, 60)], Tints(cut));
    }

    // ---- Where the Selection's outline lies (ADR-0008, 2026-10-01; the eleventh Windows run, case 11) ----

    private static string FocusStyle(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => cut.Find($"{layer} > .ex-focus").GetAttribute("style")!;

    private static Task PlaceAsync(IRenderedComponent<ExGrid<TestRow>> cut, SelectionRange range, CellPosition focus)
        => cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(range, focus, 0));

    [Theory] // ADR-0008 (2026-10-01) / UX-18: the outline lies outside its cell, as Excel's does, on every side but a top under the header or a left at the grid's edge
    [InlineData(1, 1, "")]
    [InlineData(0, 1, "; --ex-outline-in-t: 1")]
    [InlineData(1, 0, "; --ex-outline-in-l: 1")]
    [InlineData(0, 0, InsideTopAndLeft)]
    public async Task The_focus_outline_stays_inside_only_on_a_side_something_would_cover(int row, int column, string inside)
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, row, column);

        Assert.Equal(Rect(column * 100, row * 20, 100, 20) + inside, FocusStyle(cut));
    }

    [Fact] // ADR-0008 (2026-10-01) / UX-18: scrolled, the row whose top is under the header keeps its outline's top inside, and the row below has it outside
    public async Task Scrolled_the_top_under_the_header_stays_inside()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 1, 1);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 4 * RowHeightPx);

        await PlaceAsync(cut, new SelectionRange(4, 1, 2, 2), new CellPosition(4, 1));
        Assert.Contains("--ex-outline-in-t: 1", Assert.Single(Ranges(cut)).Style);

        await PlaceAsync(cut, new SelectionRange(5, 1, 2, 2), new CellPosition(5, 1));
        Assert.DoesNotContain("--ex-outline-in", Assert.Single(Ranges(cut)).Style);
    }

    [Fact] // ADR-0008 (2026-10-01) / UX-18: beside the pinned block a scrollable side stays inside, and scrolled sideways the column under its edge does
    public async Task Beside_the_pinned_block_the_left_stays_inside()
    {
        var cut = RenderGrid(pinnedColumnCount: 1);

        // The pinned column's left is the grid's edge; the first scrollable one's, the pinned block's.
        await PressCellAsync(cut, 1, 0);
        Assert.Equal(Rect(0, 20, 100, 20) + "; --ex-outline-in-l: 1", FocusStyle(cut, ".ex-selection-pinned"));
        await PressCellAsync(cut, 1, 1);
        Assert.Equal(Rect(100, 20, 100, 20) + "; --ex-outline-in-l: 1", FocusStyle(cut));
        await PressCellAsync(cut, 1, 2);
        Assert.Equal(Rect(200, 20, 100, 20), FocusStyle(cut));

        // One column across: column 2's left edge now lies against the pinned block, column 3's clear of it.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 100);
        Assert.Equal(Rect(200, 20, 100, 20) + "; --ex-outline-in-l: 1", FocusStyle(cut));
        await PlaceAsync(cut, new SelectionRange(1, 3, 1, 1), new CellPosition(1, 3));
        Assert.Equal(Rect(300, 20, 100, 20), FocusStyle(cut));
    }

    [Fact] // ADR-0008 (2026-10-01) / UX-18 / ADR-0050: beside the Row Headings the left stays inside, and a column clear of them has it outside
    public async Task Beside_the_row_headings_the_left_stays_inside()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(100))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, ViewportWidthPx)
            .Add(g => g.RowHeadings, row => (row + 1).ToString(CultureInfo.InvariantCulture)));

        await PlaceAsync(cut, new SelectionRange(1, 0, 2, 2), new CellPosition(1, 0));
        Assert.EndsWith("; --ex-outline-in-l: 1", WithoutHole(Assert.Single(Ranges(cut)).Style));

        await PlaceAsync(cut, new SelectionRange(1, 1, 2, 2), new CellPosition(1, 1));
        Assert.DoesNotContain("--ex-outline-in", Assert.Single(Ranges(cut)).Style);
    }

    /// <summary>A range's style without the Focus's hole, which comes last.</summary>
    private static string WithoutHole(string style) => System.Text.RegularExpressions.Regex.Replace(style, @";\s*--ex-range-hole:[^;]*$", "");
}
