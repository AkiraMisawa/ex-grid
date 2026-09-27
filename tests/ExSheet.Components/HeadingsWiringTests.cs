using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Headings on a Sheet (ticket 06): column letters and row numbers framing it, a click on
/// either selecting whole columns or rows, and either one hidden (ADR-0050, item 1).
/// </summary>
public class HeadingsWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static string NameBox(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-name-box").GetAttribute("value") ?? "";

    private static string FormulaBar(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    private static double HeadingWidth(IRenderedComponent<ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    private static Task PressViewportAsync(IRenderedComponent<ExSheet> cut, double x, double y, bool shift = false) =>
        cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, ShiftKey = shift, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0050 item 1: Column Headings are the letters, Row Headings the 1-based row numbers
    public void The_headings_are_letters_and_row_numbers()
    {
        var cut = RenderSheet();

        var labels = cut.FindAll(".ex-row-heading").Select(h => h.TextContent).Take(3).ToList();
        Assert.Equal(["1", "2", "3"], labels);
        Assert.Equal("A", cut.FindAll(".ex-header [role=columnheader]")[0].TextContent.Trim());
    }

    [Fact] // ADR-0050 item 1, DC-2: a header click selects the whole column and Shift+click extends; nothing sorts
    public async Task A_header_click_selects_columns()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        var lead = HeadingWidth(cut);

        await ClickHeaderAsync(cut, lead + 1.5 * SheetColumns.DefaultWidthPx);
        await ClickHeaderAsync(cut, lead + 3.5 * SheetColumns.DefaultWidthPx, shift: true);

        var range = Assert.Single(selections[^1].Ranges);
        Assert.Equal(new SelectionRange(0, 1, Sheet.RowCount, 3), range);
    }

    [Fact] // ADR-0050 item 1, DC-3: a Row Heading click selects the whole row, Shift+click extends
    public async Task A_row_heading_click_selects_rows()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));

        await PressViewportAsync(cut, 5, 28 * 2 + 5);
        await PressViewportAsync(cut, 5, 28 * 4 + 5, shift: true);

        var range = Assert.Single(selections[^1].Ranges);
        Assert.Equal(new SelectionRange(2, 0, 3, Sheet.ColumnCount), range);
    }

    [Fact] // ADR-0050 item 1: hiding the Row Headings takes the band away; the Sheet still addresses A1
    public async Task Hidden_row_headings_leave_the_addresses()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.ShowRowHeadings, false));

        await GoToAsync(cut, "C3");

        Assert.Empty(cut.FindAll(".ex-row-heading"));
        Assert.Equal("C3", NameBox(cut));
    }

    [Fact] // ADR-0050 item 1, DC-5: hiding the Column Headings takes the header band away; the Sheet still addresses A1
    public async Task Hidden_column_headings_leave_the_addresses()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.ShowColumnHeadings, false));

        await GoToAsync(cut, "B7");

        Assert.Empty(cut.FindAll(".ex-header"));
        Assert.Equal("B7", NameBox(cut));
    }

    [Fact] // ADR-0051: the Formula Bar can be hidden
    public void The_formula_bar_hides()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.ShowFormulaBar, false));

        Assert.Empty(cut.FindAll(".ex-formula-bar"));
        Assert.NotEmpty(cut.FindAll(".ex-row"));
    }
}
