using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// While an edit is open, the application's changes are refused (ticket 26, ADR-0048; SH-29).
/// Every command that changes the Sheet is refused by name and changes nothing; a Linked Table's
/// declaration and snapshots are data arriving, and are taken (ADR-0049). ExSheet learns whether
/// an edit is open from its grid (ADR-0050 section 6), says so, and raises the change, so the
/// application can grey out its buttons. Found on <c>/sheet</c>: <c>99</c> typed over C4 (Plums),
/// a row inserted above row 2 while the edit was open, and Enter wrote 99 into Pears' price.
/// </summary>
public class RefusedWhileEditingTests : SheetTestContext
{
    // The demo page's first rows: the order the bug moved under the editor.
    private static SheetDocument Fruit()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        (string Address, string Typed)[] cells =
        [
            ("A1", "Item"), ("B1", "Qty"), ("C1", "Price"), ("D1", "Amount"),
            ("A2", "Apples"), ("B2", "12"), ("C2", "0.5"), ("D2", "=B2*C2"),
            ("A3", "Pears"), ("B3", "7"), ("C3", "0.75"), ("D3", "=B3*C3"),
            ("A4", "Plums"), ("B4", "20"), ("C4", "0.2"), ("D4", "=B4*C4"),
            ("A5", "Total"), ("D5", "=SUM(D2:D4)"),
        ];
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    /// <summary>Types over the cell at <paramref name="address"/> and leaves the edit open.</summary>
    private static async Task OpenEditAsync(IRenderedComponent<ExSheet> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        await TypeAsync(cut, typed);
    }

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    /// <summary>The command is refused, naming the open edit, and the Sheet Document is as it was.</summary>
    private static async Task AssertRefusedAsync(IRenderedComponent<ExSheet> cut, Func<Task> command)
    {
        var before = cut.Instance.ToDocument().ToJson();

        var refused = await Assert.ThrowsAsync<SheetRefusedException>(command);

        Assert.Equal(SheetRefusalReason.EditIsOpen, refused.Refusal.Reason);
        Assert.Equal(before, cut.Instance.ToDocument().ToJson());
    }

    [Fact] // ADR-0048 / ADR-0050 section 6, SH-29: ExSheet says whether an edit is open, from its grid, and raises each change
    public async Task Whether_an_edit_is_open_follows_the_grid_and_is_raised()
    {
        var told = new List<bool>();
        var cut = RenderSheet(ps => ps.Add(s => s.EditingChanged, told.Add));
        Assert.False(cut.Instance.IsEditing);

        await OpenEditAsync(cut, "A1", "5");
        Assert.True(cut.Instance.IsEditing);

        await PressAsync(cut, "Enter");
        Assert.False(cut.Instance.IsEditing);
        Assert.Equal([true, false], told);
    }

