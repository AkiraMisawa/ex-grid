using System.Numerics;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 and ADR-0202: PMT, PV and FV give Excel's double, to the bit. The seventeenth Windows run
/// (verification/2026-10-11-windows-17, results-2026-10-11-run17-update-en-US.json) and the area pass
/// of 2026-10-12 (verification/2026-10-12-windows-area, results-2026-10-12-area-com-en-US.json) read
/// every case's double; <c>spikes/exsheet-financial-evaluation/</c> searched the evaluations that give
/// them. PV and FV are the doubles' evaluation of Microsoft's identity, the payment times
/// (1+rate*type)*((g-1)/rate), with 1+rate raised by squaring to a whole number, 1 over that to a
/// negative one, and as the ^ operator raises it otherwise; PMT is the payment at the end carried beyond a
/// double and rounded once, and at the start that double divided by 1+rate, rounded once. NPV is
/// refused for 0.1.0 (ADR-0202, note of 2026-10-12): no evaluation searched gives every double Excel
/// gave for it.
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

    [Theory] // ADR-0047: FV-006, FV-007 and PV-005, the area pass: Excel answers any nper at a rate other than 0
    [InlineData("=FV(0.06/12,10.5,-200)", 2150.588229777517)]
    [InlineData("=FV(0.06/12,-10,-200)", -1946.0823721571962)]
    [InlineData("=PV(0.05,10.5,-100)", 801.7640221710038)]
    public void ADR0047_PV_and_FV_answer_any_number_of_periods_as_Excel_does(string formula, double excel) =>
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

    [Theory] // ADR-0047: a fractional nper in PV and FV is the ^ operator's evaluation, one evaluation for both (Arithmetic.Power)
    [InlineData("0.42", "0.5")]
    [InlineData("0.05", "0.5")]
    [InlineData("0.06/12", "10.5")]
    [InlineData("0.06/12", "-10.5")]
    [InlineData("0.06/12", "10.25")]
    [InlineData("0.05", "10.5")]
    [InlineData("-0.5", "1/3")]
    public void ADR0047_PV_and_FV_raise_to_a_fractional_number_of_periods_as_the_power_operator_does(string rate, string periods)
    {
        Assert.Equal(Number($"=((1+{rate})^({periods})-1)/({rate})"), Number($"=FV({rate},{periods},-1)"));
        Assert.Equal(Number($"=(((1+{rate})^({periods})-1)/({rate}))/(1+{rate})^({periods})"), Number($"=PV({rate},{periods},-1)"));
    }

    [Fact] // ADR-0047: FV-016, half a period is the square root, as =2^0.5 is (ARITH-133); e^(0.5 ln(1+rate)) is one unit in the last place from it, 8 in FV's, and FV-016 asks Excel which
    public void ADR0047_Half_a_period_is_the_square_root_as_the_power_operator_takes_it()
    {
        var bySquareRoot = -(-100 * (1 * ((Math.Sqrt(1.42) - 1) / 0.42)));
        var byLogarithm = -(-100 * (1 * ((Math.Exp(0.5 * Math.Log(1.42)) - 1) / 0.42)));
        Assert.Equal(45.627983043166275, Number("=FV(0.42,0.5,-100)"));
        Assert.Equal(45.627983043166275, bySquareRoot);
        Assert.Equal(45.62798304316633, byLogarithm);
    }

    [Theory] // ADR-0047: at a rate below -1 a fractional nper is refused: ^ gives a negative base an odd root, e^(nper ln(1+rate)) none (FV-017, PV-011 ask)
    [InlineData("=FV(-9,1/3,-1)")]
    [InlineData("=PV(-1.5,0.5,-100)")]
    [InlineData("=FV(-2,10.5,-100)")]
    [InlineData("=PMT(-2,10.5,100)")]
    public void ADR0047_At_a_rate_below_minus_1_a_fractional_number_of_periods_is_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Fact] // ADR-0047: a whole nper at a rate below -1 is still raised by squaring, which does not depend on the base's sign
    public void ADR0047_At_a_rate_below_minus_1_a_whole_number_of_periods_is_answered() =>
        Assert.Equal(1, Number("=FV(-2,3,-1)"));

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

    [Theory] // ADR-0047 (note of 2026-10-12, admitted with some argument values refused): PMT-011 and PMT-012, where Excel answers and no evaluation searched gives its payment
    [InlineData("=PMT(8%/12,10.5,10000)")]
    [InlineData("=PMT(8%/12,-10,10000)")]
    [InlineData("=PMT(0.1,0.5,1000)")]
    [InlineData("=PMT(0.1,-1,1000,0,1)")]
    public void ADR0047_PMT_refuses_a_number_of_periods_that_is_not_a_whole_number_from_0(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: past 2^53 every double is a whole number, and how Excel raises 1+rate to one has not been observed
    [InlineData("=FV(0.06/12,2^53+2,-200)")]
    [InlineData("=PV(0.06/12,-(2^53+2),-200)")]
    [InlineData("=PMT(0.06/12,2^53+2,1000)")]
    public void ADR0047_At_a_rate_other_than_0_a_number_of_periods_past_2_to_the_53_is_refused(string formula) =>
        Assert.Equal(Value.FromError(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Fact] // ADR-0047: XNPV-004, asked to tell e^(x ln(1+rate)) from a correctly rounded power: Excel's is the first, Math.Pow 8 units in the last place away
    public void ADR0047_XNPV_discounts_by_e_to_the_years_times_ln_one_plus_rate()
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

    [Theory] // ADR-0047: XNPV refuses a rate of -1 or below, where e^(x ln(1+rate)) has no answer and Math.Pow, the engine's before, has some (XNPV-007 to 010 ask)
    [InlineData("-1", "39448", "39508")]
    [InlineData("-1", "39448", "39448")]
    [InlineData("-1.5", "39448", "39813")]
    [InlineData("-2", "39448", "39508")]
    public void ADR0047_XNPV_refuses_a_rate_of_minus_1_or_below(string rate, string first, string second)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "-1000");
        sheet.Enter("A2", "300");
        sheet.Enter("B1", first);
        sheet.Enter("B2", second);
        Assert.Equal(Value.FromError(ErrorValue.Value), sheet.Evaluate($"=XNPV({rate},A1:A2,B1:B2)"));
        Assert.Equal(ValueKind.Number, sheet.Evaluate("=XNPV(-0.5,A1:A2,B1:B2)").Kind);
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
