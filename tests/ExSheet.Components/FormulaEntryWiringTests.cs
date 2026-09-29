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

    [Fact] // ADR-0051: =SU offers every declared function starting with SU, which is SUM alone (ADR-0047)
    public async Task Su_offers_sum()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "A1", "=SU");

        Assert.Equal(["SUM"], Candidates(cut));
    }

    [Fact] // ADR-0051: =X offers XLOOKUP, without regard to case
    public async Task X_offers_xlookup()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "A1", "=x");

        Assert.Equal(["XLOOKUP"], Candidates(cut));
    }

    [Fact] // ADR-0051: Tab accepts the engine's replacement over the engine's span, and the hint follows
    public async Task Tab_accepts_and_the_hint_follows()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=1+su");

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

    [Fact] // ADR-0051 / SH-28 / DC-45: F4 cycles in the Formula Bar, and the Cell Editor shows the same text
    public async Task SH28_F4_cycles_the_reference_in_the_formula_bar()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2");
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await TypeInBarAsync(cut, "=SUM(A1:C3)");

        await PressInEditorAsync(cut, "F4", "=SUM(A1:C3)", 10, fromBar: true);

        Assert.Equal("=SUM($A$1:$C$3)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("=SUM($A$1:$C$3)", EditorText(cut));
    }

    [Fact] // ADR-0051 / SH-28: a selection over several References cycles each of them
    public async Task SH28_a_selection_cycles_every_reference_it_covers()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "A1", "=B1+C1");

        await PressInEditorAsync(cut, "F4", "=B1+C1", 1, selectionEnd: 6);

        Assert.Equal("=$B$1+$C$1", EditorText(cut));
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

    [Fact] // ADR-0051 / SH-28: the engine's cycle is handed to the grid as it is, text and selection
    public void SH28_the_engines_cycle_is_the_grids_rewrite()
    {
        Assert.Equal(new global::ExGrid.Cells.EditorRewrite("=$A$1+B2", 5, 5), SheetFormulaAids.CycleReference("=A1+B2", 3, 3));
        Assert.Equal(new global::ExGrid.Cells.EditorRewrite("=$A$1+$B$2", 1, 10), SheetFormulaAids.CycleReference("=A1+B2", 1, 6));
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
