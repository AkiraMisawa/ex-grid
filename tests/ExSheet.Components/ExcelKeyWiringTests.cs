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
/// ExGrid's Excel keys as ExSheet answers them (merged from <c>main</c>, 2026-09-28): Ctrl+D and
/// Ctrl+R copy the source's Entries with relative References shifted (ADR-0035, ADR-0050 item 5),
/// Delete's Clear Intent clears the Selection as one undo step and leaves the Selection and the
/// Focus where they were (ADR-0054, ADR-0052 case 14), and Ctrl+F finds over the Sheet's cells by
/// their displayed text, every row (ADR-0055).
/// </summary>
public class ExcelKeyWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static Entry? EntryAt(IRenderedComponent<ExSheet> cut, string address) =>
        cut.Instance.ToDocument().Cells.SingleOrDefault(c => c.Address == CellAddress.Parse(address))?.Entry;

    private static string NameBox(IRenderedComponent<ExSheet> cut) =>
        cut.Find(".ex-name-box").GetAttribute("value") ?? "";

    // ---- Ctrl+D and Ctrl+R ----

    [Fact] // ADR-0035 / ADR-0050 item 5 (2026-09-28), SH-23: Ctrl+D copies a Formula down with its References shifted
    public async Task Ctrl_d_copies_a_formula_down_with_its_references_shifted()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("A2", "2"), ("A3", "3"), ("B1", "=A1*10"))));
        await GoToAsync(cut, "B1:B3");

        await PressAsync(cut, "d", ctrl: true);

        Assert.Equal("20", CellText(cut, "B2"));
        Assert.Equal("30", CellText(cut, "B3"));
        Assert.Equal("=A3*10", EntryAt(cut, "B3")!.Formula);
    }

    [Fact] // ADR-0035 / ADR-0050 item 5 (2026-09-28), SH-23: Ctrl+R copies across, shifting columns
    public async Task Ctrl_r_copies_a_formula_across_with_its_references_shifted()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "1"), ("B1", "2"), ("C1", "3"), ("A2", "=A1*10"))));
        await GoToAsync(cut, "A2:C2");

        await PressAsync(cut, "r", ctrl: true);

        Assert.Equal("20", CellText(cut, "B2"));
        Assert.Equal("30", CellText(cut, "C2"));
    }

    [Fact] // ADR-0035 / ADR-0050 item 5 (2026-09-28), SH-23: on one cell, Ctrl+D copies the cell above, shifted
    public async Task Ctrl_d_on_one_cell_copies_the_cell_above()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B1", "5"), ("B2", "7"), ("A1", "=B1"))));
        await GoToAsync(cut, "A2");

        await PressAsync(cut, "d", ctrl: true);

        Assert.Equal("7", CellText(cut, "A2"));
    }

    [Fact] // ADR-0050 item 5 (2026-09-28), SH-23: a fill key copies what the handle would continue — a date stays the date
    public async Task Ctrl_d_copies_a_date_rather_than_continuing_it()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "9/26/2026"))));
        await GoToAsync(cut, "A1:A3");

        await PressAsync(cut, "d", ctrl: true);

        Assert.Equal("9/26/2026", CellText(cut, "A2"));
        Assert.Equal("9/26/2026", CellText(cut, "A3"));
    }

    [Fact] // ADR-0048 (SH-13), SH-23: a fill by key is one undo step, and the Selection stays
    public async Task Ctrl_d_is_one_undo_step_and_keeps_the_selection()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "=B1"), ("A2", "old"), ("B1", "1"), ("B2", "2"), ("B3", "3")))
            .Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "A1:A3");
        var before = selections[^1];

        await PressAsync(cut, "d", ctrl: true);
        Assert.Equal("2", CellText(cut, "A2"));
        Assert.Equal(before, selections[^1]);

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("old", CellText(cut, "A2"));
        Assert.Equal("", CellText(cut, "A3"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0035 / CP-25, SH-23: a source row scrolled out of the Window is still read, never refused as unavailable
    public async Task Ctrl_d_reads_a_source_scrolled_out_of_the_window()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=B1"))));
        await GoToAsync(cut, "A1:A3000");
        await ScrollToAsync(cut, 2500 * ExSheet.DefaultRowHeightPx, 0);

        await PressAsync(cut, "d", ctrl: true);

        Assert.Equal("=B3000", EntryAt(cut, "A3000")?.Formula);
        Assert.Equal("", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Fact] // SH-23: an ordinary paste of the same text is still read as typed, not as a fill
    public async Task A_paste_is_not_taken_for_a_fill()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B1", "1"), ("B2", "2"))));
        await GoToAsync(cut, "A2");
        var grid = Grid(cut);

        await grid.InvokeAsync(() => grid.Instance.OnPasteAsync("=B1\r\n", null));

        Assert.Equal("1", CellText(cut, "A2"));
    }

    // ---- Ctrl+Enter ----

    /// <summary>Types <paramref name="typed"/> into the Focus of the current Selection and commits it with Ctrl+Enter.</summary>
    private static async Task CtrlEnterAsync(IRenderedComponent<ExSheet> cut, string typed)
    {
        await PressAsync(cut, typed[..1]);
        await TypeAsync(cut, typed);
        await PressAsync(cut, "Enter", ctrl: true);
    }

    [Fact] // ADR-0050 (2026-09-28), SH-27: Ctrl+Enter with a Formula shifts its relative References from the Focus into every other cell
    public async Task Ctrl_enter_shifts_a_formulas_references_from_the_focus()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2:C3");

        await CtrlEnterAsync(cut, "=A1");

        Assert.Equal("=A1", EntryAt(cut, "B2")!.Formula);
        Assert.Equal("=B1", EntryAt(cut, "C2")!.Formula);
        Assert.Equal("=A2", EntryAt(cut, "B3")!.Formula);
        Assert.Equal("=B2", EntryAt(cut, "C3")!.Formula);
    }

    [Fact] // ADR-0050 (2026-09-28), SH-27: References shift relative to the Focus, not the range's top-left, and absolute parts stay
    public async Task Ctrl_enter_shifts_relative_to_a_focus_not_at_the_top_left()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2:C3");
        // Shift+Enter cycles the Focus backwards inside the Selection, from B2 to C3.
        await PressAsync(cut, "Enter", shift: true);

        await CtrlEnterAsync(cut, "=B2+$A$1");

        Assert.Equal("=B2+$A$1", EntryAt(cut, "C3")!.Formula);
        Assert.Equal("=A1+$A$1", EntryAt(cut, "B2")!.Formula);
        Assert.Equal("=B1+$A$1", EntryAt(cut, "C2")!.Formula);
        Assert.Equal("=A2+$A$1", EntryAt(cut, "B3")!.Formula);
    }

    [Fact] // ADR-0048, ADR-0050 (2026-09-28), SH-27: Ctrl+Enter is typed text, even when it equals the Sheet's own last copy
    public async Task Ctrl_enter_of_the_last_copys_text_is_typed_not_pasted()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=1+1"))));
        await GoToAsync(cut, "A1");
        Grid(cut).Instance.BuildCopyPayload(); // the clipboard now holds "2", and the Sheet its Entry =1+1
        await GoToAsync(cut, "B1:B2");

        await CtrlEnterAsync(cut, "2");

        Assert.All(new[] { "B1", "B2" }, a => Assert.False(EntryAt(cut, a)!.IsFormula));
        Assert.All(new[] { "B1", "B2" }, a => Assert.Equal("2", CellText(cut, a)));
    }

    [Fact] // ADR-0048, ADR-0050 (2026-09-28), SH-27: a Formula entered over a range with Ctrl+Enter is one undo step
    public async Task Ctrl_enter_with_a_formula_over_a_range_is_one_undo_step()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2:C3");
        await CtrlEnterAsync(cut, "=A1");

        Assert.True(await cut.Instance.UndoAsync());

        Assert.All(new[] { "B2", "C2", "B3", "C3" }, a => Assert.Null(EntryAt(cut, a)));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0050 (2026-09-28), SH-27: text that is not a Formula is written as typed into every cell, as before
    public async Task Ctrl_enter_writes_text_into_every_cell_as_typed()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2:C3");

        await CtrlEnterAsync(cut, "A1");

        Assert.All(new[] { "B2", "C2", "B3", "C3" }, a => Assert.Equal("A1", CellText(cut, a)));
    }

    // ---- Delete ----

    [Fact] // ADR-0054 / ADR-0052 case 14, SH-24: Delete clears the Selection, keeps it and the Focus, as one undo step
    public async Task Delete_clears_the_selection_as_one_step_and_keeps_it()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "1"), ("B1", "2"), ("A2", "x"), ("B2", "=A1+B1"), ("C1", "=A1*10"), ("A3", "keep")))
            .Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "A1:B2");
        var before = selections[^1];

        await PressAsync(cut, "Delete");

        foreach (var cell in new[] { "A1", "B1", "A2", "B2" }) Assert.Equal("", CellText(cut, cell));
        // A dependent outside the Selection recomputes from the Blank, and a cell outside it stands.
        Assert.Equal("0", CellText(cut, "C1"));
        Assert.Equal("keep", CellText(cut, "A3"));
        Assert.Equal(before, selections[^1]);
        Assert.Equal("A1", NameBox(cut));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("1", CellText(cut, "A1"));
        Assert.Equal("3", CellText(cut, "B2"));
        Assert.Equal("10", CellText(cut, "C1"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0054, SH-24: Delete over whole columns clears only what they hold, and keeps formats
    public async Task Delete_over_a_whole_column_clears_its_entries_and_keeps_the_format()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "1");
        sheet.Enter(CellAddress.Parse("A900000"), "2");
        sheet.SetFormat(CellAddress.Parse("A1"), NumberFormat.Parse("0.00"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await GoToAsync(cut, "A:A");

        await PressAsync(cut, "Delete");

        Assert.Null(EntryAt(cut, "A900000"));
        Assert.Equal("", CellText(cut, "A1"));
        await EnterAsync(cut, "A1", "3");
        Assert.Equal("3.00", CellText(cut, "A1"));
    }

    [Fact] // ADR-0054, SH-24: Delete over empty cells writes nothing and adds no undo step
    public async Task Delete_over_empty_cells_adds_no_step()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "C3:D4");

        await PressAsync(cut, "Delete");

        Assert.False(cut.Instance.CanUndo);
    }

    // ---- Find ----

    private static async Task FindAsync(IRenderedComponent<ExSheet> cut, string text, bool backward = false)
    {
        if (cut.FindAll("input.ex-find-field").Count == 0) await PressAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text });
        await cut.Find(backward ? "button.ex-find-previous" : "button.ex-find-next").ClickAsync(new MouseEventArgs());
    }

    [Fact] // ADR-0055 / FD-2, SH-25: ExSheet answers Find, so Ctrl+F opens the panel rather than refusing
    public async Task Ctrl_f_opens_the_find_panel()
    {
        var cut = RenderSheet();

        await PressAsync(cut, "f", ctrl: true);

        Assert.Single(cut.FindAll("input.ex-find-field"));
    }

    [Fact] // ADR-0055, SH-25: a match far below the painted rows is found, and the Focus goes there
    public async Task A_match_in_any_row_is_found()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "hay"), ("C5000", "needle"))));
        await GoToAsync(cut, "A1");

        await FindAsync(cut, "NEED");

        Assert.Equal("C5000", NameBox(cut));
    }

    [Fact] // ADR-0055, SH-25: the displayed text is matched, a Formula's Value as shown — not its Entry
    public async Task The_displayed_text_is_matched()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=2*21"), ("B2", "=A1"), ("C3", "42"))));
        await GoToAsync(cut, "A1");

        await FindAsync(cut, "42");
        Assert.Equal("B2", NameBox(cut));
        await FindAsync(cut, "42");
        Assert.Equal("C3", NameBox(cut));
        await FindAsync(cut, "42");
        Assert.Equal("A1", NameBox(cut));

        await FindAsync(cut, "A1");
        Assert.Equal("A1", NameBox(cut));
        Assert.Contains("No match", cut.Find(".ex-popover-find").TextContent);
    }

    [Fact] // ADR-0055, SH-25: by rows from the cell after the Focus, wrapping; Shift+Enter goes back
    public async Task Find_steps_by_rows_and_wraps_both_ways()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("B1", "x"), ("A2", "x"), ("C2", "x"))));
        await GoToAsync(cut, "A2");

        await FindAsync(cut, "x");
        Assert.Equal("C2", NameBox(cut));
        await FindAsync(cut, "x");
        Assert.Equal("B1", NameBox(cut));
        await FindAsync(cut, "x", backward: true);
        Assert.Equal("C2", NameBox(cut));
    }

    [Fact] // ADR-0055, SH-25: Match case and Match entire cell contents are honoured
    public async Task Match_case_and_whole_cell_are_honoured()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "abc"), ("A2", "ABC"), ("A3", "AB"))));
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-match-case").ChangeAsync(new ChangeEventArgs { Value = true });

        await FindAsync(cut, "AB");
        Assert.Equal("A2", NameBox(cut));

        await cut.Find("input.ex-find-whole-cell").ChangeAsync(new ChangeEventArgs { Value = true });
        await FindAsync(cut, "AB");
        Assert.Equal("A3", NameBox(cut));
    }

    [Fact] // ADR-0055, SH-25: with a range selected, the search stays inside it and the Selection stands
    public async Task A_selected_range_is_searched_and_stands()
    {
        var selections = new List<GridSelection>();
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, DocumentOf(("A1", "x"), ("B2", "x"), ("D4", "x")))
            .Add(s => s.SelectionChanged, selections.Add));
        await GoToAsync(cut, "A1:C3");
        var ranges = selections[^1].Ranges;

        await FindAsync(cut, "x");
        Assert.Equal("B2", NameBox(cut));
        await FindAsync(cut, "x");
        Assert.Equal("A1", NameBox(cut));
        Assert.Equal(ranges, selections[^1].Ranges);
    }
}
