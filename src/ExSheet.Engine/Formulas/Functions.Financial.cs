namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT, PV, FV and XNPV, each to Microsoft's documentation of it: the annuity identity
/// <c>pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0</c>, and its form at a
/// rate of 0. PV and FV are the one evaluation of those searched that gives every double, and every Error
/// Value, Excel was observed to give for them, with the exponential XNPV-013 found (<see cref="TryRaise"/>;
/// the seventeenth Windows run and the two area passes of 2026-10-12,
/// <c>spikes/exsheet-financial-evaluation/</c>). XNPV is the one evaluation that gives its ten observed
/// doubles, with a check of the rate that no evaluation gives, and its checks in the orders run 18a's
/// answers leave (<see cref="XNpv"/>). PMT is kept by
/// ADR-0047's exception for PMT alone (decided with the user, 2026-10-12): no evaluation searched gives every
/// payment Excel gave. Where it answers, PMT gives the exact payment rounded once, at the end of each period
/// and at the start (2026-10-13, decided with the user), and Excel's double differs from the engine's by 1 to
/// 11 units in the last place where it differs (<see cref="Pmt"/>). NPV is not declared,
/// and is <c>#NAME?</c> as IRR is: no evaluation searched gives every double Excel gave for it (ADR-0202,
/// note of 2026-10-12).
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The annuity's arguments: <c>rate</c>, <c>nper</c>, the third, and the optional fourth (0 when
    /// left out) and <c>type</c>. <c>type</c> is read as a truth value, as Excel reads it: 0 is
    /// payment at the end of each period, and any other number at the start (PMT-008 to PMT-010:
    /// 2, 0.5 and -1 each give the payment at the start; 0.5 is not truncated to 0). At a rate other
    /// than 0, an <c>nper</c> whose absolute value is a whole number past 2^53 is refused
    /// (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's), as before the area passes: how Excel
    /// raises 1+rate to one has not been observed. Every refusal of PMT, PV, FV and XNPV below is the
    /// Formula's so.
    /// </summary>
    private static bool TryAnnuity(FunctionCall call, out double rate, out double periods, out double third, out double fourth, out bool atStart, out Operand failure)
    {
        periods = third = fourth = 0;
        atStart = false;
        var type = 0.0;
        if (!TryNumber(call, 0, out rate, out failure)) return false;
        if (!TryNumber(call, 1, out periods, out failure)) return false;
        if (!TryNumber(call, 2, out third, out failure)) return false;
        if (call.Has(3) && !TryNumber(call, 3, out fourth, out failure)) return false;
        if (call.Has(4) && !TryNumber(call, 4, out type, out failure)) return false;
        if (rate != 0 && Math.Abs(periods) > WholePeriods)
        {
            failure = call.Evaluator.Refuse();
            return false;
        }
        atStart = type != 0;
        return true;
    }

    /// <summary>The largest <c>nper</c> raised by squaring: 2^53, past which a double holds no odd whole number.</summary>
    private const double WholePeriods = 9007199254740992;

    /// <summary>
    /// <paramref name="onePlusRate"/> raised to <paramref name="periods"/> in PV, FV and XNPV, or the Error
    /// Value instead: the one power of the many searched that gives every double and every Error Value
    /// Excel was observed to give for PV and FV, and every double it gave for XNPV
    /// (<c>spikes/exsheet-financial-evaluation/</c>, <c>Search.cs</c>, g22). Where PV's own observations
    /// left other readings (PV-014, PV-015), and where a correctly rounded logarithm parts from glibc's
    /// (FV-023, PV-016), Excel's doubles are this power's too (the area pass of run 18a).
    /// <list type="bullet">
    /// <item>A whole number from 0 by squaring (<see cref="Raise"/>): FV-001 to 003, 005, 009, 010, 014,
    /// 015 and 019, PV-001, 003, 004, 006 and 007, and XNPV-005 and 006 at whole years, where e^(x ln b)
    /// and <see cref="Math.Pow"/> give other doubles.</item>
    /// <item>A negative number as 1 over the power to its absolute value: FV-007 and PV-009 at -10
    /// (squaring 1/(1+rate) is 19 units in the last place away), and FV-022 at -2.5, where raising to
    /// -2.5 directly is 78 units away.</item>
    /// <item>0.5 by the square root, correctly rounded (FV-016, where e^(0.5 ln(1+rate)) is 8 units away),
    /// as the <c>^</c> operator takes it (ARITH-133).</item>
    /// <item>Any other number as e^(n ln b), the exponential and the logarithm each correctly rounded
    /// (<see cref="CorrectlyRounded"/>): FV-006, 012 and 013 and PV-005, and every discount of XNPV's
    /// observed doubles; at XNPV-013 the platform's <see cref="Math.Exp"/> is one unit in the last place
    /// below the correctly rounded exponential, and Excel's double is the correctly rounded one's. At FV-023
    /// and PV-016, where glibc's logarithm and exponential give other doubles, Excel's are the correctly
    /// rounded ones' (run 18a).</item>
    /// <item>A negative 1+rate to a fraction whose reciprocal is an odd whole number as its odd root,
    /// -e^(n ln(-b)) (FV-017, as the <c>^</c> operator's <c>=(-8)^(1/3)</c>, ARITH-043), and to any other
    /// fraction <c>#NUM!</c> (PV-011).</item>
    /// <item>1+rate of 0: to the power 0 <c>#NUM!</c> (FV-018) and to a negative power <c>#DIV/0!</c>
    /// (FV-020, FV-021, PV-012), as the <c>^</c> operator's <c>=0^0</c> and <c>=0^-1</c> are
    /// (ARITH-041, ARITH-042); to a positive power 0.</item>
    /// </list>
    /// The caller has refused an <paramref name="periods"/> past 2^53.
    /// </summary>
    private static bool TryRaise(double onePlusRate, double periods, out double growth, out ErrorValue error)
    {
        growth = 0;
        error = default;
        if (PowerError(onePlusRate, periods) is { } refused)
        {
            error = refused;
            return false;
        }
        var magnitude = Math.Abs(periods);
        double power;
        if (magnitude == Math.Floor(magnitude)) power = Raise(onePlusRate, (long)magnitude);
        else if (onePlusRate == 0) power = 0;
        else if (onePlusRate > 0) power = magnitude == 0.5 ? Math.Sqrt(onePlusRate) : CorrectlyRounded.Exp(magnitude * CorrectlyRounded.Log(onePlusRate));
        else power = -CorrectlyRounded.Exp(CorrectlyRounded.Log(-onePlusRate) * magnitude);
        growth = periods < 0 ? 1 / power : power;
        return true;
    }

    /// <summary>
    /// The Error Value the financial power gives instead of raising <paramref name="onePlusRate"/> to
    /// <paramref name="periods"/>, or null where it raises it (<see cref="TryRaise"/>): 1+rate of 0 to the
    /// power 0 is <c>#NUM!</c> (FV-018) and to a negative power <c>#DIV/0!</c> (FV-020, FV-021, PV-012); a
    /// negative 1+rate to a fraction is <c>#NUM!</c> (PV-011) but where the reciprocal of the fraction's
    /// absolute value is an odd whole number, which is its odd root (FV-017).
    /// </summary>
    private static ErrorValue? PowerError(double onePlusRate, double periods)
    {
        if (onePlusRate == 0 && periods == 0) return ErrorValue.Num;
        if (onePlusRate == 0 && periods < 0) return ErrorValue.Div0;
        if (onePlusRate >= 0 || periods == Math.Floor(periods)) return null;
        var root = 1 / Math.Abs(periods);
        return double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1 ? null : ErrorValue.Num;
    }

    /// <summary>
    /// PMT: the payment that pays <c>pv</c> down to <c>fv</c> over <c>nper</c> periods; <c>nper</c> of 0
    /// is <c>#NUM!</c>, at the end of each period (PMT-005) and at the start (PMT-046), and a rate of -1 or below
    /// is <c>#NUM!</c> (PMT-034 to PMT-037; the remarks). At a rate above -1 other than 0, <c>#VALUE!</c> where
    /// PMT refuses (below), and where it answers (<see cref="ExactPayment"/>): at the end of each period the exact
    /// payment over Excel's doubles rounded once, held between two bounds whose nearest double is the same, with
    /// 1+rate never rounded and (1+rate)^nper - 1 raised from the rate, so that its relative precision holds
    /// however small the rate; and at the start the exact payment at the start rounded once, held the same way
    /// (2026-10-13, decided with the user: the payment at the start of each period is the exact payment rounded
    /// once too, not the payment at the end's double divided by 1+rate and rounded again, the financial area's
    /// earlier rule, which gave Excel's double at PMT-008 to 010 and 019 and lay a unit from the exact payment
    /// rounded once at PMT-014, 015, 025 and 029). Excel's payment is not the doubles' evaluation of the
    /// identity, which loses the rate's low bits in 1+rate; its PMT-001 to 003, 017, 022 and 033 are the
    /// engine's.
    /// </summary>
    /// <remarks>
    /// <para>This is ADR-0047's exception for PMT alone (decided with the user, 2026-10-12): none of the
    /// evaluations searched (<c>spikes/exsheet-financial-evaluation/</c>, over 50,000) gives every payment
    /// Excel was observed to give. Excel's double differs from the engine's at PMT-008 to 016, 018 to 021 and
    /// 023 to 032, by 1 to 11 units in the last place (PMT-026, a relative difference of about 1.3e-15), and is
    /// the engine's at the other 13 of the 36 observed payments the engine gives. At
    /// PMT-013 and PMT-026, payments at the end, Excel's double and the exact payment rounded once, the
    /// engine's, differ in the 15th significant digit, which General shows (2026-10-13, decided with the user:
    /// the exception holds there too); elsewhere they agree to 15 digits. 0.1.0 lists PMT's last digits under
    /// Known limitations (ADR-0202).</para>
    /// <para><b>A rate of -1 or below is <c>#NUM!</c></b>, whatever <c>nper</c> is (run 18a): Excel gave
    /// <c>#NUM!</c> at -1 with periods after (PMT-034) and before (PMT-037), at -2 with 2 periods (PMT-035) and at
    /// -9 with a third of a period (PMT-036). PMT does not raise 1+rate as PV and FV do: their power takes the
    /// odd root at PMT-036 and gives 0 to a negative power <c>#DIV/0!</c> at PMT-037, and PMT-017, at 10^-7, is
    /// no power of 1+rate rounded to a double. A power through ln(1+rate) has no logarithm of 1+rate at 0 or
    /// below, and gives <c>#NUM!</c> at every such rate. That it is <c>#NUM!</c> at a whole <c>nper</c> other than
    /// 2 below -1, and at a fraction at -1, is the engine's answer by generalisation; no run has asked it.</para>
    /// <para>The readings of PMT were the evaluations the observed payments left: those within 64 units in the
    /// last place of Excel's at every observed payment (<c>Search.cs --pmt-spread</c>; the bound is the financial
    /// area's choice), through ln(1+rate) (log1p and expm1 in doubles, an x87's FYL2XP1 and F2XM1) or by a power
    /// carried exactly, in the forms of the identity that come near, in doubles or in an x87's register, and
    /// LibreOffice's ScGetPMT. Run 18a leaves none: no evaluation searched gives PMT-048, 049, 051 or 052, and
    /// Excel's answers at PMT-048, 049 and 052 lie beyond the last digits of the exact payment (another 10th
    /// significant digit, the other sign, and 0 for -9.3E-299). The refusals below were drawn where those
    /// readings parted; they stand as the engine's rules, and where run 18a read Excel's answer inside one, the
    /// corpus keeps it beside the refusal. Refused with <c>#VALUE!</c>:</para>
    /// <list type="bullet">
    /// <item>where a quantity some reading computed passes a double's range or falls below its normal numbers,
    /// but those Excel was seen to pass while giving the exact payment rounded once (run 18a: PMT-038 to
    /// PMT-041 and PMT-044; <see cref="ExactPayment"/> lists them): as where the growth falls below the normal
    /// numbers, and its reciprocal passes the range (PMT-042, where Excel gave <c>#NUM!</c>, which the exact
    /// payment does not give), or the rate does (PMT-043, which Excel did not take through COM). A growth below
    /// 2^-52 is answered (PMT-045, where Excel gave the exact payment): no evaluation that takes the growth as
    /// 1 + (g-1) and divides by it is Excel's;</item>
    /// <item>where a reading magnifies its own roundings more than 2^6 times (<see cref="ExactPayment"/>; the
    /// bound kept by decision with the user, 2026-10-13), so that the readings give 0, numbers of the other sign
    /// or of a few significant digits, or an Error Value: where pv g and fv nearly cancel (=PMT(0.1,2,1000,-1210),
    /// PMT-049; =PMT(0.1,1E-17,1000,-1000), PMT-050), where a growth far below 1 taken as 1 + (g-1) keeps few of
    /// its digits (PMT-051), where a rounding of the exponent moves the payment (PMT-052), and at the start where
    /// LibreOffice's divisor e^((nper+1) ln(1+rate)) - 1 - rate comes out as 0 or far from
    /// (1+rate)((1+rate)^nper - 1), at a tiny <c>nper</c>, whose nper+1 rounds to 1 (PMT-047), or a small one at a
    /// rate near -1 (PMT-048), or where the payment at the end less its rate's share, p - p r/(1+r), cancels, at
    /// a rate far above 1 (PMT-053). Run 18a read Excel's answer at each: the exact payment rounded once at
    /// PMT-050, a unit or two in the last place from it at PMT-047, 051 and 053, and another evaluation's at
    /// PMT-048, 049 and 052; the bound is kept and the eight stay refused (2026-10-16, decided with the user);</item>
    /// <item>where 1536 bits do not decide the double, as at a tie between two doubles that the enclosures
    /// cannot reach exactly.</item>
    /// </list>
    /// </remarks>
    private static Operand Pmt(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var present, out var future, out var atStart, out var failure)) return failure;
        if (periods == 0 || rate <= -1) return Operand.Of(ErrorValue.Num);
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + future) / periods));
        return ExactPayment.Of(rate, periods, present, future, atStart) is { } payment ? Operand.Of(Evaluator.Number(payment)) : call.Evaluator.Refuse();
    }

    /// <summary>
    /// PV: what a series of payments and a future value are worth now. The payment multiplies the
    /// product of 1+rate*type and ((1+rate)^nper-1)/rate, the grouping that gives Excel's doubles:
    /// multiplying the payment by 1+rate*type first is one unit in the last place from Excel's PV-007, and
    /// no evaluation that fuses a product into a multiply-add gives every observed PV double
    /// (<c>Search.cs</c>, V10). 1+rate is raised as FV raises it (<see cref="TryRaise"/>): PV's observed
    /// doubles and Error Values are all FV's power's (PV-005, 009 to 012), and so are PV-014 and PV-015, at half
    /// a period and a negative fraction, where PV's own observations had left other readings (the area pass of
    /// run 18a). PV divides by the growth: at a rate of -1 with an <c>nper</c> above 0 the growth is 0, and PV is
    /// <c>#DIV/0!</c>, as Excel gave at 10 periods (PV-013, run 18a); at another <c>nper</c> above 0 it is the
    /// engine's answer by generalisation. A growth carried past a double's range is left as it was, the doubles'
    /// division (<c>#NUM!</c>), which no run has asked.
    /// </summary>
    private static Operand Pv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var future, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(future + (payment * periods))));
        if (!TryRaise(1 + rate, periods, out var growth, out var error)) return Operand.Of(error);
        if (1 + rate == 0) return Operand.Of(ErrorValue.Div0);
        var due = 1 + (rate * (atStart ? 1 : 0));
        return Operand.Of(Evaluator.Number(-(future + (payment * (due * ((growth - 1) / rate)))) / growth));
    }

    /// <summary>
    /// FV: what a present value and a series of payments are worth after <c>nper</c> periods,
    /// <c>-(pv*g + pmt*((1+rate*type)*((g-1)/rate)))</c> in doubles, g raised by <see cref="TryRaise"/>.
    /// The payment is grouped as in <see cref="Pv"/>: the other grouping is one unit in the last place
    /// from Excel's FV-009. FV-014 and FV-015, a present value and a payment at the start, are the sum
    /// as written, where fusing pv*g or the payment's product into a multiply-add is one unit away; FV-019
    /// is it at the end.
    /// </summary>
    private static Operand Fv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var present, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + (payment * periods))));
        if (!TryRaise(1 + rate, periods, out var growth, out var error)) return Operand.Of(error);
        var due = 1 + (rate * (atStart ? 1 : 0));
        return Operand.Of(Evaluator.Number(-((present * growth) + (payment * (due * ((growth - 1) / rate))))));
    }

    /// <summary>
    /// <paramref name="value"/> to the power <paramref name="exponent"/> as PV, FV and XNPV raise 1+rate to
    /// a whole number: by squaring, from the exponent's lowest bit, each product rounded to a double.
    /// Neither the correctly rounded power, which <see cref="Math.Pow"/> approximates, nor repeated
    /// multiplication gives Excel's FV-001, FV-003, PV-001 and PV-003; this gives each of them to the bit,
    /// and XNPV-006 at whole years.
    /// </summary>
    private static double Raise(double value, long exponent)
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
    /// XNPV: each value discounted by the years from the first date, the days divided by 365, and the
    /// discounted values summed from the first. The values and the dates are ranges of the same count,
    /// numbers only: counts that differ are <c>#NUM!</c> (XNPV-003), a date before the first is <c>#NUM!</c>
    /// (XNPV-002), an Error Value among them is that Error Value, and anything else among them is refused with
    /// <c>#VALUE!</c>; where more than one of those fails, the order of XNPV's checks decides (the remarks). Each
    /// value is divided by 1+rate raised to its years as PV and FV raise it (<see cref="TryRaise"/>): by squaring
    /// at a whole number of years, and as e^(years ln(1+rate)) with the exponential and the logarithm correctly
    /// rounded otherwise. Of the 2,592 candidates searched (<c>spikes/exsheet-financial-evaluation/</c>,
    /// <c>Search.cs</c>), 4 gave all nine of Excel's observed XNPV doubles, and they were two evaluations: this
    /// one, and the same sum with Excel's last-addition adjustment (<see cref="Arithmetic.FinalAdd"/>). XNPV-018,
    /// where the last addition nearly cancels, tells them apart: Excel gave 1.1368683772161603E-13, the plain sum,
    /// where the adjustment gives 0 (run 18a). The nine are XNPV-005 and 006 at whole years (where e^(x ln b) and
    /// <see cref="Math.Pow"/> give others), XNPV-011 at 101 days (where the days times 1/365 give another),
    /// XNPV-013, where the platform's <see cref="Math.Exp"/> gives one discount a unit in the last place low, and
    /// XNPV-001, 004 and 014 to 016.
    /// </summary>
    /// <remarks>
    /// <para>A rate of 0 or below is <c>#NUM!</c>, a check of the rate made before XNPV evaluates: Excel gave
    /// <c>#NUM!</c> at 0 (XNPV-019, run 18a), at -0.01 (XNPV-020), at -0.5 (XNPV-012, XNPV-017), at -1 (XNPV-007,
    /// XNPV-008), at -1.5 (XNPV-009) and at -2 (XNPV-010), each with values and dates that are numbers in order,
    /// and none of the 4 candidates gives those, each giving numbers at some.</para>
    /// <para>The order of XNPV's checks, as run 18a read it (XNPV-021 to XNPV-030): a rate that is an Error Value
    /// is answered before an Error Value among the values (XNPV-027) and before counts that differ (XNPV-028); a
    /// rate of 0 or below before an Error Value or text among the values (XNPV-021 to XNPV-023); counts that
    /// differ before an Error Value among the values (XNPV-024); an Error Value among the dates before one among
    /// the values at the same place (XNPV-026), and before a later date before the first (XNPV-029); an Error
    /// Value among the values before a later date before the first (XNPV-025); and of two Error Values among the
    /// values, the first (XNPV-030). Those answers leave more than one order (<see cref="XnpvOrders"/>): the dates
    /// read before the values range by range, with the rate and the counts checked before them or after the
    /// dates, or the two read pair by pair, a date before its value, with a date before the first checked after
    /// every pair, after its date or after its value. XNPV gives the answer every one of those orders gives, and
    /// refuses with <c>#VALUE!</c> where they part: as where a rate of 0 or below, counts that differ, or a rate
    /// that is another Error Value, meet an Error Value among the dates, where an Error Value among the dates lies after one among the values, or
    /// where a date before the first lies before, or at the place of, an Error Value among the values. A cell
    /// that is not a number (text, a boolean or a blank) is refused where an order reaches it (what Excel gives
    /// for one has not been observed): XNPV-022's text is answered, the rate's <c>#NUM!</c> coming first in every
    /// order.</para>
    /// </remarks>
    private static Operand XNpv(FunctionCall call)
    {
        var rateRead = TryNumber(call, 0, out var rate, out var failure);
        if (!rateRead && failure.Scalar is not { IsError: true, IsRefused: false }) return failure;
        ErrorValue? rateError = rateRead ? null : failure.Scalar!.Value.Error;
        var values = CellsOf(call, 1);
        var dates = CellsOf(call, 2);
        var countsDiffer = CellCount(call.Operand(1)) != CellCount(call.Operand(2));
        var outcome = Checked(XnpvOrders[0], rateError, rate, countsDiffer, values, dates);
        foreach (var order in XnpvOrders.Skip(1))
        {
            if (Checked(order, rateError, rate, countsDiffer, values, dates) != outcome) return call.Evaluator.Refuse();
        }
        if (outcome.Refused) return call.Evaluator.Refuse();
        if (outcome.Error is { } error) return Operand.Of(error);
        var onePlusRate = 1 + rate;
        var origin = Math.Floor(dates[0]!.Value.Number);
        var total = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (!TryRaise(onePlusRate, (Math.Floor(dates[i]!.Value.Number) - origin) / 365, out var discount, out var raised)) return Operand.Of(raised);
            total += values[i]!.Value.Number / discount;
        }
        return Operand.Of(Evaluator.Number(total));
    }

    /// <summary>One check of XNPV's, or a reading of its ranges (<see cref="XnpvOrders"/>).</summary>
    private enum XnpvStep
    {
        /// <summary>The rate: its Error Value, or <c>#NUM!</c> at 0 or below.</summary>
        Rate,

        /// <summary><c>#NUM!</c> where the values and the dates are of different counts.</summary>
        Counts,

        /// <summary>Every date in order: the first Error Value, or a refusal at the first that is not a number.</summary>
        Dates,

        /// <summary>Every value in order, as <see cref="Dates"/>.</summary>
        Values,

        /// <summary><c>#NUM!</c> where a date lies before the first.</summary>
        Order,

        /// <summary>Each date and then its value, as <see cref="Dates"/> and <see cref="Values"/>.</summary>
        Pairs,

        /// <summary>As <see cref="Pairs"/>, with a date before the first checked as soon as it is read.</summary>
        PairsOrderAfterDate,

        /// <summary>As <see cref="Pairs"/>, with a date before the first checked after its value is read.</summary>
        PairsOrderAfterValue,
    }

    /// <summary>
    /// The orders of XNPV's checks that run 18a's answers leave (XNPV-021 to XNPV-030; <see cref="XNpv"/>'s
    /// remarks). Each gives every answer Excel was observed to give; XNPV answers only where they all agree.
    /// </summary>
    private static readonly XnpvStep[][] XnpvOrders =
    [
        [XnpvStep.Rate, XnpvStep.Counts, XnpvStep.Dates, XnpvStep.Values, XnpvStep.Order],
        [XnpvStep.Rate, XnpvStep.Dates, XnpvStep.Counts, XnpvStep.Values, XnpvStep.Order],
        [XnpvStep.Dates, XnpvStep.Rate, XnpvStep.Counts, XnpvStep.Values, XnpvStep.Order],
        [XnpvStep.Rate, XnpvStep.Counts, XnpvStep.Pairs, XnpvStep.Order],
        [XnpvStep.Rate, XnpvStep.Counts, XnpvStep.PairsOrderAfterDate],
        [XnpvStep.Rate, XnpvStep.Counts, XnpvStep.PairsOrderAfterValue],
    ];

    /// <summary>What one order of XNPV's checks answers: an Error Value, a refusal, or neither, the sum.</summary>
    private readonly record struct XnpvOutcome(ErrorValue? Error, bool Refused)
    {
        public static XnpvOutcome Sum => default;
    }

    /// <summary>The answer of XNPV's checks made in <paramref name="order"/>: the first that fails, or the sum.</summary>
    private static XnpvOutcome Checked(XnpvStep[] order, ErrorValue? rateError, double rate, bool countsDiffer, List<Value?> values, List<Value?> dates)
    {
        foreach (var step in order)
        {
            var outcome = step switch
            {
                XnpvStep.Rate => rateError is { } error ? new XnpvOutcome(error, false) : rate <= 0 ? new XnpvOutcome(ErrorValue.Num, false) : (XnpvOutcome?)null,
                XnpvStep.Counts => countsDiffer ? new XnpvOutcome(ErrorValue.Num, false) : null,
                XnpvStep.Dates => dates.Select(Failure).FirstOrDefault(f => f is not null),
                XnpvStep.Values => values.Select(Failure).FirstOrDefault(f => f is not null),
                XnpvStep.Order => Enumerable.Range(1, dates.Count - 1).Any(i => BeforeFirst(dates, i)) ? new XnpvOutcome(ErrorValue.Num, false) : null,
                _ => Paired(step, values, dates),
            };
            if (outcome is { } answer) return answer;
        }
        return XnpvOutcome.Sum;
    }

    /// <summary>The dates and the values read pair by pair, a date before its value, in one of the <see cref="XnpvStep"/>'s pair orders.</summary>
    private static XnpvOutcome? Paired(XnpvStep step, List<Value?> values, List<Value?> dates)
    {
        for (var i = 0; i < Math.Min(values.Count, dates.Count); i++)
        {
            if (Failure(dates[i]) is { } date) return date;
            if (step == XnpvStep.PairsOrderAfterDate && BeforeFirst(dates, i)) return new XnpvOutcome(ErrorValue.Num, false);
            if (Failure(values[i]) is { } value) return value;
            if (step == XnpvStep.PairsOrderAfterValue && BeforeFirst(dates, i)) return new XnpvOutcome(ErrorValue.Num, false);
        }
        return null;
    }

    /// <summary>A cell's failure: its Error Value, a refusal where it is not a number, or null for a number.</summary>
    private static XnpvOutcome? Failure(Value? cell) =>
        cell is { IsError: true } error ? new XnpvOutcome(error.Error, false)
        : cell is { Kind: ValueKind.Number } ? null
        : new XnpvOutcome(null, true);

    /// <summary>Whether date <paramref name="index"/>, read and a number, as the first is, lies before the first's day.</summary>
    private static bool BeforeFirst(List<Value?> dates, int index) =>
        Math.Floor(dates[index]!.Value.Number) < Math.Floor(dates[0]!.Value.Number);

    /// <summary>How many cells a range argument holds, whatever they hold.</summary>
    private static long CellCount(Operand operand) => operand.Kind switch
    {
        OperandKind.Column => operand.Column!.Count,
        OperandKind.Array => (long)operand.Array!.Rows * operand.Array.Columns,
        OperandKind.Area => (long)operand.Area.Rows * operand.Area.Columns,
        _ => 1,
    };

    /// <summary>
    /// Every cell of a range, blank as null, read in order. A range of more than 1,000,000 cells is one null,
    /// which XNPV refuses as a cell that is not a number.
    /// </summary>
    private static List<Value?> CellsOf(FunctionCall call, int index)
    {
        var operand = call.Operand(index);
        IEnumerable<Value?> cells = operand.Kind switch
        {
            OperandKind.Scalar => [operand.Scalar],
            OperandKind.Column => operand.Column!,
            OperandKind.Array => operand.Array!.All(),
            OperandKind.Area => Cells(operand.Area),
            _ => [null],
        };
        return [.. cells];

        IEnumerable<Value?> Cells(Area area)
        {
            if ((long)area.Rows * area.Columns > 1_000_000) yield return null;
            else
            {
                for (var row = area.Row1; row <= area.Row2; row++)
                {
                    for (var column = area.Column1; column <= area.Column2; column++) yield return call.Evaluator.Cells.Read(new CellAddress(row, column));
                }
            }
        }
    }
}
