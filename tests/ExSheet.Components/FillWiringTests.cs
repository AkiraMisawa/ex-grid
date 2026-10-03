using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The fill handle as ExSheet wires it (ticket 15, ADR-0050 item 5): the grid owns the gesture and
/// raises a Fill Intent, and ExSheet fills as Excel fills through the engine, as one undo step,
/// or refuses the pattern and says why, leaving the Selection on the source.
/// </summary>
public class FillWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static double HeadingWidth(IRenderedComponent<ExSheet> cut) =>
        double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

    /// <summary>
    /// Selects <paramref name="source"/> with the Name Box, grabs the fill handle at its
    /// bottom-right corner and drags it down to the row of <paramref name="to"/>, in the source's
    /// last column, and releases it there.
    /// </summary>
    private static async Task DragDownAsync(IRenderedComponent<ExSheet> cut, string source, string to)
    {
        await GoToAsync(cut, source);
        var range = CellRange.Parse(source);
        var end = CellAddress.Parse(to);
        var width = SheetColumns.DefaultWidthPx;
        var row = ExSheet.DefaultRowHeightPx;
        var viewport = cut.Find(".ex-viewport");
        var cornerX = HeadingWidth(cut) + (range.Last.Column + 1) * width;
        var cornerY = (range.Last.Row + 1) * row;
        await viewport.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = cornerX - 1, OffsetY = cornerY - 1 });
        var x = cornerX - width / 2;
        var y = end.Row * row + row / 2;
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = x, OffsetY = y });
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    [Fact] // ADR-0050 item 5: ExSheet declares fill, so the handle is painted at the Selection's corner
    public async Task The_handle_is_painted()
    {
        var cut = RenderSheet();

        await GoToAsync(cut, "A1:A2");

        Assert.Single(cut.FindAll(".ex-fill-handle"));
    }

    [Fact] // ADR-0050 item 5, SH-15: 1, 2 dragged gives 1, 2, 3, 4, and the Selection covers source and target
    public async Task One_two_continues_as_a_series()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2")))
            .Add(s => s.SelectionChanged, selections.Add));

        await DragDownAsync(cut, "A1:A2", "A4");

        Assert.Equal("3", CellText(cut, "A3"));
        Assert.Equal("4", CellText(cut, "A4"));
        Assert.Equal([new SelectionRange(0, 0, 4, 1)], selections[^1].Ranges);
    }

    [Fact] // ADR-0050 item 5, SH-15: a date goes on by day
    public async Task A_date_goes_on_by_day()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "9/26/2026"))));

        await DragDownAsync(cut, "A1", "A3");

        Assert.Equal("9/27/2026", CellText(cut, "A2"));
        Assert.Equal("9/28/2026", CellText(cut, "A3"));
    }

    [Fact] // ADR-0050 item 5, SH-15: a Formula is copied with its References shifted
    public async Task A_formula_shifts_its_references()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"), ("A3", "3"), ("B1", "=A1*10"))));

        await DragDownAsync(cut, "B1", "B3");

        Assert.Equal("20", CellText(cut, "B2"));
        Assert.Equal("30", CellText(cut, "B3"));
    }

    [Fact] // ADR-0050 item 5, SH-15: Item 1 is refused, not filled with copies; the user is told and the Selection stays
    public async Task Item_1_is_refused_and_the_user_is_told()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "Item 1")))
            .Add(s => s.SelectionChanged, selections.Add));

        await DragDownAsync(cut, "A1", "A3");

        Assert.Equal("", CellText(cut, "A2"));
        Assert.Contains("A1", cut.Find(".ex-sheet-notice").TextContent);
        Assert.Equal([new SelectionRange(0, 0, 1, 1)], selections[^1].Ranges);
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0048, SH-13: a fill is one undo step
    public async Task A_fill_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"))));
        await DragDownAsync(cut, "A1:A2", "A5");

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal("", CellText(cut, "A3"));
        Assert.Equal("", CellText(cut, "A5"));
        Assert.Equal("2", CellText(cut, "A2"));
        Assert.False(cut.Instance.CanUndo);
    }
}
