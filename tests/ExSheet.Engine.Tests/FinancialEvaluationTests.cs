using System.Numerics;
using ExSheet.Engine.Formulas;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 and ADR-0202: PV, FV and XNPV give Excel's double, to the bit, at every case Excel was asked,
/// and PMT is kept by ADR-0047's exception for PMT alone (decided with the user, 2026-10-12). The seventeenth Windows run (verification/2026-10-11-windows-17) and the two area passes
/// of 2026-10-12 (verification/2026-10-12-windows-area and -area-2) read every case's double;
/// <c>spikes/exsheet-financial-evaluation/</c> searched the evaluations that give them. PV and FV are the
/// doubles' evaluation of Microsoft's identity, the payment times (1+rate*type)*((g-1)/rate), with 1+rate
/// raised by the one power searched that gives every observed double and Error Value: squaring to a whole
/// number, 1 over the power to a negative one, the square root at 0.5, e^(n ln(1+rate)) correctly rounded
/// otherwise, an odd root of a negative 1+rate, and the ^ operator's Error Values at 1+rate = 0. XNPV
/// divides each value by that power of its years and sums from the first, and is #NUM! at a rate below 0,
/// a check no evaluation gives. Where PMT answers, it is the payment at the end carried beyond a double and
/// rounded once, with 1+rate raised exactly, and at the start that double divided by 1+rate and rounded
/// again (the financial area's rule, which ticket 13 puts to the user); 19 of the 30 payments Excel was asked
/// are 1 to 11 units in the last place from the engine's. NPV is refused for 0.1.0
/// (ADR-0202, note of 2026-10-12): no evaluation searched gives every double Excel gave for it.
/// </summary>
public class FinancialEvaluationTests
{
    private static double Number(string formula) =>
        NewSheet().Evaluate(formula) is { Kind: ValueKind.Number } v ? v.Number : throw new Xunit.Sdk.XunitException($"{formula} is {NewSheet().Evaluate(formula)}, not a number.");

    [Theory] // ADR-0047/ADR-0202: Excel's doubles as the seventeenth run and the area pass read them; Math.Pow is 4 to 39 units in the last place away
    [InlineData("=FV(0.06/12,10,-200,-500,1)", 2581.4033740601185)]
    [InlineData("=FV(0.12/12,12,-1000)", 12682.503013196976)]
    [InlineData("=FV(0.11/12,35,-2000,,1)", 82846.24637190055)]
    [InlineData("=FV(0.05/12,360,-150)", 124838.79530422161)]
    [InlineData("=PV(0.08/12,12*20,500,,0)", -59777.14585118782)]
    [InlineData("=PV(0.05,10,-100,1000,1)", 196.8689140236461)]
    [InlineData("=PV(0.05/12,37,2500,1000,0)", -86415.16626124813)]
    public void ADR0047_PV_and_FV_raise_one_plus_rate_by_squaring_in_doubles(string formula, double excel) =>
        Assert.Equal(excel, Number(formula));

    [Theory] // ADR-0047: FV-009 and PV-007, the area pass; multiplying the payment by 1+rate first is one unit in the last place below each
    [InlineData("FV", 1849.5026084246633, 1849.502608424663)]
    [InlineData("PV", 1759.484064397425, 1759.4840643974248)]
    public void ADR0047_PV_and_FV_multiply_the_payment_by_the_product_of_one_plus_rate_and_the_annuity_factor(string function, double excel, double paymentTimesDueFirst)
    {
        var rate = 0.05 / 12;
        var growth = RaisedBySquaring(1 + rate, 12);
        var annuity = -150 * (1 + rate) * ((growth - 1) / rate); // the payment times 1+rate first, then the annuity factor
        var dueFirst = function == "FV" ? -annuity : -annuity / growth;
        Assert.Equal(excel, Number($"={function}(0.05/12,12,-150,0,1)"));
        Assert.Equal(paymentTimesDueFirst, dueFirst);
        Assert.NotEqual(excel, dueFirst);
    }

    [Theory] // ADR-0047: FV-006, FV-007 and PV-005, the area pass: Excel answers an nper that is not a whole number from 0, and these are its doubles
    [InlineData("=FV(0.06/12,10.5,-200)", 2150.588229777517)]
    [InlineData("=FV(0.06/12,-10,-200)", -1946.0823721571962)]
    [InlineData("=PV(0.05,10.5,-100)", 801.7640221710038)]
    public void ADR0047_PV_and_FV_give_Excels_double_at_the_fractional_and_negative_numbers_of_periods_it_was_asked(string formula, double excel) =>
        Assert.Equal(excel, Number(formula));

    [Fact] // ADR-0047: FV-007, a negative nper is 1 over 1+rate raised by squaring; squaring 1/(1+rate) is 19 units in the last place away
    public void ADR0047_A_negative_number_of_periods_is_one_over_the_power_by_squaring()
    {
        var rate = 0.06 / 12;
        var square = (1 + rate) * (1 + rate);
        var growth = 1 / (square * (square * square * (square * square))); // 10 = 2 + 8, from the lowest bit
        var reciprocal = 1 / (1 + rate);
        var reciprocalSquare = reciprocal * reciprocal;
        var reciprocalGrowth = reciprocalSquare * (reciprocalSquare * reciprocalSquare * (reciprocalSquare * reciprocalSquare));
        Assert.Equal(-1946.0823721571962, Number("=FV(0.06/12,-10,-200)"));
        Assert.Equal(-1946.0823721571962, 200 * ((growth - 1) / rate));
        Assert.Equal(-1946.0823721572006, 200 * ((reciprocalGrowth - 1) / rate));
    }

