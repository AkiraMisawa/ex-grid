using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class FormulaGrammarTests
{
    [Theory] // ADR-0047: one spelling in every culture — ',' separates arguments and '.' is the decimal separator
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    [InlineData("en-US")]
    public void The_formula_syntax_is_invariant_in_every_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));

            Assert.Equal(3, sheet.Evaluate("=1.5*2").Number);
            Assert.Equal("=1.5*2", sheet.GetEntryText(CellAddress.Parse("ZZ1000")));
            Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", "=1,5*2"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory] // ADR-0047: what ExSheet cannot read is refused, never guessed at
    [InlineData("=1+")]
    [InlineData("=(1")]
    [InlineData("=1)")]
    [InlineData("=A1 B1")]       // intersection is not read
    [InlineData("=(A1,B1)")]     // union is not read
    [InlineData("={1,2}")]       // array constants are not read
    [InlineData("=\"open")]
    [InlineData("=#BOGUS!")]
    [InlineData("=#GETTING_DATA")]
    [InlineData("=#CIRC!")]
    [InlineData("=A1:B")]
    [InlineData("=Positions[#All]")]
    [InlineData("=Positions[@PV]")]
    [InlineData("=Positions[]")]
    [InlineData("=1E999")]
    [InlineData("=*2")]
    public void Unreadable_text_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }

    [Theory] // ADR-0047, second run: an intersection with a name in it is read, as Excel reads "- item one" typed, and is #NAME?
    [InlineData("=- item one", "=- item one")]
    [InlineData("=item one", "=item one")]
    [InlineData("=a1 total", "=A1 total")]
    [InlineData("=total A1:B2", "=total A1:B2")]
    [InlineData("=item one two", "=item one two")]
    [InlineData("=item one%", "=item one%")]
    public void An_intersection_with_a_name_is_NAME(string typed, string stored)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("B5"), typed);

        Assert.Equal(stored, sheet.GetEntry(CellAddress.Parse("B5"))!.Formula);
        Assert.Equal(ErrorValue.Name, sheet.GetValue(CellAddress.Parse("B5"))!.Value.Error);
    }

    [Theory] // ADR-0047: the intersection of two References is still not read
    [InlineData("=A1 B1 total")]
    [InlineData("=A1 TRUE")]
    [InlineData("=item SUM(A1)")]
    [InlineData("=item 1")]
    public void An_intersection_without_a_name_or_with_a_non_reference_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }
}
