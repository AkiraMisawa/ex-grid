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

        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=XL" });

        Assert.Equal(["XLOOKUP"], Candidates(cut));
    }

    [Fact] // ADR-0051 / DC-19: = ↓ ↓ writes A3, the Selection and the Focus stay, and Enter commits the Formula
    public async Task Equals_down_down_writes_a3()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A3", "7"))));
        await StartTypingAsync(cut, "A1", "=");

        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=A2", EditorText(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=A2", 3);
        Assert.Equal("=A3", EditorText(cut));
        Assert.Equal("A1", NameBox(cut));

        await PressInEditorAsync(cut, "Enter", "=A3", 3);

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

    [Fact] // ADR-0051 / DC-19: a click on a cell writes the clicked cell, and the Focus stays on the edited one
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
        Assert.Equal("A1", NameBox(cut));
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
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=SUM()" });

        // The bar opens Caret; F2 points, with the caret between the parentheses.
        await PressInEditorAsync(cut, "F2", "=SUM()", 5, fromBar: true);
        await PressInEditorAsync(cut, "ArrowUp", "=SUM()", 5, fromBar: true);

        Assert.Equal("=SUM(B1)", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("B2", NameBox(cut));
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
}
