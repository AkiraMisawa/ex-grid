using System.Numerics;
using ExSheet.Engine.Formulas;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What PV, FV, PMT and XNPV answer is in the shared cases (<c>tests/cases/sheet/financial-evaluation.json</c>). What stays
/// here is how each evaluation was found: the doubles and the exact arithmetic beside the engine's, which no case computes.
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
/// rounded once, with 1+rate raised exactly, and at the start the exact payment at the start rounded once
/// (2026-10-13, decided with the user); 23 of the 36 payments Excel was asked that the engine gives are 1 to 11
/// units in the last place from the engine's. Run 18a answered the cases the engine refused until Excel was
/// asked: PMT is #NUM! at a rate of -1 or below and with no periods, PV #DIV/0! at a growth of 0, and XNPV
/// #NUM! at a rate of 0, its checks in the orders Excel's answers leave. NPV is refused for 0.1.0
/// (ADR-0202, note of 2026-10-12): no evaluation searched gives every double Excel gave for it.
/// </summary>
public class FinancialEvaluationTests
{
    private static double Number(string formula) =>
        NewSheet().Evaluate(formula) is { Kind: ValueKind.Number } v ? v.Number : throw new Xunit.Sdk.XunitException($"{formula} is {NewSheet().Evaluate(formula)}, not a number.");

    [Theory] // ADR-0047: FV-009 and PV-007, the area pass; multiplying the payment by 1+rate first is one unit in the last place below each
    [InlineData("FV", 1849.5026084246633, 1849.502608424663)]
    [InlineData("PV", 1759.484064397425, 1759.4840643974248)]
    public void ADR0047_PV_and_FV_multiply_the_payment_by_the_product_of_one_plus_rate_and_the_annuity_factor(string function, double excel, double paymentTimesDueFirst)
    {
        var rate = 0.05 / 12;
        var growth = RaisedBySquaring(1 + rate, 12);
        var annuity = -150 * (1 + rate) * ((growth - 1) / rate); // the payment times 1+rate first, then the annuity factor
        var dueFirst = function == "FV" ? -annuity : -annuity / growth;
        Assert.Equal(paymentTimesDueFirst, dueFirst);
        Assert.NotEqual(excel, dueFirst);
    }

    [Fact] // ADR-0047: FV-007, a negative nper is 1 over 1+rate raised by squaring; squaring 1/(1+rate) is 19 units in the last place away
    public void ADR0047_A_negative_number_of_periods_is_one_over_the_power_by_squaring()
    {
        var rate = 0.06 / 12;
        var square = (1 + rate) * (1 + rate);
        var growth = 1 / (square * (square * square * (square * square))); // 10 = 2 + 8, from the lowest bit
        var reciprocal = 1 / (1 + rate);
        var reciprocalSquare = reciprocal * reciprocal;
        var reciprocalGrowth = reciprocalSquare * (reciprocalSquare * reciprocalSquare * (reciprocalSquare * reciprocalSquare));
        Assert.Equal(-1946.0823721571962, 200 * ((growth - 1) / rate));
        Assert.Equal(-1946.0823721572006, 200 * ((reciprocalGrowth - 1) / rate));
    }

    [Fact] // ADR-0047: PV-005, a fractional nper is e^(nper ln(1+rate)); a correctly rounded power is two units in the last place from Excel's double
    public void ADR0047_A_fractional_number_of_periods_is_e_to_nper_ln_one_plus_rate()
    {
        var growth = NearestSquareRootOfPower(1.05, 21); // 1.05^10.5, correctly rounded
        var correctlyRounded = -(-100 * (1 * ((growth - 1) / 0.05))) / growth;
        Assert.Equal(801.7640221710036, correctlyRounded);
        Assert.NotEqual(801.7640221710038, correctlyRounded);
    }

    [Theory] // ADR-0047: FV-012, FV-013 and PV-010, the second area pass; a fraction is e^(n ln(1+rate)) with ln and exp correctly rounded, a negative one 1 over the power to its absolute value
    [InlineData(10.25, 2098.0640302830534)]
    [InlineData(-10.5, -2040.8618907559849)]
    public void ADR0047_A_fraction_of_a_period_is_e_to_n_ln_one_plus_rate_correctly_rounded(double periods, double excel)
    {
        var r = 0.06 / 12;
        var power = CorrectlyRounded.Exp(Math.Abs(periods) * CorrectlyRounded.Log(1 + r));
        var growth = periods < 0 ? 1 / power : power;
        Assert.Equal(excel, -(-200 * (1 * ((growth - 1) / r))));
    }

    [Fact] // ADR-0047: FV-016, the second area pass: half a period is the square root of 1+rate, as the ^ operator takes it (ARITH-133), where e^(0.5 ln(1+rate)) is 8 units in the last place away
    public void ADR0047_Half_a_period_is_the_square_root_of_one_plus_rate()
    {
        var byLogarithm = -(-100 * (1 * ((CorrectlyRounded.Exp(0.5 * CorrectlyRounded.Log(1.42)) - 1) / 0.42)));
        Assert.Equal(45.627983043166275, -(-100 * (1 * ((Math.Sqrt(1.42) - 1) / 0.42))));
        Assert.Equal(45.62798304316633, byLogarithm);
    }

