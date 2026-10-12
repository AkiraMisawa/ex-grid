namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT, PV, FV and XNPV, each to Microsoft's documentation of it: the annuity identity
/// <c>pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0</c>, and its form at a
/// rate of 0. PV and FV evaluate it in doubles by each evaluation that gives every double Excel was
/// observed to give for them, and answer where those agree (<see cref="Growths"/>, <see cref="Agreed"/>);
/// PMT carries more than a double's precision, which gives some of Excel's observed payments but not
/// all (the seventeenth Windows run and the area pass of 2026-10-12,
/// <c>spikes/exsheet-financial-evaluation/</c>). NPV is not declared, and is <c>#NAME?</c> as IRR is:
/// no evaluation searched gives every double Excel gave for it (ADR-0202, note of 2026-10-12).
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The annuity's arguments: <c>rate</c>, <c>nper</c>, the third, and the optional fourth (0 when
    /// left out) and <c>type</c>. <c>type</c> is read as a truth value, as Excel reads it: 0 is
    /// payment at the end of each period, and any other number at the start (PMT-008 to PMT-010:
    /// 2, 0.5 and -1 each give the payment at the start; 0.5 is not truncated to 0). Three kinds of
    /// <c>nper</c> are refused with <c>#VALUE!</c> until Excel is asked. At a rate other than 0, one
    /// whose absolute value is a whole number past 2^53, as before the area pass of 2026-10-12. At a
    /// rate below -1, one that is not a whole number, as before that pass: where 1/nper is an odd
    /// whole number the <c>^</c> operator gives a negative base an odd root (Excel's
    /// <c>=(-8)^(1/3)</c> is -1.9999999999999998, ARITH-043) and e^(nper ln(1+rate)) has none, so the
    /// two part; anywhere else neither has an answer, and the refusal stands not because they part
    /// but because no run has observed what FV and PV give there (FV-017 and PV-011 ask). And at a
    /// rate of -1, a negative one, where 1+rate is 0: squaring and e^(nper ln(1+rate)) leave FV and
    /// PV no finite number (<c>#NUM!</c>), while the <c>^</c> operator gives 0 to a negative power as
    /// <c>#DIV/0!</c> (ARITH-042), so the readings part (FV-020, FV-021 and PV-012 ask).
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
        if ((rate != 0 && Math.Abs(periods) > WholePeriods) || (rate < -1 && periods != Math.Floor(periods)) || (rate == -1 && periods < 0))
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
    /// 1+<paramref name="rate"/> raised to <paramref name="periods"/> by every reading of PV and FV
    /// that Excel's observed doubles leave (<c>spikes/exsheet-financial-evaluation/</c>, <c>Search.cs</c>);
    /// PV and FV answer only where every reading gives them the same double.
    /// A whole number from 0 is reached by squaring (<see cref="Raise"/>), and a negative whole number
    /// is 1 over the power of its absolute value so raised: one reading each, FV-001 to 003, 005, 009
    /// and 010 and PV-001, 003, 004, 006 and 007 for the first, and FV-007 for the second (squaring
    /// 1/(1+rate) instead is 19 units in the last place away). Any other number has more than one.
    /// FV-006's and PV-005's 10.5 are e^(nper ln(1+rate)) by the platform's <see cref="Math.Exp"/> and
    /// <see cref="Math.Log(double)"/> (a correctly rounded power is two units from PV-005), which is
    /// also the <c>^</c> operator's evaluation (<see cref="Arithmetic.Power"/>) but at an exponent of
    /// exactly 0.5, where the operator takes the square root correctly rounded (ARITH-133): no
    /// observed double has an <c>nper</c> of 0.5, so both are readings (FV-016 asks). And a negative
    /// <c>nper</c> may be raised directly, or as 1 over the power to its absolute value, as FV-007's
    /// whole -10 is: no observed double has a negative <c>nper</c> that is not a whole number, so both
    /// are readings, by either evaluation (FV-013 and FV-022 ask). A correctly rounded <c>exp</c> and
    /// <c>log</c> are a reading too, which this does not compute: it gives the platform's doubles at
    /// every PV and FV input <c>Search.cs --refusals</c> draws. <see cref="TryAnnuity"/> refuses a
    /// negative 1+rate before a fractional <c>nper</c>, and 1+rate of 0 before a negative one.
    /// </summary>
    private static double[] Growths(double rate, double periods)
    {
        var onePlusRate = 1 + rate;
        if (periods == Math.Floor(periods)) return [periods >= 0 ? Raise(onePlusRate, (long)periods) : 1 / Raise(onePlusRate, (long)-periods)];
        var byLogarithm = Math.Exp(periods * Math.Log(onePlusRate));
        var asOperator = Arithmetic.Power(onePlusRate, periods) ?? double.NaN;
        if (periods > 0) return [byLogarithm, asOperator];
        var overByLogarithm = 1 / Math.Exp(-periods * Math.Log(onePlusRate));
        var overAsOperator = 1 / (Arithmetic.Power(onePlusRate, -periods) ?? double.NaN);
        return [byLogarithm, asOperator, overByLogarithm, overAsOperator];
    }

    /// <summary>
    /// The double every evaluation of <paramref name="answers"/> gives, or <c>#VALUE!</c> where two
    /// part: two numbers that are not equal, or a number and none (<c>#NUM!</c>). ADR-0047 admits an
    /// answer only where it follows from Excel's observed answers, and where the evaluations those
    /// leave part, no answer does (ADR-0202: the arguments where it disagrees are refused).
    /// </summary>
    private static Operand Agreed(List<double> answers)
    {
        var first = answers[0];
        foreach (var answer in answers)
        {
            if (answer != first && (double.IsFinite(answer) || double.IsFinite(first))) return Operand.Of(ErrorValue.Value);
        }
        return Operand.Of(Evaluator.Number(first));
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
    /// At a rate other than 0, an <c>nper</c> that is not a whole number, or is below 0, is refused
    /// with <c>#VALUE!</c>, although Excel answers it (PMT-011 and PMT-012): no evaluation searched
    /// gives Excel's payment there, nor every other payment Excel was seen to give (ADR-0047, note of
    /// 2026-10-12: admitted with some argument values refused).
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
    /// by 1+rate*type first is one unit in the last place from Excel's PV-007. No evaluation that
    /// fuses a product into a multiply-add gives every observed PV double (<c>Search.cs</c>, V10), so
    /// only 1+rate's readings (<see cref="Growths"/>) part, and PV is <c>#VALUE!</c> where they do.
    /// </summary>
    private static Operand Pv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var future, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(future + (payment * periods))));
        var due = 1 + (rate * (atStart ? 1 : 0));
        var answers = new List<double>();
        foreach (var growth in Growths(rate, periods)) answers.Add(-(future + (payment * (due * ((growth - 1) / rate)))) / growth);
        return Agreed(answers);
    }

    /// <summary>
    /// FV: what a present value and a series of payments are worth after <c>nper</c> periods. The
    /// payment is grouped as in <see cref="Pv"/>: the other grouping is one unit in the last place
    /// from Excel's FV-009. Three evaluations give every observed FV double: that sum, and the two
    /// that fuse one of its products into a multiply-add,
    /// <c>-fma(pv, g, pmt*((1+rate*type)*((g-1)/rate)))</c> and
    /// <c>-fma(pmt, (1+rate*type)*((g-1)/rate), pv*g)</c> (<c>spikes/exsheet-financial-evaluation/</c>,
    /// F4, F9 and F10). They part only where FV has both a present value and a payment; FV-001 and
    /// FV-005 have both, at the start of each period, and are the same double in all three, and no
    /// observed double has both at the end. FV is <c>#VALUE!</c> where any of them, with each of
    /// 1+rate's readings (<see cref="Growths"/>), part (FV-014 and FV-015 ask), but at the end of each
    /// period with a whole <c>nper</c> from 0, where FV gave the unfused sum before the area pass of
    /// 2026-10-12: that answer stands until Excel is asked (FV-019).
    /// </summary>
    private static Operand Fv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var present, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + (payment * periods))));
        var due = 1 + (rate * (atStart ? 1 : 0));
        var fusedToo = atStart || periods < 0 || periods != Math.Floor(periods);
        var answers = new List<double>();
        foreach (var growth in Growths(rate, periods))
        {
            var annuity = due * ((growth - 1) / rate);
            answers.Add(-((present * growth) + (payment * annuity)));
            if (!fusedToo) continue;
            answers.Add(-Math.FusedMultiplyAdd(present, growth, payment * annuity));
            answers.Add(-Math.FusedMultiplyAdd(payment, annuity, present * growth));
        }
        return Agreed(answers);
    }

    /// <summary>
    /// <paramref name="value"/> to the power <paramref name="exponent"/> as Excel raises 1+rate in PV
    /// and FV: by squaring, from the exponent's lowest bit, each product rounded to a double. Neither
    /// the correctly rounded power, which <see cref="Math.Pow"/> approximates, nor repeated
    /// multiplication gives Excel's FV-001, FV-003, PV-001 and PV-003; this gives each of them to the
    /// bit.
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
    /// A number carried as the unevaluated sum of two doubles, about 106 bits, for the payment PMT
    /// computes beyond a double's precision: in effect the exact payment over Excel's doubles, rounded
    /// once. Every operation is the standard error-free one (Dekker, Knuth; the QD library's accurate
    /// forms), so the result is the same on every platform. <see cref="High"/> is the number rounded
    /// to a double.
    /// </summary>
    /// <remarks>
    /// This is a choice among several evaluations that give all four observed payments, not the one
    /// they prove. The spike (<c>spikes/exsheet-financial-evaluation/</c>) found 2504 others that give
    /// them, 304 of them in an x87's 64 bits. Those agree with this one at the rates observed and part
    /// from it as the rate shrinks, because an x87's 1+rate keeps only the rate's bits above 2^-64: at
    /// <c>=PMT(0.045/12,12,250000,-5000)</c> 38 of the 304 are one unit in the last place away, at
    /// <c>=PMT(0.0365/365,365,10000)</c> they give four doubles across 7 units, and at
    /// <c>=PMT(1/10000000,12,1000)</c> nine across 3390, from the 13th significant digit.
    /// The area pass of 2026-10-12 asked Excel. Its PMT-017, at 10^-7, is this one's double, but its
    /// PMT-013 to 016 and 018 are one to three units in the last place from this one's, and none of
    /// the evaluations searched since (<c>search.py</c> and <c>Search.cs</c>, some 18,000) gives every
    /// payment Excel gave. This one stays until the user decides whether PMT is corrected or refused
    /// (ADR-0202).
    /// </remarks>
    private readonly record struct Wide(double High, double Low)
    {
        public static implicit operator Wide(double value) => new(value, 0);

        /// <summary>a + b exactly (Knuth's two-sum).</summary>
        public static Wide Sum(double a, double b)
        {
            var sum = a + b;
            var b2 = sum - a;
            return new(sum, (a - (sum - b2)) + (b - b2));
        }

        /// <summary>a + b exactly, where |a| is at least |b| or a is 0.</summary>
        private static Wide QuickSum(double a, double b)
        {
            var sum = a + b;
            return new(sum, b - (sum - a));
        }

        /// <summary>a * b exactly, the rounding error recovered by a fused multiply-add.</summary>
        private static Wide Product(double a, double b)
        {
            var product = a * b;
            return new(product, Math.FusedMultiplyAdd(a, b, -product));
        }

        public static Wide operator -(Wide a) => new(-a.High, -a.Low);

        public static Wide operator +(Wide a, Wide b)
        {
            var high = Sum(a.High, b.High);
            var low = Sum(a.Low, b.Low);
            var sum = QuickSum(high.High, high.Low + low.High);
            return QuickSum(sum.High, sum.Low + low.Low);
        }

        public static Wide operator -(Wide a, Wide b) => a + -b;

        public static Wide operator *(Wide a, Wide b)
        {
            var product = Product(a.High, b.High);
            return QuickSum(product.High, product.Low + ((a.High * b.Low) + (a.Low * b.High)));
        }

        public static Wide operator /(Wide a, Wide b)
        {
            var first = a.High / b.High;
            var rest = a - (b * first);
            var second = rest.High / b.High;
            rest -= b * second;
            var third = rest.High / b.High;
            return QuickSum(first, second) + third;
        }

        /// <summary><paramref name="value"/> to a whole power, by squaring.</summary>
        public static Wide Raise(Wide value, long exponent)
        {
            Wide result = 1;
            var square = value;
            while (exponent > 0)
            {
                if ((exponent & 1) != 0) result *= square;
                exponent >>= 1;
                if (exponent > 0) square *= square;
            }
            return result;
        }
    }

    /// <summary>
    /// XNPV: each value discounted by the years from the first date, at 365 days a year, and the
    /// discounted values summed from the first. The values and the dates are ranges of the same count,
    /// numbers only; a date before the first is <c>#NUM!</c>; anything else among them is refused with
    /// <c>#VALUE!</c>. Each discount is e^(years ln(1+rate)) in doubles, which, so summed, gives Excel's
    /// XNPV-001 and XNPV-004, where <see cref="Math.Pow"/> so summed is 8 units in the last place from
    /// XNPV-004 (<c>spikes/exsheet-financial-evaluation/</c>). Neither observed double has dates a whole
    /// number of years apart, where squaring (as PV and FV raise to a whole number) and
    /// <see cref="Math.Pow"/> (as the <c>^</c> operator does) are readings too, nor tells the days
    /// divided by 365 from the days multiplied by 1/365, which part at some numbers of days. XNPV is
    /// <c>#VALUE!</c> where those six evaluations part (XNPV-005 and 006 ask whole years, XNPV-011 the
    /// days).
    /// </summary>
    /// <remarks>
    /// <para>A rate of -1 or below is refused with <c>#VALUE!</c>. There 1+rate has no logarithm, so
    /// e^(years ln(1+rate)) has no answer even for the first value, while <see cref="Math.Pow"/>,
    /// which the engine took before, answers the first value, and whole years at a rate below -1
    /// (XNPV-008 and XNPV-009). No run has observed Excel there; XNPV-007 to 010 ask it.</para>
    /// <para>These six are not all the evaluations that give both observed doubles. The search found
    /// others, a correctly rounded power, an x87's 64 bits, a reciprocal, a fused or pairwise sum among
    /// them, which part from these at about half the inputs <c>Search.cs --refusals</c> draws; whether
    /// XNPV answers there is decided with the user (ADR-0202), and XNPV-012 to 017 ask Excel the inputs
    /// that tell them apart.</para>
    /// </remarks>
    private static Operand XNpv(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var rate, out var failure)) return failure;
        if (!TryNumbersOf(call, 1, out var values, out failure)) return failure;
        if (!TryNumbersOf(call, 2, out var dates, out failure)) return failure;
        if (values.Count == 0 || values.Count != dates.Count) return Operand.Of(ErrorValue.Num);
        if (rate <= -1) return Operand.Of(ErrorValue.Value);
        var first = Math.Floor(dates[0]);
        var days = new double[dates.Count];
        for (var i = 0; i < dates.Count; i++)
        {
            days[i] = Math.Floor(dates[i]) - first;
            if (days[i] < 0) return Operand.Of(ErrorValue.Num);
        }
        var onePlusRate = 1 + rate;
        var logarithm = Math.Log(onePlusRate);
        var answers = new List<double>();
        foreach (var byReciprocal in (ReadOnlySpan<bool>)[false, true])
        {
            foreach (var whole in (ReadOnlySpan<int>)[0, 1, 2])
            {
                var total = 0.0;
                for (var i = 0; i < values.Count; i++)
                {
                    var years = byReciprocal ? days[i] * (1.0 / 365) : days[i] / 365;
                    var discount = whole == 0 || years != Math.Floor(years) ? Math.Exp(years * logarithm)
                        : whole == 1 ? Raise(onePlusRate, (long)years)
                        : Math.Pow(onePlusRate, years);
                    total += values[i] / discount;
                }
                answers.Add(total);
            }
        }
        return Agreed(answers);
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
