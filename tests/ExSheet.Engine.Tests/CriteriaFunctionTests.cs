using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class CriteriaFunctionTests
{
    public static TheoryData<string, string> Observations => new(
        new[] { "sumif", "sumifs", "countif", "countifs", "averageif", "averageifs", "maxifs", "minifs", "countblank" }
            .SelectMany(area => ExcelCorpus.Cases(area).Select(c => (area, c.GetProperty("id").GetString()!))));

    [Theory, MemberData(nameof(Observations))] // ADR-0047: Windows Excel's observed criteria and aggregation semantics.
    public void Criteria_functions_match_the_observed_values(string area, string id)
    {
        var differences = ExcelCorpus.Run(ExcelCorpus.Case(area, id));
        Assert.True(differences.Count == 0, $"{id}: {string.Join("; ", differences)}");
    }

    [Fact] // ADR-0047: the effective SUMIF result range, including its extension, is a dependency.
    public void A_change_to_the_extended_result_range_recalculates_the_formula()
    {
        var sheet = NewSheet();
        sheet.Enter("A4", "1");
        sheet.Enter("B4", "40");
        sheet.Enter("D1", "=SUMIF(A1:A4,1,B1:B2)");
        Assert.Equal(40, sheet.Number("D1"));
        var change = sheet.Enter("B4", "80");
        Assert.Equal(80, sheet.Number("D1"));
        Assert.Equal(["D1"], change.Recalculated.Addresses());
        Assert.Empty(sheet.Enter("B5", "90").Recalculated);
    }

    [Fact] // ADR-0047: a cycle in an implicit result footprint taints even a row that does not match.
    public void An_extended_result_range_cannot_hide_a_cycle()
    {
        var sheet = NewSheet();
        sheet.Enter("A4", "1");
        sheet.Enter("B4", "=D1");
        sheet.Enter("D1", "=SUMIF(A1:A4,2,B1:B2)");
        Assert.Equal(Value.FromError(ErrorValue.Circ), sheet.GetValue(CellAddress.Parse("D1")));
        sheet.Enter("B4", "40");
        Assert.Equal(0, sheet.Number("D1"));
    }

    [Fact] // ADR-0047: absent cells count, without building a dense array of the Sheet.
    public void Whole_columns_keep_their_absent_cells_when_counted()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "=\"\"");
        sheet.Enter("A3", "1");
        sheet.Enter("C1", "=COUNTIF(A:A,\"=\")");
        sheet.Enter("C2", "=COUNTBLANK(A:A)");
        sheet.Enter("C3", "=COUNTIFS(A:A,\"=\",B:B,\"=\")");
        Assert.Equal(Sheet.RowCount - 2, sheet.Number("C1"));
        Assert.Equal(Sheet.RowCount - 1, sheet.Number("C2"));
        Assert.Equal(Sheet.RowCount - 2, sheet.Number("C3"));
    }

    [Theory] // ADR-0047: incomplete criteria pairs are entry refusals, not runtime exceptions.
    [InlineData("=SUMIFS(A1:A2,B1:B2)")]
    [InlineData("=SUMIFS(A1:A2,B1:B2,1,C1:C2)")]
    [InlineData("=COUNTIFS(A1:A2,1,B1:B2)")]
    [InlineData("=AVERAGEIFS(A1:A2,B1:B2,1,C1:C2)")]
    public void An_incomplete_pair_is_refused_on_entry(string formula) =>
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));

    [Theory] // ADR-0047: unobserved collation and numeric spellings must not return plausible counts.
    [InlineData("en-US", "a", ">a")]
    [InlineData("en-US", "a", ">")]
    [InlineData("en-US", "a", ">TRUE")]
    [InlineData("en-US", "é", "é")]
    [InlineData("en-US", "a", "1%")]
    [InlineData("en-US", "a", "1/2")]
    [InlineData("en-US", "a", "$1")]
    [InlineData("en-US", "a", "1,5")]
    [InlineData("de-DE", "1.5", "1.5")]
    [InlineData("de-DE", "1.5", "1")]
    [InlineData("en-US", "1.0000000000000001", "1")]
    [InlineData("en-US", "a", "1.0000000000000001")]
    public void Unadmitted_criteria_domains_are_refused(string culture, string candidate, string criterion)
    {
        var sheet = new Sheet(System.Globalization.CultureInfo.GetCultureInfo(culture));
        sheet.SetEntries([
            new(CellAddress.Parse("A1"), Entry.FromValue(Value.FromText(candidate))),
            new(CellAddress.Parse("B1"), Entry.FromValue(Value.FromText(criterion))),
        ]);
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=COUNTIF(A1,B1)"));
    }

    [Fact] // ADR-0047: Excel's long-criteria matching is not inferred from short strings.
    public void Criteria_longer_than_255_characters_are_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", new string('a', 256));
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=COUNTIF(A1,A1)"));
    }
}
