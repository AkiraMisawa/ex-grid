namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT, PV, FV and XNPV, each to Microsoft's documentation of it: the annuity identity
/// <c>pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0</c>, and its form at a
/// rate of 0. PV, FV and XNPV are the one evaluation that gives every double, and every Error Value,
/// Excel was observed to give for them (<see cref="TryRaise"/>; the seventeenth Windows run and the two
/// area passes of 2026-10-12, <c>spikes/exsheet-financial-evaluation/</c>). PMT carries more than a
/// double's precision, which gives some of Excel's observed payments but not all: no evaluation searched
/// gives every one. NPV is not declared, and is <c>#NAME?</c> as IRR is: no evaluation searched gives
/// every double Excel gave for it (ADR-0202, note of 2026-10-12).
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The annuity's arguments: <c>rate</c>, <c>nper</c>, the third, and the optional fourth (0 when
    /// left out) and <c>type</c>. <c>type</c> is read as a truth value, as Excel reads it: 0 is
    /// payment at the end of each period, and any other number at the start (PMT-008 to PMT-010:
    /// 2, 0.5 and -1 each give the payment at the start; 0.5 is not truncated to 0). At a rate other
    /// than 0, an <c>nper</c> whose absolute value is a whole number past 2^53 is refused with
    /// <c>#VALUE!</c>, as before the area passes: how Excel raises 1+rate to one has not been observed.
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
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        atStart = type != 0;
        return true;
    }

    /// <summary>The largest <c>nper</c> raised by squaring: 2^53, past which a double holds no odd whole number.</summary>
    private const double WholePeriods = 9007199254740992;

    /// <summary>
    /// <paramref name="onePlusRate"/> raised to <paramref name="periods"/> as Excel raises 1+rate in PV, FV
    /// and XNPV, or the Error Value Excel gives instead: the one evaluation of the many searched that gives
    /// every double and every Error Value Excel was observed to give for the three
    /// (<c>spikes/exsheet-financial-evaluation/</c>, <c>Search.cs</c>, g22).
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
    /// below the correctly rounded exponential, and Excel's double is the correctly rounded one's.</item>
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
        if (onePlusRate == 0 && periods == 0)
        {
            error = ErrorValue.Num;
            return false;
        }
        if (onePlusRate == 0 && periods < 0)
        {
            error = ErrorValue.Div0;
            return false;
        }
        if (periods < 0)
        {
            if (!TryRaise(onePlusRate, -periods, out var power, out error)) return false;
            growth = 1 / power;
            return true;
        }
        if (periods == Math.Floor(periods))
        {
            growth = Raise(onePlusRate, (long)periods);
            return true;
        }
        if (onePlusRate >= 0)
        {
            growth = onePlusRate == 0 ? 0 : periods == 0.5 ? Math.Sqrt(onePlusRate) : CorrectlyRounded.Exp(periods * CorrectlyRounded.Log(onePlusRate));
            return true;
        }
        var root = 1 / periods;
        if (double.IsFinite(root) && root == Math.Floor(root) && Math.Abs(root % 2) == 1)
        {
            growth = -CorrectlyRounded.Exp(CorrectlyRounded.Log(-onePlusRate) * periods);
            return true;
        }
        error = ErrorValue.Num;
        return false;
    }

    /// <summary>
    /// PMT: the payment that pays <c>pv</c> down to <c>fv</c> over <c>nper</c> periods; <c>nper</c> of 0
    /// is <c>#NUM!</c>. Excel's payment is not the doubles' evaluation of the identity, which loses
    /// the rate's low bits in 1+rate: Excel's PMT-001 to 003 and 008 equal the payment at the end of
    /// each period computed beyond a double's precision and rounded once to a double, and the payment
    /// at the start that double divided by 1+rate, again rounded once. <see cref="Wide"/> carries
    /// both, and says which of the evaluations that give those four this one is.
    /// </summary>
    /// <remarks>
    /// <para>At a rate other than 0, an <c>nper</c> that is not a whole number, or is below 0, is refused
    /// with <c>#VALUE!</c>, although Excel answers it (PMT-011 and PMT-012): no evaluation searched
    /// gives Excel's payment there, nor every other payment Excel was seen to give (ADR-0047, note of
    /// 2026-10-12: admitted with some argument values refused).</para>
    /// <para>This is a choice among several evaluations that gave the first four observed payments, not
    /// the one they prove. The area passes of 2026-10-12 asked Excel more. Its PMT-017, at 10^-7, and
    /// PMT-019, 022 and 033 are this one's doubles, but PMT-013 to 016, 018, 020, 021 and 023 to 032 are
    /// one to eleven units in the last place from this one's, and none of the evaluations searched
    /// (<c>search.py</c> and <c>Search.cs</c>, over 50,000) gives every payment Excel gave: the best give
    /// 16 of the 29. This one stays until the user decides whether PMT is corrected or refused
    /// (ADR-0202).</para>
    /// </remarks>
    private static Operand Pmt(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var present, out var future, out var atStart, out var failure)) return failure;
        if (rate != 0 && (periods < 0 || periods != Math.Floor(periods))) return Operand.Of(ErrorValue.Value);
        if (periods == 0) return Operand.Of(ErrorValue.Num);
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + future) / periods));
        var onePlusRate = Wide.Sum(1, rate);
        var growth = Wide.Raise(onePlusRate, (long)periods);
        var atEnd = (-(rate * ((present * growth) + future)) / (growth - 1)).High;
        return Operand.Of(Evaluator.Number(atStart && double.IsFinite(atEnd) ? (atEnd / onePlusRate).High : atEnd));
    }

    /// <summary>
    /// PV: what a series of payments and a future value are worth now. The payment multiplies the
    /// product of 1+rate*type and ((1+rate)^nper-1)/rate, as Excel groups it: multiplying the payment
    /// by 1+rate*type first is one unit in the last place from Excel's PV-007, and no evaluation that
    /// fuses a product into a multiply-add gives every observed PV double (<c>Search.cs</c>, V10).
    /// 1+rate is raised as FV raises it (<see cref="TryRaise"/>): PV's observed doubles and Error Values
    /// are all FV's power's (PV-005, 009 to 012). At a rate of -1 with an <c>nper</c> above 0 the growth is
    /// 0, and PV divides by it: which Error Value Excel gives there has not been observed, and PV refuses
    /// it with <c>#VALUE!</c> (PV-013 asks).
    /// </summary>
    private static Operand Pv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var future, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(future + (payment * periods))));
        if (!TryRaise(1 + rate, periods, out var growth, out var error)) return Operand.Of(error);
        if (1 + rate == 0) return Operand.Of(ErrorValue.Value);
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
    /// <paramref name="value"/> to the power <paramref name="exponent"/> as Excel raises 1+rate to a
    /// whole number in PV, FV and XNPV: by squaring, from the exponent's lowest bit, each product rounded
    /// to a double. Neither the correctly rounded power, which <see cref="Math.Pow"/> approximates, nor
    /// repeated multiplication gives Excel's FV-001, FV-003, PV-001 and PV-003; this gives each of them
    /// to the bit, and XNPV-006 at whole years.
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
    /// numbers only; a date before the first is <c>#NUM!</c>; anything else among them is refused with
    /// <c>#VALUE!</c>. Each value is divided by 1+rate raised to its years as PV and FV raise it
    /// (<see cref="TryRaise"/>): by squaring at a whole number of years, and as e^(years ln(1+rate)) with
    /// the exponential and the logarithm correctly rounded otherwise. Of the 2,592 evaluations searched
    /// (<c>spikes/exsheet-financial-evaluation/</c>, <c>Search.cs</c>), this one and the same sum with
    /// Excel's last-addition adjustment are the only ones that give all nine of Excel's observed XNPV
    /// doubles: XNPV-005 and 006 at whole years (where e^(x ln b) and <see cref="Math.Pow"/> give others),
    /// XNPV-011 at 101 days (where the days times 1/365 give another), and XNPV-013, where the platform's
    /// <see cref="Math.Exp"/> gives one discount a unit in the last place low.
    /// </summary>
    /// <remarks>
    /// <para>A rate below 0 is <c>#NUM!</c>: Excel gave <c>#NUM!</c> at -0.5 (XNPV-012, XNPV-017), at -1
    /// (XNPV-007, XNPV-008), at -1.5 (XNPV-009) and at -2 (XNPV-010), whatever the values and dates, and
    /// a number at every rate above 0 it was asked. A rate of exactly 0 is refused with <c>#VALUE!</c>:
    /// whether Excel's rule is "below 0" or "0 or below" has not been observed (XNPV-019 asks, and
    /// XNPV-020 asks whether a small negative rate is <c>#NUM!</c> as -0.5 is).</para>
    /// <para>Where the last addition nearly cancels, so that Excel's last-addition adjustment
    /// (<see cref="Arithmetic.FinalAdd"/>) would give 0 and the plain sum does not, the two evaluations
    /// part, and XNPV refuses with <c>#VALUE!</c> (XNPV-018 asks).</para>
    /// </remarks>
    private static Operand XNpv(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var rate, out var failure)) return failure;
        if (!TryNumbersOf(call, 1, out var values, out failure)) return failure;
        if (!TryNumbersOf(call, 2, out var dates, out failure)) return failure;
        if (values.Count == 0 || values.Count != dates.Count) return Operand.Of(ErrorValue.Num);
        if (rate < 0) return Operand.Of(ErrorValue.Num);
        if (rate == 0) return Operand.Of(ErrorValue.Value);
        var first = Math.Floor(dates[0]);
        var days = new double[dates.Count];
        for (var i = 0; i < dates.Count; i++)
        {
            days[i] = Math.Floor(dates[i]) - first;
            if (days[i] < 0) return Operand.Of(ErrorValue.Num);
        }
        var onePlusRate = 1 + rate;
        var total = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            if (!TryRaise(onePlusRate, days[i] / 365, out var discount, out var error)) return Operand.Of(error);
            var term = values[i] / discount;
            if (i == values.Count - 1 && Arithmetic.FinalAdd(total, term) != total + term) return Operand.Of(ErrorValue.Value);
            total += term;
        }
        return Operand.Of(Evaluator.Number(total));
    }

    /// <summary>Every cell of a range, each a number: a blank or anything else refuses the whole with <c>#VALUE!</c>; an Error Value is the result.</summary>
    private static bool TryNumbersOf(FunctionCall call, int index, out List<double> numbers, out Operand failure)
    {
        numbers = [];
        failure = default;
        var operand = call.Operand(index);
        if (operand.Kind == OperandKind.Scalar)
        {
            if (operand.Scalar is { IsError: true } error)
            {
                failure = Operand.Of(error);
                return false;
            }
            if (operand.Scalar is { Kind: ValueKind.Number } single)
            {
                numbers.Add(single.Number);
                return true;
            }
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        IEnumerable<Value?> cells = operand.Kind switch
        {
            OperandKind.Column => operand.Column!,
            OperandKind.Array => operand.Array!.All(),
            OperandKind.Area => Cells(operand.Area),
            _ => [null],
        };
        foreach (var cell in cells)
        {
            if (cell is { IsError: true } error)
            {
                failure = Operand.Of(error);
                return false;
            }
            if (cell is not { Kind: ValueKind.Number } number)
            {
                failure = Operand.Of(ErrorValue.Value);
                return false;
            }
            numbers.Add(number.Number);
        }
        return true;

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