    [Fact] // ADR-0047: FV-022, the second area pass: a negative fraction is 1 over the power to its absolute value, as FV-007's -10 is, where raising to -2.5 directly is 78 units in the last place away
    public void ADR0047_A_negative_fraction_is_one_over_the_power_to_its_absolute_value()
    {
        var rate = 0.06 / 12;
        var logarithm = CorrectlyRounded.Log(1 + rate);
        Assert.Equal(-247.8287941856272, 100 * (((1 / CorrectlyRounded.Exp(2.5 * logarithm)) - 1) / rate));
        Assert.Equal(-247.828794185625, 100 * ((CorrectlyRounded.Exp(-2.5 * logarithm) - 1) / rate));
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
    }

    [Theory] // ADR-0047 (2026-10-13, decided with the user): PMT-008, 014, 015, 025 and 029, the second area pass: the payment at the start is not the payment at the end's double over 1+rate rounded again, the earlier rule, which lay a unit in the last place from the exact payment at the start rounded once (Python's decimal) at each of these
    [InlineData("=PMT(0.1,10,1000)", 0.1, -147.95035898410146)]
    [InlineData("=PMT(0.0725/12,12,250000)", 0.0725 / 12, -21530.43001872549)]
    [InlineData("=PMT(0.05/12,12,10000)", 0.05 / 12, -852.5226402170999)]
    [InlineData("=PMT(0.065/12,60,1000,-5000)", 0.065 / 12, 50.90552130119934)]
    [InlineData("=PMT(0.06/52,60,1000,-5000)", 0.06 / 52, 63.19727216775628)]
    public void ADR0047_PMT_at_the_start_is_not_the_payment_at_the_end_rounded_again(string atEnd, double rate, double exactOnce)
    {
        var roundedAgain = Dyadic.QuotientToDouble(Dyadic.Of(Number(atEnd)), Dyadic.One + Dyadic.Of(rate));
        Assert.Equal(1, Math.Abs(BitConverter.DoubleToInt64Bits(roundedAgain) - BitConverter.DoubleToInt64Bits(exactOnce)));
    }

    [Theory] // ADR-0047, the exception for PMT (decided with the user, 2026-10-12): PMT-011 and PMT-012, a fractional and a negative nper, are the exact payment rounded once, each one unit in the last place from Excel's double
    [InlineData("=PMT(8%/12,10.5,10000)", -989.2729401451879)]
    [InlineData("=PMT(8%/12,-10,10000)", 970.3654226924854)]
    public void ADR0047_PMTs_exact_payment_is_one_unit_from_Excels_double_at_a_fractional_or_negative_number_of_periods(string formula, double excel) =>
        Assert.Equal(1, Math.Abs(BitConverter.DoubleToInt64Bits(Number(formula)) - BitConverter.DoubleToInt64Bits(excel)));

    [Fact] // ADR-0047: XNPV-013, at 797 days a correctly rounded exponential gives Excel's discount, where glibc's exp is one unit in the last place below it
    public void ADR0047_XNPV_013s_discount_is_the_correctly_rounded_exponential()
    {
        var exponent = 797.0 / 365 * CorrectlyRounded.Log(1.1);
        Assert.Equal(1.2313555711418425, CorrectlyRounded.Exp(exponent));
        Assert.Equal(CorrectlyRounded.ExpExact(exponent), CorrectlyRounded.Exp(exponent));
    }

    [Fact] // ADR-0047: XNPV-004, Excel's double, is what the engine's e^(x ln(1+rate)) summed from the first value gives (financial-evaluation.json); Math.Pow summed so is 8 units in the last place away
    public void ADR0047_Math_Pow_was_8_units_in_the_last_place_from_XNPV_004()
    {
        double[] values = [-10000, 2750, 2750, 2750, 4250];
        double[] dates = [39448, 39522, 40089, 40217, 40327];
        var sheet = NewSheet();
        for (var i = 0; i < values.Length; i++)
        {
            sheet.Enter($"A{i + 1}", values[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            sheet.Enter($"B{i + 1}", dates[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        var byPow = 0.0;
        for (var i = 0; i < values.Length; i++) byPow += values[i] / Math.Pow(1.12, (dates[i] - dates[0]) / 365);
        var engine = sheet.Evaluate("=XNPV(0.12,A1:A5,B1:B5)").Number;
        Assert.Equal(8, Math.Abs(BitConverter.DoubleToInt64Bits(engine) - BitConverter.DoubleToInt64Bits(byPow)));
    }

    [Fact] // ADR-0047: XNPV-018, run 18a: the engine's XNPV, Excel's double (financial-evaluation.json), is the plain sum from the first value, which nearly cancels, where Excel's last-addition adjustment would give 0
    public void ADR0047_XNPV_018_is_the_plain_sum_from_the_first_value()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1000");
        sheet.Enter("A2", "-1100");
        sheet.Enter("B1", "39448");
        sheet.Enter("B2", "39813");
        Assert.Equal(1000 + (-1100 / 1.1), sheet.Evaluate("=XNPV(0.1,A1:A2,B1:B2)").Number);
    }

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
