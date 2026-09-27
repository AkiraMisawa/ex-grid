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
/// Editing in the Formula Bar, and the Name Box moving the Selection (ticket 09, ADR-0050
/// item 4, ADR-0051).
/// </summary>
public class NameBoxWiringTests : SheetTestContext
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

    [Fact] // ADR-0051: typing in the Formula Bar and committing enters the cell once
    public async Task Typing_in_the_formula_bar_enters_the_cell()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "21")))
            .Add(s => s.DocumentChanged, raised.Add));
        await GoToAsync(cut, "B1");

        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "=A1+1");
        await PressAsync(cut, "Enter");

        Assert.Equal("22", CellText(cut, "B1"));
        Assert.Single(raised);
    }

    [Theory] // ADR-0050 item 4, ADR-0051: the Name Box reads an address as Excel's does and moves the Selection there
    [InlineData("d200", 199, 3, 1, 1)]
    [InlineData("A1:C3", 0, 0, 3, 3)]
    [InlineData("$C$3", 2, 2, 1, 1)]
    [InlineData(" B2 ", 1, 1, 1, 1)]
    [InlineData("B:D", 0, 1, 1_048_576, 3)]
    [InlineData("5:6", 4, 0, 2, 16_384)]
    [InlineData("XFD1048576", 1_048_575, 16_383, 1, 1)]
    public async Task The_name_box_moves_the_selection(string typed, int top, int left, int rows, int columns)
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));

        await GoToAsync(cut, typed);

        Assert.Equal(new SelectionRange(top, left, rows, columns), Assert.Single(selections[^1].Ranges));
        Assert.Equal(new CellAddress(top, left).ToString(), NameBox(cut));
        Assert.Equal("", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Theory] // ADR-0051, principle 1: an unreadable address does nothing, and says so
    [InlineData("zz")]
    [InlineData("A0")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    [InlineData("Sheet1!A1")]
    public async Task An_unreadable_address_moves_nothing_and_says_so(string typed)
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "B2");
        var before = selections.Count;

        await GoToAsync(cut, typed);

        Assert.Equal(before, selections.Count);
        Assert.Contains($"'{typed}' is not a cell address", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Fact] // ADR-0051 / ADR-0050 item 4 / ADR-0010: with an edit open, an address typed into the Name Box navigates; it is never written into the Formula
    public async Task An_address_typed_with_an_edit_open_navigates()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "B1");
        await PressAsync(cut, "=");
        await TypeAsync(cut, "=1+2");
        var focusCalls = JSInterop.Invocations.Count(i => i.Identifier is "Blazor._internal.domWrapper.focus" or "reclaimFocus");

        await cut.Find(".ex-name-box").FocusAsync(new FocusEventArgs());
        // The press keeps its meaning: the keyboard stays in the Name Box, so the address
        // typed next is the Name Box's, not a key on the grid that opens Overwrite.
        Assert.Equal(focusCalls, JSInterop.Invocations.Count(i => i.Identifier is "Blazor._internal.domWrapper.focus" or "reclaimFocus"));
        await GoToAsync(cut, "D200");

        Assert.Equal(new SelectionRange(199, 3, 1, 1), Assert.Single(selections[^1].Ranges));
        Assert.Equal("D200", NameBox(cut));
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        await GoToAsync(cut, "B1");
        Assert.Equal("=1+2", FormulaBar(cut));
        Assert.Equal("3", CellText(cut, "B1"));
    }

    [Fact] // ADR-0052 case 2: while a drag's button is down the Name Box shows Excel's size, 4R x 3C, and names the Focus again on release
    public async Task The_name_box_shows_the_size_of_a_drag_while_the_button_is_down()
    {
        var cut = RenderSheet();
        var width = SheetColumns.DefaultWidthPx;
        var row = ExSheet.DefaultRowHeightPx;
        double X(int column) => HeadingWidth(cut) + (column * width) + (width / 2);
        double Y(int r) => (r * row) + (row / 2);
        var viewport = cut.Find(".ex-viewport");

        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = X(3), OffsetY = Y(4) });   // D5
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = X(1), OffsetY = Y(1) }); // B2
        Assert.Equal("4R x 3C", NameBox(cut));

        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = X(1), OffsetY = Y(1) });
        Assert.Equal("D5", NameBox(cut));
    }
}
