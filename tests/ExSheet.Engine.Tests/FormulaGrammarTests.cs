using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class FormulaGrammarTests
{
    [Theory] // ADR-0047 (SH-6): every Reference form parses, and the invariant syntax round-trips exactly
    [InlineData("=A1")]
    [InlineData("=$A$1")]
    [InlineData("=A$1")]
    [InlineData("=$A1")]
    [InlineData("=A1:B2")]
    [InlineData("=$A$1:$B$2")]
    [InlineData("=A:A")]
    [InlineData("=$A:$C")]
    [InlineData("=1:1")]
    [InlineData("=$2:$5")]
    [InlineData("=XFD1048576")]
    [InlineData("=A:XFD")]
    [InlineData("=1:1048576")]
    [InlineData("=Sheet2!A1")]
    [InlineData("=Sheet2!A1:B2")]
    [InlineData("=Sheet2!A:A")]
    [InlineData("='My Sheet'!$A$1")]
    [InlineData("='It''s'!A1")]
    [InlineData("='A1'!B2")]
    [InlineData("=Positions[PV]")]
    [InlineData("=Positions[[Market Value]]")]
    [InlineData("=Positions[[Rate'#]]")]
    public void Every_reference_form_round_trips(string formula)
    {
        Assert.Equal(formula, Entry.FromFormula(formula).Formula);
    }

    [Theory] // ADR-0047: operators, literals and calls round-trip in the invariant syntax
    [InlineData("=1+2*3-4/5")]
    [InlineData("=(A1+B1)*C1")]
    [InlineData("=-2^2")]
    [InlineData("=2^3^2")]
    [InlineData("=50%")]
    [InlineData("=A1&\" \"&B1")]
    [InlineData("=\"say \"\"hi\"\"\"")]
    [InlineData("=A1<>B1")]
    [InlineData("=A1<=B1")]
    [InlineData("=A1>=B1")]
    [InlineData("=A1<B1")]
    [InlineData("=A1>B1")]
    [InlineData("=A1=B1")]
    [InlineData("=TRUE")]
    [InlineData("=FALSE")]
    [InlineData("=#N/A")]
    [InlineData("=#DIV/0!")]
    [InlineData("=#NULL!")]
    [InlineData("=#VALUE!")]
    [InlineData("=#REF!")]
    [InlineData("=#NAME?")]
    [InlineData("=#NUM!")]
    [InlineData("=1.5")]
    [InlineData("=0.001")]
    [InlineData("=unknown(1,,A1:B2)")]
    [InlineData("=unknown()")]
    [InlineData("=someName")]
    public void Operators_and_literals_round_trip(string formula)
    {
        Assert.Equal(formula, Entry.FromFormula(formula).Formula);
    }

    [Theory] // ADR-0047: the stored spelling is Excel's — References and literals in upper case, corners top-left first
    [InlineData("=a1+$b$2", "=A1+$B$2")]
    [InlineData("=b2:a1", "=A1:B2")]
    [InlineData("=B1:A2", "=A1:B2")]
    [InlineData("=c:a", "=A:C")]
    [InlineData("=5:2", "=2:5")]
    [InlineData("=true", "=TRUE")]
    [InlineData("=#n/a", "=#N/A")]
    [InlineData("=.5", "=0.5")]
    [InlineData("=1.50", "=1.5")]
    public void A_formula_is_recorded_in_excels_spelling(string typed, string recorded)
    {
        Assert.Equal(recorded, Entry.FromFormula(typed).Formula);
    }

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

    [Fact] // ADR-0047: a name outside the extent is not a Reference, and an unknown name is #NAME?
    public void Text_beyond_the_extent_is_a_name()
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=XFE1").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=A1048577").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=profit").Error);
    }

    [Fact] // ADR-0047: a function outside the declared set is #NAME?, whatever its arguments
    public void An_unknown_function_is_name_error()
    {
        var sheet = NewSheet();

        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=NOSUCHFUNCTION(1)").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=NOSUCHFUNCTION(1/0)").Error);
        Assert.Equal(ErrorValue.Name, sheet.Evaluate("=VLOOKUP(1,A1:B2,2,FALSE)").Error);
    }

    [Fact] // ADR-0049: a structured reference to a Linked Table that was never declared is #NAME?
    public void An_undeclared_linked_table_is_name_error()
    {
        Assert.Equal(ErrorValue.Name, NewSheet().Evaluate("=Positions[PV]").Error);
    }

    [Fact] // ADR-0046/0047: the Sheet qualifier is recorded; with one Sheet, a qualifier other than its name names no cell
    public void A_sheet_qualified_reference_is_recorded_and_names_no_cell()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "5");

        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=Sheet2!A1").Error);
        Assert.Equal("=Sheet2!A1", sheet.GetEntryText(CellAddress.Parse("ZZ1000")));
    }

    [Fact] // ADR-0047: a Reference to a whole column or row is a range, and one Formula cannot show many cells
    public void A_multi_cell_reference_as_a_result_is_value_error()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "5");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=A1:A2").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=A:A").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=1:1").Error);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=A1:A2+1").Error);
        Assert.Equal(5, sheet.Evaluate("=A1:A1").Number);
        Assert.Equal(5, sheet.Evaluate("=$A$1").Number);
    }
}
