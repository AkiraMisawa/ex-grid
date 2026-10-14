using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The painted half of Header Groups (ADR-0032): the rectangles' positions are strings
/// computed from ColumnGeometry, so the bUnit layer can pin all of it. 6 columns of
/// 100px, 20px rows, a 32px header tier.
/// </summary>
public class HeaderGroupRenderingTests : GridTestContext
{
    private const double HeaderPx = 32;

    private static HeaderGroup[] Groups() =>
    [
        new HeaderGroup("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2), TestRows.ColumnName(3)]),
        new HeaderGroup("Adjustments",
            [TestRows.ColumnName(1), TestRows.ColumnName(2), TestRows.ColumnName(3), TestRows.ColumnName(4)],
            tier: 2),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IReadOnlyList<HeaderGroup>? groups = null, int pinned = 0, double viewportHeight = 200)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, viewportHeight)
            .Add(g => g.ViewportWidth, 650)
            .Add(g => g.PinnedColumnCount, pinned)
            .Add(g => g.HeaderGroups, groups ?? Groups()));

    [Fact] // ADR-0032 / HG-2: member count is the colspan; tierSpan is the rowspan
    public void The_rectangle_spans_its_members_and_tiers()
    {
        var cut = RenderGrid();

        var rectangles = cut.FindAll(".ex-header-group");
        Assert.Equal(2, rectangles.Count);
        // CVA: columns 1-3 of 100px, one 32px tier, on the tier directly above the leaves.
        Assert.Contains("left: 100px", rectangles[0].GetAttribute("style"));
        Assert.Contains("width: 300px", rectangles[0].GetAttribute("style"));
        Assert.Contains("height: 32px", rectangles[0].GetAttribute("style"));
        Assert.Contains("top: 32px", rectangles[0].GetAttribute("style"));
        // Adjustments: columns 1-4, tier 2 — the band's top.
        Assert.Contains("width: 400px", rectangles[1].GetAttribute("style"));
        Assert.Contains("top: 0px", rectangles[1].GetAttribute("style"));
    }

    [Fact] // ADR-0032 / HG-8: the band is (1 + tiers) × HeaderHeight and the first row is unchanged by it
    public async Task The_band_leaves_the_row_arithmetic_alone()
    {
        var untiered = RenderGrid(groups: []);
        var tiered = RenderGrid();

        // Same scroll offset lands on the same first row, at 0, 1 and 2 tiers.
        await ScrollToAsync(untiered.Find(".ex-scroller"), 200, 0);
        await ScrollToAsync(tiered.Find(".ex-scroller"), 200, 0);

        Assert.Equal("11", untiered.FindAll(".ex-row")[0].GetAttribute("aria-rowindex"));
        Assert.Equal("11", tiered.FindAll(".ex-row")[0].GetAttribute("aria-rowindex"));
        // And the band's height is stated on the header: 3 tiers × 32px.
        Assert.Contains("height: 96px", tiered.Find(".ex-header").GetAttribute("style"));
        Assert.Contains("height: 96px", tiered.Find(".ex-header").GetAttribute("style"));
    }

    [Fact] // ADR-0032 / HG-3: an uncovered column's leaf header stretches the full band
    public void An_uncovered_leaf_is_band_tall()
    {
        var cut = RenderGrid();

        var leaves = cut.FindAll(".ex-header-cell");
        // Column 0 is uncovered: three tiers tall. Column 1 is covered at tier 1: one.
        Assert.Contains("height: 96px", leaves[0].GetAttribute("style"));
        Assert.Contains("height: 32px", leaves[1].GetAttribute("style"));
        // Column 4 is covered at tier 2 only: its leaf stretches two tiers.
        Assert.Contains("height: 64px", leaves[4].GetAttribute("style"));
    }

    private static GridColumn<TestRow>[] InOrder(params int[] order)
    {
        var wide = TestRows.Wide(6);
        return [.. order.Select(i => wide[i])];
    }

    [Fact] // HG-1, ADR-0032: membership is a set — the painted order comes from Columns, never the declaration
    public void HG1_members_declared_scrambled_paint_in_the_columns_order()
    {
        // Declared C03, C01, C02; the flat order puts them at positions 3-5 as C02, C03, C01.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, InOrder(0, 4, 5, 2, 3, 1))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 650)
            .Add(g => g.HeaderGroups, (HeaderGroup[])[
                new HeaderGroup("Set", [TestRows.ColumnName(3), TestRows.ColumnName(1), TestRows.ColumnName(2)])]));

        var style = cut.Find(".ex-header-group").GetAttribute("style");
        Assert.Contains("left: 300px", style);
        Assert.Contains("width: 300px", style);
        // The leaves under it, left to right, are in Columns order.
        var leaves = cut.FindAll(".ex-header-cell").Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(TestRows.ColumnName(2), leaves[3]);
        Assert.Equal(TestRows.ColumnName(3), leaves[4]);
        Assert.Equal(TestRows.ColumnName(1), leaves[5]);
        // Covered leaves are one tier tall, uncovered ones are band tall.
        Assert.Contains("height: 32px", cut.FindAll(".ex-header-cell")[3].GetAttribute("style"));
        Assert.Contains("height: 64px", cut.FindAll(".ex-header-cell")[2].GetAttribute("style"));
    }

    [Fact] // HG-5, ADR-0032: a reorder that separates a group's members is refused, naming adjacency
    public void HG5_a_reorder_that_separates_the_members_is_refused()
    {
        HeaderGroup[] groups = [new HeaderGroup("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2), TestRows.ColumnName(3)])];
        void Push(ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps, GridColumn<TestRow>[] columns) => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 650)
            .Add(g => g.HeaderGroups, groups);
        var cut = Render<ExGrid<TestRow>>(ps => Push(ps, InOrder(0, 1, 2, 3, 4, 5)));
        Assert.Single(cut.FindAll(".ex-header-group"));

        // The Consumer moves C04 between C02 and C03: the members are no longer adjacent.
        var refusal = Assert.Throws<InvalidOperationException>(() => cut.Render(ps => Push(ps, InOrder(0, 1, 2, 4, 3, 5))));
        Assert.Contains("CVA", refusal.Message);
        Assert.Contains("not adjacent", refusal.Message);
    }

    [Fact] // ADR-0032 / HG-9: a viewport that cannot hold the band plus one row is refused, naming the band
    public void A_viewport_under_the_band_is_refused_naming_the_band()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => RenderGrid(viewportHeight: 100));

        Assert.Contains("band", refusal.Message);
    }

    [Fact] // ADR-0032 / HG-10: the rectangle stands whether or not its leaves are in the DOM
    public async Task A_rectangle_stands_when_its_leaves_are_virtualised_away()
    {
        // 40 columns; a group over columns 30-32, far right of a 350px viewport.
        HeaderGroup[] groups = [new HeaderGroup("Far", [TestRows.ColumnName(30), TestRows.ColumnName(31), TestRows.ColumnName(32)])];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Wide(40))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.HeaderGroups, groups));

        var unscrolled = cut.Find(".ex-header-group").GetAttribute("style");
        Assert.Contains("left: 3000px", unscrolled);

        // Scroll a little: the rectangle's content position is unchanged — the scroll
        // moves the viewport, not the rectangle.
        await ScrollToAsync(cut.Find(".ex-scroller"), 0, 500);
        Assert.Contains("left: 3000px", cut.Find(".ex-header-group").GetAttribute("style"));
        Assert.Contains("width: 300px", cut.Find(".ex-header-group").GetAttribute("style"));
    }

    [Fact] // HG-10, ADR-0032/0004: scrolled so the group's first members leave the DOM while later ones are painted
    public async Task HG10_a_rectangle_stands_when_its_first_members_are_virtualised_away()
    {
        // 40 columns of 100px; a group over C10-C19, content 1000-2000px.
        HeaderGroup[] Groups() => [new HeaderGroup("Mid", [.. Enumerable.Range(10, 10).Select(TestRows.ColumnName)])];
        var columns = TestRows.Wide(40);
        void Push(ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps) => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.HeaderGroups, Groups());
        var cut = Render<ExGrid<TestRow>>(Push);

        var unscrolled = cut.Find(".ex-header-group").GetAttribute("style")!;
        Assert.Contains("left: 1000px", unscrolled);
        Assert.Contains("width: 1000px", unscrolled);

        // Ordinary moves to 1500px: C15 onwards is painted, C10-C12 are out of the DOM.
        var scroller = cut.Find(".ex-scroller");
        foreach (var left in new[] { 300d, 600d, 900d, 1200d, 1500d })
            await ScrollToAsync(scroller, 0, left);
        var painted = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell")
            .Select(c => TestRows.ColumnOf(c.TextContent)).ToArray();
        Assert.Contains("C16", painted);
        Assert.DoesNotContain("C10", painted);
        Assert.DoesNotContain("C12", painted);

        // The rectangle is composed again while scrolled there — the Consumer pushes its
        // declaration anew — and it must still stand on the first member, painted or not.
        cut.Render(Push);
        var scrolled = cut.Find(".ex-header-group").GetAttribute("style")!;
        Assert.Contains("left: 1000px", scrolled);
        Assert.Contains("width: 1000px", scrolled);
        Assert.Equal(unscrolled, scrolled);
    }

    [Fact] // ADR-0032 / HG-12: a group label never hashes and never feeds Auto width
    public void A_long_label_gets_the_text_treatment_and_grows_nothing()
    {
        GridColumn<TestRow>[] columns =
        [
            new("A", ColumnType.Number, r => r.Amount),
            new("B", ColumnType.Number, r => r.Amount),
        ];
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, columns)
            .Add(g => g.HeaderGroups, (HeaderGroup[])[
                new HeaderGroup("A label far longer than the two member columns can carry", ["A", "B"])]));

        Assert.DoesNotContain("####", cut.Find(".ex-header-group").TextContent);
        // The member columns keep the widths their own contents earned: both got the
        // same content, so both resolve to the same width — the long label grew neither.
        static string WidthOf(AngleSharp.Dom.IElement cell)
        {
            var style = cell.GetAttribute("style")!;
            var at = style.IndexOf("width:", StringComparison.Ordinal);
            return style[at..];
        }
        var cells = cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell");
        Assert.Equal(WidthOf(cells[0]), WidthOf(cells[1]));
    }

    [Fact] // HG-12, ADR-0032/0016: a long label over narrow Auto columns grows neither, against the same grid without it, and ellipsises
    public void HG12_a_long_group_label_feeds_no_auto_width_and_gets_the_ellipsis()
    {
        // Two Auto columns of different content, so each earns a width of its own.
        GridColumn<TestRow>[] columns =
        [
            new("A", ColumnType.Number, r => r.Amount),
            new("Book", ColumnType.Text, r => r.Book),
        ];
        const string label = "A label far longer than the two member columns could ever carry between them";
        IRenderedComponent<ExGrid<TestRow>> RenderWith(HeaderGroup[] groups) => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, columns)
            .Add(g => g.HeaderGroups, groups));
        static double WidthPx(AngleSharp.Dom.IElement element) => double.Parse(
            System.Text.RegularExpressions.Regex.Match(element.GetAttribute("style")!, @"(?:^|;)\s*width: ([0-9.]+)px").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        static double[] WidthsOf(IRenderedComponent<ExGrid<TestRow>> cut)
            => [.. cut.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell").Select(WidthPx)];

        var bare = RenderWith([]);
        var grouped = RenderWith([new HeaderGroup(label, ["A", "Book"])]);

        // The member columns keep exactly the widths their own contents earned.
        Assert.Equal(WidthsOf(bare), WidthsOf(grouped));
        var rectangle = grouped.Find(".ex-header-group");
        Assert.Equal(WidthsOf(bare).Sum(), WidthPx(rectangle));
        // The label is the Text treatment: painted verbatim, never ####, clipped by an ellipsis.
        Assert.Equal(label, rectangle.TextContent);
        Assert.Equal("ellipsis", ShippedStylesheetTests.Winning(rectangle, "text-overflow"));
        Assert.Equal("hidden", ShippedStylesheetTests.Winning(rectangle, "overflow"));
        Assert.Equal("nowrap", ShippedStylesheetTests.Winning(rectangle, "white-space"));
    }

    [Fact] // ADR-0032 / A11Y-13: a group cell carries aria-colspan equal to its member count
    public void A_group_cell_carries_its_colspan()
    {
        var cut = RenderGrid();

        Assert.Equal("3", cut.FindAll(".ex-header-group")[0].GetAttribute("aria-colspan"));
        Assert.Equal("4", cut.FindAll(".ex-header-group")[1].GetAttribute("aria-colspan"));
    }

    [Fact] // ADR-0032 / HG-16: a click on a group rectangle sorts nothing; the leaf tier still sorts
    public async Task A_group_click_does_not_sort_the_leaf_under_it()
    {
        IReadOnlyList<SortSpec>? raised = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.HeaderGroups, Groups())
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => raised = s));

        // x=150 is column 1; y=40 is inside CVA's rectangle (tier 1 of a 3-tier band).
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 150, OffsetY = 40 });
        Assert.Null(raised);

        // The same column's own leaf tier sorts.
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 150, OffsetY = 80 });
        Assert.Equal([new SortSpec(TestRows.ColumnName(1), SortDirection.Ascending)], raised);

        // And the uncovered column's stretched leaf sorts at any tier.
        raised = null;
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });
        Assert.Equal([new SortSpec(TestRows.ColumnName(0), SortDirection.Ascending)], raised);
    }

    [Fact] // ADR-0032 / ADR-0050 / ADR-0012: a group's rectangle selects nothing — not plainly under HeaderClickSelects, not with Shift, not with Ctrl; the leaf tier does
    public async Task A_group_press_selects_nothing_under_any_modifier_and_the_leaf_tier_does()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? raised = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.HeaderGroups, Groups())
            .Add(g => g.HeaderClickSelects, true)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s)
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> s) => raised = s));
        string Selected() => selection is null ? "none" : $"{selection.Focus} [{string.Join(", ", selection.Ranges)}]";
        async Task PressHeader(double y, bool shift = false, bool ctrl = false)
        {
            await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = y, ShiftKey = shift, CtrlKey = ctrl });
            await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 150, OffsetY = y, ShiftKey = shift, CtrlKey = ctrl });
        }

        // One cell of column 3 selected, so a whole-column gesture on column 1 would change it.
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 330, OffsetY = 30 });
        var before = Selected();
        Assert.NotEqual("none", before);

        // y=40 is inside CVA's rectangle over column 1 (tier 1 of a 3-tier band).
        foreach (var (shift, ctrl) in new[] { (false, false), (true, false), (false, true) })
        {
            await PressHeader(40, shift, ctrl);
            Assert.Equal(before, Selected());
        }
        Assert.Null(raised);

        // The leaf tier of the same column: each gesture selects, so the presses above were heard.
        // Ctrl adds the column beside the cell; then the plain press takes the column alone.
        await PressHeader(3 * HeaderPx - 10, ctrl: true);
        var added = Selected();
        Assert.NotEqual(before, added);
        await PressHeader(3 * HeaderPx - 10);
        Assert.NotEqual(added, Selected());
        Assert.Null(raised);
    }

    // ---- HG-16: nothing below the band knows tiers exist ---------------------------------------
    // The same gestures on the same grid, once with no Header Groups and once with two tiers of
    // them: presses on the header at every tier, the keys that climb towards it, and both copies.
    // What the grid answers — the Selection, the Focus as the root names it, and the clipboard's
    // text — is one transcript, and the tiered one must be the untiered one word for word.

    private async Task<List<string>> BelowTheBandTranscript(IReadOnlyList<HeaderGroup> groups)
    {
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 300)
            .Add(g => g.ViewportWidth, 650)
            .Add(g => g.HeaderGroups, groups)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        var band = groups.Count == 0 ? HeaderPx : 3 * HeaderPx;
        var transcript = new List<string>();
        void Note(string step) => transcript.Add(
            $"{step}: {(selection is null ? "none" : $"{selection.Focus}..{selection.Extent} [{string.Join(", ", selection.Ranges)}]")}"
            + $" focus={cut.Find(".ex-grid").GetAttribute("aria-activedescendant")?.Split('-')[^1]}");
        Task Press(string key, bool ctrl = false, bool shift = false)
            => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

        // Row 2 of column 1.
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 50 });
        Note("cell");

        // A press and a click at every height of the band over column 1: its leaf tier, and
        // where the tiered grid draws CVA and Adjustments above it.
        var heights = new[] { band - 10, Math.Max(5, band - HeaderPx - 10), 10 };
        for (var tier = 0; tier < heights.Length; tier++)
        {
            var y = heights[tier];
            await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = y });
            await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 150, OffsetY = y });
            Note($"header press {tier}");
        }

        // The keys that climb: past the first row, Focus has nowhere further to go.
        foreach (var (key, ctrl, shift) in new[]
            { ("ArrowUp", false, false), ("ArrowUp", false, false), ("ArrowUp", false, false),
              ("PageUp", false, false), ("ArrowUp", true, false), ("Home", true, false),
              ("ArrowUp", false, true), ("ArrowUp", true, true), ("PageUp", false, true) })
        {
            await Press(key, ctrl, shift);
            Note($"{(ctrl ? "Ctrl+" : "")}{(shift ? "Shift+" : "")}{key}");
        }

        // Everything, copied plain and with headers.
        await Press("a", ctrl: true);
        Note("Ctrl+A");
        var plain = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync());
        var headed = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayloadAsync(withHeaders: true));
        transcript.Add($"copy: {plain?.Text}");
        transcript.Add($"copy html: {plain?.Html}");
        transcript.Add($"copy with headers: {headed?.Text}");
        transcript.Add($"copy with headers html: {headed?.Html}");
        return transcript;
    }

    [Fact] // ADR-0032 / ADR-0012 / ADR-0005 / HG-16: headers stay unselectable, Focus never enters the header, no tier is copied — unchanged from the untiered case
    public async Task HG16_nothing_below_the_band_knows_tiers_exist()
    {
        var untiered = await BelowTheBandTranscript([]);
        var tiered = await BelowTheBandTranscript(Groups());

        Assert.Equal(untiered, tiered);

        // And what the untiered case says is what HG-16 means. A press on the header selected
        // nothing and moved no Focus: every press left the cell's answer standing.
        var cell = untiered[0]["cell: ".Length..];
        Assert.All(untiered.Where(line => line.StartsWith("header press", StringComparison.Ordinal)),
            line => Assert.EndsWith(cell, line));
        // Every key's Focus is a cell the root names, never a header: the climb stops on row 0.
        Assert.All(untiered.Where(line => !line.StartsWith("copy", StringComparison.Ordinal)),
            line => Assert.Matches(@" focus=r\d+c\d+$", line));
        Assert.EndsWith("focus=r0c1", untiered[6]);   // the third ArrowUp, from row 2
        Assert.EndsWith("focus=r0c1", untiered[7]);   // PageUp, already there
        // The plain copy is cell values only, from row 0; the one with headers adds one row of
        // leaf headers and no tier's label.
        var plain = untiered.Single(line => line.StartsWith("copy: ", StringComparison.Ordinal));
        Assert.StartsWith("copy: Row 000000/00\t", plain);
        var headed = untiered.Single(line => line.StartsWith("copy with headers: ", StringComparison.Ordinal));
        Assert.StartsWith("copy with headers: C00\tC01\tC02\tC03\tC04\tC05\r\nRow 000000/00\t", headed);
        Assert.DoesNotContain(tiered, line => line.Contains("CVA", StringComparison.Ordinal)
            || line.Contains("Adjustments", StringComparison.Ordinal));
    }

    [Fact] // ADR-0032 / HG-17: cost is per rectangle — element count scales with groups, rows unchanged
    public void Cost_is_per_rectangle_never_per_column()
    {
        var none = RenderGrid(groups: []);
        var some = RenderGrid();

        Assert.Empty(none.FindAll(".ex-header-group"));
        Assert.Equal(2, some.FindAll(".ex-header-group").Count);
        // The rows know nothing about tiers: each paints exactly the cells it painted.
        Assert.Equal(
            none.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell").Length,
            some.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell").Length);
    }

    [Fact] // HG-17, ADR-0032/0008: 3 groups over 40 columns — elements scale with groups, row renders unchanged
    public async Task HG17_three_groups_over_forty_columns_cost_per_rectangle_and_no_row_renders()
    {
        static HeaderGroup[] Spans(params (int First, int Count)[] spans) =>
            [.. spans.Select((s, n) => new HeaderGroup($"G{n}", [.. Enumerable.Range(s.First, s.Count).Select(TestRows.ColumnName)]))];
        var wide = Spans((0, 10), (10, 15), (25, 15));   // every one of the 40 columns covered
        var narrow = Spans((0, 2), (2, 2), (4, 2));      // 6 of the 40 covered
        var columns = TestRows.Wide(40);
        var window = TestRows.Many(200);
        void Push(ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps, HeaderGroup[] groups) => ps
            .Add(g => g.Window, window)
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            // The band takes one tier from the Viewport; the ungrouped grid is given it back,
            // so both paint the same rows and the comparison is about the band alone.
            .Add(g => g.ViewportHeight, groups.Length == 0 ? 300 - HeaderPx : 300)
            .Add(g => g.ViewportWidth, 650)
            .Add(g => g.HeaderGroups, groups);

        var none = Render<ExGrid<TestRow>>(ps => Push(ps, []));
        var three = Render<ExGrid<TestRow>>(ps => Push(ps, wide));
        var threeNarrow = Render<ExGrid<TestRow>>(ps => Push(ps, narrow));

        // One element per rectangle, however many columns it spans: a group over fifteen
        // columns costs what a group over two costs.
        Assert.Equal(3, three.FindAll(".ex-header-group").Count);
        static int Elements(IRenderedComponent<ExGrid<TestRow>> cut) => cut.FindAll("*").Count;
        Assert.Equal(none.FindAll(".ex-row").Count, three.FindAll(".ex-row").Count);
        Assert.Equal(Elements(three), Elements(threeNarrow));
        Assert.Equal(3 + three.FindAll(".ex-header-groups-pinned").Count, Elements(three) - Elements(none));

        // The rows know nothing of the band: the same scrolls cost the same row renders.
        static async Task<int> RowRendersOver(GridTestContext context, IRenderedComponent<ExGrid<TestRow>> cut)
        {
            foreach (var top in new[] { 20d, 40d, 60d, 400d })
                await context.ScrollToAsync(cut.Find(".ex-scroller"), top);
            return cut.FindComponents<ExGridRow<TestRow>>().Sum(r => r.RenderCount);
        }
        Assert.Equal(await RowRendersOver(this, none), await RowRendersOver(this, three));

        // And a relabelled declaration of the same shape renders the band, not one row.
        var rows = three.FindComponents<ExGridRow<TestRow>>();
        var before = rows.Select(r => r.RenderCount).ToArray();
        three.Render(ps => Push(ps, [.. wide.Select(g => new HeaderGroup(g.Label + "'", g.Columns))]));
        Assert.Equal("G0'", three.FindAll(".ex-header-group")[0].TextContent);
        Assert.Equal(before, rows.Select(r => r.RenderCount).ToArray());
        Assert.Equal(rows.Count, three.FindComponents<ExGridRow<TestRow>>().Count); // the same instances, none replaced
    }

    /// <summary>
    /// A declaration that counts how often the grid reads it, from outside the component. It
    /// counts **indexer reads, not `Count` reads**, and it takes each group exactly once, so with a
    /// single group declared this counter is the number of times the grid took the declaration in.
    ///
    /// <para>The grid takes a declaration in once, as the copy it draws from (ADR-0003, the note of
    /// 2026-10-13), and resolves membership from that copy; the resolutions themselves are counted
    /// where they happen, by <c>HeaderGroupResolutions</c>. Both are asserted: the Consumer's list is
    /// read once per push, and membership is resolved once per push and never on the render path —
    /// the whole of HG-11.</para>
    /// </summary>
    private sealed class CountingGroups(IReadOnlyList<HeaderGroup> inner) : IReadOnlyList<HeaderGroup>
    {
        internal int Resolutions { get; private set; }

        public HeaderGroup this[int index]
        {
            get
            {
                Resolutions++;
                return inner[index];
            }
        }

        public int Count => inner.Count;

        public IEnumerator<HeaderGroup> GetEnumerator() => inner.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact] // ADR-0032 / HG-11: resolution count is 1, not 20 — once per push, off the render path
    public async Task Membership_resolves_once_per_push_and_never_while_scrolling()
    {
        var groups = new CountingGroups(
            [new HeaderGroup("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2)])]);
        var cut = RenderGrid(groups: groups);
        Assert.NotEmpty(cut.FindAll(".ex-header-group"));
        var renders = cut.RenderCount;

        for (var i = 1; i <= 20; i++)
            await ScrollToAsync(cut.Find(".ex-scroller"), i * 20d);

        // The scrolls really did paint — otherwise this asserts nothing — and across all
        // of them the declaration was resolved exactly once, on the push that brought it.
        Assert.True(cut.RenderCount > renders, "the scrolls rendered nothing, so this proves nothing");
        Assert.Equal(1, groups.Resolutions);
        Assert.Equal(1, cut.Instance.HeaderGroupResolutions);
        Assert.NotEmpty(cut.FindAll(".ex-header-group"));
    }

    [Fact] // ADR-0032 / HG-11: a new declaration is a push, and is resolved again
    public void A_pushed_declaration_is_resolved_again()
    {
        var groups = new CountingGroups(
            [new HeaderGroup("CVA", [TestRows.ColumnName(1), TestRows.ColumnName(2)])]);
        var cut = RenderGrid(groups: groups);
        Assert.Equal(1, groups.Resolutions);
        Assert.Equal(1, cut.Instance.HeaderGroupResolutions);

        var pushed = new CountingGroups(
            [new HeaderGroup("CVA", [TestRows.ColumnName(2), TestRows.ColumnName(3)])]);
        cut.Render(ps => ps.Add(g => g.HeaderGroups, (IReadOnlyList<HeaderGroup>)pushed));

        // Once per push is the rule in both directions: the old declaration is not
        // re-read, and the new one is resolved rather than assumed unchanged.
        Assert.Equal(1, groups.Resolutions);
        Assert.Equal(1, pushed.Resolutions);
        Assert.Equal(2, cut.Instance.HeaderGroupResolutions);
        Assert.Contains("left: 200px", cut.FindAll(".ex-header-group")[0].GetAttribute("style"));
    }


    [Fact] // ADR-0032 / ADR-0010: the menu never offers a pin the grid would refuse — through a group it is disabled
    public async Task Pinning_through_a_group_is_not_offered()
    {
        // CVA covers columns 1-3: pinning up to 1, 2 or 3 would cut it; up to 0 or 4 would not.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 650)
            .Add(g => g.HeaderGroups, (HeaderGroup[])[Groups()[0]])
            .Add(g => g.OnPinnedCountChanged, (int _) => { }));

        bool PinOffered(int column)
        {
            var pin = cut.FindAll(".ex-popover button[role=menuitem]").Single(b => b.TextContent == "Pin up to this column");
            return !pin.HasAttribute("disabled");
        }

        var offered = new List<bool>();
        for (var column = 0; column < 5; column++)
        {
            await cut.FindAll(".ex-menu-button")[column].ClickAsync(new MouseEventArgs());
            offered.Add(PinOffered(column));
            await cut.FindAll(".ex-menu-button")[column].ClickAsync(new MouseEventArgs());
        }

        // Pinning up to column c pins c + 1 columns.
        Assert.Equal([true, false, false, true, true], offered);
    }

    [Fact] // ADR-0028 / ADR-0032: a Stretch grid paints nothing and throws nothing until the browser reports its box
    public async Task A_stretch_grid_with_groups_waits_for_its_box()
    {
        // Before the first report the column geometry is unresolved, as in a hidden tab; the
        // rectangles stand on that geometry, so they wait with the columns.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.HeaderHeight, HeaderPx)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, ViewportSize.Stretch)
            .Add(g => g.HeaderGroups, Groups()));

        Assert.Empty(cut.FindAll(".ex-header-group"));

        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 650, 200));

        var rectangles = cut.FindAll(".ex-header-group");
        Assert.Equal(2, rectangles.Count);
        Assert.Contains("left: 100px", rectangles[0].GetAttribute("style"));
        Assert.Contains("width: 300px", rectangles[0].GetAttribute("style"));
    }
}
