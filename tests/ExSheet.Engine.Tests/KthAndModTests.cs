using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What the seventeenth Windows run and the area pass of 2026-10-12 read of <c>LARGE</c> and
/// <c>SMALL</c>'s k and of <c>MOD</c> (ADR-0047): <c>LARGE(A1:B5,1.5)</c> is the 2nd largest, as
/// <c>AGGREGATE(14,6,A1:A11,1.5)</c> is; <c>SMALL(A1:A9,1.5)</c> the smallest; a k below 1 is
/// <c>#NUM!</c>; <c>MOD(2^27,1)</c> is 0 where Excel 2007 and earlier gave <c>#NUM!</c>; and
/// <c>MOD(123456789012,0.7)</c> is the exact remainder rounded once, which <c>MOD</c> answers where
/// every evaluation that gives Excel's answers agrees, and refuses where they part. The cases themselves are
/// in <c>ExcelCases/large.json</c>, <c>small.json</c>, <c>aggregate.json</c> and <c>mod.json</c>; the
/// rules across the forms are here.
/// </summary>
public class KthAndModTests
{
    private static Sheet Ten()
    {
        // 1 to 10 in A1:A10, so the k-th largest is 11 - k and the k-th smallest is k.
        var sheet = NewSheet();
        for (var i = 1; i <= 10; i++) sheet.Enter($"A{i}", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return sheet;
    }

    [Theory] // ADR-0047 (LARGE-007, AGGREGATE-038): LARGE's k that is not whole is rounded up, in LARGE and in AGGREGATE's 14 alike, and checked against the count after
    [InlineData("1.5", 9)]
    [InlineData("1.2", 9)]
    [InlineData("2.0001", 8)]
    [InlineData("9.5", 1)]
    [InlineData("1+2^-52", 10)]
    public void ADR0047_large_rounds_a_k_that_is_not_whole_up(string k, double expected)
    {
        var sheet = Ten();

        Assert.Equal(expected, sheet.Evaluate($"=LARGE(A1:A10,{k})").Number);
        Assert.Equal(expected, sheet.Evaluate($"=AGGREGATE(14,6,A1:A10,{k})").Number);
    }

    [Theory] // ADR-0047 (the area pass of 2026-10-12: LARGE-011): a k below 1 is #NUM!, whole or not, and not rounded up to 1; rounded up past the count, LARGE's k is #NUM! too
    [InlineData("10.5")]
    [InlineData("0.5")]
    [InlineData("0.99")]
    [InlineData("-0.5")]
    [InlineData("0")]
    public void ADR0047_large_outside_its_count_is_num(string k)
    {
        var sheet = Ten();

        Assert.Equal(ErrorValue.Num, sheet.Evaluate($"=LARGE(A1:A10,{k})").Error);
        Assert.Equal(ErrorValue.Num, sheet.Evaluate($"=AGGREGATE(14,6,A1:A10,{k})").Error);
    }

    [Theory] // ADR-0047 (the area pass of 2026-10-12: SMALL-004, 005, AGGREGATE-060): SMALL's k that is not whole is truncated, in SMALL and in AGGREGATE's 15 alike, and one a hair from a whole number is that number
    [InlineData("1.5", 1)]
    [InlineData("1.2", 1)]
    [InlineData("2.9", 2)]
    [InlineData("9.5", 9)]
    [InlineData("10.5", 10)]
    [InlineData("1+2^-52", 1)]
    [InlineData("2-2^-50", 2)]
    public void ADR0047_small_truncates_a_k_that_is_not_whole(string k, double expected)
    {
        var sheet = Ten();

        Assert.Equal(expected, sheet.Evaluate($"=SMALL(A1:A10,{k})").Number);
        Assert.Equal(expected, sheet.Evaluate($"=AGGREGATE(15,6,A1:A10,{k})").Number);
    }

    [Theory] // ADR-0047 (the area pass of 2026-10-12: LARGE-011, SMALL-004): SMALL's k below 1 is #NUM! as LARGE's is, though truncated it would be 0, and past the count once truncated it is #NUM!
    [InlineData("0.5")]
    [InlineData("0.99")]
    [InlineData("-0.5")]
    [InlineData("11")]
    [InlineData("11.5")]
    public void ADR0047_small_outside_its_count_is_num(string k)
    {
        var sheet = Ten();

        Assert.Equal(ErrorValue.Num, sheet.Evaluate($"=SMALL(A1:A10,{k})").Error);
        Assert.Equal(ErrorValue.Num, sheet.Evaluate($"=AGGREGATE(15,6,A1:A10,{k})").Error);
    }

    [Theory] // ADR-0047: LARGE's k rounded up and SMALL's truncated are one rule, the k-th largest being the (n+1-k)-th smallest with that index truncated
    [InlineData("1.2")]
    [InlineData("1.5")]
    [InlineData("4.7")]
    [InlineData("9.5")]
    public void ADR0047_large_and_small_read_k_as_one_rule(string k)
    {
        var sheet = Ten();

        Assert.Equal(sheet.Evaluate($"=SMALL(A1:A10,11-{k})").Number, sheet.Evaluate($"=LARGE(A1:A10,{k})").Number);
    }

    [Theory] // ADR-0047 (MOD-008, and MOD-010 to 016 in the area pass of 2026-10-12): MOD answers a quotient of 2^27 and on to the limit a Microsoft 365 user reported, 1125900000000, from which it is #NUM!
    [InlineData("=MOD(2^27,1)", 0)]
    [InlineData("=MOD(2^27+1,2)", 1)]
    [InlineData("=MOD(2^40,3)", 1)]
    [InlineData("=MOD(-(2^40),3)", 2)]
    [InlineData("=MOD(1125899999999,1)", 0)]
    [InlineData("=MOD(1125899999999.5,1)", 0.5)]
    [InlineData("=MOD(3377699999997,3)", 0)]
    public void ADR0047_mod_answers_below_its_limit(string formula, double expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Number);
    }

