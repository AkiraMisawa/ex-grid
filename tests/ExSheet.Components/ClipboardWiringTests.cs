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
/// Sheet's culture; a paste is one undo step; a copy is the engine's, answered through ExGrid's
/// copy answer (ADR-0050 item 9): the Values outward, the Entries kept for a paste back.
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
        Assert.Equal("<table data-ex-grid=\"invariant\"><tr><td>0.5</td><td>hello</td><td>1</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0050 item 9, ADR-0005: a copy running beyond the Window is answered by the engine on the synchronous route
    public async Task A_copy_beyond_the_window_is_answered_by_the_engine()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "first"), ("A1000", "last"))));
        await GoToAsync(cut, "A1:A1000");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("data", payload.Kind);
        var lines = payload.Text!.Split("\r\n");
        Assert.Equal("first", lines[0]);
        Assert.Equal("last", lines[999]);
    }

    [Fact] // ADR-0050 item 9, DC-32: the menu's asynchronous route asks the same answer
    public async Task The_asynchronous_copy_route_writes_the_engines_copy()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "50%"), ("A2", "=1/3"))));
        await GoToAsync(cut, "A1:A2");

        var grid = Grid(cut);
        var payload = await grid.InvokeAsync(() => grid.Instance.BuildCopyPayloadAsync());

        Assert.Equal("50%\r\n0.333333333333333\r\n", payload!.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\"><tr><td>0.5</td></tr><tr><td>0.3333333333333333</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0048, ADR-0050 item 9: a copy keeps its Entries, and the fields the grid's paste would read back from it
    public async Task A_copy_keeps_its_entries_and_the_fields_it_wrote()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "2"), ("B1", "=A1*2"))));
        await GoToAsync(cut, "A1:B1");

        Grid(cut).Instance.BuildCopyPayload();

        var own = cut.Instance.OwnCopy!;
        Assert.Equal(CellRange.Parse("A1:B1"), own.Block.Source);
        Assert.Equal("=A1*2", own.Block.EntryAt(0, 1)!.Formula);
        Assert.Equal(["2", "4"], Assert.Single(own.Fields));
    }

    [Fact] // ADR-0049, SH-16, DC-32: a copy reaching a waiting cell is refused in the engine's words, and nothing is written
    public async Task A_copy_reaching_getting_data_is_refused_in_the_engines_words()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "=SUM(Positions[PV])"))));
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["PV"]);
        await GoToAsync(cut, "A1:A2");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("none", payload.Kind);
        cut.WaitForAssertion(() => Assert.Contains("A2 is waiting for a Linked Table's data (#GETTING_DATA)", Notice(cut)));
        Assert.Null(cut.Instance.OwnCopy);
    }

    [Fact] // ADR-0049: a refused copy leaves the last copy's Entries standing, as the clipboard still holds it
    public async Task A_refused_copy_keeps_the_last_copy()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "=SUM(Positions[PV])"))));
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["PV"]);
        await GoToAsync(cut, "A1");
        Grid(cut).Instance.BuildCopyPayload();
        await GoToAsync(cut, "A2");

        Assert.Equal("none", Grid(cut).Instance.BuildCopyPayload().Kind);

        Assert.Equal(CellRange.Parse("A1"), cut.Instance.OwnCopy!.Block.Source);
    }

    [Fact] // ADR-0005, ADR-0016: a number no format can show goes out as its Value, never as ####
    public async Task A_number_that_cannot_be_shown_goes_out_as_its_value()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "-5");
        sheet.SetFormat([CellAddress.Parse("A1")], NumberFormat.Parse("yyyy-mm-dd"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await GoToAsync(cut, "A1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("-5\r\n", payload.Text);
        Assert.DoesNotContain("#", payload.Html!, StringComparison.Ordinal);
    }

    [Fact] // ADR-0005, ADR-0048: ranges combined into one block carry their Values, and no Entries
    public async Task Several_ranges_carry_values_and_no_entries()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("C1", "=A1+2"))));
        await GoToAsync(cut, "A1");
        await CtrlClickAsync(cut, "C1");

        var payload = Grid(cut).Instance.BuildCopyPayload();

        Assert.Equal("1\t3\r\n", payload.Text);
        Assert.Equal("<table data-ex-grid=\"invariant\"><tr><td>1</td><td>3</td></tr></table>", payload.Html);
        Assert.Null(cut.Instance.OwnCopy);
    }

    private static async Task CtrlClickAsync(IRenderedComponent<ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        var heading = double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);
        await cut.Find(".ex-viewport").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs
        {
            Button = 0, Buttons = 1, CtrlKey = true,
            OffsetX = heading + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * ExSheet.DefaultRowHeightPx + 5,
        });
        await cut.Find(".ex-viewport").MouseUpAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });
    }
}
