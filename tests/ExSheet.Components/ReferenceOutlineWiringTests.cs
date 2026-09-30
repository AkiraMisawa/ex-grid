using Bunit;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet declares ExGrid's References function (ticket 28, ADR-0057; DC-46): while a Formula is
/// edited on a Sheet, each range its References name is outlined in the selection overlay in the
/// colour the grid gave it, and a commit or a cancel takes the outlines away. What a Reference is
/// stays the engine's answer; these tests read only the wiring.
/// </summary>
public class ReferenceOutlineWiringTests : SheetTestContext
{
    private static List<string?> OutlineClasses(IRenderedComponent<ExSheet> cut) =>
        [.. cut.FindAll(".ex-selection .ex-reference-outline").Select(e => e.GetAttribute("class"))];

    /// <summary>Goes to <paramref name="address"/>, opens Overwrite with the first character and types the rest.</summary>
    private static async Task StartTypingAsync(IRenderedComponent<ExSheet> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1) await TypeAsync(cut, typed);
    }

    [Fact] // ADR-0057 / DC-46: =A1+B2:C3 on a Sheet outlines A1 and B2:C3 in the first two colours, A1 over A1's own cell
    public async Task A_formula_outlines_its_references()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "A1");
        var a1 = cut.Find(".ex-selection .ex-focus").GetAttribute("style");

        await StartTypingAsync(cut, "E5", "=A1+B2:C3");

        Assert.Equal(["ex-reference-outline ex-reference-1", "ex-reference-outline ex-reference-2"], OutlineClasses(cut));
        Assert.Equal(a1, cut.Find(".ex-selection .ex-reference-1").GetAttribute("style"));
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: =A1+A1 outlines A1 once per Reference, in one colour
    public async Task The_same_cell_twice_is_outlined_twice_in_one_colour()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "E5", "=A1+A1");

        Assert.Equal(["ex-reference-outline ex-reference-1", "ex-reference-outline ex-reference-1"], OutlineClasses(cut));
    }

    [Fact] // ADR-0057 / DC-46, the eighth Windows run: = ↓ ↓ outlines what Point points at in the first colour, with Point's dashes over it; Enter takes both away
    public async Task Points_outline_is_the_first_colour_until_the_commit()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "B2", "=");

        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        await PressInEditorAsync(cut, "ArrowDown", "=B3", 3);

        Assert.Equal("=B4", EditorText(cut));
        Assert.Equal(["ex-reference-outline ex-reference-1"], OutlineClasses(cut));
        Assert.Equal("ex-point ex-point-on-reference", cut.Find(".ex-selection .ex-point").GetAttribute("class"));
        await PressInEditorAsync(cut, "Enter", "=B4", 3);
        Assert.Empty(cut.FindAll(".ex-reference-outline"));
        Assert.Empty(cut.FindAll(".ex-point"));
    }

    [Fact] // ADR-0057 / DC-46: a constant is not a Formula and outlines nothing; Escape takes a Formula's outlines away
    public async Task Only_a_formula_is_outlined_and_escape_removes_it()
    {
        var cut = RenderSheet();
        await StartTypingAsync(cut, "E5", "A1+B2");
        Assert.Empty(cut.FindAll(".ex-reference-outline"));

        await TypeAsync(cut, "=A1+B2");
        Assert.Equal(2, OutlineClasses(cut).Count);
        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-reference-outline"));
    }

    [Fact] // ADR-0057 / DC-46, SH-30: this Sheet's own qualifier names the same cells, so Sheet1!A1+A1 outlines A1 in one colour
    public async Task This_sheets_qualifier_names_the_same_cells()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "E5", "=Sheet1!A1+A1");

        Assert.Equal(["ex-reference-outline ex-reference-1", "ex-reference-outline ex-reference-1"], OutlineClasses(cut));
    }

    [Fact] // ADR-0057 / DC-46, SH-30: an unfinished Formula is outlined as far as it goes
    public async Task An_unfinished_formula_is_outlined_as_far_as_it_goes()
    {
        var cut = RenderSheet();

        await StartTypingAsync(cut, "E5", "=SUM(A1,B2:C3,");

        Assert.Equal(["ex-reference-outline ex-reference-1", "ex-reference-outline ex-reference-2"], OutlineClasses(cut));
    }
}
