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
