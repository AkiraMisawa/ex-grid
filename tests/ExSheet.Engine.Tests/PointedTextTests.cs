using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What a Pointing Scope writes (ADR-0058, SH-32's layer 1 half): a pressed cell as the lookup of its
/// row by key, <c>XLOOKUP(&lt;key&gt;, T[&lt;key column&gt;], T[&lt;column&gt;])</c>, with the key
/// written as Excel writes a constant of its kind, and a pressed column as its structured reference.
/// What is written reads back as the cell it was written for.
/// </summary>
public class PointedTextTests
{
    [Theory] // ADR-0058 / SH-32: text is written in double quotes, with any quote inside doubled
    [InlineData("R-4471", "\"R-4471\"")]
    [InlineData("R-\"5\"", "\"R-\"\"5\"\"\"")]
    [InlineData("", "\"\"")]
    [InlineData("TRUE", "\"TRUE\"")]
    public void ADR0058_text_is_written_quoted(string key, string written)
    {
        Assert.Equal(written, FormulaEntry.ConstantText(Value.FromText(key)));
    }

    [Theory] // ADR-0058 / SH-32: a number is written in the invariant form the engine writes a number constant in
    [InlineData(1250, "1250")]
    [InlineData(-0.5, "-0.5")]
    [InlineData(318.25, "318.25")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(0, "0")]
    public void ADR0058_a_number_is_written_invariant(double key, string written)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // A culture whose decimal separator is a comma changes nothing.
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(written, FormulaEntry.ConstantText(Value.FromNumber(key)));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact] // ADR-0058 / SH-32: a boolean is written TRUE or FALSE, and an Error Value not at all
    public void ADR0058_a_boolean_is_written_in_capitals_and_an_error_not_at_all()
    {
        Assert.Equal("TRUE", FormulaEntry.ConstantText(Value.FromBoolean(true)));
        Assert.Equal("FALSE", FormulaEntry.ConstantText(Value.FromBoolean(false)));
        Assert.Null(FormulaEntry.ConstantText(Value.FromError(ErrorValue.NA)));
    }

    [Fact] // ADR-0058 / SH-32: a pressed column is its structured reference, escaped as the engine writes one back
    public void ADR0058_a_column_is_its_structured_reference()
    {
        Assert.Equal("Positions[PV]", FormulaEntry.StructuredReferenceText("Positions", "PV"));
        Assert.Equal("Positions[Market Value]", FormulaEntry.StructuredReferenceText("Positions", "Market Value"));
        Assert.Equal("Rates[Rate'#]", FormulaEntry.StructuredReferenceText("Rates", "Rate#"));
        Assert.Throws<ArgumentException>(() => FormulaEntry.StructuredReferenceText("Positions", ""));
    }

    [Fact] // ADR-0058 / SH-32: a pressed cell is the lookup of its row by key, the key column and the pressed column
    public void ADR0058_a_cell_is_the_lookup_of_its_row_by_key()
    {
        Assert.Equal(
            "XLOOKUP(\"R-4471\", Positions[Id], Positions[PV])",
            FormulaEntry.LookupText("Positions", "Id", Value.FromText("R-4471"), "PV"));
        Assert.Equal(
            "XLOOKUP(42, Cds[Code], Cds[Spread])",
            FormulaEntry.LookupText("Cds", "Code", Value.FromNumber(42), "Spread"));
        Assert.Throws<ArgumentException>(() => FormulaEntry.LookupText("Positions", "Id", Value.FromError(ErrorValue.NA), "PV"));
    }

    [Fact] // ADR-0058 / SH-32: what is written reads the cell it was written for, for a key of each kind
    public void ADR0058_what_is_written_reads_the_pressed_cell()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("T", ["Key", "Rate#", "PV"], key: "Key");
        sheet.PushLinkedTable("T",
        [
            [Value.FromText("R-\"5\""), Value.FromNumber(1), Value.FromNumber(10)],
            [Value.FromNumber(0.1), Value.FromNumber(2), Value.FromNumber(20)],
            [Value.FromBoolean(true), Value.FromNumber(3), Value.FromNumber(30)],
            [Value.FromText("1250"), Value.FromNumber(4), Value.FromNumber(40)],
        ]);

        Assert.Equal(Value.FromNumber(10), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromText("R-\"5\""), "PV")));
        Assert.Equal(Value.FromNumber(2), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromNumber(0.1), "Rate#")));
        Assert.Equal(Value.FromNumber(30), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromBoolean(true), "PV")));
        Assert.Equal(Value.FromNumber(40), sheet.Evaluate("=" + FormulaEntry.LookupText("T", "Key", Value.FromText("1250"), "PV")));
        Assert.Equal(Value.FromNumber(100), sheet.Evaluate("=SUM(" + FormulaEntry.StructuredReferenceText("T", "PV") + ")"));
    }
}
