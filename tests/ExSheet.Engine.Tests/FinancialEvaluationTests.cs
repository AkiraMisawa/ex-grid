using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 and ADR-0202: PMT, PV and FV give Excel's double, to the bit. The seventeenth Windows run
/// (verification/2026-10-11-windows-17, results-2026-10-11-run17-update-en-US.json) read every case's
/// double; <c>spikes/exsheet-financial-evaluation/</c> searched the evaluations that give them. PV and
/// FV are the doubles' evaluation of Microsoft's identity with 1+rate raised by squaring; PMT is the
/// payment at the end carried beyond a double and rounded once, and at the start that double divided
/// by 1+rate, rounded once.
/// </summary>
public class FinancialEvaluationTests
{
    private static double Number(string formula) =>
        NewSheet().Evaluate(formula) is { Kind: ValueKind.Number } v ? v.Number : throw new Xunit.Sdk.XunitException($"{formula} is {NewSheet().Evaluate(formula)}, not a number.");

    [Theory] // ADR-0047/ADR-0202: Excel's doubles as the seventeenth run read them; Math.Pow is 4 to 39 units in the last place away
    [InlineData("=FV(0.06/12,10,-200,-500,1)", 2581.4033740601185)]
    [InlineData("=FV(0.12/12,12,-1000)", 12682.503013196976)]
    [InlineData("=FV(0.11/12,35,-2000,,1)", 82846.24637190055)]
    [InlineData("=PV(0.08/12,12*20,500,,0)", -59777.14585118782)]
    [InlineData("=PV(0.05,10,-100,1000,1)", 196.8689140236461)]
    public void ADR0047_PV_and_FV_raise_one_plus_rate_by_squaring_in_doubles(string formula, double excel) =>
        Assert.Equal(excel, Number(formula));

    [Theory] // ADR-0047/ADR-0202: the doubles' evaluation is 36 to 225 units in the last place from these
    [InlineData("=PMT(8%/12,10,10000)", -1037.0320893591522)]
    [InlineData("=PMT(8%/12,10,10000,0,1)", -1030.1643271779658)]
    [InlineData("=PMT(6%/12,18*12,0,50000)", -129.08116086799092)]
    [InlineData("=PMT(0.1,10,1000,0,2)", -147.95035898410148)]
    public void ADR0047_PMT_is_carried_beyond_a_double_and_rounded_once(string formula, double excel) =>
        Assert.Equal(excel, Number(formula));

    [Fact] // ADR-0047: PMT-008, at the start, is the payment at the end over 1+rate unrounded; over the double 1.1 it is one unit lower
    public void ADR0047_PMT_at_the_start_divides_the_payment_at_the_end_by_one_plus_rate_unrounded()
    {
        var atEnd = Number("=PMT(0.1,10,1000)");
        var atStart = Number("=PMT(0.1,10,1000,0,1)");
        Assert.Equal(-162.74539488251162, atEnd);
        Assert.Equal(-147.95035898410148, atStart);
        Assert.Equal(-147.95035898410146, atEnd / (1 + 0.1));
    }

    [Theory] // ADR-0047: the evaluation found, at inputs no run has asked; spikes/exsheet-financial-evaluation/ computes each exactly
    [InlineData("=PMT(-2/12/100,360,125000,20000,0)", -326.9633871434769)]
    [InlineData("=FV(-2/12/100,360,125000,20000,0)", -33870696.20924745)]
    [InlineData("=PV(-2/12/100,360,125000,20000,0)", -61763793.24623326)]
    [InlineData("=PMT(8/12/100,480,-1000000,1000,1)", 6906.785212764484)]
    [InlineData("=FV(8/12/100,480,-1000000,1000,1)", 3514256943.5256577)]
    [InlineData("=PV(8/12/100,480,-1000000,1000,1)", 144779153.72637767)]
    [InlineData("=PMT(6/52/100,36,-250000,-1000,1)", 7112.692686159469)]
    [InlineData("=FV(6/52/100,36,-250000,-1000,1)", 9195769.500246907)]
    [InlineData("=PV(6/52/100,36,-250000,-1000,1)", 8821786.639617125)]
    [InlineData("=PMT(9/1200/100,1000,-250000,0,0)", 259.501547004884)]
    [InlineData("=FV(9/1200/100,1000,-250000,0,0)", 259603731.6379252)]
    [InlineData("=PV(9/1200/100,1000,-250000,0,0)", 240846348.39918622)]
    public void ADR0047_The_evaluation_found_gives_the_search_models_double(string formula, double model) =>
        Assert.Equal(model, Number(formula));

    [Theory] // ADR-0047: a type that is a whole number other than 0 is payment at the start, as Excel read 2 (PMT-008)
    [InlineData("PMT", "0.1,10,1000,0")]
    [InlineData("PV", "0.05,10,-100,1000")]
    [InlineData("FV", "0.06/12,10,-200,-500")]
    public void ADR0047_A_whole_number_type_other_than_0_is_payment_at_the_start(string function, string arguments)
    {
        var atStart = Number($"={function}({arguments},1)");
        Assert.Equal(atStart, Number($"={function}({arguments},2)"));
        Assert.Equal(atStart, Number($"={function}({arguments},-1)"));
        Assert.NotEqual(atStart, Number($"={function}({arguments},0)"));
    }

    [Theory] // ADR-0047/ADR-0202: refused, not guessed, until Excel is asked how it reads a fractional type
    [InlineData("=PMT(0.1,10,1000,0,0.5)")]
    [InlineData("=PV(0.05,10,-100,1000,1.5)")]
    [InlineData("=FV(0.06/12,10,-200,-500,-0.5)")]
    public void ADR0047_A_fractional_type_is_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047/ADR-0202: squaring raises to a whole number from 0; how Excel raises to any other has not been observed
    [InlineData("=PMT(8%/12,10.5,10000)")]
    [InlineData("=PMT(8%/12,-10,10000)")]
    [InlineData("=PV(0.05,10.5,-100)")]
    [InlineData("=PV(0.05,-1,-100)")]
    [InlineData("=FV(0.06/12,0.5,-200)")]
    [InlineData("=FV(0.06/12,-10,-200)")]
    [InlineData("=FV(0.06/12,2^53+2,-200)")]
    public void ADR0047_At_a_rate_other_than_0_a_number_of_periods_that_squaring_cannot_raise_to_is_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: at a rate of 0 no power is raised, so any number of periods is answered
    [InlineData("=FV(0,10.5,-100)", 1050)]
    [InlineData("=PV(0,-2,-100)", -200)]
    [InlineData("=PMT(0,2.5,1000)", -400)]
    public void ADR0047_At_a_rate_of_0_any_number_of_periods_is_answered(string formula, double expected) =>
        Assert.Equal(expected, Number(formula));

    [Theory] // ADR-0047: no periods and a rate leave PV and FV what they were; PMT has nothing to divide (PMT-005)
    [InlineData("=FV(0.1,0,-100,-500)", 500)]
    [InlineData("=PV(0.1,0,-100,1000)", -1000)]
    public void ADR0047_No_periods_leave_the_value_as_it_is(string formula, double expected) =>
        Assert.Equal(expected, Number(formula));

    [Fact] // ADR-0047: a growth past a double's range is #NUM!, never a number
    public void ADR0047_A_growth_past_a_doubles_range_is_NUM() =>
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate("=FV(1,2000,-1)"));
}
