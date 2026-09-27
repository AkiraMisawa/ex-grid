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
}