    [Fact] // ADR-0047: PV-005, a fractional nper is e^(nper ln(1+rate)); a correctly rounded power is two units in the last place from Excel's double
    public void ADR0047_A_fractional_number_of_periods_is_e_to_nper_ln_one_plus_rate()
    {
        var growth = NearestSquareRootOfPower(1.05, 21); // 1.05^10.5, correctly rounded
        var correctlyRounded = -(-100 * (1 * ((growth - 1) / 0.05))) / growth;
        Assert.Equal(801.7640221710038, Number("=PV(0.05,10.5,-100)"));
        Assert.Equal(801.7640221710036, correctlyRounded);
        Assert.NotEqual(801.7640221710038, correctlyRounded);
    }

    [Theory] // ADR-0047: FV-012, FV-013 and PV-010, the second area pass; a fraction is e^(n ln(1+rate)) with ln and exp correctly rounded, a negative one 1 over the power to its absolute value
    [InlineData("0.06/12", 10.25, 2098.0640302830534)]
    [InlineData("0.06/12", -10.5, -2040.8618907559849)]
    public void ADR0047_A_fraction_of_a_period_is_e_to_n_ln_one_plus_rate_correctly_rounded(string rate, double periods, double excel)
    {
        var r = 0.06 / 12;
        var power = CorrectlyRounded.Exp(Math.Abs(periods) * CorrectlyRounded.Log(1 + r));
        var growth = periods < 0 ? 1 / power : power;
        Assert.Equal(excel, -(-200 * (1 * ((growth - 1) / r))));
        Assert.Equal(excel, Number($"=FV({rate},{periods.ToString(System.Globalization.CultureInfo.InvariantCulture)},-200)"));
        Assert.Equal(48.19985410293382, Number("=PV(0.05,0.5,-100)"));
    }

    [Fact] // ADR-0047: FV-016, the second area pass: half a period is the square root of 1+rate, as the ^ operator takes it (ARITH-133), where e^(0.5 ln(1+rate)) is 8 units in the last place away
    public void ADR0047_Half_a_period_is_the_square_root_of_one_plus_rate()
    {
        var byLogarithm = -(-100 * (1 * ((CorrectlyRounded.Exp(0.5 * CorrectlyRounded.Log(1.42)) - 1) / 0.42)));
        Assert.Equal(45.627983043166275, Number("=FV(0.42,0.5,-100)"));
        Assert.Equal(45.627983043166275, -(-100 * (1 * ((Math.Sqrt(1.42) - 1) / 0.42))));
        Assert.Equal(45.62798304316633, byLogarithm);
        Assert.Equal(38.290152786502574, Number("=PV(0.42,0.5,-100)"));      // PV-014 asks: PV raises as FV does
        Assert.Equal(-38.29015278650258, Number("=FV(0.42,-0.5,-100)"));     // FV-024 asks: 1 over the square root
    }

    [Fact] // ADR-0047: FV-022, the second area pass: a negative fraction is 1 over the power to its absolute value, as FV-007's -10 is, where raising to -2.5 directly is 78 units in the last place away
    public void ADR0047_A_negative_fraction_is_one_over_the_power_to_its_absolute_value()
    {
        var rate = 0.06 / 12;
        var logarithm = CorrectlyRounded.Log(1 + rate);
        Assert.Equal(-247.8287941856272, Number("=FV(0.06/12,-2.5,-100)"));
        Assert.Equal(-247.8287941856272, 100 * (((1 / CorrectlyRounded.Exp(2.5 * logarithm)) - 1) / rate));
        Assert.Equal(-247.828794185625, 100 * ((CorrectlyRounded.Exp(-2.5 * logarithm) - 1) / rate));
        Assert.Equal(-250.93828076244577, Number("=PV(0.06/12,-2.5,-100)"));  // PV-015 asks
    }

    [Theory] // ADR-0047: FV-014, FV-015 and FV-019, the second area pass: a present value and a payment are the sum as written, where fusing either product into a multiply-add is one unit in the last place away
    [InlineData("=FV(0.05/12,12,-150,-1000,1)", 0.05 / 12, 12, -150, -1000, 2900.664506306397)]
    [InlineData("=FV(0.03/12,240,-100,-50000,1)", 0.03 / 12, 240, -100, -50000, 123950.02507801351)]
    [InlineData("=FV(0.06/12,10,-150,1000)", 0.06 / 12, 10, -150, 1000, 483.06382918288546)]
    public void ADR0047_FV_is_the_sum_as_written_not_a_fused_one(string formula, double rate, int periods, double payment, double present, double excel)
    {
        var growth = RaisedBySquaring(1 + rate, periods);
        var annuity = (1 + (formula.EndsWith(",1)") ? rate : 0)) * ((growth - 1) / rate);
        Assert.Equal(excel, -((present * growth) + (payment * annuity)));
        Assert.Contains(new[] { -Math.FusedMultiplyAdd(present, growth, payment * annuity), -Math.FusedMultiplyAdd(payment, annuity, present * growth) }, fused => fused != excel);
        Assert.Equal(excel, Number(formula));
    }

