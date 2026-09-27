using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The clipboard as ExSheet wires it (ticket 14, ADR-0048/0050/0005): a paste may spill from one
/// cell and the block becomes the Selection; every pasted field is taken as if typed under the
/// Sheet's culture; a paste is one undo step; the copy's <c>text/html</c> flavour carries the
/// unformatted Values. The copy of Entries inside ExSheet is not wired: ExGrid assembles a copy
/// from the columns, and a Consumer cannot supply it (the ticket says what is missing).
/// </summary>
public class ClipboardWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static Task PasteAsync(IRenderedComponent<ExSheet> cut, string text, string? html = null)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnPasteAsync(text, html));
    }

    private static string FormulaBar(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    [Fact] // ADR-0048: a Formula and a number pasted from another program are taken as typed under the culture
    public async Task Fields_from_outside_are_taken_as_typed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "41"))));
        await GoToAsync(cut, "B1:C1");

        await PasteAsync(cut, "=A1+1\t\"1,234\"\r\n");

        Assert.Equal("42", CellText(cut, "B1"));
        Assert.Equal("1234", CellText(cut, "C1"));
        await GoToAsync(cut, "B1");
        Assert.Equal("=A1+1", FormulaBar(cut));
    }

    [Fact] // ADR-0050 item 3: a 3×3 block pasted onto one cell writes 3×3 and becomes the Selection
    public async Task A_block_onto_one_cell_spills_and_is_selected()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps.Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "B2");

        await PasteAsync(cut, "1\t2\t3\r\n4\t5\t6\r\n7\t8\t9\r\n");

        Assert.Equal("1", CellText(cut, "B2"));
        Assert.Equal("9", CellText(cut, "D4"));
        Assert.Equal(new SelectionRange(1, 1, 3, 3), Assert.Single(selections[^1].Ranges));
        Assert.Equal(new CellPosition(1, 1), selections[^1].Focus);
    }

    [Fact] // ADR-0048: a paste is one undo step, however many cells it writes
    public async Task A_paste_is_one_undo_step()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B2", "old"))));
        await GoToAsync(cut, "B2");
        await PasteAsync(cut, "1\t2\r\n3\t4\r\n");

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal("old", CellText(cut, "B2"));
        Assert.Equal("", CellText(cut, "C3"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0014: one value over a selected range fills every cell of it
    public async Task One_value_fills_the_selected_range()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1:A3");

        await PasteAsync(cut, "7\r\n");

        Assert.Equal("7", CellText(cut, "A1"));
        Assert.Equal("7", CellText(cut, "A3"));
    }

    [Fact] // ADR-0050 item 3: a spill past the Sheet's edge is refused by name, and nothing is written
    public async Task A_spill_past_the_edge_is_refused_by_name()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1048576");

        await PasteAsync(cut, "1\r\n2\r\n");

        Assert.Contains("past the Sheet's edge", Notice(cut));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0047: a pasted Formula that cannot be read refuses the whole paste, and says why
    public async Task An_unreadable_formula_refuses_the_whole_paste()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1:B1");

        await PasteAsync(cut, "5\t=SUM(\r\n");

        Assert.StartsWith("Nothing was pasted: a pasted Formula cannot be read", Notice(cut));
        Assert.Equal("", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0005/0048: outward, the text flavour is the Values as shown and the HTML flavour the unformatted Values
    public async Task A_copy_carries_the_values_shown_and_unformatted()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "50%"), ("B1", "hello"), ("C1", "=2-1"))));
        await GoToAsync(cut, "A1:C1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("data", payload.Kind);
        Assert.Equal("50%\thello\t1\r\n", payload.Text);
        Assert.Equal("<table><tr><td>0.5</td><td>hello</td><td>1</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0005: a copy running beyond the Window is answered from the Sheet, never refused as unavailable
    public async Task A_copy_beyond_the_window_is_answered_from_the_sheet()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "first"), ("A1000", "last"))));
        await GoToAsync(cut, "A1:A1000");

        var grid = Grid(cut);
        Assert.Equal("async", grid.Instance.BuildCopyPayload().Kind);
        var payload = await grid.InvokeAsync(() => grid.Instance.BuildCopyPayloadAsync());

        var lines = payload!.Text!.Split("\r\n");
        Assert.Equal("first", lines[0]);
        Assert.Equal("last", lines[999]);
    }
}
