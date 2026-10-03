using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class DateDifferencesTests
{
    public static IEnumerable<object[]> YearFracCases() => ExcelCorpus.Cases("yearfrac").Select(c => new object[] { c.GetProperty("id").GetString()! });

    [Theory, MemberData(nameof(YearFracCases))]
    public void ADR0047_YearFrac_matches_the_Windows_date_matrix(string id) =>
        Assert.Empty(ExcelCorpus.Run(ExcelCorpus.Case("yearfrac", id)));

    public static IEnumerable<object[]> DateDifCases() => ExcelCorpus.Cases("datedif").Select(c => new object[] { c.GetProperty("id").GetString()! });

    [Theory, MemberData(nameof(DateDifCases))]
    public void ADR0047_DateDif_matches_the_Windows_date_matrix_except_the_MD_refusal(string id) =>
        Assert.Empty(ExcelCorpus.Run(ExcelCorpus.Case("datedif", id)));

    [Theory]
    [InlineData("=DATEDIF(DATE(2020,1,1),DATE(2022,3,1),\"YD\")")]
    [InlineData("=DATEDIF(1,2,\"MD\")")]
    public void ADR0047_DateDif_refuses_unresolved_year_ignoring_and_MD_domains(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Fact]
    public void ADR0047_Date_differences_recalculate_when_a_date_changes()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "59");
        sheet.Enter("B1", "=DATEDIF(A1,61,\"D\")");
        sheet.Enter("C1", "=YEARFRAC(A1,61,2)");
        var changed = sheet.Enter("A1", "60");
        Assert.Equal(1, sheet.Number("B1"));
        Assert.Equal(0.0027777777777777779, sheet.Number("C1"));
        Assert.Equal(["B1", "C1"], changed.Recalculated.Addresses().Order());
    }

    [Fact]
    public void ADR0047_DateDif_counts_the_fictitious_1900_day()
    {
        Assert.Equal(Value.FromNumber(2), NewSheet().Evaluate("=DATEDIF(59,61,\"D\")"));
    }

    [Fact]
    public void ADR0047_YearFrac_counts_actual_days_on_the_360_day_basis()
    {
        Assert.Equal(Value.FromNumber(0.0055555555555555558), NewSheet().Evaluate("=YEARFRAC(59,61,2)"));
    }
}