    [Theory] // ADR-0047: FV-020, FV-021 and PV-012, the second area pass: 0 to a negative power is #DIV/0!, as the ^ operator's =0^-1 is (ARITH-042)
    [InlineData("=FV(-1,-0.5,-100)", "#DIV/0!")]
    [InlineData("=FV(-1,-10,-100)", "#DIV/0!")]
    [InlineData("=PV(-1,-10,-100)", "#DIV/0!")]
    [InlineData("=PV(-1,-0.5,-100,1000,1)", "#DIV/0!")]
    public void ADR0047_A_negative_number_of_periods_at_a_rate_of_minus_1_is_DIV0(string formula, string error) =>
        Assert.Equal(error, NewSheet().Evaluate(formula).ToString());

    [Fact] // ADR-0047: FV-018, the second area pass: no periods at a rate of -1 is 0^0, #NUM!, as the ^ operator's =0^0 is (ARITH-041), where squaring gave 1 and FV 500
    public void ADR0047_No_periods_at_a_rate_of_minus_1_is_NUM()
    {
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate("=FV(-1,0,-100,-500)"));
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate("=PV(-1,0,-100,-500)"));
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate("=(1+-1)^0"));
    }

    [Fact] // ADR-0047: PV-013 asks: at a rate of -1 the growth is 0 and PV divides by it; which Error Value Excel gives there has not been observed
    public void ADR0047_PV_at_a_rate_of_minus_1_with_periods_is_refused()
    {
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate("=PV(-1,10,-100)"));
        Assert.Equal(100, Number("=FV(-1,10,-100)")); // FV multiplies by the growth, 0, and answers
    }

    [Theory] // ADR-0047: FV-017 and PV-011, the second area pass: a negative 1+rate has an odd root where 1/nper is odd, as the ^ operator's =(-8)^(1/3) (ARITH-043), and no other fractional power (#NUM!); PMT is #NUM! there too, where none of its readings has a power or a logarithm
    [InlineData("=FV(-9,1/3,-1)", "0.3333333333333333")]
    [InlineData("=PV(-1.5,0.5,-100)", "#NUM!")]
    [InlineData("=FV(-2,10.5,-100)", "#NUM!")]
    [InlineData("=PMT(-2,10.5,100)", "#NUM!")]
    [InlineData("=PMT(-9,0.25,100,0,1)", "#NUM!")]
    public void ADR0047_At_a_rate_below_minus_1_a_fractional_number_of_periods_is_an_odd_root_or_NUM(string formula, string expected) =>
        Assert.Equal(expected, NewSheet().Evaluate(formula).ToString());

    [Fact] // ADR-0047: a whole nper at a rate below -1 is still raised by squaring, which does not depend on the base's sign
    public void ADR0047_At_a_rate_below_minus_1_a_whole_number_of_periods_is_answered() =>
        Assert.Equal(1, Number("=FV(-2,3,-1)"));

    [Theory] // ADR-0047/ADR-0202: PMT-001 and PMT-003, at the end, are the exact payment rounded once; the doubles' evaluation is 36 to 225 units in the last place from them
    [InlineData("=PMT(8%/12,10,10000)", -1037.0320893591522)]
    [InlineData("=PMT(6%/12,18*12,0,50000)", -129.08116086799092)]
    public void ADR0047_PMT_at_the_end_is_carried_beyond_a_double_and_rounded_once(string formula, double excel) =>
        Assert.Equal(excel, Number(formula));

    [Theory] // ADR-0047: PMT-002 and PMT-008, at the start, are the payment at the end's double divided by 1+rate exactly and rounded again, the financial area's rule at the start (ticket 13 puts it to the user)
    [InlineData("=PMT(8%/12,10,10000,0,1)", -1030.1643271779658)]
    [InlineData("=PMT(0.1,10,1000,0,2)", -147.95035898410148)]
    public void ADR0047_PMT_at_the_start_is_the_payment_at_the_end_over_one_plus_rate_rounded_again(string formula, double excel) =>
        Assert.Equal(excel, Number(formula));

    [Fact] // ADR-0047: PMT-008, at the start, is the payment at the end over 1+rate unrounded; over the double 1.1 it is one unit lower, and so is the exact payment at the start rounded once (Python's decimal), which the engine's rule at the start does not give
    public void ADR0047_PMT_at_the_start_divides_the_payment_at_the_end_by_one_plus_rate_unrounded()
    {
        var atEnd = Number("=PMT(0.1,10,1000)");
        var atStart = Number("=PMT(0.1,10,1000,0,1)");
        Assert.Equal(-162.74539488251162, atEnd);
        Assert.Equal(-147.95035898410148, atStart);
        Assert.Equal(-147.95035898410146, atEnd / (1 + 0.1));
        const double exactPaymentAtTheStartRoundedOnce = -147.95035898410146;
        Assert.NotEqual(exactPaymentAtTheStartRoundedOnce, atStart);
    }

    [Theory] // ADR-0047: PMT-014, 015, 025 and 029, the second area pass: the engine's rule at the start lies a unit in the last place from the exact payment at the start rounded once (Python's decimal), and Excel's double on the other side of it; ticket 13 puts the rule at the start to the user
    [InlineData("=PMT(0.0725/12,12,250000,0,1)", -21530.430018725492, -21530.43001872549, -21530.430018725485)]
    [InlineData("=PMT(0.05/12,12,10000,0,1)", -852.5226402170998, -852.5226402170999, -852.5226402171)]
    [InlineData("=PMT(0.065/12,60,1000,-5000,1)", 50.90552130119935, 50.90552130119934, 50.90552130119933)]
    [InlineData("=PMT(0.06/52,60,1000,-5000,1)", 63.197272167756275, 63.19727216775628, 63.197272167756296)]
    public void ADR0047_PMT_at_the_start_is_not_everywhere_the_exact_payment_rounded_once(string formula, double engine, double exactOnce, double excel)
    {
        Assert.Equal(engine, Number(formula));
        Assert.Equal(1, Math.Abs(BitConverter.DoubleToInt64Bits(engine) - BitConverter.DoubleToInt64Bits(exactOnce)));
        Assert.True(Math.Abs(excel - exactOnce) < Math.Abs(excel - engine));
    }

    [Theory] // ADR-0047: the evaluation found, at inputs no run has asked; spikes/exsheet-financial-evaluation/ computes each exactly
    [InlineData("=PMT(-2/12/100,360,125000,20000,0)", -326.9633871434769)]
    [InlineData("=FV(-2/12/100,360,125000,20000,0)", -33870696.20924745)]
    [InlineData("=PV(-2/12/100,360,125000,20000,0)", -61763793.24623326)]
    [InlineData("=PMT(8/12/100,480,-1000000,1000,1)", 6906.785212764484)]
    [InlineData("=PV(8/12/100,480,-1000000,1000,1)", 144779153.72637767)]
    [InlineData("=PMT(6/52/100,36,-250000,-1000,1)", 7112.692686159469)]
    [InlineData("=FV(6/52/100,36,-250000,-1000,1)", 9195769.500246907)]
    [InlineData("=PV(6/52/100,36,-250000,-1000,1)", 8821786.639617125)]
    [InlineData("=PMT(9/1200/100,1000,-250000,0,0)", 259.501547004884)]
    [InlineData("=FV(9/1200/100,1000,-250000,0,0)", 259603731.6379252)]
    [InlineData("=PV(9/1200/100,1000,-250000,0,0)", 240846348.39918622)]
    public void ADR0047_The_evaluation_found_gives_the_search_models_double(string formula, double model) =>
        Assert.Equal(model, Number(formula));

    [Theory] // ADR-0047: type is a truth value: 2 (PMT-008), 0.5 (PMT-009) and -1 (PMT-010) are each payment at the start, as Excel read them
    [InlineData("PMT", "0.1,10,1000,0")]
    [InlineData("PV", "0.05,10,-100,1000")]
    [InlineData("FV", "0.06/12,10,-200,-500")]
    public void ADR0047_Any_type_other_than_0_is_payment_at_the_start(string function, string arguments)
    {
        var atStart = Number($"={function}({arguments},1)");
        Assert.Equal(atStart, Number($"={function}({arguments},2)"));
        Assert.Equal(atStart, Number($"={function}({arguments},-1)"));
        Assert.Equal(atStart, Number($"={function}({arguments},0.5)"));
        Assert.Equal(atStart, Number($"={function}({arguments},-0.5)"));
        Assert.Equal(atStart, Number($"={function}({arguments},0.1)"));
        Assert.NotEqual(atStart, Number($"={function}({arguments},0)"));
    }

    [Fact] // ADR-0047: PMT-009, the area pass: a type of 0.5 is not truncated to 0, which would give the payment at the end
    public void ADR0047_A_fractional_type_is_not_truncated()
    {
        Assert.Equal(-147.95035898410148, Number("=PMT(0.1,10,1000,0,0.5)"));
        Assert.Equal(-162.74539488251162, Number("=PMT(0.1,10,1000,0,0)"));
    }

    [Theory] // ADR-0047, the exception for PMT (decided with the user, 2026-10-12): PMT-011 and PMT-012, a fractional and a negative nper, are the exact payment rounded once, each one unit in the last place from Excel's double
    [InlineData("=PMT(8%/12,10.5,10000)", -989.2729401451878, -989.2729401451879)]
    [InlineData("=PMT(8%/12,-10,10000)", 970.3654226924855, 970.3654226924854)]
    public void ADR0047_PMT_gives_the_exact_payment_at_a_fractional_or_negative_number_of_periods(string formula, double exact, double excel)
    {
        Assert.Equal(exact, Number(formula));
        Assert.Equal(1, Math.Abs(BitConverter.DoubleToInt64Bits(exact) - BitConverter.DoubleToInt64Bits(excel)));
    }

    [Theory] // ADR-0047, the exception for PMT: the engine's payment, computed with Python's decimal (the exact payment rounded once at the end, and at the start that double over 1+rate rounded again), at inputs no run has asked; spikes/exsheet-financial-evaluation/pmt_exact.py checks 20,000 such inputs
    [InlineData("=PMT(0.1,0.5,1000)", -2148.8088481701516)]
    [InlineData("=PMT(0.1,-1,1000,0,1)", 909.0909090909091)]
    [InlineData("=PMT(-0.3,-0.5,0,20000,0)", 30733.20053068151)]
    [InlineData("=PMT(0.42,-1/3,1000,-5000,0)", -15649.385313401854)]
    [InlineData("=PMT(-0.3,0.25,1000,20000,0)", -73549.37333577585)]
    [InlineData("=PMT(1E-7,-240.75,10000,0,1)", 41.53636189198373)]
    [InlineData("=PMT(1E-7,0.25,10000,0,1)", -39999.99850000012)]
    [InlineData("=PMT(0.42,-1/3,250000,20000,1)", 649988.3863764966)]
    [InlineData("=PMT(-0.3,-1/3,0,20000,1)", 67893.64337294643)]
    [InlineData("=PMT(0.05,12,0,0)", 0)]
    public void ADR0047_PMT_raises_one_plus_rate_exactly_to_fractional_and_negative_numbers_of_periods(string formula, double exact) =>
        Assert.Equal(exact, Number(formula));

    [Theory] // ADR-0047, the exception for PMT: below a rate of about 1e-15, 1+rate in double-double held (1+rate)^nper - 1 to about 53 bits, and the previous round's payment was 1 or 2 units in the last place from the exact one at 874 of 3,167 such inputs of pmt_exact.py's; the payment is now held between bounds that decide its double. Python's decimal at 1200 digits, rounded once
    [InlineData("=PMT(1E-20,12,1000)", -83.33333333333333)]
    [InlineData("=PMT(1.35964379977615E-261,109.10388740984,-3.72021037548094,0,0)", 0.034097871888893086)]
    [InlineData("=PMT(-1.66782567493401E-214,450.154168942057,13857.7460935048,-0.000477145499331959,0)", -30.784443580579264)]
    [InlineData("=PMT(1.47177375994885E-17,410,235.577961475681,16010356.1114486,1)", -39050.223632707384)]
    [InlineData("=PMT(-1.85280934809099E-24,-145,-55549223.7358894,1.89338060457055E+68,0)", 1.3057797272900346E+66)]
    [InlineData("=PMT(5.7413834832409E-21,10.5,-249.243481190686,17175.8914195337,0)", -1612.0617084136202)]
    [InlineData("=PMT(-1.12980118685588E-18,0.5,14939049.7964629,-2167881.96100433,1)", -25542335.67091714)]
    [InlineData("=PMT(0.0001,1.12427561208993E-12,0,2146708.76746529,0)", -1.9095105132842696E+18)]
    public void ADR0047_PMT_is_the_exact_payment_at_rates_where_one_plus_rate_holds_none_of_the_rate(string formula, double exact) =>
        Assert.Equal(exact, Number(formula));

    [Theory] // ADR-0047: PMT-034 to 037 ask: at a rate of -1 the forms of the identity part between -fv and an Error Value, and with negative periods a power gives #DIV/0! and ln(0) #NUM!; below -1 a power gives a number, or a growth of exactly 1 every form divides by, and ln(1+rate) none
    [InlineData("=PMT(-1,10,1000,500)")]
    [InlineData("=PMT(-1,10.5,1000,500,1)")]
    [InlineData("=PMT(-1,-10,100)")]
    [InlineData("=PMT(-1,-0.5,100,0,1)")]
    [InlineData("=PMT(-2,2,1000)")]
    [InlineData("=PMT(-2,10,1000,0,1)")]
    [InlineData("=PMT(-9,1/3,-1)")]
    [InlineData("=PMT(-1.5,10,1000)")]
    [InlineData("=PMT(-9,360,1000,500)")]
    public void ADR0047_PMT_refuses_at_a_rate_of_minus_1_or_below_where_its_readings_part(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: PMT-038 to 044 ask: where a quantity a reading of PMT computes in doubles passes a double's range or falls below its normal numbers, some readings give an Error Value or a number of few digits and others the payment
    [InlineData("=PMT(1,1100,1000)")] // the growth past the range
    [InlineData("=PMT(3,1000.5,1000)")]
    [InlineData("=PMT(1,1020,1000)")] // pv times the growth past it, the growth within
    [InlineData("=PMT(0.5,1740,1000)")]
    [InlineData("=PMT(1E10,30,1)")] // the rate times pv times the growth
    [InlineData("=PMT(-0.99,160,1000,500)")] // the growth below the normal numbers, so that 1 over it passes the range
    [InlineData("=PMT(1E-320,0.5,1000)")] // a rate below the normal numbers
    [InlineData("=PMT(1E-315,10.5,1000)")]
    [InlineData("=PMT(5.1699029180828E-320,0.0907921350606893,-1000,20000)")]
    [InlineData("=PMT(-1.64435447903749E-307,283.5,1E-05,0)")] // pv times the rate below them
    [InlineData("=PMT(0.05,12,1E-310)")] // pv below them
    [InlineData("=PMT(0.05,12,9E307)")] // pv times the growth past the range
    public void ADR0047_PMT_refuses_where_a_reading_passes_a_doubles_range(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: PMT-045 and PMT-046 ask: a reading that takes the growth as 1 + (g-1) holds one below 2^-54 as 0 and divides by it; LibreOffice's reading of no periods at the start divides by e^(ln(1+rate)) - 1 - rate, 0 or a rounding's remainder
    [InlineData("=PMT(-0.5,360,1000,500)")]
    [InlineData("=PMT(0.1,-400,1000)")]
    [InlineData("=PMT(0.42,0,1000,0,1)")]
    [InlineData("=PMT(0.1,0,1000,0,1)")]
    public void ADR0047_PMT_refuses_a_growth_below_2_to_the_minus_52_and_no_periods_at_the_start(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: no periods at the end is #NUM! (PMT-005), and at the start where every reading gives an Error Value: a rate of 0, or of -1 or below
    [InlineData("=PMT(0.1,0,1000)")]
    [InlineData("=PMT(0,0,1000,0,1)")]
    [InlineData("=PMT(-1,0,1000,0,1)")]
    [InlineData("=PMT(-2,0,1000,0,1)")]
    public void ADR0047_PMT_with_no_periods_is_NUM_where_every_reading_gives_it(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: within the range the readings agree in kind and PMT answers, at the range's edges too; Python's decimal at 1200 digits, rounded once
    [InlineData("=PMT(1,1000,1000)", -1000)]
    [InlineData("=PMT(1E-300,12,1000)", -83.33333333333333)]
    [InlineData("=PMT(-0.5,52,1000,500,1)", -500.00000000000034)] // a growth of 2^-52, the least answered
    public void ADR0047_PMT_answers_within_the_range(string formula, double exact) =>
        Assert.Equal(exact, Number(formula));

    [Theory] // ADR-0047: PMT-047 to PMT-053 ask: where a reading magnifies its own roundings more than 2^6 times, the readings give 0, numbers of the other sign or of a few digits, or an Error Value, and PMT refuses (ExactPayment.Conditioned, the financial area's bound)
    [InlineData("=PMT(0.1,2,1000,-1210)")] // pv g and fv cancel: readings give 0, 1.7E-14 and -5.8E-15
    [InlineData("=PMT(0.1,1E-17,1000,-1000)")] // the same at a tiny nper: readings give 0, -100 and -104.8
    [InlineData("=PMT(1,1,1000,-2000)")] // pv g + fv is exactly 0
    [InlineData("=PMT(-0.5,36.25,-150,0,0)")] // a growth of 1.2E-11 taken as 1 + (g-1)
    [InlineData("=PMT(-0.5,52,1000)")] // a growth of 2^-52 taken so
    [InlineData("=PMT(1,1000,0,1000)")] // the exponent, 693, rounded: hundreds of units apart
    [InlineData("=PMT(0.1,1E-17,1000,0,1)")] // at the start, LibreOffice's divisor is 0, #NUM!
    [InlineData("=PMT(0.1,-5E-17,1000,0,1)")]
    [InlineData("=PMT(-0.999999,1E-12,1000,0,1)")]
    [InlineData("=PMT(0.1,1E-17,0,0,1)")] // no pv and no fv: 0 from the others, 0/0 from LibreOffice's
    [InlineData("=PMT(1000,2,1000,0,1)")] // at the start, p - p r/(1+r) cancels
    public void ADR0047_PMT_refuses_where_a_reading_magnifies_its_roundings(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: within the bound PMT answers, the observed payments included (PMT-020's largest factor, 32.4, is the largest among them); the readings lie within 64 units in the last place of these (Search.cs --pmt-readings)
    [InlineData("=PMT(0.15,12,1000,-5000,1)", -10.501648240558218)]
    [InlineData("=PMT(30,3,1000,0,1)", -967.7744209466264)] // |1+r| + |r| is 61
    [InlineData("=PMT(0.1,1E-17,1000,0,0)", -1.0492058687257069E+20)] // a tiny nper at the end, pv only
    [InlineData("=PMT(0.05,12,0,0,1)", 0)]
    public void ADR0047_PMT_answers_within_the_bound(string formula, double expected) =>
        Assert.Equal(expected, Number(formula));

    [Fact] // ADR-0047: no periods is #NUM! (PMT-005) at a rate of -1 too, where the power's 0^0 is #NUM! as well
    public void ADR0047_PMT_with_no_periods_is_NUM_at_a_rate_of_minus_1() =>
        Assert.Equal(Value.FromError(ErrorValue.Num), NewSheet().Evaluate("=PMT(-1,0,1000)"));

    [Theory] // ADR-0047: past 2^53 every double is a whole number, and how Excel raises 1+rate to one has not been observed; PMT refuses it as PV and FV do
    [InlineData("=FV(0.06/12,2^53+2,-200)")]
    [InlineData("=PV(0.06/12,-(2^53+2),-200)")]
    [InlineData("=PMT(0.06/12,2^53+2,1000)")]
    public void ADR0047_At_a_rate_other_than_0_a_number_of_periods_past_2_to_the_53_is_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Fact] // ADR-0047: XNPV-004, Excel's double, is what e^(x ln(1+rate)) summed from the first value gives, where Math.Pow summed so is 8 units in the last place away
    public void ADR0047_XNPV_gives_XNPV_004_where_Math_Pow_was_8_units_in_the_last_place_away()
    {
        var sheet = NewSheet();
        string[] values = ["-10000", "2750", "2750", "2750", "4250"];
        string[] dates = ["39448", "39522", "40089", "40217", "40327"];
        for (var i = 0; i < values.Length; i++)
        {
            sheet.Enter($"A{i + 1}", values[i]);
            sheet.Enter($"B{i + 1}", dates[i]);
        }
        Assert.Equal(Value.FromNumber(342.0430874765725), sheet.Evaluate("=XNPV(0.12,A1:A5,B1:B5)"));
        var byPow = 0.0;
        for (var i = 0; i < values.Length; i++) byPow += double.Parse(values[i]) / Math.Pow(1.12, (double.Parse(dates[i]) - 39448) / 365);
        Assert.Equal(342.04308747657205, byPow);
    }

    [Theory] // ADR-0047: XNPV-005, 006, 011 and 013, the second area pass: whole years by squaring, the days over 365, and e^(x ln(1+rate)) with ln and exp correctly rounded otherwise
    [InlineData("0.07", new[] { "-10000", "3000", "4200", "6800" }, new[] { "39448", "39813", "40178", "40543" }, 2023.0065393622904)]
    [InlineData("0.06", new[] { "-10000", "3000", "4200", "6800" }, new[] { "39448", "39813", "40178", "40543" }, 2277.5848519247406)]
    [InlineData("0.05", new[] { "-10000", "2750" }, new[] { "39448", "39549" }, -7286.877809009438)]
    [InlineData("0.1", new[] { "1000", "4200", "-10000", "3000", "-1000", "3250" }, new[] { "39448", "41204", "41007", "40245", "40272", "41174" }, 700.2269770028577)]
    public void ADR0047_XNPV_gives_Excels_double(string rate, string[] values, string[] dates, double excel)
    {
        var sheet = NewSheet();
        for (var i = 0; i < values.Length; i++)
        {
            sheet.Enter($"A{i + 1}", values[i]);
            sheet.Enter($"B{i + 1}", dates[i]);
        }
        Assert.Equal(Value.FromNumber(excel), sheet.Evaluate($"=XNPV({rate},A1:A{values.Length},B1:B{values.Length})"));
    }

    [Fact] // ADR-0047: XNPV-013, at 797 days a correctly rounded exponential gives Excel's discount, where glibc's exp is one unit in the last place below it
    public void ADR0047_XNPV_013s_discount_is_the_correctly_rounded_exponential()
    {
        var exponent = 797.0 / 365 * CorrectlyRounded.Log(1.1);
        Assert.Equal(1.2313555711418425, CorrectlyRounded.Exp(exponent));
        Assert.Equal(CorrectlyRounded.ExpExact(exponent), CorrectlyRounded.Exp(exponent));
    }

    [Fact] // ADR-0047: whole years where squaring gives what 1+rate is, and days that 365 divides, are answered
    public void ADR0047_XNPV_answers_whole_years_by_squaring()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "-10000");
        sheet.Enter("A2", "3000");
        sheet.Enter("B1", "39448");
        sheet.Enter("B2", "39813");
        Assert.Equal(-10000 + (3000 / 1.07), sheet.Evaluate("=XNPV(0.07,A1:A2,B1:B2)").Number);
    }

    [Theory] // ADR-0047: XNPV-007 to 010, 012 and 017, the second area pass: Excel gave #NUM! at every rate below 0 it was asked, -0.5 to -2, which no evaluation gives; at -0.01 it is the engine's answer, which XNPV-020 asks
    [InlineData("-1", "39448", "39508")]
    [InlineData("-1", "39448", "39448")]
    [InlineData("-1.5", "39448", "39813")]
    [InlineData("-2", "39448", "39508")]
    [InlineData("-0.5", "39448", "39508")]
    [InlineData("-0.01", "39448", "39508")]
    public void ADR0047_XNPV_at_a_rate_below_0_is_NUM(string rate, string first, string second)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "-1000");
        sheet.Enter("A2", "300");
        sheet.Enter("B1", first);
        sheet.Enter("B2", second);
        Assert.Equal(Value.FromError(ErrorValue.Num), sheet.Evaluate($"=XNPV({rate},A1:A2,B1:B2)"));
    }

    [Fact] // ADR-0047: XNPV-019 asks: at a rate of 0 "below 0" answers the sum and "0 or below" #NUM!, so XNPV refuses it, but where a date before the first or the counts give #NUM!, which both readings give
    public void ADR0047_XNPV_at_a_rate_of_0_is_refused_only_where_the_readings_part()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "-1000");
        sheet.Enter("A2", "300");
        sheet.Enter("A3", "500");
        sheet.Enter("B1", "39448");
        sheet.Enter("B2", "39508");
        sheet.Enter("B3", "39000");
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(0,A1:A2,B1:B2)"));
        Assert.Equal(Value.FromError(ErrorValue.Num), sheet.Evaluate("=XNPV(0,A1:A3,B1:B3)"));
        Assert.Equal(Value.FromError(ErrorValue.Num), sheet.Evaluate("=XNPV(0,A1:A3,B1:B2)"));
    }

    [Theory] // ADR-0047: XNPV-021 to 026 ask: the order of XNPV's checks has not been observed, so where a failing cell of its ranges and another failing check give different answers, the answer would be the order's, and XNPV refuses
    [InlineData("-0.5", "#N/A", "39448", "#VALUE!")] // the rate first gives #NUM!, the values first #N/A
    [InlineData("-0.5", "text", "39448", "#VALUE!")]
    [InlineData("0", "#N/A", "39448", "#VALUE!")] // a rate of 0, "0 or below" and the rate first, #NUM!
    [InlineData("0.09", "#N/A", "40000", "#VALUE!")] // a date before the first, #NUM!
    [InlineData("0.09", "#N/A", "#DIV/0!", "#VALUE!")] // the values first give #N/A, the dates first #DIV/0!
    [InlineData("0.09", "#N/A", "#N/A", "#N/A")] // every check that fails gives #N/A
    [InlineData("0.09", "#N/A", "39448", "#N/A")] // nothing else fails
    [InlineData("-0.5", "#NUM!", "39448", "#NUM!")] // every check that fails gives #NUM!
    [InlineData("-0.5", "-10000", "40000", "#NUM!")]
    public void ADR0047_XNPV_refuses_where_the_order_of_its_checks_would_decide(string rate, string firstValue, string firstDate, string expected)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", firstValue);
        sheet.Enter("A2", "2750");
        sheet.Enter("B1", firstDate);
        sheet.Enter("B2", "39508");
        Assert.Equal(expected, sheet.Evaluate($"=XNPV({rate},A1:A2,B1:B2)").ToString());
    }

    [Fact] // ADR-0047: XNPV-027 to 030 ask: XNPV reads the rate and every cell of both ranges, so that neither a rate that is an Error Value, nor where a failing cell lies, nor which of two Error Values a range holds first, decides the answer
    public void ADR0047_XNPV_reads_the_rate_and_every_cell_before_it_answers()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "#N/A");
        sheet.Enter("A2", "2750");
        sheet.Enter("A3", "4250");
        sheet.Enter("B1", "39448");
        sheet.Enter("B2", "39508");
        sheet.Enter("B3", "39000");
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(1/0,A1:A2,B1:B2)")); // the rate first #DIV/0!, the values first #N/A
        Assert.Equal(Value.FromError(ErrorValue.NA), sheet.Evaluate("=XNPV(0.09,A1:A2,B1:B2)"));
        sheet.Enter("A1", "-10000");
        Assert.Equal(Value.FromError(ErrorValue.Div0), sheet.Evaluate("=XNPV(1/0,A1:A2,B1:B2)"));
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(1/0,A1:A3,B1:B2)")); // the counts first #NUM!
        sheet.Enter("B2", "#N/A");
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(0.09,A1:A3,B1:B3)")); // B3 before the first, #NUM!, beside B2's #N/A
        sheet.Enter("B2", "39508");
        Assert.Equal(Value.FromError(ErrorValue.Num), sheet.Evaluate("=XNPV(0.09,A1:A3,B1:B3)"));
        sheet.Enter("A1", "#N/A");
        sheet.Enter("A2", "#DIV/0!");
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(0.09,A1:A2,B1:B2)")); // the first of the range #N/A, the last #DIV/0!
        sheet.Enter("A2", "#N/A");
        Assert.Equal(Value.FromError(ErrorValue.NA), sheet.Evaluate("=XNPV(0.09,A1:A2,B1:B2)"));
    }

    [Fact] // ADR-0047: XNPV-024 asks: counts that differ give #NUM! (XNPV-003), and beside an Error Value among the values the order would decide
    public void ADR0047_XNPV_refuses_an_Error_Value_beside_counts_that_differ()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "#N/A");
        sheet.Enter("A2", "2750");
        sheet.Enter("A3", "4250");
        sheet.Enter("B1", "39448");
        sheet.Enter("B2", "39508");
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(0.09,A1:A3,B1:B2)"));
        sheet.Enter("A1", "-10000");
        Assert.Equal(Value.FromError(ErrorValue.Num), sheet.Evaluate("=XNPV(0.09,A1:A3,B1:B2)"));
    }

    [Fact] // ADR-0047: XNPV-018 asks: where the last addition nearly cancels, the sum and the sum with Excel's last-addition adjustment part, and both give every observed XNPV double
    public void ADR0047_XNPV_is_refused_where_its_last_addition_nearly_cancels()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1000");
        sheet.Enter("A2", "-1100");
        sheet.Enter("B1", "39448");
        sheet.Enter("B2", "39813");
        Assert.Equal(1.1368683772161603E-13, 1000 + (-1100 / 1.1));
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate("=XNPV(0.1,A1:A2,B1:B2)"));
        sheet.Enter("A2", "-1000");
        Assert.Equal(1000 + (-1000 / 1.1), sheet.Evaluate("=XNPV(0.1,A1:A2,B1:B2)").Number);
    }

    [Theory] // ADR-0202, note of 2026-10-12: NPV is undeclared for 0.1.0, #NAME? as IRR is; XNPV stays
    [InlineData("=NPV(10%,-10000,3000,4200,6800)")]
    [InlineData("=NPV(-1,1,2)")]
    public void ADR0202_NPV_is_refused_as_IRR_is(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Name), NewSheet().Evaluate(formula));

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

    /// <summary><paramref name="value"/> to a whole power by squaring from the lowest bit, each product a double, as PV and FV raise it.</summary>
    private static double RaisedBySquaring(double value, int exponent)
    {
        var result = 1.0;
        var square = value;
        while (exponent > 0)
        {
            if ((exponent & 1) != 0) result *= square;
            exponent >>= 1;
            if (exponent > 0) square *= square;
        }
        return result;
    }

    /// <summary>
    /// The double nearest the square root of <paramref name="value"/> to the power
    /// <paramref name="exponent"/>, computed exactly: the value's significand raised in whole numbers,
    /// its square root taken to the floor, and that rounded once to 53 bits, half to even. For a normal
    /// positive value.
    /// </summary>
    private static double NearestSquareRootOfPower(double value, int exponent)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var significand = new BigInteger((bits & 0xFFFFFFFFFFFFFL) | (1L << 52));
        var twos = ((int)((bits >> 52) & 0x7FF) - 1075) * exponent; // value^exponent = significand^exponent * 2^twos
        var shift = 128 + (Math.Abs(twos) % 2);                       // twos - shift is even
        var radicand = BigInteger.Pow(significand, exponent) << shift;
        var root = FloorSquareRoot(radicand);
        var dropped = (int)root.GetBitLength() - 53;
        var kept = root >> dropped;
        var rest = root - (kept << dropped);
        var half = BigInteger.One << (dropped - 1);
        var exact = root * root == radicand;
        if (rest > half || (rest == half && (!exact || !kept.IsEven))) kept += 1;
        return Math.ScaleB((double)kept, dropped + ((twos - shift) / 2));
    }

    private static BigInteger FloorSquareRoot(BigInteger n)
    {
        var x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            var y = (x + (n / x)) >> 1;
            if (y >= x) return x;
            x = y;
        }
    }
}