    [Theory] // ADR-0047: from a quotient of 1125900000000, either sign, MOD is #NUM!
    [InlineData("=MOD(1125900000000,1)")]
    [InlineData("=MOD(-1125900000000,1)")]
    [InlineData("=MOD(1125900000000,-1)")]
    [InlineData("=MOD(3377700000000,3)")]
    [InlineData("=MOD(2^53,3)")]
    [InlineData("=MOD(1E+16,7)")]
    public void ADR0047_mod_is_num_from_its_limit(string formula)
    {
        Assert.Equal(ErrorValue.Num, NewSheet().Evaluate(formula).Error);
    }

    [Theory] // ADR-0047 (MOD-006, 007, 009 and, in the area pass of 2026-10-12, MOD-017): the doubles Excel gave, which the identity in doubles misses at MOD-017
    [InlineData("=MOD(123456789012,0.7)", 0.5000078322611261)]
    [InlineData("=MOD(0.3,0.1)", 0.09999999999999998)]
    [InlineData("=MOD(5.5,2)", 1.5)]
    [InlineData("=MOD(-7.5,2)", 0.5)]
    public void ADR0047_mod_gives_the_doubles_excel_gave(string formula, double expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Number);
    }

    [Theory] // ADR-0047, ADR-0202 (MOD-018, 019): where every evaluation that gives Excel's answers gives one double, MOD answers it, the exact remainder rounded once; the identity in doubles gave 0.09999999999999964 for MOD(10,0.3) and 0.19999999999999996 for MOD(-0.7,0.3)
    [InlineData("=MOD(-123456789012,0.7)", 0.19999216773887385)]
    [InlineData("=MOD(123456789012,-0.7)", -0.19999216773887385)]
    [InlineData("=MOD(-123456789012,-0.7)", -0.5000078322611261)]
    [InlineData("=MOD(10,0.3)", 0.10000000000000037)]
    [InlineData("=MOD(-10,0.3)", 0.19999999999999962)]
    [InlineData("=MOD(-0.7,0.3)", 0.2)]
    [InlineData("=MOD(45000.75,1)", 0.75)]
    public void ADR0047_mod_answers_where_the_evaluations_that_fit_agree(string formula, double expected)
    {
        Assert.Equal(expected, NewSheet().Evaluate(formula).Number);
    }

    [Theory] // ADR-0047, ADR-0202 (MOD-020 to 028): the double quotient rounds up onto a whole number the exact one is below; the exact remainder is about the divisor, a fused multiply-add a hair of the wrong sign, and that taken as 0 is 0, so MOD is refused until Excel is asked. The identity in doubles gave 0, -4.44E-16 or -1.11E-16 here, and fits no observed case
    [InlineData("=MOD(1,0.1)")]
    [InlineData("=MOD(0.5,0.1)")]
    [InlineData("=MOD(5,0.1)")]
    [InlineData("=MOD(-2.2,0.1)")]
    [InlineData("=MOD(-1,-0.1)")]
    [InlineData("=MOD(3.3,0.01)")]
    [InlineData("=MOD(0.7,0.01)")]
    [InlineData("=MOD(100000000000,0.1)")]
    [InlineData("=MOD(-693380,-93.7)")]
    public void ADR0047_mod_is_refused_where_the_double_quotient_rounds_up_onto_a_whole_number(string formula)
    {
        Assert.Equal(ErrorValue.Value, NewSheet().Evaluate(formula).Error);
    }

    [Theory] // ADR-0047, ADR-0202 (MOD-030, 031): a remainder that is not 0 but within 2^-49 of the number is 0 under Excel's cancellation of a final addition, and itself without it, so MOD is refused until Excel is asked
    [InlineData("=MOD(1.1,0.1)")]
    [InlineData("=MOD(-0.9,0.1)")]
    [InlineData("=MOD(-0.3,0.1)")]
    [InlineData("=MOD(2^30+2^-22,1)")]
    public void ADR0047_mod_is_refused_where_cancellation_would_make_the_remainder_0(string formula)
    {
        Assert.Equal(ErrorValue.Value, NewSheet().Evaluate(formula).Error);
    }

    [Theory] // ADR-0047, ADR-0202 (MOD-032): the remainder plus the divisor rounds to the divisor itself, which an evaluation may give or take as 0, so MOD is refused until Excel is asked
    [InlineData("=MOD(-1E-20,1)")]
    [InlineData("=MOD(1E-20,-1)")]
    public void ADR0047_mod_is_refused_where_the_remainder_rounds_to_the_divisor(string formula)
    {
        Assert.Equal(ErrorValue.Value, NewSheet().Evaluate(formula).Error);
    }

    [Theory] // ADR-0047, ADR-0202 (MOD-029): adding the divisor in an x87 register's 64 bits first would round twice, to the next double, so MOD is refused until Excel is asked
    [InlineData("=MOD(-0.087626,6630)")]
    public void ADR0047_mod_is_refused_where_an_x87_register_would_round_twice(string formula)
    {
        Assert.Equal(ErrorValue.Value, NewSheet().Evaluate(formula).Error);
    }

    [Theory] // ADR-0047: a remainder of 0 is 0, never -0, whatever the signs
    [InlineData("=MOD(-4,2)")]
    [InlineData("=MOD(4,-2)")]
    [InlineData("=MOD(-4,-2)")]
    [InlineData("=MOD(0,-3)")]
    public void ADR0047_mod_of_a_multiple_is_zero(string formula)
    {
        var value = NewSheet().Evaluate(formula);

        Assert.Equal(0, value.Number);
        Assert.False(double.IsNegative(value.Number));
    }
}