    [Fact] // ADR-0048 / ADR-0051, SH-29: a press into the Formula Bar opens an edit too
    public async Task A_press_into_the_formula_bar_is_an_open_edit()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await GoToAsync(cut, "C4");

        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        Assert.True(cut.Instance.IsEditing);
        await AssertRefusedAsync(cut, () => cut.Instance.DoAsync(SheetEdit.InsertRows(1)));
    }

    [Fact] // ADR-0018 / ADR-0048, SH-29: an edit open in one ExSheet refuses nothing in another on the same page
    public async Task An_edit_open_in_one_sheet_refuses_nothing_in_another()
    {
        var first = RenderSheet();
        var second = RenderSheet();

        await OpenEditAsync(first, "A1", "5");
        await second.Instance.DoAsync(SheetEdit.InsertRows(0));

        Assert.True(first.Instance.IsEditing);
        Assert.False(second.Instance.IsEditing);
        Assert.True(second.Instance.CanUndo);
    }

    [Fact] // ADR-0048 / ADR-0050 section 6, SH-29: a command the application gives as the edit ends comes after the committed value, which stays in its row
    public async Task A_command_given_on_hearing_the_end_comes_after_the_committed_value()
    {
        // The application inserts the moment the edit ends, as a click on a button the end has
        // just re-enabled would. Heard before the value was handled, the insertion moved Plums
        // down first, and 99 went into Pears' price.
        IRenderedComponent<ExSheet>? cut = null;
        cut = RenderSheet(ps => ps
            .Add(s => s.Document, Fruit())
            .Add(s => s.EditingChanged, async (bool open) =>
            {
                if (!open) await cut!.Instance.DoAsync(SheetEdit.InsertRows(1));
            }));
        await OpenEditAsync(cut, "C4", "99");

        await PressAsync(cut, "Enter");

        Assert.Equal("Plums", CellText(cut, "A5"));
        Assert.Equal("99", CellText(cut, "C5"));
        Assert.Equal("Pears", CellText(cut, "A4"));
        Assert.Equal("0.75", CellText(cut, "C4"));
    }

    // ---- The commands (SH-29: one per command) ----

    [Fact] // ADR-0048, SH-29: the /sheet repro — 99 over C4, a row inserted above row 2, Enter: 99 lands in Plums' row
    public async Task DoAsync_is_refused_and_the_typing_lands_where_it_was_typed()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await OpenEditAsync(cut, "C4", "99");

        await AssertRefusedAsync(cut, () => cut.Instance.DoAsync(SheetEdit.InsertRows(1)));
        Assert.Equal("99", EditorText(cut));

        await PressAsync(cut, "Enter");

        Assert.Equal("Plums", CellText(cut, "A4"));
        Assert.Equal("99", CellText(cut, "C4"));
        Assert.Equal("Pears", CellText(cut, "A3"));
        Assert.Equal("0.75", CellText(cut, "C3"));
        Assert.Equal("1991.25", CellText(cut, "D5"));
    }

    [Fact] // ADR-0048, SH-29: UndoAsync is refused while an edit is open, and the step stays to be undone
    public async Task UndoAsync_is_refused()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await OpenEditAsync(cut, "A2", "2");

        await AssertRefusedAsync(cut, () => cut.Instance.UndoAsync());

        Assert.Equal("1", CellText(cut, "A1"));
        Assert.True(cut.Instance.CanUndo);
        Assert.Equal("2", EditorText(cut));
    }

    [Fact] // ADR-0048, SH-29: RedoAsync is refused while an edit is open, and the undone step stays to be redone
    public async Task RedoAsync_is_refused()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        Assert.True(await cut.Instance.UndoAsync());
        await OpenEditAsync(cut, "A2", "2");

        await AssertRefusedAsync(cut, () => cut.Instance.RedoAsync());

        Assert.Equal("", CellText(cut, "A1"));
        Assert.True(cut.Instance.CanRedo);
        Assert.Equal("2", EditorText(cut));
    }

    [Fact] // ADR-0050 item 8 / section 6, SH-29: undo and redo the grid forwards while ExSheet still hears an edit open are said, never thrown into the key handler
    public async Task Undo_and_redo_from_the_grids_keys_while_an_edit_is_heard_open_are_said_not_thrown()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1");
        await OpenEditAsync(cut, "A2", "2");
        var grid = Grid(cut);

        // The grid forwards the keys only once its edit has ended, and after a discard a
        // parameter change caused, ExSheet hears that end a render later (ADR-0050 section 6).
        // The keys landing in that render are staged by raising the grid's own callbacks while
        // ExSheet still hears the edit open.
        await grid.InvokeAsync(() => grid.Instance.OnUndo.InvokeAsync());
        await grid.InvokeAsync(() => grid.Instance.OnRedo.InvokeAsync());

        Assert.Equal("1", CellText(cut, "A1"));
        Assert.True(cut.Instance.CanUndo);
        Assert.Equal(SheetWords.EditIsOpen, cut.Find(".ex-sheet-notice").TextContent);
        Assert.Equal("2", EditorText(cut));
    }

    [Fact] // ADR-0048, SH-29: SetNumberFormatAsync is refused while an edit is open
    public async Task SetNumberFormatAsync_is_refused()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234.5");
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "F2");

        await AssertRefusedAsync(cut, () => cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("#,##0.00")));

        await PressAsync(cut, "Escape");
        Assert.Equal("1234.5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048, SH-29: SetAlignmentAsync is refused while an edit is open
    public async Task SetAlignmentAsync_is_refused()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "text");
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "F2");

        await AssertRefusedAsync(cut, () => cut.Instance.SetAlignmentAsync(HorizontalAlignment.Right));

        Assert.Equal("text", EditorText(cut));
    }

    [Fact] // ADR-0071 / ADR-0048, SH-43: SetCellFormatAsync is refused while an edit is open, and no part of the change is set
    public async Task SetCellFormatAsync_is_refused()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "1234.5");
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "F2");

        await AssertRefusedAsync(cut, () => cut.Instance.SetCellFormatAsync(new CellFormatChange
        {
            NumberFormat = NumberFormat.Parse("#,##0.00"),
            Bold = true,
            Fill = CellFill.Solid(CellColour.FromRgb(0xFFFF00)),
            Borders = BorderChange.Outline(new BorderLine(BorderLineStyle.Thin)),
        }));

        Assert.Equal("1234.5", EditorText(cut));
        await PressAsync(cut, "Escape");
        Assert.Equal(CellFormat.Default, cut.Instance.CellFormatAt(CellAddress.Parse("A1")));
        Assert.Equal("1234.5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0071, SH-43 / SH-44: CellFormatAt is a read, so it answers while an edit is open, and the edit stays
    public async Task CellFormatAt_answers_while_an_edit_is_open()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1");
        Assert.True(await cut.Instance.SetCellFormatAsync(new CellFormatChange { Bold = true }));
        await OpenEditAsync(cut, "A1", "99");

        var format = cut.Instance.CellFormatAt(CellAddress.Parse("A1"));

        Assert.True(format.Font.Bold);
        Assert.True(cut.Instance.IsEditing);
        Assert.Equal("99", EditorText(cut));
    }

    [Fact] // ADR-0049, SH-29: a Linked Table's declaration is data arriving, not a command, and is taken while an edit is open
    public async Task A_linked_table_declaration_is_taken()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await OpenEditAsync(cut, "C4", "99");

        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);

        Assert.Equal("Positions", Assert.Single(cut.Instance.LinkedTables).Name);
        Assert.True(cut.Instance.IsEditing);
        Assert.Equal("99", EditorText(cut));
    }

    [Fact] // ADR-0049, SH-29: a Linked Table's snapshot recalculates its readers while an edit is open, and the typing stands
    public async Task A_linked_table_snapshot_is_taken()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await EnterAsync(cut, "F1", "=SUM(Positions[PV])");
        await OpenEditAsync(cut, "C4", "99");

        await cut.Instance.PushLinkedTableAsync("Positions",
            [[Value.FromText("R-1"), Value.FromNumber(100)], [Value.FromText("R-2"), Value.FromNumber(1.5)]]);

        Assert.Equal("101.5", CellText(cut, "F1"));
        Assert.Equal("99", EditorText(cut));
        await PressAsync(cut, "Enter");
        Assert.Equal("99", CellText(cut, "C4"));
        Assert.Equal("Plums", CellText(cut, "A4"));
    }

    // ---- The Sheet Document replaced while an edit is open (ADR-0048, decided 2026-09-29) ----

    // Another document, whose C4 is not Plums' price: where a kept edit would have landed.
    private static SheetDocument Other()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A4"), "Figs");
        sheet.Enter(CellAddress.Parse("C4"), "3");
        return sheet.ToDocument();
    }

    [Fact] // ADR-0048 / ADR-0050 section 6, SH-29: a replaced Document discards the open edit, says so, and writes nothing
    public async Task Replacing_the_document_while_an_edit_is_open_discards_it_and_says_so()
    {
        var told = new List<bool>();
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()).Add(s => s.EditingChanged, told.Add).Add(s => s.DocumentChanged, raised.Add));
        await OpenEditAsync(cut, "C4", "99");

        cut.Render(ps => ps.Add(s => s.Document, Other()));

        Assert.False(cut.Instance.IsEditing);
        Assert.Equal([true, false], told);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(SheetWords.EditDiscardedByNewDocument, Notice(cut));
        // The grid announced the same sentence in its own live region, as the Consumer's (ADR-0050 section 6).
        Assert.Equal(SheetWords.EditDiscardedByNewDocument, Grid(cut).Find(".ex-announce").TextContent);
        Assert.Equal("Figs", CellText(cut, "A4"));
        Assert.Equal("3", CellText(cut, "C4"));

        // Nothing is left to commit: Enter moves, and writes nothing into the new document.
        await PressAsync(cut, "Enter");
        Assert.Equal("3", CellText(cut, "C4"));
        Assert.Empty(raised);
    }

    [Fact] // ADR-0048, SH-29: the document the Sheet raised, handed back as a two-way binding does, is no replacement and keeps the edit
    public async Task Handing_back_the_raised_document_keeps_the_edit()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()).Add(s => s.DocumentChanged, d => raised = d));
        await OpenEditAsync(cut, "C4", "99");
        // A declaration is recorded in the document, so it raises one while the edit stays open.
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        Assert.NotNull(raised);

        cut.Render(ps => ps.Add(s => s.Document, raised));

        Assert.True(cut.Instance.IsEditing);
        Assert.Equal("99", EditorText(cut));
        Assert.Equal("", Notice(cut));
        await PressAsync(cut, "Enter");
        Assert.Equal("99", CellText(cut, "C4"));
    }

    [Fact] // ADR-0048, SH-29: a replaced Document with no edit open has nothing to discard, and says nothing
    public void Replacing_the_document_with_no_edit_open_says_nothing()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));

        cut.Render(ps => ps.Add(s => s.Document, Other()));

        Assert.Equal("", Notice(cut));
        Assert.Equal("", Grid(cut).Find(".ex-announce").TextContent);
        Assert.Equal("Figs", CellText(cut, "A4"));
    }

    // ---- The ways an edit ends (SH-29: one per way) ----

    [Fact] // ADR-0048 / ADR-0050 section 6, SH-29: once the edit is committed, the commands are taken again
    public async Task After_a_commit_the_commands_are_taken()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await OpenEditAsync(cut, "C4", "99");

        await PressAsync(cut, "Enter");
        await cut.Instance.DoAsync(SheetEdit.InsertRows(1));

        Assert.False(cut.Instance.IsEditing);
        Assert.Equal("Plums", CellText(cut, "A5"));
        Assert.Equal("99", CellText(cut, "C5"));
    }

    [Fact] // ADR-0048 / ADR-0050 section 6, SH-29: once the edit is cancelled, the commands are taken again
    public async Task After_a_cancel_the_commands_are_taken()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await OpenEditAsync(cut, "C4", "99");

        await PressAsync(cut, "Escape");
        await cut.Instance.DoAsync(SheetEdit.InsertRows(1));

        Assert.False(cut.Instance.IsEditing);
        Assert.Equal("Plums", CellText(cut, "A5"));
        Assert.Equal("0.2", CellText(cut, "C5"));
    }

    [Fact] // ADR-0048 / ADR-0011 / ADR-0050 section 6, SH-29: once the edit is discarded, the commands are taken again
    public async Task After_a_discard_the_commands_are_taken()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await OpenEditAsync(cut, "C4", "99");
        // The Window moves on beneath the editor, and the row it was opened on leaves it: the
        // commit has no row to land on, and the text is discarded (ADR-0011).
        await ScrollToAsync(cut, 5000 * ExSheet.DefaultRowHeightPx, 0);

        await PressAsync(cut, "Enter");
        Assert.False(cut.Instance.IsEditing);
        // Said, and it stands past the move that completes the Enter (ADR-0011).
        Assert.Equal(SheetWords.EditDiscarded(global::ExGrid.Cells.EditDiscardReason.RowLeftTheWindow), Notice(cut));
        await cut.Instance.DoAsync(SheetEdit.InsertRows(1));

        await ScrollToAsync(cut, 0, 0);
        Assert.Equal("Plums", CellText(cut, "A5"));
        Assert.Equal("0.2", CellText(cut, "C5"));
    }

    [Fact] // ADR-0034 / ADR-0048, SH-29: a Reject holds the edit open, so the commands stay refused until it ends
    public async Task A_rejected_commit_holds_the_edit_open_and_the_commands_stay_refused()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Fruit()));
        await OpenEditAsync(cut, "C4", "=SUM(");

        await PressAsync(cut, "Enter");

        Assert.True(cut.Instance.IsEditing);
        await AssertRefusedAsync(cut, () => cut.Instance.DoAsync(SheetEdit.InsertRows(1)));

        await PressAsync(cut, "Escape");
        await cut.Instance.DoAsync(SheetEdit.InsertRows(1));
        Assert.Equal("Plums", CellText(cut, "A5"));
    }

    [Fact] // ADR-0142 / LV-20, principle 1: every commit refusal the grid can raise has a sentence of its own, naming the cell
    public void Every_commit_refusal_is_worded_with_its_cell()
        => Assert.All(Enum.GetValues<global::ExGrid.Cells.CommitRefusalReason>(),
            static reason => Assert.Contains("C4", SheetWords.CommitRefused("C4", reason)));

    [Fact] // ADR-0142 / LV-13, principle 1: every paste refusal the grid can raise has a sentence of its own, and a write aimed under a moved order is not told to select a cell
    public void Every_paste_refusal_is_worded_and_an_order_move_is_told_as_one()
    {
        var sentences = Enum.GetValues<global::ExGrid.Clipboard.PasteRefusalReason>().Select(SheetWords.PasteRefused).ToArray();

        Assert.Equal(sentences.Length, sentences.Distinct().Count());
        var orderMoved = SheetWords.PasteRefused(global::ExGrid.Clipboard.PasteRefusalReason.OrderMoved);
        Assert.Contains("rows moved", orderMoved);
        Assert.NotEqual(SheetWords.PasteRefused(global::ExGrid.Clipboard.PasteRefusalReason.EmptySelection), orderMoved);
    }
}
