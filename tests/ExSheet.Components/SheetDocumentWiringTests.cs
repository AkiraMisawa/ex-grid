using System.Globalization;
using Bunit;
using ExGrid.Clipboard;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Sheet Document between ExSheet and its Consumer (ADR-0048): raised after every change,
/// handed in to replace the Sheet, and recognised when it comes back.
/// </summary>
public class SheetDocumentWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(CultureInfo culture, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(culture);
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    [Fact] // ADR-0048: every change raises the Sheet Document — Entries, never Values
    public async Task Every_change_raises_the_sheet_document()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, raised.Add));

        await EnterAsync(cut, "A1", "2");
        await EnterAsync(cut, "B1", "=A1*21");

        Assert.Equal(2, raised.Count);
        var cells = raised[^1].Cells.ToDictionary(c => c.Address.ToString(), c => c.Entry!.ToString());
        Assert.Equal("=A1*21", cells["B1"]);
        Assert.DoesNotContain("42", raised[^1].ToJson());
    }

    [Fact] // ADR-0048, SH-17: a document handed in shows the same Values the engine alone computes
    public void A_document_handed_in_shows_the_engines_values()
    {
        var document = DocumentOf(CultureInfo.GetCultureInfo("en-US"), ("A1", "1,234.5"), ("A2", "=A1*2"));

        var cut = RenderSheet(ps => ps.Add(s => s.Document, document));

        var alone = Sheet.Open(SheetDocument.FromJson(document.ToJson()));
        Assert.Equal(alone.GetDisplay(CellAddress.Parse("A1")).Text, CellText(cut, "A1"));
        Assert.Equal(alone.GetDisplay(CellAddress.Parse("A2")).Text, CellText(cut, "A2"));
        // A product takes the format of the cell it reads on entry (ADR-0047, second run): #,##0.00.
        Assert.Equal("2,469.00", CellText(cut, "A2"));
    }

    /// <summary>
    /// A document as a Consumer would have saved it, the headless suite's (ExSheet.Engine.Tests'
    /// HeadlessTests) in version 5: a culture that is not the component's, formats, a date, a
    /// cycle, an error and a Linked Table nobody declared, with column B wide enough that
    /// nothing in it is fitted.
    /// </summary>
    private const string Saved = """
        {
          "version": 5,
          "name": "Sheet1",
          "culture": "de-DE",
          "columnWidths": [ { "at": "B:B", "width": 20, "custom": true } ],
          "cells": [
            { "at": "A1", "text": "Nominal" },
            { "at": "B1", "number": 1234.5, "format": "#,##0.00" },
            { "at": "A2", "text": "Rate" },
            { "at": "B2", "number": 0.0375, "format": "0.00%" },
            { "at": "A3", "text": "Start" },
            { "at": "B3", "number": 46292, "format": "dd.mm.yyyy" },
            { "at": "A4", "text": "Interest" },
            { "at": "B4", "formula": "=ROUND(B1*B2,2)", "format": "#,##0.00" },
            { "at": "B5", "formula": "=B3+30", "format": "dd.mm.yyyy" },
            { "at": "B6", "formula": "=IF(B4>40,\"high\",\"low\")" },
            { "at": "B7", "formula": "=SUM(B1,B4)/COUNT(B1:B4)" },
            { "at": "B8", "formula": "=XLOOKUP(\"Rate\",A1:A4,B1:B4)" },
            { "at": "B9", "formula": "=B10+1" },
            { "at": "B10", "formula": "=B9+1" },
            { "at": "B11", "formula": "=1/0" },
            { "at": "B12", "formula": "=IFERROR(B11,-1)" },
            { "at": "B13", "formula": "=SUM(Positions[PV])" }
          ]
        }
        """;

    [Fact] // ADR-0047/0048, SH-17 (ticket 17): a saved document's Values on screen are the ones ExSheet.Engine alone computes
    public void A_saved_document_shows_what_the_engine_alone_computes()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, SheetDocument.FromJson(Saved)));

        var alone = Sheet.Open(SheetDocument.FromJson(Saved));
        var width = alone.GetColumnWidth(1)!.Value.Width;
        for (var row = 1; row <= 13; row++)
        {
            foreach (var (column, name) in new[] { (0, "A"), (1, "B") })
            {
                var address = $"{name}{row}";
                var headless = column == 1
                    ? alone.GetDisplay(CellAddress.Parse(address), width)
                    : alone.GetDisplay(CellAddress.Parse(address));
                Assert.False(headless.CannotShow, address);
                Assert.Equal(headless.Text, CellText(cut, address));
            }
        }
        // Read on screen, not only compared: the document's own culture, not the component's.
        Assert.Equal("46,29", CellText(cut, "B4"));
        Assert.Equal("27.10.2026", CellText(cut, "B5"));
        Assert.Equal("#DIV/0!", CellText(cut, "B11"));
    }

    [Fact] // ADR-0048: out, in, the same Values
    public async Task The_document_round_trips_through_the_component()
    {
        SheetDocument? raised = null;
        var first = RenderSheet(ps => ps.Add(s => s.DocumentChanged, d => raised = d));
        await EnterAsync(first, "A1", "3");
        await EnterAsync(first, "A2", "=A1+0.5");

        var second = RenderSheet(ps => ps.Add(s => s.Document, SheetDocument.FromJson(raised!.ToJson())));

        Assert.Equal("3.5", CellText(second, "A2"));
    }

    [Fact] // ADR-0048: a document the component raised, handed back (two-way binding), is the Sheet already open
    public async Task Handing_back_the_raised_document_changes_nothing()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, d => raised = d));
        await EnterAsync(cut, "A1", "3");

        cut.Render(ps => ps.Add(s => s.Document, raised));

        Assert.True(cut.Instance.CanUndo);
        Assert.Equal("3", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: a Consumer that keeps handing in its first document does not lose the user's edits
    public async Task An_unchanged_document_parameter_does_not_reopen_the_sheet()
    {
        var document = DocumentOf(CultureInfo.GetCultureInfo("en-US"), ("A1", "1"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, document).Add(s => s.DocumentChanged, _ => { }));
        await EnterAsync(cut, "A1", "5");

        cut.Render(ps => ps.Add(s => s.Document, document));

        Assert.Equal("5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: a different document replaces the Sheet
    public async Task A_different_document_replaces_the_sheet()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "5");

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(CultureInfo.GetCultureInfo("en-US"), ("B2", "hello"))));

        Assert.Equal("", CellText(cut, "A1"));
        Assert.Equal("hello", CellText(cut, "B2"));
    }

    [Fact] // ADR-0048: a document's culture is the Sheet's, whatever the Culture parameter says
    public void A_document_carries_its_own_culture()
    {
        var german = DocumentOf(CultureInfo.GetCultureInfo("de-DE"), ("A1", "1234,5"));

        var cut = RenderSheet(ps => ps.Add(s => s.Document, german));

        Assert.Equal("1234,5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: the culture is the Sheet's own; changing it on a live Sheet is refused by name
    public void Changing_the_culture_of_a_live_sheet_is_refused()
    {
        var cut = RenderSheet();

        var error = Assert.Throws<InvalidOperationException>(() =>
            cut.Render(ps => ps.Add(s => s.Culture, CultureInfo.GetCultureInfo("ja-JP"))));
        Assert.Contains("en-US", error.Message);
    }

    // ---- Another document opened in place of the one shown (ADR-0142, ADR-0011, ADR-0046) ----
    //
    // A Sheet's rows are places, so its own edits never move the Row Sequence Version it hands its
    // grid; another Sheet Document opened in place of the one shown does, so that a gesture taken on
    // what the old document painted is refused rather than written into the new one at the same place.

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static int Paint(IRenderedComponent<global::ExSheet.Components.ExSheet> cut)
        => int.Parse(Grid(cut).Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    private static string Notice(IRenderedComponent<global::ExSheet.Components.ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    [Fact] // ADR-0142 / ADR-0011, ADR-0046: another Sheet Document opened in place of the one shown drops the Selection: its cells stand at the old one's places
    public async Task A_replaced_document_drops_the_selection()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "old"))));
        await GoToAsync(cut, "A1");
        Assert.False(Grid(cut).Instance.ReadSelection().Selection.IsEmpty);

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "keep"))));

        Assert.True(Grid(cut).Instance.ReadSelection().Selection.IsEmpty);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0011: a paste taken on A1 of the old document and released after another was opened is refused as OrderMoved, said as the document replaced, and writes nothing into the new one
    public async Task A_paste_aimed_at_the_replaced_document_is_refused()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "old"))).Add(s => s.DocumentChanged, raised.Add));
        await GoToAsync(cut, "A1");
        var pressedOn = Paint(cut);

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "keep"))));
        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", pressedOn));

        Assert.Equal("keep", CellText(cut, "A1"));
        Assert.Empty(raised);
        Assert.Equal(SheetWords.PasteRefused(PasteRefusalReason.OrderMoved), Notice(cut));
    }

    [Theory] // ADR-0142 / LV-13, ADR-0054, ADR-0035: Delete and Ctrl+D taken on A1:A2 of the old document are refused after another was opened — neither clears the new one's cells nor fills A2 from its A1
    [InlineData("Delete", false)]
    [InlineData("d", true)]
    public async Task A_write_key_aimed_at_the_replaced_document_is_refused(string key, bool ctrl)
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "old"), ("A2", "below"))).Add(s => s.DocumentChanged, raised.Add));
        await GoToAsync(cut, "A1:A2");
        var pressedOn = Paint(cut);

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "keep"), ("A2", "stays"))));
        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, ctrl, false, false, false, false, paint: pressedOn));

        Assert.Equal("keep", CellText(cut, "A1"));
        Assert.Equal("stays", CellText(cut, "A2"));
        Assert.Empty(raised);
        Assert.Equal(SheetWords.PasteRefused(PasteRefusalReason.OrderMoved), Notice(cut));
    }

    [Fact] // ADR-0142 / ADR-0011, ADR-0012 (decided 2026-10-08): `5` `0` `0` Enter typed on A1 of the old document and reaching the Sheet after another was opened opens nothing, writes nothing into the new one, and is said once, as typing that reached the Sheet after another document was opened
    public async Task Typing_aimed_at_the_replaced_document_writes_nothing_and_is_said()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "old"))).Add(s => s.DocumentChanged, raised.Add));
        await GoToAsync(cut, "A1");
        var typedOn = Paint(cut);

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "keep"))));
        var grid = Grid(cut);
        foreach (var key in new[] { "5", "0", "0", "Enter" })
            await grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, false, false, false, false, false, paint: typedOn));

        Assert.False(cut.Instance.IsEditing);
        Assert.Equal("keep", CellText(cut, "A1"));
        Assert.Equal("", CellText(cut, "A2"));
        Assert.Empty(raised);
        Assert.True(grid.Instance.ReadSelection().Selection.IsEmpty);
        Assert.Equal(SheetWords.EditDiscarded(global::ExGrid.Cells.EditDiscardReason.OrderMoved), Notice(cut));
    }

    [Fact] // ADR-0142 / ADR-0012: a key typed after the new document is on screen keeps the first-key rule: it places the Focus on A1 and the editor opens there holding it
    public async Task Typing_on_the_new_document_opens_an_edit_by_the_first_key_rule()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "old"))));
        await GoToAsync(cut, "A1");

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "keep"))));
        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnKeyAsync("5", false, false, false, false, false, paint: Paint(cut)));

        Assert.True(cut.Instance.IsEditing);
        Assert.Equal("", Notice(cut));
    }

    [Fact] // ADR-0142, principle 1 (decided 2026-10-08): every edit discard the grid can raise has a sentence of its own, and typing that reached the Sheet after another document was opened — the one order move a Sheet has — says so, and that nothing was written
    public void Every_edit_discard_is_worded_and_an_order_move_is_told_as_another_document_opened()
    {
        var sentences = Enum.GetValues<global::ExGrid.Cells.EditDiscardReason>().Select(SheetWords.EditDiscarded).ToArray();

        Assert.Equal(sentences.Length, sentences.Distinct().Count());
        var orderMoved = SheetWords.EditDiscarded(global::ExGrid.Cells.EditDiscardReason.OrderMoved);
        Assert.Contains("another Sheet Document was opened", orderMoved);
        Assert.Contains("nothing was written", orderMoved);
    }

    [Fact] // ADR-0142 / ADR-0048: a document this Sheet raised, handed back as a two-way binding does, is no replacement: the Selection stays, and a paste taken before it came back lands
    public async Task The_sheets_own_document_coming_back_moves_nothing()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(EnUs, ("A1", "old"))).Add(s => s.DocumentChanged, d => raised = d));
        await EnterAsync(cut, "B1", "2");
        Assert.NotNull(raised);
        await GoToAsync(cut, "A1");
        var pressedOn = Paint(cut);

        cut.Render(ps => ps.Add(s => s.Document, raised));
        var grid = Grid(cut);
        Assert.False(grid.Instance.ReadSelection().Selection.IsEmpty);
        await grid.InvokeAsync(() => grid.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", pressedOn));

        Assert.Equal("x", CellText(cut, "A1"));
        Assert.Equal("", Notice(cut));
    }
}
