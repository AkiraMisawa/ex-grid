using System.Globalization;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Heading Mark (ADR-0008, 2026-10-13): for every Selection, the Headings of the columns and the
/// rows that hold part of it are marked, by an overlay — one box per run of marked Headings over the
/// header band and over the Row Headings — so no row learns the Selection. Where each box stands is
/// geometry and is pinned here; the line on its edge toward the cells is the stylesheet's
/// (<see cref="ShippedStylesheetTests"/>), and what it paints is layer 3's.
///
/// 50 rows of 20px under a 24px header, six 100px columns in a 350 × 200 Viewport; the Row Headings,
/// where declared, are 40px wide, so column c starts at 40 + 100c.
/// </summary>
public class HeadingMarkTests : GridTestContext
{
    private const double HeaderPx = 24;

    private static readonly Func<int, string> RowNumbers = row => (row + 1).ToString(CultureInfo.InvariantCulture);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null, bool rowHeadings = true)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, TestRows.Wide(6))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.HeaderHeight, HeaderPx)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350);
            if (rowHeadings)
                ps.Add(g => g.RowHeadings, RowNumbers).Add(g => g.RowHeadingWidth, 40d);
            extra?.Invoke(ps);
        });

    /// <summary>A press at the centre of cell (row, column), the rows counted from the top and the
    /// columns after a band of <paramref name="leadPx"/>.</summary>
    private static Task PressCellAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, int row, int column, bool shift = false, bool ctrl = false, double leadPx = 40)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            OffsetX = leadPx + (column * 100) + 50,
            OffsetY = (row * 20) + 10,
            Button = 0,
            Buttons = 1,
            ShiftKey = shift,
            CtrlKey = ctrl,
        });

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x)
        => cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = 10 });

    private static string Box(double left, double top, double width, double height)
        => FormattableString.Invariant($"left: {left}px; top: {top}px; width: {width}px; height: {height}px");

    /// <summary>A Row Heading's mark box: the band's width, at the painted slice's coordinates.</summary>
    private static string RowBox(double top, double height, double width = 40)
        => FormattableString.Invariant($"left: 0; top: {top}px; width: {width}px; height: {height}px");

    /// <summary>The column marks the header band holds outside its pinned layer, as their styles.</summary>
    private static string[] ColumnMarks(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-header > .ex-heading-mark").Select(mark => mark.GetAttribute("style")!)];

    /// <summary>The column marks in the header band's pinned layer, as their styles.</summary>
    private static string[] PinnedColumnMarks(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-header > .ex-heading-marks-pinned > .ex-heading-mark").Select(mark => mark.GetAttribute("style")!)];

    /// <summary>The row marks over the Row Headings, as their styles.</summary>
    private static string[] RowMarks(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-viewport > .ex-heading-marks-rows > .ex-heading-mark").Select(mark => mark.GetAttribute("style")!)];

    [Fact] // ADR-0008 (2026-10-13): with nothing selected no Heading is marked, and no layer stands for the mark
    public void ADR0008_nothing_selected_marks_no_heading()
    {
        var cut = RenderGrid();

        Assert.Empty(cut.FindAll(".ex-heading-mark"));
        Assert.Empty(cut.FindAll(".ex-heading-marks-rows"));
        Assert.Empty(cut.FindAll(".ex-heading-marks-pinned"));
    }

    [Fact] // ADR-0008 (2026-10-13): one cell marks its column's Heading and its row's, each a box of the Heading's own, classed by the edge its line is on
    public async Task ADR0008_one_cell_marks_its_column_heading_and_its_row_heading()
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, 2, 1);

        // The column's Heading: column 1's box over the band's one tier.
        var column = Assert.Single(cut.FindAll(".ex-header .ex-heading-mark"));
        Assert.Equal("ex-heading-mark ex-heading-mark-column", column.GetAttribute("class"));
        Assert.Equal("true", column.GetAttribute("aria-hidden"));
        Assert.Equal([Box(140, 0, 100, HeaderPx)], ColumnMarks(cut));
        // The row's Heading: row 2's box over the band of Row Headings, in a layer of the Viewport.
        var row = Assert.Single(cut.FindAll(".ex-viewport .ex-heading-mark"));
        Assert.Equal("ex-heading-mark ex-heading-mark-row", row.GetAttribute("class"));
        Assert.Equal([RowBox(40, 20)], RowMarks(cut));
        Assert.Equal("true", cut.Find(".ex-heading-marks-rows").GetAttribute("aria-hidden"));
        // No Pinned Column, so no pinned layer.
        Assert.Empty(cut.FindAll(".ex-heading-marks-pinned"));
    }

    [Fact] // ADR-0008 (2026-10-13): a range marks every column and row it spans, as one box per axis, however many Headings that is
    public async Task ADR0008_a_range_is_one_box_per_axis()
    {
        var cut = RenderGrid();

        await PressCellAsync(cut, 1, 1);
        await PressCellAsync(cut, 4, 3, shift: true);

        Assert.Equal([Box(140, 0, 300, HeaderPx)], ColumnMarks(cut));
        Assert.Equal([RowBox(20, 80)], RowMarks(cut));
    }

    [Fact] // ADR-0008 (2026-10-13): several ranges mark each one's columns and rows, apart where Headings between them are not selected, joined where they meet — as R4 read Excel's under A:B and D:E
    public async Task ADR0008_several_ranges_mark_each_ranges_headings_and_neighbours_join()
    {
        var cut = RenderGrid();

        // R4's selection: A1:B2, then Ctrl and D4:E6. Column C and row 3 lie between, unmarked.
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 1, 1, shift: true);
        await PressCellAsync(cut, 3, 3, ctrl: true);
        await PressCellAsync(cut, 5, 4, shift: true, ctrl: true);
        Assert.Equal(2, cut.FindAll(".ex-range").Count);
        Assert.Equal([Box(40, 0, 200, HeaderPx), Box(340, 0, 200, HeaderPx)], ColumnMarks(cut));
        Assert.Equal([RowBox(0, 40), RowBox(60, 60)], RowMarks(cut));

        // Enter walks the Focus through the ranges (ADR-0052): the Focus marks nothing of its own, so
        // the marks stay as the ranges have them.
        await KeyAsync(cut, "Enter");
        Assert.Equal([Box(40, 0, 200, HeaderPx), Box(340, 0, 200, HeaderPx)], ColumnMarks(cut));
        Assert.Equal([RowBox(0, 40), RowBox(60, 60)], RowMarks(cut));

        // C8 added: column C now joins the two runs into one line, A to E; row 8 is a run of its own.
        await PressCellAsync(cut, 7, 2, ctrl: true);
        Assert.Equal([Box(40, 0, 500, HeaderPx)], ColumnMarks(cut));
        Assert.Equal([RowBox(0, 40), RowBox(60, 60), RowBox(140, 20)], RowMarks(cut));

        // A press without Ctrl makes one cell the Selection again, and only its Headings are marked.
        await PressCellAsync(cut, 6, 5);
        Assert.Equal([Box(540, 0, 100, HeaderPx)], ColumnMarks(cut));
        Assert.Equal([RowBox(120, 20)], RowMarks(cut));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0050 / ADR-0053: a whole column marks every painted Row Heading, cut to the painted rows; a whole row marks every column's Heading
    public async Task ADR0008_whole_columns_and_whole_rows_mark_the_whole_other_axis()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.HeaderClickSelects, true));

        await ClickHeaderAsync(cut, 190);                         // column 1
        Assert.Equal([Box(140, 0, 100, HeaderPx)], ColumnMarks(cut));
        // Every row is selected; the box is cut to the painted rows and the one past their end.
        var rows = cut.FindAll(".ex-viewport > .ex-row");
        var last = int.Parse(rows[^1].GetAttribute("aria-rowindex")!, CultureInfo.InvariantCulture) - 1;
        Assert.Equal([RowBox(0, (last + 2) * 20)], RowMarks(cut));

        // Scrolled down, the box starts a row above the first painted one.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 300);
        var first = int.Parse(cut.FindAll(".ex-viewport > .ex-row")[0].GetAttribute("aria-rowindex")!, CultureInfo.InvariantCulture) - 1;
        var count = cut.FindAll(".ex-viewport > .ex-row").Count;
        Assert.True(first > 1);
        Assert.Equal([RowBox(-20, Math.Min(count + 2, 50 - first + 1) * 20)], RowMarks(cut));

        // A Row Heading's press selects the whole row: every column's Heading is marked, as one box.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 10, OffsetY = 45 });
        Assert.Equal([Box(40, 0, 600, HeaderPx)], ColumnMarks(cut));
        Assert.Equal([RowBox(40, 20)], RowMarks(cut));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0053: a run of rows wholly outside the painted rows has no box, and its columns' Headings are still marked
    public async Task ADR0008_rows_scrolled_away_take_no_box()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 1, 2);

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 400);

        Assert.Empty(RowMarks(cut));
        Assert.Equal([Box(240, 0, 100, HeaderPx)], ColumnMarks(cut));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0004: a Pinned Column's part of a run rides the band's pinned layer, cut at the boundary, and the rest passes beneath it
    public async Task ADR0008_the_pinned_columns_part_rides_the_pinned_layer()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.PinnedColumnCount, 2));

        await PressCellAsync(cut, 0, 1);
        await PressCellAsync(cut, 0, 3, shift: true);

        Assert.Equal([Box(140, 0, 100, HeaderPx)], PinnedColumnMarks(cut));
        Assert.Equal([Box(240, 0, 200, HeaderPx)], ColumnMarks(cut));
        // The pinned layer is the band's first child, so its place in the flow is the band's left edge.
        var header = cut.Find(".ex-header");
        Assert.Equal("ex-heading-marks-pinned", header.Children[0].ClassName);
        Assert.Equal("true", header.Children[0].GetAttribute("aria-hidden"));

        // A selection clear of the Pinned Columns leaves no pinned layer standing.
        await PressCellAsync(cut, 0, 4);
        Assert.Empty(cut.FindAll(".ex-heading-marks-pinned"));
        Assert.Equal([Box(440, 0, 100, HeaderPx)], ColumnMarks(cut));
        // And one inside them stands in the pinned layer alone.
        await PressCellAsync(cut, 0, 0);
        Assert.Equal([Box(40, 0, 100, HeaderPx)], PinnedColumnMarks(cut));
        Assert.Empty(ColumnMarks(cut));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0032: under Header Groups the box stands on the band's last tier, where the leaves stand, and the line lies on the band's bottom edge
    public async Task ADR0008_under_header_groups_the_mark_is_on_the_last_tier()
    {
        HeaderGroup[] groups = [new HeaderGroup("Group", [TestRows.ColumnName(1), TestRows.ColumnName(2)])];
        var cut = RenderGrid(ps => ps.Add(g => g.HeaderGroups, groups), rowHeadings: false);

        await PressCellAsync(cut, 0, 2, leadPx: 0);

        // Two tiers: the band is 48px, and the mark is its second 24.
        Assert.Contains("height: 48px", cut.Find(".ex-header").GetAttribute("style"));
        Assert.Equal([Box(200, HeaderPx, 100, HeaderPx)], ColumnMarks(cut));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0050: a hidden header carries no mark and the Row Headings still do; without Row Headings no layer stands for them
    public async Task ADR0008_each_band_is_marked_only_where_it_stands()
    {
        var hidden = RenderGrid(ps => ps.Add(g => g.HideHeader, true));
        await hidden.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 190, OffsetY = 30 });
        Assert.Empty(hidden.FindAll(".ex-header"));
        Assert.Empty(hidden.FindAll(".ex-heading-mark-column"));
        Assert.Equal([RowBox(20, 20)], RowMarks(hidden));

        var plain = RenderGrid(rowHeadings: false);
        await PressCellAsync(plain, 1, 1, leadPx: 0);
        Assert.Empty(plain.FindAll(".ex-heading-marks-rows"));
        Assert.Empty(plain.FindAll(".ex-heading-mark-row"));
        Assert.Equal([Box(100, 0, 100, HeaderPx)], ColumnMarks(plain));
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0003: the mark moves with the Selection and no row renders for it — rows still skip their render
    public async Task ADR0008_marking_headings_renders_no_row()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.HeaderClickSelects, true).Add(g => g.PinnedColumnCount, 1));
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();
        var marks = new HashSet<string>(StringComparer.Ordinal);
        void Note() => marks.Add(string.Join(" | ", cut.FindAll(".ex-heading-mark").Select(m => m.GetAttribute("style"))));

        await PressCellAsync(cut, 1, 1);
        Note();
        await PressCellAsync(cut, 3, 2, shift: true);
        Note();
        await PressCellAsync(cut, 6, 0, ctrl: true);
        Note();
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowDown", shift: true);
        Note();
        await ClickHeaderAsync(cut, 390);
        Note();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 10, OffsetY = 65 });
        Note();
        await ClickHeaderAsync(cut, 20);                            // the corner: every cell
        Note();

        // Seven Selections, seven different marks, and not one row rendered for any of them.
        Assert.Equal(7, marks.Count);
        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }
}
