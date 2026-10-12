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
/// Completion, the argument hint and Point mode, as ExSheet answers them (tickets 10 and 11,
/// ADR-0051): the grid reports the editor's text and caret, and ExSheet answers from the engine's
/// <see cref="FormulaEntry"/>.
/// </summary>
public class FormulaEntryWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    private static List<string> Candidates(IRenderedComponent<ExSheet> cut) =>
        [.. cut.FindAll(".ex-completion .ex-completion-item").Select(item => item.TextContent)];

    private static string NameBox(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-name-box").GetAttribute("value") ?? "";

    /// <summary>Goes to <paramref name="address"/>, opens Overwrite with the first character and types the rest.</summary>
    private static async Task StartTypingAsync(IRenderedComponent<ExSheet> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1) await TypeAsync(cut, typed);
    }

    [Fact] // ADR-0051: =SU offers every declared function starting with SU, in order (ADR-0047's set and its additions)
    public async Task Su_offers_substitute_sum_and_sumproduct()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "A1", "=SU");

        Assert.Equal(["SUBSTITUTE", "SUBTOTAL", "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT"], Candidates(cut));
    }

    [Fact] // ADR-0051: =X offers every declared function starting with X, without regard to case
    public async Task X_offers_the_x_functions()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "A1", "=x");

        Assert.Equal(["XLOOKUP", "XMATCH", "XNPV", "XOR"], Candidates(cut));
    }

    [Fact] // ADR-0051: Tab accepts the engine's replacement over the engine's span, and the hint follows
    public async Task Tab_accepts_and_the_hint_follows()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=1+sum");

        await PressAsync(cut, "Tab");

        Assert.Equal("=1+SUM(", EditorText(cut));
        Assert.Empty(Candidates(cut));
        var hint = cut.Find(".ex-completion .ex-completion-hint");
        Assert.Equal("SUM(number1, [number2], ...)", hint.TextContent);
        Assert.Equal("number1", hint.QuerySelector("strong")!.TextContent);
    }

    [Fact] // ADR-0051: the argument the caret is in is set off, counted by the commas
    public async Task The_hint_sets_off_the_argument_the_caret_is_in()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "A1", "=ROUND(A1,");

        var hint = cut.Find(".ex-completion .ex-completion-hint");
        Assert.StartsWith("ROUND(", hint.TextContent);
        Assert.Equal("num_digits", hint.QuerySelector("strong")!.TextContent);
    }

    [Fact] // ADR-0051: Escape closes the list and leaves the edit open
    public async Task Escape_closes_the_list_and_leaves_the_edit_open()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=SU");

        await PressAsync(cut, "Escape");

        Assert.Empty(Candidates(cut));
        Assert.Equal("=SU", EditorText(cut));
    }

    [Fact] // ADR-0051: a constant is not a Formula, and gets no aid
    public async Task A_constant_gets_no_completion()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "A1", "SU");

        Assert.Empty(cut.FindAll(".ex-completion"));
    }

    [Fact] // ADR-0051: completion works from the Formula Bar as from the cell
    public async Task Completion_works_from_the_formula_bar()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());

        await TypeInBarAsync(cut, "=XL");

        Assert.Equal(["XLOOKUP"], Candidates(cut));
    }

    [Fact] // ADR-0051 / DC-19: = ↓ ↓ writes A3 and the Name Box names it (Excel), the Focus stays, and Enter commits the Formula
    public async Task Equals_down_down_writes_a3()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A3", "7"))));
        await StartTypingAsync(cut, "A1", "=");

        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=A2", EditorText(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=A2", 3);
        Assert.Equal("=A3", EditorText(cut));
        Assert.Equal("A3", NameBox(cut));

        await PressInEditorAsync(cut, "Enter", "=A3", 3);
        Assert.Equal("A2", NameBox(cut));

        Assert.Equal("7", CellText(cut, "A1"));
        await GoToAsync(cut, "A1");
        Assert.Equal("=A3", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
    }

    [Fact] // ADR-0051: Shift+arrow extends the pointed range, written from its top-left
    public async Task Shift_extends_to_a_range()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "B1", "=SUM(");

        await PressInEditorAsync(cut, "ArrowDown", "=SUM(", 5);
        await PressInEditorAsync(cut, "ArrowRight", "=SUM(B2", 7, shift: true);
        await PressInEditorAsync(cut, "ArrowDown", "=SUM(B2:C2", 10, shift: true);

        Assert.Equal("=SUM(B2:C3", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-19: a click on a cell writes the clicked cell, and the Name Box names it (Excel)
    public async Task A_click_writes_the_clicked_cell()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=1+");
        var heading = double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);

        // Column C, row 4: past the Row Headings and two columns, and three rows down the viewport.
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = heading + 2 * SheetColumns.DefaultWidthPx + 5, OffsetY = 28 * 3 + 5,
        });

        Assert.Equal("=1+C4", EditorText(cut));
        Assert.Equal("C4", NameBox(cut));
    }

    [Fact] // ADR-0051: F2 switches between moving the caret and pointing
    public async Task F2_switches_between_caret_and_pointing()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=1+");

        await PressInEditorAsync(cut, "F2", "=1+", 3);
        await PressInEditorAsync(cut, "F2", "=1+", 3);
        await PressInEditorAsync(cut, "ArrowDown", "=1+", 3);

        Assert.Equal("=1+A2", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-19: pointing works from the Formula Bar
    public async Task Pointing_works_from_the_formula_bar()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "=SUM()");

        // The bar opens Caret; F2 points, with the caret between the parentheses.
        await PressInEditorAsync(cut, "F2", "=SUM()", 5, fromBar: true);
        await PressInEditorAsync(cut, "ArrowUp", "=SUM()", 5, fromBar: true);

        Assert.Equal("=SUM(B1)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("B1", NameBox(cut));
    }

    [Fact] // ADR-0051/0012: typing a constant, the arrows still commit and move
    public async Task Typing_a_constant_the_arrows_commit_and_move()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "5");

        await PressInEditorAsync(cut, "ArrowDown", "5", 1);

        Assert.Equal("5", CellText(cut, "A1"));
        Assert.Equal("A2", NameBox(cut));
    }

    [Fact] // ADR-0051: after an operand, a Reference cannot go at the caret, so the arrow commits
    public async Task After_an_operand_the_arrow_commits()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=1+2");

        await PressInEditorAsync(cut, "ArrowDown", "=1+2", 4);

        Assert.Equal("3", CellText(cut, "A1"));
        Assert.Equal("A2", NameBox(cut));
    }

    [Fact] // ADR-0051: the Reference text is the Sheet's name for the pointed range
    public void The_reference_text_is_a1_or_a_range_from_its_top_left()
    {
        Assert.Equal("A3", SheetFormulaAids.ReferenceText(new SelectionRange(2, 0, 1, 1)));
        Assert.Equal("B7:C9", SheetFormulaAids.ReferenceText(new SelectionRange(6, 1, 3, 2)));
    }

    [Fact] // ADR-0051: the hint's emphasis is counted over the argument names, never searched for
    public void The_hint_emphasis_is_where_the_argument_stands()
    {
        var hint = SheetFormulaAids.HintOf(new ArgumentHint(DeclaredFunction.Find("SUM")!, 5, "[number2]"))!;

        Assert.Equal("SUM(number1, [number2], ...)", hint.Text);
        Assert.Equal(("SUM(number1, ", "[number2]", ", ...)"), hint.Parts());
        Assert.Equal(("IF(logical_test, value_if_true, [value_if_false])", "", ""),
            SheetFormulaAids.HintOf(new ArgumentHint(DeclaredFunction.Find("IF")!, 3, null))!.Parts());
    }

    // ---- F4 cycles the Reference at the caret (ADR-0051, 2026-09-29) ----

    [Fact] // ADR-0051 / SH-28 / DC-45: =B2 and F4 four times in the cell gives $B$2, B$2, $B2 and B2, each press decided from the text it carries
    public async Task SH28_F4_cycles_the_reference_in_the_cell()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=B2");
        var seen = new List<string>();

        foreach (var text in new[] { "=B2", "=$B$2", "=B$2", "=$B2" })
        {
            await PressInEditorAsync(cut, "F4", text, text.Length);
            seen.Add(EditorText(cut));
        }

        Assert.Equal(["=$B$2", "=B$2", "=$B2", "=B2"], seen);
    }

    [Fact] // ADR-0051 / SH-28: what F4 wrote is what Enter commits
    public async Task SH28_what_F4_wrote_is_committed()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=B2*2");

        await PressInEditorAsync(cut, "F4", "=B2*2", 3);
        await PressInEditorAsync(cut, "Enter", "=$B$2*2", 5);

        await GoToAsync(cut, "A1");
        Assert.Equal("=$B$2*2", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
    }

    [Fact] // ADR-0051 / SH-28 / DC-45: F4 cycles in the Formula Bar — the end of the range at the caret — and the Cell Editor shows the same text; the caret moved by ←, the rewritten end is selected (observed, R1 (v): =SUM(A1:B2), ←, F4 gave =SUM(A1:$B$2) with $B$2 selected)
    public async Task SH28_F4_cycles_the_reference_in_the_formula_bar()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "=SUM(A1:B2)");

        await PressInEditorAsync(cut, "F4", "=SUM(A1:B2)", 10, fromBar: true, moved: true);

        Assert.Equal("=SUM(A1:$B$2)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("=SUM(A1:$B$2)", EditorText(cut));
        Assert.Equal(("=SUM(A1:$B$2)", 8, 12), LastPlaced());
    }

    [Fact] // ADR-0051 / SH-28 / DC-45: typed into the Formula Bar, F4 leaves a caret at the end of the rewritten Reference, nothing selected (observed, R1 (iv): =B2, F4 gave =$B$2 with a caret at its end)
    public async Task SH28_F4_typed_into_the_formula_bar_leaves_a_caret()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "=B2");

        await PressInEditorAsync(cut, "F4", "=B2", 3, fromBar: true);

        Assert.Equal("=$B$2", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal(("=$B$2", 5, 5), LastPlaced());
    }

    [Fact] // ADR-0051 / ED-29: in the Formula Bar, after =, ↓ is the editor's: the edit stays in Caret, the text stays =, nothing is outlined; F4 and ↓ change nothing, and + types on (observed, R1 (i) to (iii))
    public async Task ED29_in_the_formula_bar_down_after_equals_does_not_point()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "=");
        var grid = Grid(cut);
        var placed = JSInterop.Invocations.Count(i => i.Identifier == "setCaret");

        // The gate leaves ↓ to the editor in Caret; one that reaches the core all the same, as F4 does,
        // changes nothing.
        await PressInEditorAsync(cut, "ArrowDown", "=", 1, fromBar: true);
        await PressInEditorAsync(cut, "F4", "=", 1, fromBar: true);
        await PressInEditorAsync(cut, "ArrowDown", "=", 1, fromBar: true);

        Assert.Equal("=", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Empty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("caret", JSInterop.Invocations.Last(i => i.Identifier == "setEditing").Arguments[0]);
        Assert.Equal(placed, JSInterop.Invocations.Count(i => i.Identifier == "setCaret"));
        Assert.Equal("D10", NameBox(cut));

        await TypeInBarAsync(cut, "=+");
        Assert.Equal("=+", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("=+", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal(new CellPosition(9, 3), grid.Instance.ReadSelection().Selection.Focus);
    }

    [Fact] // ADR-0051 / SH-28: a selection over several References cycles each of them
    public async Task SH28_a_selection_cycles_every_reference_it_covers()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=B1+C1");

        await PressInEditorAsync(cut, "F4", "=B1+C1", 1, selectionEnd: 6);

        Assert.Equal("=$B$1+$C$1", EditorText(cut));
    }

    [Fact] // ADR-0051 / SH-28 / DC-45: a selection touching References at its ends cycles them — + selected in =A1+B1 gives =$A$1+$B$1 (observed in Excel)
    public async Task SH28_a_selection_touching_references_cycles_them()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "C1", "=A1+B1");

        await PressInEditorAsync(cut, "F4", "=A1+B1", 3, selectionEnd: 4);

        Assert.Equal("=$A$1+$B$1", EditorText(cut));
    }

    [Fact] // ADR-0051 / SH-28 / DC-45: while pointing, F4 cycles the pointed Reference and pointing goes on; the next arrow writes the relative form
    public async Task SH28_F4_while_pointing_cycles_the_pointed_reference()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=");
        await PressInEditorAsync(cut, "ArrowDown", "=", 1);

        await PressInEditorAsync(cut, "F4", "=A2", 3);
        Assert.Equal("=$A$2", EditorText(cut));
        Assert.Equal("A2", NameBox(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=$A$2", 5);

        Assert.Equal("=A3", EditorText(cut));
    }

    [Fact] // ADR-0051 / SH-28: a function name, a number and text that is not a Formula are left as they are
    public async Task SH28_F4_leaves_what_is_not_a_reference()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "B2");

        await PressInEditorAsync(cut, "F4", "B2", 2);
        Assert.Equal("B2", EditorText(cut));
        Assert.Null(SheetFormulaAids.CycleReference("=SUM(A1)", 2, 2));
        Assert.Null(SheetFormulaAids.CycleReference("=1+2", 4, 4));
    }

    [Fact] // ADR-0051 / SH-28: the engine's cycle is handed to the grid as it is, text and the span rewritten — for a caret as for a selection
    public void SH28_the_engines_cycle_is_the_grids_rewrite()
    {
        Assert.Equal(new global::ExGrid.Cells.EditorRewrite("=$A$1+B2", 1, 5), SheetFormulaAids.CycleReference("=A1+B2", 3, 3));
        Assert.Equal(new global::ExGrid.Cells.EditorRewrite("=$A$1+$B$2", 1, 10), SheetFormulaAids.CycleReference("=A1+B2", 1, 6));
    }

    /// <summary>The selection the grid last asked the listener to place: text, start, end.</summary>
    private (string Text, int Start, int End) LastPlaced()
    {
        var placed = JSInterop.Invocations.Last(i => i.Identifier == "setCaret");
        return ((string)placed.Arguments[0]!, (int)placed.Arguments[1]!, (int)placed.Arguments[2]!);
    }

    [Fact] // ADR-0051 / SH-28 / C-diff-4, C-diff-3: in Edit mode (F2), =SUM(A1:B2) with the caret moved after B2 by ← cycles B2 alone, each form selected (observed, the seventeenth Windows run, Part C, case 5: 8-12, 8-11, 8-11, 8-10)
    public async Task SH28_in_edit_mode_F4_cycles_the_end_of_a_range_at_the_caret_and_selects_it()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=SUM(A1:B2)");
        await PressInEditorAsync(cut, "F2", "=SUM(A1:B2)", 11);
        var (text, start, end) = ("=SUM(A1:B2)", 10, 10);
        var seen = new List<(string, int, int)>();

        for (var press = 0; press < 4; press++)
        {
            // The first press carries the caret ← moved; each after it, the selection the one before placed.
            await PressInEditorAsync(cut, "F4", text, start, selectionEnd: end, moved: press == 0);
            (text, start, end) = LastPlaced();
            seen.Add((text, start, end));
        }

        Assert.Equal([("=SUM(A1:$B$2)", 8, 12), ("=SUM(A1:B$2)", 8, 11), ("=SUM(A1:$B2)", 8, 11), ("=SUM(A1:B2)", 8, 10)], seen);
        Assert.Equal("=SUM(A1:B2)", EditorText(cut));
    }

    [Fact] // ADR-0051 / SH-28 / C-diff-3, C-diff-6, C-diff-8: typed into a cell (Enter mode), F4 leaves a caret at the end of the rewritten end, in upper case (observed, the seventeenth Windows run, Part C, cases 21 and 22)
    public async Task SH28_in_enter_mode_F4_leaves_a_caret_after_the_rewritten_reference()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=b2:a1");

        await PressInEditorAsync(cut, "F4", "=b2:a1", 6);

        Assert.Equal("=b2:$A$1", EditorText(cut));
        Assert.Equal(("=b2:$A$1", 8, 8), LastPlaced());
    }

    [Fact] // ADR-0051 / SH-28 / DC-45: while pointing, F4 cycles both ends of the pointed range together and pointing goes on over it, the caret at its end, so that a key typed next follows it (observed, R1 (vi): =, ↓, Shift+↓, F4 in D10 gave =$D$11:$D$12; Part C, case 16: + typed after F4 follows the Reference)
    public async Task SH28_while_pointing_F4_cycles_both_ends_of_the_pointed_range()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=");
        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        await PressInEditorAsync(cut, "ArrowDown", "=D11", 4, shift: true);
        Assert.Equal("=D11:D12", EditorText(cut));

        await PressInEditorAsync(cut, "F4", "=D11:D12", 8);

        Assert.Equal("=$D$11:$D$12", EditorText(cut));
        Assert.Equal(("=$D$11:$D$12", 12, 12), LastPlaced());
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("D11", NameBox(cut));

        // Pointing goes on over the rewritten range: F4 again cycles it whole.
        await PressInEditorAsync(cut, "F4", "=$D$11:$D$12", 12);
        Assert.Equal("=D$11:D$12", EditorText(cut));

        // A key typed next follows the Reference, and pointing ends.
        await TypeAsync(cut, "=D$11:D$12+");
        Assert.Equal("=D$11:D$12+", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-selection .ex-point"));
    }

    [Fact] // ADR-0051 / ADR-0010, ED-2: while pointing, Shift+Home extends the outline to the row's first column and pointing goes on (observed, R1 (viii): =, ↓, Shift+Home in D10 gave =A11:D11, in Point)
    public async Task ED2_while_pointing_shift_home_extends_to_the_rows_first_column()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=");
        await PressInEditorAsync(cut, "ArrowDown", "=", 1);

        await PressInEditorAsync(cut, "Home", "=D11", 4, shift: true);

        Assert.Equal("=A11:D11", EditorText(cut));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("D11", NameBox(cut));
        Assert.Equal(new CellPosition(9, 3), Grid(cut).Instance.ReadSelection().Selection.Focus);
    }

    [Fact] // ADR-0051 / ADR-0010, ED-2: while pointing at a range, Shift+Home extends it from its fixed end to the row's first column, as it extends a Selection (a reading: Excel was asked from a single cell, R1 (viii))
    public async Task ED2_while_pointing_at_a_range_shift_home_extends_from_its_fixed_end()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=");
        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        await PressInEditorAsync(cut, "ArrowDown", "=D11", 4, shift: true);

        await PressInEditorAsync(cut, "Home", "=D11:D12", 8, shift: true);

        Assert.Equal("=A11:D12", EditorText(cut));
        Assert.Equal("D11", NameBox(cut));
    }

    [Fact] // ADR-0051 / ADR-0010, ED-2: typed = into D10, Shift+Home points from D10 to the row's first column (observed with a Ctrl+C before it, R1's reading after (vi)'s first step: = in D10, a Ctrl+C that copied nothing, then Shift+Home pointed, and a Ctrl+C copied A10:D10; the text =A10:D10 by generalisation)
    public async Task ED2_after_equals_shift_home_points_from_the_edited_cell()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "=");

        await PressInEditorAsync(cut, "Home", "=", 1, shift: true);

        Assert.Equal("=A10:D10", EditorText(cut));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
    }

    [Theory] // ADR-0010 / ED-2: typed into a cell (Enter mode), Ctrl+→ commits and moves to the Sheet's edge, Ctrl+Shift+→ commits and selects to it, and the Shift+arrows commit and extend from D10 (observed, R1 (vii))
    [InlineData("ArrowRight", true, false, "XFD10", 9, 16383, 9, 16383)]
    [InlineData("ArrowRight", true, true, "D10", 9, 3, 9, 16383)]
    [InlineData("ArrowRight", false, true, "D10", 9, 3, 9, 4)]
    [InlineData("ArrowDown", false, true, "D10", 9, 3, 10, 3)]
    [InlineData("ArrowLeft", false, true, "D10", 9, 3, 9, 2)]
    public async Task ED2_in_enter_mode_the_keys_commit_and_move_or_extend(
        string key, bool ctrl, bool shift, string nameBox, int focusRow, int focusColumn, int extentRow, int extentColumn)
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "D10", "abc");

        await PressInEditorAsync(cut, key, "abc", 3, shift: shift, ctrl: ctrl);

        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(nameBox, NameBox(cut));
        var selection = Grid(cut).Instance.ReadSelection().Selection;
        Assert.Equal(new CellPosition(focusRow, focusColumn), selection.Focus);
        Assert.Equal(new CellPosition(extentRow, extentColumn), selection.Extent);
        await GoToAsync(cut, "D10");
        Assert.Equal("abc", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
    }

    // ---- The editor's verdict (ADR-0034) ----

    [Fact] // ADR-0034 / TYPED-054: signed text the engine reads as a Formula and refuses is rejected by the editor, which holds it with the engine's reason
    public async Task Signed_text_the_engine_refuses_holds_the_editor_with_its_reason()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "-B2 C2");

        await PressAsync(cut, "Enter");

        // The editor holds the text, flagged, rather than committing into OnEditAsync's net.
        var editor = cut.Find(".ex-viewport .ex-editor");
        Assert.Equal("-B2 C2", editor.GetAttribute("value"));
        Assert.Equal("true", editor.GetAttribute("aria-invalid"));
        Assert.Contains("the intersection operator", cut.Markup);
        Assert.Empty(cut.Instance.ToDocument().Cells);
        Assert.Equal("", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Theory] // ADR-0034: signed text the engine reads — a signed Formula, a signed number — commits
    [InlineData("-B2", "0")]
    [InlineData("-5", "-5")]
    [InlineData("+5", "5")]
    public async Task Signed_text_the_engine_reads_commits(string typed, string shown)
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", typed);

        await PressAsync(cut, "Enter");

        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(shown, CellText(cut, "A1"));
    }
}
