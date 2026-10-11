using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// <c>LET</c> through the component (ADR-0181, docs/specs/exsheet-functions ticket 14, SH-67): a
/// <c>LET</c> Excel's keyboard refuses is refused on entry exactly as a Formula the engine cannot read
/// is (ADR-0034) — the edit stays open with the engine's reason beside it, the text as typed, and
/// nothing is written — and one it accepts is entered and computed.
/// </summary>
public class LetWiringTests : SheetTestContext
{
    private static async Task CommitAsync(IRenderedComponent<Components.ExSheet> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        await TypeAsync(cut, typed);
        await PressAsync(cut, "Enter");
    }

    private static string Message(IRenderedComponent<Components.ExSheet> cut) => cut.Find(".ex-message").TextContent;

    private static string Unreadable(string typed) =>
        SheetWords.FormulaUnreadable(Assert.Throws<FormulaSyntaxException>(() => Entry.Parse(typed, CultureInfo.GetCultureInfo("en-US"))));

    [Theory] // ADR-0181, ADR-0034, SH-67: a refused LET is refused on entry as an unreadable Formula is
    [InlineData("=LET(x,1,x,2,x)")]
    [InlineData("=LET(x,1)")]
    [InlineData("=LET(x,1,y,2,x+y,3)")]
    [InlineData("=LET(A1,2,A1)")]
    [InlineData("=LET(SUM,2,SUM+1)")]
    [InlineData("=LET(TRUE,2,1)")]
    [InlineData("=LET(R1C1,2,1)")]
    [InlineData("=LET(a.b,2,a.b)")]
    [InlineData("=SUM(")] // the unreadable Formula it is refused as
    public async Task ADR0181_a_refused_let_is_refused_as_an_unreadable_formula_is(string typed)
    {
        var cut = RenderSheet();

        await CommitAsync(cut, "B2", typed);

        Assert.True(cut.Instance.IsEditing);
        Assert.Equal(typed, EditorText(cut));
        Assert.Equal(Unreadable(typed), Message(cut));
        Assert.Equal("true", cut.Find(".ex-viewport .ex-editor").GetAttribute("aria-invalid"));
        Assert.DoesNotContain(cut.Instance.ToDocument().Cells, c => c.Address == CellAddress.Parse("B2"));
    }

    [Fact] // ADR-0181, SH-67: a LET Excel's keyboard accepts is entered, written as typed, and computed
    public async Task ADR0181_a_let_excel_accepts_is_entered()
    {
        var cut = RenderSheet();

        await CommitAsync(cut, "B2", "=let(名前,2,c,3,名前*c)");

        Assert.False(cut.Instance.IsEditing);
        Assert.Equal("6", CellText(cut, "B2"));
        Assert.Equal("=LET(名前,2,c,3,名前*c)", cut.Instance.ToDocument().Cells.Single(c => c.Address == CellAddress.Parse("B2")).Entry!.Formula);
    }
}
