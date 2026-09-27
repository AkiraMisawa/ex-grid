using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Headings declarations (ADR-0050, item 1; DC-1 to DC-6): a header click that
/// selects, Row Headings outside the column index space, the corner, and hiding either.
/// 50 rows of 20px, six 100px columns in a 350 × 200 Viewport; the Row Headings, where
/// declared, are 40px wide and label a row with its 1-based number.
/// </summary>
public class HeadingsTests : GridTestContext
{
    private static readonly Func<int, string> RowNumbers = row => (row + 1).ToString(CultureInfo.InvariantCulture);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null,
        Action<GridSelection>? onSelection = null,
        Action<IReadOnlyList<SortSpec>>? onSort = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, TestRows.Wide(6))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, onSelection ?? (_ => { }))
              .Add(g => g.OnSortChanged, onSort ?? (_ => { }));
            extra?.Invoke(ps);
        });

    private static void WithRowHeadings(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.RowHeadings, RowNumbers).Add(g => g.RowHeadingWidth, 40d);

    private static Task ClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, bool shift = false)
        => cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, ShiftKey = shift, OffsetX = x, OffsetY = 10 });

    private static Task PressViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, ShiftKey = shift, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    [Fact] // ADR-0050 / DC-1: with nothing declared, no heading, corner or bar is painted
    public void The_default_markup_gains_no_new_element()
    {
        var cut = RenderGrid();

        Assert.DoesNotContain("ex-row-heading", cut.Markup);
        Assert.DoesNotContain("ex-headings-corner", cut.Markup);
        Assert.DoesNotContain("ex-formula-bar", cut.Markup);
        Assert.Single(cut.FindAll(".ex-header"));
    }

    [Fact] // ADR-0050 / DC-1: undeclared, the header click still sorts and selects nothing
    public async Task Undeclared_the_header_click_still_sorts()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(onSelection: s => selection = s, onSort: s => sorted = s);

        await ClickHeaderAsync(cut, 150);

        Assert.NotNull(sorted);
        Assert.Null(selection);
    }

    [Fact] // ADR-0050 / DC-2: declared, a plain header click selects the whole column and nothing sorts
    public async Task Declared_a_header_click_selects_the_whole_column()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(ps => ps.Add(g => g.HeaderClickSelects, true), s => selection = s, s => sorted = s);

        await ClickHeaderAsync(cut, 150);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 1, 50, 1)], selection!.Ranges);
        // Anchored on the first visible row: the Viewport does not move for a header click.
        Assert.Equal(new CellPosition(0, 1), selection.Focus);
        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0050 / DC-2: Shift+click extends whole columns from the Anchor's column
    public async Task Declared_shift_click_on_a_header_extends_from_the_anchor_column()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(ps => ps.Add(g => g.HeaderClickSelects, true), s => selection = s, s => sorted = s);

        await ClickHeaderAsync(cut, 150);
        await ClickHeaderAsync(cut, 320, shift: true);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 1), selection.Anchor);
    }

    [Fact] // ADR-0050 / DC-3: a Row Heading beside every painted row, with the Consumer's label and the resolved width inline
    public void Row_headings_are_painted_beside_every_row_with_the_consumer_label()
    {
        var cut = RenderGrid(WithRowHeadings);

        var rows = cut.FindAll(".ex-viewport > .ex-row");
        var headings = cut.FindAll(".ex-row > .ex-row-heading");
        Assert.NotEmpty(rows);
        Assert.Equal(rows.Count, headings.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var index = int.Parse(rows[i].GetAttribute("aria-rowindex")!, CultureInfo.InvariantCulture);
            // First in the row's flow, so every column after it starts where the band ends.
            Assert.Same(headings[i], rows[i].FirstElementChild);
            Assert.Equal(index.ToString(CultureInfo.InvariantCulture), headings[i].TextContent);
            Assert.Equal("width: 40px", headings[i].GetAttribute("style"));
        }
        Assert.Single(cut.FindAll(".ex-header > .ex-headings-corner"));
    }

    [Fact] // ADR-0050 / DC-3: a click on a Row Heading selects the whole row; Shift+click extends
    public async Task A_row_heading_click_selects_the_row_and_shift_extends()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(WithRowHeadings, s => selection = s);

        await PressViewportAsync(cut, 10, 45);
        Assert.Equal([new SelectionRange(2, 0, 1, 6)], selection!.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Focus);

        await PressViewportAsync(cut, 10, 105, shift: true);
        Assert.Equal([new SelectionRange(2, 0, 4, 6)], selection.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Anchor);
        Assert.Equal(new CellPosition(5, 0), selection.Focus);
    }

    [Fact] // ADR-0050 / DC-3: the corner where the two Headings meet selects all
    public async Task The_corner_selects_all()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(WithRowHeadings, s => selection = s, s => sorted = s);

        await ClickHeaderAsync(cut, 20);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 0, 50, 6)], selection!.Ranges);
    }

    [Fact] // ADR-0050/0008: a press past the band lands on the column the geometry puts there, offset by the band
    public async Task Cells_start_after_the_band_and_the_overlay_follows()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(WithRowHeadings, s => selection = s);

        // x 45 is 5px into column 0, which now starts at 40.
        await PressViewportAsync(cut, 45, 5);

        Assert.Equal(new CellPosition(0, 0), selection!.Focus);
        Assert.Contains("left: 40px", cut.Find(".ex-focus").GetAttribute("style"));
    }

    [Fact] // ADR-0050/0004: Pinned Columns stick clear of the band, at their offset after it
    public void Pinned_columns_stick_after_the_band()
    {
        var cut = RenderGrid(ps =>
        {
            WithRowHeadings(ps);
            ps.Add(g => g.PinnedColumnCount, 1);
        });

        Assert.Equal("width: 100px; left: 40px", cut.Find(".ex-row .ex-pinned").GetAttribute("style"));
        Assert.Equal("width: 100px; left: 40px", cut.Find(".ex-header .ex-pinned").GetAttribute("style"));
    }

    [Fact] // ADR-0050/0011/0012 / DC-4: Ctrl+A then copy carries no heading label; Tab never lands on a heading
    public async Task Row_headings_are_outside_selection_copy_select_all_and_the_cycle()
    {
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(3))
            .Add(g => g.TotalCount, 3)
            .Add(g => g.Columns, TestRows.Wide(2))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.RowHeadings, RowNumbers)
            .Add(g => g.RowHeadingWidth, 40d)
            .Add(g => g.SelectionChanged, s => selection = s));

        await PressViewportAsync(cut, 45, 5);
        await PressAsync(cut, "a", ctrl: true);
        Assert.Equal([new SelectionRange(0, 0, 3, 2)], selection!.Ranges);

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
        Assert.Equal(
            "Row 000000/00\tRow 000000/01\r\nRow 000001/00\tRow 000001/01\r\nRow 000002/00\tRow 000002/01\r\n",
            payload.Text);

        var visited = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            await PressAsync(cut, "Tab");
            visited.Add(selection.Focus.Column);
        }
        Assert.All(visited, column => Assert.InRange(column, 0, 1));
    }

    [Fact] // ADR-0050/0028 / DC-5: hiding the header takes the band away and the rows start at the top
    public async Task Hiding_the_header_removes_the_band_and_the_geometry_follows()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps.Add(g => g.HideHeader, true), s => selection = s);

        Assert.Empty(cut.FindAll(".ex-header"));
        Assert.StartsWith("top: 0px;", cut.Find(".ex-viewport").GetAttribute("style"));
        // 200px of rows, not 180: ten rows fit, so PageDown moves ten.
        await PressViewportAsync(cut, 50, 5);
        await PressAsync(cut, "PageDown");
        Assert.Equal(new CellPosition(10, 0), selection!.Focus);
        // The spacer is the rows' height alone.
        Assert.Contains("height: 1000px", cut.Find(".ex-spacer").GetAttribute("style"));
    }

    [Fact] // ADR-0050 / DC-5: Row Headings hide by not being declared, and the geometry is as before
    public async Task Without_row_headings_the_first_column_starts_at_the_edge()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelection: s => selection = s);

        await PressViewportAsync(cut, 10, 5);

        Assert.Equal(new CellPosition(0, 0), selection!.Focus);
        Assert.StartsWith("left: 0px;", cut.Find(".ex-focus").GetAttribute("style"));
        Assert.DoesNotContain("ex-row-heading", cut.Markup);
    }

    [Fact] // ADR-0050/0028 / DC-6: undeclared, the width is resolved from the Cell Metrics, inline
    public void The_default_width_is_estimated_from_the_labels()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.RowHeadings, RowNumbers));

        var expected = ExGrid<TestRow>.DefaultCellMetrics.EstimatePx("50");
        Assert.Equal(FormattableString.Invariant($"width: {expected}px"), cut.Find(".ex-row-heading").GetAttribute("style"));
    }

    [Fact] // ADR-0027/0028 / DC-6: the stylesheet sizes no heading — no literal to pair with the C# width
    public void The_stylesheet_carries_no_heading_width()
    {
        var css = ShippedStylesheet();
        var rules = Regex.Matches(css, @"([^{}]*ex-(row-heading|headings-corner)[^{}]*)\{([^}]*)\}");

        Assert.NotEmpty(rules);
        foreach (Match rule in rules)
            Assert.DoesNotMatch(@"(^|[;\s])(min-|max-)?width\s*:", rule.Groups[3].Value);
    }

    [Fact] // ADR-0003 / DC-3: a Row Heading click re-renders no row
    public async Task Selecting_by_a_row_heading_renders_no_row()
    {
        var cut = RenderGrid(WithRowHeadings);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressViewportAsync(cut, 10, 45);
        await ClickHeaderAsync(cut, 20);

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    private static string ShippedStylesheet()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ExGrid.staticwebassets.runtime.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return document.RootElement.GetProperty("ContentRoots").EnumerateArray()
            .Select(root => root.GetString()!)
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "ex-grid.css", SearchOption.AllDirectories))
            .Select(File.ReadAllText)
            .First();
    }
}
