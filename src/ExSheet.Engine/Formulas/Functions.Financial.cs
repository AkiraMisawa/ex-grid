namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT, PV, FV and XNPV, each to Microsoft's documentation of it: the annuity identity
/// <c>pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0</c>, and its form at a
/// rate of 0. PV and FV are the one evaluation of those searched that gives every double, and every Error
/// Value, Excel was observed to give for them, with the exponential XNPV-013 found (<see cref="TryRaise"/>;
/// the seventeenth Windows run and the two area passes of 2026-10-12,
/// <c>spikes/exsheet-financial-evaluation/</c>). XNPV is one of the two evaluations that give its nine
/// observed doubles, with a check of the rate that no evaluation gives (<see cref="XNpv"/>). PMT is kept by
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
    /// leave other readings (PV-014, PV-015), and where a correctly rounded logarithm parts from glibc's
    /// (FV-023, PV-016), it is the engine's reading, which those cases ask.
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
    /// below the correctly rounded exponential, and Excel's double is the correctly rounded one's. No
    /// observed double tells a correctly rounded logarithm from glibc's.</item>
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
    /// is <c>#NUM!</c> (PMT-005), but at the start (below). At a rate above -1 other than 0, <c>#VALUE!</c> where
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
    /// 023 to 032, by 1 to 11 units in the last place (PMT-026, a relative difference of about 1.3e-15). At
    /// PMT-013 and PMT-026, payments at the end, Excel's double and the exact payment rounded once, the
    /// engine's, differ in the 15th significant digit, which General shows (2026-10-13, decided with the user:
    /// the exception holds there too); elsewhere they agree to 15 digits. 0.1.0 lists PMT's last digits under
    /// Known limitations (ADR-0202).</para>
    /// <para>The readings of PMT are the evaluations the observed payments leave: PMT-017, at 10^-7, rules out
    /// every one that rounds 1+rate to a double (millions of units in the last place from Excel's payments),
    /// and leaves those that carry (1+rate)^nper - 1 accurately, through ln(1+rate) (log1p and expm1 in
    /// doubles, an x87's FYL2XP1 and F2XM1) or by a power carried exactly, in the forms of the identity that
    /// come near, in doubles or in an x87's register, and LibreOffice's ScGetPMT, which reaches the payment at
    /// the start through e^((nper+1) ln(1+rate)) - 1 - rate. The best of each family lies 8 to 21 units in the
    /// last place from Excel's at its farthest observed payment, and the readings are taken as every candidate
    /// within 64 (<c>Search.cs --pmt-spread</c>; the bound is the financial area's choice). Refused with
    /// <c>#VALUE!</c> until Excel is asked, where they part in kind, or beyond the last digits (the financial
    /// area's rules; <c>Search.cs --refusals</c> and <c>--pmt-readings</c> run them with a double's range):</para>
    /// <list type="bullet">
    /// <item>a rate of -1 with an <c>nper</c> above 0, where the forms that multiply by a growth of 0 give -fv
    /// and those that divide by it an Error Value (PMT-034), and with a negative one, where the readings give
    /// <c>#NUM!</c> or a number, and the power PV and FV share 0 to a negative power's <c>#DIV/0!</c>
    /// (PMT-037);</item>
    /// <item>a rate below -1 with a whole <c>nper</c>, where a reading with the growth exact gives a number,
    /// or divides by a growth of exactly 1 (PMT-035), and one through ln(1+rate) an Error Value; and with a
    /// fraction whose reciprocal is an odd whole number, where the power PV and FV share takes the odd root
    /// and gives a number, and every reading through ln(1+rate) an Error Value (PMT-036);</item>
    /// <item>where a quantity some reading computes would pass a double's range or fall below its normal
    /// numbers, so that some give an Error Value or a number of few digits and others the payment
    /// (PMT-038 to PMT-044; <see cref="ExactPayment"/> lists the quantities), and where the growth lies
    /// below 2^-52, which a reading that takes it as 1 + (g-1) holds as 0 and divides by (PMT-045);</item>
    /// <item>no periods at the start, at a rate above -1 other than 0, where LibreOffice's reading divides by
    /// e^(ln(1+rate)) - 1 - rate, 0 or a rounding's remainder, and the others by 0 (PMT-046);</item>
    /// <item>where a reading magnifies its own roundings more than 2^6 times (<see cref="ExactPayment"/>), so
    /// that the readings give 0, numbers of the other sign or of a few significant digits, or an Error Value:
    /// where pv g and fv nearly cancel (=PMT(0.1,2,1000,-1210), PMT-049; =PMT(0.1,1E-17,1000,-1000),
    /// PMT-050), where a growth far below 1 taken as 1 + (g-1) keeps few of its digits (PMT-051), where a
    /// rounding of the exponent moves the payment (PMT-052), and at the start where LibreOffice's divisor
    /// e^((nper+1) ln(1+rate)) - 1 - rate comes out as 0 or far from (1+rate)((1+rate)^nper - 1), at a tiny
    /// <c>nper</c>, whose nper+1 rounds to 1 (PMT-047), or a small one at a rate near -1 (PMT-048), or where
    /// the payment at the end less its rate's share, p - p r/(1+r), cancels, at a rate far above 1
    /// (PMT-053);</item>
    /// <item>where 1536 bits do not decide the double, as at a tie between two doubles that the enclosures
    /// cannot reach exactly.</item>
    /// </list>
    /// <para>At a rate below -1 and an <c>nper</c> that is any other fraction, every reading gives
    /// <c>#NUM!</c>: a negative 1+rate has no such power, as <see cref="PowerError"/> gives for PV and FV,
    /// and no logarithm.</para>
    /// </remarks>
    private static Operand Pmt(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var present, out var future, out var atStart, out var failure)) return failure;
        if (periods == 0) return atStart && rate > -1 && rate != 0 ? call.Evaluator.Refuse() : Operand.Of(ErrorValue.Num);
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + future) / periods));
        if (rate <= -1) return rate < -1 && PowerError(1 + rate, periods) == ErrorValue.Num ? Operand.Of(ErrorValue.Num) : call.Evaluator.Refuse();
        return ExactPayment.Of(rate, periods, present, future, atStart) is { } payment ? Operand.Of(Evaluator.Number(payment)) : call.Evaluator.Refuse();
    }

    /// <summary>
    /// PV: what a series of payments and a future value are worth now. The payment multiplies the
    /// product of 1+rate*type and ((1+rate)^nper-1)/rate, the grouping that gives Excel's doubles:
    /// multiplying the payment by 1+rate*type first is one unit in the last place from Excel's PV-007, and
    /// no evaluation that fuses a product into a multiply-add gives every observed PV double
    /// (<c>Search.cs</c>, V10). 1+rate is raised as FV raises it (<see cref="TryRaise"/>): PV's observed
    /// doubles and Error Values are all FV's power's (PV-005, 009 to 012), and where PV's own observations
    /// leave other readings, at half a period and a negative fraction, PV-014 and PV-015 ask. At a rate of
    /// -1 with an <c>nper</c> above 0 the growth is 0, and PV divides by it: which Error Value Excel gives
    /// there has not been observed, and PV refuses it with <c>#VALUE!</c> (PV-013 asks).
    /// </summary>
    private static Operand Pv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var future, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(future + (payment * periods))));
        if (!TryRaise(1 + rate, periods, out var growth, out var error)) return Operand.Of(error);
        if (1 + rate == 0) return call.Evaluator.Refuse();
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
    /// (XNPV-002), an Error Value among them is that Error Value where no other check gives another answer
    /// (the remarks), and anything else among them is refused with <c>#VALUE!</c>. Each value is divided by
    /// 1+rate raised to its years as PV and FV raise it (<see cref="TryRaise"/>): by squaring at a whole
    /// number of years, and as e^(years ln(1+rate)) with the exponential and the logarithm correctly rounded
    /// otherwise. Of the 2,592 candidates searched
    /// (<c>spikes/exsheet-financial-evaluation/</c>, <c>Search.cs</c>), 4 give all nine of Excel's observed
    /// XNPV doubles, and they are two evaluations: this one, and the same sum with Excel's last-addition
    /// adjustment. The nine are XNPV-005 and 006 at whole years (where e^(x ln b) and <see cref="Math.Pow"/>
    /// give others), XNPV-011 at 101 days (where the days times 1/365 give another), XNPV-013, where the
    /// platform's <see cref="Math.Exp"/> gives one discount a unit in the last place low, and XNPV-001, 004
    /// and 014 to 016.
    /// </summary>
    /// <remarks>
    /// <para>A rate below 0 is <c>#NUM!</c>, a check of the rate made before XNPV evaluates: Excel gave
    /// <c>#NUM!</c> at -0.5 (XNPV-012, XNPV-017), at -1 (XNPV-007, XNPV-008), at -1.5 (XNPV-009) and at -2
    /// (XNPV-010), each with values and dates that are numbers in order, and none of the 4 candidates gives
    /// those six, each giving numbers at some. At a negative rate Excel was not asked, between -0.5 and 0 in
    /// particular, the <c>#NUM!</c> is the engine's answer, which XNPV-020 asks at -0.01. A rate of exactly 0
    /// is refused with <c>#VALUE!</c>: whether Excel's check is "below 0" or "0 or below" has not been
    /// observed (XNPV-019 asks).</para>
    /// <para>The order of XNPV's checks has not been observed: of the rate (an Error Value, or below 0, or 0
    /// under "0 or below"), of the counts (XNPV-003's <c>#NUM!</c>), of a date before the first (XNPV-002's),
    /// and of each cell of the values and of the dates, nor the order in which a range's cells are read. So
    /// XNPV reads every cell of both ranges, and takes the answer of every check that fails: each Error Value
    /// of the rate and of the cells, <c>#VALUE!</c> for a cell that is not a number (the engine's refusal: what
    /// Excel gives for one has not been observed either), and <c>#NUM!</c> for counts that differ, for a date
    /// before the first among the dates that are numbers, the first being one, and for the rate. Where those
    /// are one answer, it is XNPV's; where they are more than one, the answer would be the order's, and XNPV
    /// refuses with <c>#VALUE!</c> (XNPV-021 to XNPV-030 ask): an Error Value at a rate below 0, or of 0, or
    /// beside counts that differ or a date before the first wherever it lies, two different Error Values in
    /// one range or the two, an Error Value beside a cell that is not a number, and a rate that is an Error
    /// Value beside another failure.</para>
    /// <para>Where the last addition nearly cancels, so that Excel's last-addition adjustment
    /// (<see cref="Arithmetic.FinalAdd"/>) would give 0 and the plain sum does not, the two evaluations
    /// part, and XNPV refuses with <c>#VALUE!</c> (XNPV-018 asks).</para>
    /// </remarks>
    private static Operand XNpv(FunctionCall call)
    {
        var answers = new HashSet<ErrorValue>();
        var rateRead = TryNumber(call, 0, out var rate, out var failure);
        if (!rateRead) answers.Add(failure.Scalar!.Value.Error);
        // A cell that is not a number is refused: what Excel gives for one has not been observed.
        var notNumber = false;
        var values = NumbersOf(call, 1, answers, ref notNumber);
        var dates = NumbersOf(call, 2, answers, ref notNumber);
        if (notNumber) return call.Evaluator.Refuse();
        if (CellCount(call.Operand(1)) != CellCount(call.Operand(2))) answers.Add(ErrorValue.Num);
        // A date before the first, among the dates that are numbers, where the first is one.
        if (dates.Count > 0 && dates[0] is { } first && dates.Any(d => d is { } date && Math.Floor(date) < Math.Floor(first))) answers.Add(ErrorValue.Num);
        if (rateRead && rate < 0) answers.Add(ErrorValue.Num);
        if (rateRead && rate == 0)
        {
            // "0 or below" gives #NUM!; "below 0" passes the rate to the other checks, or to the sum.
            if (answers.Count == 0) return call.Evaluator.Refuse();
            answers.Add(ErrorValue.Num);
        }
        if (answers.Count > 0) return answers.Count == 1 ? Operand.Of(answers.Single()) : call.Evaluator.Refuse();
        var onePlusRate = 1 + rate;
        var origin = dates.Count > 0 ? Math.Floor(dates[0]!.Value) : 0;
        var total = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (!TryRaise(onePlusRate, (Math.Floor(dates[i]!.Value) - origin) / 365, out var discount, out var error)) return Operand.Of(error);
            var term = values[i]!.Value / discount;
            if (i == values.Count - 1 && Arithmetic.FinalAdd(total, term) != total + term) return call.Evaluator.Refuse();
            total += term;
        }
        return Operand.Of(Evaluator.Number(total));
    }

    /// <summary>How many cells a range argument holds, whatever they hold.</summary>
    private static long CellCount(Operand operand) => operand.Kind switch
    {
        OperandKind.Column => operand.Column!.Count,
        OperandKind.Array => (long)operand.Array!.Rows * operand.Array.Columns,
        OperandKind.Area => (long)operand.Area.Rows * operand.Area.Columns,
        _ => 1,
    };

    /// <summary>
    /// Every cell of a range, each a number or null: a cell that is an Error Value adds that Error Value to
    /// <paramref name="answers"/>, and a blank or anything else that is not a number sets
    /// <paramref name="notNumber"/>, which XNPV refuses. Every cell is read, so that the answer does not depend
    /// on where the first that fails lies. A range of more than 1,000,000 cells is one null.
    /// </summary>
    private static List<double?> NumbersOf(FunctionCall call, int index, HashSet<ErrorValue> answers, ref bool notNumber)
    {
        var numbers = new List<double?>();
        var operand = call.Operand(index);
        IEnumerable<Value?> cells = operand.Kind switch
        {
            OperandKind.Scalar => [operand.Scalar],
            OperandKind.Column => operand.Column!,
            OperandKind.Array => operand.Array!.All(),
            OperandKind.Area => Cells(operand.Area),
            _ => [null],
        };
        foreach (var cell in cells)
        {
            if (cell is { IsError: true } error) answers.Add(error.Error);
            else if (cell is not { Kind: ValueKind.Number }) notNumber = true;
            numbers.Add(cell is { Kind: ValueKind.Number } number ? number.Number : null);
        }
        return numbers;

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
