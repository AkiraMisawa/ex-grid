namespace ExSheet.Engine.Formulas;

/// <summary>
/// PMT, PV, FV and NPV, each to Microsoft's documentation of it: the annuity identity
/// <c>pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0</c>, and its form at a
/// rate of 0. PV and FV evaluate it as Excel does, in doubles, raising 1+rate to nper by squaring;
/// PMT carries more than a double's precision, as Excel's does (the seventeenth Windows run,
/// <c>spikes/exsheet-financial-evaluation/</c>).
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>
    /// The annuity's arguments: <c>rate</c>, <c>nper</c>, the third, and the optional fourth (0 when
    /// left out) and <c>type</c>. A <c>type</c> that is a whole number other than 0 is payment at the
    /// start of each period, as Excel reads one (PMT-008: 2 reads as 1); a fractional one is refused
    /// with <c>#VALUE!</c> until Excel is asked how it reads one. At a rate other than 0, an
    /// <c>nper</c> that is not a whole number from 0 to 2^53 is refused with <c>#VALUE!</c>: Excel
    /// raises 1+rate to it by squaring, which needs a whole number, and how it raises to any other
    /// has not been observed.
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
        if (type != Math.Floor(type) || (rate != 0 && !(periods >= 0 && periods <= WholePeriods && periods == Math.Floor(periods))))
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
    /// PMT: the payment that pays <c>pv</c> down to <c>fv</c> over <c>nper</c> periods; <c>nper</c> of 0
    /// is <c>#NUM!</c>. Excel's payment is not the doubles' evaluation of the identity, which loses
    /// the rate's low bits in 1+rate: it is the payment at the end of each period computed beyond a
    /// double's precision and rounded once to a double, and the payment at the start is that double
    /// divided by 1+rate, again rounded once (PMT-001 to 003 and 008). <see cref="Wide"/> carries
    /// both.
    /// </summary>
    private static Operand Pmt(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var present, out var future, out var atStart, out var failure)) return failure;
        if (periods == 0) return Operand.Of(ErrorValue.Num);
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + future) / periods));
        var onePlusRate = Wide.Sum(1, rate);
        var growth = Wide.Raise(onePlusRate, (long)periods);
        var atEnd = (-(rate * ((present * growth) + future)) / (growth - 1)).High;
        return Operand.Of(Evaluator.Number(atStart && double.IsFinite(atEnd) ? (atEnd / onePlusRate).High : atEnd));
    }

    /// <summary>PV: what a series of payments and a future value are worth now.</summary>
    private static Operand Pv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var future, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(future + (payment * periods))));
        var growth = Raise(1 + rate, (long)periods);
        var due = 1 + (rate * (atStart ? 1 : 0));
        return Operand.Of(Evaluator.Number(-(future + (payment * due * ((growth - 1) / rate))) / growth));
    }

    /// <summary>FV: what a present value and a series of payments are worth after <c>nper</c> periods.</summary>
    private static Operand Fv(FunctionCall call)
    {
        if (!TryAnnuity(call, out var rate, out var periods, out var payment, out var present, out var atStart, out var failure)) return failure;
        if (rate == 0) return Operand.Of(Evaluator.Number(-(present + (payment * periods))));
        var growth = Raise(1 + rate, (long)periods);
        var due = 1 + (rate * (atStart ? 1 : 0));
        return Operand.Of(Evaluator.Number(-((present * growth) + (payment * due * ((growth - 1) / rate)))));
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
    /// A number carried as the unevaluated sum of two doubles, about 106 bits, for the payment Excel
    /// computes beyond a double's precision: an x87's 64 bits would serve as well, and the two give the
    /// same double but within about 2^-60 of a rounding boundary. Every operation is the standard
    /// error-free one (Dekker, Knuth; the QD library's accurate forms), so the result is the same on
    /// every platform. <see cref="High"/> is the number rounded to a double.
    /// </summary>
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
    /// NPV: each value discounted by its period, the first one period away; the values are the
    /// numbers SUM takes. A rate of -1 is <c>#DIV/0!</c>.
    /// </summary>
    private static Operand Npv(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var rate, out var failure)) return failure;
        var values = new List<double>();
        if (CollectNumbers(call, values, from: 1) is { } error) return Operand.Of(error);
        if (rate == -1) return Operand.Of(ErrorValue.Div0);
        var total = 0.0;
        for (var i = 0; i < values.Count; i++) total += values[i] / Math.Pow(1 + rate, i + 1);
        return Operand.Of(Evaluator.Number(total));
    }

    /// <summary>
    /// XNPV: each value discounted by the years from the first date, at 365 days a year. The values
    /// and the dates are ranges of the same count, numbers only; a date before the first is
    /// <c>#NUM!</c>; anything else among them is refused with <c>#VALUE!</c>.
    /// </summary>
    private static Operand XNpv(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var rate, out var failure)) return failure;
        if (!TryNumbersOf(call, 1, out var values, out failure)) return failure;
        if (!TryNumbersOf(call, 2, out var dates, out failure)) return failure;
        if (values.Count == 0 || values.Count != dates.Count) return Operand.Of(ErrorValue.Num);
        var first = Math.Floor(dates[0]);
        var total = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            var date = Math.Floor(dates[i]);
            if (date < first) return Operand.Of(ErrorValue.Num);
            total += values[i] / Math.Pow(1 + rate, (date - first) / 365);
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
