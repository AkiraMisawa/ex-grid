using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
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
        HeaderGroup[]? groups = null, int pinned = 0, double viewportHeight = 200)
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
}
