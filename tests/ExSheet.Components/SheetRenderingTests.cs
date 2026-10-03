using Bunit;
using ExGrid.Components;
using ExSheet;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The tracer (ticket 02): a Sheet typed into and drawn through one ExGrid, as that grid's
/// Consumer (ADR-0046). 28 px rows in a 400 × 700 box under the Formula Bar and the header.
/// </summary>
public class SheetRenderingTests : SheetTestContext
{
    [Fact] // ADR-0046: one ExGrid, Columns A … XFD, Excel's 1,048,576 rows
    public void A_sheet_renders_one_grid_over_excels_extent()
    {
        var cut = RenderSheet();

        Assert.Single(cut.FindComponents<ExGrid<SheetRow>>());
        var root = cut.Find(".ex-grid");
        Assert.Equal("1048576", root.GetAttribute("aria-rowcount"));
        Assert.Equal("16384", root.GetAttribute("aria-colcount"));
        var headers = cut.FindAll(".ex-header [role=columnheader]").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(["A", "B", "C"], headers.Take(3));
    }

    [Fact] // ADR-0046 / ticket 02: typing a constant and a Formula shows the Value
    public async Task Typing_constants_and_a_formula_shows_its_value()
    {
        var cut = RenderSheet();

        await EnterAsync(cut, "A1", "2");
        await EnterAsync(cut, "B1", "3");
        await EnterAsync(cut, "C1", "=A1+B1");

        Assert.Equal("5", CellText(cut, "C1"));
        await EnterAsync(cut, "A1", "10");
        Assert.Equal("13", CellText(cut, "C1"));
    }

    [Fact] // ADR-0046/0003, SH-4: an edit repaints only the rows whose Values changed
    public async Task An_edit_repaints_only_the_rows_whose_values_changed()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "2");
        await EnterAsync(cut, "C3", "=A1*2");
        await EnterAsync(cut, "B5", "text");
        var before = cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));

        await EnterAsync(cut, "A1", "7");

        var after = cut.FindComponents<ExGridRow<SheetRow>>().ToDictionary(r => r.Instance.RowIndex, r => (r.Instance.Row, r.RenderCount));
        Assert.Equal("14", CellText(cut, "C3"));
        foreach (var (index, (row, count)) in after)
        {
            if (index is 0 or 2)
            {
                // A new instance for each changed row: painted once, as a new row.
                Assert.NotSame(before[index].Row, row);
                continue;
            }
            Assert.Same(before[index].Row, row);
            Assert.Equal(before[index].RenderCount, count);
        }
    }

    [Fact] // ADR-0046/0053, SH-3: a row height at which 1,048,576 rows pass the scroll ceiling is refused by name
    public void A_row_height_past_the_scroll_ceiling_is_refused_by_name()
    {
        var error = Assert.ThrowsAny<ArgumentOutOfRangeException>(() => RenderSheet(ps => ps.Add(s => s.RowHeight, 32d)));

        Assert.Contains("32px", error.Message);
        Assert.Contains("33,554,428px", error.Message);
        Assert.Contains("1,048,576", error.Message);
    }

    [Theory] // ADR-0046, SH-3: every height up to 31 px fits, 28 px included
    [InlineData(28d)]
    [InlineData(31d)]
    public void A_row_height_that_fits_is_taken(double height)
    {
        var cut = RenderSheet(ps => ps.Add(s => s.RowHeight, height));

        Assert.NotEmpty(cut.FindAll(".ex-row"));
    }

    [Fact] // ADR-0046, SH-2: the DOM does not grow with the extent — the same bound at A1 and at XFD1048576
    public async Task The_dom_is_bounded_at_the_top_and_at_the_far_corner()
    {
        var cut = RenderSheet();
        var atTop = cut.FindAll("*").Count;
        var cellsAtTop = cut.FindAll("[role=gridcell]").Count;

        await GoToAsync(cut, "XFD1048576");
        await ScrollToAsync(cut, (Sheet.RowCount - 1) * 28d, (Sheet.ColumnCount - 1) * SheetColumns.DefaultWidthPx);

        Assert.Contains(cut.FindAll(".ex-row"), r => r.GetAttribute("aria-rowindex") == "1048576");
        Assert.Contains(cut.FindAll("[role=columnheader]"), h => h.TextContent.Trim() == "XFD");
        var atCorner = cut.FindAll("*").Count;
        var cellsAtCorner = cut.FindAll("[role=gridcell]").Count;
        Assert.True(cellsAtTop < 1_000, $"{cellsAtTop} cells at the top");
        Assert.True(cellsAtCorner <= cellsAtTop, $"{cellsAtCorner} cells at the corner, {cellsAtTop} at the top");
        Assert.True(atCorner <= atTop + 20, $"{atCorner} elements at the corner, {atTop} at the top");
    }

    [Fact] // ADR-0046: ExGrid's sort and filter are not wired — a header click selects, nothing sorts, the column menu cannot sort
    public async Task Sort_and_filter_are_not_wired()
    {
        var cut = RenderSheet();

        await ClickHeaderAsync(cut, 200);

        Assert.Empty(cut.FindAll("[aria-sort=ascending], [aria-sort=descending]"));
        Assert.False(Grid(cut).Instance.OnSortChanged.HasDelegate);
        Assert.False(Grid(cut).Instance.OnFilterChanged.HasDelegate);
        // Nor is there a column menu to reach them from: the Headings are Excel's (ADR-0050 item 12).
        Assert.Empty(cut.FindAll(".ex-menu-button"));
    }

    [Fact] // ADR-0046: Pinned Columns work on a Sheet — the leading columns stay painted when scrolled sideways
    public async Task Pinned_columns_stay_while_scrolling_sideways()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.PinnedColumnCount, 2));
        await EnterAsync(cut, "A1", "label");

        await ScrollToAsync(cut, 0, 500 * SheetColumns.DefaultWidthPx);

        var headers = cut.FindAll(".ex-header [role=columnheader]").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(["A", "B"], headers.Take(2));
        Assert.DoesNotContain("C", headers);
        Assert.Equal("label", CellText(cut, "A1"));
    }
}
