using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class ObservedRoundingTests
{
    [Theory] // ADR-0047: division at an unobserved integer boundary must not invent a tolerance.
    [InlineData("=FLOOR.MATH(0.3,0.1)")]
    [InlineData("=CEILING.MATH(0.07,0.01)")]
    public void Ambiguous_integer_boundaries_are_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: a nonzero quotient that underflows is not a known zero.
    [InlineData("=CEILING.MATH(1E-300,1E300)")]
    [InlineData("=FLOOR.MATH(-1E-300,1E300)")]
    public void A_nonzero_quotient_that_underflows_is_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate(formula));

    public static IEnumerable<object[]> CeilingCases() => ExcelCorpus.Cases("ceiling-math").Select(c => new object[] { c.GetProperty("id").GetString()! });

    [Theory, MemberData(nameof(CeilingCases))]
    public void ADR0047_CeilingMath_matches_the_Windows_observation(string id) =>
        Assert.Empty(ExcelCorpus.Run(ExcelCorpus.Case("ceiling-math", id)));

    public static IEnumerable<object[]> FloorCases() => ExcelCorpus.Cases("floor-math").Select(c => new object[] { c.GetProperty("id").GetString()! });

    [Theory, MemberData(nameof(FloorCases))]
    public void ADR0047_FloorMath_matches_the_Windows_observation(string id) =>
        Assert.Empty(ExcelCorpus.Run(ExcelCorpus.Case("floor-math", id)));

    public static IEnumerable<object[]> MroundCases() => ExcelCorpus.Cases("mround").Select(c => new object[] { c.GetProperty("id").GetString()! });

    [Theory, MemberData(nameof(MroundCases))]
    public void ADR0047_Mround_matches_the_Windows_observation_or_refuses_the_unresolved_domain(string id) =>
        Assert.Empty(ExcelCorpus.Run(ExcelCorpus.Case("mround", id)));

    [Theory]
    [InlineData("=MROUND(10,3)", 9)]
    [InlineData("=MROUND(-10,-3)", -9)]
    [InlineData("=MROUND(17,5)", 15)]
    [InlineData("=MROUND(18,5)", 20)]
    [InlineData("=MROUND(0,-5)", 0)]
    [InlineData("=CEILING.MATH(2.5)", 3)]
    [InlineData("=FLOOR.MATH(2.5)", 2)]
    public void ADR0047_Documented_integer_multiple_and_default_significance_cases(string formula, double expected) =>
        Assert.Equal(Value.FromNumber(expected), NewSheet().Evaluate(formula));

    [Theory]
    [InlineData("=MROUND(1.3,0.2)")]
    [InlineData("=MROUND(-1.3,-0.2)")]
    public void ADR0047_Mround_refuses_fractional_multiples(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory]
    [InlineData("=MROUND(1.7E307*10,1E307*10)")]
    [InlineData("=CEILING.MATH(1.7E307*10,1E307*10)")]
    [InlineData("=FLOOR.MATH(-1.7E307*10,1E307*10)")]
    public void ADR0047_Rounding_overflow_returns_an_Error_Value(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate(formula));

    [Theory]
    [InlineData("=MROUND(2.5,TRUE)")]
    [InlineData("=MROUND(A1,1)")]
    public void ADR0047_Mround_rejects_boolean_arguments_including_References(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "TRUE");
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate(formula));
    }

    [Fact]
    public void ADR0047_Rounding_recalculates_when_the_multiple_changes()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "3");
        sheet.Enter("B1", "=MROUND(10,A1)");
        Assert.Equal(9, sheet.Number("B1"));
        var changed = sheet.Enter("A1", "4");
        Assert.Equal(12, sheet.Number("B1"));
        Assert.Equal(["B1"], changed.Recalculated.Addresses());
    }

    [Fact]
    public void ADR0047_Mround_rounds_to_an_integer_multiple_away_from_zero_at_halfway()
    {
        Assert.Equal(Value.FromNumber(-3), NewSheet().Evaluate("=MROUND(-2.5,-1)"));
    }

    [Fact]
    public void ADR0047_FloorMath_uses_the_observed_negative_number_mode()
    {
        Assert.Equal(Value.FromNumber(-2), NewSheet().Evaluate("=FLOOR.MATH(-2.5,1,1)"));
    }

    [Fact]
    public void ADR0047_CeilingMath_uses_the_observed_negative_number_mode()
    {
        var sheet = NewSheet();
        Assert.Equal(Value.FromNumber(-3), sheet.Evaluate("=CEILING.MATH(-2.5,1,1)"));
    }
}
